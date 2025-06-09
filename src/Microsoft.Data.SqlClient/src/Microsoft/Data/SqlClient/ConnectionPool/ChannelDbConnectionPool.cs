// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using static Microsoft.Data.SqlClient.ConnectionPool.DbConnectionPoolState;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// A connection pool implementation based on the channel data structure.
    /// Provides methods to manage the pool of connections, including acquiring and releasing connections.
    /// </summary>
    internal sealed class ChannelDbConnectionPool : IDbConnectionPool
    {
        #region Properties
        /// <inheritdoc />
        public ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> AuthenticationContexts => throw new NotImplementedException();

        /// <inheritdoc />
        public DbConnectionFactory ConnectionFactory => _connectionFactory;

        /// <inheritdoc />
        public int Count => _numConnections;

        /// <inheritdoc />
        public bool ErrorOccurred => throw new NotImplementedException();

        /// <inheritdoc />
        public int Id => _objectID;

        /// <inheritdoc />
        public DbConnectionPoolIdentity Identity => _identity;

        /// <inheritdoc />
        public bool IsRunning => State is Running;

        /// <inheritdoc />
        public TimeSpan LoadBalanceTimeout => PoolGroupOptions.LoadBalanceTimeout;

        /// <inheritdoc />
        public DbConnectionPoolGroup PoolGroup => _connectionPoolGroup;

        /// <inheritdoc />
        public DbConnectionPoolGroupOptions PoolGroupOptions => _connectionPoolGroupOptions;

        /// <inheritdoc />
        public DbConnectionPoolProviderInfo ProviderInfo => _connectionPoolProviderInfo;

        /// <inheritdoc />
        public DbConnectionPoolState State
        {
            get;
            set;
        }

        /// <inheritdoc />
        public bool UseLoadBalancing => PoolGroupOptions.UseLoadBalancing;

        private int MaxPoolSize => PoolGroupOptions.MaxPoolSize;
        #endregion

        #region Fields
        private readonly DbConnectionPoolIdentity _identity;

        private readonly DbConnectionFactory _connectionFactory;
        private readonly DbConnectionPoolGroup _connectionPoolGroup;
        private readonly DbConnectionPoolGroupOptions _connectionPoolGroupOptions;
        private DbConnectionPoolProviderInfo _connectionPoolProviderInfo;

        /// <summary>
        /// The private member which carries the set of authenticationcontexts for this pool (based on the user's identity).
        /// </summary>
        private readonly ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> _pooledDbAuthenticationContexts;

        // Prevents synchronous operations which depend on async operations on managed
        // threads from blocking on all available threads, which would stop async tasks
        // from being scheduled and cause deadlocks. Use ProcessorCount/2 as a balance
        // between sync and async tasks.
        private static SemaphoreSlim SyncOverAsyncSemaphore { get; } = new(Math.Max(1, Environment.ProcessorCount / 2));

        private static int _objectTypeCount; // EventSource counter

        private readonly int _objectID = Interlocked.Increment(ref _objectTypeCount);

        /// <summary>
        /// Tracks all connections currently managed by this pool, whether idle or busy.
        /// Only updated rarely - when physical connections are opened/closed - but is read in perf-sensitive contexts.
        /// </summary>
        private readonly DbConnectionInternal?[] _connections;

        /// <summary>
        /// Reader side for the idle connection channel. Contains nulls in order to release waiting attempts after
        /// a connection has been physically closed/broken.
        /// </summary>
        private readonly ChannelReader<DbConnectionInternal?> _idleConnectionReader;
        private readonly ChannelWriter<DbConnectionInternal?> _idleConnectionWriter;

        // Counts the total number of open connections tracked by the pool.
        private volatile int _numConnections;
        #endregion


        /// <summary>
        /// Initializes a new PoolingDataSource.
        /// </summary>
        internal ChannelDbConnectionPool(
            DbConnectionFactory connectionFactory,
            DbConnectionPoolGroup connectionPoolGroup,
            DbConnectionPoolIdentity identity,
            DbConnectionPoolProviderInfo connectionPoolProviderInfo)
        {
            State = Initializing;

            _connectionFactory = connectionFactory;
            _connectionPoolGroup = connectionPoolGroup;
            _connectionPoolGroupOptions = connectionPoolGroup.PoolGroupOptions;
            _connectionPoolProviderInfo = connectionPoolProviderInfo;
            _identity = identity;
            _pooledDbAuthenticationContexts = new ConcurrentDictionary<
                DbConnectionPoolAuthenticationContextKey,
                DbConnectionPoolAuthenticationContext>(
                    concurrencyLevel: 4 * Environment.ProcessorCount,
                    capacity: 2);

            _connections = new DbConnectionInternal[MaxPoolSize];

            // We enforce Max Pool Size, so no need to create a bounded channel (which is less efficient)
            // On the consuming side, we have the multiplexing write loop but also non-multiplexing Rents
            // On the producing side, we have connections being released back into the pool (both multiplexing and not)
            var idleChannel = Channel.CreateUnbounded<DbConnectionInternal?>();
            _idleConnectionReader = idleChannel.Reader;
            _idleConnectionWriter = idleChannel.Writer;

            State = Running;
        }

        #region Methods
        /// <inheritdoc />
        public void Clear()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void PutObjectFromTransactedPool(DbConnectionInternal obj)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public DbConnectionInternal ReplaceConnection(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void ReturnInternalConnection(DbConnectionInternal obj, object owningObject)
        {
            // Once a connection is closing (which is the state that we're in at
            // this point in time) you cannot delegate a transaction to or enlist
            // a transaction in it, so we can correctly presume that if there was
            // not a delegated or enlisted transaction to start with, that there
            // will not be a delegated or enlisted transaction once we leave the
            // lock.

            lock (obj)
            {
                // Calling PrePush prevents the object from being reclaimed
                // once we leave the lock, because it sets _pooledCount such
                // that it won't appear to be out of the pool.  What that
                // means, is that we're now responsible for this connection:
                // it won't get reclaimed if it gets lost.
                obj.PrePush(owningObject);
            }

            if (!CheckConnection(obj))
            {
                return;
            }

            SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.DeactivateObject|RES|CPOOL> {0}, Connection {1}, Deactivating.", Id, obj.ObjectID);
            obj.DeactivateConnection();

            if (obj.IsConnectionDoomed || 
                !obj.CanBePooled || 
                State is ShuttingDown)
            {
                CloseConnection(obj);
            }
            else
            {
                var written = _idleConnectionWriter.TryWrite(obj);
                Debug.Assert(written);
            }
        }

        /// <summary>
        /// Checks that the provided connector is live and unexpired and closes it if needed.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns>Returns true if the connector is live and unexpired, otherwise returns false.</returns>
        private bool CheckConnection(DbConnectionInternal? connection)
        {
            if (connection == null)
            {
                return false;
            }

            if (!connection.IsConnectionAlive())
            {
                CloseConnection(connection);
                return false;
            }

            if (LoadBalanceTimeout != TimeSpan.Zero && DateTime.UtcNow > connection.CreateTime + LoadBalanceTimeout)
            {
                CloseConnection(connection);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Closes the provided connector and adjust pool state accordingly.
        /// </summary>
        /// <param name="connection">The connector to be closed.</param>
        private void CloseConnection(DbConnectionInternal connection)
        {
            connection.Dispose();

            int i;
            for (i = 0; i < MaxPoolSize; i++)
            {
                if (Interlocked.CompareExchange(ref _connections[i], null, connection) == connection)
                {
                    break;
                }
            }

            // If CloseConnection is being called from within OpenNewConnection (e.g. an error happened during a connection initializer which
            // causes the connector to Break, and therefore return the connector), then we haven't yet added the connector to Connections.
            // In this case, there's no state to revert here (that's all taken care of in OpenNewConnection), skip it.
            if (i == MaxPoolSize)
            {
                return;
            }

            var numConnections = Interlocked.Decrement(ref _numConnections);
            Debug.Assert(numConnections >= 0);

            // If a connection has been closed for any reason, we write a null to the idle connection channel to wake up
            // a waiter, who will open a new physical connection
            // Statement order is important since we have synchronous completions on the channel.
            _idleConnectionWriter.TryWrite(null);
        }

        /// <summary>
        /// Sets connection state and activates the connection for use. Should always be called after a connection is created or retrieved from the pool.
        /// </summary>
        /// <param name="owningObject">The owning DbConnection instance.</param>
        /// <param name="obj">The DbConnectionInternal to be activated.</param>
        private void PrepareConnection(DbConnection owningObject, DbConnectionInternal obj)
        {
            lock (obj)
            {   // Protect against Clear and ReclaimEmancipatedObjects, which call IsEmancipated, which is affected by PrePush and PostPop
                obj.PostPop(owningObject);
            }

            try
            {
                //TODO: pass through transaction
                obj.ActivateConnection(null);
            }
            catch
            {
                // if Activate throws an exception
                // put it back in the pool or have it properly disposed of
                this.ReturnInternalConnection(obj, owningObject);
                throw;
            }
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Startup()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool TryGetConnection(DbConnection owningObject, TaskCompletionSource<DbConnectionInternal> taskCompletionSource, DbConnectionOptions userOptions, out DbConnectionInternal? connection)
        {
            // If taskCompletionSource is not null, we are in an async context.
            if (taskCompletionSource is not null)
            {
                /* This is ugly, but async anti-patterns above and below us in the stack necessitate a fresh task to be created.
                 * Ideally we would just return the Task from GetInternalConnection and let the caller await it as needed,
                 * but instead we need to signal to the provided TaskCompletionSource when the connection is established.
                 * This pattern has implications for connection open retry logic that are intricate enough to merit
                 * dedicated work. For now, callers that need to open many connections asynchronously and in parallel *must*
                 * pre-prevision threads in the managed thread pool to avoid exhaustion and timeouts.
                 * 
                 * Also note that we don't have access to the cancellation token passed by the caller to the original OpenAsync call.
                 * This means that we cannot cancel the connection open operation if the caller's token is cancelled. We can only cancel
                 * based on our own timeout, which is set to the owningObject's ConnectionTimeout. Some 
                 */
                Task.Run(async () =>
                {
                    if (taskCompletionSource.Task.IsCompleted)                     {
                        // If the task is already completed, we don't need to do anything.
                        return;
                    }

                    // We're potentially on a new thread, so we need to properly set the ambient transaction.
                    // We rely on the caller to capture the ambient transaction in the TaskCompletionSource's AsyncState
                    // so that we can access it here. Read: area for improvement.
                    ADP.SetCurrentTransaction(taskCompletionSource.Task.AsyncState as Transaction);

                    try
                    {
                        var connection = await GetInternalConnection(owningObject, userOptions, TimeSpan.FromSeconds(owningObject.ConnectionTimeout), true, CancellationToken.None).ConfigureAwait(false);
                        taskCompletionSource.SetResult(connection);
                    }
                    catch (Exception e)
                    {
                        taskCompletionSource.SetException(e);
                    }
                });
                connection = null;
                return false;
            }
            else
            {
                var task = GetInternalConnection(owningObject, userOptions, TimeSpan.FromSeconds(owningObject.ConnectionTimeout), false, CancellationToken.None);

                // When running synchronously, we are guaranteed that the task is already completed.
                // We don't need to guard the managed threadpool at this spot because we pass the async flag as false
                // to GetInternalConnection, which means it will not use Task.Run or any async-await logic that would
                // schedule tasks on the managed threadpool.
                connection = task.ConfigureAwait(false).GetAwaiter().GetResult();
                return connection is not null;
            }
        }

        private async Task<DbConnectionInternal> GetInternalConnection(DbConnection owningConnection, DbConnectionOptions userOptions, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            // First pass
            //
            // Try to get an idle connection from the channel or open a new one.
            DbConnectionInternal? connection = null;

            connection ??= GetIdleConnection();

            connection ??= await OpenNewInternalConnection(owningConnection, userOptions, timeout, async, cancellationToken).ConfigureAwait(false);

            if (connection != null)
            {
                PrepareConnection(owningConnection, connection);
                return connection;
            }

            // Second pass
            //
            // We're at max capacity. Block on the idle channel with a timeout.
            // Note that Channels guarantee fair FIFO behavior to callers of ReadAsync (first-come first-
            // served), which is crucial to us.
            using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken finalToken = linkedSource.Token;
            linkedSource.CancelAfter(timeout);

            try
            {
                // Continue looping around until we create/retrieve a connection or the timeout expires.
                while (true)
                {
                    try
                    {
                        if (async)
                        {
                            connection = await _idleConnectionReader.ReadAsync(finalToken).ConfigureAwait(false);
                        }
                        else
                        {
                            // If there are no connections in the channel, then ReadAsync will block until one is available.
                            // Channels doesn't offer a sync API, so running ReadAsync synchronously on this thread may spawn
                            // additional new async work items in the managed thread pool if there are no items available in the
                            // channel. We need to ensure that we don't block all available managed threads with these child
                            // tasks or we could deadlock. Prefer to block the current user-owned thread, and limit throughput
                            // to the managed threadpool.
                            SyncOverAsyncSemaphore.Wait(finalToken);
                            try
                            {
                                ConfiguredValueTaskAwaitable<DbConnectionInternal?>.ConfiguredValueTaskAwaiter awaiter =
                                    _idleConnectionReader.ReadAsync(finalToken).ConfigureAwait(false).GetAwaiter();
                                using ManualResetEventSlim mres = new ManualResetEventSlim(false, 0);

                                // Cancellation happens through the ReadAsync call, which will complete the task.
                                awaiter.UnsafeOnCompleted(() => mres.Set());
                                mres.Wait(CancellationToken.None);
                                connection = awaiter.GetResult();
                            }
                            finally
                            {
                                SyncOverAsyncSemaphore.Release();
                            }
                        }

                        if (connection != null && CheckConnection(connection))
                        {
                            PrepareConnection(owningConnection, connection);
                            return connection;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Debug.Assert(finalToken.IsCancellationRequested);

                        throw ADP.PooledOpenTimeout();
                    }
                    catch (ChannelClosedException)
                    {
                        //TODO: exceptions from resource file
                        throw new Exception("The connection pool has been shut down.");
                    }

                    // Third pass
                    //
                    // Try again to get an idle connection or open a new one.
                    // If we're here, our waiting attempt on the idle connection channel was released with a null
                    // (or bad connection), or we're in sync mode. Check again if a new idle connection has appeared since we last checked.
                    connection ??= GetIdleConnection();

                    // We might have closed a connection in the meantime and no longer be at max capacity
                    // so try to open a new connection and if that fails, loop again.
                    connection ??= await OpenNewInternalConnection(owningConnection, userOptions, timeout, async, cancellationToken).ConfigureAwait(false);

                    if (connection != null)
                    {
                        PrepareConnection(owningConnection, connection);
                        return connection;
                    }
                }
            }
            finally
            {
                //TODO: log error
            }
        }

        /// <summary>
        /// Tries to read a connection from the idle connection channel.
        /// </summary>
        /// <returns>Returns true if a valid idles connection is found, otherwise returns false.</returns>
        /// TODO: profile the inlining to see if it's necessary (comment copied from npgsql implementation)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DbConnectionInternal? GetIdleConnection()
        {
            while (_idleConnectionReader.TryRead(out DbConnectionInternal? connection))
            {
                if (CheckConnection(connection))
                {
                    return connection;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        internal Task<DbConnectionInternal?> OpenNewInternalConnection(DbConnection? owningConnection, DbConnectionOptions userOptions, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            // As long as we're under max capacity, attempt to increase the connection count and open a new connection.
            for (var numConnections = _numConnections; numConnections < MaxPoolSize; numConnections = _numConnections)
            {
                // Note that we purposefully don't use SpinWait for this: https://github.com/dotnet/coreclr/pull/21437
                if (Interlocked.CompareExchange(ref _numConnections, numConnections + 1, numConnections) != numConnections)
                {
                    continue;
                }

                try
                {
                    // We've managed to increase the open counter, open a physical connection.
                    var startTime = Stopwatch.GetTimestamp();

                    /* TODO: This blocks the thread for several network calls!
                     * When running async, the blocked thread is one allocated from the managed thread pool (due to use of Task.Run above).
                     * This is why it's critical for async callers to pre-provision threads in the managed thread pool.
                     * Our options are limited because DbConnectionInternal doesn't support an async open.
                     * It's better to block this thread and keep throughput high than to queue all of our opens onto a single worker thread.
                     * Add an async path when this support is added to DbConnectionInternal.
                     */
                    DbConnectionInternal? newConnection = ConnectionFactory.CreatePooledConnection(
                        this,
                        owningConnection,
                        _connectionPoolGroup.ConnectionOptions,
                        _connectionPoolGroup.PoolKey,
                        userOptions);

                    if (newConnection == null)
                    {
                        throw ADP.InternalError(ADP.InternalErrorCode.CreateObjectReturnedNull);    // CreateObject succeeded, but null object
                    }
                    if (!newConnection.CanBePooled)
                    {
                        throw ADP.InternalError(ADP.InternalErrorCode.NewObjectCannotBePooled);        // CreateObject succeeded, but non-poolable object
                    }

                    newConnection.PrePush(null);

                    int i;
                    for (i = 0; i < MaxPoolSize; i++)
                    {
                        if (Interlocked.CompareExchange(ref _connections[i], newConnection, null) == null)
                        {
                            break;
                        }
                    }

                    Debug.Assert(i < MaxPoolSize, $"Could not find free slot in {_connections} when opening.");
                    if (i == MaxPoolSize)
                    {
                        //TODO: generic exception?
                        throw new Exception($"Could not find free slot in {_connections} when opening. Please report a bug.");
                    }

                    return Task.FromResult<DbConnectionInternal?>(newConnection);
                }
                catch
                {
                    // Physical open failed, decrement the open and busy counter back down.
                    Interlocked.Decrement(ref _numConnections);

                    // In case there's a waiting attempt on the channel, we write a null to the idle connection channel
                    // to wake it up, so it will try opening (and probably throw immediately)
                    // Statement order is important since we have synchronous completions on the channel.
                    _idleConnectionWriter.TryWrite(null);

                    throw;
                }
            }

            return Task.FromResult<DbConnectionInternal?>(null);
        }
        #endregion
    }
}

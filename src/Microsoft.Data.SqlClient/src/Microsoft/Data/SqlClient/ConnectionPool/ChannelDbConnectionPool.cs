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
        #region Fields
        // Limits synchronous operations which depend on async operations on managed
        // threads from blocking on all available threads, which would stop async tasks
        // from being scheduled and cause deadlocks. Use ProcessorCount/2 as a balance
        // between sync and async tasks.
        private static SemaphoreSlim _syncOverAsyncSemaphore = new(Math.Max(1, Environment.ProcessorCount / 2));

        /// <summary>
        /// Tracks the number of instances of this class. Used to generate unique IDs for each instance.
        /// </summary>
        private static int _instanceCount;

        private readonly int _instanceId = Interlocked.Increment(ref _instanceCount);

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
            ConnectionFactory = connectionFactory;
            PoolGroup = connectionPoolGroup;
            PoolGroupOptions = connectionPoolGroup.PoolGroupOptions;
            ProviderInfo = connectionPoolProviderInfo;
            Identity = identity;
            AuthenticationContexts = new();

            _connections = new DbConnectionInternal[MaxPoolSize];

            // We enforce Max Pool Size, so no need to create a bounded channel (which is less efficient)
            // On the consuming side, we have the multiplexing write loop but also non-multiplexing Rents
            // On the producing side, we have connections being released back into the pool (both multiplexing and not)
            var idleChannel = Channel.CreateUnbounded<DbConnectionInternal?>();
            _idleConnectionReader = idleChannel.Reader;
            _idleConnectionWriter = idleChannel.Writer;

            State = Running;
        }

        #region Properties
        /// <inheritdoc />
        public ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> AuthenticationContexts
        {
            get;
            init;
        }

        /// <inheritdoc />
        public DbConnectionFactory ConnectionFactory
        {
            get;
            init;
        }

        /// <inheritdoc />
        /// Note: maintain _numConnections backing field to enable atomic operations
        public int Count => _numConnections;

        /// <inheritdoc />
        public bool ErrorOccurred => throw new NotImplementedException();

        /// <inheritdoc />
        public int Id => _instanceId;

        /// <inheritdoc />
        public DbConnectionPoolIdentity Identity
        {
            get;
            private set;
        }

        /// <inheritdoc />
        public bool IsRunning => State == Running;

        /// <inheritdoc />
        public TimeSpan LoadBalanceTimeout => PoolGroupOptions.LoadBalanceTimeout;

        /// <inheritdoc />
        public DbConnectionPoolGroup PoolGroup
        {
            get; 
            init;
        }

        /// <inheritdoc />
        public DbConnectionPoolGroupOptions PoolGroupOptions
        {
            get;
            init;
        }

        /// <inheritdoc />
        public DbConnectionPoolProviderInfo ProviderInfo
        {
            get;
            init;
        }

        /// <inheritdoc />
        public DbConnectionPoolState State
        {
            get;
            private set;
        }

        /// <inheritdoc />
        public bool UseLoadBalancing => PoolGroupOptions.UseLoadBalancing;

        private int MaxPoolSize => PoolGroupOptions.MaxPoolSize;
        #endregion

        #region Methods
        /// <inheritdoc />
        public void Clear()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void PutObjectFromTransactedPool(DbConnectionInternal connection)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public DbConnectionInternal ReplaceConnection(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void ReturnInternalConnection(DbConnectionInternal connection, object owningObject)
        {
            lock (connection)
            {
                // Calling PrePush prevents the object from being reclaimed
                // once we leave the lock, because it sets _pooledCount such
                // that it won't appear to be out of the pool.  What that
                // means, is that we're now responsible for this connection:
                // it won't get reclaimed if it gets lost.
                connection.PrePush(owningObject);
            }

            if (!CheckConnection(connection))
            {
                return;
            }

            SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.DeactivateObject|RES|CPOOL> {0}, Connection {1}, Deactivating.", Id, connection.ObjectID);
            connection.DeactivateConnection();

            if (connection.IsConnectionDoomed || 
                !connection.CanBePooled || 
                State == ShuttingDown)
            {
                CloseConnection(connection);
            }
            else
            {
                var written = _idleConnectionWriter.TryWrite(connection);
                Debug.Assert(written, "Failed to write returning connection to the idle channel.");
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
            // If taskCompletionSource is null, we are in a sync context.
            if (taskCompletionSource is null)
            {
                var task = GetInternalConnection(
                        owningObject,
                        userOptions,
                        TimeSpan.FromSeconds(owningObject.ConnectionTimeout),
                        async: false,
                        CancellationToken.None);

                // When running synchronously, we are guaranteed that the task is already completed.
                // We don't need to guard the managed threadpool at this spot because we pass the async flag as false
                // to GetInternalConnection, which means it will not use Task.Run or any async-await logic that would
                // schedule tasks on the managed threadpool.
                connection = task.ConfigureAwait(false).GetAwaiter().GetResult();
                return connection is not null;
            }

            // Early exit if the task is already completed.
            if (taskCompletionSource.Task.IsCompleted)
            {
                connection = null;
                return false;
            }

            /* 
                * This is ugly, but async anti-patterns above and below us in the stack necessitate a fresh task to be
                * created. Ideally we would just return the Task from GetInternalConnection and let the caller await 
                * it as needed, but instead we need to signal to the provided TaskCompletionSource when the connection
                * is established. This pattern has implications for connection open retry logic that are intricate 
                * enough to merit dedicated work. For now, callers that need to open many connections asynchronously 
                * and in parallel *must* pre-prevision threads in the managed thread pool to avoid exhaustion and 
                * timeouts.
                * 
                * Also note that we don't have access to the cancellation token passed by the caller to the original 
                * OpenAsync call. This means that we cannot cancel the connection open operation if the caller's token
                * is cancelled. We can only cancel based on our own timeout, which is set to the owningObject's 
                * ConnectionTimeout. 
                */
            Task.Run(async () =>
            {
                if (taskCompletionSource.Task.IsCompleted)
                {
                    return;
                }

                // We're potentially on a new thread, so we need to properly set the ambient transaction.
                // We rely on the caller to capture the ambient transaction in the TaskCompletionSource's AsyncState
                // so that we can access it here. Read: area for improvement.
                ADP.SetCurrentTransaction(taskCompletionSource.Task.AsyncState as Transaction);

                try
                {
                    var connection = await GetInternalConnection(
                        owningObject, 
                        userOptions, 
                        TimeSpan.FromSeconds(owningObject.ConnectionTimeout), 
                        async: true, 
                        CancellationToken.None
                    ).ConfigureAwait(false);
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

        /// <inheritdoc/>
        internal Task<DbConnectionInternal?> OpenNewInternalConnection(DbConnection? owningConnection, DbConnectionOptions userOptions, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            // As long as we're under max capacity, attempt to increase the connection count and open a new connection.
            for (var numConnections = _numConnections; numConnections < MaxPoolSize; numConnections = _numConnections)
            {
                // Try to reserve a spot in the pool by incrementing _numConnections.
                // If _numConnections changed underneath us, then another thread already reserved a spot.
                // Cycle back through the check above to reset our expected numConnections and to make sure we don't go
                // over MaxPoolSize.
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
                     * When running async, the blocked thread is one allocated from the managed thread pool (due to 
                     * use of Task.Run in TryGetConnection). This is why it's critical for async callers to 
                     * pre-provision threads in the managed thread pool. Our options are limited because 
                     * DbConnectionInternal doesn't support an async open. It's better to block this thread and keep
                     * throughput high than to queue all of our opens onto a single worker thread. Add an async path 
                     * when this support is added to DbConnectionInternal.
                     */
                    DbConnectionInternal? newConnection = ConnectionFactory.CreatePooledConnection(
                        this,
                        owningConnection,
                        PoolGroup.ConnectionOptions,
                        PoolGroup.PoolKey,
                        userOptions);

                    if (newConnection == null)
                    {
                        throw ADP.InternalError(ADP.InternalErrorCode.CreateObjectReturnedNull);
                    }
                    if (!newConnection.CanBePooled)
                    {
                        throw ADP.InternalError(ADP.InternalErrorCode.NewObjectCannotBePooled);
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

        /// <summary>
        /// Checks that the provided connection is live and unexpired and closes it if needed.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns>Returns true if the connection is live and unexpired, otherwise returns false.</returns>
        private bool CheckConnection(DbConnectionInternal connection)
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
        /// Closes the provided connection and removes it from the pool.
        /// </summary>
        /// <param name="connection">The connection to be closed.</param>
        private void CloseConnection(DbConnectionInternal connection)
        {
            connection.Dispose();

            bool found = false;
            for (int i = 0; i < _connections.Length; i++)
            {
                if (Interlocked.CompareExchange(ref _connections[i], null, connection) == connection)
                {
                    found = true;
                    break;
                }
            }

            // If CloseConnection is being called from within OpenNewConnection (e.g. an error happened during a connection initializer which
            // causes the connection to Break, and therefore return the connection), then we haven't yet added the connection to Connections.
            // In this case, there's no state to revert here (that's all taken care of in OpenNewConnection), skip it.
            if (!found)
            {
                return;
            }

            var numConnections = Interlocked.Decrement(ref _numConnections);
            Debug.Assert(numConnections >= 0);

            // This connection was tracked by the pool, so closing it has opened a free spot in the pool.
            // Write a null to the idle connection channel to wake up a waiter, who can now open a new
            // connection. Statement order is important since we have synchronous completions on the channel.
            _idleConnectionWriter.TryWrite(null);
        }

        /// <summary>
        /// Tries to read a connection from the idle connection channel.
        /// </summary>
        /// <returns>Returns true if a valid idle connection is found, otherwise returns false.</returns>
        private DbConnectionInternal? GetIdleConnection()
        {
            // The channel may contain nulls. Read until we find a non-null connection or exhaust the channel.
            while (_idleConnectionReader.TryRead(out DbConnectionInternal? connection))
            {
                if (connection is not null && CheckConnection(connection))
                {
                    return connection;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets an internal connection from the pool, either by retrieving an idle connection or opening a new one.
        /// </summary>
        /// <param name="owningConnection">The DbConnection that will own this internal connection</param>
        /// <param name="userOptions">The user options to set on the internal connection</param>
        /// <param name="timeout">After this TimeSpan, the operation will timeout if a connection is not retrieved.</param>
        /// <param name="async">A boolean indicating whether the operation should be asynchronous.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <returns>Returns a DbConnectionInternal that is retrieved from the pool.</returns>
        private async Task<DbConnectionInternal> GetInternalConnection(DbConnection owningConnection, DbConnectionOptions userOptions, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            // First pass
            //
            // Try to get an idle connection from the channel or open a new one.
            DbConnectionInternal? connection = GetIdleConnection();

            connection ??= await OpenNewInternalConnection(owningConnection, userOptions, timeout, async, cancellationToken).ConfigureAwait(false);

            if (connection is not null)
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
                while (true && !finalToken.IsCancellationRequested)
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
                            _syncOverAsyncSemaphore.Wait(finalToken);
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
                                _syncOverAsyncSemaphore.Release();
                            }
                        }

                        if (connection is not null && CheckConnection(connection))
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

                    if (connection is not null)
                    {
                        PrepareConnection(owningConnection, connection);
                        return connection;
                    }
                }

                throw ADP.PooledOpenTimeout();
            }
            finally
            {
                //TODO: metrics
            }
        }

        /// <summary>
        /// Sets connection state and activates the connection for use. Should always be called after a connection is created or retrieved from the pool.
        /// </summary>
        /// <param name="owningObject">The owning DbConnection instance.</param>
        /// <param name="connection">The DbConnectionInternal to be activated.</param>
        private void PrepareConnection(DbConnection owningObject, DbConnectionInternal connection)
        {
            lock (connection)
            {   
                // Protect against Clear which calls IsEmancipated, which is affected by PrePush and PostPop
                connection.PostPop(owningObject);
            }

            try
            {
                //TODO: pass through transaction
                connection.ActivateConnection(null);
            }
            catch
            {
                // At this point, the connection is "out of the pool" (the call to postpop). If we hit a transient
                // error anywhere along the way when enlisting the connection in the transaction, we need to get
                // the connection back into the pool so that it isn't leaked.
                this.ReturnInternalConnection(connection, owningObject);
                throw;
            }
        }
        #endregion
    }
}

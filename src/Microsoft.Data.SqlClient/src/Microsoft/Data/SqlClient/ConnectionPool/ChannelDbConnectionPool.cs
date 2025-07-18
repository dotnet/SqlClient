// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
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
        private readonly ConnectionPoolSlots _connectionSlots;

        /// <summary>
        /// Reader side for the idle connection channel. Contains nulls in order to release waiting attempts after
        /// a connection has been physically closed/broken.
        /// </summary>
        private readonly ChannelReader<DbConnectionInternal?> _idleConnectionReader;
        private readonly ChannelWriter<DbConnectionInternal?> _idleConnectionWriter;
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
            MaxPoolSize = Convert.ToUInt32(PoolGroupOptions.MaxPoolSize);

            _connectionSlots = new(MaxPoolSize);

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
        public ConcurrentDictionary<
            DbConnectionPoolAuthenticationContextKey, 
            DbConnectionPoolAuthenticationContext> AuthenticationContexts { get; }

        /// <inheritdoc />
        public DbConnectionFactory ConnectionFactory { get; }

        /// <inheritdoc />
        public int Count => _connectionSlots.ReservationCount;

        /// <inheritdoc />
        public bool ErrorOccurred => throw new NotImplementedException();

        /// <inheritdoc />
        public int Id => _instanceId;

        /// <inheritdoc />
        public DbConnectionPoolIdentity Identity { get; }

        /// <inheritdoc />
        public bool IsRunning => State == Running;

        /// <inheritdoc />
        public TimeSpan LoadBalanceTimeout => PoolGroupOptions.LoadBalanceTimeout;

        /// <inheritdoc />
        public DbConnectionPoolGroup PoolGroup { get; }

        /// <inheritdoc />
        public DbConnectionPoolGroupOptions PoolGroupOptions { get; }

        /// <inheritdoc />
        public DbConnectionPoolProviderInfo ProviderInfo { get; }

        /// <inheritdoc />
        public DbConnectionPoolState State { get; private set; }

        /// <inheritdoc />
        public bool UseLoadBalancing => PoolGroupOptions.UseLoadBalancing;

        private uint MaxPoolSize { get; }
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
        public DbConnectionInternal ReplaceConnection(
            DbConnection owningObject, 
            DbConnectionOptions userOptions, 
            DbConnectionInternal oldConnection)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void ReturnInternalConnection(DbConnectionInternal connection, DbConnection? owningObject)
        {
            ValidateOwnershipAndSetPoolingState(connection, owningObject);

            if (!IsLiveConnection(connection))
            {
                RemoveConnection(connection);
                return;
            }

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "<prov.DbConnectionPool.DeactivateObject|RES|CPOOL> {0}, Connection {1}, Deactivating.", 
                Id, 
                connection.ObjectID);
            connection.DeactivateConnection();

            if (connection.IsConnectionDoomed || 
                !connection.CanBePooled || 
                State == ShuttingDown)
            {
                RemoveConnection(connection);
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
        public bool TryGetConnection(
            DbConnection owningObject, 
            TaskCompletionSource<DbConnectionInternal> taskCompletionSource,
            DbConnectionOptions userOptions, 
            out DbConnectionInternal? connection)
        {
            var timeout = TimeSpan.FromSeconds(owningObject.ConnectionTimeout);

            // If taskCompletionSource is null, we are in a sync context.
            if (taskCompletionSource is null)
            {
                var task = GetInternalConnection(
                        owningObject,
                        userOptions,
                        async: false,
                        timeout);

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
 
            // This is ugly, but async anti-patterns above and below us in the stack necessitate a fresh task to be
            // created. Ideally we would just return the Task from GetInternalConnection and let the caller await 
            // it as needed, but instead we need to signal to the provided TaskCompletionSource when the connection
            // is established. This pattern has implications for connection open retry logic that are intricate 
            // enough to merit dedicated work. For now, callers that need to open many connections asynchronously 
            // and in parallel *must* pre-prevision threads in the managed thread pool to avoid exhaustion and 
            // timeouts.
            // 
            // Also note that we don't have access to the cancellation token passed by the caller to the original 
            // OpenAsync call. This means that we cannot cancel the connection open operation if the caller's token
            // is cancelled. We can only cancel based on our own timeout, which is set to the owningObject's 
            // ConnectionTimeout. 
            Task.Run(async () =>
            {
                if (taskCompletionSource.Task.IsCompleted)
                {
                    return;
                }

                // We're potentially on a new thread, so we need to properly set the ambient transaction.
                // We rely on the caller to capture the ambient transaction in the TaskCompletionSource's AsyncState
                // so that we can access it here. Read: area for improvement.
                // TODO: ADP.SetCurrentTransaction(taskCompletionSource.Task.AsyncState as Transaction);
                DbConnectionInternal? connection = null;

                try
                {
                    connection = await GetInternalConnection(
                        owningObject,
                        userOptions,
                        async: true,
                        timeout
                    ).ConfigureAwait(false);

                    if (!taskCompletionSource.TrySetResult(connection))
                    {
                        // We were able to get a connection, but the task was cancelled out from under us.
                        // This can happen if the caller's CancellationToken is cancelled while we're waiting for a connection.
                        // Check the success to avoid an unnecessary exception.
                        ReturnInternalConnection(connection, owningObject);
                    }
                }
                catch (Exception e)
                {
                    if (connection != null)
                    {
                        ReturnInternalConnection(connection, owningObject);
                    }

                    // It's possible to fail to set an exception on the TaskCompletionSource if the task is already
                    // completed. In that case, this exception will be swallowed because nobody directly awaits this
                    // task.
                    taskCompletionSource.TrySetException(e);
                }
            });

            connection = null;
            return false;
        }

        private struct CreateState
        {
            internal ChannelDbConnectionPool pool;
            internal DbConnection? owningConnection;
            internal DbConnectionOptions userOptions;
        }

        /// <summary>
        /// Opens a new internal connection to the database.
        /// </summary>
        /// <param name="owningConnection">The owning connection.</param>
        /// <param name="userOptions">The options for the connection.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, with a result of the new internal connection.</returns>
        /// <throws>InvalidOperationException - when the newly created connection is invalid or already in the pool.</throws>
        private DbConnectionInternal? OpenNewInternalConnection(
            DbConnection? owningConnection, 
            DbConnectionOptions userOptions, 
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Opening a connection can be a slow operation and we don't want to hold a lock for the duration.
            // Instead, we reserve a connection slot prior to attempting to open a new connection and release the slot
            // in case of an exception.

            return _connectionSlots.Add(
                createCallback: static (state) =>
                {
                    // https://github.com/dotnet/SqlClient/issues/3459
                    // TODO: This blocks the thread for several network calls!
                    // When running async, the blocked thread is one allocated from the managed thread pool (due to 
                    // use of Task.Run in TryGetConnection). This is why it's critical for async callers to 
                    // pre-provision threads in the managed thread pool. Our options are limited because 
                    // DbConnectionInternal doesn't support an async open. It's better to block this thread and keep
                    // throughput high than to queue all of our opens onto a single worker thread. Add an async path 
                    // when this support is added to DbConnectionInternal.
                    return state.pool.ConnectionFactory.CreatePooledConnection(
                        state.pool,
                        state.owningConnection,
                        state.pool.PoolGroup.ConnectionOptions,
                        state.pool.PoolGroup.PoolKey,
                        state.userOptions);
                },
                cleanupCallback: static (newConnection, idleConnectionWriter) =>
                {
                    idleConnectionWriter.TryWrite(null);
                    newConnection?.Dispose();
                },
                new CreateState
                {
                    pool = this,
                    owningConnection = owningConnection,
                    userOptions = userOptions
                },
                _idleConnectionWriter);
        }

        /// <summary>
        /// Checks that the provided connection is live and unexpired and closes it if needed.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns>Returns true if the connection is live and unexpired, otherwise returns false.</returns>
        private bool IsLiveConnection(DbConnectionInternal connection)
        {
            if (!connection.IsConnectionAlive())
            {
                return false;
            }

            if (LoadBalanceTimeout != TimeSpan.Zero && DateTime.UtcNow > connection.CreateTime + LoadBalanceTimeout)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Closes the provided connection and removes it from the pool.
        /// </summary>
        /// <param name="connection">The connection to be closed.</param>
        private void RemoveConnection(DbConnectionInternal connection)
        {
            _connectionSlots.TryRemove(connection);
            
            // Removing a connection from the pool opens a free slot.
            // Write a null to the idle connection channel to wake up a waiter, who can now open a new
            // connection. Statement order is important since we have synchronous completions on the channel.
            _idleConnectionWriter.TryWrite(null);

            connection.Dispose();
        }

        /// <summary>
        /// Tries to read a connection from the idle connection channel.
        /// </summary>
        /// <returns>A connection from the idle channel, or null if the channel is empty.</returns>
        private DbConnectionInternal? GetIdleConnection()
        {
            // The channel may contain nulls. Read until we find a non-null connection or exhaust the channel.
            while (_idleConnectionReader.TryRead(out DbConnectionInternal? connection))
            {
                if (connection is null) 
                {
                    continue;
                }

                if (!IsLiveConnection(connection))
                {
                    RemoveConnection(connection);
                    continue;
                }

                return connection;
            }

            return null;
        }

        /// <summary>
        /// Gets an internal connection from the pool, either by retrieving an idle connection or opening a new one.
        /// </summary>
        /// <param name="owningConnection">The DbConnection that will own this internal connection</param>
        /// <param name="userOptions">The user options to set on the internal connection</param>
        /// <param name="async">A boolean indicating whether the operation should be asynchronous.</param>
        /// <param name="timeout">The timeout for the operation.</param>
        /// <returns>Returns a DbConnectionInternal that is retrieved from the pool.</returns>
        private async Task<DbConnectionInternal> GetInternalConnection(
            DbConnection owningConnection, 
            DbConnectionOptions userOptions, 
            bool async, 
            TimeSpan timeout)
        {
            DbConnectionInternal? connection = null;
            using CancellationTokenSource cancellationTokenSource = new(timeout);
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            // Continue looping until we create or retrieve a connection
            do
            {
                try
                {
                    // Optimistically try to get an idle connection from the channel
                    // Doesn't wait if the channel is empty, just returns null.
                    connection ??= GetIdleConnection();


                    // If we didn't find an idle connection, try to open a new one.  
                    connection ??= OpenNewInternalConnection(
                        owningConnection,
                        userOptions,
                        cancellationToken);

                    // If we're at max capacity and couldn't open a connection. Block on the idle channel with a
                    // timeout. Note that Channels guarantee fair FIFO behavior to callers of ReadAsync
                    // (first-come, first-served), which is crucial to us.
                    if (async)
                    {
                        connection ??= await _idleConnectionReader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        connection ??= ReadChannelSyncOverAsync(cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw ADP.PooledOpenTimeout();
                }
                catch (ChannelClosedException)
                {
                    //TODO: exceptions from resource file
                    throw new Exception("The connection pool has been shut down.");
                }

                if (connection is not null && !IsLiveConnection(connection))
                {
                    // If the connection is not live, we need to remove it from the pool and try again.
                    RemoveConnection(connection);
                    connection = null;
                }
            }
            while (connection is null);

            PrepareConnection(owningConnection, connection);
            return connection;
        }

        /// <summary>
        /// Performs a blocking synchronous read from the idle connection channel.
        /// </summary>
        /// <param name="cancellationToken">Cancels the read operation.</param>
        /// <returns>The connection read from the channel.</returns>
        private DbConnectionInternal? ReadChannelSyncOverAsync(CancellationToken cancellationToken)
        {
            // If there are no connections in the channel, then ReadAsync will block until one is available.
            // Channels doesn't offer a sync API, so running ReadAsync synchronously on this thread may spawn
            // additional new async work items in the managed thread pool if there are no items available in the
            // channel. We need to ensure that we don't block all available managed threads with these child
            // tasks or we could deadlock. Prefer to block the current user-owned thread, and limit throughput
            // to the managed threadpool.

            _syncOverAsyncSemaphore.Wait(cancellationToken);
            try
            {
                ConfiguredValueTaskAwaitable<DbConnectionInternal?>.ConfiguredValueTaskAwaiter awaiter =
                    _idleConnectionReader.ReadAsync(cancellationToken).ConfigureAwait(false).GetAwaiter();
                using ManualResetEventSlim mres = new ManualResetEventSlim(false, 0);

                // Cancellation happens through the ReadAsync call, which will complete the task.
                // Even a failed task will complete and set the ManualResetEventSlim.
                awaiter.UnsafeOnCompleted(() => mres.Set());
                mres.Wait(CancellationToken.None);
                return awaiter.GetResult();
            }
            finally
            {
                _syncOverAsyncSemaphore.Release();
            }
        }

        /// <summary>
        /// Sets connection state and activates the connection for use. Should always be called after a connection is 
        /// created or retrieved from the pool.
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
                ReturnInternalConnection(connection, owningObject);
                throw;
            }
        }

        /// <summary>
        /// Validates that the connection is owned by the provided DbConnection and that it is in a valid state to be returned to the pool.
        /// </summary>
        /// <param name="owningObject">The owning DbConnection instance.</param>
        /// <param name="connection">The DbConnectionInternal to be validated.</param>
        private void ValidateOwnershipAndSetPoolingState(DbConnectionInternal connection, DbConnection? owningObject)
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
        }
        #endregion
    }
}

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
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using static Microsoft.Data.SqlClient.ConnectionPool.DbConnectionPoolState;
using Microsoft.Data.SqlClient.Internal;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// A connection pool implementation based on the channel data structure.
    /// Provides methods to manage the pool of connections, including acquiring and releasing connections.
    ///
    /// This implementation uses <see cref="System.Threading.Channels.Channel{T}"/> for managing idle connections,
    /// which offers several advantages over the traditional <c>WaitHandleDbConnectionPool</c>:
    ///
    /// <list type="bullet">
    /// <item><description>
    /// <strong>Better async performance:</strong> Channels provide native async/await support without blocking
    /// threads, unlike wait handles which can block managed threads and potentially cause thread pool starvation.
    /// </description></item>
    /// <item><description>
    /// <strong>FIFO fairness:</strong> Channels guarantee first-come, first-served ordering for connection requests,
    /// ensuring fair access to connections under high contention scenarios.
    /// </description></item>
    /// <item><description>
    /// <strong>Reduced lock contention:</strong> The channel-based approach minimizes lock usage compared to
    /// traditional synchronization primitives, improving scalability under concurrent load.
    /// </description></item>
    /// <item><description>
    /// <strong>Simplified state management:</strong> Eliminates complex wait handle coordination and reduces
    /// the potential for race conditions in connection lifecycle management.
    /// </description></item>
    /// </list>
    ///
    /// The trade-off is slightly higher memory overhead per pool instance due to the channel infrastructure,
    /// but this is generally offset by the performance benefits in async-heavy workloads.
    /// </summary>
    internal sealed class ChannelDbConnectionPool : IDbConnectionPool, IDisposable
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
        /// The idle connection channel. Contains nulls in order to release waiting attempts after
        /// a connection has been physically closed/broken. Also tracks the count of non-null idle connections.
        /// </summary>
        private readonly IdleConnectionChannel _idleChannel;

        /// <summary>
        /// The current generation of the pool. Incremented atomically on each <see cref="Clear"/> call.
        /// Connections stamped with a generation that does not match are considered stale and are destroyed
        /// rather than returned to the idle channel.
        /// Must be updated using <see cref="Interlocked"/> operations to ensure thread safety.
        /// </summary>
        private volatile int _clearGeneration;

        /// <summary>
        /// Guard to prevent concurrent <see cref="Clear"/> operations from draining the idle channel
        /// simultaneously. The generation counter is still incremented by every caller so stale connections
        /// are always caught lazily, but only one thread performs the actual drain.
        /// Must be updated using <see cref="Interlocked"/> operations to ensure thread safety.
        /// </summary>
        private volatile int _isClearing;

        /// <summary>
        /// Optional rate limiter that throttles the number of concurrent physical connection
        /// creation attempts. When null, no rate limiting is applied. A non-null limiter is
        /// supplied at pool construction time; there is no default. Callers fast-fail against
        /// the limiter and fall back to the idle-channel wait when no permit is available.
        /// </summary>
        private readonly RateLimiter? _connectionCreationRateLimiter;

        /// <summary>
        /// Encapsulates the blocking-period error state for this pool: cached exception, exponential
        /// backoff timer, and synchronization. See <see cref="BlockingPeriodErrorState"/>.
        /// </summary>
        private readonly BlockingPeriodErrorState _errorState;
        #endregion

        /// <summary>
        /// Initializes a new PoolingDataSource.
        /// </summary>
        internal ChannelDbConnectionPool(
            SqlConnectionFactory connectionFactory,
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
            TransactedConnectionPool = new(this);

            _connectionSlots = new(MaxPoolSize);
            _idleChannel = new();
            _errorState = new BlockingPeriodErrorState(_instanceId);

            // Pruning is only useful when the pool can grow beyond MinPoolSize.
            // If min >= max, the pool is fixed-size and pruning would never activate.
            if (MinPoolSize < MaxPoolSize)
            {
                Pruner = new PoolPruner(this, PoolGroupOptions.LoadBalanceTimeout);
            }

            State = Running;
        }

        #region Properties
        /// <inheritdoc />
        public ConcurrentDictionary<
            DbConnectionPoolAuthenticationContextKey,
            DbConnectionPoolAuthenticationContext> AuthenticationContexts { get; }

        /// <inheritdoc />
        public SqlConnectionFactory ConnectionFactory { get; }

        /// <inheritdoc />
        public int Count => _connectionSlots.ReservationCount;

        /// <inheritdoc />
        public int IdleCount => _idleChannel.Count;

        /// <inheritdoc />
        public bool ErrorOccurred => _errorState.HasError;

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
        public TransactedConnectionPool TransactedConnectionPool { get; }

        /// <inheritdoc />
        public bool UseLoadBalancing => PoolGroupOptions.UseLoadBalancing;

        private uint MaxPoolSize { get; }

        private int MinPoolSize => PoolGroupOptions.MinPoolSize;
        #endregion

        #region Methods
        /// <inheritdoc />
        public void Clear()
        {
            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "<prov.DbConnectionPool.Clear|RES|CPOOL> {0}, Clearing.", Id);

            // Clearing the pool implies the caller wants a clean slate, so abandon any cached
            // error state. FR-011.
            _errorState.Clear();

            Interlocked.Increment(ref _clearGeneration);

            // If another thread is already draining, skip the drain. The generation counter has
            // already been incremented, so stale connections will still be caught lazily by
            // IsLiveConnection on their next retrieval or return.
            if (Interlocked.CompareExchange(ref _isClearing, 1, 0) == 1)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "<prov.DbConnectionPool.Clear|RES|CPOOL> {0}, Skip drain, already clearing.", Id);
                return;
            }

            try
            {
                // Drain idle connections from the channel and destroy them. Limit iterations to
                // the current idle count to prevent an unbounded loop if connections are
                // concurrently returned to the channel during the drain.
                // Any connections from a previous generation that are returned to the pool
                // after we start draining will fail the _clearCounter comparison and will be closed.
                int numToDrain = IdleCount;
                while (numToDrain > 0 && _idleChannel.TryRead(out DbConnectionInternal? connection))
                {
                    if (connection is not null)
                    {
                        RemoveConnection(connection);
                        numToDrain--;
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isClearing, 0);
            }

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "<prov.DbConnectionPool.Clear|RES|CPOOL> {0}, Cleared.", Id);
        }

        /// <inheritdoc />
        public void PutObjectFromTransactedPool(DbConnectionInternal connection)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public DbConnectionInternal ReplaceConnection(
            DbConnection owningObject,
            DbConnectionInternal oldConnection,
            TimeoutTimer timeout)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void ReturnInternalConnection(DbConnectionInternal connection, DbConnection owningObject)
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
                var written = _idleChannel.TryWrite(connection);
                Debug.Assert(written, "Failed to write returning connection to the idle channel.");
            }
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            State = ShuttingDown;
            Pruner?.Dispose();
        }

        /// <summary>
        /// Disposes the pool by calling <see cref="Shutdown"/>. Does not throw.
        /// </summary>
        public void Dispose() => Shutdown();

        /// <inheritdoc />
        public void Startup()
        {
            // No-op for now, warmup will be implemented later.
        }

        /// <inheritdoc />
        public void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool TryGetConnection(
            DbConnection owningObject,
            TaskCompletionSource<DbConnectionInternal>? taskCompletionSource,
            TimeoutTimer timeout,
            out DbConnectionInternal? connection)
        {
            // If taskCompletionSource is null, we are in a sync context.
            if (taskCompletionSource is null)
            {
                var task = GetInternalConnection(
                        owningObject,
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

        /// <summary>
        /// Opens a new internal connection to the database, throttled by the pool's rate limiter.
        /// </summary>
        /// <param name="owningConnection">The owning connection.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <param name="timeout">The overall timeout budget. Passed through to the physical connection
        /// so it uses the remaining budget rather than starting a fresh timeout.</param>
        /// <returns>The new internal connection, or null if the pool has no available slot or the
        /// rate limiter is currently saturated. In the latter case the caller should fall back to
        /// the idle-channel wait; the rate limiter will write a null to the idle channel when a
        /// permit is released so the waiter can retry.</returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the cancellation token is cancelled before the connection operation completes.
        /// </exception>
        private DbConnectionInternal? OpenNewInternalConnection(
            DbConnection? owningConnection,
            CancellationToken cancellationToken,
            TimeoutTimer timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Fast-fail in the error state. FR-006.
            _errorState.ThrowIfActive();

            try
            {
                // Reserve a pool slot up front so we don't pay the rate-limit cost only to
                // discover the pool is full. Add() reserves synchronously and returns null
                // immediately if no slot is available; the rate-limit check only happens inside
                // the createCallback, which runs after the reservation succeeds.
                DbConnectionInternal? connection = _connectionSlots.Add(
                    createCallback: () =>
                    {
                        // Fast-fail rate-limit attempt when a limiter is configured.
                        // AttemptAcquire returns synchronously and does not queue: if no permit
                        // is available right now, the lease comes back with IsAcquired == false.
                        // We deliberately do not block here so the caller can fall back to
                        // waiting on the idle channel, where it can be satisfied either by a
                        // returning connection or by a null poke from another caller releasing
                        // its rate-limit lease (see finally below). When no limiter is
                        // configured we substitute a no-op acquired lease.
                        // FR-001, FR-002, FR-003.
                        try
                        {
                            using RateLimitLease lease = _connectionCreationRateLimiter?.AttemptAcquire(1) ?? NoOpAcquiredLease.Instance;
                            
                            if (!lease.IsAcquired)
                            {
                                // TODO: When we fail to acquire a lease, surface the lease metadata
                                // (e.g. RateLimitMetadataName.RetryAfter, ReasonPhrase) in the error
                                // path so the user can identify why the lease was denied.
                                return null;
                            }

                            cancellationToken.ThrowIfCancellationRequested();

                            // https://github.com/dotnet/SqlClient/issues/3459
                            // TODO: This blocks the thread for several network calls!
                            // When running async, the blocked thread is one allocated from the managed thread pool (due to 
                            // use of Task.Run in TryGetConnection). This is why it's critical for async callers to 
                            // pre-provision threads in the managed thread pool. Our options are limited because 
                            // DbConnectionInternal doesn't support an async open. It's better to block this thread and keep
                            // throughput high than to queue all of our opens onto a single worker thread. Add an async path 
                            // when this support is added to DbConnectionInternal.
                            // TODO: ultimately, the connection factory should also accept our cancellation token.
                            var newConnection = ConnectionFactory.CreatePooledConnection(
                                owningConnection,
                                this,
                                timeout);

                            if (newConnection is not null)
                            {
                                newConnection.ClearGeneration = _clearGeneration;
                            }

                            return newConnection;
                        }
                        finally
                        {
                            // Disposing the lease releases the permit back to the limiter (no-op
                            // for the default lease). After releasing, signal a waiter on the
                            // idle channel that they may now retry an open. We only do this if
                            // the pool can still grow; if we're at MaxPoolSize, only a connection
                            // return can satisfy a waiter. FR-004.
                            if (lease.IsAcquired && _connectionSlots.ReservationCount < MaxPoolSize)
                            {
                                _idleChannel.TryWrite(null);
                            }
                        }
                    },
                    cleanupCallback: (newConnection) =>
                    {
                        // If we fail to open a connection, we need to write a null to the idle channel to
                        // wake up any waiters
                        _idleChannel.TryWrite(null);
                        newConnection?.Dispose();
                    });

                if (connection is not null)
                {
                    // A new connection was added to the pool. If we've grown past MinPoolSize,
                    // start the pruning timer so idle connections can be reclaimed.
                    Pruner?.UpdateTimer();
                }

                // A successful creation clears any prior error state and resets backoff. FR-009.
                if (connection is not null && _errorState.HasError)
                {
                    _errorState.Clear();
                }

                return connection;
            }
            catch (Exception ex) when (ADP.IsCatchableExceptionType(ex))
            {
                // Enter the blocking period error state on creation failure if configured.
                // FR-006, FR-007.
                if (PoolGroup.IsBlockingPeriodEnabled())
                {
                    _errorState.Enter(ex);
                }

                throw;
            }
        }

        /// <summary>
        /// Checks that the provided connection is live and unexpired and closes it if needed.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns>Returns true if the connection is live and unexpired, otherwise returns false.</returns>
        private bool IsLiveConnection(DbConnectionInternal connection)
        {
            // Broken physical connection
            if (!connection.IsConnectionAlive())
            {
                return false;
            }

            // Connection has been alive longer than the load balance timeout
            if (LoadBalanceTimeout != TimeSpan.Zero && DateTime.UtcNow > connection.CreateTime + LoadBalanceTimeout)
            {
                return false;
            }

            // Connection was created before the last Clear, so it's stale.
            if (connection.ClearGeneration != _clearGeneration)
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
            _idleChannel.TryWrite(null);

            connection.Dispose();

            // If this removal brought us back to MinPoolSize, disable the pruning timer.
            Pruner?.UpdateTimer();
        }

        /// <summary>
        /// Tries to read a connection from the idle connection channel.
        /// </summary>
        /// <returns>A connection from the idle channel, or null if the channel is empty.</returns>
        private DbConnectionInternal? GetIdleConnection()
        {
            // The channel may contain nulls. Read until we find a non-null connection or exhaust the channel.
            while (_idleChannel.TryRead(out DbConnectionInternal? connection))
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
        /// <param name="async">A boolean indicating whether the operation should be asynchronous.</param>
        /// <param name="timeout">The overall timeout budget for this connection request. Time spent waiting
        /// in the pool is deducted from the budget available for physical connection creation.</param>
        /// <returns>Returns a DbConnectionInternal that is retrieved from the pool.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when an OperationCanceledException is caught, indicating that the timeout period
        /// elapsed prior to obtaining a connection from the pool.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when a ChannelClosedException is caught, indicating that the connection pool
        /// has been shut down.
        /// </exception>
        private async Task<DbConnectionInternal> GetInternalConnection(
            DbConnection owningConnection,
            bool async,
            TimeoutTimer timeout)
        {
            DbConnectionInternal? connection = null;

            // Derive a CancellationTokenSource from the TimeoutTimer so pool-internal wait operations
            // (channel reads, semaphore waits) are cancelled when the overall budget expires.
            using CancellationTokenSource cancellationTokenSource = timeout.CreateCancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            // Continue looping until we create or retrieve a connection
            do
            {
                try
                {
                    // Optimistically try to get an idle connection from the channel
                    // Doesn't wait if the channel is empty, just returns null.
                    connection ??= GetIdleConnection();


                    // If we didn't find an idle connection, try to open a new one. This may
                    // return null if the pool is full or the rate limiter is currently saturated;
                    // in either case the caller falls through to the idle-channel wait below.
                    connection ??= OpenNewInternalConnection(
                        owningConnection,
                        cancellationToken,
                        timeout);

                    // If we're at max capacity and couldn't open a connection. Block on the idle channel with a
                    // timeout. Note that Channels guarantee fair FIFO behavior to callers of ReadAsync
                    // (first-come, first-served), which is crucial to us.
                    if (async)
                    {
                        connection ??= await _idleChannel.ReadAsync(cancellationToken).ConfigureAwait(false);
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
                    throw new InvalidOperationException(StringsHelper.GetString(Strings.SQL_ConnectionPoolShutDown));
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
                    _idleChannel.ReadAsync(cancellationToken).ConfigureAwait(false).GetAwaiter();
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
        /// <exception cref="Exception">
        /// Thrown when any exception occurs during connection activation.
        /// </exception>
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

        #region Pruning
        /// <summary>
        /// Manages idle connection pruning. Null when the pool is fixed-size (MinPoolSize >= MaxPoolSize)
        /// because pruning would never activate.
        /// </summary>
        internal PoolPruner? Pruner { get; }

        /// <summary>
        /// Removes up to <paramref name="count"/> idle connections from the pool, respecting
        /// the <see cref="MinPoolSize"/> floor. Called by <see cref="PoolPruner"/> after computing
        /// the median of collected samples.
        /// </summary>
        internal void PruneConnections(int count)
        {
            while (count > 0
                && IsRunning
                && _connectionSlots.ReservationCount > MinPoolSize
                && _idleChannel.TryRead(out var connection))
            {
                if (connection is null)
                {
                    continue;
                }

                RemoveConnection(connection);
                count--;
            }
        }
        #endregion
    }
}

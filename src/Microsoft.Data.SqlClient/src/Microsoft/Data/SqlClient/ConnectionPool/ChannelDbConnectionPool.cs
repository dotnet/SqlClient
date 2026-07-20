// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data.Common;
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
    ///
    /// <para>
    /// Comments in this file reference requirements by tag (e.g. <c>FR-001</c>). These tags are
    /// defined in the feature spec at <c>specs/006-pool-rate-limiting/spec.md</c> (see the
    /// "Functional Requirements" section); consult it for the authoritative description of each
    /// requirement.
    /// </para>
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
        /// Tracks whether <see cref="Shutdown"/> has already initiated the shutdown sequence so that
        /// repeated calls are observed as no-ops. Updated atomically via
        /// <see cref="Interlocked.CompareExchange(ref int, int, int)"/>.
        /// </summary>
        private int _shutdownInitiated;

        /// <summary>
        /// Optional concurrency limiter that throttles the number of concurrent physical connection
        /// creation attempts. When null, no rate limiting is applied. A non-null limiter is
        /// supplied at pool construction time; there is no default. Callers fast-fail against
        /// the limiter and fall back to the idle-channel wait when no permit is available.
        /// Scope note: the permit-release wake is signaled on this pool's own idle channel, so a
        /// limiter is expected to be scoped to a single pool. Sharing one limiter across multiple
        /// pools is not supported, because a permit released by another pool would not wake waiters
        /// parked here and they could stall until their timeout expires.
        /// Lifetime note: the pool does not own this limiter and never disposes it; the caller that
        /// constructs the limiter owns its lifetime (it may outlive the pool).
        /// </summary>
        private readonly ConcurrencyLimiter? _connectionCreationRateLimiter;

        /// <summary>
        /// Encapsulates the blocking-period error state for this pool: cached exception, exponential
        /// backoff timer, and synchronization. Created only when blocking period is enabled for
        /// this pool group. See <see cref="BlockingPeriodErrorState"/>.
        /// </summary>
        private readonly BlockingPeriodErrorState? _errorState;

        /// <summary>
        /// Cancels in-flight background warmup/replenishment when the pool shuts down so no new
        /// connections are created after shutdown begins (Story 4). The token is observed at the
        /// warmup loop's await points and before each creation attempt, so it stops the loop and
        /// prevents further attempts promptly. It cannot abort an already in-progress physical open:
        /// that path is synchronous and the connection factory does not yet accept a cancellation
        /// token (see the TODO in <see cref="OpenNewInternalConnection"/>).
        /// </summary>
        private readonly CancellationTokenSource _warmupCts = new();

        /// <summary>
        /// Coalescing guard ensuring only one warmup loop executes at a time. Transitioned
        /// 0 -> 1 via <see cref="Interlocked.CompareExchange(ref int, int, int)"/> by the first
        /// requester to start the loop, and reset to 0 by the loop when it drains.
        /// </summary>
        private int _warmupLoopRunning;

        /// <summary>
        /// Test seam controlling how the warmup loop is launched. Defaults to
        /// <see cref="Task.Run(Func{Task})"/> on the thread pool in production. Tests inject a
        /// scheduler that runs the loop on a dedicated thread so warmup's first (synchronous,
        /// possibly gated) physical open cannot be delayed by thread-pool starvation, keeping
        /// timing-sensitive assertions deterministic without mutating global thread-pool state.
        /// </summary>
        private readonly Action<Func<Task>> _warmupLoopScheduler;
        #endregion

        /// <summary>
        /// Initializes a new PoolingDataSource.
        /// </summary>
        internal ChannelDbConnectionPool(
            SqlConnectionFactory connectionFactory,
            DbConnectionPoolGroup connectionPoolGroup,
            DbConnectionPoolIdentity identity,
            DbConnectionPoolProviderInfo connectionPoolProviderInfo,
            ConcurrencyLimiter? connectionCreationRateLimiter = null,
            TimeProvider? timeProvider = null,
            Action<Func<Task>>? warmupLoopScheduler = null)
        {
            ConnectionFactory = connectionFactory;
            PoolGroup = connectionPoolGroup;
            PoolGroupOptions = connectionPoolGroup.PoolGroupOptions;
            ProviderInfo = connectionPoolProviderInfo;
            Identity = identity;
            AuthenticationContexts = new();
            MaxPoolSize = Convert.ToUInt32(PoolGroupOptions.MaxPoolSize);
            TransactedConnectionPool = new(this);
            _connectionCreationRateLimiter = connectionCreationRateLimiter;
            _warmupLoopScheduler = warmupLoopScheduler ?? (static loop => Task.Run(loop));

            _connectionSlots = new(MaxPoolSize);
            _idleChannel = new();
            if (PoolGroup.IsBlockingPeriodEnabled())
            {
                // timeProvider is injected only by tests so the blocking-period exit timer can be
                // driven deterministically; in production it is null and the error state falls back
                // to TimeProvider.System.
                _errorState = new BlockingPeriodErrorState(_instanceId, timeProvider: timeProvider);
            }

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
        public bool ErrorOccurred => _errorState?.HasError ?? false;

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
            _errorState?.Clear();

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

            // Stamp the return time before IsLiveConnection runs so the idle-expiry gate inside it
            // measures time-in-pool, not time-since-last-return. Without this, a connection whose
            // checkout exceeded IdleTimeout (e.g. a long-running query) would be wrongly evicted on
            // return even though it was actively in use on the wire. The same gating conditions are
            // applied here as in IsLiveConnection so we avoid the per-return DateTime.UtcNow when
            // idle expiry is disabled or the legacy idle-timeout behavior is in effect.
            if (!LocalAppContextSwitches.UseLegacyIdleTimeoutBehavior &&
                PoolGroupOptions.IdleTimeout != TimeSpan.Zero)
            {
                connection.SetReturnedTime();
            }

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
                if (!_idleChannel.TryWrite(connection))
                {
                    // The channel has been completed (pool is shutting down). Race window
                    // between the State check above and TryWrite: destroy instead of pooling.
                    RemoveConnection(connection);
                }
            }
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            // idempotent. Compare-and-exchange ensures only one caller performs shutdown work.
            if (Interlocked.CompareExchange(ref _shutdownInitiated, 1, 0) != 0)
            {
                return;
            }

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "<prov.DbConnectionPool.Shutdown|RES|INFO|CPOOL> {0}", Id);

            // Transition to ShuttingDown. After this point, ReturnInternalConnection
            // routes returning connections to RemoveConnection.
            State = ShuttingDown;

            // Cancel any in-flight background warmup/replenishment so no new connections are
            // created after shutdown begins (Story 4). The warmup loop also observes the
            // State transition above; cancelling here stops the loop promptly by tripping its
            // await points and pre-create checks. It does not abort an already in-progress
            // synchronous physical open (see _warmupCts field docs). Cancel is idempotent.
            try
            {
                _warmupCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Expected no-op: the cancellation source is already disposed, so there is nothing
                // left to cancel. This is not a failure, so it is not traced.
            }
            catch (Exception ex)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "<prov.DbConnectionPool.Shutdown|RES|CPOOL> {0}, _warmupCts.Cancel threw, continuing shutdown: {1}", Id, ex);
            }

            // Each cleanup step is independent and best-effort. A failure in one step must not
            // prevent later steps from running, otherwise the pool can be left half-shut-down
            // (e.g. timer disposed but channel never completed -> waiters stuck forever).

            // Stop the idle-pruning timer before draining so a tick cannot race with
            // the final drain below. PoolPruner.Dispose is idempotent and non-throwing
            // in normal use; the catch is defense-in-depth.
            try
            {
                Pruner?.Dispose();
            }
            catch (Exception ex)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "<prov.DbConnectionPool.Shutdown|RES|CPOOL> {0}, Pruner.Dispose threw, continuing shutdown: {1}", Id, ex);
            }

            // Dispose the error state so its exit timer is released. Otherwise a timer scheduled
            // during the blocking period would keep this pool reachable and continue firing
            // callbacks/logging after shutdown.
            try
            {
                _errorState?.Dispose();
            }
            catch (Exception ex)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "<prov.DbConnectionPool.Shutdown|RES|CPOOL> {0}, _errorState.Dispose threw, continuing shutdown: {1}", Id, ex);
            }

            // Complete the channel writer so:
            //  - no further idle connections can be enqueued (TryWrite returns false), and
            //  - in-flight / future async waiters on ReadAsync fault with ChannelClosedException.
            // IdleConnectionChannel.Complete wraps ChannelWriter.TryComplete and is idempotent
            // (a second call returns false rather than throwing), so this is safe even if the
            // shutdown sequence is ever refactored to invoke this step more than once.
            _idleChannel.Complete();

            // Reuse Clear() for the drain. Clear bumps _clearGeneration so any active
            // checked-out connection fails IsLiveConnection on return and is removed, and it
            // drains the idle channel up to its captured IdleCount.
            try
            {
                Clear();
            }
            catch (Exception ex)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "<prov.DbConnectionPool.Shutdown|RES|CPOOL> {0}, Clear threw, continuing shutdown: {1}", Id, ex);
            }

            // Clear() may short-circuit if another caller is already draining. Because the
            // channel is now completed, no new items can be enqueued, so it is safe to do a
            // final unbounded drain to mop up anything Clear() may have skipped.
            while (_idleChannel.TryRead(out DbConnectionInternal? connection))
            {
                if (connection is null)
                {
                    // null sentinels are wake-up signals only; nothing to destroy.
                    continue;
                }

                // Isolate per-connection failure: one bad Dispose must not strand the rest.
                try
                {
                    RemoveConnection(connection);
                }
                catch (Exception ex)
                {
                    SqlClientEventSource.Log.TryPoolerTraceEvent(
                        "<prov.DbConnectionPool.Shutdown|RES|CPOOL> {0}, RemoveConnection threw during drain, continuing: {1}", Id, ex);
                }
            }

            // Release the warmup cancellation source now that the loop has been signalled to stop
            // and no further connections will be created. Dispose is best-effort and idempotent.
            try
            {
                _warmupCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Expected no-op: the cancellation source is already disposed. Dispose is documented
                // as safe to call repeatedly, so this is not a failure and is not traced.
            }
            catch (Exception ex)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "<prov.DbConnectionPool.Shutdown|RES|CPOOL> {0}, _warmupCts.Dispose threw, continuing shutdown: {1}", Id, ex);
            }
        }

        /// <summary>
        /// Disposes the pool by calling <see cref="Shutdown"/>. Does not throw.
        /// </summary>
        public void Dispose() => Shutdown();

        /// <inheritdoc />
        public void Startup()
        {
            // State is set to Running in the constructor, and PoolPruner (when present, i.e.
            // MinPoolSize < MaxPoolSize) is also constructed eagerly there; its timer arms/disarms
            // via UpdateTimer() calls from OpenNewInternalConnection and RemoveConnection as the
            // pool grows/shrinks.
            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "<prov.DbConnectionPool.Startup|RES|INFO|CPOOL> {0}", Id);

            // Kick off background warmup so the pool pre-creates connections up to MinPoolSize
            // without blocking the caller (Story 1). No-op when MinPoolSize == 0.
            RequestWarmup();
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
            // Short-circuit when the pool is not Running (i.e., shut down or never started).
            // Returning (true, null) matches WaitHandleDbConnectionPool.TryGetConnection and tells
            // the caller "completed; no connection available" without entering the channel path,
            // which would otherwise reserve a slot, attempt to open a fresh physical connection,
            // and then immediately destroy it on return because State == ShuttingDown.
            if (State is not Running)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "<prov.DbConnectionPool.TryGetConnection|RES|CPOOL> {0}, State != Running.", Id);
                connection = null;
                return true;
            }

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

            // Fast-fail if the pool is in the blocking-period error state. FR-006. Warmup goes
            // through this same path (it has no isWarmup exemption): it mirrors the legacy WaitHandle
            // pool, whose replenishment enters/clears the same error state as user requests. In
            // practice the warmup loop already stands down before reaching here (its loop condition
            // checks ErrorOccurred); this covers the narrow race where the state flips in between.
            _errorState?.ThrowIfActive();

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
                        // its rate-limit lease (see finally below). We prefer to recycle existing
                        // connections rather than queue on the rate limit. When no limiter is
                        // configured we substitute a no-op acquired lease.
                        // FR-001, FR-002, FR-003.

                        RateLimitLease lease = _connectionCreationRateLimiter?.AttemptAcquire(1) ?? NoOpAcquiredLease.Instance;

                        // Tracks whether we are leaving this block via an exception rather than a
                        // normal return. It is read in the finally below to decide whether to poke
                        // the idle channel: every return path sets it to false first, so by the time
                        // the finally runs it is true only when an exception is propagating. This
                        // lets us skip the wake on exception paths, where cleanupCallback already
                        // pokes, and avoid a redundant double wake.
                        bool faulted = true;
                        try
                        {
                            if (!lease.IsAcquired)
                            {
                                // TODO: When we fail to acquire a lease, surface the lease metadata
                                // (e.g. RateLimitMetadataName.RetryAfter, ReasonPhrase) in the error
                                // path so the user can identify why the lease was denied.
                                faulted = false;
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

                            faulted = false;
                            return newConnection;
                        }
                        finally
                        {
                            // Capture the acquired state before disposing: the poke condition below
                            // reads it, and accessing a lease after Dispose is fragile even if the
                            // current ConcurrencyLimiter lease happens to keep IsAcquired stable.
                            bool leaseAcquired = lease.IsAcquired;

                            // Release the permit back to the limiter (no-op for the default lease)
                            // BEFORE signaling a waiter. Otherwise a woken waiter could consume the
                            // null poke and retry its acquire before the permit is actually returned,
                            // fail to acquire, and fall back to waiting with no subsequent signal -
                            // stalling connection creation even though the limiter has capacity.
                            // When no limiter is configured this is NoOpAcquiredLease.Instance, whose
                            // Dispose is an idempotent no-op, so disposing the shared singleton here
                            // is safe.
                            lease.Dispose();

                            // After releasing, signal a waiter on the idle channel that they may now
                            // retry an open. We only poke on non-faulted completion: on exception paths
                            // the cleanupCallback below already writes a wake, so poking here too would
                            // produce a redundant double wake. We also only poke when a limiter is
                            // configured (a waiter only falls back to the idle channel due to rate
                            // limiting in that case) and the pool can still grow; if we're at
                            // MaxPoolSize, only a connection return can satisfy a waiter. FR-004. This
                            // is best-effort; releasing a lease doesn't guarantee the rate limiter
                            // immediately has an available permit, but the waiter we wake will fall
                            // back to waiting again if not.
                            if (!faulted &&
                                leaseAcquired &&
                                _connectionCreationRateLimiter is not null &&
                                _connectionSlots.ReservationCount < MaxPoolSize)
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

                    // A successful creation clears error/backoff state (FR-009). Warmup goes through
                    // this same path and clears the state on success too, mirroring the legacy
                    // WaitHandle pool: a connection that opens proves the server is reachable.
                    _errorState?.Clear();
                }

                return connection;
            }
            catch (Exception ex) when (ADP.IsCatchableExceptionType(ex) && ex is not OperationCanceledException)
            {
                // Enter the blocking period error state on creation failure if configured. Warmup
                // goes through this same path (the warmup loop absorbs the rethrow in its own catch),
                // mirroring the legacy WaitHandle pool, whose replenishment failures also enter the
                // error state.
                //
                // We deliberately exclude OperationCanceledException: that is thrown when the
                // caller's own timeout/cancellation budget expires while waiting, which is
                // client-side contention rather than a physical connection creation failure and
                // must not poison the pool into fast-fail/backoff for other callers.
                // FR-006, FR-007.
                _errorState?.Enter(ex);

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
            // Connection has been sitting idle longer than the configured idle timeout.
            // Checked before the (potentially expensive) liveness probe so an idle-expired
            // connection is discarded without an SNI round-trip.
            // ReturnedTime is initialized to CreateTime so a freshly minted connection never trips this
            // check on first retrieval, and is then stamped by ReturnInternalConnection on every return.
            // Use subtraction rather than addition so the comparison cannot throw if ReturnedTime is
            // ever close to DateTime.MaxValue. A clock skew that leaves ReturnedTime in the future
            // produces a negative TimeSpan, which falls through as not-expired (fail safe).
            TimeSpan idleTimeout = PoolGroupOptions.IdleTimeout;
            if (!LocalAppContextSwitches.UseLegacyIdleTimeoutBehavior &&
                idleTimeout != TimeSpan.Zero &&
                DateTime.UtcNow - connection.ReturnedTime > idleTimeout)
            {
                return false;
            }

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

            // Any removal that drops the pool below MinPoolSize triggers replenishment through the
            // shared serial, rate-limited warmup path (Story 5). This is the single choke point for
            // every below-minimum event: connections destroyed on return (broken/lifetime), idle
            // timeout eviction, and pruning all funnel through RemoveConnection. RequestWarmup is a
            // no-op when MinPoolSize == 0, the pool is not Running, or a warmup loop is already
            // running/queued, so calling it unconditionally here is cheap.
            RequestWarmup();
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

        #region Warmup
        /// <summary>
        /// Delay before retrying a warmup creation that was denied by the shared rate limiter, so
        /// warmup waits its turn instead of bypassing the limiter or spinning (Story 2).
        /// </summary>
        private static readonly TimeSpan s_warmupRateLimitRetryDelay = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Requests background warmup/replenishment: the pool asynchronously pre-creates connections
        /// up to <see cref="MinPoolSize"/>, serially and through the shared rate limiter. Safe to
        /// call from any thread and from hot paths; it is cheap and non-blocking.
        ///
        /// This is the single entry point for every warmup trigger: pool startup and any event that
        /// drops the pool below <see cref="MinPoolSize"/> (connection destruction on return, idle
        /// timeout eviction, pruning). Concurrent requests are coalesced by the
        /// <see cref="_warmupLoopRunning"/> guard so that only one warmup loop ever runs at a time: a
        /// request that arrives while a loop is already running is simply dropped, because that loop
        /// re-reads <see cref="Count"/> on every iteration and will drive the pool to
        /// <see cref="MinPoolSize"/> regardless.
        /// </summary>
        private void RequestWarmup()
        {
            // No-op when there is nothing to pre-create (MinPoolSize == 0), the pool is not running,
            // or shutdown has cancelled background activity.
            if (MinPoolSize == 0 || State != Running || _warmupCts.IsCancellationRequested)
            {
                return;
            }

            // Fast path: the pool is already at or above the minimum, so there is nothing to warm
            // up. Return before scheduling a thread-pool work item. This keeps hot-path callers
            // (e.g. RemoveConnection on every return) cheap. The check is best-effort under
            // concurrency; a below-minimum condition missed here is still observed by the running
            // loop, which re-reads Count on every iteration.
            if (Count >= MinPoolSize)
            {
                return;
            }

            // Coalesce: only start a loop if one is not already running.
            if (Interlocked.CompareExchange(ref _warmupLoopRunning, 1, 0) != 0)
            {
                return;
            }

            try
            {
                // Fire-and-forget on the thread pool (or an injected test scheduler) so warmup never
                // blocks the caller. The loop absorbs its own exceptions and always releases the
                // single-loop guard on exit.
                _warmupLoopScheduler(RunWarmupLoopAsync);
            }
            catch (Exception ex)
            {
                // Scheduling the loop failed (e.g. an injected scheduler threw, or the thread pool
                // refused the work item). Release the guard so warmup isn't permanently pinned off
                // for the life of the pool; the next below-minimum trigger will try again.
                Volatile.Write(ref _warmupLoopRunning, 0);
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "<prov.DbConnectionPool.RequestWarmup|RES|CPOOL> {0}, Failed to schedule warmup loop, absorbing: {1}", Id, ex);
            }
        }

        /// <summary>
        /// The coalesced warmup loop: creates connections one at a time (serially) up to
        /// <see cref="MinPoolSize"/>, then releases the single-loop guard. Each creation goes through
        /// the same slot-reservation and rate-limited path as user requests
        /// (<see cref="OpenNewInternalConnection"/>), and freshly created connections are published
        /// to the idle channel for waiting or future user requests. All failures are absorbed so
        /// warmup can never surface an unhandled exception (Story 3); a genuine open failure still
        /// enters the pool's blocking-period error state via the shared creation path, mirroring the
        /// legacy WaitHandle pool.
        /// </summary>
        private async Task RunWarmupLoopAsync()
        {
            try
            {
                CancellationToken token;
                try
                {
                    token = _warmupCts.Token;
                }
                catch (ObjectDisposedException)
                {
                    // The pool shut down and disposed the cancellation source before this loop
                    // started. There is nothing to warm up; the finally releases the guard.
                    return;
                }

                // Single pass: re-reads Count each iteration, so any below-minimum drop that happens
                // while the loop is running is picked up here without a separate request flag.
                //
                // The loop keeps creating while the pool is running, shutdown has not cancelled
                // background work, and the pool is not in the blocking-period error state. While user
                // requests are failing and the pool is blocking (ErrorOccurred), warmup stands down
                // rather than piling more doomed opens onto a struggling server - mirroring the legacy
                // WaitHandle pool, which skips replenishment while blocking. (Warmup still
                // participates in the error state on its own creations - entering it on failure and
                // clearing it on success - via OpenNewInternalConnection.)
                while (State == Running
                    && !token.IsCancellationRequested
                    && !ErrorOccurred
                    && Count < MinPoolSize)
                {
                    // Fresh per-attempt timeout budget based on the pool's CreationTimeout, since
                    // warmup has no owning Open() call to inherit a budget from. Matches the
                    // replenishment behavior of the legacy WaitHandle pool.
                    TimeoutTimer timeout = TimeoutTimer.StartNew(
                        TimeSpan.FromMilliseconds(PoolGroupOptions.CreationTimeout));

                    DbConnectionInternal? connection;
                    try
                    {
                        // owningConnection is null: warmup connections are created unattached and
                        // enter the pool as idle. A null return means the shared rate limiter was
                        // saturated; a thrown exception means the physical open genuinely failed.
                        connection = OpenNewInternalConnection(
                            owningConnection: null,
                            cancellationToken: token,
                            timeout: timeout);
                    }
                    catch (OperationCanceledException)
                    {
                        // Shutdown/cancellation unwound the in-flight create. Stop warming up.
                        break;
                    }
                    catch (Exception ex) when (ADP.IsCatchableExceptionType(ex))
                    {
                        // A genuine connection-open failure (not rate limiting). It has already
                        // entered the pool's blocking-period error state (see OpenNewInternalConnection);
                        // trace and absorb the rethrow here, then stop this pass rather than retrying
                        // on a tight cadence. The pool stays operational: user requests fast-fail
                        // during the blocking window and resume creating on demand once it expires,
                        // and the next below-minimum trigger re-requests warmup (Story 3).
                        SqlClientEventSource.Log.TryPoolerTraceEvent(
                            "<prov.DbConnectionPool.RunWarmupLoopAsync|RES|CPOOL> {0}, Warmup connection creation failed, stopping pass: {1}", Id, ex);
                        break;
                    }

                    if (connection is null)
                    {
                        // A slot is guaranteed available here (Count < MinPoolSize <= MaxPoolSize),
                        // and creation failures throw rather than return null, so a null return
                        // means the shared rate limiter is currently saturated. Wait our turn
                        // rather than bypassing it (Story 2), then retry. Capacity frees up as user
                        // requests release their permits.
                        try
                        {
                            await Task.Delay(s_warmupRateLimitRetryDelay, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }

                        continue;
                    }

                    // Publish the freshly created connection as idle. It was PrePush'd at creation
                    // (CreatePooledConnection) and never activated, so it is in the correct state
                    // to enter the idle channel directly. If a Clear raced and bumped the
                    // generation, the stale connection is harmlessly removed by IsLiveConnection
                    // on its next retrieval, so we don't check the generation here.
                    if (!_idleChannel.TryWrite(connection))
                    {
                        // Channel completed (pool shutting down). Destroy instead of pooling.
                        RemoveConnection(connection);
                        break;
                    }

                    // OpenNewInternalConnection is synchronous and blocks the loop's thread for
                    // the duration of the physical open. Yield between creations so a multi-
                    // connection warmup returns its thread-pool worker to the scheduler between
                    // opens (rather than monopolizing one worker for the whole sequence) and stays
                    // responsive to cancellation. There is no sync-over-async anywhere in this path.
                    await Task.Yield();
                }
            }
            catch (Exception ex)
            {
                // Defense in depth: the loop must never throw onto the thread pool.
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "<prov.DbConnectionPool.RunWarmupLoopAsync|RES|CPOOL> {0}, Warmup loop failed, absorbing: {1}", Id, ex);
            }
            finally
            {
                // Always release the single-loop guard, whatever exit path we took, so a future
                // below-minimum trigger can start a new loop.
                Volatile.Write(ref _warmupLoopRunning, 0);
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

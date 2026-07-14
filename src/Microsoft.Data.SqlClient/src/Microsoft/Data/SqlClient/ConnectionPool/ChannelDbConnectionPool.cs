// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
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
        /// The idle/creation channel. Every connection request queues here (there is no optimistic
        /// fast path around it), which gives strict FIFO ordering to parked waiters. It carries
        /// <see cref="CreateOutcome"/> values: a connection (idle or freshly pumped), a captured
        /// creation error to rethrow on a waiter, or a bare wake ("capacity changed, re-evaluate").
        /// Also tracks the count of connection-bearing outcomes (idle connections).
        /// </summary>
        private readonly IdleConnectionChannel _idleChannel;

        /// <summary>
        /// The number of requests currently parked waiting for an outcome on <see cref="_idleChannel"/>.
        /// Incremented immediately before a request pumps and parks, and decremented once its wait
        /// completes. The pump reads this to decide whether there is unmet demand worth creating a
        /// connection for, and slot-freeing events read it to decide whether a bare wake is needed.
        ///
        /// Ordering hinge: a waiter increments this <em>before</em> it evaluates pool capacity and
        /// parks. Combined with the fact that a slot-freeing event frees its slot before it reads
        /// this counter, that guarantees no lost wake-up: either the waiter observes the freed slot
        /// and creates a connection itself, or the freeing event observes the waiter and wakes it.
        /// Updated via <see cref="Interlocked"/>.
        /// </summary>
        private int _waiterCount;

        /// <summary>
        /// The number of background create ("pump") tasks currently in flight. Used by the pump's
        /// demand/capacity gate so it launches at most one create per unit of unmet demand and never
        /// over-commits past <see cref="MaxPoolSize"/>. Updated via <see cref="Interlocked"/>.
        /// </summary>
        private int _pendingCreates;

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
        /// This will be implemented later when we add support for the pool blocking period after errors. For now, it always returns false.
        public bool ErrorOccurred => false;

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
                while (numToDrain > 0 && _idleChannel.TryRead(out CreateOutcome outcome))
                {
                    if (outcome.Connection is not null)
                    {
                        RemoveConnection(outcome.Connection);
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
                if (!_idleChannel.TryWrite(new CreateOutcome(connection)))
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
            while (_idleChannel.TryRead(out CreateOutcome outcome))
            {
                DbConnectionInternal? connection = outcome.Connection;
                if (connection is null)
                {
                    // Bare-wake and error outcomes carry no connection; nothing to destroy.
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
        }

        /// <summary>
        /// Disposes the pool by calling <see cref="Shutdown"/>. Does not throw.
        /// </summary>
        public void Dispose() => Shutdown();

        /// <inheritdoc />
        public void Startup()
        {
            // Startup is currently a no-op for this pool: State is set to Running in the
            // constructor, and PoolPruner (when present, i.e. MinPoolSize < MaxPoolSize) is
            // also constructed eagerly there; its timer arms/disarms via UpdateTimer() calls
            // from OpenNewInternalConnection and RemoveConnection as the pool grows/shrinks.
            // This method exists as the symmetrical counterpart of Shutdown and as a hook
            // for future warmup behavior.
            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "<prov.DbConnectionPool.Startup|RES|INFO|CPOOL> {0}", Id);
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
        /// Opens a new internal connection to the database.
        /// </summary>
        /// <param name="owningConnection">The owning connection.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <param name="timeout">The overall timeout budget. Passed through to the physical connection
        /// so it uses the remaining budget rather than starting a fresh timeout.</param>
        /// <returns>A task representing the asynchronous operation, with a result of the new internal connection.</returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the cancellation token is cancelled before the connection operation completes.
        /// </exception>
        private DbConnectionInternal? OpenNewInternalConnection(
            DbConnection? owningConnection,
            CancellationToken cancellationToken,
            TimeoutTimer timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Opening a connection can be a slow operation and we don't want to hold a lock for the duration.
            // Instead, we reserve a connection slot prior to attempting to open a new connection and release the slot
            // in case of an exception.

            var result = _connectionSlots.Add(
                createCallback: () =>
                {
                    // https://github.com/dotnet/SqlClient/issues/3459
                    // TODO: This blocks the thread for several network calls!
                    // When running async, the blocked thread is one allocated from the managed thread pool (due to
                    // use of Task.Run in TryGetConnection). This is why it's critical for async callers to
                    // pre-provision threads in the managed thread pool. Our options are limited because
                    // DbConnectionInternal doesn't support an async open. It's better to block this thread and keep
                    // throughput high than to queue all of our opens onto a single worker thread. Add an async path
                    // when this support is added to DbConnectionInternal.
                    // TODO: ultimately, the connection factory should also accept our cancellation token.
                    var connection = ConnectionFactory.CreatePooledConnection(
                        owningConnection,
                        this,
                        timeout);

                    if (connection is not null)
                    {
                        connection.ClearGeneration = _clearGeneration;
                    }

                    return connection;
                },
                cleanupCallback: (newConnection) =>
                {
                    // Creation failed or produced no slot. Error propagation and re-driving of
                    // remaining waiters are handled by the pump task (LaunchCreate); here we only
                    // dispose whatever partial connection may have been produced.
                    newConnection?.Dispose();
                });

            if (result is not null)
            {
                // A new connection was added to the pool. If we've grown past MinPoolSize,
                // start the pruning timer so idle connections can be reclaimed.
                Pruner?.UpdateTimer();
            }

            return result;
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

            // Removing a connection frees a slot. If a request is currently parked waiting for a
            // connection, wake one so it can re-pump and create a replacement using its own owning
            // connection. The bare-wake outcome carries neither a connection nor an error.
            //
            // The wake is skipped when no request is waiting: there is no one to notify, and the
            // freed slot will be observed by the next request's own pump. This also prevents stale
            // wakes from accumulating in the channel. See _waiterCount for the ordering argument
            // that guarantees a genuinely-parked waiter is never missed.
            if (Volatile.Read(ref _waiterCount) > 0)
            {
                _idleChannel.TryWrite(default);
            }

            connection.Dispose();

            // If this removal brought us back to MinPoolSize, disable the pruning timer.
            Pruner?.UpdateTimer();
        }

        /// <summary>
        /// The pump: signals demand and, when there is unmet demand and free capacity, launches
        /// background create tasks to grow the pool. Called by a request that is about to park (so
        /// its demand is reflected in <see cref="_waiterCount"/> first) and whenever a create task
        /// completes and frees its accounting.
        ///
        /// The gate is a cheap synchronous check, so most calls are near-free. Each launched task
        /// reserves a slot and opens one physical connection, so multiple waiters are served in
        /// parallel (bounded by <see cref="MaxPoolSize"/> and, once rate limiting lands, by the
        /// limiter inside the create task).
        /// </summary>
        /// <param name="owningConnection">A live requesting connection whose creation options seed the
        /// physical open. Every pump is triggered by a request that is actively waiting, so this is
        /// always a real, in-use connection.</param>
        private void TryPumpCreate(DbConnection owningConnection)
        {
            while (true)
            {
                int waiters = Volatile.Read(ref _waiterCount);
                int pending = Volatile.Read(ref _pendingCreates);

                // Demand already covered by in-flight creates plus connections sitting in the
                // channel? Then nothing to do.
                if (pending + _idleChannel.Count >= waiters)
                {
                    return;
                }

                // Any capacity to create? ReservationCount counts slots already held by live
                // connections; adding pending creates (each of which will reserve a slot) gives the
                // projected occupancy. This is advisory - ConnectionPoolSlots.Add re-checks
                // atomically - but it prevents a busy loop of no-slot creates when the pool is full.
                if (_connectionSlots.ReservationCount + pending >= MaxPoolSize)
                {
                    return;
                }

                // Reserve a create optimistically, then launch it. If we lose a race the create
                // task will find no slot and no-op, which is harmless.
                Interlocked.Increment(ref _pendingCreates);
                LaunchCreate(owningConnection);
            }
        }

        /// <summary>
        /// Runs a single background connection create and publishes the result to the channel for
        /// whichever waiter is at the FIFO head:
        /// <list type="bullet">
        /// <item><description>success: the connection is written as a connection outcome;</description></item>
        /// <item><description>failure: the captured error is written as an error outcome so a waiter
        /// rethrows it (one failure is consumed by one waiter);</description></item>
        /// <item><description>no slot available: nothing is written (capacity is unchanged).</description></item>
        /// </list>
        /// </summary>
        private void LaunchCreate(DbConnection owningConnection)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    // Creation is decoupled from any specific caller, so it uses the pool's
                    // configured CreationTimeout for the physical open rather than a caller's
                    // remaining budget. A caller's own ConnectTimeout governs only how long it waits
                    // on the channel. CreationTimeout of 0 maps to an infinite timer (TimeSpan.Zero
                    // has zero ticks, which TimeoutTimer treats as infinite).
                    TimeoutTimer timeout = TimeoutTimer.StartNew(
                        TimeSpan.FromMilliseconds(PoolGroupOptions.CreationTimeout));

                    DbConnectionInternal? connection =
                        OpenNewInternalConnection(owningConnection, CancellationToken.None, timeout);

                    if (connection is not null && !_idleChannel.TryWrite(new CreateOutcome(connection)))
                    {
                        // The channel was completed (pool shutting down) between creation and
                        // publish. Destroy the orphan rather than leaking it.
                        RemoveConnection(connection);
                    }
                }
                catch (Exception ex)
                {
                    // Deliver the error to a waiter so the originating request surface sees a real
                    // failure instead of only timing out. The linger guard avoids leaving a stale
                    // error in the channel for an unrelated future caller when nobody is waiting.
                    if (Volatile.Read(ref _waiterCount) > 0)
                    {
                        _idleChannel.TryWrite(new CreateOutcome(ExceptionDispatchInfo.Capture(ex)));
                    }
                    else
                    {
                        SqlClientEventSource.Log.TryPoolerTraceEvent(
                            "<prov.DbConnectionPool.LaunchCreate|RES|CPOOL> {0}, create failed with no waiter to receive it: {1}", Id, ex);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingCreates);

                    // Re-drive remaining waiters. A failed create frees its slot and a no-slot
                    // create may run once capacity opens, so nudge a parked waiter to re-evaluate.
                    // Guarded so stale wakes don't accumulate when nobody is waiting.
                    if (Volatile.Read(ref _waiterCount) > 0)
                    {
                        _idleChannel.TryWrite(default);
                    }
                }
            });
        }

        /// <summary>
        /// Gets an internal connection from the pool. Every request queues on the idle/creation
        /// channel - there is no optimistic fast path around it - which gives strict FIFO ordering
        /// to parked waiters. A request signals demand via the pump, which creates connections on
        /// background tasks as capacity allows, then blocks on the channel until an outcome arrives.
        /// </summary>
        /// <param name="owningConnection">The DbConnection that will own this internal connection.</param>
        /// <param name="async">A boolean indicating whether the operation should be asynchronous.</param>
        /// <param name="timeout">The overall timeout budget for this request. It bounds only how long
        /// the request waits on the channel; physical connection creation uses the pool's
        /// CreationTimeout instead.</param>
        /// <returns>Returns a DbConnectionInternal that is retrieved from the pool.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when an OperationCanceledException is caught, indicating that the timeout period
        /// elapsed prior to obtaining a connection from the pool.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when a ChannelClosedException is caught, indicating that the connection pool
        /// has been shut down. Also rethrows a background creation error delivered to this waiter.
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

            // Continue looping until we retrieve a live connection.
            do
            {
                CreateOutcome outcome;

                // Fast check for an immediately-available outcome. This is FIFO-safe: a buffered
                // item only exists when no reader is parked (the channel hands writes directly to
                // parked readers), so this cannot barge ahead of a waiting request.
                if (!_idleChannel.TryRead(out outcome))
                {
                    // Nothing buffered. Register demand *before* pumping and parking so the pump and
                    // any concurrent slot-freeing event both observe this waiter (see _waiterCount).
                    Interlocked.Increment(ref _waiterCount);
                    try
                    {
                        TryPumpCreate(owningConnection);

                        // Block until an outcome arrives. Channels guarantee FIFO delivery to parked
                        // ReadAsync callers, which is what preserves fair ordering here.
                        if (async)
                        {
                            outcome = await _idleChannel.ReadAsync(cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            outcome = ReadChannelSyncOverAsync(cancellationToken);
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
                    finally
                    {
                        Interlocked.Decrement(ref _waiterCount);
                    }
                }

                // A background create failed; propagate its error to this waiter.
                outcome.Error?.Throw();

                connection = outcome.Connection;

                if (connection is not null && !IsLiveConnection(connection))
                {
                    // Stale or dead connection: remove it (which frees its slot and re-drives
                    // waiters) and loop to try again. A bare-wake outcome (no connection, no error)
                    // also lands here as null and simply loops.
                    RemoveConnection(connection);
                    connection = null;
                }
            }
            while (connection is null);

            PrepareConnection(owningConnection, connection);
            return connection;
        }

        /// <summary>
        /// Performs a blocking synchronous read from the idle/creation channel.
        /// </summary>
        /// <param name="cancellationToken">Cancels the read operation.</param>
        /// <returns>The outcome read from the channel.</returns>
        private CreateOutcome ReadChannelSyncOverAsync(CancellationToken cancellationToken)
        {
            // If there are no outcomes in the channel, then ReadAsync will block until one is available.
            // Channels doesn't offer a sync API, so running ReadAsync synchronously on this thread may spawn
            // additional new async work items in the managed thread pool if there are no items available in the
            // channel. We need to ensure that we don't block all available managed threads with these child
            // tasks or we could deadlock. Prefer to block the current user-owned thread, and limit throughput
            // to the managed threadpool.

            _syncOverAsyncSemaphore.Wait(cancellationToken);
            try
            {
                ConfiguredValueTaskAwaitable<CreateOutcome>.ConfiguredValueTaskAwaiter awaiter =
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
                && _idleChannel.TryRead(out var outcome))
            {
                if (outcome.Connection is null)
                {
                    continue;
                }

                RemoveConnection(outcome.Connection);
                count--;
            }
        }
        #endregion
    }
}

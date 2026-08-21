// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
using Microsoft.Data.SqlClient.Diagnostics;
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
        /// Serializes emancipated-connection sweeps. Held for the duration of a sweep, including the
        /// routing of every reclaimed connection, so that a connection this sweep has selected
        /// cannot be claimed by another sweep before it is returned.
        /// </summary>
        private readonly object _reclaimSweepGate = new();

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
        /// Time source for idle-timeout expiry and the blocking-period exit timer. Defaults to
        /// <see cref="TimeProvider.System"/> in production; tests inject a fake provider so idle
        /// expiry and the blocking period can be driven deterministically without real waits.
        /// </summary>
        private readonly TimeProvider _timeProvider;

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
            ISqlClientMetrics? metrics = null)
        {
            ConnectionFactory = connectionFactory;
            // metrics is injected only by tests, so a pool's counters can be asserted without
            // interference from unrelated connection activity elsewhere in the process.
            Metrics = metrics ?? SqlClientDiagnostics.Metrics;
            PoolGroup = connectionPoolGroup;
            PoolGroupOptions = connectionPoolGroup.PoolGroupOptions;
            ProviderInfo = connectionPoolProviderInfo;
            Identity = identity;
            AuthenticationContexts = new();
            MaxPoolSize = Convert.ToUInt32(PoolGroupOptions.MaxPoolSize);
            TransactedConnectionPool = new(this, Metrics);
            _connectionCreationRateLimiter = connectionCreationRateLimiter;
            // timeProvider is injected only by tests so idle-timeout expiry and the blocking-period
            // exit timer can be driven deterministically; in production it is null and falls back to
            // TimeProvider.System, whose UTC time equals DateTime.UtcNow.
            _timeProvider = timeProvider ?? TimeProvider.System;

            _connectionSlots = new(MaxPoolSize);
            _idleChannel = new(Metrics);
            if (PoolGroup.IsBlockingPeriodEnabled())
            {
                _errorState = new BlockingPeriodErrorState(_instanceId, timeProvider: _timeProvider);
            }

            // Pruning is only useful when the pool can grow beyond MinPoolSize and idle
            // connections are subject to reclamation. If min >= max the pool is fixed-size so
            // pruning would never activate; if Connection Idle Timeout is zero, idle connections
            // are never reclaimed and there is nothing to prune. The pruning window (and thus how
            // many samples are collected) is derived from IdleTimeout.
            if (MinPoolSize < MaxPoolSize && PoolGroupOptions.IdleTimeout != TimeSpan.Zero)
            {
                Pruner = new PoolPruner(this, PoolGroupOptions.IdleTimeout);
            }

            Reclaimer = new PoolReclaimer(this, _timeProvider);

            State = Running;

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.ChannelDbConnectionPool | INFO | {0}, Constructed. MinPoolSize={1}, MaxPoolSize={2}",
                Id,
                MinPoolSize,
                MaxPoolSize);
        }

        #region Properties
        /// <inheritdoc />
        public ConcurrentDictionary<
            DbConnectionPoolAuthenticationContextKey,
            DbConnectionPoolAuthenticationContext> AuthenticationContexts { get; }

        /// <inheritdoc />
        public SqlConnectionFactory ConnectionFactory { get; }

        /// <inheritdoc />
        public ISqlClientMetrics Metrics { get; }

        /// <inheritdoc />
        /// <remarks>
        /// Reports connections that actually belong to the pool, not
        /// <see cref="ConnectionPoolSlots.ReservationCount"/>, which also counts reservations held
        /// for connections that are still being opened. The distinction matters to the SQL Express
        /// user instance path in <see cref="SqlConnectionFactory.CreateConnection"/>: it treats
        /// <c>Count &lt;= 0</c> as "nothing in the pool yet", opens a probe connection, and caches the
        /// resolved instance name on the pool's provider info. Counting an in-flight open here sends
        /// the first caller down the cached branch instead, where it reads an instance name that
        /// nothing has set yet.
        ///
        /// Internal sizing decisions (the max-pool-size gate, warmup, and pruning) all use
        /// <see cref="ConnectionPoolSlots.ReservationCount"/> instead, so that connections another
        /// thread is currently opening count toward the pool's size.
        /// </remarks>
        public int Count => _connectionSlots.ConnectionCount;

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

        /// <summary>
        /// Indicates whether the pool automatically enlists connections in the ambient transaction.
        /// When disabled, the ambient transaction is neither used to consult the
        /// <see cref="TransactedConnectionPool"/> nor handed to activation.
        ///
        /// This governs the ambient transaction only. A caller may still enlist explicitly via
        /// SqlConnection.EnlistTransaction, and such a connection is parked in the transacted store
        /// on return regardless of this flag, since it is bound to a transaction either way.
        /// </summary>
        private bool HasTransactionAffinity => PoolGroupOptions.HasTransactionAffinity;

        /// <summary>
        /// Drives background sweeps for emancipated connections. Unlike <see cref="Pruner"/> this is
        /// always present, since reclamation applies to every pool configuration. Internal rather
        /// than private so tests can drive the timer bookkeeping directly.
        /// </summary>
        internal PoolReclaimer Reclaimer { get; }

        /// <summary>
        /// The most recently launched warmup/replenishment loop task, exposed so tests can await a
        /// warmup pass to a deterministic completion instead of polling pool counters. May be null
        /// (warmup never requested) or reference an already-completed pass (requests are coalesced);
        /// a caller that only needs "some warmup pass has finished" can await it regardless. Only
        /// consumed by unit tests, which read it after synchronously triggering warmup on their own
        /// thread, so a plain auto-property is sufficient.
        /// </summary>
        internal Task? WarmupLoopTask { get; private set; }
        #endregion

        #region Methods
        /// <inheritdoc />
        public void Clear()
        {
            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.Clear | INFO | {0}, Clearing.", Id);

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
                    "ChannelDbConnectionPool.Clear | INFO | {0}, Skip drain, already clearing.", Id);
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
                "ChannelDbConnectionPool.Clear | INFO | {0}, Cleared.", Id);
        }

        /// <inheritdoc />
        public void PutObjectFromTransactedPool(DbConnectionInternal connection)
        {
            Debug.Assert(connection.EnlistedTransaction is null,
                "PutObjectFromTransactedPool was called with a connection that is still enlisted. " +
                "The transaction must have ended and been detached before the connection returns to " +
                "general circulation, otherwise it could be vended to a caller in a different transaction.");

            // Called by the transacted connection pool once it has removed the connection from its
            // list. We put the connection back into general circulation.
            //
            // NOTE: no locking is required here because if we're in this method we can safely
            // presume that the caller is the only one using the connection, that all pre-push logic
            // has been done, and that all transactions have ended.
            if (State is Running && connection.CanBePooled)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.PutObjectFromTransactedPool | INFO | {0}, Connection {1}, Transaction has ended; returning connection to pool.",
                    Id,
                    connection.ObjectID);

                connection.ResetConnection();

                // probeLiveness: false because we are running on the System.Transactions
                // transaction-completion callback thread (see DbConnectionInternal
                // .DelegatedTransactionEnded, whose contract requires the caller to hold a lock on
                // the connection). DbConnectionInternal.IsConnectionAlive polls the socket, and we
                // do not want to do socket work while holding that lock on a thread we do not own.
                // WaitHandleDbConnectionPool does not probe here either; it simply calls
                // ResetConnection followed by PutNewObject. The connection is still validated on
                // its way back out of the idle channel, so a connection that died during the
                // transaction is detected before it is vended. The idle-expiry, load-balance and
                // clear-generation checks all still run below; only the socket poll is skipped.
                PutConnectionInIdleChannel(connection, probeLiveness: false);
            }
            else
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.PutObjectFromTransactedPool | INFO | {0}, Connection {1}, Transaction has ended; destroying unpoolable connection.",
                    Id,
                    connection.ObjectID);

                // RemoveConnection triggers replenishment, which is the channel pool's equivalent
                // of the wait handle pool's QueuePoolCreateRequest.
                RemoveConnection(connection);
            }
        }

        /// <inheritdoc />
        public DbConnectionInternal ReplaceConnection(
            DbConnection owningObject,
            DbConnectionInternal oldConnection,
            TimeoutTimer timeout)
        {
            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.ReplaceConnection | INFO | {0}, replacing connection.", Id);

            // First, prefer to get an idle connection from the pool. 
            // If one is available, we can avoid the cost of creating a new connection.
            DbConnectionInternal? newConnection = GetIdleConnection();

            if (newConnection is not null)
            {
                // Carry the old connection's enlistment over to the replacement so that a connection
                // replaced mid-transaction stays bound to the same transaction.
                PrepareConnection(owningObject, newConnection, oldConnection.EnlistedTransaction);

                // newConnection came from the idle channel, so it already holds a slot of its own.
                // Releasing oldConnection's slot here keeps the pool's count accurate. This is
                // deliberately different from the create-new branch below, which hands oldConnection's
                // slot to the replacement via _connectionSlots.TryReplace and therefore only disposes
                // it -- calling RemoveConnection there would signal a free slot that does not exist.
                oldConnection.DeactivateConnection();

                // RemoveConnection early-returns without freeing the slot when the connection is a
                // transaction root awaiting its transaction's end, which would leave the pool
                // holding two reservations for one logical connection. That cannot happen here:
                // IsTxRootWaitingForTxEnd is set only by SetInStasis, and the only path that puts a
                // pooled connection in stasis is ReturnInternalConnection. oldConnection is checked
                // out by owningObject at this point, so it has not been returned and cannot be in
                // stasis. Being a transaction root is not sufficient; the connection must also have
                // been returned. The slot is therefore always released here.
                //
                // If that ever changes, the outcome is a transient over-reservation rather than a
                // leak: the connection comes back through PutObjectFromTransactedPool when its
                // transaction ends, which calls RemoveConnection again once stasis has been
                // terminated, and the slot is reclaimed then.
                RemoveConnection(oldConnection);
            }
            else
            {
                _errorState?.ThrowIfActive();

                // Unlike OpenNewInternalConnection, this direct create intentionally bypasses
                // _connectionCreationRateLimiter. This mirrors the behavior in WaitHandleDbConnectionPool.ReplaceConnection.
                try
                {
                    newConnection = ConnectionFactory.CreatePooledConnection(owningObject, this, timeout);
                }
                catch (Exception ex) when (ADP.IsCatchableExceptionType(ex) && ex is not OperationCanceledException)
                {
                    // A failed physical open means the server is unreachable, so enter the blocking
                    // period exactly as OpenNewInternalConnection and WaitHandleDbConnectionPool.CreateObject
                    // do: subsequent opens fast-fail until the period expires. Activation failures in the
                    // try below are intentionally excluded -- the server proved reachable -- matching the
                    // WaitHandle pool, where PrepareConnection runs outside CreateObject's error-state catch.
                    // We exclude OperationCanceledException (caller-side timeout/cancellation, not a physical
                    // failure) and only enter while Running, mirroring OpenNewInternalConnection.
                    if (State == Running)
                    {
                        _errorState?.Enter(ex);
                    }

                    throw;
                }

                try
                {
                    newConnection.ClearGeneration = _clearGeneration;

                    lock (newConnection)
                    {
                        // PostPop requires a lock on the connection.
                        newConnection.PostPop(owningObject);
                    }

                    // Carry the old connection's enlistment over to the replacement so that a
                    // connection replaced mid-transaction stays bound to the same transaction.
                    newConnection.ActivateConnection(oldConnection.EnlistedTransaction);

                    // Place new into old's slot
                    bool replaced = _connectionSlots.TryReplace(oldConnection, newConnection);

                    if (!replaced)
                    {
                        // Should never happen (oldConnection is checked out, so its slot is stable),
                        // but guard against vending a connection the pool isn't tracking.
                        throw new InvalidOperationException(StringsHelper.GetString(Strings.SQL_ConnectionPoolReplaceConnectionFailed));
                    }
                }
                catch
                {
                    try
                    {
                        newConnection.DeactivateConnection();
                    }
                    catch
                    {
                        // Preserve the original failure; best-effort cleanup only.
                    }

                    newConnection.Dispose();

                    // The physical connection was opened (and counted by HardConnectRequest in the
                    // factory) before activation failed, so balance the counter here. The
                    // connection never occupied a slot, so the pooled gauge is untouched.
                    Metrics.HardDisconnectRequest();
                    throw;
                }

                // A successful open clears the blocking period, mirroring OpenNewInternalConnection.
                _errorState?.Clear();

                // Only retire the old connection after the replacement is fully activated and we know we won't fail.
                oldConnection.DeactivateConnection();
                oldConnection.Dispose();

                // The replacement took over the old connection's slot, so the pooled gauge is
                // already correct. The old connection was vended to the caller and is now destroyed
                // rather than returned, so balance both the soft gauge (it was counted as a
                // checkout) and the hard gauge (its physical connection is going away). Traced as a
                // destroy so the connection's exit is visible in the pooler trace stream.
                Metrics.SoftDisconnectRequest();
                Metrics.HardDisconnectRequest();

                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.ReplaceConnection | INFO | {0}, Connection {1}, Disposed.",
                    Id,
                    oldConnection.ObjectID);
            }

            Metrics.SoftConnectRequest();

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.ReplaceConnection | INFO | {0}, connection replaced successfully.", Id);

            return newConnection;
        }

        /// <inheritdoc />
        public void ReturnInternalConnection(DbConnectionInternal connection, DbConnection? owningObject)
        {
            Metrics.SoftDisconnectRequest();

            ValidateOwnershipAndSetPoolingState(connection, owningObject);

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.ReturnInternalConnection | INFO | {0}, Connection {1}, Deactivating.",
                Id,
                connection.ObjectID);

            // Deactivate before inspecting the connection, because DeactivateConnection mutates both
            // of the gates we branch on below:
            //  - Deactivate() dooms the connection when async commands are still outstanding, which
            //    the IsConnectionDoomed check must observe.
            //  - DeactivateConnection dooms it via DoNotPoolThisConnection when the load-balance
            //    timeout has elapsed, which the CanBePooled check must observe.
            // WaitHandleDbConnectionPool.DeactivateObject orders it the same way.
            connection.DeactivateConnection();

            if (connection.IsConnectionDoomed)
            {
                // The connection is not fit for reuse -- just dispose of it.
                RemoveConnection(connection);
                return;
            }

            // Note: this logic mirrors WaitHandleDbConnectionPool.ReturnObject, minus one dead
            // branch. Its Running path also checks IsTransactionRoot && Pool == null -> SetInStasis,
            // under its own "how did we get here if the pool is null?" TODO. A connection cannot
            // arrive here without a pool, because this method is called through the connection's own
            // Pool reference. The branch is redundant in any case: putting a transaction root in
            // stasis is exactly what the first case below does when the connection cannot be pooled.
            ReturnDisposition disposition;
            lock (connection)
            {
                if (State is not Running || !connection.CanBePooled)
                {
                    // A transaction root that cannot be pooled must be put in stasis rather than
                    // closed. Closing it would orphan the root transaction with no means to promote
                    // itself to a full delegated transaction, or to commit or roll back.
                    // System.Transactions keeps the connection owned (not lost) and is certain to
                    // call the appropriate callback when the transaction ends.
                    if (connection.IsTransactionRoot)
                    {
                        connection.SetInStasis();
                        disposition = ReturnDisposition.HeldByTransaction;
                    }
                    else
                    {
                        disposition = ReturnDisposition.Destroy;
                    }
                }
                else if (connection.EnlistedTransaction is { } transaction)
                {
                    // A connection that is still enlisted cannot be handed to a different customer
                    // until its transaction actually completes, so it is parked in the transacted
                    // store keyed by that transaction and comes back via
                    // PutObjectFromTransactedPool when it ends.
                    //
                    // Transacted connections are deliberately not stamped with a returned time:
                    // they are never proactively closed (doing so would abort a possibly
                    // distributed transaction), so idle-timeout enforcement does not apply while
                    // they are parked. They are stamped when they rejoin the idle channel in
                    // PutObjectFromTransactedPool.
                    TransactedConnectionPool.PutTransactedObject(transaction, connection);
                    disposition = ReturnDisposition.HeldByTransaction;
                }
                else
                {
                    disposition = ReturnDisposition.Reuse;
                }
            }

            switch (disposition)
            {
                case ReturnDisposition.Reuse:
                    PutConnectionInIdleChannel(connection);
                    break;

                case ReturnDisposition.Destroy:
                    RemoveConnection(connection);
                    break;

                case ReturnDisposition.HeldByTransaction:
                    // Nothing further to do. The connection is parked in the transacted store or
                    // in stasis, and comes back through PutObjectFromTransactedPool once its
                    // transaction ends. Neither path returns it to the idle channel, so without
                    // this trace the connection simply disappears from the pool's trace stream
                    // after deactivation.
                    SqlClientEventSource.Log.TryPoolerTraceEvent(
                        "ChannelDbConnectionPool.ReturnInternalConnection | INFO | {0}, Connection {1}, Held by a transaction; not returned to the general pool.",
                        Id,
                        connection.ObjectID);
                    break;
            }
        }

        /// <summary>
        /// The outcome of evaluating a connection that is being returned to the pool.
        /// </summary>
        private enum ReturnDisposition
        {
            /// <summary>
            /// The connection is fit for general reuse and belongs in the idle channel.
            /// </summary>
            Reuse,

            /// <summary>
            /// The connection cannot be reused and must be closed.
            /// </summary>
            Destroy,

            /// <summary>
            /// The connection is owned by a live transaction, either parked in the transacted
            /// store or held in stasis, and must not be touched by the pool until that
            /// transaction ends.
            /// </summary>
            HeldByTransaction,
        }

        /// <summary>
        /// Places a connection that is fit for general reuse into the idle channel, stamping its
        /// idle-return time and dropping it if it is no longer live.
        /// </summary>
        /// <param name="connection">The connection to make available to other callers.</param>
        /// <param name="probeLiveness">
        /// Whether to poll the physical connection to confirm it is still alive. Pass false when
        /// running on a thread that must not block, such as a System.Transactions completion
        /// callback. The idle-expiry, load-balance timeout and clear-generation checks run
        /// regardless; only the socket poll is suppressed.
        /// </param>
        private void PutConnectionInIdleChannel(DbConnectionInternal connection, bool probeLiveness = true)
        {
            // Stamp the return time before IsLiveConnection runs so the idle-expiry gate inside it
            // measures time-in-pool, not time-since-last-return. Without this, a connection whose
            // checkout exceeded IdleTimeout (e.g. a long-running query) would be wrongly evicted on
            // return even though it was actively in use on the wire. The same gating conditions are
            // applied here as in IsLiveConnection so we avoid the per-return timestamp read when
            // idle expiry is disabled or the legacy idle-timeout behavior is in effect.
            //
            // A connection parked in the transacted store does not pass through here, so it is not
            // subject to idle timeout for as long as its transaction is live. That is intentional
            // and matches WaitHandleDbConnectionPool: closing it would abort a possibly distributed
            // transaction. It is stamped here when the transaction ends and
            // PutObjectFromTransactedPool returns it to general circulation, so the idle clock
            // starts from the moment it actually becomes available to other callers.
            if (!LocalAppContextSwitches.UseLegacyIdleTimeoutBehavior &&
                PoolGroupOptions.IdleTimeout != TimeSpan.Zero)
            {
                connection.SetReturnedTime(_timeProvider.GetUtcNow().UtcDateTime);
            }

            if (!IsLiveConnection(connection, probeLiveness))
            {
                RemoveConnection(connection);
                return;
            }

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.PutConnectionInIdleChannel | INFO | {0}, Connection {1}, Writing to idle channel.",
                Id,
                connection.ObjectID);

            if (!_idleChannel.TryWrite(connection))
            {
                // The channel has been completed (pool is shutting down). Race window
                // between the State check by the caller and TryWrite: destroy instead of pooling.
                RemoveConnection(connection);
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
                "ChannelDbConnectionPool.Shutdown | INFO | {0}", Id);

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
                    "ChannelDbConnectionPool.Shutdown | INFO | {0}, _warmupCts.Cancel threw, continuing shutdown: {1}", Id, ex);
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
                    "ChannelDbConnectionPool.Shutdown | INFO | {0}, Pruner.Dispose threw, continuing shutdown: {1}", Id, ex);
            }

            // Best effort: ITimer.Dispose does not wait for a sweep already in flight. Late
            // reclaims are handled by _idleChannel.Complete() below, after which connections are
            // destroyed rather than pooled.
            try
            {
                Reclaimer.Dispose();
            }
            catch (Exception ex)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.Shutdown | INFO | {0}, Reclaimer.Dispose threw, continuing shutdown: {1}", Id, ex);
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
                    "ChannelDbConnectionPool.Shutdown | INFO | {0}, _errorState.Dispose threw, continuing shutdown: {1}", Id, ex);
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
                    "ChannelDbConnectionPool.Shutdown | INFO | {0}, Clear threw, continuing shutdown: {1}", Id, ex);
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
                        "ChannelDbConnectionPool.Shutdown | INFO | {0}, RemoveConnection threw during drain, continuing: {1}", Id, ex);
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
                    "ChannelDbConnectionPool.Shutdown | INFO | {0}, _warmupCts.Dispose threw, continuing shutdown: {1}", Id, ex);
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
                "ChannelDbConnectionPool.Startup | INFO | {0}", Id);

            // Kick off background warmup so the pool pre-creates connections up to MinPoolSize
            // without blocking the caller (Story 1). No-op when MinPoolSize == 0.
            RequestWarmup();
        }

        /// <inheritdoc />
        public void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject)
        {
            // Note: the connection may still be associated with the transaction due to the explicit
            // unbinding requirement.
            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.TransactionEnded | INFO | {0}, Transaction {1}, Connection {2}, Transaction Completed",
                Id,
                transaction.GetHashCode(),
                transactedObject.ObjectID);

            // Removal from the transacted list happens synchronously inside this call, and
            // TransactedConnectionPool.TransactionEnded calls back into PutObjectFromTransactedPool
            // itself to return the connection to general circulation.
            //
            // We deliberately do not call PutObjectFromTransactedPool ourselves afterwards: that
            // callback is conditional on the connection actually having been found in the list.
            // A transaction can complete while the application still holds the connection, in which
            // case the connection was never parked, and returning it here would hand a connection
            // that is still in use to another caller. In that case the connection instead reaches
            // the pool through the normal ReturnInternalConnection path when it is closed.
            // This mirrors WaitHandleDbConnectionPool.TransactionEnded.
            TransactedConnectionPool.TransactionEnded(transaction, transactedObject);
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
                    "ChannelDbConnectionPool.TryGetConnection | INFO | {0}, State != Running.", Id);
                connection = null;
                return true;
            }

            // If taskCompletionSource is null, we are in a sync context.
            if (taskCompletionSource is null)
            {
                // We're on the caller's thread, so the ambient transaction is directly observable.
                var task = GetInternalConnection(
                        owningObject,
                        async: false,
                        timeout,
                        ADP.GetCurrentTransaction());

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
            //
            // The ambient transaction is captured by the caller, on the caller's thread, and handed
            // to us in the TaskCompletionSource's AsyncState (see SqlConnection.InternalOpenAsync).
            //
            // We must not read Transaction.Current inside the Task.Run below. A
            // TransactionScope created with TransactionScopeAsyncFlowOption.Enabled stores the
            // transaction in an AsyncLocal, which does flow onto the pool's worker thread, so that
            // would appear to work. But Enabled is not the default: a plain TransactionScope keeps
            // the transaction in thread-static storage, which does not flow, and reading
            // Transaction.Current on the worker would silently fail to enlist. The WaitHandle pool
            // enlists correctly in that case, so this is also a compatibility requirement.
            // AsyncState is correct under both options.
            //
            // This does not make the suppressed-flow pattern work -- the caller's own scope is
            // still broken past the first await -- but it keeps the connection in the transaction
            // the caller intended rather than silently running outside it.
            //
            // Note that we deliberately do not assign Transaction.Current on the thread pool
            // thread either. That assignment writes to thread-static storage which is *not* unwound
            // when the ExecutionContext is restored, so it would outlive this open and be observed
            // by unrelated work later scheduled onto the same thread pool thread -- including the
            // login-time auto-enlistment that non-pooled connections perform against
            // Transaction.Current. The WaitHandle pool can get away with assigning it because it
            // processes pending opens on a dedicated non-thread-pool thread.
            Transaction? ambientTransaction = taskCompletionSource.Task.AsyncState as Transaction;

            Task.Run(async () =>
            {
                if (taskCompletionSource.Task.IsCompleted)
                {
                    return;
                }

                DbConnectionInternal? connection = null;

                try
                {
                    connection = await GetInternalConnection(
                        owningObject,
                        async: true,
                        timeout,
                        ambientTransaction
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
        /// <param name="timeout">The overall timeout budget. Passed through to the physical connection
        /// so it uses the remaining budget rather than starting a fresh timeout.</param>
        /// <param name="cancellationToken">An optional cancellation token used by background warmup.
        /// Caller timeout cancellation is reserved for pool waits so physical connection failures
        /// retain the same exception behavior as the legacy pool.</param>
        /// <returns>The new internal connection, or null if the pool has no available slot or the
        /// rate limiter is currently saturated. In the latter case the caller should fall back to
        /// the idle-channel wait; the rate limiter will write a null to the idle channel when a
        /// permit is released so the waiter can retry.</returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the cancellation token is cancelled before the connection operation completes.
        /// </exception>
        private DbConnectionInternal? OpenNewInternalConnection(
            DbConnection? owningConnection,
            TimeoutTimer timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Fast-fail if the pool is in the blocking-period error state. FR-006. Warmup goes
            // through this same path (it has no isWarmup exemption): it mirrors the legacy WaitHandle
            // pool, whose replenishment enters/clears the same error state as user requests. In
            // practice the warmup loop already stands down before reaching here (its loop condition
            // checks ErrorOccurred); this covers the narrow race where the state flips in between.
            if (ErrorOccurred)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.OpenNewInternalConnection | INFO | {0}, Errors are set.", Id);
            }

            _errorState?.ThrowIfActive();

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.OpenNewInternalConnection | INFO | {0}, Creating new connection.", Id);

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
                                SqlClientEventSource.Log.TryPoolerTraceEvent(
                                    "ChannelDbConnectionPool.OpenNewInternalConnection | INFO | {0}, Rate limiter saturated; deferring creation to the idle wait.",
                                    Id);
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

                        if (newConnection is not null)
                        {
                            // The connection opened, so a hard connect was counted for it. It never
                            // reached the pool, so balance the hard-connection gauge here rather
                            // than leaving it inflated for the lifetime of the pool.
                            newConnection.Dispose();
                            Metrics.HardDisconnectRequest();
                        }
                    });

                if (connection is not null)
                {
                    SqlClientEventSource.Log.TryPoolerTraceEvent(
                        "ChannelDbConnectionPool.OpenNewInternalConnection | INFO | {0}, Connection {1}, Added to pool.",
                        Id,
                        connection.ObjectID);

                    Metrics.EnterPooledConnection();

                    // A new connection was added to the pool. If we've grown past MinPoolSize,
                    // start the pruning timer so idle connections can be reclaimed.
                    Pruner?.UpdateTimer();

                    // A successful creation clears error/backoff state (FR-009). Warmup goes through
                    // this same path and clears the state on success too, mirroring the legacy
                    // WaitHandle pool: a connection that opens proves the server is reachable.
                    _errorState?.Clear();
                }
                else
                {
                    SqlClientEventSource.Log.TryPoolerTraceEvent(
                        "ChannelDbConnectionPool.OpenNewInternalConnection | INFO | {0}, No connection created; pool is full or creation is rate limited.",
                        Id);
                }

                return connection;
            }
            catch (Exception ex) when (ADP.IsCatchableExceptionType(ex) && ex is not OperationCanceledException)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.OpenNewInternalConnection | INFO | {0}, PoolCreateRequest called CreateConnection which threw an exception: {1}",
                    Id,
                    ex);

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
                //
                // Only enter the error state while the pool is still Running. A synchronous physical
                // open cannot be cancelled once it is in progress (see _warmupCts field docs), so a
                // warmup/user open that began before Shutdown can complete with a failure after
                // Shutdown has already disposed _errorState. Entering here would re-arm the exit
                // timer and keep callbacks/logging alive past teardown, so we skip it once shutdown
                // has begun; the throw below still propagates the failure to the caller/warmup loop.
                if (State == Running)
                {
                    _errorState?.Enter(ex);
                }

                throw;
            }
        }

        /// <summary>
        /// Checks that the provided connection is live and unexpired and closes it if needed.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="probeLiveness">
        /// Whether to poll the physical connection to confirm it is still alive. Pass false when
        /// running on a thread that must not block; the remaining checks are all cheap and local.
        /// </param>
        /// <returns>Returns true if the connection is live and unexpired, otherwise returns false.</returns>
        private bool IsLiveConnection(DbConnectionInternal connection, bool probeLiveness = true)
        {
            // Connection has been sitting idle longer than the configured idle timeout.
            // Checked before the (potentially expensive) liveness probe so an idle-expired
            // connection is discarded without an SNI round-trip.
            // ReturnedTime is initialized to CreateTime so a freshly minted connection never trips this
            // check on first retrieval, and is then stamped by ReturnInternalConnection on every return.
            // Uses the pool's TimeProvider (TimeProvider.System in production) so the return stamp and
            // this expiry check read the same clock, letting tests drive idle expiry deterministically.
            // Use subtraction rather than addition so the comparison cannot throw if ReturnedTime is
            // ever close to DateTime.MaxValue. A clock skew that leaves ReturnedTime in the future
            // produces a negative TimeSpan, which falls through as not-expired (fail safe).
            TimeSpan idleTimeout = PoolGroupOptions.IdleTimeout;
            if (!LocalAppContextSwitches.UseLegacyIdleTimeoutBehavior &&
                idleTimeout != TimeSpan.Zero &&
                _timeProvider.GetUtcNow().UtcDateTime - connection.ReturnedTime > idleTimeout)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.IsLiveConnection | INFO | {0}, Connection {1}, exceeded the connection idle timeout and removed.",
                    Id,
                    connection.ObjectID);
                return false;
            }

            // Broken physical connection. Skipped when the caller cannot afford to block: this
            // polls the socket, so it must not run on a thread we do not own.
            if (probeLiveness && !connection.IsConnectionAlive())
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.IsLiveConnection | INFO | {0}, Connection {1}, found dead and removed.",
                    Id,
                    connection.ObjectID);
                return false;
            }

            // Connection has been alive longer than the load balance timeout
            if (LoadBalanceTimeout != TimeSpan.Zero && DateTime.UtcNow > connection.CreateTime + LoadBalanceTimeout)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.IsLiveConnection | INFO | {0}, Connection {1}, exceeded the load balance timeout and removed.",
                    Id,
                    connection.ObjectID);
                return false;
            }

            // Connection was created before the last Clear, so it's stale.
            if (connection.ClearGeneration != _clearGeneration)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.IsLiveConnection | INFO | {0}, Connection {1}, was created before the last Clear and removed.",
                    Id,
                    connection.ObjectID);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Closes the provided connection and removes it from the pool, freeing its slot.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Not guaranteed: a connection that is a transaction root awaiting its transaction's end
        /// is left alone and its slot stays reserved, because disposing it would abort a possibly
        /// distributed transaction. Callers that treat this method as "the slot is now free" must
        /// tolerate that, including <see cref="Clear"/> and the <see cref="Shutdown"/> drain, which
        /// can therefore complete while the pool still owns a connection.
        /// </para>
        /// <para>
        /// In practice no current caller reaches that early return, because a connection in stasis
        /// is filed in neither the idle channel nor the transacted store, and every caller sources
        /// its connection from one of those or from a connection that is still checked out. The
        /// guard is retained to match WaitHandleDbConnectionPool.DestroyObject and to keep the
        /// invariant enforced if a future caller does reach it.
        /// </para>
        /// </remarks>
        /// <param name="connection">The connection to be closed.</param>
        private void RemoveConnection(DbConnectionInternal connection)
        {
            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.RemoveConnection | INFO | {0}, Connection {1}, Removing from pool.",
                Id,
                connection.ObjectID);

            // A connection with a delegated transaction cannot be disposed of until the delegated
            // transaction has actually completed; disposing it would abort the (possibly
            // distributed) transaction. Leave it alone: when the transaction completes it comes
            // back through PutObjectFromTransactedPool, which calls us again.
            if (connection.IsTxRootWaitingForTxEnd)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.RemoveConnection | INFO | {0}, Connection {1}, Has Delegated Transaction, waiting to Dispose.",
                    Id,
                    connection.ObjectID);
                return;
            }

            if (_connectionSlots.TryRemove(connection))
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.RemoveConnection | INFO | {0}, Connection {1}, Removed from pool.",
                    Id,
                    connection.ObjectID);

                Metrics.ExitPooledConnection();
            }

            // Removing a connection from the pool opens a free slot.
            // Write a null to the idle connection channel to wake up a waiter, who can now open a new
            // connection.
            _idleChannel.TryWrite(null);

            connection.Dispose();
            Metrics.HardDisconnectRequest();

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.RemoveConnection | INFO | {0}, Connection {1}, Disposed.",
                Id,
                connection.ObjectID);

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

                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.GetIdleConnection | INFO | {0}, Connection {1}, Read from idle channel.",
                    Id,
                    connection.ObjectID);

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
        /// <param name="ambientTransaction">The ambient transaction captured on the caller's thread, or
        /// null when the caller is not inside a transaction. It is passed explicitly rather than read
        /// from <see cref="Transaction.Current"/> because this method may run on a thread pool thread
        /// that the ambient transaction does not flow to.</param>
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
            TimeoutTimer timeout,
            Transaction? ambientTransaction)
        {
            DbConnectionInternal? connection = null;

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.GetInternalConnection | INFO | {0}, Getting connection.", Id);

            // When automatic enlistment is disabled, the connection must never be bound to the
            // ambient transaction, so we neither consult the transacted store nor hand the
            // transaction to activation. HasTransactionAffinity is derived from the connection
            // string's Enlist keyword.
            Transaction? transaction = HasTransactionAffinity ? ambientTransaction : null;

            // Derive a CancellationTokenSource from the TimeoutTimer so pool-internal wait operations
            // (channel reads, semaphore waits) are cancelled when the overall budget expires.
            using CancellationTokenSource cancellationTokenSource = timeout.CreateCancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            // Continue looping until we create or retrieve a connection.
            while (connection is null)
            {
                try
                {
                    // A connection already enlisted in our transaction is always preferred, since
                    // reusing it avoids promoting the transaction to a distributed one. This is
                    // re-checked on every iteration so that a connection returned to the transacted
                    // store while we were looping is picked up rather than being passed over in
                    // favor of a fresh connection.
                    if (transaction is not null)
                    {
                        connection = GetFromTransactedPool(transaction);
                        if (connection is not null)
                        {
                            // Skip the liveness/idle/generation gate at the bottom of the loop:
                            // GetFromTransactedPool has already probed liveness, and a transacted
                            // connection is exempt from idle-timeout, load-balance and
                            // clear-generation eviction because closing it would abort its
                            // (possibly distributed) transaction.
                            break;
                        }
                    }

                    // Optimistically try to get an idle connection from the channel
                    // Doesn't wait if the channel is empty, just returns null.
                    connection ??= GetIdleConnection();


                    // If we didn't find an idle connection, try to open a new one. This may
                    // return null if the pool is full or the rate limiter is currently saturated;
                    // in either case the caller falls through to the idle-channel wait below.
                    connection ??= OpenNewInternalConnection(
                        owningConnection,
                        timeout);

                    // If we're at max capacity and couldn't open a connection. Block on the idle channel with a
                    // timeout. Note that Channels guarantee fair FIFO behavior to callers of ReadAsync
                    // (first-come, first-served), which is crucial to us.
                    //
                    // Registering with the reclaimer is what keeps a leaked connection from stranding
                    // us here forever; it sweeps on a timer while anyone is parked and routes what it
                    // reclaims back through this channel. Sweeping inline instead would cost an
                    // O(MaxPoolSize) walk on every saturated acquire in applications that never leak.
                    if (connection is null)
                    {
                        Reclaimer.EnterParkedWait();
                        try
                        {
                            connection = async
                                ? await _idleChannel.ReadAsync(cancellationToken).ConfigureAwait(false)
                                : ReadChannelSyncOverAsync(cancellationToken);
                        }
                        finally
                        {
                            Reclaimer.ExitParkedWait();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    SqlClientEventSource.Log.TryPoolerTraceEvent(
                        "ChannelDbConnectionPool.GetInternalConnection | INFO | {0}, Wait timed out.", Id);

                    throw ADP.PooledOpenTimeout();
                }
                catch (ChannelClosedException)
                {
                    SqlClientEventSource.Log.TryPoolerTraceEvent(
                        "ChannelDbConnectionPool.GetInternalConnection | INFO | {0}, Pool is shutting down; abandoning wait.", Id);
                    throw new InvalidOperationException(StringsHelper.GetString(Strings.SQL_ConnectionPoolShutDown));
                }

                if (connection is not null && !IsLiveConnection(connection))
                {
                    // If the connection is not live, we need to remove it from the pool and try again.
                    RemoveConnection(connection);
                    connection = null;
                }
            }

            // Counted before activation: if PrepareConnection fails it returns the connection to
            // the pool, which emits the matching soft disconnect. Counting after would leave that
            // disconnect unpaired and drive the active-soft-connects gauge negative.
            Metrics.SoftConnectRequest();
            PrepareConnection(owningConnection, connection, transaction);

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.GetInternalConnection | INFO | {0}, Connection {1}, Obtained.", Id, connection.ObjectID);

            return connection;
        }

        /// <summary>
        /// Reclaims connections whose owning <see cref="DbConnection"/> has been garbage collected
        /// without being closed or disposed. Such connections are still tracked by the pool but can
        /// never be returned by their owner, so without this sweep they would leak pool slots.
        /// </summary>
        internal void ReclaimEmancipatedConnections()
        {
            // One sweep at a time, so nothing else can claim a connection between the point it is
            // found emancipated and the PrePush that claims it. TryEnter rather than Enter: whatever
            // the in-flight sweep reclaims lands in the idle channel either way.
            bool sweeping = false;
            try
            {
                Monitor.TryEnter(_reclaimSweepGate, ref sweeping);
                if (!sweeping)
                {
                    return;
                }

                SweepEmancipatedConnections();
            }
            finally
            {
                if (sweeping)
                {
                    Monitor.Exit(_reclaimSweepGate);
                }
            }
        }

        /// <summary>
        /// Body of <see cref="ReclaimEmancipatedConnections"/>. Must be called with
        /// <see cref="_reclaimSweepGate"/> held.
        /// </summary>
        private void SweepEmancipatedConnections()
        {
            List<DbConnectionInternal>? reclaimed = null;

            // No collection-level lock, unlike WaitHandleDbConnectionPool's scan under lock
            // (_objectList): each slot is read individually, so the walk can see a slot that was
            // concurrently emptied or refilled. Safe here because a connection is only emancipated
            // while checked out, and a checked-out connection is not in the idle channel, so neither
            // the pruner nor Clear can remove it underneath us. A concurrently replaced slot costs
            // this sweep a miss, never a connection resurrected after removal.
            foreach (DbConnectionInternal connection in _connectionSlots)
            {
                // IsEmancipated is only stable under the connection lock, which guards the
                // PrePush/PostPop that move it in and out of the pool. TryEnter rather than Enter: a
                // connection someone else holds is mid-handout or mid-return, so it is not
                // emancipated anyway and blocking on it would only stall that caller.
                bool locked = false;
                try
                {
                    Monitor.TryEnter(connection, ref locked);

                    if (locked && connection.IsEmancipated)
                    {
                        (reclaimed ??= new List<DbConnectionInternal>()).Add(connection);
                    }
                }
                finally
                {
                    if (locked)
                    {
                        Monitor.Exit(connection);
                    }
                }
            }

            if (reclaimed is null)
            {
                return;
            }

            int returned = 0;
            foreach (DbConnectionInternal connection in reclaimed)
            {
                try
                {
                    connection.DetachCurrentTransactionIfEnded();
                    ReturnInternalConnection(connection, owningObject: null);
                }
                catch (Exception ex)
                {
                    // One connection failing to return must not strand the rest of the sweep.
                    SqlClientEventSource.Log.TryPoolerTraceEvent(
                        "ChannelDbConnectionPool.ReclaimEmancipatedConnections | ERR | {0}, Connection {1}, Return threw: {2}.",
                        Id,
                        connection.ObjectID,
                        ex);

                    continue;
                }

                Metrics.ReclaimedConnectionRequest();
                returned++;
            }

            if (returned > 0)
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.ReclaimEmancipatedConnections | INFO | {0}, Reclaimed {1} emancipated connection(s).",
                    Id,
                    returned);
            }
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
        /// <param name="transaction">The transaction to enlist the connection in, or null to activate cleanly.</param>
        /// <exception cref="Exception">
        /// Thrown when any exception occurs during connection activation.
        /// </exception>
        private void PrepareConnection(DbConnection owningObject, DbConnectionInternal connection, Transaction? transaction = null)
        {
            lock (connection)
            {
                // Protect against Clear which calls IsEmancipated, which is affected by PrePush and PostPop
                connection.PostPop(owningObject);
            }

            try
            {
                connection.ActivateConnection(transaction);
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
        /// Attempts to retrieve a connection that is already enlisted in the given transaction.
        /// </summary>
        /// <param name="transaction">The transaction the connection must already be enlisted in.</param>
        /// <returns>A live connection already enlisted in the transaction, or null.</returns>
        private DbConnectionInternal? GetFromTransactedPool(Transaction transaction)
        {
            DbConnectionInternal? connection = TransactedConnectionPool.GetTransactedObject(transaction);
            if (connection is null)
            {
                return null;
            }

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.GetFromTransactedPool | INFO | {0}, Transaction {1}, Connection {2}, Popped from transacted pool.",
                Id,
                transaction.GetHashCode(),
                connection.ObjectID);

            Metrics.ExitFreeConnection();

            // Transacting connections are exempt from idle-timeout and clear-generation eviction
            // (closing them would abort the transaction, which may be distributed), so only
            // liveness is checked here rather than the full IsLiveConnection gate.
            bool isAlive = false;
            try
            {
                // A dead transaction root must surface the underlying failure to the caller, since
                // there is no way to recover the delegated transaction on another connection. Any
                // other dead connection is simply reported so the caller can pick up or open a
                // different one. Either way the connection is dropped in the finally below.
                isAlive = connection.IsConnectionAlive(throwOnException: connection.IsTransactionRoot);
            }
            finally
            {
                if (!isAlive)
                {
                    SqlClientEventSource.Log.TryPoolerTraceEvent(
                        "ChannelDbConnectionPool.GetFromTransactedPool | INFO | {0}, Connection {1}, found dead and removed.",
                        Id,
                        connection.ObjectID);
                    RemoveConnection(connection);
                    connection = null;
                }
            }

            return connection;
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
        /// Requests background warmup/replenishment: the pool asynchronously pre-creates connections
        /// up to <see cref="MinPoolSize"/>, serially and through the shared rate limiter. Safe to
        /// call from any thread and from hot paths; it is cheap and non-blocking.
        ///
        /// This is the single entry point for every warmup trigger: pool startup and any event that
        /// drops the pool below <see cref="MinPoolSize"/> (connection destruction on return, idle
        /// timeout eviction, pruning). It is also invoked directly by tests to exercise the loop
        /// deterministically. Concurrent requests are coalesced by the
        /// <see cref="_warmupLoopRunning"/> guard so that only one warmup loop ever runs at a time: a
        /// request that arrives while a loop is already running is simply dropped, because that loop
        /// re-reads <see cref="Count"/> on every iteration and will drive the pool to
        /// <see cref="MinPoolSize"/> regardless.
        /// </summary>
        internal void RequestWarmup()
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
            // loop, which re-reads the count on every iteration.
            //
            // This gates on ReservationCount rather than Count so that connections another thread
            // is currently opening count toward the minimum. Gating on Count would make warmup
            // create duplicates for every creation already in flight.
            if (_connectionSlots.ReservationCount >= MinPoolSize)
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
                // Fire-and-forget on the thread pool so warmup never blocks the caller. The loop
                // absorbs its own exceptions and always releases the single-loop guard on exit. The
                // task is published so tests can await a warmup pass to a deterministic completion.
                WarmupLoopTask = Task.Run(RunWarmupLoopAsync);

                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.RequestWarmup | INFO | {0}, Scheduled warmup loop. Count={1}, MinPoolSize={2}", Id, Count, MinPoolSize);
            }
            catch (Exception ex)
            {
                // Scheduling the loop failed (e.g. the thread pool refused the work item). Release the
                // guard so warmup isn't permanently pinned off for the life of the pool; the next
                // below-minimum trigger will try again. Release the guard for every exception, but
                // only absorb catchable ones - a non-catchable exception (e.g. OutOfMemoryException)
                // must not be swallowed into a pool that keeps running in a potentially corrupted state.
                Interlocked.Exchange(ref _warmupLoopRunning, 0);
                if (!ADP.IsCatchableExceptionType(ex))
                {
                    throw;
                }

                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.RequestWarmup | INFO | {0}, Failed to schedule warmup loop, absorbing: {1}", Id, ex);
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
            int warmedUp = 0;

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.RunWarmupLoopAsync | INFO | {0}, Warmup loop starting. Count={1}, MinPoolSize={2}", Id, Count, MinPoolSize);

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
                    && _connectionSlots.ReservationCount < MinPoolSize)
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
                            timeout: timeout,
                            cancellationToken: token);
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
                            "ChannelDbConnectionPool.RunWarmupLoopAsync | INFO | {0}, Warmup connection creation failed, stopping pass: {1}", Id, ex);
                        break;
                    }

                    if (connection is null)
                    {
                        // A slot is guaranteed available here (ReservationCount < MinPoolSize <= MaxPoolSize),
                        // and creation failures throw rather than return null, so a null return means
                        // the shared rate limiter is currently saturated. Rather than bypassing the
                        // limiter or spinning on it (Story 2), end this warmup pass. Saturation only
                        // happens while user requests are actively creating connections - those very
                        // creations fill the pool toward MinPoolSize, and any later drop below the
                        // minimum re-triggers warmup through RemoveConnection - so warmup does not need
                        // to compete for a permit here.
                        break;
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

                    warmedUp++;

                    // OpenNewInternalConnection is synchronous and blocks the loop's thread for
                    // the duration of the physical open. Yield between creations so a multi-
                    // connection warmup returns its thread-pool worker to the scheduler between
                    // opens (rather than monopolizing one worker for the whole sequence) and stays
                    // responsive to cancellation. There is no sync-over-async anywhere in this path.
                    await Task.Yield();
                }

                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.RunWarmupLoopAsync | INFO | {0}, Warmup loop finished. Warmed up {1} connections. Count={2}", Id, warmedUp, Count);
            }
            catch (Exception ex) when (ADP.IsCatchableExceptionType(ex))
            {
                // Defense in depth: the loop must never throw a catchable exception onto the thread
                // pool. A non-catchable exception (e.g. OutOfMemoryException) is left to propagate
                // rather than absorbed into a pool that keeps running in a potentially corrupted
                // state; the finally below still releases the single-loop guard on that path.
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "ChannelDbConnectionPool.RunWarmupLoopAsync | INFO | {0}, Warmup loop failed, absorbing. Warmed up {1} connections: {2}", Id, warmedUp, ex);
            }
            finally
            {
                // Always release the single-loop guard, whatever exit path we took, so a future
                // below-minimum trigger can start a new loop. Interlocked.Exchange mirrors the
                // Interlocked.CompareExchange acquire in RequestWarmup.
                Interlocked.Exchange(ref _warmupLoopRunning, 0);
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
            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.PruneConnections | INFO | {0}, Pruning up to {1} idle connections. IdleCount={2}, Count={3}",
                Id,
                count,
                IdleCount,
                Count);

            int pruned = 0;

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
                pruned++;
            }

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "ChannelDbConnectionPool.PruneConnections | INFO | {0}, Pruned {1} idle connections.",
                Id,
                pruned);
        }
        #endregion
    }
}

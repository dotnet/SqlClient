// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Internal;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// Drives background sweeps for emancipated connections in <see cref="ChannelDbConnectionPool"/>
    /// while callers are parked waiting for a connection.
    /// <para>
    /// A connection becomes emancipated when its owning <see cref="System.Data.Common.DbConnection"/>
    /// is garbage collected without ever being closed or disposed. Emancipation only becomes
    /// observable once the collector has run, which routinely happens after a caller has already
    /// parked, so a single sweep at request time cannot cover it. Without a background sweep every
    /// parked caller would wait out its full timeout even though a pool slot was recoverable the
    /// whole time.
    /// </para>
    /// <para>
    /// The sweep cannot run on the parked caller's own thread. <see cref="ChannelDbConnectionPool"/>
    /// relies on the FIFO ordering that <c>Channel</c> guarantees to readers, so a parked caller that
    /// cancelled its read in order to re-sweep would lose its place in line to callers that arrived
    /// later. The sweep therefore runs on a timer callback instead.
    /// </para>
    /// <para>
    /// The timer is demand-driven: it is armed when the first caller parks and disarmed when the last
    /// one leaves, so a pool with no blocked callers never wakes the process. This mirrors the
    /// approach taken for the connection factory's pruning timer (issue #1881).
    /// </para>
    /// </summary>
    internal sealed class PoolReclaimer : IDisposable
    {
        /// <summary>
        /// Interval between sweeps while at least one caller is parked.
        /// <para>
        /// Chosen relative to the connect timeout rather than to any pool sizing option: the value
        /// bounds how long a parked caller can wait beyond the point at which its connection became
        /// reclaimable, so it only has to be small compared to the default 15 second connect timeout.
        /// It is not worth going lower, because emancipation only becomes observable after a garbage
        /// collection and sweeping faster than the collector produces new information just burns CPU.
        /// </para>
        /// </summary>
        internal static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(1);

        private readonly ChannelDbConnectionPool _pool;

        /// <summary>
        /// One-shot timer that triggers a sweep. Re-armed at the end of each callback rather than
        /// being created as a periodic timer, so a sweep that runs longer than
        /// <see cref="SweepInterval"/> can never overlap with the next one.
        /// </summary>
        private readonly ITimer _timer;

        /// <summary>
        /// Guards every state transition of this instance, including all
        /// <see cref="ITimer.Change(TimeSpan, TimeSpan)"/> calls.
        /// </summary>
        private readonly object _lock = new();

        /// <summary>
        /// Number of callers currently parked waiting on the pool's idle channel. The timer is armed
        /// exactly while this is non-zero.
        /// </summary>
        private int _parkedWaiters;

        /// <summary>
        /// Whether the timer is currently armed. Tracked explicitly rather than being inferred from
        /// <see cref="_parkedWaiters"/> so an in-flight callback can tell that it was disarmed while
        /// it was running and decline to re-arm itself.
        /// </summary>
        private bool _armed;

        /// <summary>
        /// Whether <see cref="Dispose"/> has run. Once set, the timer is never armed again.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Creates a reclaimer for the given pool. The timer is created disarmed; it is armed on
        /// demand by <see cref="EnterParkedWait"/>.
        /// </summary>
        /// <param name="pool">The owning connection pool.</param>
        /// <param name="timeProvider">
        /// Time source used to create the timer. Tests inject a fake provider so sweeps can be driven
        /// deterministically without real waits.
        /// </param>
        internal PoolReclaimer(ChannelDbConnectionPool pool, TimeProvider timeProvider)
        {
            _pool = pool;

            // The execution context is deliberately not captured. The timer is armed by whichever
            // caller happens to park first, and capturing that caller's context would pin its async
            // locals (and any impersonation context) for as long as this pool-scoped timer lives.
            _timer = ADP.UnsafeCreateTimer(
                timeProvider,
                static reclaimer => reclaimer.OnSweepCallback(),
                this,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
        }

        #region Internal test surface

        /// <summary>Whether the sweep timer is currently armed. Exposed for unit tests.</summary>
        internal bool IsTimerEnabled
        {
            get
            {
                lock (_lock)
                {
                    return _armed;
                }
            }
        }

        /// <summary>
        /// Number of callers currently registered as parked. Exposed for unit tests.
        /// <para>
        /// Not an exact count of callers sitting on the idle channel. On the synchronous path
        /// registration happens before <c>ReadChannelSyncOverAsync</c> takes the process-wide
        /// sync-over-async semaphore, so a caller counts as parked while it is still queued for that
        /// semaphore. The effect is only that the timer arms slightly early, which is harmless: the
        /// caller is blocked either way, and a sweep that runs before it reaches the channel simply
        /// finds nothing.
        /// </para>
        /// </summary>
        internal int ParkedWaiters
        {
            get
            {
                lock (_lock)
                {
                    return _parkedWaiters;
                }
            }
        }

        #endregion

        /// <summary>
        /// Registers a caller that is about to park on the pool's idle channel, arming the sweep
        /// timer if this is the only parked caller. Must be paired with <see cref="ExitParkedWait"/>
        /// in a finally block so a cancelled or timed out wait still releases its registration.
        /// </summary>
        internal void EnterParkedWait()
        {
            lock (_lock)
            {
                _parkedWaiters++;

                if (_armed || _disposed)
                {
                    return;
                }

                _armed = true;
                _timer.Change(SweepInterval, Timeout.InfiniteTimeSpan);

                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "PoolReclaimer.EnterParkedWait | INFO | {0}, Sweep timer started.", _pool.Id);
            }
        }

        /// <summary>
        /// Unregisters a caller that is no longer parked, disarming the sweep timer once the last
        /// one leaves so an unblocked pool does not keep waking the process.
        /// </summary>
        internal void ExitParkedWait()
        {
            lock (_lock)
            {
                // An unbalanced call means some park site is missing its EnterParkedWait, which
                // would leave the timer armed forever (or, if it under-counted the other way, stop
                // sweeping while a caller is still parked). Clamped rather than thrown in release
                // builds: a mis-paired registration is not worth failing a connection attempt over.
                Debug.Assert(_parkedWaiters > 0, "ExitParkedWait called without a matching EnterParkedWait.");

                if (_parkedWaiters > 0)
                {
                    _parkedWaiters--;
                }

                if (_parkedWaiters != 0 || !_armed)
                {
                    return;
                }

                _armed = false;
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "PoolReclaimer.ExitParkedWait | INFO | {0}, Sweep timer stopped, no parked waiters.", _pool.Id);
            }
        }

        /// <summary>
        /// Timer callback that sweeps the pool for emancipated connections and re-arms itself while
        /// callers remain parked. Reclaimed connections are routed back to the idle channel, which
        /// wakes a parked caller.
        /// </summary>
        internal void OnSweepCallback()
        {
            lock (_lock)
            {
                // Disarmed (or disposed) after this callback was already scheduled.
                if (!_armed || _disposed)
                {
                    return;
                }
            }

            // Sweep outside the lock: reclamation deactivates connections, which can make server
            // round trips, and must not block EnterParkedWait/ExitParkedWait on the hot path.
            try
            {
                if (_pool.IsRunning)
                {
                    _pool.ReclaimEmancipatedConnections();
                }
            }
            catch (Exception ex)
            {
                // A timer callback must never throw: an unhandled exception on the timer's thread
                // would tear down the process. A failed sweep is not fatal to the pool, so trace it
                // and let the next sweep try again.
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "PoolReclaimer.OnSweepCallback | ERR | {0}, Sweep threw, continuing: {1}.", _pool.Id, ex);
            }

            lock (_lock)
            {
                // Re-check rather than re-arming unconditionally: ExitParkedWait may have disarmed
                // the timer while the sweep above was running, in which case there is no longer
                // anyone to wake and re-arming would resurrect a timer that is meant to be idle.
                if (_armed && !_disposed)
                {
                    _timer.Change(SweepInterval, Timeout.InfiniteTimeSpan);
                }
            }
        }

        /// <summary>
        /// Stops the sweep timer and releases resources. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                _armed = false;
                _timer.Dispose();
            }
        }
    }
}

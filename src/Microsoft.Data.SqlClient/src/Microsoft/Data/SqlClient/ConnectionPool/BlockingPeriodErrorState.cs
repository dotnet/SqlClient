// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Internal;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// Encapsulates a connection pool's blocking-period error state: cached exception, exponential
    /// backoff timer, and synchronization. Kept as a separate class so the pool's
    /// connection-acquisition path remains focused on capacity/queue concerns and stays
    /// decoupled from the (independent) rate limiting policy.
    /// </summary>
    internal sealed class BlockingPeriodErrorState : IDisposable
    {
        // Backoff interval used the first time the pool enters the blocking period, and the
        // value the backoff resets to on a successful create or Clear().
        private static readonly TimeSpan InitialWait = TimeSpan.FromSeconds(5);

        // Upper bound the exponential backoff is capped at; further failures never wait longer.
        private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(60);

        // Identifier of the owning pool, included in trace events for diagnostics.
        private readonly int _ownerPoolId;

        // Optional callback invoked (under _lock) when the state enters the blocking period;
        // Must be cheap and non-reentrant. Must not throw. Must be idempotent: it may be invoked
        // more than once without an intervening exit (each failure re-enters via Enter()), so
        // repeated invocations while already in the blocking period must be harmless. If null,
        // no action is taken.
        private readonly Action? _onEnter;

        // Optional callback invoked (under _lock) when the state leaves the blocking period
        // via the exit timer or Clear(). Must be cheap and non-reentrant. Must not throw.
        // Must be idempotent: it may be invoked more than once for a single blocking period
        // (e.g. the exit timer fires and a later Clear() also signals exit), so redundant
        // invocations must be harmless. If null, no action is taken.
        private readonly Action? _onExit;

        // Time source used to create the exit timer; overridable so tests can drive scheduling
        // deterministically. Defaults to TimeProvider.System.
        private readonly TimeProvider _timeProvider;

        // Guards the mutable error state (_cachedException, _exitTimer, _nextWait, _disposed).
        private readonly object _lock = new();

        // Non-null while the pool is in the blocking period. Doubles as the "has error"
        // flag, so callers don't need a separate bool. Volatile so other threads observe
        // entry/exit transitions without acquiring _lock.
        private volatile Exception? _cachedException;

        // True from the moment Enter() activates the blocking period until Clear()/Dispose()
        // fully resets it. The exit timer clears _cachedException but leaves this set so a
        // later Clear() still resets the backoff. Volatile so Clear() can take a
        // lock-free path when there is nothing to do. This allows Clear() to be called on hot paths.
        private volatile bool _inElevatedState;

        // The armed exit timer that ends the current blocking period; null when no period is
        // active. Replaced (and the old one disposed) each time Enter() is called.
        private ITimer? _exitTimer;

        // The backoff interval to use for the next Enter(); doubles per failure up to MaxWait
        // and resets to InitialWait on a successful create or Clear().
        private TimeSpan _nextWait = InitialWait;

        // True once Dispose() has run, so repeated disposal and post-teardown work are no-ops.
        private bool _disposed;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="ownerPoolId">Identifier of the owning pool, used in trace events.</param>
        /// <param name="onEnter">Optional callback invoked (while holding the internal lock) after
        /// the state transitions into the blocking period. Used by the legacy wait-handle pool to
        /// signal its error wait handle. Because it runs under a lock, it must be cheap and
        /// non-reentrant. It must also be idempotent: consecutive failures re-enter via
        /// <see cref="Enter"/> without an intervening exit, so repeated invocations while already
        /// in the blocking period must be harmless.</param>
        /// <param name="onExit">Optional callback invoked (while holding the internal lock) after the
        /// state transitions out of the blocking period via the exit timer or <see cref="Clear"/>.
        /// Because it runs under a lock, it must be cheap and non-reentrant. It must also be
        /// idempotent: a single blocking period may signal exit more than once (for example, the
        /// exit timer fires and a subsequent <see cref="Clear"/> also fires), so redundant
        /// invocations must be harmless.</param>
        /// <param name="timeProvider">The time provider used to create the exit timer. Defaults to
        /// <see cref="TimeProvider.System"/>. Inject a test double (e.g.
        /// <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c>) in unit tests to
        /// control timer scheduling deterministically.</param>
        internal BlockingPeriodErrorState(int ownerPoolId, Action? onEnter = null, Action? onExit = null, TimeProvider? timeProvider = null)
        {
            _ownerPoolId = ownerPoolId;
            _onEnter = onEnter;
            _onExit = onExit;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// True while the pool is in the blocking period. Subsequent acquisition attempts
        /// should fast-fail with the cached exception.
        /// </summary>
        internal bool HasError => _cachedException is not null;

        /// <summary>
        /// Throws the cached error if the pool is currently in the blocking period.
        /// </summary>
        internal void ThrowIfActive()
        {
            Exception? cached = _cachedException;
            if (cached is null)
            {
                return;
            }

            // Clone SqlExceptions so stack traces are not shared across callers; other
            // exception types are rethrown as-is.
            // TODO: Can we create a new exception to wrap the cached exception?
            throw cached is SqlException sqlEx ? sqlEx.InternalClone() : cached;
        }

        /// <summary>
        /// Enters the blocking period, caching the supplied exception and scheduling a timer
        /// to exit the period after the current backoff interval. Subsequent failures double
        /// the backoff up to <see cref="MaxWait"/>.
        /// </summary>
        internal void Enter(Exception ex)
        {
            TimeSpan wait;
            ITimer? oldTimer;

            // If we call this, we're already in an exception path. Prefer correctness over performance.
            lock (_lock)
            {
                _inElevatedState = true;
                _cachedException = ex;
                wait = _nextWait;

                ITimer newTimer = ADP.UnsafeCreateTimer(
                    _timeProvider,
                    ExitCallback,
                    this,
                    wait,
                    wait);
                oldTimer = _exitTimer;
                _exitTimer = newTimer;

                // Bump the backoff for the next failure, capped at MaxWait. FR-008.
                TimeSpan doubled = _nextWait + _nextWait;
                _nextWait = doubled > MaxWait ? MaxWait : doubled;

                // Signal the enter event while still holding the lock so the external signal order
                // (onEnter/onExit) can never diverge from the internal state transitions under
                // concurrent Enter/Clear/exit-timer activity. The callbacks are expected to be
                // cheap, non-reentrant operations.
                _onEnter?.Invoke();
            }

            oldTimer?.Dispose();

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "<prov.DbConnectionPool.EnterErrorState|RES|CPOOL> {0}, Entering blocking period for {1}ms.",
                _ownerPoolId,
                (int)wait.TotalMilliseconds);
        }

        /// <summary>
        /// Clears the cached error state, disposes the exit timer, and resets the backoff to
        /// its initial value.
        /// </summary>
        internal void Clear()
        {
            // Fast path: the create flow calls Clear() after every successful create, so avoid
            // taking the lock in the common (no-error) case where there is nothing to reset.
            if (!_inElevatedState)
            {
                return;
            }

            ITimer? oldTimer;

            lock (_lock)
            {
                _cachedException = null;
                _nextWait = InitialWait;
                _inElevatedState = false;
                oldTimer = _exitTimer;
                _exitTimer = null;

                // Signal and trace under the lock so the exit signal is ordered consistently
                // with the state transition relative to concurrent Enter/exit-timer callbacks.
                _onExit?.Invoke();
            }

            oldTimer?.Dispose();

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "<prov.DbConnectionPool.ClearErrorState|RES|CPOOL> {0}, Error state cleared.", _ownerPoolId);
        }

        /// <summary>
        /// Timer callback that exits the blocking period by clearing the cached exception,
        /// allowing the next caller to attempt a fresh connection creation. The current
        /// backoff is left intact so that, if the next attempt fails, the backoff continues
        /// to grow rather than resetting. The backoff is reset only on <see cref="Clear"/>.
        /// </summary>
        private static void ExitCallback(BlockingPeriodErrorState state)
        {
            ITimer? oldTimer;

            lock (state._lock)
            {
                state._cachedException = null;
                oldTimer = state._exitTimer;
                state._exitTimer = null;

                // Signal under the lock so the exit signal is ordered consistently
                // with the state transition relative to concurrent Enter/Clear callbacks.
                try {
                    state._onExit?.Invoke();
                } catch {
                    // Ignore exceptions from the exit event.
                }
            }

            oldTimer?.Dispose();

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "<prov.DbConnectionPool.ExitErrorStateCallback|RES|CPOOL> {0}, Exiting blocking period.", state._ownerPoolId);
        }

        /// <summary>
        /// Disposes the instance, releasing the exit timer if one is active. Clears the
        /// error state so that any waiting callers do not observe a stale exception after
        /// the owning pool is torn down.
        /// </summary>
        public void Dispose()
        {
            ITimer? timerToDispose;
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _cachedException = null;
                _inElevatedState = false;
                timerToDispose = _exitTimer;
                _exitTimer = null;
            }

            timerToDispose?.Dispose();
            _disposed = true;
        }
    }
}

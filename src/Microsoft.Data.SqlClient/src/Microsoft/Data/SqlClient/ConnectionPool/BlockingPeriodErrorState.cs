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

        // Optional error wait handle owned by the pool. Set() (under _lock) when the state
        // enters the blocking period and Reset() (under _lock) when it leaves via the exit
        // timer or Clear(). Set/Reset are inherently idempotent, so the repeated signalling
        // that occurs across consecutive Enter() calls or an exit-timer/Clear() overlap is
        // harmless. This instance does NOT own the handle and must never dispose it. If null,
        // no signalling occurs.
        private readonly ManualResetEvent? _errorEvent;

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

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="ownerPoolId">Identifier of the owning pool, used in trace events.</param>
        /// <param name="errorEvent">Optional error wait handle owned by the pool. It is
        /// <see cref="EventWaitHandle.Set"/> (while holding the internal lock) when the state
        /// enters the blocking period and <see cref="EventWaitHandle.Reset"/> when it leaves via
        /// the exit timer or <see cref="Clear"/>, so a thread parked in <c>WaitHandle.WaitAny</c>
        /// over the pool's handles wakes immediately on entry/exit. This instance does not own the
        /// handle and never disposes it; the caller is responsible for its lifetime. Pass
        /// <see langword="null"/> (the default) to disable signalling.</param>
        /// <param name="timeProvider">The time provider used to create the exit timer. Defaults to
        /// <see cref="TimeProvider.System"/>. Inject a test double (e.g.
        /// <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c>) in unit tests to
        /// control timer scheduling deterministically.</param>
        internal BlockingPeriodErrorState(int ownerPoolId, ManualResetEvent? errorEvent = null, TimeProvider? timeProvider = null)
        {
            _ownerPoolId = ownerPoolId;
            _errorEvent = errorEvent;
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

                // Signal the error wait handle while still holding the lock so the external signal
                // order (Set/Reset) can never diverge from the internal state transitions under
                // concurrent Enter/Clear/exit-timer activity. Set() is idempotent, so re-entering
                // the blocking period on consecutive failures is harmless. The handle is owned by
                // the pool, not this instance, so treat signalling as best-effort: it may be
                // disposed out from under us during teardown, which would throw
                // ObjectDisposedException. Swallow it so it cannot mask the original
                // connection-creation exception.
                try
                {
                    _errorEvent?.Set();
                }
                catch
                {
                    // Ignore: the pool-owned handle may have been disposed during teardown.
                }
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

                // Reset the error wait handle under the lock so the exit signal is ordered
                // consistently with the state transition relative to concurrent Enter/exit-timer
                // callbacks. Reset() is idempotent, so an overlapping exit-timer/Clear() is harmless.
                // The handle is owned by the pool, not this instance, so treat signalling as
                // best-effort: it may be disposed out from under us during teardown, which would
                // throw ObjectDisposedException. Swallow it so it cannot surface on the successful
                // connection-create path and break pool recovery.
                try
                {
                    _errorEvent?.Reset();
                }
                catch
                {
                    // Ignore: the pool-owned handle may have been disposed during teardown.
                }
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

                // Reset the error wait handle under the lock so the exit signal is ordered
                // consistently with the state transition relative to concurrent Enter/Clear
                // callbacks. Reset() is idempotent, so an overlapping Clear() is harmless.
                // The handle is owned by the pool, not this instance, so treat signalling as
                // best-effort: it may be disposed out from under us during teardown, which would
                // throw ObjectDisposedException on this ThreadPool timer callback. Swallow it.
                try
                {
                    state._errorEvent?.Reset();
                }
                catch
                {
                    // Ignore: the pool-owned handle may have been disposed during teardown.
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
            lock (_lock)
            {
                _cachedException = null;
                _inElevatedState = false;
                ITimer? timerToDispose = _exitTimer;
                _exitTimer = null;
                timerToDispose?.Dispose();
            }

        }
    }
}

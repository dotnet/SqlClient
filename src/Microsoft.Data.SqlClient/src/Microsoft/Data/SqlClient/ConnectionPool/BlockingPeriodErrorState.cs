// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// Encapsulates a connection pool's blocking-period error state: cached exception, exponential
    /// backoff timer, and synchronization. Kept as a separate class so the pool's
    /// connection-acquisition path remains focused on capacity/queue concerns and stays
    /// decoupled from the (independent) rate limiting policy.
    /// </summary>
    internal sealed class BlockingPeriodErrorState
    {
        // Mirrors the values used by WaitHandleDbConnectionPool (5s initial, 60s cap).
        private static readonly TimeSpan InitialWait = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(60);

        private readonly int _ownerPoolId;
        private readonly object _lock = new();
        private volatile bool _hasError;
        private Exception? _cachedException;
        private Timer? _exitTimer;
        private TimeSpan _nextWait = InitialWait;

        internal BlockingPeriodErrorState(int ownerPoolId)
        {
            _ownerPoolId = ownerPoolId;
        }

        /// <summary>
        /// True while the pool is in the blocking period. Subsequent acquisition attempts
        /// should fast-fail with the cached exception.
        /// </summary>
        internal bool HasError => _hasError;

        /// <summary>
        /// Throws the cached error if the pool is currently in the blocking period.
        /// </summary>
        internal void ThrowIfActive()
        {
            if (!_hasError)
            {
                return;
            }

            Exception? cached = _cachedException;
            if (cached is null)
            {
                return;
            }

            // Clone SqlExceptions so stack traces are not shared across callers; other
            // exception types are rethrown as-is.
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
            Timer? oldTimer;
            Timer newTimer;

            lock (_lock)
            {
                _cachedException = ex;
                _hasError = true;
                wait = _nextWait;

                newTimer = new Timer(ExitCallback, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                oldTimer = _exitTimer;
                _exitTimer = newTimer;

                // Bump the backoff for the next failure, capped at MaxWait. FR-008.
                TimeSpan doubled = _nextWait + _nextWait;
                _nextWait = doubled >= MaxWait ? MaxWait : doubled;
            }

            oldTimer?.Dispose();
            newTimer.Change(wait, Timeout.InfiniteTimeSpan);

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
            Timer? oldTimer;
            lock (_lock)
            {
                if (!_hasError && _cachedException is null && _exitTimer is null && _nextWait == InitialWait)
                {
                    return;
                }

                _hasError = false;
                _cachedException = null;
                _nextWait = InitialWait;
                oldTimer = _exitTimer;
                _exitTimer = null;
            }

            oldTimer?.Dispose();

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "<prov.DbConnectionPool.ClearErrorState|RES|CPOOL> {0}, Error state cleared.", _ownerPoolId);
        }

        /// <summary>
        /// Timer callback that exits the blocking period, allowing the next caller to attempt
        /// a fresh connection creation. The cached exception and current backoff are left
        /// intact so that, if the very next attempt fails, the backoff continues to grow
        /// rather than resetting. They are reset only on a successful creation or on
        /// <see cref="Clear"/>.
        /// </summary>
        private void ExitCallback(object? state)
        {
            Timer? oldTimer;
            lock (_lock)
            {
                _hasError = false;
                oldTimer = _exitTimer;
                _exitTimer = null;
            }

            oldTimer?.Dispose();

            SqlClientEventSource.Log.TryPoolerTraceEvent(
                "<prov.DbConnectionPool.ExitErrorStateCallback|RES|CPOOL> {0}, Exiting blocking period.", _ownerPoolId);
        }
    }
}

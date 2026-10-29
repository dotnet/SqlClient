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
    /// Single source of truth for whether a pool is running and which operations it admits.
    /// Makes idle pool retirement atomic with admission of requests and background creation.
    /// Activity covers queued work as well as creation not yet reflected in the pool count.
    /// </summary>
    /// <remarks>
    /// Admission is represented by a <see cref="Lease"/>. Whoever holds the lease owns the
    /// admission and must dispose it; ownership moves with the lease when it is handed to a
    /// background worker, and the giver clears its copy (<c>lease = default</c>).
    /// <para>
    /// Closing (prune or shutdown) is permanent and keeps the count, so operations admitted
    /// before an explicit shutdown still release normally.
    /// </para>
    /// </remarks>
    internal sealed class PoolPruningGuard
    {
        /// <summary>Set once the pool is pruned or shut down; never cleared.</summary>
        private const int ClosedFlag = unchecked((int)0x80000000);

        /// <summary>Set only while a prune decision is in progress; the count is zero then.</summary>
        private const int PruningFlag = 0x40000000;

        /// <summary>Bits holding the number of admitted operations.</summary>
        private const int CountMask = 0x3FFFFFFF;

        /// <summary>
        /// Packs <see cref="ClosedFlag"/>, <see cref="PruningFlag"/> and the active operation count
        /// (<see cref="CountMask"/>) into one <see cref="int"/>, so every transition (admit,
        /// release, prune, close) is a single compare-and-swap.
        /// </summary>
        /// <remarks>
        /// Splitting these into separate fields would reopen the races this type exists to close:
        /// a pool could be retired between a caller's state check and its count increment, or be
        /// reported as running after it already refused admission. Admission is lock-free and
        /// allocation-free because it runs on every checkout.
        /// </remarks>
        private int _state;

        private static bool IsClosed(int state) => (state & ClosedFlag) != 0;

        private static bool IsPruning(int state) => (state & PruningFlag) != 0;

        private static int ActiveCount(int state) => state & CountMask;

        /// <summary>
        /// Whether the pool is running. Pools start running during construction, and stop
        /// running permanently once Shutdown() is called (or the pool is pruned). A caller refused
        /// by <see cref="TryEnter"/> always observes <see langword="false"/> here.
        /// </summary>
        internal bool IsRunning => !IsClosed(Volatile.Read(ref _state));

        /// <summary>Highest active operation count the guard can represent.</summary>
        internal const int MaxActiveCount = CountMask;

        /// <summary>
        /// Test hook: sets the active operation count of an open guard, so tests can reach
        /// counts (such as the overflow boundary) that are impractical to admit one by one.
        /// </summary>
        internal void SetActiveCountForTest(int count)
        {
            Debug.Assert(count >= 0 && count <= CountMask, "Count out of range.");
            Volatile.Write(ref _state, count);
        }

        /// <summary>
        /// Admits an operation. The returned lease is inactive (<see cref="Lease.IsActive"/> is
        /// <see langword="false"/>) once the pool has been pruned or shut down.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The active operation count would overflow into the flag bits.
        /// </exception>
        /// <remarks>
        /// While a prune decision is in progress this spins briefly (never takes a lock) until
        /// the decision either reopens admission or marks the pool pruned.
        /// </remarks>
        internal Lease TryEnter()
        {
            SpinWait spinner = default;
            while (true)
            {
                int state = Volatile.Read(ref _state);
                if (IsClosed(state))
                {
                    return default;
                }
                if (IsPruning(state))
                {
                    // Wait for the prune decision to either reopen admission or mark the pool pruned.
                    spinner.SpinOnce();
                    continue;
                }
                if (ActiveCount(state) == CountMask)
                {
                    // Incrementing further would carry into the flag bits and corrupt the state.
                    throw ADP.InvalidOperation("PoolPruningGuard active operation count overflow.");
                }
                if (Interlocked.CompareExchange(ref _state, state + 1, state) == state)
                {
                    return new Lease(this);
                }
            }
        }

        /// <summary>
        /// Retires the pool if it is idle: no error, no connections and no admitted operations.
        /// On success the pool stops running and refuses admission in one step, then is shut down.
        /// </summary>
        internal bool TryPrune(IDbConnectionPool pool)
        {
            if (pool.ErrorOccurred || pool.Count != 0 ||
                Interlocked.CompareExchange(ref _state, PruningFlag, 0) != 0)
            {
                return false;
            }

            // No operation is admitted and none can enter, so Count cannot grow; re-check it
            // because the read above may predate a creation that published and then released.
            if (pool.ErrorOccurred || pool.Count != 0)
            {
                Volatile.Write(ref _state, 0);
                return false;
            }

            // Stop running and refuse admission in one step, so a refused caller always sees
            // IsRunning == false and is redirected rather than reporting a pool timeout.
            Volatile.Write(ref _state, ClosedFlag);

            // Shutdown is idempotent and is retried by QueuePoolForRelease, so a failure here is
            // only traced.
            try
            {
                pool.Shutdown();
            }
            catch (Exception e) when (ADP.IsCatchableExceptionType(e))
            {
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "<prov.PoolPruningGuard.TryPrune|RES|CPOOL> {0}, Shutdown during prune failed: {1}", pool.Id, e);
            }
            return true;
        }

        /// <summary>
        /// Stops admission permanently while keeping the count of already admitted operations.
        /// Returns <see langword="true"/> only for the call that closed the guard.
        /// </summary>
        /// <remarks>
        /// Spins briefly (never takes a lock) while a prune decision is in progress.
        /// </remarks>
        internal bool TryClose()
        {
            SpinWait spinner = default;
            while (true)
            {
                int state = Volatile.Read(ref _state);
                if (IsClosed(state))
                {
                    return false;
                }
                if (IsPruning(state))
                {
                    // Wait for the prune decision to either reopen admission or mark the pool pruned.
                    spinner.SpinOnce();
                    continue;
                }
                if (Interlocked.CompareExchange(ref _state, state | ClosedFlag, state) == state)
                {
                    return true;
                }
            }
        }

        /// <remarks>
        /// Never waits on another operation; it only retries when a concurrent admit, release or
        /// close changed the state between the read and the compare-and-swap.
        /// </remarks>
        private void Release()
        {
            SpinWait spinner = default;
            while (true)
            {
                int state = Volatile.Read(ref _state);

                // Refuse to go below zero: that would corrupt the flags and could allow retiring
                // a pool that is still in use.
                if (ActiveCount(state) == 0)
                {
                    Debug.Fail("PoolPruningGuard released more often than admitted.");
                    return;
                }

                // Decrementing the count leaves the closed flag intact.
                if (Interlocked.CompareExchange(ref _state, state - 1, state) == state)
                {
                    return;
                }
                spinner.SpinOnce();
            }
        }

        /// <summary>
        /// An admitted operation. Dispose exactly when the operation (including any work handed
        /// off to a background worker) completes. A struct so admission does not allocate; disposing
        /// the same storage location again is a no-op.
        /// </summary>
        internal struct Lease : IDisposable
        {
            private PoolPruningGuard? _guard;

            internal Lease(PoolPruningGuard guard) => _guard = guard;

            /// <summary><see langword="false"/> when admission was refused or the lease was released.</summary>
            internal readonly bool IsActive => _guard is not null;

            public void Dispose() => Interlocked.Exchange(ref _guard, null)?.Release();
        }
    }
}

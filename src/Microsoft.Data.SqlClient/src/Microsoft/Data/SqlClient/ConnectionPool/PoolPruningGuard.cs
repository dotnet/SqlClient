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
    /// Makes idle pool retirement atomic with admission of requests and background creation.
    /// Activity covers queued work as well as creation not yet reflected in the pool count.
    /// Explicit shutdown is independent: requests already admitted may still finish.
    /// </summary>
    /// <remarks>
    /// Admission is represented by a <see cref="Lease"/>. Whoever holds the lease owns the
    /// admission and must dispose it; ownership moves with the work when it is handed to a
    /// background worker, and the giver clears its copy (<c>lease = default</c>).
    /// <para>
    /// Admission is lock-free and allocation-free because it runs on every checkout.
    /// <see cref="_state"/> is the active operation count, or <see cref="Pruning"/> /
    /// <see cref="Pruned"/>.
    /// </para>
    /// </remarks>
    internal sealed class PoolPruningGuard
    {
        private const int Pruned = -1;
        private const int Pruning = -2;

        private int _state;

        /// <summary>
        /// Admits an operation. The returned lease is inactive (<see cref="Lease.IsActive"/> is
        /// <see langword="false"/>) once the pool has been pruned.
        /// </summary>
        internal Lease TryEnter()
        {
            SpinWait spinner = default;
            while (true)
            {
                int state = Volatile.Read(ref _state);
                if (state == Pruned)
                {
                    return default;
                }
                if (state == Pruning)
                {
                    // A prune decision is in progress and resolves without blocking.
                    spinner.SpinOnce();
                    continue;
                }
                if (Interlocked.CompareExchange(ref _state, state + 1, state) == state)
                {
                    return new Lease(this);
                }
            }
        }

        internal bool TryPrune(IDbConnectionPool pool)
        {
            if (pool.ErrorOccurred || pool.Count != 0 ||
                Interlocked.CompareExchange(ref _state, Pruning, 0) != 0)
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
            Volatile.Write(ref _state, Pruned);

            // The pool is idle and no longer admits work, so it is retired whatever happens next.
            // Shutting down here (rather than only in QueuePoolForRelease) lets the factory redirect
            // a caller that already fetched this pool instead of reporting a pool timeout. Shutdown
            // is idempotent and is retried by QueuePoolForRelease, so a failure here is only traced.
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

        private void Release()
        {
            SpinWait spinner = default;
            while (true)
            {
                int state = Volatile.Read(ref _state);

                // Refuse to go negative: a lower count would read as "no active operations" (or
                // as a pruning state) and allow retiring a pool that is still in use.
                if (state <= 0)
                {
                    Debug.Fail("PoolPruningGuard released more often than admitted.");
                    return;
                }
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
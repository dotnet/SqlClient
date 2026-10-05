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
    /// background worker. Disposal is idempotent, so the active count cannot be released twice.
    /// </remarks>
    internal sealed class PoolPruningGuard
    {
        private readonly object _syncRoot = new();
        private int _activeOperations;
        private bool _pruned;

        /// <summary>
        /// Admits an operation. Returns <see langword="null"/> once the pool has been pruned.
        /// </summary>
        internal Lease? TryEnter()
        {
            lock (_syncRoot)
            {
                if (_pruned)
                {
                    return null;
                }
                _activeOperations++;
                return new Lease(this);
            }
        }

        internal bool TryPrune(IDbConnectionPool pool)
        {
            lock (_syncRoot)
            {
                if (_activeOperations != 0 || pool.ErrorOccurred || pool.Count != 0)
                {
                    return false;
                }

                _pruned = true;
            }

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
            lock (_syncRoot)
            {
                // Leases release at most once, so this cannot underflow; refuse to go negative
                // regardless, because a negative count would read as "no active operations".
                if (_activeOperations <= 0)
                {
                    Debug.Fail("PoolPruningGuard released more often than admitted.");
                    return;
                }
                _activeOperations--;
            }
        }

        /// <summary>
        /// An admitted operation. Dispose exactly when the operation (including any work handed
        /// off to a background worker) completes. Extra disposals are ignored.
        /// </summary>
        internal sealed class Lease : IDisposable
        {
            private PoolPruningGuard? _guard;

            internal Lease(PoolPruningGuard guard) => _guard = guard;

            public void Dispose() => Interlocked.Exchange(ref _guard, null)?.Release();
        }
    }
}
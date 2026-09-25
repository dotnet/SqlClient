// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// Makes idle pool retirement atomic with admission of requests and background creation.
    /// Activity covers queued work as well as creation not yet reflected in the pool count.
    /// Explicit shutdown is independent: requests already admitted may still finish.
    /// </summary>
    internal sealed class PoolPruningGuard
    {
        private readonly object _syncRoot = new();
        private int _activeOperations;
        private bool _pruned;

        internal bool TryEnter()
        {
            lock (_syncRoot)
            {
                if (_pruned)
                {
                    return false;
                }
                _activeOperations++;
                return true;
            }
        }

        internal void Exit()
        {
            lock (_syncRoot)
            {
                Debug.Assert(_activeOperations > 0);
                _activeOperations--;
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
                // Publish shutdown before rejecting admission so the factory can redirect a
                // caller that already fetched this pool, rather than reporting a pool timeout.
                pool.Shutdown();
                return true;
            }
        }
    }
}

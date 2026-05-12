// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Test-only extension methods that adapt legacy 3-arg pool calls to the
    /// new <see cref="TimeoutTimer"/>-aware signatures by deriving the timer
    /// from the owning connection's <see cref="DbConnection.ConnectionTimeout"/>.
    /// </summary>
    internal static class PoolTestExtensions
    {
        public static bool TryGetConnection(
            this IDbConnectionPool pool,
            DbConnection owningObject,
            TaskCompletionSource<DbConnectionInternal>? taskCompletionSource,
            out DbConnectionInternal? connection)
        {
            TimeoutTimer timeout = TimeoutTimer.StartNew(
                TimeSpan.FromSeconds(owningObject?.ConnectionTimeout ?? 15));
            return pool.TryGetConnection(owningObject!, taskCompletionSource, timeout, out connection);
        }

        public static void ReplaceConnection(
            this IDbConnectionPool pool,
            DbConnection owningObject,
            DbConnectionInternal oldConnection)
        {
            TimeoutTimer timeout = TimeoutTimer.StartNew(
                TimeSpan.FromSeconds(owningObject?.ConnectionTimeout ?? 15));
            pool.ReplaceConnection(owningObject!, oldConnection, timeout);
        }
    }
}

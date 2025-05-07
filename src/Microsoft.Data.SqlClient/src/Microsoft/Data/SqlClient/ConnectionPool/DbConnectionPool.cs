// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    internal abstract class DbConnectionPool
    {
        private static int _objectTypeCount;

        internal int ObjectId { get; } = System.Threading.Interlocked.Increment(ref _objectTypeCount);

        internal DbConnectionPoolState State { get; set; }

        #region Abstract Properties
        internal abstract int Count { get; }

        internal abstract DbConnectionFactory ConnectionFactory { get; }

        internal abstract bool ErrorOccurred { get; }

        internal abstract TimeSpan LoadBalanceTimeout { get; }

        internal abstract DbConnectionPoolIdentity Identity { get; }

        internal abstract bool IsRunning { get; }

        internal abstract DbConnectionPoolGroup PoolGroup { get; }

        internal abstract DbConnectionPoolGroupOptions PoolGroupOptions { get; }

        internal abstract DbConnectionPoolProviderInfo ProviderInfo { get; }

        internal abstract ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> AuthenticationContexts { get; }

        internal abstract bool UseLoadBalancing { get; }
        #endregion

        #region Abstract Methods
        internal abstract void Clear();

        internal abstract bool TryGetConnection(DbConnection owningObject, TaskCompletionSource<DbConnectionInternal> taskCompletionSource, DbConnectionOptions userOptions, out DbConnectionInternal connection);

        internal abstract DbConnectionInternal ReplaceConnection(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection);

        internal abstract void ReturnInternalConnection(DbConnectionInternal obj, object owningObject);

        internal abstract void PutObjectFromTransactedPool(DbConnectionInternal obj);

        internal abstract void Startup();

        internal abstract void Shutdown();

        internal abstract void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject);
        #endregion
    }
}

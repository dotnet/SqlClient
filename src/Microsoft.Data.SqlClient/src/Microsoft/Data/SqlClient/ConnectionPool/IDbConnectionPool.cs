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
    /// <summary>
    /// A base class for implementing database connection pools.
    /// Responsible for managing the lifecycle of connections and providing access to database connections.
    /// </summary>
    internal interface IDbConnectionPool
    {
        #region Properties
        int ObjectId { get; }

        DbConnectionPoolState State { get; set; }

        int Count { get; }

        DbConnectionFactory ConnectionFactory { get; }

        bool ErrorOccurred { get; }

        TimeSpan LoadBalanceTimeout { get; }

        DbConnectionPoolIdentity Identity { get; }

        bool IsRunning { get; }

        DbConnectionPoolGroup PoolGroup { get; }

        DbConnectionPoolGroupOptions PoolGroupOptions { get; }

        DbConnectionPoolProviderInfo ProviderInfo { get; }

        ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> AuthenticationContexts { get; }

        bool UseLoadBalancing { get; }
        #endregion

        #region Methods
        void Clear();

        bool TryGetConnection(DbConnection owningObject, TaskCompletionSource<DbConnectionInternal> retry, DbConnectionOptions userOptions, out DbConnectionInternal connection);

        DbConnectionInternal ReplaceConnection(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection);

        void ReturnInternalConnection(DbConnectionInternal obj, object owningObject);

        void PutObjectFromTransactedPool(DbConnectionInternal obj);

        void Startup();

        void Shutdown();

        void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject);
        #endregion
    }
}

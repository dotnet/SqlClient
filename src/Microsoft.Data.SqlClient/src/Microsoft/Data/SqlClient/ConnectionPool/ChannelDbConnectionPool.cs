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

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// A connection pool implementation based on the channel data structure.
    /// Provides methods to manage the pool of connections, including acquiring and releasing connections.
    /// </summary>
    internal sealed class ChannelDbConnectionPool : IDbConnectionPool
    {
        public int ObjectId => throw new NotImplementedException();

        public DbConnectionPoolState State { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int Count => throw new NotImplementedException();

        public DbConnectionFactory ConnectionFactory => throw new NotImplementedException();

        public bool ErrorOccurred => throw new NotImplementedException();

        public TimeSpan LoadBalanceTimeout => throw new NotImplementedException();

        public DbConnectionPoolIdentity Identity => throw new NotImplementedException();

        public bool IsRunning => throw new NotImplementedException();

        public DbConnectionPoolGroup PoolGroup => throw new NotImplementedException();

        public DbConnectionPoolGroupOptions PoolGroupOptions => throw new NotImplementedException();

        public DbConnectionPoolProviderInfo ProviderInfo => throw new NotImplementedException();

        public ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> AuthenticationContexts => throw new NotImplementedException();

        public bool UseLoadBalancing => throw new NotImplementedException();

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public void PutObjectFromTransactedPool(DbConnectionInternal obj)
        {
            throw new NotImplementedException();
        }

        public DbConnectionInternal ReplaceConnection(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection)
        {
            throw new NotImplementedException();
        }

        public void ReturnInternalConnection(DbConnectionInternal obj, object owningObject)
        {
            throw new NotImplementedException();
        }

        public void Shutdown()
        {
            throw new NotImplementedException();
        }

        public void Startup()
        {
            throw new NotImplementedException();
        }

        public void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject)
        {
            throw new NotImplementedException();
        }

        public bool TryGetConnection(DbConnection owningObject, TaskCompletionSource<DbConnectionInternal> taskCompletionSource, DbConnectionOptions userOptions, out DbConnectionInternal connection)
        {
            throw new NotImplementedException();
        }
    }
}

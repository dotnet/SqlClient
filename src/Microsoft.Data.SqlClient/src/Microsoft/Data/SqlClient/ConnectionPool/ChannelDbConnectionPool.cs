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
    internal sealed class ChannelDbConnectionPool : DbConnectionPool
    {
        internal override int Count => throw new NotImplementedException();

        internal override DbConnectionFactory ConnectionFactory => throw new NotImplementedException();

        internal override bool ErrorOccurred => throw new NotImplementedException();

        internal override TimeSpan LoadBalanceTimeout => throw new NotImplementedException();

        internal override DbConnectionPoolIdentity Identity => throw new NotImplementedException();

        internal override bool IsRunning => throw new NotImplementedException();

        internal override DbConnectionPoolGroup PoolGroup => throw new NotImplementedException();

        internal override DbConnectionPoolGroupOptions PoolGroupOptions => throw new NotImplementedException();

        internal override DbConnectionPoolProviderInfo ProviderInfo => throw new NotImplementedException();

        internal override ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> AuthenticationContexts => throw new NotImplementedException();

        internal override bool UseLoadBalancing => throw new NotImplementedException();

        internal override void Clear()
        {
            throw new NotImplementedException();
        }

        internal override void PutObjectFromTransactedPool(DbConnectionInternal obj)
        {
            throw new NotImplementedException();
        }

        internal override DbConnectionInternal ReplaceConnection(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection)
        {
            throw new NotImplementedException();
        }

        internal override void ReturnInternalConnection(DbConnectionInternal obj, object owningObject)
        {
            throw new NotImplementedException();
        }

        internal override void Shutdown()
        {
            throw new NotImplementedException();
        }

        internal override void Startup()
        {
            throw new NotImplementedException();
        }

        internal override void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject)
        {
            throw new NotImplementedException();
        }

        internal override bool TryGetConnection(DbConnection owningObject, TaskCompletionSource<DbConnectionInternal> retry, DbConnectionOptions userOptions, out DbConnectionInternal connection)
        {
            throw new NotImplementedException();
        }
    }
}

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
        #region Properties
        /// <inheritdoc />
        public ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> AuthenticationContexts => throw new NotImplementedException();

        /// <inheritdoc />
        public DbConnectionFactory ConnectionFactory => throw new NotImplementedException();

        /// <inheritdoc />
        public int Count => throw new NotImplementedException();

        /// <inheritdoc />
        public bool ErrorOccurred => throw new NotImplementedException();

        /// <inheritdoc />
        public int Id => throw new NotImplementedException();

        /// <inheritdoc />
        public DbConnectionPoolIdentity Identity => throw new NotImplementedException();

        /// <inheritdoc />
        public bool IsRunning => throw new NotImplementedException();

        /// <inheritdoc />
        public TimeSpan LoadBalanceTimeout => throw new NotImplementedException();

        /// <inheritdoc />
        public DbConnectionPoolGroup PoolGroup => throw new NotImplementedException();

        /// <inheritdoc />
        public DbConnectionPoolGroupOptions PoolGroupOptions => throw new NotImplementedException();

        /// <inheritdoc />
        public DbConnectionPoolProviderInfo ProviderInfo => throw new NotImplementedException();

        /// <inheritdoc />
        public DbConnectionPoolState State { 
            get => throw new NotImplementedException(); 
            set => throw new NotImplementedException(); 
        }

        /// <inheritdoc />
        public bool UseLoadBalancing => throw new NotImplementedException();
        #endregion



        #region Methods
        /// <inheritdoc />
        public void Clear()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void PutObjectFromTransactedPool(DbConnectionInternal obj)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public DbConnectionInternal ReplaceConnection(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void ReturnInternalConnection(DbConnectionInternal obj, object owningObject)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Startup()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool TryGetConnection(DbConnection owningObject, TaskCompletionSource<DbConnectionInternal> taskCompletionSource, DbConnectionOptions userOptions, out DbConnectionInternal connection)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}

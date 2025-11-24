// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// A base interface for implementing database connection pools.
    /// Implementations are responsible for managing the lifecycle 
    /// of connections and providing access to database connections.
    /// </summary>
    internal interface IDbConnectionPool
    {
        #region Properties
        /// <summary>
        /// Gets the authentication contexts cached by the pool.
        /// </summary>
        ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> AuthenticationContexts { get; }

        /// <summary>
        /// Gets the factory used to create database connections.
        /// </summary>
        SqlConnectionFactory ConnectionFactory { get; }

        /// <summary>
        /// The number of connections currently managed by the pool.
        /// May be larger than the number of connections currently sitting idle in the pool.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Indicates whether an error has occurred in the pool.
        /// Primarily used to support the pool blocking period feature.
        /// </summary>
        bool ErrorOccurred { get; }

        /// <summary>
        /// An id that uniqely identifies this connection pool.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets the identity used by the connection pool when establishing connections.
        /// </summary>
        DbConnectionPoolIdentity Identity { get; }

        /// <summary>
        /// Indicates whether the connection pool is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets the duration of time to wait before reassigning a connection to a different server in a load-balanced
        /// environment.
        /// </summary>
        TimeSpan LoadBalanceTimeout { get; }

        /// <summary>
        /// Gets a reference to the connection pool group that this pool belongs to.
        /// </summary>
        DbConnectionPoolGroup PoolGroup { get; }

        /// <summary>
        /// Gets the options for the connection pool group.
        /// </summary>
        DbConnectionPoolGroupOptions PoolGroupOptions { get; }

        /// <summary>
        /// Gets the provider information for the connection pool.
        /// </summary>
        DbConnectionPoolProviderInfo ProviderInfo { get; }

        /// <summary>
        /// The current state of the connection pool.
        /// </summary>
        DbConnectionPoolState State { get; }

        /// <summary>
        /// Holds connections that are currently enlisted in a transaction.
        /// </summary>
        TransactedConnectionPool TransactedConnectionPool { get; }

        /// <summary>
        /// Indicates whether the connection pool is using load balancing.
        /// </summary>
        bool UseLoadBalancing { get; }
        #endregion

        #region Methods
        /// <summary>
        /// Clears the connection pool, releasing all connections and resetting the state.
        /// </summary>
        void Clear();

        /// <summary>
        /// Attempts to get a connection from the pool.
        /// </summary>
        /// <param name="owningObject">The SqlConnection that will own this internal connection.</param>
        /// <param name="taskCompletionSource">Used when calling this method in an async context. 
        /// The internal connection will be set on completion source rather than passed out via the out parameter.</param>
        /// <param name="userOptions">The user options to use if a new connection must be opened.</param>
        /// <param name="connection">The retrieved connection will be passed out via this parameter.</param>
        /// <returns>True if a connection was set in the out parameter, otherwise returns false.</returns>
        bool TryGetConnection(DbConnection owningObject, TaskCompletionSource<DbConnectionInternal>? taskCompletionSource, DbConnectionOptions userOptions, out DbConnectionInternal? connection);

        /// <summary>
        /// Replaces the internal connection currently associated with owningObject with a new internal connection from the pool.
        /// </summary>
        /// <param name="owningObject">The connection whos internal connection should be replaced.</param>
        /// <param name="userOptions">The user options to use if a new connection must be opened.</param>
        /// <param name="oldConnection">The internal connection currently associated with the owning object.</param>
        /// <returns>A reference to the new DbConnectionInternal.</returns>
        DbConnectionInternal ReplaceConnection(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection);

        /// <summary>
        /// Returns an internal connection to the pool.
        /// </summary>
        /// <param name="obj">The internal connection to return to the pool.</param>
        /// <param name="owningObject">The connection that currently owns this internal connection. Used to verify ownership.</param>
        void ReturnInternalConnection(DbConnectionInternal obj, DbConnection owningObject);

        /// <summary>
        /// Puts an internal connection from a transacted pool back into the general pool.
        /// </summary>
        /// <param name="obj">The internal connection to return to the pool.</param>
        void PutObjectFromTransactedPool(DbConnectionInternal obj);

        /// <summary>
        /// Initializes and starts the connection pool. Should be called once when the pool is created.
        /// </summary>
        void Startup();

        /// <summary>
        /// Shuts down the connection pool releasing any resources. Should be called once when the pool is no longer needed.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Informs the pool that a transaction has ended. The pool will commit and reset any internal
        /// the transacted object associated with this transaction.
        /// </summary>
        /// <param name="transaction">The transaction that has ended.</param>
        /// <param name="transactedObject">The internal connection that should be committed and reset.</param>
        void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject);
        #endregion
    }
}

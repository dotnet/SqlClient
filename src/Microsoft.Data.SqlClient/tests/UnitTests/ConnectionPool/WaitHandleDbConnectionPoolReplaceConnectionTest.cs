// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Transactions;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool;

/// <summary>
/// Verifies replacement rollback behavior in <see cref="WaitHandleDbConnectionPool"/>.
/// </summary>
public sealed class WaitHandleDbConnectionPoolReplaceConnectionTest
{
    /// <summary>
    /// Verifies that an activation failure disposes the failed replacement and restores the
    /// original connection as the pool's single tracked, checked-out connection.
    /// </summary>
    [Fact]
    public void ReplaceConnection_ActivationFails_RestoresOriginalConnectionAndCounters()
    {
        // Arrange
        var factory = new ReplacementActivationFailingConnectionFactory();
        WaitHandleDbConnectionPool pool = CreatePool(factory);
        using var owner = new SqlConnection();

        Assert.True(pool.TryGetConnection(
            owner,
            taskCompletionSource: null,
            TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
            out DbConnectionInternal? oldConnection));

        TrackingConnection original = Assert.IsType<TrackingConnection>(oldConnection);

        // Act
        Assert.Throws<InvalidOperationException>(() =>
            pool.ReplaceConnection(
                owner,
                original,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));

        // Assert
        Assert.False(original.IsDisposed);
        Assert.Same(pool, original.Pool);
        Assert.True(factory.Replacement.IsDisposed);
        Assert.Null(factory.Replacement.Pool);
        Assert.Equal(1, pool.Count);

        pool.ReturnInternalConnection(original, owner);
        pool.Shutdown();
        pool.Clear();
    }

    /// <summary>
    /// Creates and starts a WaitHandle pool backed by the supplied factory.
    /// </summary>
    /// <param name="factory">Factory used to create the original and replacement connections.</param>
    /// <returns>A running connection pool.</returns>
    private static WaitHandleDbConnectionPool CreatePool(SqlConnectionFactory factory)
    {
        var poolGroupOptions = new DbConnectionPoolGroupOptions(
            poolByIdentity: false,
            minPoolSize: 0,
            maxPoolSize: 50,
            creationTimeout: 15_000,
            loadBalanceTimeout: 0,
            hasTransactionAffinity: true,
            idleTimeout: 0);

        var poolGroup = new DbConnectionPoolGroup(
            new SqlConnectionOptions("Data Source=localhost;"),
            new ConnectionPoolKey(
                "TestDataSource",
                credential: null,
                accessToken: null,
                accessTokenCallback: null,
                sspiContextProvider: null),
            poolGroupOptions);

        var pool = new WaitHandleDbConnectionPool(
            factory,
            poolGroup,
            DbConnectionPoolIdentity.NoIdentity,
            new DbConnectionPoolProviderInfo());

        pool.Startup();
        return pool;
    }

    /// <summary>
    /// Creates a normal original connection followed by a replacement that fails activation.
    /// </summary>
    private sealed class ReplacementActivationFailingConnectionFactory : SqlConnectionFactory
    {
        private int _created;

        internal TrackingConnection Replacement { get; private set; } = null!;

        /// <inheritdoc />
        protected override DbConnectionInternal CreateConnection(
            SqlConnectionOptions options,
            ConnectionPoolKey poolKey,
            DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
            IDbConnectionPool pool,
            DbConnection owningConnection,
            TimeoutTimer timeout)
        {
            bool failActivation = Interlocked.Increment(ref _created) > 1;
            var connection = new TrackingConnection(failActivation);
            if (failActivation)
            {
                Replacement = connection;
            }

            return connection;
        }
    }

    /// <summary>
    /// Records disposal and optionally fails when activated.
    /// </summary>
    private sealed class TrackingConnection : DbConnectionInternal
    {
        private readonly bool _failActivation;

        internal TrackingConnection(bool failActivation)
        {
            _failActivation = failActivation;
        }

        internal bool IsDisposed { get; private set; }

        public override string ServerVersion => "Mock";

        public override DbTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel) =>
            throw new NotImplementedException();

        public override void EnlistTransaction(Transaction? transaction)
        {
            EnlistedTransaction = transaction;
        }

        protected override void Activate(Transaction? transaction)
        {
            if (_failActivation)
            {
                throw new InvalidOperationException("Simulated activation failure");
            }

            EnlistedTransaction = transaction;
        }

        protected override void Deactivate()
        {
        }

        internal override void ResetConnection()
        {
        }

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }
    }
}

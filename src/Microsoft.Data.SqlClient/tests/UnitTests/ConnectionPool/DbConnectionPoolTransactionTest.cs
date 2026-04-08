// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool;

/// <summary>
/// Parameterized transaction tests that run against both WaitHandleDbConnectionPool 
/// and ChannelDbConnectionPool to verify identical transaction behavior across pool implementations.
/// </summary>
public class DbConnectionPoolTransactionTest : IDisposable
{
    private const int DefaultMaxPoolSize = 50;
    private const int DefaultMinPoolSize = 0;
    private const int DefaultCreationTimeoutInSeconds = 15;

    private IDbConnectionPool _pool = null!;
    private string _poolType = null!;

    /// <summary>
    /// Provides pool type names for parameterized tests.
    /// </summary>
    public static TheoryData<string> PoolTypes => new()
    {
        "WaitHandle",
        "Channel"
    };

    private void InitializePool(
        string poolType,
        int maxPoolSize = DefaultMaxPoolSize,
        int minPoolSize = DefaultMinPoolSize,
        bool hasTransactionAffinity = true)
    {
        // Clean up previous pool if re-initializing
        if (_pool != null)
        {
            CleanupPool();
        }

        _poolType = poolType;
        _pool = CreatePool(poolType, maxPoolSize, minPoolSize, hasTransactionAffinity);
    }

    private void CleanupPool()
    {
        if (_pool is WaitHandleDbConnectionPool)
        {
            _pool.Shutdown();
            _pool.Clear();
        }
        // ChannelDbConnectionPool doesn't have Shutdown/Clear yet
    }

    public void Dispose()
    {
        // Verify no leaked transactions before cleanup
        Assert.Empty(_pool.TransactedConnectionPool.TransactedConnections);

        CleanupPool();
    }

    #region Helper Methods

    private static IDbConnectionPool CreatePool(
        string poolType,
        int maxPoolSize = DefaultMaxPoolSize,
        int minPoolSize = DefaultMinPoolSize,
        bool hasTransactionAffinity = true)
    {
        var poolGroupOptions = new DbConnectionPoolGroupOptions(
            poolByIdentity: false,
            minPoolSize: minPoolSize,
            maxPoolSize: maxPoolSize,
            creationTimeout: DefaultCreationTimeoutInSeconds * 1000,
            loadBalanceTimeout: 0,
            hasTransactionAffinity: hasTransactionAffinity
        );

        var dbConnectionPoolGroup = new DbConnectionPoolGroup(
            new DbConnectionOptions("DataSource=localhost;", null),
            new DbConnectionPoolKey("TestDataSource"),
            poolGroupOptions
        );

        var connectionFactory = new MockSqlConnectionFactory();

        if (poolType == "WaitHandle")
        {
            var pool = new WaitHandleDbConnectionPool(
                connectionFactory,
                dbConnectionPoolGroup,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo()
            );
            pool.Startup();
            return pool;
        }
        else if (poolType == "Channel")
        {
            return new ChannelDbConnectionPool(
                connectionFactory,
                dbConnectionPoolGroup,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo()
            );
        }

        throw new ArgumentException($"Unknown pool type: {poolType}");
    }

    private DbConnectionInternal GetConnection(SqlConnection owner)
    {
        _pool.TryGetConnection(
            owner,
            taskCompletionSource: null,
            new DbConnectionOptions("", null),
            out DbConnectionInternal? connection);
        return connection!;
    }

    private async Task<DbConnectionInternal> GetConnectionAsync(
        SqlConnection owner,
        Transaction? transaction = null)
    {
        var tcs = new TaskCompletionSource<DbConnectionInternal>(transaction);
        _pool.TryGetConnection(
            owner,
            taskCompletionSource: tcs,
            new DbConnectionOptions("", null),
            out DbConnectionInternal? connection);
        return connection ?? await tcs.Task;
    }

    private void ReturnConnection(DbConnectionInternal connection, SqlConnection owner)
    {
        _pool.ReturnInternalConnection(connection, owner);
    }

    private void AssertPoolMetrics()
    {
        Assert.True(_pool.Count <= _pool.PoolGroupOptions.MaxPoolSize,
            $"Pool count ({_pool.Count}) exceeded max pool size ({_pool.PoolGroupOptions.MaxPoolSize})");
        Assert.True(_pool.Count >= 0,
            $"Pool count ({_pool.Count}) is negative");
        Assert.Empty(_pool.TransactedConnectionPool.TransactedConnections);
    }

    #endregion

    #region Transaction Routing Tests

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void GetConnection_UnderTransaction_RoutesToTransactedPool(string poolType)
    {
        InitializePool(poolType);

        using var scope = new TransactionScope();
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        var owner = new SqlConnection();
        var conn = GetConnection(owner);
        Assert.NotNull(conn);

        ReturnConnection(conn, owner);

        // Connection should be in the transacted pool
        Assert.True(_pool.TransactedConnectionPool.TransactedConnections.ContainsKey(transaction));
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections[transaction]);

        scope.Complete();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void GetConnection_WithoutTransaction_RoutesToGeneralPool(string poolType)
    {
        InitializePool(poolType);

        var owner = new SqlConnection();
        var conn = GetConnection(owner);
        Assert.NotNull(conn);

        ReturnConnection(conn, owner);

        // Transacted pool should be empty
        Assert.Empty(_pool.TransactedConnectionPool.TransactedConnections);
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void GetConnection_UnderTransaction_ReturnsSameConnectionFromTransactedPool(string poolType)
    {
        InitializePool(poolType);

        using var scope = new TransactionScope();

        var owner1 = new SqlConnection();
        var conn1 = GetConnection(owner1);
        Assert.NotNull(conn1);
        ReturnConnection(conn1, owner1);

        // Second call should retrieve the SAME connection from the transacted pool (LIFO)
        var owner2 = new SqlConnection();
        var conn2 = GetConnection(owner2);
        Assert.NotNull(conn2);
        Assert.Same(conn1, conn2);

        ReturnConnection(conn2, owner2);
        scope.Complete();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public async Task GetConnectionAsync_UnderTransaction_ReturnsSameConnectionFromTransactedPool(string poolType)
    {
        InitializePool(poolType);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var transaction = Transaction.Current;

        var owner1 = new SqlConnection();
        var conn1 = await GetConnectionAsync(owner1, transaction: transaction);
        Assert.NotNull(conn1);
        ReturnConnection(conn1, owner1);

        // Second call should retrieve the SAME connection from the transacted pool
        var owner2 = new SqlConnection();
        var conn2 = await GetConnectionAsync(owner2, transaction: transaction);
        Assert.NotNull(conn2);
        Assert.Same(conn1, conn2);

        ReturnConnection(conn2, owner2);
        scope.Complete();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void GetConnection_WithTransactionAffinityDisabled_SkipsTransactedPool(string poolType)
    {
        InitializePool(poolType, hasTransactionAffinity: false);

        using var scope = new TransactionScope();

        var owner = new SqlConnection();
        var conn = GetConnection(owner);
        Assert.NotNull(conn);
        ReturnConnection(conn, owner);

        // Even though a transaction is active, transacted pool is not used
        Assert.Empty(_pool.TransactedConnectionPool.TransactedConnections);

        scope.Complete();
    }

    #endregion

    #region Transaction Lifecycle Tests

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void TransactionCommit_ClearsTransactedPool(string poolType)
    {
        InitializePool(poolType);

        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            // While transaction is active, connection should be in transacted pool
            Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);

            scope.Complete();
        }

        // After transaction completes, transacted pool should be empty
        AssertPoolMetrics();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void TransactionRollback_ClearsTransactedPool(string poolType)
    {
        InitializePool(poolType);

        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);

            // Don't call scope.Complete() — triggers rollback
        }

        // Transacted pool should be empty after rollback too
        AssertPoolMetrics();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void MultipleGetReturn_SameTransaction_ReusesConnection(string poolType)
    {
        InitializePool(poolType);

        using var scope = new TransactionScope();
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        for (int i = 0; i < 10; i++)
        {
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);
        }

        // Only one connection should be in the transacted pool
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections[transaction]);

        scope.Complete();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public async Task MultipleGetReturn_SameTransaction_Async_ReusesConnection(string poolType)
    {
        InitializePool(poolType);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        for (int i = 0; i < 10; i++)
        {
            var owner = new SqlConnection();
            var conn = await GetConnectionAsync(owner, transaction: transaction);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);
        }

        // Only one connection should be in the transacted pool
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections[transaction]);

        scope.Complete();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void AlternatingCommitAndRollback_MaintainsConsistentState(string poolType)
    {
        InitializePool(poolType);

        for (int i = 0; i < 20; i++)
        {
            using var scope = new TransactionScope();
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            if (i % 2 == 0)
            {
                scope.Complete();
            }
            // else: rollback (no Complete)
        }

        AssertPoolMetrics();
    }

    #endregion

    #region Nested Transaction Tests

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void NestedTransaction_Required_SharesSameTransactedEntry(string poolType)
    {
        InitializePool(poolType);

        using var outerScope = new TransactionScope();
        var outerTxn = Transaction.Current;
        Assert.NotNull(outerTxn);

        var owner1 = new SqlConnection();
        var conn1 = GetConnection(owner1);
        Assert.NotNull(conn1);
        ReturnConnection(conn1, owner1);

        // Nested scope with Required shares the same transaction
        using (var innerScope = new TransactionScope(TransactionScopeOption.Required))
        {
            Assert.Same(outerTxn, Transaction.Current);

            var owner2 = new SqlConnection();
            var conn2 = GetConnection(owner2);
            Assert.NotNull(conn2);
            Assert.Same(conn1, conn2); // Same transaction -> same connection from transacted pool
            ReturnConnection(conn2, owner2);

            // Only one transaction tracked
            Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);

            innerScope.Complete();
        }

        outerScope.Complete();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void NestedTransaction_RequiresNew_CreatesSeparateTransactedEntry(string poolType)
    {
        InitializePool(poolType);

        using var outerScope = new TransactionScope();
        var outerTxn = Transaction.Current;
        Assert.NotNull(outerTxn);

        var owner1 = new SqlConnection();
        var conn1 = GetConnection(owner1);
        Assert.NotNull(conn1);
        ReturnConnection(conn1, owner1);

        // Nested scope with RequiresNew creates a new transaction
        using (var innerScope = new TransactionScope(TransactionScopeOption.RequiresNew))
        {
            var innerTxn = Transaction.Current;
            Assert.NotNull(innerTxn);
            Assert.NotEqual(outerTxn, innerTxn);

            var owner2 = new SqlConnection();
            var conn2 = GetConnection(owner2);
            Assert.NotNull(conn2);
            Assert.NotSame(conn1, conn2); // Different transaction -> different connection
            ReturnConnection(conn2, owner2);

            // Two separate transactions tracked
            Assert.Equal(2, _pool.TransactedConnectionPool.TransactedConnections.Count);

            innerScope.Complete();
        }

        outerScope.Complete();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void NestedTransaction_RequiresNew_CompletesIndependently(string poolType)
    {
        InitializePool(poolType);

        using (var outerScope = new TransactionScope())
        {
            var owner1 = new SqlConnection();
            var conn1 = GetConnection(owner1);
            Assert.NotNull(conn1);
            ReturnConnection(conn1, owner1);

            using (var innerScope = new TransactionScope(TransactionScopeOption.RequiresNew))
            {
                var owner2 = new SqlConnection();
                var conn2 = GetConnection(owner2);
                Assert.NotNull(conn2);
                ReturnConnection(conn2, owner2);
                innerScope.Complete();
            }

            // Inner transaction completed - its entry should be cleared
            // Outer transaction entry should still exist
            Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);

            outerScope.Complete();
        }

        // Both completed
        AssertPoolMetrics();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void DeeplyNestedTransactions_RequiresNew_AllTrackedSeparately(string poolType)
    {
        InitializePool(poolType);

        using var scope1 = new TransactionScope();
        var owner1 = new SqlConnection();
        var conn1 = GetConnection(owner1);
        ReturnConnection(conn1, owner1);

        using var scope2 = new TransactionScope(TransactionScopeOption.RequiresNew);
        var owner2 = new SqlConnection();
        var conn2 = GetConnection(owner2);
        ReturnConnection(conn2, owner2);

        using var scope3 = new TransactionScope(TransactionScopeOption.RequiresNew);
        var owner3 = new SqlConnection();
        var conn3 = GetConnection(owner3);
        ReturnConnection(conn3, owner3);

        // Three separate transactions tracked
        Assert.Equal(3, _pool.TransactedConnectionPool.TransactedConnections.Count);

        scope3.Complete();
        scope2.Complete();
        scope1.Complete();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void DeeplyNestedTransactions_Required_AllShareOneEntry(string poolType)
    {
        InitializePool(poolType);

        using var scope1 = new TransactionScope();
        var txn = Transaction.Current;
        var owner1 = new SqlConnection();
        var conn1 = GetConnection(owner1);
        ReturnConnection(conn1, owner1);

        using var scope2 = new TransactionScope(TransactionScopeOption.Required);
        Assert.Same(txn, Transaction.Current);
        var owner2 = new SqlConnection();
        var conn2 = GetConnection(owner2);
        Assert.Same(conn1, conn2);
        ReturnConnection(conn2, owner2);

        using var scope3 = new TransactionScope(TransactionScopeOption.Required);
        Assert.Same(txn, Transaction.Current);
        var owner3 = new SqlConnection();
        var conn3 = GetConnection(owner3);
        Assert.Same(conn1, conn3);
        ReturnConnection(conn3, owner3);

        // Single transaction entry
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);

        scope3.Complete();
        scope2.Complete();
        scope1.Complete();
    }

    #endregion

    #region Mixed Transacted and Non-Transacted Tests

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void MixedWorkload_AlternatingTransactedAndNonTransacted(string poolType)
    {
        InitializePool(poolType);

        for (int i = 0; i < 10; i++)
        {
            if (i % 2 == 0)
            {
                using var scope = new TransactionScope();
                var owner = new SqlConnection();
                var conn = GetConnection(owner);
                Assert.NotNull(conn);
                ReturnConnection(conn, owner);
                scope.Complete();
            }
            else
            {
                var owner = new SqlConnection();
                var conn = GetConnection(owner);
                Assert.NotNull(conn);
                ReturnConnection(conn, owner);
            }
        }

        AssertPoolMetrics();
    }

    #endregion

    #region Shared Transaction Tests

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void SharedTransaction_DependentScopes_UseTransactedPool(string poolType)
    {
        InitializePool(poolType);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        var owner1 = new SqlConnection();
        var conn1 = GetConnection(owner1);
        Assert.NotNull(conn1);
        ReturnConnection(conn1, owner1);

        // Use dependent scope on same transaction
        using (var innerScope = new TransactionScope(transaction))
        {
            Assert.Same(transaction, Transaction.Current);
            var owner2 = new SqlConnection();
            var conn2 = GetConnection(owner2);
            Assert.NotNull(conn2);
            Assert.Same(conn1, conn2); // Same transaction -> same connection
            ReturnConnection(conn2, owner2);
            innerScope.Complete();
        }

        // Still one transaction entry
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections[transaction]);

        scope.Complete();
    }

    #endregion

    #region Controlled Concurrency Tests

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void TwoThreads_SharedTransaction_AccessSameTransactedEntry(string poolType)
    {
        InitializePool(poolType);

        using var task1Returned = new ManualResetEventSlim(false);
        DbConnectionInternal? connFromTask1 = null;
        DbConnectionInternal? connFromTask2 = null;

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        var task1 = Task.Run(() =>
        {
            using var innerScope = new TransactionScope(transaction);
            var owner = new SqlConnection();
            connFromTask1 = GetConnection(owner);
            Assert.NotNull(connFromTask1);

            // Return the connection so it's available in the transacted pool
            ReturnConnection(connFromTask1, owner);
            innerScope.Complete();

            task1Returned.Set();
        });

        var task2 = Task.Run(() =>
        {
            Assert.True(task1Returned.Wait(TimeSpan.FromSeconds(10)),
                "Timed out waiting for task1 to return its connection.");

            using var innerScope = new TransactionScope(transaction);
            var owner = new SqlConnection();
            connFromTask2 = GetConnection(owner);
            Assert.NotNull(connFromTask2);
            ReturnConnection(connFromTask2, owner);
            innerScope.Complete();
        });

        Task.WaitAll(task1, task2);

        // Both tasks should have received the same connection via the transacted pool
        Assert.Same(connFromTask1, connFromTask2);
        scope.Complete();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public async Task TwoThreads_SeparateTransactions_Async_IsolatedTransactedEntries(string poolType)
    {
        InitializePool(poolType);

        using var barrier = new SemaphoreSlim(0, 2);

        var task1 = Task.Run(async () =>
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var transaction = Transaction.Current;
            var owner = new SqlConnection();
            var conn = await GetConnectionAsync(owner, transaction: transaction);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            barrier.Release();
            await barrier.WaitAsync();

            scope.Complete();
        });

        var task2 = Task.Run(async () =>
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var transaction = Transaction.Current;
            var owner = new SqlConnection();
            var conn = await GetConnectionAsync(owner, transaction: transaction);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            barrier.Release();
            await barrier.WaitAsync();

            scope.Complete();
        });

        await Task.WhenAll(task1, task2);

        AssertPoolMetrics();
    }

    #endregion

    #region Transaction Complete Before Return Tests

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void TransactionComplete_ThenReturn_ConnectionStillReturned(string poolType)
    {
        InitializePool(poolType);

        var owner = new SqlConnection();
        DbConnectionInternal conn;

        using (var scope = new TransactionScope())
        {
            conn = GetConnection(owner);
            Assert.NotNull(conn);
            scope.Complete();
        }
        // Transaction is fully disposed here

        // Return connection after transaction ended
        ReturnConnection(conn, owner);

        // No leak, pool metrics consistent
        AssertPoolMetrics();
        Assert.True(_pool.Count > 0, "Pool should still have the connection");
    }

    #endregion

    #region Sequential Transaction Isolation Tests

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void SequentialTransactions_EachGetsOwnTransactedEntry(string poolType)
    {
        InitializePool(poolType);

        for (int i = 0; i < 5; i++)
        {
            using var scope = new TransactionScope();
            var transaction = Transaction.Current;
            Assert.NotNull(transaction);

            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            // Only the current transaction should be tracked
            Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);
            Assert.True(_pool.TransactedConnectionPool.TransactedConnections.ContainsKey(transaction));

            scope.Complete();
        }

        AssertPoolMetrics();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public async Task SequentialTransactions_Async_EachGetsOwnTransactedEntry(string poolType)
    {
        InitializePool(poolType);

        for (int i = 0; i < 5; i++)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var transaction = Transaction.Current;
            Assert.NotNull(transaction);

            var owner = new SqlConnection();
            var conn = await GetConnectionAsync(owner, transaction: transaction);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);
            Assert.True(_pool.TransactedConnectionPool.TransactedConnections.ContainsKey(transaction));

            scope.Complete();
        }

        AssertPoolMetrics();
    }

    [Theory]
    [MemberData(nameof(PoolTypes))]
    public void SequentialTransactions_CanReuseConnections(string poolType)
    {
        InitializePool(poolType);

        DbConnectionInternal conn1;
        DbConnectionInternal conn2;
        Transaction? txn1;
        Transaction? txn2;

        using (var scope1 = new TransactionScope())
        {
            txn1 = Transaction.Current;
            var owner1 = new SqlConnection();
            conn1 = GetConnection(owner1);
            Assert.NotNull(conn1);
            ReturnConnection(conn1, owner1);
            scope1.Complete();
        }

        using (var scope2 = new TransactionScope())
        {
            txn2 = Transaction.Current;
            var owner2 = new SqlConnection();
            conn2 = GetConnection(owner2);
            Assert.NotNull(conn2);
            ReturnConnection(conn2, owner2);
            scope2.Complete();
        }

        // The connection was returned to the general pool and picked up by the second transaction
        Assert.NotSame(txn1, txn2);
        Assert.Same(conn1, conn2);
        AssertPoolMetrics();
    }

    #endregion

    #region Mock Classes

    internal class MockSqlConnectionFactory : SqlConnectionFactory
    {
        protected override DbConnectionInternal CreateConnection(
            DbConnectionOptions options,
            DbConnectionPoolKey poolKey,
            DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
            IDbConnectionPool pool,
            DbConnection owningConnection,
            DbConnectionOptions userOptions)
        {
            return new MockDbConnectionInternal();
        }
    }

    internal class MockDbConnectionInternal : DbConnectionInternal
    {
        private static int s_nextId = 1;
        public int MockId { get; } = Interlocked.Increment(ref s_nextId);

        public override string ServerVersion => "Mock";

        public override DbTransaction BeginTransaction(System.Data.IsolationLevel il)
        {
            throw new NotImplementedException();
        }

        public override void EnlistTransaction(Transaction? transaction)
        {
            if (transaction != null)
            {
                EnlistedTransaction = transaction;
            }
        }

        protected override void Activate(Transaction? transaction)
        {
            EnlistedTransaction = transaction;
        }

        protected override void Deactivate()
        {
        }

        public override string ToString() => $"MockConnection_{MockId}";

        internal override void ResetConnection()
        {
        }
    }

    #endregion
}

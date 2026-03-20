// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
/// Deterministic tests for WaitHandleDbConnectionPool transaction functionality.
/// These tests exercise transacted connection pathways with controlled synchronization
/// to verify correct behavior without relying on probabilistic concurrency.
/// </summary>
public class WaitHandleDbConnectionPoolTransactionTest : IDisposable
{
    private const int DefaultMaxPoolSize = 50;
    private const int DefaultMinPoolSize = 0;
    private const int DefaultCreationTimeoutInMilliseconds = 15000;

    private IDbConnectionPool _pool = null!;

    public WaitHandleDbConnectionPoolTransactionTest()
    {
        _pool = CreatePool();
    }

    public void Dispose()
    {
        _pool?.Shutdown();
        _pool?.Clear();
    }

    #region Helper Methods

    private WaitHandleDbConnectionPool CreatePool(
        int maxPoolSize = DefaultMaxPoolSize,
        int minPoolSize = DefaultMinPoolSize,
        bool hasTransactionAffinity = true)
    {
        var poolGroupOptions = new DbConnectionPoolGroupOptions(
            poolByIdentity: false,
            minPoolSize: minPoolSize,
            maxPoolSize: maxPoolSize,
            creationTimeout: DefaultCreationTimeoutInMilliseconds,
            loadBalanceTimeout: 0,
            hasTransactionAffinity: hasTransactionAffinity
        );

        var dbConnectionPoolGroup = new DbConnectionPoolGroup(
            new DbConnectionOptions("DataSource=localhost;", null),
            new DbConnectionPoolKey("TestDataSource"),
            poolGroupOptions
        );

        var connectionFactory = new MockSqlConnectionFactory();

        var pool = new WaitHandleDbConnectionPool(
            connectionFactory,
            dbConnectionPoolGroup,
            DbConnectionPoolIdentity.NoIdentity,
            new DbConnectionPoolProviderInfo()
        );

        pool.Startup();
        return pool;
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

    [Fact]
    public void GetConnection_UnderTransaction_RoutesToTransactedPool()
    {
        // Arrange & Act
        using var scope = new TransactionScope();
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        var owner = new SqlConnection();
        var conn = GetConnection(owner);
        Assert.NotNull(conn);

        ReturnConnection(conn, owner);

        // Assert - connection should be in the transacted pool
        Assert.True(_pool.TransactedConnectionPool.TransactedConnections.ContainsKey(transaction));
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections[transaction]);

        scope.Complete();
    }

    [Fact]
    public void GetConnection_WithoutTransaction_RoutesToGeneralPool()
    {
        // Arrange & Act (no TransactionScope)
        var owner = new SqlConnection();
        var conn = GetConnection(owner);
        Assert.NotNull(conn);

        ReturnConnection(conn, owner);

        // Assert - transacted pool should be empty
        Assert.Empty(_pool.TransactedConnectionPool.TransactedConnections);
    }

    [Fact]
    public void GetConnection_UnderTransaction_ReturnsSameConnectionFromTransactedPool()
    {
        // Arrange
        using var scope = new TransactionScope();

        // Act - first call creates a new connection
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

    [Fact]
    public async Task GetConnectionAsync_UnderTransaction_ReturnsSameConnectionFromTransactedPool()
    {
        // Arrange
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var transaction = Transaction.Current;

        // Act - first call creates a new connection
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

    [Fact]
    public void GetConnection_WithTransactionAffinityDisabled_SkipsTransactedPool()
    {
        // Arrange
        _pool.Shutdown();
        _pool.Clear();
        _pool = CreatePool(hasTransactionAffinity: false);

        using var scope = new TransactionScope();

        // Act
        var owner = new SqlConnection();
        var conn = GetConnection(owner);
        Assert.NotNull(conn);
        ReturnConnection(conn, owner);

        // Assert - even though a transaction is active, transacted pool is not used
        Assert.Empty(_pool.TransactedConnectionPool.TransactedConnections);

        scope.Complete();
    }

    #endregion

    #region Transaction Lifecycle Tests

    [Fact]
    public void TransactionCommit_ClearsTransactedPool()
    {
        // Arrange & Act
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

        // Assert - after transaction completes, transacted pool should be empty
        AssertPoolMetrics();
    }

    [Fact]
    public void TransactionRollback_ClearsTransactedPool()
    {
        // Arrange & Act
        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);

            // Don't call scope.Complete() — triggers rollback
        }

        // Assert - transacted pool should be empty after rollback too
        AssertPoolMetrics();
    }

    [Fact]
    public void MultipleGetReturn_SameTransaction_ReusesConnection()
    {
        // Arrange
        using var scope = new TransactionScope();
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        // Act - get and return multiple times within same transaction
        for (int i = 0; i < 10; i++)
        {
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);
        }

        // Assert - only one connection should be in the transacted pool
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections[transaction]);

        scope.Complete();
    }

    [Fact]
    public async Task MultipleGetReturn_SameTransaction_Async_ReusesConnection()
    {
        // Arrange
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        // Act - get and return multiple times within same transaction
        for (int i = 0; i < 10; i++)
        {
            var owner = new SqlConnection();
            var conn = await GetConnectionAsync(owner, transaction: transaction);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);
        }

        // Assert - only one connection should be in the transacted pool
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections[transaction]);

        scope.Complete();
    }

    [Fact]
    public void AlternatingCommitAndRollback_MaintainsConsistentState()
    {
        // Act - alternate between commit and rollback
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

        // Assert
        AssertPoolMetrics();
    }

    #endregion

    #region Nested Transaction Tests

    [Fact]
    public void NestedTransaction_Required_SharesSameTransactedEntry()
    {
        // Arrange
        using var outerScope = new TransactionScope();
        var outerTxn = Transaction.Current;
        Assert.NotNull(outerTxn);

        var owner1 = new SqlConnection();
        var conn1 = GetConnection(owner1);
        Assert.NotNull(conn1);
        ReturnConnection(conn1, owner1);

        // Act - nested scope with Required shares the same transaction
        using (var innerScope = new TransactionScope(TransactionScopeOption.Required))
        {
            Assert.Equal(outerTxn, Transaction.Current);

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

    [Fact]
    public void NestedTransaction_RequiresNew_CreatesSeparateTransactedEntry()
    {
        // Arrange
        using var outerScope = new TransactionScope();
        var outerTxn = Transaction.Current;
        Assert.NotNull(outerTxn);

        var owner1 = new SqlConnection();
        var conn1 = GetConnection(owner1);
        Assert.NotNull(conn1);
        ReturnConnection(conn1, owner1);

        // Act - nested scope with RequiresNew creates a new transaction
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

    [Fact]
    public void NestedTransaction_RequiresNew_CompletesIndependently()
    {
        // Arrange & Act
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

    [Fact]
    public void DeeplyNestedTransactions_RequiresNew_AllTrackedSeparately()
    {
        // Arrange & Act
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

        // Assert - three separate transactions tracked
        Assert.Equal(3, _pool.TransactedConnectionPool.TransactedConnections.Count);

        scope3.Complete();
        scope2.Complete();
        scope1.Complete();
    }

    [Fact]
    public void DeeplyNestedTransactions_Required_AllShareOneEntry()
    {
        // Arrange & Act
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

        // Assert - single transaction entry
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);

        scope3.Complete();
        scope2.Complete();
        scope1.Complete();
    }

    #endregion

    #region Mixed Transacted and Non-Transacted Tests

    [Fact]
    public void MixedWorkload_AlternatingTransactedAndNonTransacted()
    {
        // Act - alternate between transacted and non-transacted
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

        // Assert
        AssertPoolMetrics();
    }

    #endregion

    #region Shared Transaction Tests

    [Fact]
    public void SharedTransaction_DependentScopes_UseTransactedPool()
    {
        // Arrange
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        // Act - first connection
        var owner1 = new SqlConnection();
        var conn1 = GetConnection(owner1);
        Assert.NotNull(conn1);
        ReturnConnection(conn1, owner1);

        // Use dependent scope on same transaction
        using (var innerScope = new TransactionScope(transaction))
        {
            Assert.Equal(transaction, Transaction.Current);
            var owner2 = new SqlConnection();
            var conn2 = GetConnection(owner2);
            Assert.NotNull(conn2);
            Assert.Same(conn1, conn2); // Same transaction -> same connection
            ReturnConnection(conn2, owner2);
            innerScope.Complete();
        }

        // Assert - still one transaction entry
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections[transaction]);

        scope.Complete();
    }

    #endregion

    #region Pool Saturation with Transactions Tests

    [Fact]
    public void PoolSaturation_HoldAndRelease_ConnectionAvailableAfterRelease()
    {
        // Arrange - pool with only 1 connection
        _pool.Shutdown();
        _pool.Clear();
        _pool = CreatePool(maxPoolSize: 1);

        using var holdBarrier = new ManualResetEventSlim(false);
        using var acquiredSignal = new ManualResetEventSlim(false);

        // Act - thread 1: acquire and hold the only connection
        var holdTask = Task.Run(() =>
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);

            acquiredSignal.Set();     // Signal: connection is held
            holdBarrier.Wait();       // Wait until test says to release

            ReturnConnection(conn, owner);
            scope.Complete();
        });

        acquiredSignal.Wait(); // Wait until thread 1 has the connection
        Assert.Equal(1, _pool.Count);

        // Release and let thread 1 return the connection
        holdBarrier.Set();
        holdTask.Wait();

        // Assert - now we can get a connection again
        using var scope2 = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var owner2 = new SqlConnection();
        var conn2 = GetConnection(owner2);
        Assert.NotNull(conn2);
        ReturnConnection(conn2, owner2);
        scope2.Complete();
    }

    [Fact]
    public void PoolSaturation_PostRelease_ConnectionObtainable()
    {
        // Arrange - small pool
        _pool.Shutdown();
        _pool.Clear();
        _pool = CreatePool(maxPoolSize: 3);

        // Act - acquire all 3 connections under separate transactions and return them
        for (int i = 0; i < 3; i++)
        {
            using var scope = new TransactionScope();
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);
            scope.Complete();
        }

        // Assert - all are returned, we can still get connections
        var owner2 = new SqlConnection();
        var conn2 = GetConnection(owner2);
        Assert.NotNull(conn2);
        ReturnConnection(conn2, owner2);
        AssertPoolMetrics();
    }

    #endregion

    #region Controlled Concurrency Tests

    [Fact]
    public void TwoThreads_SeparateTransactions_IsolatedTransactedEntries()
    {
        // Arrange
        using var barrier = new Barrier(2);
        DbConnectionInternal? conn1 = null;
        DbConnectionInternal? conn2 = null;

        // Act - two threads each with their own transaction
        var task1 = Task.Run(() =>
        {
            using var scope = new TransactionScope();
            var owner = new SqlConnection();
            conn1 = GetConnection(owner);
            Assert.NotNull(conn1);
            ReturnConnection(conn1, owner);

            barrier.SignalAndWait(); // Sync: both threads have connections in their transacted pools
            barrier.SignalAndWait(); // Sync: wait before completing

            scope.Complete();
        });

        var task2 = Task.Run(() =>
        {
            using var scope = new TransactionScope();
            var owner = new SqlConnection();
            conn2 = GetConnection(owner);
            Assert.NotNull(conn2);
            ReturnConnection(conn2, owner);

            barrier.SignalAndWait(); // Sync: both threads have connections
            barrier.SignalAndWait(); // Sync: wait before completing

            scope.Complete();
        });

        Task.WaitAll(task1, task2);

        // Assert - both transactions completed, pool is clean
        Assert.NotSame(conn1, conn2); // Different transactions -> different connections
        AssertPoolMetrics();
    }

    [Fact]
    public void TwoThreads_SharedTransaction_AccessSameTransactedEntry()
    {
        // Arrange
        using var barrier = new Barrier(2);

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        // Act - two threads sharing the same transaction
        var task1 = Task.Run(() =>
        {
            using var innerScope = new TransactionScope(transaction);
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);

            barrier.SignalAndWait(); // Sync: both have run

            ReturnConnection(conn, owner);
            innerScope.Complete();
        });

        var task2 = Task.Run(() =>
        {
            barrier.SignalAndWait(); // Sync: wait for thread 1 to get connection first

            using var innerScope = new TransactionScope(transaction);
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);
            innerScope.Complete();
        });

        Task.WaitAll(task1, task2);
        scope.Complete();
    }

    [Fact]
    public async Task TwoTasks_SeparateTransactions_Async_IsolatedTransactedEntries()
    {
        // Arrange
        using var barrier = new SemaphoreSlim(0, 2);

        // Act
        var task1 = Task.Run(async () =>
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var transaction = Transaction.Current;
            var owner = new SqlConnection();
            var conn = await GetConnectionAsync(owner, transaction: transaction);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            barrier.Release(); // Signal ready
            await barrier.WaitAsync(); // Wait for other task

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

            barrier.Release(); // Signal ready
            await barrier.WaitAsync(); // Wait for other task

            scope.Complete();
        });

        await Task.WhenAll(task1, task2);

        // Assert
        AssertPoolMetrics();
    }

    #endregion

    #region Pool Shutdown with Transactions Tests

    [Fact]
    public void PoolShutdown_AfterTransactionComplete_NoLeaks()
    {
        // Arrange
        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);
            scope.Complete();
        }

        // Act
        _pool.Shutdown();

        // Assert
        AssertPoolMetrics();
    }

    [Fact]
    public void PoolShutdown_WhileConnectionHeld_NoException()
    {
        // Arrange
        using var scope = new TransactionScope();
        var owner = new SqlConnection();
        var conn = GetConnection(owner);
        Assert.NotNull(conn);

        // Act - shutdown while connection is held (not yet returned)
        _pool.Shutdown();

        // Return after shutdown - should not throw
        ReturnConnection(conn, owner);
    }

    #endregion

    #region Transaction Complete Before Return Tests

    [Fact]
    public void TransactionComplete_ThenReturn_ConnectionStillReturned()
    {
        // Arrange
        var owner = new SqlConnection();
        DbConnectionInternal conn;

        using (var scope = new TransactionScope())
        {
            conn = GetConnection(owner);
            Assert.NotNull(conn);
            scope.Complete();
        }
        // Transaction is fully disposed here

        // Act - return connection after transaction ended
        ReturnConnection(conn, owner);

        // Assert - no leak, pool metrics consistent
        AssertPoolMetrics();
        Assert.True(_pool.Count > 0, "Pool should still have the connection");
    }

    #endregion

    #region Sequential Transaction Isolation Tests

    [Fact]
    public void SequentialTransactions_EachGetsOwnTransactedEntry()
    {
        // Act - create multiple sequential transactions
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
            Assert.True(_pool.TransactedConnectionPool.TransactedConnections.ContainsKey(transaction));

            scope.Complete();
        }

        // Assert - after all are done, pool should be clean
        AssertPoolMetrics();
    }

    [Fact]
    public async Task SequentialTransactions_Async_EachGetsOwnTransactedEntry()
    {
        // Act - create multiple sequential transactions
        for (int i = 0; i < 5; i++)
        {
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
            var transaction = Transaction.Current;
            Assert.NotNull(transaction);

            var owner = new SqlConnection();
            var conn = await GetConnectionAsync(owner, transaction: transaction);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            Assert.True(_pool.TransactedConnectionPool.TransactedConnections.ContainsKey(transaction));

            scope.Complete();
        }

        // Assert
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

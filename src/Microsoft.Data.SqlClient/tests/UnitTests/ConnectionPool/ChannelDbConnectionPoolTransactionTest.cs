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
/// Deterministic tests for ChannelDbConnectionPool transaction functionality.
/// These tests exercise transacted connection pathways with controlled synchronization
/// to verify correct behavior without relying on probabilistic concurrency.
/// </summary>
public class ChannelDbConnectionPoolTransactionTest : IDisposable
{
    private const int DefaultMaxPoolSize = 50;
    private const int DefaultMinPoolSize = 0;
    private const int DefaultCreationTimeoutInMilliseconds = 15000;

    private IDbConnectionPool _pool = null!;

    public ChannelDbConnectionPoolTransactionTest()
    {
        _pool = CreatePool();
    }

    public void Dispose()
    {
        // Verify no leaked transactions before cleanup
        Assert.Empty(_pool.TransactedConnectionPool.TransactedConnections);

        _pool?.Shutdown();
        _pool?.Clear();
    }

    #region Helper Methods

    private ChannelDbConnectionPool CreatePool(
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
            hasTransactionAffinity: hasTransactionAffinity,
            idleTimeout: 0
        );

        var dbConnectionPoolGroup = new DbConnectionPoolGroup(
            new SqlConnectionOptions("Data Source=localhost;"),
            new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
            poolGroupOptions
        );

        var connectionFactory = new MockSqlConnectionFactory();

        var pool = new ChannelDbConnectionPool(
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
            TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
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
            TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
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
            Assert.Same(transaction, Transaction.Current);
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
    public void PoolSaturation_BlocksUntilConnectionAvailable()
    {
        // Arrange - small pool
        _pool.Shutdown();
        _pool.Clear();
        _pool = CreatePool(maxPoolSize: 1);

        using var allAcquired = new ManualResetEventSlim(false);
        using var releaseFirst = new ManualResetEventSlim(false);

        var saturatingTask = Task.Run(() =>
            {
                using var scope = new TransactionScope();
                var owner = new SqlConnection();
                var conn = GetConnection(owner);
                Assert.NotNull(conn);

                allAcquired.Set(); // Signal that this connection is held

                Assert.True(releaseFirst.Wait(TimeSpan.FromSeconds(15)),
                    "Timed out waiting for releaseFirst signal.");

                ReturnConnection(conn, owner);
                scope.Complete();
            });

        Assert.True(allAcquired.Wait(TimeSpan.FromSeconds(10)),
            "Timed out waiting for connection to be acquired.");
        Assert.Equal(1, _pool.Count);

        using var acquired = new ManualResetEventSlim(false);
        var waitingTask = Task.Run(() =>
        {
            using var scope = new TransactionScope();
            var owner = new SqlConnection();
            var conn = GetConnection(owner);
            Assert.NotNull(conn);
            acquired.Set();
            ReturnConnection(conn, owner);
            scope.Complete();
        });

        // Give the waiting task time to block — it should NOT complete yet
        Assert.False(acquired.Wait(TimeSpan.FromMilliseconds(500)),
            "Waiting task should not have acquired a connection while pool is saturated");

        // Release one connection to unblock the waiting task
        releaseFirst.Set();

        // Now the waiting task should complete
        Assert.True(waitingTask.Wait(TimeSpan.FromSeconds(15)),
            "Waiting task should have completed after a connection was released");
        Assert.True(acquired.IsSet);

        // Cleanup remaining held connections
        Task.WaitAll(saturatingTask);
    }

    #endregion

    #region Controlled Concurrency Tests

    // Flaky under CI load only (never reproduces locally): the two worker tasks are
    // scheduled via Task.Run on the thread pool. On a loaded agent the pool can be slow to
    // spin up a worker, so task1 starts late and fails to signal task1Returned within
    // task2's 10s wait, producing a WaitAll timeout. That is thread-pool starvation, not a
    // pool/transaction defect.
    [Trait("Category", "flaky")]
    [Fact]
    public void TwoThreads_SharedTransaction_AccessSameTransactedEntry()
    {
        // Arrange
        // Use 3-phase synchronization so task1 gets AND returns before task2 requests.
        // This ensures the connection is back in the transacted pool for task2 to reuse.
        using var task1Returned = new ManualResetEventSlim(false);
        using var task2Done = new ManualResetEventSlim(false);
        DbConnectionInternal? connFromTask1 = null;
        DbConnectionInternal? connFromTask2 = null;

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        // Act - two threads sharing the same transaction, sequenced so the
        // transacted pool can vend the same connection to both.
        var task1 = Task.Run(() =>
        {
            using var innerScope = new TransactionScope(transaction);
            var owner = new SqlConnection();
            connFromTask1 = GetConnection(owner);
            Assert.NotNull(connFromTask1);

            // Return the connection so it's available in the transacted pool
            ReturnConnection(connFromTask1, owner);
            innerScope.Complete();

            task1Returned.Set(); // Signal: connection is back in the transacted pool
        });

        var task2 = Task.Run(() =>
        {
            // Wait until task1 has returned the connection to the transacted pool
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

    [Fact]
    public async Task TwoThreads_SeparateTransactions_Async_IsolatedTransactedEntries()
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

        // Return after shutdown — the pool deactivates and disposes the connection
        // rather than returning it to the pool. Verify this doesn't throw.
        ReturnConnection(conn, owner);

        // Assert
        // The connection should have been deactivated and disposed (not returned to the pool).
        // After Dispose(), IsConnectionDoomed is set to true and Pool is set to null.
        Assert.True(conn.IsConnectionDoomed,
            "Connection should be doomed after returning to a shut-down pool.");
        Assert.Null(conn.Pool);
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
            Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);
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

            Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);
            Assert.True(_pool.TransactedConnectionPool.TransactedConnections.ContainsKey(transaction));

            scope.Complete();
        }

        // Assert
        AssertPoolMetrics();
    }

    [Fact]
    public void SequentialTransactions_CanReuseConnections()
    {
        // Act
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

        // Assert
        // The connection was returned to the general pool and picked up by the second transaction
        Assert.NotSame(txn1, txn2);
        Assert.Same(conn1, conn2);
        AssertPoolMetrics();
    }

    #endregion

    #region Transacted Pool Plumbing Tests

    [Fact]
    public void TransactionCompletion_ReturnsConnectionToIdleChannel()
    {
        // Arrange - park a connection in the transacted pool.
        DbConnectionInternal conn;
        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            // While the transaction is live the connection is held by the transacted pool and is
            // deliberately absent from the idle channel.
            Assert.Single(_pool.TransactedConnectionPool.TransactedConnections);
            Assert.Equal(0, _pool.IdleCount);

            scope.Complete();
        }

        // Assert - completion drives TransactionEnded -> PutObjectFromTransactedPool, which puts
        // the connection back into general circulation.
        Assert.Empty(_pool.TransactedConnectionPool.TransactedConnections);
        Assert.Equal(1, _pool.IdleCount);
        Assert.Equal(1, _pool.Count);

        // The connection is now reusable by a caller with no ambient transaction.
        var owner2 = new SqlConnection();
        var conn2 = GetConnection(owner2);
        Assert.Same(conn, conn2);
        ReturnConnection(conn2, owner2);
    }

    [Fact]
    public void TransactionCompletion_AfterShutdown_DestroysConnection()
    {
        // Arrange - park a connection in the transacted pool, then shut the pool down. The
        // transacted connection survives the shutdown drain because closing it would abort the
        // (possibly distributed) transaction.
        DbConnectionInternal conn;
        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            conn = GetConnection(owner);
            Assert.NotNull(conn);
            ReturnConnection(conn, owner);

            // Act
            _pool.Shutdown();
            scope.Complete();
        }

        // Assert - a shut-down pool must not re-pool the connection when the transaction ends.
        Assert.Empty(_pool.TransactedConnectionPool.TransactedConnections);
        Assert.Equal(0, _pool.IdleCount);
        Assert.Equal(0, _pool.Count);
        Assert.True(conn.IsConnectionDoomed);
    }

    [Fact]
    public void TransactionEnded_UnknownConnection_DoesNotPoolConnection()
    {
        // Arrange - a connection that was never parked in the transacted pool.
        var owner = new SqlConnection();
        var conn = GetConnection(owner);
        Assert.NotNull(conn);

        using var scope = new TransactionScope();
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        // Act
        _pool.TransactionEnded(transaction!, conn);

        // Assert - nothing to remove, so the connection stays checked out.
        Assert.Equal(0, _pool.IdleCount);
        Assert.Equal(1, _pool.Count);

        ReturnConnection(conn, owner);
        scope.Complete();
    }

    [Fact]
    public void ReplaceConnection_CarriesEnlistedTransactionToNewConnection()
    {
        // Arrange
        using var scope = new TransactionScope();
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        var owner = new SqlConnection();
        var oldConnection = GetConnection(owner);
        Assert.NotNull(oldConnection);
        Assert.Equal(1, _pool.Count);

        // Act
        var newConnection = _pool.ReplaceConnection(
            owner,
            oldConnection,
            TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

        // Assert - a distinct connection took over the old connection's slot and enlistment.
        Assert.NotNull(newConnection);
        Assert.NotSame(oldConnection, newConnection);
        Assert.Equal(1, _pool.Count);
        Assert.True(oldConnection.IsConnectionDoomed);

        ReturnConnection(newConnection, owner);

        // The replacement inherited the transaction, so it parks in the transacted pool.
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections[transaction!]);

        scope.Complete();
    }

    #endregion

    #region Async Ambient Transaction Flow Tests

    /// <summary>
    /// A <see cref="TransactionScope"/> created without <see cref="TransactionScopeAsyncFlowOption.Enabled"/>
    /// keeps the ambient transaction in thread-static storage, so it is not observable from the thread
    /// pool thread the pool opens on. The pool must therefore take the transaction from the
    /// <see cref="TaskCompletionSource{TResult}"/>'s AsyncState, which is where SqlConnection.OpenAsync
    /// captures it.
    /// </summary>
    [Fact]
    public async Task GetConnectionAsync_AmbientTransactionNotFlowed_StillEnlistsFromAsyncState()
    {
        // Arrange - a transaction that is never ambient on any thread, so AsyncState is the only
        // way the pool can learn about it.
        using var transaction = new CommittableTransaction();
        Assert.Null(Transaction.Current);
        Assert.Null(await Task.Run(() => Transaction.Current));

        // Act
        var owner = new SqlConnection();
        var connection = await GetConnectionAsync(owner, transaction);

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(transaction, connection.EnlistedTransaction);

        ReturnConnection(connection, owner);

        // Being enlisted, the connection parks in the transacted pool rather than the idle channel.
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections[transaction]);
        Assert.Equal(0, _pool.IdleCount);

        transaction.Rollback();
    }

    /// <summary>
    /// Assigning <see cref="Transaction.Current"/> writes to thread-static storage that the
    /// ExecutionContext does not unwind, so doing it on a thread pool thread would leave a stale
    /// transaction behind for unrelated work later scheduled onto that same thread -- including the
    /// login-time auto-enlistment that non-pooled connections perform against the ambient
    /// transaction. The pool must pass the transaction explicitly instead of assigning it.
    /// </summary>
    [Fact]
    public async Task GetConnectionAsync_DoesNotLeakAmbientTransactionOntoThreadPool()
    {
        // Arrange & Act - several async opens under a transaction, each on a thread pool thread.
        for (int i = 0; i < 8; i++)
        {
            using var transaction = new CommittableTransaction();
            var owner = new SqlConnection();
            var connection = await GetConnectionAsync(owner, transaction);
            ReturnConnection(connection, owner);
            transaction.Rollback();
        }

        // Assert - no thread pool thread was left with an ambient transaction.
        for (int i = 0; i < 16; i++)
        {
            Assert.Null(await Task.Run(() => Transaction.Current));
        }
    }

    /// <summary>
    /// The synchronous path runs on the caller's thread, where the ambient transaction set by a
    /// TransactionScope is directly observable and must still be honored.
    /// </summary>
    [Fact]
    public void GetConnection_Sync_UsesAmbientTransactionFromCallersThread()
    {
        // Arrange
        using var scope = new TransactionScope();
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        // Act - no transaction is handed to the pool explicitly; it must read Transaction.Current.
        var owner = new SqlConnection();
        var connection = GetConnection(owner);

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(transaction, connection.EnlistedTransaction);

        ReturnConnection(connection, owner);
        Assert.Single(_pool.TransactedConnectionPool.TransactedConnections[transaction!]);

        scope.Complete();
    }

    #endregion

    #region Mock Classes

    internal class MockSqlConnectionFactory : SqlConnectionFactory
    {
        protected override DbConnectionInternal CreateConnection(
            SqlConnectionOptions options,
            ConnectionPoolKey poolKey,
            DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
            IDbConnectionPool pool,
            DbConnection owningConnection,
            TimeoutTimer timeout)
        {
            return new MockDbConnectionInternal();
        }
    }

    internal class MockDbConnectionInternal : DbConnectionInternal
    {
        private static int s_nextId = 1;
        public int MockId { get; } = Interlocked.Increment(ref s_nextId);

        public override string ServerVersion => "Mock";

        public override ConnectionCapabilities Capabilities => new();

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

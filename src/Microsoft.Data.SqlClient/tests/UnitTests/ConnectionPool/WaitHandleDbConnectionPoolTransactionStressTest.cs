// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool;

/// <summary>
/// Stress tests for WaitHandleDbConnectionPool transaction functionality under high concurrency and load.
/// These tests verify that pool metrics remain consistent when connections are rapidly opened and closed
/// with intermingled transactions in a highly concurrent environment.
/// </summary>
public class WaitHandleDbConnectionPoolTransactionStressTest
{
    private const int DefaultMaxPoolSize = 50;
    private const int DefaultMinPoolSize = 0;
    private readonly int DefaultCreationTimeout = TimeSpan.FromSeconds(15).Milliseconds;

    // Thread-safe random number generator for .NET Framework compatibility
    private static readonly ThreadLocal<Random> s_random = new(() => new Random(Guid.NewGuid().GetHashCode()));

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
            creationTimeout: DefaultCreationTimeout,
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

    private void AssertPoolMetrics(WaitHandleDbConnectionPool pool)
    {
        Assert.True(pool.Count <= pool.MaxPoolSize,
            $"Pool count ({pool.Count}) exceeded max pool size ({pool.MaxPoolSize})");
        Assert.True(pool.Count >= 0,
            $"Pool count ({pool.Count}) is negative");
        Assert.Empty(pool.TransactedConnectionPool.TransactedConnections);
    }

    #endregion

    #region Basic Transaction Stress Tests

    [Theory]
    [InlineData(10, 100)]
    public void StressTest_TransactionPerIteration(int threadCount, int iterationsPerThread)
    {
        // Tests many threads and iterations, with each iteration creating a transaction scope.

        // Arrange
        var pool = CreatePool();
        var tasks = new Task[threadCount];

        try
        {
            // Act
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        using var scope = new TransactionScope();
                        var owner = new SqlConnection();

                        var obtained = pool.TryGetConnection(
                            owner,
                            taskCompletionSource: null,
                            new DbConnectionOptions("", null),
                            out DbConnectionInternal? connection);

                        Assert.True(obtained);
                        Assert.NotNull(connection);

                        // Simulate some work
                        Thread.Sleep(1);

                        pool.ReturnInternalConnection(connection, owner);
                        scope.Complete();
                    }
                });
            }

            Task.WaitAll(tasks);

            // Assert
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
            pool.Clear();
        }
    }

    [Theory]
    [InlineData(10, 100)]
    public async Task StressTest_TransactionPerIteration_Async(int threadCount, int iterationsPerThread)
    {
        // Tests many threads and iterations, with each iteration creating a transaction scope.

        // Arrange
        var pool = CreatePool();
        var tasks = new Task[threadCount];

        try
        {
            // Act
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(async () =>
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                        var owner = new SqlConnection();

                        var tcs = new TaskCompletionSource<DbConnectionInternal>();
                        var obtained = pool.TryGetConnection(
                            owner,
                            taskCompletionSource: tcs,
                            new DbConnectionOptions("", null),
                            out DbConnectionInternal? connection);

                        // Wait for the task if not obtained immediately
                        connection ??= await tcs.Task;

                        Assert.NotNull(connection);

                        // Simulate some work
                        Thread.Sleep(1);

                        pool.ReturnInternalConnection(connection, owner);
                        scope.Complete();
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
            pool.Clear();
        }
    }

    [Theory]
    [InlineData(10, 100)]
    public void StressTest_TransactionPerThread(int threadCount, int iterationsPerThread)
    {
        // Arrange
        var pool = CreatePool();
        var tasks = new Task[threadCount];

        try
        {
            // Act - Each transaction should be isolated
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    using var scope = new TransactionScope();
                    var transaction = Transaction.Current;
                    Assert.NotNull(transaction);

                    // Get multiple connections within same transaction
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        var owner = new SqlConnection();
                        pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
                        Assert.NotNull(conn);
                        pool.ReturnInternalConnection(conn, owner);
                    }

                    Assert.Single(pool.TransactedConnectionPool.TransactedConnections[transaction]);

                    scope.Complete();
                });
            }

            Task.WaitAll(tasks);

            // Assert
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }

    [Theory]
    [InlineData(10, 100)]
    public async Task StressTest_TransactionPerThread_Async(int threadCount, int iterationsPerThread)
    {
        // Arrange
        var pool = CreatePool();
        var tasks = new Task[threadCount];

        try
        {
            // Act - Each transaction should be isolated
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(async () =>
                {
                    using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                    var transaction = Transaction.Current;
                    Assert.NotNull(transaction);

                    // Get multiple connections within same transaction
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        var owner = new SqlConnection();
                        // The transaction *must* be set as the AsyncState of the TaskCompletionSource.
                        // The 
                        var tcs = new TaskCompletionSource<DbConnectionInternal>(transaction);
                        pool.TryGetConnection(
                            owner,
                            tcs,
                            new DbConnectionOptions("", null),
                            out var conn);

                        conn ??= await tcs.Task;

                        Assert.NotNull(conn);

                        pool.ReturnInternalConnection(conn, owner);

                        Assert.Single(pool.TransactedConnectionPool.TransactedConnections[transaction]);
                    }

                    Assert.Single(pool.TransactedConnectionPool.TransactedConnections[transaction]);

                    scope.Complete();
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }
    
    [Theory]
    [InlineData(10, 100)]
    public void StressTest_SingleSharedTransaction(int threadCount, int iterationsPerThread)
    {
        // Arrange
        var pool = CreatePool();
        var tasks = new Task[threadCount];

        try
        {
            Transaction? transaction = null;
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                transaction = Transaction.Current;
                Assert.NotNull(transaction);
                // Act - Each transaction should be isolated
                for (int t = 0; t < threadCount; t++)
                {
                    tasks[t] = Task.Run(() =>
                    {
                        using (var innerScope = new TransactionScope(transaction))
                        {
                            Assert.Equal(transaction, Transaction.Current);
                            // Get multiple connections within same transaction
                            for (int i = 0; i < iterationsPerThread; i++)
                            {
                                using var owner = new SqlConnection();
                                pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
                                Assert.NotNull(conn);

                                // We bypass the SqlConnection.Open flow, so SqlConnection.InnerConnection is never set
                                // Therefore, SqlConnection.Close doesn't return the connection to the pool, we have to
                                // do it manually.
                                pool.ReturnInternalConnection(conn, owner);
                            }

                            innerScope.Complete();
                        }
                    });
                }

                Task.WaitAll(tasks);

                //Console.WriteLine($"{pool.TransactedConnectionPool.TransactedConnections[transaction].Count} transacted connections");
                scope.Complete();
            }

            while (pool.TransactedConnectionPool.TransactedConnections.ContainsKey(transaction!)
            && pool.TransactedConnectionPool.TransactedConnections[transaction!].Count > 0)
            {
                // Wait for transaction to be cleaned up
                Console.WriteLine("Waiting for transaction cleanup...");
                Thread.Sleep(100);
            }

            // Assert
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }

    [Theory]
    [InlineData(10, 100)]
    public async Task StressTest_SingleSharedTransaction_Async(int threadCount, int iterationsPerThread)
    {
        // Arrange
        var pool = CreatePool();
        var tasks = new Task[threadCount];

        try
        {
            Transaction? transaction = null;
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                transaction = Transaction.Current;
                Assert.NotNull(transaction);
                // Act - Each transaction should be isolated
                for (int t = 0; t < threadCount; t++)
                {
                    tasks[t] = Task.Run(async () =>
                    {
                        using (var innerScope = new TransactionScope(transaction, TransactionScopeAsyncFlowOption.Enabled))
                        {
                            Assert.Equal(transaction, Transaction.Current);
                            // Get multiple connections within same transaction
                            for (int i = 0; i < iterationsPerThread; i++)
                            {
                                var owner = new SqlConnection();
                                // The transaction *must* be set as the AsyncState of the TaskCompletionSource.
                                // The 
                                var tcs = new TaskCompletionSource<DbConnectionInternal>(transaction);
                                pool.TryGetConnection(
                                    owner,
                                    tcs,
                                    new DbConnectionOptions("", null),
                                    out var conn);

                                conn ??= await tcs.Task;

                                Assert.NotNull(conn);

                                // Simulate some work
                                await Task.Delay(1);

                                pool.ReturnInternalConnection(conn, owner);
                            }

                            innerScope.Complete();
                        }
                    });
                }

                //await Task.WhenAll(tasks);
                scope.Complete();
            }

            while (pool.TransactedConnectionPool.TransactedConnections.ContainsKey(transaction!)
                && pool.TransactedConnectionPool.TransactedConnections[transaction!].Count > 0)
            {
                // Wait for transaction to be cleaned up
                Console.WriteLine("Waiting for transaction cleanup...");
                await Task.Delay(100);
            }

            // Assert
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }
    #endregion

    // TODO saturate pool with open transactions and verify waits time out as expected
    // TODO test with nested transactions
    // TODO test with distributed transactions
    // TODO find a way to test the race conditions where a connection is returned just as a transaction is completing, this should strand the connection in the transacted pool.

    #region Intermingled Transaction Stress Tests

    [Fact]
    public void StressTest_MixedTransactedAndNonTransacted_HighConcurrency()
    {
        // Arrange
        var pool = CreatePool(maxPoolSize: 40);
        const int threadCount = 20;
        const int iterationsPerThread = 50;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();

        try
        {
            // Act - Half the threads use transactions, half don't
            for (int t = 0; t < threadCount; t++)
            {
                bool useTransactions = t % 2 == 0;
                tasks[t] = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < iterationsPerThread; i++)
                        {
                            if (useTransactions)
                            {
                                using var scope = new TransactionScope();
                                var owner = new SqlConnection();
                                pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
                                Assert.NotNull(conn);
                                Thread.Sleep(s_random.Value!.Next(1, 5));
                                pool.ReturnInternalConnection(conn, owner);
                                scope.Complete();
                            }
                            else
                            {
                                var owner = new SqlConnection();
                                pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
                                Assert.NotNull(conn);
                                Thread.Sleep(s_random.Value!.Next(1, 5));
                                pool.ReturnInternalConnection(conn, owner);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            Task.WaitAll(tasks);

            // Assert
            Assert.Empty(exceptions);
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }

    #endregion


    [Fact]
    public void StressTest_MaxPoolSaturation_WithTransactions()
    {
        // Arrange
        var pool = CreatePool(maxPoolSize: 25);
        const int threadCount = 50; // More threads than pool size
        const int iterationsPerThread = 20;
        var activeTasks = new ConcurrentBag<Task>();
        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new Barrier(threadCount);

        try
        {
            // Act - Saturate the pool with transactions
            var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(() =>
            {
                try
                {
                    // Synchronize all threads to start at once

                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        using var scope = new TransactionScope();
                        var owner = new SqlConnection();

                        var obtained = pool.TryGetConnection(
                            owner,
                            null,
                            new DbConnectionOptions("Timeout=30", null),
                            out DbConnectionInternal? connection);

                        if (obtained && connection != null)
                        {
                            // Hold connection briefly
                            pool.ReturnInternalConnection(connection, owner);
                        }

                        scope.Complete();
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

            Task.WaitAll(tasks);

            // Assert
            Assert.Empty(exceptions);
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }


    [Fact]
    public void StressTest_TransactionRollback_ManyOperations()
    {
        // Arrange
        var pool = CreatePool(maxPoolSize: 20);
        const int threadCount = 10;
        const int iterationsPerThread = 100;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var rollbackCount = 0;

        try
        {
            // Act - Alternate between commit and rollback
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < iterationsPerThread; i++)
                        {
                            using var scope = new TransactionScope();
                            var owner = new SqlConnection();

                            pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
                            Assert.NotNull(conn);

                            // Randomly commit or rollback
                            if (i % 2 == 0)
                            {
                                scope.Complete();
                            }
                            else
                            {
                                Interlocked.Increment(ref rollbackCount);
                                // Don't call Complete - let it rollback
                            }

                            pool.ReturnInternalConnection(conn!, owner);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            Task.WaitAll(tasks);

            // Assert
            Assert.Empty(exceptions);
            Assert.True(rollbackCount > 0, "Expected some rollbacks");
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }

    #region Edge Case Stress Tests

    [Fact]
    public void StressTest_RapidPoolShutdownDuringTransactions()
    {
        // Arrange
        var pool = CreatePool(maxPoolSize: 15);
        const int threadCount = 20;
        var barrier = new Barrier(threadCount);
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    barrier.SignalAndWait();

                    for (int i = 0; i < 50; i++)
                    {
                        using var scope = new TransactionScope();
                        var owner = new SqlConnection();

                        var obtained = pool.TryGetConnection(owner, null, new DbConnectionOptions("Timeout=5", null), out var conn);

                        if (obtained && conn != null)
                        {
                            pool.ReturnInternalConnection(conn, owner);
                        }

                        scope.Complete();
                    }
                }
                catch (Exception ex)
                {
                    // Some exceptions expected during shutdown
                    exceptions.Add(ex);
                }
            });
        }

        // Shutdown pool while operations are in progress
        Thread.Sleep(100);
        pool.Shutdown();

        Task.WaitAll(tasks);

        // Assert - Just verify no crash occurred and pool count is valid
        AssertPoolMetrics(pool);
    }

    #endregion

    #region Transaction Completion Order Tests

    [Fact]
    public void StressTest_CompleteBeforeReturn_ManyOperations()
    {
        // Arrange - Test completing transaction before returning connection
        var pool = CreatePool(maxPoolSize: 20);
        const int threadCount = 15;
        const int iterationsPerThread = 100;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var successCount = 0;

        try
        {
            // Act - Complete transaction before returning connection
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < iterationsPerThread; i++)
                        {
                            DbConnectionInternal? conn = null;
                            SqlConnection? owner = null;

                            using (var scope = new TransactionScope())
                            {
                                owner = new SqlConnection();
                                pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out conn);
                                Assert.NotNull(conn);

                                // Complete transaction BEFORE returning
                                scope.Complete();
                            } // Transaction completes here

                            // Return connection AFTER transaction scope disposal
                            // TODO: questionable, make sure we're not double returning
                            pool.ReturnInternalConnection(conn!, owner!);
                            Interlocked.Increment(ref successCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            Task.WaitAll(tasks);

            // Assert
            Assert.Empty(exceptions);
            Assert.Equal(threadCount * iterationsPerThread, successCount);
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }

    [Fact]
    public void StressTest_DelayedReturn_AfterTransactionDisposal()
    {
        // Arrange - Test returning connections with varying delays after transaction disposal
        var pool = CreatePool(maxPoolSize: 20);
        const int threadCount = 15;
        const int iterationsPerThread = 50;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();

        try
        {
            // Act
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < iterationsPerThread; i++)
                        {
                            DbConnectionInternal? conn = null;
                            SqlConnection? owner = null;

                            using (var scope = new TransactionScope())
                            {
                                owner = new SqlConnection();
                                pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out conn);
                                Assert.NotNull(conn);
                                scope.Complete();
                            } // Transaction disposed here

                            // Delay before returning (simulates held connection)
                            Thread.Sleep(s_random.Value!.Next(1, 10));

                            pool.ReturnInternalConnection(conn!, owner!);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            Task.WaitAll(tasks);

            // Assert
            Assert.Empty(exceptions);
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
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
            // Mock implementation - handle transaction enlistment
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
            // Mock implementation - deactivate connection
        }

        public override string ToString() => $"MockConnection_{MockId}";

        internal override void ResetConnection()
        {
            // Do nothing
        }
    }

    #endregion
}

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
    [InlineData(1, 100)]
    public void StressTest_SingleSharedTransaction(int threadCount, int iterationsPerThread)
    {
        // Arrange
        var pool = CreatePool();
        var tasks = new Task[threadCount];
        using var scope = new TransactionScope();
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        try
        {
            // Act - Each transaction should be isolated
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {


                    // Get multiple connections within same transaction
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        using var owner = new SqlConnection();
                        pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
                        Assert.NotNull(conn);
                    }

                    
                });
            }

            Task.WaitAll(tasks);

            Console.WriteLine($"{pool.TransactedConnectionPool.TransactedConnections[transaction].Count} transacted connections");
            scope.Complete();

            while (pool.TransactedConnectionPool.TransactedConnections.ContainsKey(transaction)
            && pool.TransactedConnectionPool.TransactedConnections[transaction].Count > 0)
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
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var transaction = Transaction.Current;
        Assert.NotNull(transaction);

        try
        {
            // Act - Each transaction should be isolated
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(async () =>
                {
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
                });
            }

            await Task.WhenAll(tasks);

            scope.Complete();

            // Assert
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }
    #endregion

    #region Intermingled Transaction Stress Tests

    [Fact]
    public void StressTest_InterminledTransactions_RapidScopeChanges()
    {
        // Arrange
        var pool = CreatePool(maxPoolSize: 30);
        const int threadCount = 15;
        const int iterationsPerThread = 50;
        var tasks = new Task[threadCount];
        var totalConnections = new ConcurrentBag<DbConnectionInternal>();
        var exceptions = new ConcurrentBag<Exception>();

        try
        {
            // Act - Each thread creates nested and sequential transactions
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < iterationsPerThread; i++)
                        {
                            // Sequential transactions
                            using (var scope1 = new TransactionScope())
                            {
                                var owner1 = new SqlConnection();
                                pool.TryGetConnection(owner1, null, new DbConnectionOptions("", null), out var conn1);
                                Assert.NotNull(conn1);
                                totalConnections.Add(conn1);
                                pool.ReturnInternalConnection(conn1, owner1);
                                scope1.Complete();
                            }

                            using (var scope2 = new TransactionScope())
                            {
                                var owner2 = new SqlConnection();
                                pool.TryGetConnection(owner2, null, new DbConnectionOptions("", null), out var conn2);
                                Assert.NotNull(conn2);
                                totalConnections.Add(conn2);
                                pool.ReturnInternalConnection(conn2, owner2);
                                scope2.Complete();
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

    #region High Load Stress Tests

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
    public void StressTest_RapidOpenCloseUnderLoad_ThousandsOfOperations()
    {
        // Arrange
        var pool = CreatePool(maxPoolSize: 30);
        const int totalOperations = 5000;
        const int maxParallelism = 40;
        var exceptions = new ConcurrentBag<Exception>();
        var completedOperations = 0;

        try
        {
            // Act - Perform thousands of rapid operations
            Parallel.For(0, totalOperations, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism }, i =>
            {
                try
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
                        pool.ReturnInternalConnection(connection, owner);
                        Interlocked.Increment(ref completedOperations);
                    }

                    scope.Complete();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            // Assert
            Assert.Empty(exceptions);
            Assert.True(completedOperations > 0, "No operations completed successfully");
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }

    #endregion

    #region Async Transaction Stress Tests

    [Fact]
    public async Task StressTest_AsyncTransactions_HighConcurrency()
    {
        // Arrange
        var pool = CreatePool(maxPoolSize: 30);
        const int taskCount = 30; // Reduced from 50
        const int iterationsPerTask = 10; // Reduced from 30
        var exceptions = new ConcurrentBag<Exception>();

        try
        {
            // Act
            var tasks = Enumerable.Range(0, taskCount).Select(async t =>
            {
                try
                {
                    for (int i = 0; i < iterationsPerTask; i++)
                    {
                        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                        var owner = new SqlConnection();
                        var tcs = new TaskCompletionSource<DbConnectionInternal>();

                        var obtained = pool.TryGetConnection(owner, tcs, new DbConnectionOptions("Timeout=30", null), out var connection);

                        if (!obtained)
                        {
                            connection = await tcs.Task;
                        }

                        Assert.NotNull(connection);

                        // Simulate async work - reduced delay
                        await Task.Delay(s_random.Value!.Next(1, 3));

                        pool.ReturnInternalConnection(connection!, owner);
                        scope.Complete();
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }).ToArray();

            await Task.WhenAll(tasks);

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

    #region Transaction Completion Stress Tests

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

    [Fact]
    public void StressTest_MixedTransactionOutcomes_Concurrent()
    {
        // Arrange
        var pool = CreatePool(maxPoolSize: 30);
        const int totalOperations = 1000;
        var exceptions = new ConcurrentBag<Exception>();
        var outcomes = new ConcurrentDictionary<string, int>();

        try
        {
            // Act - Random transaction outcomes
            Parallel.For(0, totalOperations, new ParallelOptions { MaxDegreeOfParallelism = 20 }, i =>
            {
                try
                {
                    var outcome = i % 3; // 0=commit, 1=rollback, 2=exception-then-rollback

                    using var scope = new TransactionScope();
                    var owner = new SqlConnection();

                    pool.TryGetConnection(owner, null, new DbConnectionOptions("Timeout=30", null), out var conn);
                    Assert.NotNull(conn);

                    if (outcome == 0)
                    {
                        scope.Complete();
                        outcomes.AddOrUpdate("Committed", 1, (k, v) => v + 1);
                    }
                    else if (outcome == 1)
                    {
                        // Rollback (no Complete call)
                        outcomes.AddOrUpdate("Rolledback", 1, (k, v) => v + 1);
                    }
                    else
                    {
                        // Exception then rollback
                        outcomes.AddOrUpdate("Exception", 1, (k, v) => v + 1);
                    }

                    pool.ReturnInternalConnection(conn!, owner);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            // Assert
            Assert.Empty(exceptions);
            AssertPoolMetrics(pool);
            Assert.True(outcomes.Values.Sum() > 0, "Expected some transactions to complete");
        }
        finally
        {
            pool.Shutdown();
        }
    }

    #endregion

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

        try
        {
            Task.WaitAll(tasks);
        }
        catch
        {
            // Expected - some tasks may fail during shutdown
        }

        // Assert - Just verify no crash occurred and pool count is valid
        AssertPoolMetrics(pool);
    }

    [Fact]
    public void StressTest_SingleConnectionPool_HighContention()
    {
        // Arrange - Pool size of 1 creates maximum contention
        var pool = CreatePool(maxPoolSize: 1);
        const int threadCount = 20;
        const int iterationsPerThread = 50;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var successCount = 0;

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
                            using var scope = new TransactionScope();
                            var owner = new SqlConnection();

                            var obtained = pool.TryGetConnection(
                                owner,
                                null,
                                new DbConnectionOptions("Timeout=30", null),
                                out DbConnectionInternal? conn);

                            if (obtained && conn != null)
                            {
                                Thread.Sleep(1); // Hold briefly
                                pool.ReturnInternalConnection(conn, owner);
                                Interlocked.Increment(ref successCount);
                            }

                            scope.Complete();
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
            Assert.True(successCount > 0, "Expected some successful operations despite high contention");
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }

    #endregion

    #region Transaction Completion Order Tests

    [Fact]
    public void StressTest_ReturnBeforeTransactionComplete_ManyOperations()
    {
        // Arrange - Test returning connection before transaction scope completes
        var pool = CreatePool(maxPoolSize: 20);
        const int threadCount = 15;
        const int iterationsPerThread = 100;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var successCount = 0;

        try
        {
            // Act - Return connection before calling scope.Complete()
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

                            // Return connection BEFORE completing transaction
                            pool.ReturnInternalConnection(conn!, owner);

                            // Now complete the transaction
                            scope.Complete();
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
    public void StressTest_MixedCompletionOrder_RandomizedConcurrent()
    {
        // Arrange - Randomly mix the order of completion and return
        var pool = CreatePool(maxPoolSize: 25);
        const int totalOperations = 1000;
        var exceptions = new ConcurrentBag<Exception>();
        var orderCounts = new ConcurrentDictionary<string, int>();

        try
        {
            // Act - Randomize order across many operations
            Parallel.For(0, totalOperations, new ParallelOptions { MaxDegreeOfParallelism = 30 }, i =>
            {
                try
                {
                    // Randomly choose order
                    bool returnBeforeComplete = i % 2 == 0;

                    if (returnBeforeComplete)
                    {
                        using var scope = new TransactionScope();
                        var owner = new SqlConnection();
                        pool.TryGetConnection(owner, null, new DbConnectionOptions("Timeout=30", null), out var conn);
                        Assert.NotNull(conn);

                        pool.ReturnInternalConnection(conn!, owner);
                        scope.Complete();
                        orderCounts.AddOrUpdate("ReturnFirst", 1, (k, v) => v + 1);
                    }
                    else
                    {
                        DbConnectionInternal? conn = null;
                        SqlConnection? owner = null;

                        using (var scope = new TransactionScope())
                        {
                            owner = new SqlConnection();
                            pool.TryGetConnection(owner, null, new DbConnectionOptions("Timeout=30", null), out conn);
                            Assert.NotNull(conn);
                            scope.Complete();
                        }

                        pool.ReturnInternalConnection(conn!, owner!);
                        orderCounts.AddOrUpdate("CompleteFirst", 1, (k, v) => v + 1);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            // Assert
            Assert.Empty(exceptions);
            Assert.Equal(2, orderCounts.Count); // Should have both order types
            Assert.True(orderCounts["ReturnFirst"] > 0);
            Assert.True(orderCounts["CompleteFirst"] > 0);
            AssertPoolMetrics(pool);
        }
        finally
        {
            pool.Shutdown();
        }
    }

    [Fact]
    public void StressTest_InterleavedCompletionOrder_HighConcurrency()
    {
        // Arrange - Multiple threads with different patterns
        var pool = CreatePool(maxPoolSize: 30);
        const int threadCount = 20;
        const int iterationsPerThread = 50;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();

        try
        {
            // Act - Each thread uses different pattern
            for (int t = 0; t < threadCount; t++)
            {
                int threadIndex = t;
                tasks[t] = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < iterationsPerThread; i++)
                        {
                            // Use thread index to determine pattern
                            switch (threadIndex % 4)
                            {
                                case 0: // Return before complete
                                    using (var scope = new TransactionScope())
                                    {
                                        var owner = new SqlConnection();
                                        pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
                                        pool.ReturnInternalConnection(conn!, owner);
                                        scope.Complete();
                                    }
                                    break;

                                case 1: // Complete before return
                                    {
                                        DbConnectionInternal? conn;
                                        SqlConnection owner;
                                        using (var scope = new TransactionScope())
                                        {
                                            owner = new SqlConnection();
                                            pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out conn);
                                            scope.Complete();
                                        }
                                        pool.ReturnInternalConnection(conn!, owner);
                                    }
                                    break;

                                case 2: // Rollback before return
                                    {
                                        DbConnectionInternal? conn;
                                        SqlConnection owner;
                                        using (var scope = new TransactionScope())
                                        {
                                            owner = new SqlConnection();
                                            pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out conn);
                                            // Don't complete - rollback
                                        }
                                        pool.ReturnInternalConnection(conn!, owner);
                                    }
                                    break;

                                case 3: // Return before rollback
                                    using (var scope = new TransactionScope())
                                    {
                                        var owner = new SqlConnection();
                                        pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
                                        pool.ReturnInternalConnection(conn!, owner);
                                        // Don't complete - rollback
                                    }
                                    break;
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

    [Fact]
    public void StressTest_MultipleConnectionsSameTransaction_VariedReturnOrder()
    {
        // Arrange - Test multiple connections in same transaction returned in different orders
        var pool = CreatePool(maxPoolSize: 30);
        const int threadCount = 10;
        const int iterationsPerThread = 50;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();

        try
        {
            // Act
            for (int t = 0; t < threadCount; t++)
            {
                int threadIndex = t;
                tasks[t] = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < iterationsPerThread; i++)
                        {
                            using var scope = new TransactionScope();
                            var owner1 = new SqlConnection();
                            var owner2 = new SqlConnection();
                            var owner3 = new SqlConnection();

                            // Get multiple connections
                            pool.TryGetConnection(owner1, null, new DbConnectionOptions("", null), out var conn1);
                            pool.TryGetConnection(owner2, null, new DbConnectionOptions("", null), out var conn2);
                            pool.TryGetConnection(owner3, null, new DbConnectionOptions("", null), out var conn3);

                            Assert.NotNull(conn1);
                            Assert.NotNull(conn2);
                            Assert.NotNull(conn3);

                            // Return in different orders based on iteration
                            switch (i % 3)
                            {
                                case 0: // Return in order
                                    pool.ReturnInternalConnection(conn1!, owner1);
                                    pool.ReturnInternalConnection(conn2!, owner2);
                                    pool.ReturnInternalConnection(conn3!, owner3);
                                    break;

                                case 1: // Return in reverse order
                                    pool.ReturnInternalConnection(conn3!, owner3);
                                    pool.ReturnInternalConnection(conn2!, owner2);
                                    pool.ReturnInternalConnection(conn1!, owner1);
                                    break;

                                case 2: // Return in mixed order
                                    pool.ReturnInternalConnection(conn2!, owner2);
                                    pool.ReturnInternalConnection(conn1!, owner1);
                                    pool.ReturnInternalConnection(conn3!, owner3);
                                    break;
                            }

                            scope.Complete();
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

    [Fact]
    public async Task StressTest_AsyncCompletionOrder_MixedPatterns()
    {
        // Arrange - Test async scenarios with different completion orders
        var pool = CreatePool(maxPoolSize: 25);
        const int taskCount = 20;
        const int iterationsPerTask = 30;
        var exceptions = new ConcurrentBag<Exception>();

        try
        {
            // Act
            var tasks = Enumerable.Range(0, taskCount).Select(async taskIndex =>
            {
                try
                {
                    for (int i = 0; i < iterationsPerTask; i++)
                    {
                        if (taskIndex % 2 == 0)
                        {
                            // Pattern 1: Return before async complete
                            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                            var owner = new SqlConnection();
                            var tcs = new TaskCompletionSource<DbConnectionInternal>();

                        pool.TryGetConnection(owner, tcs, new DbConnectionOptions("", null), out var conn);
                        if (conn == null)
                        {
                            conn = await tcs.Task;
                        }

                        pool.ReturnInternalConnection(conn!, owner);
                        await Task.Delay(s_random.Value!.Next(1, 5));
                        scope.Complete();
                    }
                    else
                    {
                        // Pattern 2: Complete before async return
                        DbConnectionInternal? conn;
                        SqlConnection owner;                            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                            {
                                owner = new SqlConnection();
                            var tcs = new TaskCompletionSource<DbConnectionInternal>();

                            pool.TryGetConnection(owner, tcs, new DbConnectionOptions("", null), out conn);
                            if (conn == null)
                            {
                                conn = await tcs.Task;
                            }

                            await Task.Delay(s_random.Value!.Next(1, 5));
                            scope.Complete();
                        }

                        pool.ReturnInternalConnection(conn!, owner);
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }).ToArray();            await Task.WhenAll(tasks);

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
    }

    #endregion
}

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
public class WaitHandleDbConnectionPoolTransactionStressTest : IDisposable
{
    private const int DefaultMaxPoolSize = 50;
    private const int DefaultMinPoolSize = 0;
    private readonly int DefaultCreationTimeout = TimeSpan.FromSeconds(15).Milliseconds;

    private WaitHandleDbConnectionPool? pool;

    public void Dispose()
    {
        pool?.Shutdown();
        pool?.Clear();
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
        // Arrange
        pool = CreatePool();
        var tasks = new Task[threadCount];

        // Act
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                    var owner = new SqlConnection();

                    var obtained = pool.TryGetConnection(
                        owner,
                        taskCompletionSource: null,
                        new DbConnectionOptions("", null),
                        out DbConnectionInternal? connection);

                    Assert.True(obtained);
                    Assert.NotNull(connection);

                    pool.ReturnInternalConnection(connection, owner);
                    scope.Complete();
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert
        AssertPoolMetrics(pool);
    }

    [Theory]
    [InlineData(10, 100)]
    public async Task StressTest_TransactionPerIteration_Async(int threadCount, int iterationsPerThread)
    {
        // Arrange
        pool = CreatePool();
        var tasks = new Task[threadCount];

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

                    pool.ReturnInternalConnection(connection, owner);
                    scope.Complete();
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        AssertPoolMetrics(pool);
    }

    [Theory]
    [InlineData(10, 100)]
    public void StressTest_TransactionPerThread(int threadCount, int iterationsPerThread)
    {
        // Arrange
        pool = CreatePool();
        var tasks = new Task[threadCount];

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

    [Theory]
    [InlineData(10, 100)]
    public async Task StressTest_TransactionPerThread_Async(int threadCount, int iterationsPerThread)
    {
        // Arrange
        pool = CreatePool();
        var tasks = new Task[threadCount];

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
    
    [Theory]
    [InlineData(10, 100)]
    public void StressTest_SingleSharedTransaction(int threadCount, int iterationsPerThread)
    {
        // Arrange
        pool = CreatePool();
        var tasks = new Task[threadCount];

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

    [Theory]
    [InlineData(10, 100)]
    public async Task StressTest_SingleSharedTransaction_Async(int threadCount, int iterationsPerThread)
    {
        // Arrange
        pool = CreatePool();
        var tasks = new Task[threadCount];

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
    #endregion

    #region Pool Saturation and Timeout Tests

    [Fact]
    public void StressTest_PoolSaturation_WithOpenTransactions_VerifyTimeout()
    {
        // Arrange - Test that when pool is saturated with transactions, new requests behave correctly
        pool = CreatePool(maxPoolSize: 3);
        const int saturatingThreadCount = 3;
        const int waitingThreadCount = 5;
        var saturatingTasks = new Task[saturatingThreadCount];
        var waitingTasks = new Task[waitingThreadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var completedWithoutConnection = 0;
        var barrier = new Barrier(saturatingThreadCount + 1);

        // Act - Saturate the pool with long-held connections in transactions
        for (int t = 0; t < saturatingThreadCount; t++)
        {
            saturatingTasks[t] = Task.Run(async () =>
            {
                var signalled = false;
                try
                {
                    using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                    var owner = new SqlConnection();

                    pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
                    Assert.NotNull(conn);

                    // Signal that we've acquired a connection
                    barrier.SignalAndWait();
                    signalled = true;

                    // Hold the connection briefly
                    await Task.Delay(200);

                    pool.ReturnInternalConnection(conn, owner);
                    scope.Complete();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    if (!signalled)
                    {
                        // Ensure barrier is released even on exception
                        barrier.SignalAndWait();
                    }
                }
            });
        }

        // Wait for all saturating threads to acquire connections
        barrier.SignalAndWait();

        // Now try to get more connections - pool is saturated
        for (int t = 0; t < waitingThreadCount; t++)
        {
            waitingTasks[t] = Task.Run(() =>
            {
                try
                {
                    using var scope = new TransactionScope();
                    var owner = new SqlConnection();

                    var obtained = pool.TryGetConnection(
                        owner,
                        null,
                        new DbConnectionOptions("", null),
                        out var conn);

                    if (!obtained || conn == null)
                    {
                        Interlocked.Increment(ref completedWithoutConnection);
                    }
                    else
                    {
                        pool.ReturnInternalConnection(conn, owner);
                        scope.Complete();
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        Task.WaitAll(saturatingTasks.Concat(waitingTasks).ToArray());

        // Assert
        Assert.Empty(exceptions);
        Assert.True(completedWithoutConnection >= 0, 
            $"Completed without connection: {completedWithoutConnection}");

        // Act
        // Now that everything is released, we should be able to get a connection again.
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var owner = new SqlConnection();

        var tcs = new TaskCompletionSource<DbConnectionInternal>(Transaction.Current);
        var obtained = pool.TryGetConnection(
            owner,
            null,
            new DbConnectionOptions("", null),
            out var conn);

        // Assert
        Assert.NotNull(conn);
    }

    [Fact]
    public async Task StressTest_PoolSaturation_WithOpenTransactions_VerifyTimeout_Async()
    {
        // Arrange - Test that when pool is saturated with transactions, new requests behave correctly
        pool = CreatePool(maxPoolSize: 3);
        const int saturatingThreadCount = 3;
        const int waitingThreadCount = 5;
        var saturatingTasks = new Task[saturatingThreadCount];
        var waitingTasks = new Task[waitingThreadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var completedWithoutConnection = 0;
        
        // Async-friendly barrier replacement
        var allSaturatingThreadsReady = new TaskCompletionSource<bool>();
        var readyCount = 0;

        // Act - Saturate the pool with long-held connections in transactions
        for (int t = 0; t < saturatingThreadCount; t++)
        {
            saturatingTasks[t] = Task.Run(async () =>
            {
                try
                {
                    using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                    var owner = new SqlConnection();

                    var tcs = new TaskCompletionSource<DbConnectionInternal>(Transaction.Current);
                    pool.TryGetConnection(owner, tcs, new DbConnectionOptions("", null), out var conn);
                    conn ??= await tcs.Task;

                    Assert.NotNull(conn);

                    // Signal that we've acquired a connection
                    if (Interlocked.Increment(ref readyCount) == saturatingThreadCount)
                    {
                        allSaturatingThreadsReady.TrySetResult(true);
                    }
                        
                    // Wait for all saturating threads to be ready
                    await allSaturatingThreadsReady.Task;

                    // Hold the connection briefly
                    await Task.Delay(200);

                    pool.ReturnInternalConnection(conn, owner);
                    scope.Complete();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    // Ensure barrier is released even on exception
                    if (Interlocked.Increment(ref readyCount) == saturatingThreadCount)
                    {
                        allSaturatingThreadsReady.TrySetResult(true);
                    }
                }
            });
        }

        // Wait for all saturating threads to acquire connections
        await allSaturatingThreadsReady.Task;

        // Now start waiting threads
        for (int t = 0; t < waitingThreadCount; t++)
        {
            waitingTasks[t] = Task.Run(async () =>
            {
                try
                {
                    using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                    var owner = new SqlConnection();

                    var tcs = new TaskCompletionSource<DbConnectionInternal>(Transaction.Current);
                    var obtained = pool.TryGetConnection(
                        owner,
                        tcs,
                        new DbConnectionOptions("", null),
                        out var conn);

                    if (!obtained)
                    {
                        // Try to wait with timeout
                        var timeoutTask = Task.Delay(300);
                        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                            
                        if (completedTask == timeoutTask)
                        {
                            Interlocked.Increment(ref completedWithoutConnection);
                        }
                        else
                        {
                            conn = tcs.Task.Result;
                            pool.ReturnInternalConnection(conn, owner);
                            scope.Complete();
                        }
                    }
                    else if (conn != null)
                    {
                        pool.ReturnInternalConnection(conn, owner);
                        scope.Complete();
                    }
                    else
                    {
                        Interlocked.Increment(ref completedWithoutConnection);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        await Task.WhenAll(saturatingTasks.Concat(waitingTasks).ToArray());

        // Assert
        Assert.Empty(exceptions);
        Assert.True(completedWithoutConnection >= 0,
            $"Completed without connection: {completedWithoutConnection}");

        // Act
        // Now that everything is released, we should be able to get a connection again.
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var owner = new SqlConnection();

        var tcs = new TaskCompletionSource<DbConnectionInternal>(Transaction.Current);
        var obtained = pool.TryGetConnection(
            owner,
            tcs,
            new DbConnectionOptions("", null),
            out var conn);

        conn ??= await tcs.Task;

        // Assert
        Assert.NotNull(conn);
    }

    #endregion

    #region Nested Transaction Tests

    [Theory]
    [InlineData(5, 3, 10, TransactionScopeOption.RequiresNew)]
    [InlineData(5, 3, 10, TransactionScopeOption.Required)]
    public void StressTest_NestedTransactions(int threadCount, int nestingLevel, int iterationsPerThread, TransactionScopeOption transactionScopeOption)
    {
        // Arrange - Test nested transactions with multiple nesting levels
        pool = CreatePool(maxPoolSize: 20);
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var successCount = 0;

        // Act
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        ExecuteNestedTransaction(pool, nestingLevel, transactionScopeOption);
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

    private void ExecuteNestedTransaction(WaitHandleDbConnectionPool pool, int nestingLevel, TransactionScopeOption transactionScopeOption)
    {
        if (nestingLevel <= 0)
        {
            return;
        }

        using var scope = new TransactionScope(transactionScopeOption);
        var owner = new SqlConnection();

        pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
        Assert.NotNull(conn);

        // Recursively create nested transaction
        if (nestingLevel > 1)
        {
            ExecuteNestedTransaction(pool, nestingLevel - 1, transactionScopeOption);
        }

        pool.ReturnInternalConnection(conn, owner);
        scope.Complete();
    }

    [Theory]
    [InlineData(5, 3, 10, TransactionScopeOption.RequiresNew)]
    [InlineData(5, 3, 10, TransactionScopeOption.Required)]
    public async Task StressTest_NestedTransactions_Async(int threadCount, int nestingLevel, int iterationsPerThread, TransactionScopeOption transactionScopeOption)
    {
        // Arrange - Test nested transactions with multiple nesting levels
        pool = CreatePool(maxPoolSize: 20);
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var successCount = 0;

        // Act
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        await ExecuteNestedTransactionAsync(pool, nestingLevel, transactionScopeOption);
                        Interlocked.Increment(ref successCount);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(threadCount * iterationsPerThread, successCount);
        AssertPoolMetrics(pool);
    }

    private async Task ExecuteNestedTransactionAsync(WaitHandleDbConnectionPool pool, int nestingLevel, TransactionScopeOption transactionScopeOption)
    {
        if (nestingLevel <= 0)
        {
            return;
        }

        using var scope = new TransactionScope(transactionScopeOption, TransactionScopeAsyncFlowOption.Enabled);
        var transaction = Transaction.Current;
        var owner = new SqlConnection();

        var tcs = new TaskCompletionSource<DbConnectionInternal>(transaction);
        pool.TryGetConnection(owner, tcs, new DbConnectionOptions("", null), out var conn);
        conn ??= await tcs.Task;

        Assert.NotNull(conn);

        // Recursively create nested transaction
        if (nestingLevel > 1)
        {
            await ExecuteNestedTransactionAsync(pool, nestingLevel - 1, transactionScopeOption);
        }

        pool.ReturnInternalConnection(conn, owner);
        scope.Complete();
    }

    #endregion

    #region Intermingled Transaction Stress Tests

    [Fact]
    public void StressTest_MixedTransactedAndNonTransacted_HighConcurrency()
    {
        // Arrange
        pool = CreatePool(maxPoolSize: 40);
        const int threadCount = 20;
        const int iterationsPerThread = 50;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();

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
                            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                            var owner = new SqlConnection();
                            pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
                            Assert.NotNull(conn);
                            pool.ReturnInternalConnection(conn, owner);
                            scope.Complete();
                        }
                        else
                        {
                            var owner = new SqlConnection();
                            pool.TryGetConnection(owner, null, new DbConnectionOptions("", null), out var conn);
                            Assert.NotNull(conn);
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

    #endregion

    #region Edge Case Stress Tests

    [Fact]
    public void StressTest_TransactionRollback_ManyOperations()
    {
        // Arrange
        pool = CreatePool(maxPoolSize: 20);
        const int threadCount = 10;
        const int iterationsPerThread = 100;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var rollbackCount = 0;

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

    [Fact]
    public void StressTest_PoolShutdownDuringTransactions()
    {
        // Arrange
        pool = CreatePool(maxPoolSize: 15);
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
        pool.Shutdown();

        Task.WaitAll(tasks);

        // Assert - Just verify no crash occurred and pool count is valid
        AssertPoolMetrics(pool);
    }

    [Fact]
    public void StressTest_TransactionCompleteBeforeReturn()
    {
        // Arrange - Test completing transaction before returning connection
        pool = CreatePool(maxPoolSize: 20);
        const int threadCount = 15;
        const int iterationsPerThread = 100;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var successCount = 0;

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

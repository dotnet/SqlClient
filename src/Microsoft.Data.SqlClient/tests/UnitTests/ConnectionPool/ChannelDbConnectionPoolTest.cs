// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Unit tests for <see cref="ChannelDbConnectionPool"/> covering connection acquisition,
    /// timeouts, reuse, pool clearing, blocking-period behavior, and timeout-budget propagation.
    /// </summary>
    public class ChannelDbConnectionPoolTest
    {
        private static readonly SqlConnectionFactory SuccessfulConnectionFactory = new SuccessfulSqlConnectionFactory();
        private static readonly SqlConnectionFactory TimeoutConnectionFactory = new TimeoutSqlConnectionFactory();

        /// <summary>
        /// Creates a <see cref="ChannelDbConnectionPool"/> with configurable test dependencies so
        /// individual tests can focus on the behavior under test without repeating setup logic.
        /// </summary>
        /// <param name="connectionFactory">The factory used to create physical connections.</param>
        /// <param name="identity">Optional pool identity override.</param>
        /// <param name="dbConnectionPoolGroup">Optional pool group override.</param>
        /// <param name="poolGroupOptions">Optional pool options override.</param>
        /// <param name="connectionPoolProviderInfo">Optional provider info override.</param>
        /// <returns>A configured <see cref="ChannelDbConnectionPool"/> instance for testing.</returns>
        private ChannelDbConnectionPool ConstructPool(SqlConnectionFactory connectionFactory,
            DbConnectionPoolIdentity? identity = null,
            DbConnectionPoolGroup? dbConnectionPoolGroup = null,
            DbConnectionPoolGroupOptions? poolGroupOptions = null,
            DbConnectionPoolProviderInfo? connectionPoolProviderInfo = null)
        {
            poolGroupOptions ??= new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 50,
                    creationTimeout: 15,
                    loadBalanceTimeout: 0,
                    hasTransactionAffinity: true
            );
            dbConnectionPoolGroup ??= new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions
            );
            return new ChannelDbConnectionPool(
                connectionFactory,
                dbConnectionPoolGroup,
                identity ?? DbConnectionPoolIdentity.NoIdentity,
                connectionPoolProviderInfo ?? new DbConnectionPoolProviderInfo()
            );
        }

        /// <summary>
        /// Verifies that requesting connections from an empty pool causes the pool to create new
        /// physical connections until the requested count is reached.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void GetConnectionEmptyPool_ShouldCreateNewConnection(int numConnections)
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act
            for (int i = 0; i < numConnections; i++)
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? internalConnection
                );

                // Assert
                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            // Assert
            Assert.Equal(numConnections, pool.Count);
        }

        /// <summary>
        /// Verifies that asynchronous requests against an empty pool create new physical
        /// connections and complete through the provided task completion source.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public async Task GetConnectionAsyncEmptyPool_ShouldCreateNewConnection(int numConnections)
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act
            for (int i = 0; i < numConnections; i++)
            {
                var tcs = new TaskCompletionSource<DbConnectionInternal>();
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    tcs,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? internalConnection
                );

                // Assert
                Assert.False(completed);
                Assert.Null(internalConnection);
                Assert.NotNull(await tcs.Task);
            }

            // Assert
            Assert.Equal(numConnections, pool.Count);
        }

        /// <summary>
        /// Verifies that a synchronous request against an exhausted pool fails with the pooled-open
        /// timeout once the caller's timeout budget has already expired.
        /// </summary>
        [Fact]
        public void GetConnectionMaxPoolSize_ShouldTimeoutAfterPeriod()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            for (int i = 0; i < pool.PoolGroupOptions.MaxPoolSize; i++)
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? internalConnection
                );

                // Assert
                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            // Build a timer backed by a fake time provider, then advance virtual time past
            // the timer's expiration so the pool's CancellationTokenSource is created
            // already-cancelled and the timeout path fires deterministically without any
            // wall-clock wait.
            var fakeTime = new FakeTimeProvider();
            TimeoutTimer expiredTimer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(1), fakeTime);
            fakeTime.Advance(TimeSpan.FromSeconds(2));

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    expiredTimer,
                    out DbConnectionInternal? extraConnection);
            });

            Assert.Equal(
                "Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.",
                ex.Message);
            Assert.Equal(pool.PoolGroupOptions.MaxPoolSize, pool.Count);
        }

        /// <summary>
        /// Verifies that an asynchronous request against an exhausted pool completes with the
        /// pooled-open timeout once the caller's timeout budget has already expired.
        /// </summary>
        [Fact]
        public async Task GetConnectionAsyncMaxPoolSize_ShouldTimeoutAfterPeriod()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            for (int i = 0; i < pool.PoolGroupOptions.MaxPoolSize; i++)
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? internalConnection
                );

                // Assert
                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            // Build a timer backed by a fake time provider then advance past expiration so
            // the pool's CTS is created already-cancelled.
            var fakeTime = new FakeTimeProvider();
            TimeoutTimer expiredTimer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(1), fakeTime);
            fakeTime.Advance(TimeSpan.FromSeconds(2));

            // Act & Assert
            TaskCompletionSource<DbConnectionInternal> taskCompletionSource = new();
            pool.TryGetConnection(
                new SqlConnection(),
                taskCompletionSource,
                expiredTimer,
                out _);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => taskCompletionSource.Task);

            Assert.Equal(
                "Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.",
                ex.Message);
            Assert.Equal(pool.PoolGroupOptions.MaxPoolSize, pool.Count);
        }

        /// <summary>
        /// Verifies that a waiting synchronous caller reuses a connection that is returned to an
        /// exhausted pool instead of creating a new physical connection.
        /// </summary>
        [Fact]
        public async Task GetConnectionMaxPoolSize_ShouldReuseAfterConnectionReleased()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            SqlConnection firstOwningConnection = new();

            pool.TryGetConnection(
                firstOwningConnection,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? firstConnection
            );

            for (int i = 1; i < pool.PoolGroupOptions.MaxPoolSize; i++)
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? internalConnection
                );

                // Assert
                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            // Act
            var task = Task.Run(() =>
            {
                pool.TryGetConnection(
                    new SqlConnection(""),
                    taskCompletionSource: null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? extraConnection
                );
                return extraConnection;
            });
            pool.ReturnInternalConnection(firstConnection!, firstOwningConnection);
            var extraConnection = await task;

            // Assert
            Assert.Equal(firstConnection, extraConnection);
        }

        /// <summary>
        /// Verifies that a waiting asynchronous caller reuses a connection that is returned to an
        /// exhausted pool instead of creating a new physical connection.
        /// </summary>
        [Fact]
        public async Task GetConnectionAsyncMaxPoolSize_ShouldReuseAfterConnectionReleased()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            SqlConnection firstOwningConnection = new();

            pool.TryGetConnection(
                firstOwningConnection,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? firstConnection
            );

            for (int i = 1; i < pool.PoolGroupOptions.MaxPoolSize; i++)
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? internalConnection
                );

                // Assert
                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            TaskCompletionSource<DbConnectionInternal> taskCompletionSource = new();

            // Act
            pool.TryGetConnection(
                new SqlConnection(""),
                taskCompletionSource,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? recycledConnection
            );
            pool.ReturnInternalConnection(firstConnection!, firstOwningConnection);
            recycledConnection = await taskCompletionSource.Task;

            // Assert
            Assert.Equal(firstConnection, recycledConnection);
        }

        /// <summary>
        /// Verifies that synchronous waiters are served in request order when the pool is full,
        /// ensuring the first queued request receives the next returned connection.
        /// </summary>
        [Fact]
        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3730")]
        public async Task GetConnectionMaxPoolSize_ShouldRespectOrderOfRequest()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            SqlConnection firstOwningConnection = new();

            pool.TryGetConnection(
                firstOwningConnection,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? firstConnection
            );

            for (int i = 1; i < pool.PoolGroupOptions.MaxPoolSize; i++)
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? internalConnection
                );

                // Assert
                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            // Use ManualResetEventSlim to synchronize the tasks
            // and force the request queueing order.
            using ManualResetEventSlim mresQueueOrder = new();
            using CountdownEvent allRequestsQueued = new(2);

            // Act
            var recycledTask = Task.Run(() =>
            {
                mresQueueOrder.Set();
                allRequestsQueued.Signal();
                pool.TryGetConnection(
                    new SqlConnection(""),
                    null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? recycledConnection
                );
                return recycledConnection;
            });
            var failedTask = Task.Run(() =>
            {
                // Force this request to be second in the queue.
                mresQueueOrder.Wait();
                allRequestsQueued.Signal();
                pool.TryGetConnection(
                    new SqlConnection("Timeout=1"),
                    null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(1)),
                    out DbConnectionInternal? failedConnection
                );
                return failedConnection;
            });

            allRequestsQueued.Wait();
            pool.ReturnInternalConnection(firstConnection!, firstOwningConnection);
            var recycledConnection = await recycledTask;

            // Assert
            Assert.Equal(firstConnection, recycledConnection);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await failedTask);
        }

        /// <summary>
        /// Verifies that asynchronous waiters are served in request order when the pool is full,
        /// ensuring the first queued request receives the next returned connection.
        /// </summary>
        [Fact]
        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3730")]
        public async Task GetConnectionAsyncMaxPoolSize_ShouldRespectOrderOfRequest()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            SqlConnection firstOwningConnection = new();

            pool.TryGetConnection(
                firstOwningConnection,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? firstConnection
            );

            for (int i = 1; i < pool.PoolGroupOptions.MaxPoolSize; i++)
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? internalConnection
                );

                // Assert
                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            TaskCompletionSource<DbConnectionInternal> recycledTaskCompletionSource = new();
            TaskCompletionSource<DbConnectionInternal> failedCompletionSource = new();

            // Act
            pool.TryGetConnection(
                new SqlConnection(""),
                recycledTaskCompletionSource,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? recycledConnection
            );

            // Gives time for the recycled connection to be queued before the failed request is initiated.
            await Task.Delay(1000);

            pool.TryGetConnection(
                new SqlConnection("Timeout=1"),
                failedCompletionSource,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(1)),
                out DbConnectionInternal? failedConnection
            );

            pool.ReturnInternalConnection(firstConnection!, firstOwningConnection);
            recycledConnection = await recycledTaskCompletionSource.Task;

            // Assert
            Assert.Equal(firstConnection, recycledConnection);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => failedConnection = await failedCompletionSource.Task);
        }

        /// <summary>
        /// Verifies that a connection returned to the idle channel is reused by a subsequent
        /// request instead of allocating a new internal connection.
        /// </summary>
        [Fact]
        public void ConnectionsAreReused()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            SqlConnection owningConnection = new();

            // Act: Get the first connection
            var completed1 = pool.TryGetConnection(
                owningConnection,
                null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? internalConnection1
            );

            // Assert: First connection should succeed
            Assert.True(completed1);
            Assert.NotNull(internalConnection1);

            // Act: Return the first connection to the pool
            pool.ReturnInternalConnection(internalConnection1, owningConnection);

            // Act: Get the second connection (should reuse the first one)
            var completed2 = pool.TryGetConnection(
                owningConnection,
                null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? internalConnection2
            );

            // Assert: Second connection should succeed and reuse the first connection
            Assert.True(completed2);
            Assert.NotNull(internalConnection2);
            Assert.Same(internalConnection1, internalConnection2);
        }

        /// <summary>
        /// Verifies that synchronous connection creation failures propagate the pooled-open timeout
        /// exception from the connection factory.
        /// </summary>
        [Fact]
        public void GetConnectionTimeout_ShouldThrowTimeoutException()
        {
            // Arrange
            var pool = ConstructPool(TimeoutConnectionFactory);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? internalConnection
                );
            });

            // Use the resource-backed message rather than a hardcoded English
            // string so the assertion stays meaningful under any localized build.
            Assert.Equal(ADP.PooledOpenTimeout().Message, ex.Message);
        }

        /// <summary>
        /// Verifies that asynchronous connection creation failures propagate the pooled-open timeout
        /// exception through the caller's task completion source.
        /// </summary>
        [Fact]
        public async Task GetConnectionAsyncTimeout_ShouldThrowTimeoutException()
        {
            // Arrange
            var pool = ConstructPool(TimeoutConnectionFactory);
            TaskCompletionSource<DbConnectionInternal> taskCompletionSource = new();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? internalConnection
                );

                await taskCompletionSource.Task;
            });

            // Use the resource-backed message rather than a hardcoded English
            // string so the assertion stays meaningful under any localized build.
            Assert.Equal(ADP.PooledOpenTimeout().Message, ex.Message);
        }

        /// <summary>
        /// Verifies under concurrent synchronous load that the pool never grows beyond its
        /// configured maximum size and continues to serve requests safely.
        /// </summary>
        [Fact]
        public void StressTest()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            ConcurrentBag<Task> tasks = new();

            // Act
            for (int i = 1; i < pool.PoolGroupOptions.MaxPoolSize * 3; i++)
            {
                var t = Task.Run(() =>
                {
                    SqlConnection owningObject = new();
                    var completed = pool.TryGetConnection(
                        owningObject,
                        taskCompletionSource: null,
                        TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                        out DbConnectionInternal? internalConnection
                    );
                    if (completed)
                    {
                        pool.ReturnInternalConnection(internalConnection!, owningObject);
                    }

                    Assert.True(completed);
                    Assert.NotNull(internalConnection);
                });
                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.True(pool.Count <= pool.PoolGroupOptions.MaxPoolSize, "Pool size exceeded max pool size after stress test.");
        }

        /// <summary>
        /// Verifies under concurrent asynchronous load that the pool never grows beyond its
        /// configured maximum size and continues to serve requests safely.
        /// </summary>
        [Fact]
        public void StressTestAsync()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            ConcurrentBag<Task> tasks = new();

            // Act
            for (int i = 1; i < pool.PoolGroupOptions.MaxPoolSize * 3; i++)
            {
                var t = Task.Run(async () =>
                {
                    SqlConnection owningObject = new();
                    TaskCompletionSource<DbConnectionInternal> taskCompletionSource = new();
                    var completed = pool.TryGetConnection(
                        owningObject,
                        taskCompletionSource,
                        TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                        out DbConnectionInternal? internalConnection
                    );
                    internalConnection = await taskCompletionSource.Task;
                    pool.ReturnInternalConnection(internalConnection, owningObject);

                    Assert.NotNull(internalConnection);
                });
                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.True(pool.Count <= pool.PoolGroupOptions.MaxPoolSize, "Pool size exceeded max pool size after stress test.");
        }


        #region Property Tests

        /// <summary>
        /// Verifies that the pool exposes the <see cref="SqlConnectionFactory"/> instance it was
        /// constructed with.
        /// </summary>
        [Fact]
        public void TestConnectionFactory()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act & Assert
            Assert.Equal(SuccessfulConnectionFactory, pool.ConnectionFactory);
        }

        /// <summary>
        /// Verifies that a newly constructed pool starts with zero tracked connections.
        /// </summary>
        [Fact]
        public void TestCount()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act & Assert
            Assert.Equal(0, pool.Count);
        }

        /// <summary>
        /// Verifies that a newly constructed pool reports no blocking-period error by default.
        /// </summary>
        [Fact]
        public void TestErrorOccurred()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act & Assert
            Assert.False(pool.ErrorOccurred);
        }

        /// <summary>
        /// Verifies that the pool assigns a positive instance identifier at construction time.
        /// </summary>
        [Fact]
        public void TestId()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act & Assert
            Assert.True(pool.Id >= 1);
        }

        /// <summary>
        /// Verifies that the pool exposes the identity object it was constructed with.
        /// </summary>
        [Fact]
        public void TestIdentity()
        {
            // Arrange
            var identity = DbConnectionPoolIdentity.GetCurrent();
            var pool = ConstructPool(SuccessfulConnectionFactory, identity);

            // Act & Assert
            Assert.Equal(identity, pool.Identity);
        }

        /// <summary>
        /// Verifies that a newly constructed pool begins in the running state.
        /// </summary>
        [Fact]
        public void TestIsRunning()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act & Assert
            Assert.True(pool.IsRunning);
        }

        /// <summary>
        /// Verifies that the pool exposes the configured load-balance timeout from its pool group
        /// options.
        /// </summary>
        [Fact]
        public void TestLoadBalanceTimeout()
        {
            // Arrange
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 50,
                    creationTimeout: 15,
                    loadBalanceTimeout: 500,
                    hasTransactionAffinity: true
            );
            var pool = ConstructPool(SuccessfulConnectionFactory, poolGroupOptions: poolGroupOptions);

            // Act & Assert
            Assert.Equal(poolGroupOptions.LoadBalanceTimeout, pool.LoadBalanceTimeout);
        }

        /// <summary>
        /// Verifies that the pool exposes the exact <see cref="DbConnectionPoolGroup"/> instance it
        /// was constructed with.
        /// </summary>
        [Fact]
        public void TestPoolGroup()
        {
            // Arrange
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 50,
                    creationTimeout: 15,
                    loadBalanceTimeout: 500,
                    hasTransactionAffinity: true));
            var pool = ConstructPool(SuccessfulConnectionFactory, dbConnectionPoolGroup: dbConnectionPoolGroup);

            // Act & Assert
            Assert.Equal(dbConnectionPoolGroup, pool.PoolGroup);
        }

        /// <summary>
        /// Verifies that the pool exposes the exact <see cref="DbConnectionPoolGroupOptions"/>
        /// instance it was constructed with.
        /// </summary>
        [Fact]
        public void TestPoolGroupOptions()
        {
            // Arrange
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 50,
                creationTimeout: 15,
                loadBalanceTimeout: 500,
                hasTransactionAffinity: true);
            var pool = ConstructPool(SuccessfulConnectionFactory, poolGroupOptions: poolGroupOptions);

            // Act & Assert
            Assert.Equal(poolGroupOptions, pool.PoolGroupOptions);
        }

        /// <summary>
        /// Verifies that the pool exposes the provider info object it was constructed with.
        /// </summary>
        [Fact]
        public void TestProviderInfo()
        {
            // Arrange
            var connectionPoolProviderInfo = new DbConnectionPoolProviderInfo();
            var pool = ConstructPool(SuccessfulConnectionFactory, connectionPoolProviderInfo: connectionPoolProviderInfo);

            // Act & Assert
            Assert.Equal(connectionPoolProviderInfo, pool.ProviderInfo);
        }

        /// <summary>
        /// Verifies that the pool state getter reports <see cref="DbConnectionPoolState.Running"/>
        /// immediately after construction.
        /// </summary>
        [Fact]
        public void TestStateGetter()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act & Assert
            Assert.Equal(DbConnectionPoolState.Running, pool.State);
        }

        /// <summary>
        /// Verifies that the pool state remains <see cref="DbConnectionPoolState.Running"/> after
        /// construction when no shutdown has been requested.
        /// </summary>
        [Fact]
        public void TestStateSetter()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act & Assert
            Assert.Equal(DbConnectionPoolState.Running, pool.State);
        }

        /// <summary>
        /// Verifies that the pool exposes whether load balancing is enabled based on its configured
        /// pool group options.
        /// </summary>
        [Fact]
        public void TestUseLoadBalancing()
        {
            // Arrange
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 50,
                creationTimeout: 15,
                loadBalanceTimeout: 500,
                hasTransactionAffinity: true);
            var pool = ConstructPool(SuccessfulConnectionFactory, poolGroupOptions: poolGroupOptions);

            // Act & Assert
            Assert.Equal(poolGroupOptions.UseLoadBalancing, pool.UseLoadBalancing);
        }

        #endregion

        #region Not Implemented Method Tests

        /// <summary>
        /// Verifies that <see cref="ChannelDbConnectionPool.PutObjectFromTransactedPool"/> remains
        /// unimplemented and throws <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void TestPutObjectFromTransactedPool()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => pool.PutObjectFromTransactedPool(null!));
        }

        /// <summary>
        /// Verifies that <see cref="ChannelDbConnectionPool.ReplaceConnection(System.Data.Common.DbConnection, Microsoft.Data.ProviderBase.DbConnectionInternal, Microsoft.Data.ProviderBase.TimeoutTimer)"/>
        /// remains unimplemented and throws <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void TestReplaceConnection()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => pool.ReplaceConnection(null!, null!, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));
        }

        /// <summary>
        /// Verifies that <see cref="ChannelDbConnectionPool.TransactionEnded(System.Transactions.Transaction, Microsoft.Data.ProviderBase.DbConnectionInternal)"/>
        /// remains unimplemented and throws <see cref="NotImplementedException"/>.
        /// </summary>
        [Fact]
        public void TestTransactionEnded()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => pool.TransactionEnded(null!, null!));
        }
        #endregion

        #region Pool Clear Tests

        /// <summary>
        /// Verifies that clearing an empty pool is a no-op and leaves the pool in a valid state.
        /// </summary>
        [Fact]
        public void Clear_EmptyPool_DoesNotThrow()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act
            pool.Clear();

            // Assert
            Assert.Equal(0, pool.Count);
        }

        /// <summary>
        /// Verifies that clearing a pool with only idle connections destroys them immediately and
        /// leaves the pool empty.
        /// </summary>
        [Fact]
        public void Clear_MultipleIdleConnections_AllAreDestroyed()
        {
            // Arrange
            int numConnections = 5;
            var pool = ConstructPool(SuccessfulConnectionFactory);
            var owningConnections = new SqlConnection[numConnections];
            var internalConnections = new DbConnectionInternal?[numConnections];

            for (int i = 0; i < numConnections; i++)
            {
                owningConnections[i] = new SqlConnection();
                pool.TryGetConnection(
                    owningConnections[i],
                    taskCompletionSource: null,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out internalConnections[i]
                );
                Assert.Equal(0, internalConnections[i]!.ClearGeneration);
            }

            // Return all connections to the pool
            for (int i = 0; i < numConnections; i++)
            {
                pool.ReturnInternalConnection(internalConnections[i]!, owningConnections[i]);
            }

            // Act
            pool.Clear();

            // Assert
            Assert.Equal(0, pool.Count);
        }

        /// <summary>
        /// Verifies that clearing the pool does not immediately destroy a connection that is still
        /// checked out by a caller.
        /// </summary>
        [Fact]
        public void Clear_BusyConnection_NotDestroyedImmediately()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            SqlConnection owningConnection = new();

            pool.TryGetConnection(
                owningConnection,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? busyConnection
            );
            Assert.NotNull(busyConnection);
            Assert.Equal(0, busyConnection.ClearGeneration);

            // Act - Clear while connection is still busy
            pool.Clear();

            // Assert - Busy connection is still tracked in the pool and retains its old generation
            Assert.Equal(1, pool.Count);
            Assert.Equal(0, busyConnection.ClearGeneration);
        }

        /// <summary>
        /// Verifies that a busy connection checked out during <see cref="ChannelDbConnectionPool.Clear"/>
        /// is destroyed when it is later returned because its generation is stale.
        /// </summary>
        [Fact]
        public void Clear_BusyConnectionReturned_IsDestroyed()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            SqlConnection owningConnection = new();

            pool.TryGetConnection(
                owningConnection,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? busyConnection
            );
            Assert.NotNull(busyConnection);
            Assert.Equal(0, busyConnection.ClearGeneration);

            // Act - Clear, then return the busy connection
            pool.Clear();

            // Assert - Busy connection is still tracked but has stale generation
            Assert.Equal(1, pool.Count);

            // Act - Return the busy connection
            pool.ReturnInternalConnection(busyConnection, owningConnection);

            // Assert - The connection should have been destroyed on return (generation mismatch)
            Assert.Equal(0, pool.Count);
        }

        /// <summary>
        /// Verifies that clearing a pool with both busy and idle connections destroys only the idle
        /// connections immediately and defers busy-connection cleanup until return.
        /// </summary>
        [Fact]
        public void Clear_MixedBusyAndIdle_OnlyIdleDestroyedImmediately()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            SqlConnection busyOwner = new();
            SqlConnection idleOwner = new();

            pool.TryGetConnection(
                busyOwner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? busyConnection
            );
            pool.TryGetConnection(
                idleOwner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? idleConnection
            );
            Assert.NotNull(busyConnection);
            Assert.NotNull(idleConnection);
            Assert.Equal(0, busyConnection.ClearGeneration);
            Assert.Equal(0, idleConnection.ClearGeneration);

            // Return only the idle connection
            pool.ReturnInternalConnection(idleConnection, idleOwner);

            // Act
            pool.Clear();

            // Assert - Only the busy connection remains with stale generation
            Assert.Equal(1, pool.Count);
            Assert.Equal(0, busyConnection.ClearGeneration);

            // Now return the busy connection - it should be destroyed (generation 0 != pool generation 1)
            pool.ReturnInternalConnection(busyConnection, busyOwner);
            Assert.Equal(0, pool.Count);
        }

        /// <summary>
        /// Verifies that connections created after a clear are stamped with the new generation and
        /// are pooled and reused normally.
        /// </summary>
        [Fact]
        public void Clear_NewConnectionsAfterClear_ArePooledNormally()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            SqlConnection owningConnection = new();

            pool.TryGetConnection(
                owningConnection,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? oldConnection
            );
            Assert.Equal(0, oldConnection!.ClearGeneration);
            pool.ReturnInternalConnection(oldConnection, owningConnection);

            // Act
            pool.Clear();

            // Get a new connection after clear
            SqlConnection newOwner = new();
            pool.TryGetConnection(
                newOwner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? newConnection
            );
            Assert.NotNull(newConnection);

            // The new connection should be different from the old one and have generation 1
            Assert.NotSame(oldConnection, newConnection);
            Assert.Equal(1, newConnection.ClearGeneration);

            // Return the new connection - it should be pooled normally
            pool.ReturnInternalConnection(newConnection, newOwner);
            Assert.Equal(1, pool.Count);

            // Get another connection - it should reuse the post-clear connection (same generation)
            SqlConnection reuseOwner = new();
            pool.TryGetConnection(
                reuseOwner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? reusedConnection
            );
            Assert.Same(newConnection, reusedConnection);
            Assert.Equal(1, reusedConnection!.ClearGeneration);
        }

        /// <summary>
        /// Verifies that repeated clear operations do not corrupt pool state and that each clear
        /// increments the pool generation as expected.
        /// </summary>
        [Fact]
        public void Clear_MultipleClearCalls_DoNotCorruptState()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            SqlConnection owningConnection = new();

            pool.TryGetConnection(
                owningConnection,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? connection
            );
            Assert.Equal(0, connection!.ClearGeneration);
            pool.ReturnInternalConnection(connection, owningConnection);

            // Act - Call clear multiple times rapidly
            pool.Clear();
            pool.Clear();
            pool.Clear();

            // Assert - Pool state is still valid
            Assert.Equal(0, pool.Count);

            // New connections should have generation 3 (incremented three times)
            SqlConnection newOwner = new();
            pool.TryGetConnection(
                newOwner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? newConnection
            );
            Assert.NotNull(newConnection);
            Assert.Equal(1, pool.Count);
            Assert.Equal(3, newConnection.ClearGeneration);
        }

        #endregion

        #region Test classes

        /// <summary>
        /// Test connection factory that always succeeds and captures the timeout budget passed in by
        /// the pool so timeout propagation can be asserted.
        /// </summary>
        internal class SuccessfulSqlConnectionFactory : SqlConnectionFactory
        {
            /// <summary>
            /// Gets the last timeout budget passed through by the pool to the factory.
            /// </summary>
            internal TimeoutTimer? CapturedTimeout { get; private set; }

            /// <summary>
            /// Creates a successful stub internal connection and records the timeout budget used for
            /// the creation attempt.
            /// </summary>
            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
            {
                CapturedTimeout = timeout;
                return new StubDbConnectionInternal();
            }
        }

        /// <summary>
        /// Test connection factory that always throws the pooled-open timeout to exercise failure
        /// paths in the pool.
        /// </summary>
        internal class TimeoutSqlConnectionFactory : SqlConnectionFactory
        {
            /// <summary>
            /// Throws the pooled-open timeout exception to simulate a failed physical connection
            /// creation.
            /// </summary>
            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
            {
                throw ADP.PooledOpenTimeout();
            }
        }

        /// <summary>
        /// Minimal <see cref="DbConnectionInternal"/> test double used by the pool tests to avoid
        /// involving a real provider-specific connection implementation.
        /// </summary>
        internal class StubDbConnectionInternal : DbConnectionInternal
        {
            #region Not Implemented Members
            public override string ServerVersion => throw new NotImplementedException();

            public override DbTransaction BeginTransaction(System.Data.IsolationLevel il)
            {
                throw new NotImplementedException();
            }

            public override void EnlistTransaction(Transaction transaction)
            {
                return;
            }

            protected override void Activate(Transaction transaction)
            {
                return;
            }

            protected override void Deactivate()
            {
                return;
            }

            internal override void ResetConnection()
            {
                return;
            }
            #endregion
        }
        #endregion

        /// <summary>
        /// Verifies that constructing the pool with a zero max pool size fails with the expected
        /// capacity validation error.
        /// </summary>
        [Fact]
        public void Constructor_WithZeroMaxPoolSize_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 0, // This should cause an exception
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true
            );
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions
            );

            // Act
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => 
                new ChannelDbConnectionPool(
                    SuccessfulConnectionFactory,
                    dbConnectionPoolGroup,
                    DbConnectionPoolIdentity.NoIdentity,
                    new DbConnectionPoolProviderInfo()
                ));

            // Assert
            Assert.Equal("fixedCapacity", exception.ParamName);
            Assert.Contains("Capacity must be greater than zero", exception.Message);
        }

        /// <summary>
        /// Verifies that large but valid max pool sizes pass capacity validation and either succeed
        /// or fail only due to memory pressure rather than argument validation.
        /// </summary>
        [Fact]
        public void Constructor_WithLargeMaxPoolSize()
        {
            // Arrange
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 10000,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true
            );
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions
            );

            try
            {
                // Act
                var pool = new ChannelDbConnectionPool(
                    SuccessfulConnectionFactory,
                    dbConnectionPoolGroup,
                    DbConnectionPoolIdentity.NoIdentity,
                    new DbConnectionPoolProviderInfo()
                );

                // Assert
                Assert.NotNull(pool);
                Assert.Equal(0, pool.Count);
            }
            catch (OutOfMemoryException)
            {
                // OutOfMemoryException is acceptable when trying to allocate an array of int.MaxValue size
                // This test is primarily checking that ArgumentOutOfRangeException is not thrown for the capacity validation
                // The fact that we reach the OutOfMemoryException means the capacity validation passed
            }
        }

        /// <summary>
        /// Verifies that small valid max pool sizes construct successfully and produce usable pool
        /// instances.
        /// </summary>
        [Fact]
        public void Constructor_WithValidSmallPoolSizes_WorksCorrectly()
        {
            // Arrange
            var poolGroupOptions1 = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 1,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true
            );
            var dbConnectionPoolGroup1 = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions1
            );

            // Act
            var pool1 = new ChannelDbConnectionPool(
                SuccessfulConnectionFactory,
                dbConnectionPoolGroup1,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo()
            );

            // Assert
            Assert.NotNull(pool1);
            Assert.Equal(0, pool1.Count);

            // Arrange
            var poolGroupOptions2 = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 2,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true
            );
            var dbConnectionPoolGroup2 = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions2
            );

            // Act
            var pool2 = new ChannelDbConnectionPool(
                SuccessfulConnectionFactory,
                dbConnectionPoolGroup2,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo()
            );

            // Assert
            Assert.NotNull(pool2);
            Assert.Equal(0, pool2.Count);
        }

        #region Rate Limiting And Blocking Period Tests

        /// <summary>
        /// Verifies that a connection creation failure enters the blocking-period error state when
        /// blocking is enabled for the pool.
        /// </summary>
        [Fact]
        public void ErrorOccurred_FailureWithBlockingEnabled_BecomesTrue()
        {
            // Arrange
            // Default PoolBlockingPeriod is Auto; localhost is non-Azure so blocking is enabled.
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 4,
                    creationTimeout: 15,
                    loadBalanceTimeout: 0,
                    hasTransactionAffinity: true));
            var pool = ConstructPool(TimeoutConnectionFactory, dbConnectionPoolGroup: dbConnectionPoolGroup);

            // Act
            Assert.False(pool.ErrorOccurred);

            Assert.Throws<InvalidOperationException>(() =>
                pool.TryGetConnection(new SqlConnection(), taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));

            // Assert
            Assert.True(pool.ErrorOccurred);
        }

        /// <summary>
        /// Verifies that a connection creation failure does not enter the blocking-period error state
        /// when the connection string disables blocking with NeverBlock.
        /// </summary>
        [Fact]
        public void ErrorOccurred_FailureWithNeverBlock_StaysFalse()
        {
            // Arrange
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;Pool Blocking Period=NeverBlock;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 4,
                    creationTimeout: 15,
                    loadBalanceTimeout: 0,
                    hasTransactionAffinity: true));
            var pool = ConstructPool(TimeoutConnectionFactory, dbConnectionPoolGroup: dbConnectionPoolGroup);

            // Act
            Assert.Throws<InvalidOperationException>(() =>
                pool.TryGetConnection(new SqlConnection(), taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));

            // Assert - FR-007: NeverBlock must not enter the error state.
            Assert.False(pool.ErrorOccurred);
        }

        /// <summary>
        /// Verifies that a connection creation failure enters the blocking-period error state when
        /// the connection string explicitly enables AlwaysBlock.
        /// </summary>
        [Fact]
        public void ErrorOccurred_FailureWithAlwaysBlock_BecomesTrue()
        {
            // Arrange
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;Pool Blocking Period=AlwaysBlock;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 4,
                    creationTimeout: 15,
                    loadBalanceTimeout: 0,
                    hasTransactionAffinity: true));
            var pool = ConstructPool(TimeoutConnectionFactory, dbConnectionPoolGroup: dbConnectionPoolGroup);

            // Act
            Assert.Throws<InvalidOperationException>(() =>
                pool.TryGetConnection(new SqlConnection(), taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));

            // Assert
            Assert.True(pool.ErrorOccurred);
        }

        /// <summary>
        /// Verifies that once the pool enters the blocking period, subsequent synchronous requests
        /// fail fast with the cached exception instead of waiting for a full connection attempt.
        /// </summary>
        [Fact]
        public void ErrorOccurred_BlockingEnabled_SubsequentRequestFastFails()
        {
            // Arrange
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 4,
                    creationTimeout: 15,
                    loadBalanceTimeout: 0,
                    hasTransactionAffinity: true));
            var pool = ConstructPool(TimeoutConnectionFactory, dbConnectionPoolGroup: dbConnectionPoolGroup);

            // Act
            var first = Assert.Throws<InvalidOperationException>(() =>
                pool.TryGetConnection(new SqlConnection(), taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));
            Assert.True(pool.ErrorOccurred);

            // FR-006: subsequent requests inside the blocking window must fail fast with the
            // cached exception. We assert it returns very quickly compared to a fresh attempt.
            var sw = Stopwatch.StartNew();
            var second = Assert.Throws<InvalidOperationException>(() =>
                pool.TryGetConnection(new SqlConnection(), taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));
            sw.Stop();

            // Assert
            Assert.Equal(first.Message, second.Message);
            Assert.True(sw.ElapsedMilliseconds < 1000,
                $"Expected fast-fail (<1000ms) while in blocking period, took {sw.ElapsedMilliseconds}ms.");
        }

        /// <summary>
        /// Verifies that clearing the pool while in the blocking-period error state resets the
        /// externally visible error indicator.
        /// </summary>
        [Fact]
        public void Clear_InErrorState_ResetsErrorOccurred()
        {
            // Arrange
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 4,
                    creationTimeout: 15,
                    loadBalanceTimeout: 0,
                    hasTransactionAffinity: true));
            var pool = ConstructPool(TimeoutConnectionFactory, dbConnectionPoolGroup: dbConnectionPoolGroup);

            Assert.Throws<InvalidOperationException>(() =>
                pool.TryGetConnection(new SqlConnection(), taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));
            Assert.True(pool.ErrorOccurred);

            // Act - FR-011: Clear must reset the error state.
            pool.Clear();

            // Assert
            Assert.False(pool.ErrorOccurred);
        }

        /// <summary>
        /// Verifies that a successful connection creation after a prior failure leaves the pool out
        /// of the blocking-period error state.
        /// </summary>
        [Fact]
        public void SuccessfulCreate_AfterFailure_ClearsErrorState()
        {
            // Arrange
            var factory = new ToggleFailureConnectionFactory();
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 4,
                    creationTimeout: 15,
                    loadBalanceTimeout: 0,
                    hasTransactionAffinity: true));
            var pool = ConstructPool(factory, dbConnectionPoolGroup: dbConnectionPoolGroup);

            // First call fails and enters the error state.
            factory.FailNextCreate = true;
            Assert.Throws<InvalidOperationException>(() =>
                pool.TryGetConnection(new SqlConnection(), taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));
            Assert.True(pool.ErrorOccurred);

            // Manually clear the error flag (simulating the backoff timer firing) and then
            // verify that a subsequent successful create clears the cached error state. FR-009.
            pool.Clear();
            Assert.False(pool.ErrorOccurred);

            factory.FailNextCreate = false;

            // Act
            var completed = pool.TryGetConnection(new SqlConnection(), taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out var conn);

            // Assert
            Assert.True(completed);
            Assert.NotNull(conn);
            Assert.False(pool.ErrorOccurred);
        }

        /// <summary>
        /// Verifies that failed connection attempts release any acquired rate-limiter lease so the
        /// pool does not starve future callers after repeated failures.
        /// </summary>
        [Fact]
        public async Task RateLimiter_LeaseDisposedOnFailure_DoesNotStarvePool()
        {
            // Arrange
            // If the rate limiter lease were not disposed on failure, after N failures (where N is
            // the limiter's permit count) every subsequent request would deadlock. Verify that we
            // can keep getting failures back without ever blocking the thread pool.
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;Pool Blocking Period=NeverBlock;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 4,
                    creationTimeout: 15,
                    loadBalanceTimeout: 0,
                    hasTransactionAffinity: true));
            var pool = ConstructPool(TimeoutConnectionFactory, dbConnectionPoolGroup: dbConnectionPoolGroup);

            // Act & Assert
            for (int i = 0; i < 8; i++)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    var tcs = new TaskCompletionSource<DbConnectionInternal>();
                    pool.TryGetConnection(new SqlConnection(), tcs, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _);
                    await tcs.Task;
                });
            }
        }

        /// <summary>
        /// Test connection factory that can be toggled between failure and success to exercise pool
        /// recovery behavior after blocking-period entry.
        /// </summary>
        internal class ToggleFailureConnectionFactory : SqlConnectionFactory
        {
            /// <summary>
            /// Gets or sets whether the next connection creation attempt should fail.
            /// </summary>
            public bool FailNextCreate { get; set; }

            /// <summary>
            /// Creates a stub connection or throws the pooled-open timeout based on
            /// <see cref="FailNextCreate"/>.
            /// </summary>
            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
            {
                if (FailNextCreate)
                {
                    throw ADP.PooledOpenTimeout();
                }

                return new StubDbConnectionInternal();
            }
        }

        #endregion

        #region Connection Timeout Awareness Tests

        /// <summary>
        /// Verifies that two concurrent callers waiting for the same exhausted
        /// pool observe their own per-caller <see cref="TimeoutTimer"/> deadlines
        /// independently: the caller with the shorter timeout fails with the
        /// pool-timeout error while the caller with the longer timeout continues
        /// to wait and eventually succeeds when a connection is returned.
        /// </summary>
        /// <remarks>
        /// Both callers share a single <see cref="FakeTimeProvider"/> so that
        /// advancing virtual time deterministically expires only the short-timeout
        /// caller's CTS without consuming any wall-clock time.
        /// </remarks>
        [Fact]
        public async Task ConcurrentCallers_ShouldTimeoutIndependently()
        {
            // Arrange: pool at max capacity so both callers must wait
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 1,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true
            );
            var pool = ConstructPool(SuccessfulConnectionFactory, poolGroupOptions: poolGroupOptions);

            SqlConnection firstOwner = new();
            pool.TryGetConnection(firstOwner, taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? firstConnection);
            Assert.NotNull(firstConnection);

            // Use a single fake time provider shared by both callers so we can independently
            // expire each caller's timeout via virtual time without any wall-clock waits.
            // Build the timers up-front so they are anchored at virtual time t=0.
            var fakeTime = new FakeTimeProvider();
            TimeoutTimer timerA = TimeoutTimer.StartNew(TimeSpan.FromSeconds(1), fakeTime);
            TimeoutTimer timerB = TimeoutTimer.StartNew(TimeSpan.FromSeconds(10), fakeTime);

            // Caller A: 1s virtual timeout, Caller B: 10s virtual timeout. Both run in
            // background tasks so the sync pool path can block on the channel as in production.
            var callerATask = Task.Run(() =>
            {
                pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    timerA,
                    out DbConnectionInternal? connectionA);
                return connectionA;
            });

            var callerBTask = Task.Run(() =>
            {
                pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    timerB,
                    out DbConnectionInternal? connectionB);
                return connectionB;
            });

            // Act: advance virtual time past A's 1s timeout but well within B's 10s timeout.
            // A's CancellationTokenSource fires (cancelling its channel wait), B's does not.
            fakeTime.Advance(TimeSpan.FromSeconds(2));

            // Assert: Caller A should observe the timeout
            var exA = await Assert.ThrowsAsync<InvalidOperationException>(() => callerATask);
            Assert.Equal(
                "Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.",
                exA.Message);

            // Caller B should still be waiting (8s of virtual budget remain)
            Assert.False(callerBTask.IsCompleted, "Caller B should still be waiting");

            // Release the connection so caller B can succeed
            pool.ReturnInternalConnection(firstConnection, firstOwner);

            // Bound the wait so a regression in the pool can't hang the test suite
            // indefinitely; a real success completes well under this budget.
            Task completed = await Task.WhenAny(callerBTask, Task.Delay(TimeSpan.FromSeconds(30)));
            Assert.Same(callerBTask, completed);
            var resultB = await callerBTask;

            // Caller B got the connection
            Assert.NotNull(resultB);
            Assert.Same(firstConnection, resultB);
        }

        /// <summary>
        /// Verifies that the <see cref="TimeoutTimer"/> the pool hands to the
        /// connection factory reports a reduced remaining-time budget once the
        /// timer's clock has advanced. This guarantees the factory observes the
        /// actual remaining budget at the moment of the call rather than a
        /// fresh, full timeout.
        /// </summary>
        /// <remarks>
        /// Drives elapsed time deterministically with a
        /// <see cref="FakeTimeProvider"/> so the test does not depend on real
        /// wall-clock waits or thread sleeps.
        /// </remarks>
        [Fact]
        public void GetConnection_TimeoutTimerReflectsPoolWaitTime()
        {
            // Arrange: a capturing factory and a fake-time-backed timer with a
            // 30-second budget anchored at virtual time t = 0.
            var factory = new SuccessfulSqlConnectionFactory();
            var pool = ConstructPool(factory);
            var owner = new SqlConnection("Timeout=30");
            var fakeTime = new FakeTimeProvider();
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(30), fakeTime);

            // Act: advance virtual time by 5 seconds before invoking the pool,
            // simulating budget that was consumed elsewhere (e.g., waiting on a
            // pool slot) before the factory was called.
            fakeTime.Advance(TimeSpan.FromSeconds(5));
            pool.TryGetConnection(owner, taskCompletionSource: null, timer, out DbConnectionInternal? connection);

            // Assert: factory received the same timer, and it reports the
            // reduced 25-second remaining budget.
            Assert.NotNull(connection);
            Assert.Same(timer, factory.CapturedTimeout);
            Assert.Equal(25_000, factory.CapturedTimeout!.MillisecondsRemainingInt);
        }

        #endregion
    }
}


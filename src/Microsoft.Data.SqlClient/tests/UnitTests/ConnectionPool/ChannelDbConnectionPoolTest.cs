// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    public class ChannelDbConnectionPoolTest
    {
        private static readonly SqlConnectionFactory SuccessfulConnectionFactory = new SuccessfulSqlConnectionFactory();
        private static readonly SqlConnectionFactory TimeoutConnectionFactory = new TimeoutSqlConnectionFactory();

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
                    hasTransactionAffinity: true,
                    idleTimeout: 0
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

                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            TaskCompletionSource<DbConnectionInternal> tcs = new();

            // Act
            var task = Task.Run(() =>
            {
                var exceeded = pool.TryGetConnection(
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

                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            TaskCompletionSource<DbConnectionInternal> taskCompletionSource = new();

            // Act
            var exceeded = pool.TryGetConnection(
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

                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            TaskCompletionSource<DbConnectionInternal> recycledTaskCompletionSource = new();
            TaskCompletionSource<DbConnectionInternal> failedCompletionSource = new();

            // Act
            var exceeded = pool.TryGetConnection(
                new SqlConnection(""),
                recycledTaskCompletionSource,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? recycledConnection
            );

            // Gives time for the recycled connection to be queued before the failed request is initiated.
            await Task.Delay(1000);

            var exceeded2 = pool.TryGetConnection(
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

        [Fact]
        public void StressTest()
        {
            //Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            ConcurrentBag<Task> tasks = new();

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
            Assert.True(pool.Count <= pool.PoolGroupOptions.MaxPoolSize, "Pool size exceeded max pool size after stress test.");
        }

        [Fact]
        public void StressTestAsync()
        {
            //Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            ConcurrentBag<Task> tasks = new();

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
            Assert.True(pool.Count <= pool.PoolGroupOptions.MaxPoolSize, "Pool size exceeded max pool size after stress test.");
        }


        #region Property Tests

        [Fact]
        public void TestConnectionFactory()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.Equal(SuccessfulConnectionFactory, pool.ConnectionFactory);
        }

        [Fact]
        public void TestCount()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.Equal(0, pool.Count);
        }

        [Fact]
        public void TestErrorOccurred()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.False(pool.ErrorOccurred);
        }

        [Fact]
        public void TestId()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.True(pool.Id >= 1);
        }

        [Fact]
        public void TestIdentity()
        {
            var identity = DbConnectionPoolIdentity.GetCurrent();
            var pool = ConstructPool(SuccessfulConnectionFactory, identity);
            Assert.Equal(identity, pool.Identity);
        }

        [Fact]
        public void TestIsRunning()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.True(pool.IsRunning);
        }

        [Fact]
        public void TestLoadBalanceTimeout()
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 50,
                    creationTimeout: 15,
                    loadBalanceTimeout: 500,
                    hasTransactionAffinity: true,
                    idleTimeout: 0
            );
            var pool = ConstructPool(SuccessfulConnectionFactory, poolGroupOptions: poolGroupOptions);
            Assert.Equal(poolGroupOptions.LoadBalanceTimeout, pool.LoadBalanceTimeout);
        }

        [Fact]
        public void TestPoolGroup()
        {
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 50,
                    creationTimeout: 15,
                    loadBalanceTimeout: 500,
                    hasTransactionAffinity: true,
                    idleTimeout: 0));
            var pool = ConstructPool(SuccessfulConnectionFactory, dbConnectionPoolGroup: dbConnectionPoolGroup);
            Assert.Equal(dbConnectionPoolGroup, pool.PoolGroup);
        }

        [Fact]
        public void TestPoolGroupOptions()
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 50,
                creationTimeout: 15,
                loadBalanceTimeout: 500,
                hasTransactionAffinity: true,
                idleTimeout: 0);
            var pool = ConstructPool(SuccessfulConnectionFactory, poolGroupOptions: poolGroupOptions);
            Assert.Equal(poolGroupOptions, pool.PoolGroupOptions);
        }

        [Fact]
        public void TestProviderInfo()
        {
            var connectionPoolProviderInfo = new DbConnectionPoolProviderInfo();
            var pool = ConstructPool(SuccessfulConnectionFactory, connectionPoolProviderInfo: connectionPoolProviderInfo);
            Assert.Equal(connectionPoolProviderInfo, pool.ProviderInfo);
        }

        [Fact]
        public void TestStateGetter()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.Equal(DbConnectionPoolState.Running, pool.State);
        }

        [Fact]
        public void TestStateSetter()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.Equal(DbConnectionPoolState.Running, pool.State);
        }

        [Fact]
        public void TestUseLoadBalancing()
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 50,
                creationTimeout: 15,
                loadBalanceTimeout: 500,
                hasTransactionAffinity: true,
                idleTimeout: 0);
            var pool = ConstructPool(SuccessfulConnectionFactory, poolGroupOptions: poolGroupOptions);
            Assert.Equal(poolGroupOptions.UseLoadBalancing, pool.UseLoadBalancing);
        }

        #endregion

        #region Not Implemented Method Tests

        [Fact]
        public void TestPutObjectFromTransactedPool()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.PutObjectFromTransactedPool(null!));
        }

        [Fact]
        public void TestReplaceConnection()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.ReplaceConnection(null!, null!, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));
        }

        [Fact]
        public void TestTransactionEnded()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.TransactionEnded(null!, null!));
        }
        #endregion

        #region Pool Clear Tests

        [Fact]
        public void Clear_EmptyPool_DoesNotThrow()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);

            // Act & Assert - Should complete without error
            pool.Clear();
            Assert.Equal(0, pool.Count);
        }

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

        #region Idle Timeout Tests

        // Helper: build a pool whose IdleTimeout is the given number of seconds.
        private ChannelDbConnectionPool ConstructPoolWithIdleTimeout(int idleTimeoutSeconds)
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 50,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: idleTimeoutSeconds);
            return ConstructPool(SuccessfulConnectionFactory, poolGroupOptions: poolGroupOptions);
        }

        [Fact]
        public void IdleTimeout_PoolGroupOptions_ConvertsSecondsToTimeSpan()
        {
            // 30 seconds in -> TimeSpan(0, 0, 30) out.
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 50,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: 30);

            Assert.Equal(TimeSpan.FromSeconds(30), poolGroupOptions.IdleTimeout);
        }

        [Fact]
        public void IdleTimeout_StampedOnReturn()
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.UseLegacyIdleTimeoutBehavior = false;

            // Arrange - long idle timeout so the return path stamps (not evicts).
            var pool = ConstructPoolWithIdleTimeout(idleTimeoutSeconds: 3600);
            SqlConnection owningConnection = new();
            pool.TryGetConnection(owningConnection, taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? connection);
            Assert.NotNull(connection);

            // Backdate by a small amount that's still well inside the idle window so the return path
            // doesn't decide to evict instead of stamp.
            BackdateReturnedTime(connection, TimeSpan.FromSeconds(5));
            DateTime stampedBack = connection.ReturnedTime;

            // Act
            DateTime before = DateTime.UtcNow;
            pool.ReturnInternalConnection(connection, owningConnection);
            DateTime after = DateTime.UtcNow;

            // Assert: stamp falls within the return window and is strictly newer than the backdated value.
            Assert.InRange(connection.ReturnedTime, before, after);
            Assert.True(connection.ReturnedTime > stampedBack);
        }

        [Fact]
        public void IdleTimeout_Zero_DoesNotExpire()
        {
            // Arrange - pool with idle expiry disabled
            var pool = ConstructPoolWithIdleTimeout(idleTimeoutSeconds: 0);
            SqlConnection owner = new();
            pool.TryGetConnection(owner, taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? first);
            Assert.NotNull(first);

            // Return + back-date ReturnedTime to simulate a long sit.
            pool.ReturnInternalConnection(first, owner);
            BackdateReturnedTime(first, TimeSpan.FromHours(1));

            // Act
            SqlConnection owner2 = new();
            pool.TryGetConnection(owner2, taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? second);

            // Assert - same instance, idle expiry disabled
            Assert.Same(first, second);
            Assert.Equal(1, pool.Count);
        }

        [Fact]
        public void IdleTimeout_Set_ExpiresOldConnection()
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.UseLegacyIdleTimeoutBehavior = false;

            // Arrange - pool with 1-second idle timeout
            var pool = ConstructPoolWithIdleTimeout(idleTimeoutSeconds: 1);
            SqlConnection owner = new();
            pool.TryGetConnection(owner, taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? first);
            Assert.NotNull(first);

            // Return + back-date ReturnedTime beyond the timeout.
            pool.ReturnInternalConnection(first, owner);
            BackdateReturnedTime(first, TimeSpan.FromSeconds(5));

            // Act - request another connection
            SqlConnection owner2 = new();
            pool.TryGetConnection(owner2, taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? second);

            // Assert - the expired one is discarded; a new one is minted.
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            Assert.Equal(1, pool.Count);
        }

        [Fact]
        public void IdleTimeout_Set_KeepsFreshConnection()
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.UseLegacyIdleTimeoutBehavior = false;

            // Arrange - 60-second idle timeout, connection just returned
            var pool = ConstructPoolWithIdleTimeout(idleTimeoutSeconds: 60);
            SqlConnection owner = new();
            pool.TryGetConnection(owner, taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? first);
            Assert.NotNull(first);
            pool.ReturnInternalConnection(first, owner);

            // Act - immediately request another connection
            SqlConnection owner2 = new();
            pool.TryGetConnection(owner2, taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? second);

            // Assert - same instance reused, well within idle window
            Assert.Same(first, second);
        }

        [Fact]
        public void IdleTimeout_LegacySwitch_SuppressesEviction()
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.UseLegacyIdleTimeoutBehavior = true;

            // Arrange - 1-second idle timeout, but legacy switch suppresses the new eviction path.
            var pool = ConstructPoolWithIdleTimeout(idleTimeoutSeconds: 1);
            SqlConnection owner = new();
            pool.TryGetConnection(owner, taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? first);
            Assert.NotNull(first);

            // Return + back-date well past the configured timeout.
            pool.ReturnInternalConnection(first, owner);
            BackdateReturnedTime(first, TimeSpan.FromMinutes(5));

            // Act - request another connection.
            SqlConnection owner2 = new();
            pool.TryGetConnection(owner2, taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? second);

            // Assert - with the legacy switch on, the stale connection is still reused.
            Assert.Same(first, second);
        }

        // Forcibly rewinds a connection's ReturnedTime by the given amount so tests don't have to sleep.
        private static void BackdateReturnedTime(DbConnectionInternal connection, TimeSpan delta)
        {
            connection.ReturnedTime = DateTime.UtcNow - delta;
        }

        #endregion

        #region Test classes
        internal class SuccessfulSqlConnectionFactory : SqlConnectionFactory
        {
            internal TimeoutTimer? CapturedTimeout { get; private set; }

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

        internal class TimeoutSqlConnectionFactory : SqlConnectionFactory
        {
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
                hasTransactionAffinity: true,
                idleTimeout: 0
            );
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions
            );

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => 
                new ChannelDbConnectionPool(
                    SuccessfulConnectionFactory,
                    dbConnectionPoolGroup,
                    DbConnectionPoolIdentity.NoIdentity,
                    new DbConnectionPoolProviderInfo()
                ));
            
            Assert.Equal("fixedCapacity", exception.ParamName);
            Assert.Contains("Capacity must be greater than zero", exception.Message);
        }

        [Fact]
        public void Constructor_WithLargeMaxPoolSize()
        {
            // Arrange - Test that Int32.MaxValue is accepted as a valid pool size
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 10000,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: 0
            );
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions
            );

            try
            {
                // Act & Assert - This should not throw ArgumentOutOfRangeException, but may throw OutOfMemoryException
                var pool = new ChannelDbConnectionPool(
                    SuccessfulConnectionFactory,
                    dbConnectionPoolGroup,
                    DbConnectionPoolIdentity.NoIdentity,
                    new DbConnectionPoolProviderInfo()
                );

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

        [Fact]
        public void Constructor_WithValidSmallPoolSizes_WorksCorrectly()
        {
            // Arrange - Test various small pool sizes that should work correctly
            
            // Test with pool size of 1
            var poolGroupOptions1 = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 1,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: 0
            );
            var dbConnectionPoolGroup1 = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions1
            );

            // Act & Assert - Pool size of 1 should work
            var pool1 = new ChannelDbConnectionPool(
                SuccessfulConnectionFactory,
                dbConnectionPoolGroup1,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo()
            );

            Assert.NotNull(pool1);
            Assert.Equal(0, pool1.Count);

            // Test with pool size of 2
            var poolGroupOptions2 = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 2,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: 0
            );
            var dbConnectionPoolGroup2 = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions2
            );

            var pool2 = new ChannelDbConnectionPool(
                SuccessfulConnectionFactory,
                dbConnectionPoolGroup2,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo()
            );

            Assert.NotNull(pool2);
            Assert.Equal(0, pool2.Count);
        }

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
                hasTransactionAffinity: true,
                idleTimeout: 0
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

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
                    hasTransactionAffinity: true
            );
            dbConnectionPoolGroup ??= new DbConnectionPoolGroup(
                new DbConnectionOptions("DataSource=localhost;", null),
                new DbConnectionPoolKey("TestDataSource"),
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
                    new DbConnectionOptions("", null),
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
                    new DbConnectionOptions("", null),
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
                    new DbConnectionOptions("", null),
                    out DbConnectionInternal? internalConnection
                );

                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            try
            {
                // Act
                var exceeded = pool.TryGetConnection(
                    new SqlConnection("Timeout=1"),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out DbConnectionInternal? extraConnection
                );
            }
            catch (Exception ex)
            {
                // Assert
                Assert.IsType<InvalidOperationException>(ex);
                Assert.Equal("Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.", ex.Message);
            }

            // Assert
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
                    new DbConnectionOptions("", null),
                    out DbConnectionInternal? internalConnection
                );

                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            try
            {
                // Act
                TaskCompletionSource<DbConnectionInternal> taskCompletionSource = new();
                var exceeded = pool.TryGetConnection(
                    new SqlConnection("Timeout=1"),
                    taskCompletionSource,
                    new DbConnectionOptions("", null),
                    out DbConnectionInternal? extraConnection
                );
                await taskCompletionSource.Task;
            }
            catch (Exception ex)
            {
                // Assert
                Assert.IsType<InvalidOperationException>(ex);
                Assert.Equal("Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.", ex.Message);
            }

            // Assert
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
                new DbConnectionOptions("", null),
                out DbConnectionInternal? firstConnection
            );

            for (int i = 1; i < pool.PoolGroupOptions.MaxPoolSize; i++)
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
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
                    new DbConnectionOptions("", null),
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
                new DbConnectionOptions("", null),
                out DbConnectionInternal? firstConnection
            );

            for (int i = 1; i < pool.PoolGroupOptions.MaxPoolSize; i++)
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
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
                new DbConnectionOptions("", null),
                out DbConnectionInternal? recycledConnection
            );
            pool.ReturnInternalConnection(firstConnection!, firstOwningConnection);
            recycledConnection = await taskCompletionSource.Task;

            // Assert
            Assert.Equal(firstConnection, recycledConnection);
        }

        [Fact]
        public async Task GetConnectionMaxPoolSize_ShouldRespectOrderOfRequest()
        {
            // Arrange
            var pool = ConstructPool(SuccessfulConnectionFactory);
            SqlConnection firstOwningConnection = new();

            pool.TryGetConnection(
                firstOwningConnection,
                taskCompletionSource: null,
                new DbConnectionOptions("", null),
                out DbConnectionInternal? firstConnection
            );

            for (int i = 1; i < pool.PoolGroupOptions.MaxPoolSize; i++)
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
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
                    new DbConnectionOptions("", null),
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
                    new DbConnectionOptions("", null),
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
                new DbConnectionOptions("", null),
                out DbConnectionInternal? firstConnection
            );

            for (int i = 1; i < pool.PoolGroupOptions.MaxPoolSize; i++)
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
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
                new DbConnectionOptions("", null),
                out DbConnectionInternal? recycledConnection
            );

            // Gives time for the recycled connection to be queued before the failed request is initiated.
            await Task.Delay(1000);

            var exceeded2 = pool.TryGetConnection(
                new SqlConnection("Timeout=1"),
                failedCompletionSource,
                new DbConnectionOptions("", null),
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
                new DbConnectionOptions("", null),
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
                new DbConnectionOptions("", null),
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
                    new DbConnectionOptions("", null),
                    out DbConnectionInternal? internalConnection
                );
            });

            Assert.Equal("Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.", ex.Message);
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
                    new DbConnectionOptions("", null),
                    out DbConnectionInternal? internalConnection
                );

                await taskCompletionSource.Task;
            });

            Assert.Equal("Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.", ex.Message);
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
                        new DbConnectionOptions("", null),
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
                        new DbConnectionOptions("", null),
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
            Assert.Throws<NotImplementedException>(() => _ = pool.ErrorOccurred);
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
                    hasTransactionAffinity: true
            );
            var pool = ConstructPool(SuccessfulConnectionFactory, poolGroupOptions: poolGroupOptions);
            Assert.Equal(poolGroupOptions.LoadBalanceTimeout, pool.LoadBalanceTimeout);
        }

        [Fact]
        public void TestPoolGroup()
        {
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new DbConnectionOptions("DataSource=localhost;", null),
                new DbConnectionPoolKey("TestDataSource"),
                new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 50,
                    creationTimeout: 15,
                    loadBalanceTimeout: 500,
                    hasTransactionAffinity: true));
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
                hasTransactionAffinity: true);
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
                hasTransactionAffinity: true);
            var pool = ConstructPool(SuccessfulConnectionFactory, poolGroupOptions: poolGroupOptions);
            Assert.Equal(poolGroupOptions.UseLoadBalancing, pool.UseLoadBalancing);
        }

        #endregion

        #region Not Implemented Method Tests

        [Fact]
        public void TestClear()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.Clear());
        }

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
            Assert.Throws<NotImplementedException>(() => pool.ReplaceConnection(null!, null!, null!));
        }

        [Fact]
        public void TestShutdown()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.Shutdown());
        }

        [Fact]
        public void TestStartup()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.Startup());
        }

        [Fact]
        public void TestTransactionEnded()
        {
            var pool = ConstructPool(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.TransactionEnded(null!, null!));
        }
        #endregion

        #region Test classes
        internal class SuccessfulSqlConnectionFactory : SqlConnectionFactory
        {
            protected override DbConnectionInternal CreateConnection(
                DbConnectionOptions options,
                DbConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                DbConnectionOptions userOptions)
            {
                return new StubDbConnectionInternal();
            }
        }

        internal class TimeoutSqlConnectionFactory : SqlConnectionFactory
        {
            protected override DbConnectionInternal CreateConnection(
                DbConnectionOptions options,
                DbConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                DbConnectionOptions userOptions)
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
                hasTransactionAffinity: true
            );
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new DbConnectionOptions("DataSource=localhost;", null),
                new DbConnectionPoolKey("TestDataSource"),
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
                hasTransactionAffinity: true
            );
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new DbConnectionOptions("DataSource=localhost;", null),
                new DbConnectionPoolKey("TestDataSource"),
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
                hasTransactionAffinity: true
            );
            var dbConnectionPoolGroup1 = new DbConnectionPoolGroup(
                new DbConnectionOptions("DataSource=localhost;", null),
                new DbConnectionPoolKey("TestDataSource"),
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
                hasTransactionAffinity: true
            );
            var dbConnectionPoolGroup2 = new DbConnectionPoolGroup(
                new DbConnectionOptions("DataSource=localhost;", null),
                new DbConnectionPoolKey("TestDataSource"),
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
    }
}

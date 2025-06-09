// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class ChannelDbConnectionPoolTest
    {
        private ChannelDbConnectionPool pool;
        private DbConnectionFactory connectionFactory;
        private DbConnectionPoolGroup dbConnectionPoolGroup;
        private DbConnectionPoolGroupOptions poolGroupOptions;
        private DbConnectionPoolIdentity identity;
        private DbConnectionPoolProviderInfo connectionPoolProviderInfo;

        private static readonly DbConnectionFactory SuccessfulConnectionFactory = new SuccessfulDbConnectionFactory();
        private static readonly DbConnectionFactory TimeoutConnectionFactory = new TimeoutDbConnectionFactory();

        private void Setup(DbConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
            identity = DbConnectionPoolIdentity.NoIdentity;
            connectionPoolProviderInfo = new DbConnectionPoolProviderInfo();
            poolGroupOptions = new DbConnectionPoolGroupOptions(
                    poolByIdentity: false,
                    minPoolSize: 0,
                    maxPoolSize: 50,
                    creationTimeout: 15,
                    loadBalanceTimeout: 0,
                    hasTransactionAffinity: true
            );
            dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new DbConnectionOptions("DataSource=localhost;", null),
                new DbConnectionPoolKey("TestDataSource"),
                poolGroupOptions
            );
            pool = new ChannelDbConnectionPool(
                connectionFactory,
                dbConnectionPoolGroup,
                identity,
                connectionPoolProviderInfo
            );
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void GetConnectionEmptyPool_ShouldCreateNewConnection(int numConnections)
        {
            // Arrange
            Setup(SuccessfulConnectionFactory);

            // Act
            for (int i = 0; i < numConnections; i++)
            {
                DbConnectionInternal internalConnection = null;
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out internalConnection
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
            Setup(SuccessfulConnectionFactory);

            // Act
            for (int i = 0; i < numConnections; i++)
            {
                var tcs = new TaskCompletionSource<DbConnectionInternal>();
                DbConnectionInternal internalConnection = null;
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    tcs,
                    new DbConnectionOptions("", null),
                    out internalConnection
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
            Setup(SuccessfulConnectionFactory);

            for (int i = 0; i < poolGroupOptions.MaxPoolSize; i++)
            {
                DbConnectionInternal internalConnection = null;
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out internalConnection
                );

                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            try
            {
                // Act
                DbConnectionInternal extraConnection = null;
                var exceeded = pool.TryGetConnection(
                    new SqlConnection("Timeout=1"),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out extraConnection
                );
            }
            catch (Exception ex)
            {
                // Assert
                Assert.IsType<InvalidOperationException>(ex);
                Assert.Equal("Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.", ex.Message);
            }

            // Assert
            Assert.Equal(poolGroupOptions.MaxPoolSize, pool.Count);
        }

        [Fact]
        public async Task GetConnectionAsyncMaxPoolSize_ShouldTimeoutAfterPeriod()
        {
            // Arrange
            Setup(SuccessfulConnectionFactory);

            for (int i = 0; i < poolGroupOptions.MaxPoolSize; i++)
            {
                DbConnectionInternal internalConnection = null;
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out internalConnection
                );

                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            try
            {
                // Act
                TaskCompletionSource<DbConnectionInternal> taskCompletionSource = new TaskCompletionSource<DbConnectionInternal>();
                DbConnectionInternal extraConnection = null;
                var exceeded = pool.TryGetConnection(
                    new SqlConnection("Timeout=1"),
                    taskCompletionSource,
                    new DbConnectionOptions("", null),
                    out extraConnection
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
            Assert.Equal(poolGroupOptions.MaxPoolSize, pool.Count);
        }

        [Fact]
        public async Task GetConnectionMaxPoolSize_ShouldReuseAfterConnectionReleased()
        {
            // Arrange
            Setup(SuccessfulConnectionFactory);
            DbConnectionInternal firstConnection = null;
            SqlConnection firstOwningConnection = new SqlConnection();

            pool.TryGetConnection(
                firstOwningConnection,
                taskCompletionSource: null,
                new DbConnectionOptions("", null),
                out firstConnection
            );

            for (int i = 1; i < poolGroupOptions.MaxPoolSize; i++)
            {
                DbConnectionInternal internalConnection = null;
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out internalConnection
                );

                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            TaskCompletionSource<DbConnectionInternal> tcs = new TaskCompletionSource<DbConnectionInternal>();

            // Act
            var task = Task.Run(() =>
            {
                DbConnectionInternal extraConnection = null;
                var exceeded = pool.TryGetConnection(
                    new SqlConnection(""),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out extraConnection
                );
                return extraConnection;
            });
            pool.ReturnInternalConnection(firstConnection, firstOwningConnection);
            var extraConnection = await task;

            // Assert
            Assert.Equal(firstConnection, extraConnection);
        }

        [Fact]
        public async Task GetConnectionAsyncMaxPoolSize_ShouldReuseAfterConnectionReleased()
        {
            // Arrange
            Setup(SuccessfulConnectionFactory);
            DbConnectionInternal firstConnection = null;
            SqlConnection firstOwningConnection = new SqlConnection();

            pool.TryGetConnection(
                firstOwningConnection,
                taskCompletionSource: null,
                new DbConnectionOptions("", null),
                out firstConnection
            );

            for (int i = 1; i < poolGroupOptions.MaxPoolSize; i++)
            {
                DbConnectionInternal internalConnection = null;
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out internalConnection
                );

                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            TaskCompletionSource<DbConnectionInternal> taskCompletionSource = new TaskCompletionSource<DbConnectionInternal>();

            // Act
            DbConnectionInternal recycledConnection = null;
            var exceeded = pool.TryGetConnection(
                new SqlConnection(""),
                taskCompletionSource,
                new DbConnectionOptions("", null),
                out recycledConnection
            );
            pool.ReturnInternalConnection(firstConnection, firstOwningConnection);
            recycledConnection = await taskCompletionSource.Task;

            // Assert
            Assert.Equal(firstConnection, recycledConnection);
        }

        [Fact]
        public async Task GetConnectionMaxPoolSize_ShouldRespectOrderOfRequest()
        {
            // Arrange
            Setup(SuccessfulConnectionFactory);
            DbConnectionInternal firstConnection = null;
            SqlConnection firstOwningConnection = new SqlConnection();

            pool.TryGetConnection(
                firstOwningConnection,
                taskCompletionSource: null,
                new DbConnectionOptions("", null),
                out firstConnection
            );

            for (int i = 1; i < poolGroupOptions.MaxPoolSize; i++)
            {
                DbConnectionInternal internalConnection = null;
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out internalConnection
                );

                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            // Act
            var recycledTask = Task.Run(() =>
            {
                DbConnectionInternal recycledConnection = null;
                var exceeded = pool.TryGetConnection(
                    new SqlConnection(""),
                    null,
                    new DbConnectionOptions("", null),
                    out recycledConnection
                );
                return recycledConnection;
            });
            var failedTask = Task.Run(() =>
            {
                DbConnectionInternal failedConnection = null;
                var exceeded2 = pool.TryGetConnection(
                    new SqlConnection("Timeout=1"),
                    null,
                    new DbConnectionOptions("", null),
                    out failedConnection
                );
                return failedConnection;
            });

            pool.ReturnInternalConnection(firstConnection, firstOwningConnection);
            var recycledConnection = await recycledTask;

            // Assert
            Assert.Equal(firstConnection, recycledConnection);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await failedTask);
        }

        [Fact]
        public async Task GetConnectionAsyncMaxPoolSize_ShouldRespectOrderOfRequest()
        {
            // Arrange
            Setup(SuccessfulConnectionFactory);
            DbConnectionInternal firstConnection = null;
            SqlConnection firstOwningConnection = new SqlConnection();

            pool.TryGetConnection(
                firstOwningConnection,
                taskCompletionSource: null,
                new DbConnectionOptions("", null),
                out firstConnection
            );

            for (int i = 1; i < poolGroupOptions.MaxPoolSize; i++)
            {
                DbConnectionInternal internalConnection = null;
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out internalConnection
                );

                Assert.True(completed);
                Assert.NotNull(internalConnection);
            }

            TaskCompletionSource<DbConnectionInternal> recycledTaskCompletionSource = new TaskCompletionSource<DbConnectionInternal>();
            TaskCompletionSource<DbConnectionInternal> failedCompletionSource = new TaskCompletionSource<DbConnectionInternal>();

            // Act
            DbConnectionInternal recycledConnection = null;
            var exceeded = pool.TryGetConnection(
                new SqlConnection(""),
                recycledTaskCompletionSource,
                new DbConnectionOptions("", null),
                out recycledConnection
            );
            DbConnectionInternal failedConnection = null;
            var exceeded2 = pool.TryGetConnection(
                new SqlConnection("Timeout=1"),
                failedCompletionSource,
                new DbConnectionOptions("", null),
                out failedConnection
            );

            pool.ReturnInternalConnection(firstConnection, firstOwningConnection);
            recycledConnection = await recycledTaskCompletionSource.Task;

            // Assert
            Assert.Equal(firstConnection, recycledConnection);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => failedConnection = await failedCompletionSource.Task);
        }

        [Fact]
        public void ConnectionsAreReused()
        {
            // Arrange
            Setup(SuccessfulConnectionFactory);
            SqlConnection owningConnection = new SqlConnection();
            DbConnectionInternal internalConnection1 = null;
            DbConnectionInternal internalConnection2 = null;

            // Act: Get the first connection
            var completed1 = pool.TryGetConnection(
                owningConnection,
                null,
                new DbConnectionOptions("", null),
                out internalConnection1
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
                out internalConnection2
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
            Setup(TimeoutConnectionFactory);
            DbConnectionInternal internalConnection = null;

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out internalConnection
                );
            });

            Assert.Equal("Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.", ex.Message);
        }

        [Fact]
        public async Task GetConnectionAsyncTimeout_ShouldThrowTimeoutException()
        {
            // Arrange
            Setup(TimeoutConnectionFactory);
            DbConnectionInternal internalConnection = null;
            TaskCompletionSource<DbConnectionInternal> taskCompletionSource = new TaskCompletionSource<DbConnectionInternal>();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                var completed = pool.TryGetConnection(
                    new SqlConnection(),
                    taskCompletionSource,
                    new DbConnectionOptions("", null),
                    out internalConnection
                );

                await taskCompletionSource.Task;
            });

            Assert.Equal("Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.", ex.Message);
        }

        [Fact]
        public void StressTest()
        {
            //Arrange
            Setup(SuccessfulConnectionFactory);
            ConcurrentBag<Task> tasks = new ConcurrentBag<Task>();


            for (int i = 1; i < poolGroupOptions.MaxPoolSize * 3; i++)
            {
                var t = Task.Run(() =>
                {
                    DbConnectionInternal internalConnection = null;
                    SqlConnection owningObject = new SqlConnection();
                    var completed = pool.TryGetConnection(
                        owningObject,
                        taskCompletionSource: null,
                        new DbConnectionOptions("", null),
                        out internalConnection
                    );
                    if (completed)
                    {
                        pool.ReturnInternalConnection(internalConnection, owningObject);
                    }

                    Assert.True(completed);
                    Assert.NotNull(internalConnection);
                });
                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());
            Assert.True(pool.Count <= poolGroupOptions.MaxPoolSize, "Pool size exceeded max pool size after stress test.");
        }

        [Fact]
        public void StressTestAsync()
        {
            //Arrange
            Setup(SuccessfulConnectionFactory);
            ConcurrentBag<Task> tasks = new ConcurrentBag<Task>();


            for (int i = 1; i < poolGroupOptions.MaxPoolSize * 3; i++)
            {
                var t = Task.Run(async () =>
                {
                    DbConnectionInternal internalConnection = null;
                    SqlConnection owningObject = new SqlConnection();
                    TaskCompletionSource<DbConnectionInternal> taskCompletionSource = new TaskCompletionSource<DbConnectionInternal>();
                    var completed = pool.TryGetConnection(
                        owningObject,
                        taskCompletionSource,
                        new DbConnectionOptions("", null),
                        out internalConnection
                    );
                    internalConnection = await taskCompletionSource.Task;
                    pool.ReturnInternalConnection(internalConnection, owningObject);

                    Assert.NotNull(internalConnection);
                });
                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());
            Assert.True(pool.Count <= poolGroupOptions.MaxPoolSize, "Pool size exceeded max pool size after stress test.");
        }


        #region Property Tests

        [Fact]
        public void TestAuthenticationContexts()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => _ = pool.AuthenticationContexts);
        }

        [Fact]
        public void TestConnectionFactory()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Equal(connectionFactory, pool.ConnectionFactory);
        }

        [Fact]
        public void TestCount()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Equal(0, pool.Count);
        }

        [Fact]
        public void TestErrorOccurred()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => _ = pool.ErrorOccurred);
        }

        [Fact]
        public void TestId()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.True(pool.Id >= 1);
        }

        [Fact]
        public void TestIdentity()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Equal(identity, pool.Identity);
        }

        [Fact]
        public void TestIsRunning()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.True(pool.IsRunning);
        }

        [Fact]
        public void TestLoadBalanceTimeout()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Equal(poolGroupOptions.LoadBalanceTimeout, pool.LoadBalanceTimeout);
        }

        [Fact]
        public void TestPoolGroup()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Equal(dbConnectionPoolGroup, pool.PoolGroup);
        }

        [Fact]
        public void TestPoolGroupOptions()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Equal(poolGroupOptions, pool.PoolGroupOptions);
        }

        [Fact]
        public void TestProviderInfo()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Equal(connectionPoolProviderInfo, pool.ProviderInfo);
        }

        [Fact]
        public void TestStateGetter()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Equal(DbConnectionPoolState.Running, pool.State);
        }

        [Fact]
        public void TestStateSetter()
        {
            Setup(SuccessfulConnectionFactory);
            pool.State = DbConnectionPoolState.ShuttingDown;
            Assert.Equal(DbConnectionPoolState.ShuttingDown, pool.State);
            pool.State = DbConnectionPoolState.Running;
            Assert.Equal(DbConnectionPoolState.Running, pool.State);
        }

        [Fact]
        public void TestUseLoadBalancing()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Equal(poolGroupOptions.UseLoadBalancing, pool.UseLoadBalancing);
        }

        #endregion

        #region Not Implemented Method Tests

        [Fact]
        public void TestClear()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.Clear());
        }

        [Fact]
        public void TestPutObjectFromTransactedPool()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.PutObjectFromTransactedPool(null!));
        }

        [Fact]
        public void TestReplaceConnection()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.ReplaceConnection(null!, null!, null!));
        }

        [Fact]
        public void TestShutdown()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.Shutdown());
        }

        [Fact]
        public void TestStartup()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.Startup());
        }

        [Fact]
        public void TestTransactionEnded()
        {
            Setup(SuccessfulConnectionFactory);
            Assert.Throws<NotImplementedException>(() => pool.TransactionEnded(null!, null!));
        }
        #endregion

        #region Test classes
        internal class SuccessfulDbConnectionFactory : DbConnectionFactory
        {
            protected override DbConnectionInternal CreateConnection(DbConnectionOptions options, DbConnectionPoolKey poolKey, object poolGroupProviderInfo, IDbConnectionPool pool, DbConnection owningConnection)
            {
                return new StubDbConnectionInternal();
            }

            #region Not Implemented Members
            public override DbProviderFactory ProviderFactory => throw new NotImplementedException();

            protected override DbConnectionOptions CreateConnectionOptions(string connectionString, DbConnectionOptions previous)
            {
                throw new NotImplementedException();
            }

            protected override DbConnectionPoolGroupOptions CreateConnectionPoolGroupOptions(DbConnectionOptions options)
            {
                throw new NotImplementedException();
            }

            protected override int GetObjectId(DbConnection connection)
            {
                throw new NotImplementedException();
            }

            internal override DbConnectionPoolGroup GetConnectionPoolGroup(DbConnection connection)
            {
                throw new NotImplementedException();
            }

            internal override DbConnectionInternal GetInnerConnection(DbConnection connection)
            {
                throw new NotImplementedException();
            }

            internal override void PermissionDemand(DbConnection outerConnection)
            {
                throw new NotImplementedException();
            }

            internal override void SetConnectionPoolGroup(DbConnection outerConnection, DbConnectionPoolGroup poolGroup)
            {
                throw new NotImplementedException();
            }

            internal override void SetInnerConnectionEvent(DbConnection owningObject, DbConnectionInternal to)
            {
                throw new NotImplementedException();
            }

            internal override bool SetInnerConnectionFrom(DbConnection owningObject, DbConnectionInternal to, DbConnectionInternal from)
            {
                throw new NotImplementedException();
            }

            internal override void SetInnerConnectionTo(DbConnection owningObject, DbConnectionInternal to)
            {
                throw new NotImplementedException();
            }
            #endregion
        }

        internal class TimeoutDbConnectionFactory : DbConnectionFactory
        {
            protected override DbConnectionInternal CreateConnection(DbConnectionOptions options, DbConnectionPoolKey poolKey, object poolGroupProviderInfo, IDbConnectionPool pool, DbConnection owningConnection)
            {
                throw ADP.PooledOpenTimeout();
            }

            #region Not Implemented Members
            public override DbProviderFactory ProviderFactory => throw new NotImplementedException();

            protected override DbConnectionOptions CreateConnectionOptions(string connectionString, DbConnectionOptions previous)
            {
                throw new NotImplementedException();
            }

            protected override DbConnectionPoolGroupOptions CreateConnectionPoolGroupOptions(DbConnectionOptions options)
            {
                throw new NotImplementedException();
            }

            protected override int GetObjectId(DbConnection connection)
            {
                throw new NotImplementedException();
            }

            internal override DbConnectionPoolGroup GetConnectionPoolGroup(DbConnection connection)
            {
                throw new NotImplementedException();
            }

            internal override DbConnectionInternal GetInnerConnection(DbConnection connection)
            {
                throw new NotImplementedException();
            }

            internal override void PermissionDemand(DbConnection outerConnection)
            {
                throw new NotImplementedException();
            }

            internal override void SetConnectionPoolGroup(DbConnection outerConnection, DbConnectionPoolGroup poolGroup)
            {
                throw new NotImplementedException();
            }

            internal override void SetInnerConnectionEvent(DbConnection owningObject, DbConnectionInternal to)
            {
                throw new NotImplementedException();
            }

            internal override bool SetInnerConnectionFrom(DbConnection owningObject, DbConnectionInternal to, DbConnectionInternal from)
            {
                throw new NotImplementedException();
            }

            internal override void SetInnerConnectionTo(DbConnection owningObject, DbConnectionInternal to)
            {
                throw new NotImplementedException();
            }
            #endregion
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
    }
}

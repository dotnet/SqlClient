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

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class ChannelDbConnectionPoolTest
    {
        private readonly ChannelDbConnectionPool pool;
        private readonly DbConnectionPoolGroup dbConnectionPoolGroup;
        private readonly DbConnectionPoolGroupOptions poolGroupOptions;
        private readonly DbConnectionFactory connectionFactory;
        private readonly DbConnectionPoolIdentity identity;
        private readonly DbConnectionPoolProviderInfo connectionPoolProviderInfo;

        public ChannelDbConnectionPoolTest()
        {
            // Use a stubbed connection factory to avoid network code
            connectionFactory = new SuccessfulConnectionFactory();
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
        public void TestGetConnectionFromEmptyPoolSync_ShouldCreateNewConnection(int numConnections)
        {
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
        public async Task TestGetConnectionFromEmptyPoolAsync_ShouldCreateNewConnection(int numConnections)
        {
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

        // Test that requests to get connection from the pool fails when max pool size is reached
       

        #region Property Tests

        [Fact]
        public void TestAuthenticationContexts()
        {
            Assert.Throws<NotImplementedException>(() => _ = pool.AuthenticationContexts);
        }

        [Fact]
        public void TestConnectionFactory()
        {
            Assert.Equal(connectionFactory, pool.ConnectionFactory);
        }

        [Fact]
        public void TestCount()
        {
            Assert.Equal(0, pool.Count);
        }

        [Fact]
        public void TestErrorOccurred()
        {
            Assert.Throws<NotImplementedException>(() => _ = pool.ErrorOccurred);
        }

        [Fact]
        public void TestId()
        {
            Assert.True(pool.Id >= 1);
        }

        [Fact]
        public void TestIdentity()
        {
            Assert.Equal(identity, pool.Identity);
        }

        [Fact]
        public void TestIsRunning()
        {
            Assert.True(pool.IsRunning);
        }

        [Fact]
        public void TestLoadBalanceTimeout()
        {
            Assert.Throws<NotImplementedException>(() => _ = pool.LoadBalanceTimeout);
        }

        [Fact]
        public void TestPoolGroup()
        {
            Assert.Equal(dbConnectionPoolGroup, pool.PoolGroup);
        }

        [Fact]
        public void TestPoolGroupOptions()
        {
            Assert.Equal(poolGroupOptions, pool.PoolGroupOptions);
        }

        [Fact]
        public void TestProviderInfo()
        {
            Assert.Equal(connectionPoolProviderInfo, pool.ProviderInfo);
        }

        [Fact]
        public void TestStateGetter()
        {
            Assert.Equal(DbConnectionPoolState.Running, pool.State);
        }

        [Fact]
        public void TestStateSetter()
        {
            pool.State = DbConnectionPoolState.ShuttingDown;
            Assert.Equal(DbConnectionPoolState.ShuttingDown, pool.State);
            pool.State = DbConnectionPoolState.Running;
            Assert.Equal(DbConnectionPoolState.Running, pool.State);
        }

        [Fact]
        public void TestUseLoadBalancing()
        {
            Assert.Throws<NotImplementedException>(() => _ = pool.UseLoadBalancing);
        }

        #endregion

        #region Not Implemented Method Tests

        [Fact]
        public void TestClear()
        {
            Assert.Throws<NotImplementedException>(() => pool.Clear());
        }

        [Fact]
        public void TestPutObjectFromTransactedPool()
        {
            Assert.Throws<NotImplementedException>(() => pool.PutObjectFromTransactedPool(null!));
        }

        [Fact]
        public void TestReplaceConnection()
        {
            Assert.Throws<NotImplementedException>(() => pool.ReplaceConnection(null!, null!, null!));
        }

        [Fact]
        public void TestReturnInternalConnection()
        {
            Assert.Throws<NotImplementedException>(() => pool.ReturnInternalConnection(null!, null!));
        }

        [Fact]
        public void TestShutdown()
        {
            Assert.Throws<NotImplementedException>(() => pool.Shutdown());
        }

        [Fact]
        public void TestStartup()
        {
            Assert.Throws<NotImplementedException>(() => pool.Startup());
        }

        [Fact]
        public void TestTransactionEnded()
        {
            Assert.Throws<NotImplementedException>(() => pool.TransactionEnded(null!, null!));
        }
        [Fact]
        public void TestGetConnectionFailsWhenMaxPoolSizeReached()
        {
            // Arrange
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
                    //TODO: set timeout to make this faster
                    new SqlConnection(),
                    taskCompletionSource: null,
                    new DbConnectionOptions("", null),
                    out extraConnection
                );
            } catch (Exception ex)
            {
                // Assert
                Assert.IsType<InvalidOperationException>(ex);
                Assert.Equal("Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.", ex.Message);
            }

            // Assert
            Assert.Equal(poolGroupOptions.MaxPoolSize, pool.Count);
        }
        #endregion

        #region Test classes
        internal class SuccessfulConnectionFactory : DbConnectionFactory
        {
            protected override DbConnectionInternal CreateConnection(DbConnectionOptions options, DbConnectionPoolKey poolKey, object poolGroupProviderInfo, IDbConnectionPool pool, DbConnection owningConnection)
            {
                return new SuccessfulDbConnectionInternal();
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

        internal class SuccessfulDbConnectionInternal : DbConnectionInternal
        {
            #region Not Implemented Members
            public override string ServerVersion => throw new NotImplementedException();

            public override DbTransaction BeginTransaction(System.Data.IsolationLevel il)
            {
                throw new NotImplementedException();
            }

            public override void EnlistTransaction(Transaction transaction)
            {
                throw new NotImplementedException();
            }

            protected override void Activate(Transaction transaction)
            {
                throw new NotImplementedException();
            }

            protected override void Deactivate()
            {
                throw new NotImplementedException();
            }
            #endregion
        }

        internal class TimeoutConnectionFactory : DbConnectionFactory
        {
            protected override DbConnectionInternal CreateConnection(DbConnectionOptions options, DbConnectionPoolKey poolKey, object poolGroupProviderInfo, IDbConnectionPool pool, DbConnection owningConnection)
            {
                return new SuccessfulDbConnectionInternal();
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
        #endregion
    }
}

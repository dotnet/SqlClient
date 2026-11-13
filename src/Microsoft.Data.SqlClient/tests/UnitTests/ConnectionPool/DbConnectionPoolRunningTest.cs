// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Pins the running/not-running contract shared by both pool implementations.
    /// </summary>
    public class DbConnectionPoolRunningTest
    {
        /// <summary>
        /// Construction enables acquisition; startup and clear must not retire a running pool.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ConstructionStartupAndClear_KeepPoolRunning(bool useChannelPool)
        {
            IDbConnectionPool pool = CreatePool(useChannelPool);
            try
            {
                Assert.True(pool.IsRunning);
                pool.Startup();
                Assert.True(pool.IsRunning);
                pool.Clear();
                Assert.True(pool.IsRunning);
            }
            finally
            {
                pool.Shutdown();
                pool.Clear();
            }
        }

        /// <summary>
        /// Shutdown before maintenance starts must be terminal, including after repeated shutdown,
        /// clear, and startup. Both acquisition paths must leave the caller's completion source untouched.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void ShutdownBeforeStartup_RemainsNotRunning(bool useChannelPool, bool async)
        {
            IDbConnectionPool pool = CreatePool(useChannelPool);
            using var owner = new SqlConnection();
            var completion = async ? new TaskCompletionSource<DbConnectionInternal>() : null;
            try
            {
                pool.Shutdown();
                Assert.False(pool.IsRunning);
                pool.Shutdown();
                Assert.False(pool.IsRunning);
                pool.Clear();
                Assert.False(pool.IsRunning);
                pool.Startup();
                Assert.False(pool.IsRunning);

                // A stopped pool must return without waiting, so an already-expired timer suffices.
                Assert.True(pool.TryGetConnection(
                    owner, completion, TimeoutTimer.StartExpired(), out DbConnectionInternal? connection));
                Assert.Null(connection);
                Assert.Equal(0, pool.Count);
                Assert.Equal(0, pool.IdleCount);
                if (completion is not null)
                {
                    Assert.False(completion.Task.IsCompleted);
                }
                if (pool is WaitHandleDbConnectionPool waitHandlePool)
                {
                    Assert.Null(waitHandlePool._cleanupTimer);
                }
            }
            finally
            {
                pool.Shutdown();
                pool.Clear();
            }
        }

        /// <summary>
        /// A sync caller that fetched a pool before pruning retired it must be redirected with a
        /// null connection, without starting a physical open on the retired pool.
        /// </summary>
        /// <param name="useChannelPool">Selects the channel rather than wait-handle implementation.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TryGetConnection_SyncAfterPrune_ReturnsNullWithoutOpening(bool useChannelPool)
        {
            // Arrange
            IDbConnectionPool pool = CreatePool(useChannelPool);
            using var owner = new SqlConnection();
            Assert.True(pool.TryPrune());
            try
            {
                // Act
                bool completed = pool.TryGetConnection(
                    owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(10)),
                    out DbConnectionInternal? connection);

                // Assert
                Assert.True(completed);
                Assert.Null(connection);
                Assert.False(pool.IsRunning);
                Assert.Equal(0, pool.Count);
            }
            finally
            {
                // Cleanup
                pool.Shutdown();
                pool.Clear();
            }
        }

        /// <summary>
        /// An async caller that fetched a pool before pruning retired it must be redirected with a
        /// null connection, without queuing an open on the retired pool or completing its task.
        /// </summary>
        /// <param name="useChannelPool">Selects the channel rather than wait-handle implementation.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TryGetConnection_AsyncAfterPrune_ReturnsNullWithoutQueuing(bool useChannelPool)
        {
            // Arrange
            IDbConnectionPool pool = CreatePool(useChannelPool);
            using var owner = new SqlConnection();
            var completion = new TaskCompletionSource<DbConnectionInternal>();
            Assert.True(pool.TryPrune());
            try
            {
                // Act
                bool completed = pool.TryGetConnection(
                    owner, completion, TimeoutTimer.StartNew(TimeSpan.FromSeconds(10)),
                    out DbConnectionInternal? connection);

                // Assert
                Assert.True(completed);
                Assert.Null(connection);
                Assert.False(completion.Task.IsCompleted);
                Assert.False(pool.IsRunning);
                Assert.Equal(0, pool.Count);
            }
            finally
            {
                // Cleanup
                pool.Shutdown();
                pool.Clear();
            }
        }

        /// <summary>
        /// A replacement request on a pool retired by pruning must return null, so the factory
        /// selects a live pool instead of creating on one already queued for release.
        /// </summary>
        /// <param name="useChannelPool">Selects the channel rather than wait-handle implementation.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReplaceConnection_AfterPrune_ReturnsNull(bool useChannelPool)
        {
            // Arrange
            IDbConnectionPool pool = CreatePool(useChannelPool);
            using var owner = new SqlConnection();
            var oldConnection = new WaitHandleDbConnectionPoolTransactionTest.MockDbConnectionInternal();
            Assert.True(pool.TryPrune());
            try
            {
                // Act
                DbConnectionInternal? connection = pool.ReplaceConnection(
                    owner, oldConnection, TimeoutTimer.StartNew(TimeSpan.FromSeconds(10)));

                // Assert
                Assert.Null(connection);
                Assert.False(pool.IsRunning);
                Assert.Equal(0, pool.Count);
            }
            finally
            {
                // Cleanup
                oldConnection.Dispose();
                pool.Shutdown();
                pool.Clear();
            }
        }

        /// <summary>Creates a pool without starting maintenance or requiring a SQL Server.</summary>
        /// <param name="useChannelPool">Selects the channel rather than wait-handle implementation.</param>
        /// <returns>A newly constructed pool with no background warmup or idle pruning.</returns>
        private static IDbConnectionPool CreatePool(bool useChannelPool)
        {
            var options = new DbConnectionPoolGroupOptions(
                poolByIdentity: false, minPoolSize: 0, maxPoolSize: 5, creationTimeout: 15000,
                loadBalanceTimeout: 0, hasTransactionAffinity: true, idleTimeout: 0);
            var group = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null,
                    accessTokenCallback: null, sspiContextProvider: null),
                options);
            var factory = new WaitHandleDbConnectionPoolTransactionTest.MockSqlConnectionFactory();
            return useChannelPool
                ? new ChannelDbConnectionPool(factory, group, DbConnectionPoolIdentity.NoIdentity, new DbConnectionPoolProviderInfo())
                : new WaitHandleDbConnectionPool(factory, group, DbConnectionPoolIdentity.NoIdentity, new DbConnectionPoolProviderInfo());
        }
    }
}

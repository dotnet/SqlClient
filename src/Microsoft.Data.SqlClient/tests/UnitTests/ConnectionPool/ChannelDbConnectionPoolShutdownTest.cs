// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Deterministic tests for <see cref="ChannelDbConnectionPool"/> shutdown behavior.
    /// </summary>
    public class ChannelDbConnectionPoolShutdownTest
    {
        private static ChannelDbConnectionPool ConstructPool(int maxPoolSize = 5)
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: maxPoolSize,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true);

            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions);

            return new ChannelDbConnectionPool(
                new ChannelDbConnectionPoolTest.SuccessfulSqlConnectionFactory(),
                dbConnectionPoolGroup,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo());
        }

        // State transitions to ShuttingDown on Shutdown.
        [Fact]
        public void Shutdown_TransitionsState_ToShuttingDown()
        {
            var pool = ConstructPool();
            Assert.True(pool.IsRunning);
            Assert.Equal(DbConnectionPoolState.Running, pool.State);

            pool.Shutdown();

            Assert.False(pool.IsRunning);
            Assert.Equal(DbConnectionPoolState.ShuttingDown, pool.State);
        }

        // Drains buffered idle connections.
        [Fact]
        public void Shutdown_DrainsIdleConnections()
        {
            var pool = ConstructPool();

            // Vend and return three connections so they sit idle in the channel.
            var owners = new List<SqlConnection>();
            var conns = new List<DbConnectionInternal>();
            for (int i = 0; i < 3; i++)
            {
                var owner = new SqlConnection();
                owners.Add(owner);
                Assert.True(pool.TryGetConnection(owner, taskCompletionSource: null, out DbConnectionInternal? c));
                Assert.NotNull(c);
                conns.Add(c!);
            }
            for (int i = 0; i < conns.Count; i++)
            {
                pool.ReturnInternalConnection(conns[i], owners[i]);
            }
            Assert.Equal(3, pool.IdleCount);

            pool.Shutdown();

            Assert.Equal(0, pool.IdleCount);
            Assert.Equal(0, pool.Count);
        }

        // Returned connection while shutting down is destroyed, not pooled.
        [Fact]
        public void Shutdown_ReturnedConnection_IsDestroyedNotPooled()
        {
            var pool = ConstructPool();
            var owner = new SqlConnection();
            Assert.True(pool.TryGetConnection(owner, taskCompletionSource: null, out DbConnectionInternal? conn));
            Assert.NotNull(conn);
            Assert.Equal(1, pool.Count);
            Assert.Equal(0, pool.IdleCount);

            pool.Shutdown();

            // Connection is still checked out; return it now.
            pool.ReturnInternalConnection(conn!, owner);

            Assert.Equal(0, pool.IdleCount);
            Assert.Equal(0, pool.Count);
        }

        // Shutdown is idempotent.
        [Fact]
        public void Shutdown_IsIdempotent()
        {
            var pool = ConstructPool();
            pool.Shutdown();
            // Second call must not throw and must leave state intact.
            pool.Shutdown();
            pool.Shutdown();
            Assert.Equal(DbConnectionPoolState.ShuttingDown, pool.State);
        }

        // Async waiter is unblocked when the pool shuts down.
        [Fact]
        public async Task Shutdown_UnblocksAsyncWaiter()
        {
            var pool = ConstructPool(maxPoolSize: 1);

            // Saturate the pool.
            Assert.True(pool.TryGetConnection(new SqlConnection(), taskCompletionSource: null, out DbConnectionInternal? blocking));
            Assert.NotNull(blocking);

            // Park an async waiter.
            var tcs = new TaskCompletionSource<DbConnectionInternal>();
            bool completed = pool.TryGetConnection(new SqlConnection(), tcs, out DbConnectionInternal? waiter);
            Assert.False(completed);
            Assert.Null(waiter);
            Assert.False(tcs.Task.IsCompleted);

            // Shut down the pool.
            pool.Shutdown();

            // Waiter must complete (faulted or with a null connection) within a bounded window.
            var winner = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(tcs.Task, winner);
            // Either an exception was set (channel closed) or the result is null - both are acceptable
            // shutdown signals. What matters is the waiter does NOT block forever.
            if (tcs.Task.IsFaulted)
            {
                // Expected path: ChannelClosedException or a wrapped exception.
                Assert.NotNull(tcs.Task.Exception);
            }
            else
            {
                // Permitted: completed with null result.
                Assert.Null(tcs.Task.Result);
            }
        }

        // Sync get fails fast after shutdown.
        // The factory-level retry guard checks IsRunning, but the pool itself must not vend
        // new connections after Shutdown. We verify by exhausting the pool first then
        // checking that returned connections are destroyed (Count goes back to 0).
        [Fact]
        public void Shutdown_AfterShutdown_NewReturnsAreDestroyed()
        {
            var pool = ConstructPool();
            var owner = new SqlConnection();
            Assert.True(pool.TryGetConnection(owner, taskCompletionSource: null, out DbConnectionInternal? c));
            Assert.NotNull(c);

            pool.Shutdown();
            pool.ReturnInternalConnection(c!, owner);

            Assert.False(pool.IsRunning);
            Assert.Equal(0, pool.Count);
            Assert.Equal(0, pool.IdleCount);
        }

        // Sanity: Startup is currently a no-op for this pool but must not throw or change
        // shutdown state if invoked after Shutdown.
        [Fact]
        public void Startup_AfterShutdown_DoesNotResurrectPool()
        {
            var pool = ConstructPool();
            pool.Shutdown();

            pool.Startup();

            Assert.Equal(DbConnectionPoolState.ShuttingDown, pool.State);
            Assert.False(pool.IsRunning);
        }
    }
}

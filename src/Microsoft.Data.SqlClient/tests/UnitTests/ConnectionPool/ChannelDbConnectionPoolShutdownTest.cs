// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
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
                hasTransactionAffinity: true,
                idleTimeout: 0);

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

        /// <summary>Shutdown permanently stops the pool from accepting new requests.</summary>
        [Fact]
        public void Shutdown_StopsRunning()
        {
            var pool = ConstructPool();
            Assert.True(pool.IsRunning);

            pool.Shutdown();

            Assert.False(pool.IsRunning);
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
                Assert.True(pool.TryGetConnection(owner, taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? c));
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
            Assert.True(pool.TryGetConnection(owner, taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn));
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
            Assert.False(pool.IsRunning);
        }

        /// <summary>
        /// Shutdown releases both sync and async channel waiters with the pool shutdown error,
        /// rather than leaving them blocked until their acquisition timeout expires.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Shutdown_UnblocksWaiter(bool async)
        {
            using var pool = ConstructPool(maxPoolSize: 1);
            using var blockingOwner = new SqlConnection();
            using var waitingOwner = new SqlConnection();

            // Saturate the pool.
            Assert.True(pool.TryGetConnection(blockingOwner, taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? blocking));
            Assert.NotNull(blocking);

            var tcs = new TaskCompletionSource<DbConnectionInternal>();
            Task pending;
            if (async)
            {
                Assert.False(pool.TryGetConnection(waitingOwner, tcs, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? waiter));
                Assert.Null(waiter);
                pending = tcs.Task;
            }
            else
            {
                pending = Task.Factory.StartNew(
                    () => pool.TryGetConnection(waitingOwner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _),
                    CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            try
            {
                Assert.True(SpinWait.SpinUntil(() => pool.Reclaimer.ParkedWaiters == 1, TimeSpan.FromSeconds(5)));
                Assert.False(pending.IsCompleted);

                pool.Shutdown();

                Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(5))));
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
                Assert.Equal(StringsHelper.GetString(Strings.SQL_ConnectionPoolShutDown), error.Message);
                Assert.False(pool.IsRunning);
                Assert.Equal(0, pool.Reclaimer.ParkedWaiters);
            }
            finally
            {
                pool.Shutdown();
                pool.ReturnInternalConnection(blocking!, blockingOwner);
                Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(20))));
                if (pending.IsFaulted)
                {
                    _ = pending.Exception;
                }
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
            Assert.True(pool.TryGetConnection(owner, taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? c));
            Assert.NotNull(c);

            pool.Shutdown();
            pool.ReturnInternalConnection(c!, owner);

            Assert.False(pool.IsRunning);
            Assert.Equal(0, pool.Count);
            Assert.Equal(0, pool.IdleCount);
        }

        /// <summary>Startup must not restart background warmup on a retired pool.</summary>
        [Fact]
        public void Startup_AfterShutdown_DoesNotResurrectPool()
        {
            var pool = ConstructPool();
            pool.Shutdown();

            pool.Startup();

            Assert.False(pool.IsRunning);
        }

        // After Shutdown, a synchronous TryGetConnection must short-circuit and return
        // (true, null) rather than entering the channel path and opening a fresh physical
        // connection that would be immediately destroyed on return.
        [Fact]
        public void TryGetConnection_AfterShutdown_Sync_ShortCircuits()
        {
            var pool = ConstructPool();
            pool.Shutdown();

            int countBefore = pool.Count;
            bool completed = pool.TryGetConnection(
                new SqlConnection(),
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(5)),
                out DbConnectionInternal? conn);

            Assert.True(completed, "Sync TryGetConnection on a shut-down pool should return true to signal completion.");
            Assert.Null(conn);
            Assert.Equal(countBefore, pool.Count);
        }

        // Same contract for the async (TaskCompletionSource) path: a TryGetConnection call
        // issued after Shutdown must short-circuit without opening a new connection.
        [Fact]
        public void TryGetConnection_AfterShutdown_Async_ShortCircuits()
        {
            var pool = ConstructPool();
            pool.Shutdown();

            int countBefore = pool.Count;
            var tcs = new TaskCompletionSource<DbConnectionInternal>();
            bool completed = pool.TryGetConnection(
                new SqlConnection(),
                tcs,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(5)),
                out DbConnectionInternal? conn);

            // Match WaitHandleDbConnectionPool: short-circuit returns (true, null) regardless of TCS.
            Assert.True(completed);
            Assert.Null(conn);
            Assert.Equal(countBefore, pool.Count);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Deterministic tests for <see cref="WaitHandleDbConnectionPool"/> shutdown behavior.
    /// </summary>
    public class WaitHandleDbConnectionPoolShutdownTest
    {
        private static WaitHandleDbConnectionPool CreatePool(int maxPoolSize = 5)
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: maxPoolSize,
                creationTimeout: 15000,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true);

            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions);

            var pool = new WaitHandleDbConnectionPool(
                new WaitHandleDbConnectionPoolTransactionTest.MockSqlConnectionFactory(),
                dbConnectionPoolGroup,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo());
            pool.Startup();
            return pool;
        }

        // State transitions to ShuttingDown on Shutdown.
        [Fact]
        public void Shutdown_TransitionsState_ToShuttingDown()
        {
            var pool = CreatePool();
            Assert.True(pool.IsRunning);

            pool.Shutdown();

            Assert.False(pool.IsRunning);
            Assert.Equal(DbConnectionPoolState.ShuttingDown, pool.State);
        }

        // Cleanup timer is disposed.
        [Fact]
        public void Shutdown_DisposesCleanupTimer()
        {
            var pool = CreatePool();
            Assert.NotNull(pool._cleanupTimer);

            pool.Shutdown();

            Assert.Null(pool._cleanupTimer);
        }

        // Error timer is disposed when present.
        [Fact]
        public void Shutdown_DisposesErrorTimer_WhenPresent()
        {
            var pool = CreatePool();
            // Inject a real Timer into _errorTimer to mimic an error-state pool.
            pool._errorTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);

            pool.Shutdown();

            Assert.Null(pool._errorTimer);
        }

        // Drains idle stacks.
        [Fact]
        public void Shutdown_DrainsIdleStacks()
        {
            var pool = CreatePool();

            // Vend a few connections then return them so they sit in _stackNew.
            var owner1 = new SqlConnection();
            var owner2 = new SqlConnection();
            pool.TryGetConnection(owner1, taskCompletionSource: null, out DbConnectionInternal? c1);
            pool.TryGetConnection(owner2, taskCompletionSource: null, out DbConnectionInternal? c2);
            Assert.NotNull(c1);
            Assert.NotNull(c2);
            pool.ReturnInternalConnection(c1!, owner1);
            pool.ReturnInternalConnection(c2!, owner2);

            Assert.Equal(2, pool.IdleCount);
            Assert.Equal(2, pool.Count);

            pool.Shutdown();

            Assert.Equal(0, pool.IdleCount);
            Assert.Equal(0, pool.Count);
        }

        // Shutdown is idempotent.
        [Fact]
        public void Shutdown_IsIdempotent()
        {
            var pool = CreatePool();
            pool.Shutdown();
            pool.Shutdown();
            pool.Shutdown();
            Assert.Equal(DbConnectionPoolState.ShuttingDown, pool.State);
        }

        // Cleanup callback after shutdown is a no-op.
        [Fact]
        public void CleanupCallback_AfterShutdown_IsNoOp()
        {
            var pool = CreatePool();
            pool.Shutdown();

            // Invoke the callback directly. Must not throw and must not re-arm any pool
            // create requests.
            var ex = Record.Exception(() => pool.CleanupCallback(state: null));
            Assert.Null(ex);
            Assert.Equal(DbConnectionPoolState.ShuttingDown, pool.State);
        }

        // Error callback after shutdown is a no-op.
        [Fact]
        public void ErrorCallback_AfterShutdown_IsNoOp()
        {
            var pool = CreatePool();
            pool.Shutdown();

            var ex = Record.Exception(() => pool.ErrorCallback(state: null));
            Assert.Null(ex);
        }

        // Sync caller arriving after shutdown gets a null connection (factory will
        // see this and return up the retry chain). The pool's TryGetConnection short-circuits
        // on State != Running.
        [Fact]
        public void TryGetConnection_AfterShutdown_ReturnsNullWithoutBlocking()
        {
            var pool = CreatePool();
            pool.Shutdown();

            bool completed = pool.TryGetConnection(
                new SqlConnection(),
                taskCompletionSource: null,
                out DbConnectionInternal? conn);

            // TryGetConnection returns true with a null connection when State != Running.
            Assert.True(completed);
            Assert.Null(conn);
        }

        // Shutdown wakes up a thread parked in WaitHandle.WaitAny.
        [Fact]
        public void Shutdown_UnblocksSyncWaiter()
        {
            var pool = CreatePool(maxPoolSize: 1);

            // Saturate the pool.
            var owner = new SqlConnection();
            Assert.True(pool.TryGetConnection(owner, taskCompletionSource: null, out DbConnectionInternal? blocking));
            Assert.NotNull(blocking);

            // Park a sync waiter on a worker thread with a long creation timeout.
            DbConnectionInternal? waiterResult = null;
            bool waiterCompleted = false;
            Exception? waiterEx = null;

            var t = new Thread(() =>
            {
                try
                {
                    waiterCompleted = pool.TryGetConnection(
                        new SqlConnection(),
                        taskCompletionSource: null,
                        out waiterResult);
                }
                catch (Exception ex)
                {
                    waiterEx = ex;
                }
            })
            { IsBackground = true };
            t.Start();

            // Wait deterministically until the worker has incremented _waitCount, which
            // happens immediately before it enters WaitHandle.WaitAny. Polling avoids the
            // CI-flakiness of a fixed Thread.Sleep on slow agents.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline && pool._waitCount < 1)
            {
                Thread.Yield();
            }
            Assert.True(pool._waitCount >= 1, "Waiter did not park within 5s.");
            Assert.True(t.IsAlive, "Waiter should be parked, but thread already exited.");

            pool.Shutdown();

            Assert.True(t.Join(TimeSpan.FromSeconds(5)), "Waiter did not unblock within 5s of Shutdown.");
            // Acceptable outcomes: either returned false/null (timed out / abandoned) or
            // returned true/null (state-check short-circuit). Either way, it must NOT block
            // forever, and it must NOT vend a real connection from a shut-down pool.
            Assert.Null(waiterResult);
            Assert.Null(waiterEx);
            // Suppress unused warning - presence of waiterCompleted just documents the contract.
            _ = waiterCompleted;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
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
        private static WaitHandleDbConnectionPool CreatePool(int maxPoolSize = 5, SqlConnectionFactory? factory = null)
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: maxPoolSize,
                creationTimeout: 15000,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: 0);

            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions);

            var pool = new WaitHandleDbConnectionPool(
                factory ?? new WaitHandleDbConnectionPoolTransactionTest.MockSqlConnectionFactory(),
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

        /// <summary>
        /// Leaves idle connections for the factory's explicit or deferred Clear call.
        /// </summary>
        [Fact]
        public void Shutdown_LeavesIdleConnectionsUntilClear()
        {
            // Arrange
            var pool = CreatePool();

            // An empty pool would pass even if Shutdown still called Clear internally.
            // Keep idle inventory so the two lifecycle operations have distinguishable effects.
            var owner1 = new SqlConnection();
            var owner2 = new SqlConnection();
            pool.TryGetConnection(owner1, taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? c1);
            pool.TryGetConnection(owner2, taskCompletionSource: null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? c2);
            Assert.NotNull(c1);
            Assert.NotNull(c2);
            pool.ReturnInternalConnection(c1!, owner1);
            pool.ReturnInternalConnection(c2!, owner2);

            Assert.Equal(2, pool.IdleCount);
            Assert.Equal(2, pool.Count);

            try
            {
                // Act
                pool.Shutdown();

                // Assert: unchanged inventory and poolability detect both effects of an
                // unintended Clear: draining idle objects and marking them non-poolable.
                Assert.False(pool.IsRunning);
                Assert.Equal(2, pool.IdleCount);
                Assert.Equal(2, pool.Count);
                Assert.True(c1!.CanBePooled);
                Assert.True(c2!.CanBePooled);

                // Act: perform the Clear that the factory owns, separately from shutdown.
                pool.Clear();

                // Assert
                Assert.Equal(0, pool.IdleCount);
                Assert.Equal(0, pool.Count);
            }
            finally
            {
                // Cleanup
                pool.Shutdown();
                pool.Clear();
            }
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
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? conn);

            // TryGetConnection returns true with a null connection when State != Running.
            Assert.True(completed);
            Assert.Null(conn);
        }

        // Async overload (taskCompletionSource != null) called after Shutdown must take the
        // (true, null) short-circuit instead of enqueueing a PendingGetConnection and spinning
        // up a WaitForPendingOpen background loop. The TCS must be left untouched so the caller
        // surfaces a deterministic shutdown signal rather than an eventual PooledOpenTimeout.
        [Fact]
        public void TryGetConnection_Async_AfterShutdown_ShortCircuits_NoPendingOpenScheduled()
        {
            var pool = CreatePool();
            pool.Shutdown();

            var tcs = new TaskCompletionSource<DbConnectionInternal>();
            bool completed = pool.TryGetConnection(
                new SqlConnection(),
                tcs,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? conn);

            Assert.True(completed);
            Assert.Null(conn);
            Assert.False(tcs.Task.IsCompleted);
            Assert.Equal(0, Volatile.Read(ref pool._waitCount));
        }

        /// <summary>
        /// Starts with no idle connections. Pauses the first physical connection creation
        /// while it holds the creation semaphore, then admits a second request that waits
        /// for that semaphore. Shuts down the pool before releasing the first creation.
        /// Both requests create their own connections on the retired pool, and both
        /// connections are destroyed when returned. In the cancellation case, the worker
        /// returns and destroys the second connection instead of delivering it to the caller.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task Shutdown_InFlightRequest_CompletesOnRetiredPool(bool async, bool cancel)
        {
            // Arrange
            // Initiate a request to the pool. It will be blocked by the gated connection factory.
            using var factory = new GatedConnectionFactory();
            var pool = CreatePool(maxPoolSize: 2, factory: factory);
            using var firstOwner = new SqlConnection();
            using var pendingOwner = new SqlConnection();
            var completion = new TaskCompletionSource<DbConnectionInternal>();
            Task<DbConnectionInternal?> first = Acquire(pool, firstOwner, completion: null);
            Task<DbConnectionInternal?>? pending = null;

            // Make sure the first request is blocked in the connection factory. Then, initiate a second request.
            // The second request will be blocked on the create semaphore in the pool.
            Assert.True(factory.Entered.Wait(TimeSpan.FromSeconds(10)));
            pending = Acquire(pool, pendingOwner, async ? completion : null);
            // Make sure the second request is also blocked.
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref pool._waitCount) == 2, TimeSpan.FromSeconds(10)));
            Assert.Equal(1, factory.CreateCount);
            Assert.False(first.IsCompleted);
            Assert.False(pending.IsCompleted);

            // Act
            // Now, shut down the pool. New requests will no longer be accepted, but in-flight requests should proceed.
            pool.Shutdown();
            if (cancel)
            {
                completion.SetCanceled();
            }

            // Unblock the first request, allowing both to proceed, in turn.
            factory.Release.Set();

            // Assert
            // Wait for the first request to complete
            Assert.False(pool.IsRunning);
            Assert.Same(first, await Task.WhenAny(first, Task.Delay(TimeSpan.FromSeconds(10))));
            DbConnectionInternal? firstConnection = await first;
            Assert.NotNull(firstConnection);
            Assert.Same(pool, firstConnection.Pool);

            // Wait for the second request to complete
            Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(10))));
            if (cancel)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                Assert.True(SpinWait.SpinUntil(() => pool.Count == 1 && Volatile.Read(ref pool._waitCount) == 0, TimeSpan.FromSeconds(10)));
            }
            else
            {
                DbConnectionInternal? pendingConnection = await pending;
                Assert.NotNull(pendingConnection);
                Assert.Same(pool, pendingConnection.Pool);
                Assert.Equal(0, Volatile.Read(ref pool._waitCount));
            }

            // Assert that both requests created new connections
            Assert.Equal(2, factory.CreateCount);

            // Cleanup
            factory.Release.Set();
            pool.Shutdown();
            await ReturnWhenCompleted(pool, firstOwner, first);
            if (pending is not null)
            {
                await ReturnWhenCompleted(pool, pendingOwner, pending);
            }

            // Assert: returned connections were destroyed rather than pooled.
            Assert.Equal(0, pool.IdleCount);
            Assert.Equal(0, pool.Count);
        }

        /// <summary>Starts a sync acquisition on a dedicated thread or queues an async acquisition.</summary>
        private static Task<DbConnectionInternal?> Acquire(WaitHandleDbConnectionPool pool, SqlConnection owner,
            TaskCompletionSource<DbConnectionInternal>? completion)
        {
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(15));
            if (completion is null)
            {
                return Task.Factory.StartNew(() =>
                {
                    Assert.True(pool.TryGetConnection(owner, null, timer, out DbConnectionInternal connection));
                    return (DbConnectionInternal?)connection;
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            // A false return here means async acquisition was queued, not that it failed.
            // Its eventual result is delivered through completion.Task.
            Assert.False(pool.TryGetConnection(owner, completion, timer, out DbConnectionInternal pending));
            Assert.Null(pending);
            return completion.Task!;
        }

        /// <summary>Drains test work and returns successful acquisitions even when an assertion failed.</summary>
        private static async Task ReturnWhenCompleted(WaitHandleDbConnectionPool pool, SqlConnection owner, Task<DbConnectionInternal?> task)
        {
            Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20))));
            if (task.Status == TaskStatus.RanToCompletion && task.Result is { } connection)
            {
                pool.ReturnInternalConnection(connection, owner);
            }
            else if (task.IsFaulted)
            {
                // Observe failures during assertion cleanup without replacing the original failure.
                _ = task.Exception;
            }
        }

        /// <summary>Holds the first physical creation so a second acquisition waits on its semaphore.</summary>
        private sealed class GatedConnectionFactory : WaitHandleDbConnectionPoolTransactionTest.MockSqlConnectionFactory, IDisposable
        {
            internal readonly ManualResetEventSlim Entered = new();
            internal readonly ManualResetEventSlim Release = new();
            private int _calls;

            internal int CreateCount => Volatile.Read(ref _calls);

            protected override DbConnectionInternal CreateConnection(SqlConnectionOptions options, ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo, IDbConnectionPool pool, DbConnection owningConnection, TimeoutTimer timeout)
            {
                if (Interlocked.Increment(ref _calls) == 1)
                {
                    Entered.Set();
                    Assert.True(Release.Wait(TimeSpan.FromSeconds(15)), "Physical creation was not released.");
                }
                return base.CreateConnection(options, poolKey, poolGroupProviderInfo, pool, owningConnection, timeout);
            }

            public void Dispose()
            {
                Entered.Dispose();
                Release.Dispose();
            }
        }

        // Startup() must be a no-op when the pool has already been shut down. Without the
        // guard it would create a fresh _cleanupTimer and queue a PoolCreateRequest against
        // a pool that will never accept connections back.
        [Fact]
        public void Startup_AfterShutdown_DoesNotResurrectPool()
        {
            var pool = CreatePool();
            pool.Shutdown();
            Assert.Null(pool._cleanupTimer);
            Assert.Equal(DbConnectionPoolState.ShuttingDown, pool.State);

            pool.Startup();

            Assert.Equal(DbConnectionPoolState.ShuttingDown, pool.State);
            Assert.False(pool.IsRunning);
            // No new cleanup timer must have been scheduled against a shut-down pool.
            Assert.Null(pool._cleanupTimer);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Unit tests for background pool warmup and replenishment in
    /// <see cref="ChannelDbConnectionPool"/>. See <c>specs/003-pool-warmup/spec.md</c>.
    /// </summary>
    public class ChannelDbConnectionPoolWarmupTest
    {
        private const int DefaultTimeoutMs = 5000;

        #region Helpers

        private static ChannelDbConnectionPool ConstructPool(
            SqlConnectionFactory connectionFactory,
            int minPoolSize = 0,
            int maxPoolSize = 50,
            int idleTimeout = 0,
            int loadBalanceTimeout = 0,
            ConcurrencyLimiter? connectionCreationRateLimiter = null)
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: minPoolSize,
                maxPoolSize: maxPoolSize,
                creationTimeout: 15,
                loadBalanceTimeout: loadBalanceTimeout,
                hasTransactionAffinity: true,
                idleTimeout: idleTimeout);

            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions);

            return new ChannelDbConnectionPool(
                connectionFactory,
                dbConnectionPoolGroup,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo(),
                connectionCreationRateLimiter);
        }

        /// <summary>
        /// Spins until <paramref name="condition"/> is true or the timeout elapses. Warmup runs on
        /// background tasks, so tests observe its effects by polling pool counters.
        /// </summary>
        private static bool WaitFor(Func<bool> condition, int timeoutMs = DefaultTimeoutMs)
            => SpinWait.SpinUntil(condition, timeoutMs);

        /// <summary>
        /// Checks out a single connection from the pool synchronously.
        /// </summary>
        private static DbConnectionInternal CheckOut(ChannelDbConnectionPool pool)
        {
            bool completed = pool.TryGetConnection(
                new SqlConnection(),
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? connection);
            Assert.True(completed);
            Assert.NotNull(connection);
            return connection!;
        }

        #endregion

        #region Story 1 - Background warmup on pool creation

        // Warmup is a no-op when MinPoolSize == 0: no connections are ever created in the background.
        [Fact]
        public void Startup_MinPoolSizeZero_DoesNotWarmUp()
        {
            var factory = new ChannelDbConnectionPoolTest.CountingSuccessfulConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 0, maxPoolSize: 10);

            pool.Startup();

            // Give any (erroneous) background warmup a chance to run before asserting it did not.
            Assert.False(
                WaitFor(() => factory.CreateCount > 0, timeoutMs: 500),
                "Warmup created a connection even though MinPoolSize is 0.");
            Assert.Equal(0, pool.Count);
        }

        // Warmup pre-creates connections up to MinPoolSize for various sizes (1 and N).
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(10)]
        public void Startup_WithMinPoolSize_WarmsUpToMinimum(int minPoolSize)
        {
            var factory = new ChannelDbConnectionPoolTest.CountingSuccessfulConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: minPoolSize, maxPoolSize: minPoolSize + 10);

            pool.Startup();

            Assert.True(
                WaitFor(() => pool.Count >= minPoolSize),
                $"Warmup did not reach MinPoolSize; Count={pool.Count}, expected {minPoolSize}.");

            // Warmup must not overshoot the minimum.
            Assert.False(
                WaitFor(() => pool.Count > minPoolSize, timeoutMs: 500),
                $"Warmup overshot MinPoolSize; Count={pool.Count}.");
            Assert.Equal(minPoolSize, pool.Count);
            Assert.Equal(minPoolSize, pool.IdleCount);
            Assert.Equal(minPoolSize, factory.CreateCount);
        }

        // A user request during warmup is served immediately and does not wait for warmup to finish.
        // The gated factory blocks warmup's first (serial) creation, yet a user open still completes
        // by creating its own connection, proving warmup does not block user requests and does not
        // create connections in parallel.
        [Fact]
        public async Task Startup_UserRequestDuringWarmup_ServedImmediately()
        {
            using var createGate = new ManualResetEventSlim(initialState: false);
            var factory = new ChannelDbConnectionPoolTest.GatedSuccessfulConnectionFactory(createGate);
            using var pool = ConstructPool(factory, minPoolSize: 3, maxPoolSize: 10);

            // Warmup begins and its single, serial creation blocks on the gate.
            pool.Startup();
            Assert.True(
                factory.FirstCreateStarted.Wait(TimeSpan.FromSeconds(5)),
                "Timed out waiting for warmup to begin its first creation.");

            // A user request runs while warmup is still blocked. It must complete promptly by
            // creating its own (second) connection rather than waiting for warmup.
            Task<DbConnectionInternal> userOpen = Task.Run(() => CheckOut(pool));
            Task completed = await Task.WhenAny(userOpen, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.True(completed == userOpen, "User request was blocked behind warmup.");
            Assert.NotNull(await userOpen);

            // Warmup is still parked on its single in-flight creation (serial, not parallel): only
            // warmup's first creation (count 1) and the user's creation (count 2) have occurred.
            Assert.Equal(2, factory.CreateCount);

            createGate.Set();
        }

        #endregion

        #region Story 2 - Warmup through the shared rate limiter

        // Warmup creates connections through the shared rate limiter and still reaches MinPoolSize.
        [Fact]
        public void Warmup_ThroughSharedRateLimiter_ReachesMinimum()
        {
            using var rateLimiter = new ConcurrencyLimiter(
                new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });
            var factory = new ChannelDbConnectionPoolTest.CountingSuccessfulConnectionFactory();
            using var pool = ConstructPool(
                factory,
                minPoolSize: 3,
                maxPoolSize: 10,
                connectionCreationRateLimiter: rateLimiter);

            pool.Startup();

            Assert.True(
                WaitFor(() => pool.Count >= 3),
                $"Warmup did not reach MinPoolSize through the rate limiter; Count={pool.Count}.");
            Assert.Equal(3, pool.Count);
            // Every warmup creation acquired (and released) a permit from the shared limiter.
            Assert.True(rateLimiter.GetStatistics()!.TotalSuccessfulLeases >= 3);
        }

        // When the shared rate limiter is saturated by a user request, warmup waits its turn rather
        // than bypassing the limiter: the user request that arrives while warmup holds the only
        // permit is denied by the same limiter.
        [Fact]
        public async Task Warmup_RateLimiterSaturated_UserSharesSameLimiter()
        {
            using var createGate = new ManualResetEventSlim(initialState: false);
            var factory = new ChannelDbConnectionPoolTest.GatedSuccessfulConnectionFactory(createGate);
            using var rateLimiter = new ConcurrencyLimiter(
                new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });
            using var pool = ConstructPool(
                factory,
                minPoolSize: 2,
                maxPoolSize: 5,
                connectionCreationRateLimiter: rateLimiter);

            // Warmup begins, acquires the only permit, and blocks in creation while holding it.
            pool.Startup();
            Assert.True(
                factory.FirstCreateStarted.Wait(TimeSpan.FromSeconds(5)),
                "Timed out waiting for warmup to begin its first creation.");

            // A concurrent user request competes for the same limiter and is denied a permit,
            // proving warmup and user requests share one limiter (warmup did not bypass it).
            long failedBefore = rateLimiter.GetStatistics()!.TotalFailedLeases;
            var tcs = new TaskCompletionSource<DbConnectionInternal>();
            pool.TryGetConnection(new SqlConnection(), tcs, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _);

            Assert.True(
                WaitFor(() => rateLimiter.GetStatistics()!.TotalFailedLeases > failedBefore),
                "User request was not denied by the shared rate limiter while warmup held the permit.");

            // Release warmup; the pool must still converge to MinPoolSize and the user request
            // completes once capacity frees up.
            createGate.Set();
            Assert.True(WaitFor(() => pool.Count >= 2), $"Pool did not reach MinPoolSize; Count={pool.Count}.");

            DbConnectionInternal? userConnection = await tcs.Task;
            Assert.NotNull(userConnection);
        }

        #endregion

        #region Story 3 - Warmup failure resilience

        // Warmup creation failures are absorbed: no exception surfaces, the pool stays empty, and the
        // pool is NOT put into the blocking-period error state.
        [Fact]
        public void Warmup_AllCreationsFail_AbsorbedAndNoErrorState()
        {
            var factory = new WarmupFailingConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 3, maxPoolSize: 10);

            pool.Startup();

            // Let warmup attempt (and fail) its creations.
            Assert.True(
                WaitFor(() => factory.WarmupAttemptCount >= 1),
                "Warmup never attempted a creation.");

            // Pool never accumulates idle connections, but nothing crashes and the pool is not in
            // error state. (Count reflects in-flight reservations transiently, so IdleCount is the
            // reliable measure of successfully pooled connections.)
            Assert.False(
                WaitFor(() => pool.IdleCount > 0, timeoutMs: 500),
                "A warmup connection was pooled despite all creations failing.");
            Assert.Equal(0, pool.IdleCount);
            Assert.False(pool.ErrorOccurred, "Warmup failures must not trigger the pool error state.");

            // A genuine creation failure stops the warmup pass instead of retrying on a tight
            // cadence, so the failing factory is not hammered. Without a fresh below-minimum trigger
            // the attempt count stays bounded (a single failed pass), rather than spinning up dozens
            // of attempts against a server that is failing to accept connections.
            int attemptsAfterFailure = factory.WarmupAttemptCount;
            Thread.Sleep(300);
            Assert.True(
                factory.WarmupAttemptCount <= attemptsAfterFailure + 1,
                $"Warmup spun on a persistent failure: {factory.WarmupAttemptCount} attempts observed.");
        }

        // After warmup fails, a subsequent user request creates a connection on demand and succeeds.
        // The factory fails only warmup creations (owning connection is null) and succeeds for user
        // requests, so the two paths are cleanly separated.
        [Fact]
        public void Warmup_Fails_UserRequestStillSucceeds()
        {
            var factory = new WarmupFailingConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 2, maxPoolSize: 10);

            pool.Startup();
            Assert.True(WaitFor(() => factory.WarmupAttemptCount >= 1), "Warmup never attempted a creation.");

            // A real user request (non-null owning connection) succeeds on demand.
            DbConnectionInternal connection = CheckOut(pool);
            Assert.NotNull(connection);
            Assert.False(pool.ErrorOccurred);
        }

        #endregion

        #region Story 4 - Warmup cancellation on shutdown

        // When the pool is shut down mid-warmup, warmup stops promptly: no connections created after
        // shutdown begins, and any in-flight connection is cleaned up.
        [Fact]
        public void Shutdown_DuringWarmup_StopsAndCleansUp()
        {
            using var createGate = new ManualResetEventSlim(initialState: false);
            var factory = new ChannelDbConnectionPoolTest.GatedSuccessfulConnectionFactory(createGate);
            using var pool = ConstructPool(factory, minPoolSize: 3, maxPoolSize: 10);

            // Warmup begins and blocks on its first serial creation.
            pool.Startup();
            Assert.True(
                factory.FirstCreateStarted.Wait(TimeSpan.FromSeconds(5)),
                "Timed out waiting for warmup to begin its first creation.");

            // Shut down while warmup is parked. This cancels warmup and completes the idle channel.
            pool.Shutdown();

            // Release the in-flight creation; it must be cleaned up, not pooled.
            createGate.Set();

            // No further warmup creations happen after shutdown, and nothing lingers in the pool.
            Assert.True(
                WaitFor(() => pool.Count == 0),
                $"In-flight warmup connection was not cleaned up on shutdown; Count={pool.Count}.");
            Assert.False(
                WaitFor(() => factory.CreateCount > 1, timeoutMs: 500),
                $"Warmup created additional connections after shutdown; CreateCount={factory.CreateCount}.");
            Assert.Equal(1, factory.CreateCount);
        }

        #endregion

        #region Story 5 - Replenishment on any below-minimum event

        // Destroying a connection on return (non-poolable/broken) drops the pool below MinPoolSize and
        // triggers replenishment back to the minimum.
        [Fact]
        public void Replenish_AfterDoomedReturn_RefillsToMinimum()
        {
            var factory = new DoomableConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 2, maxPoolSize: 10);

            pool.Startup();
            Assert.True(WaitFor(() => pool.Count >= 2), $"Initial warmup failed; Count={pool.Count}.");

            // Check out a connection, mark it non-poolable, and return it: it is destroyed on return.
            var owner = new SqlConnection();
            pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection);
            Assert.NotNull(connection);
            ((DoomableStubConnection)connection!).MarkDoNotPool();
            pool.ReturnInternalConnection(connection, owner);

            // Replenishment restores the pool to MinPoolSize.
            Assert.True(
                WaitFor(() => pool.Count >= 2),
                $"Pool did not replenish to MinPoolSize after a doomed return; Count={pool.Count}.");
            Assert.Equal(2, pool.Count);
        }

        // Clearing the pool drops it to zero (below MinPoolSize) and triggers replenishment back to
        // the minimum with fresh-generation connections.
        [Fact]
        public void Replenish_AfterClear_RefillsToMinimum()
        {
            var factory = new ChannelDbConnectionPoolTest.CountingSuccessfulConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 3, maxPoolSize: 10);

            pool.Startup();
            Assert.True(WaitFor(() => pool.Count >= 3), $"Initial warmup failed; Count={pool.Count}.");

            pool.Clear();

            Assert.True(
                WaitFor(() => pool.Count >= 3),
                $"Pool did not replenish to MinPoolSize after Clear; Count={pool.Count}.");
            Assert.Equal(3, pool.Count);
        }

        // Destroying a connection while still at/above the minimum does NOT trigger replenishment
        // beyond the minimum: the pool stays at MinPoolSize.
        [Fact]
        public void Replenish_DestroyWhileAtMinimum_NoOvershoot()
        {
            var factory = new DoomableConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 2, maxPoolSize: 10);

            pool.Startup();
            Assert.True(WaitFor(() => pool.Count >= 2), $"Initial warmup failed; Count={pool.Count}.");

            // Grow above the minimum. Two checkouts reuse the two idle warmup connections; a third
            // finds no idle connection and creates a new one, bringing the total to 3.
            var owner1 = new SqlConnection();
            var owner2 = new SqlConnection();
            var owner3 = new SqlConnection();
            pool.TryGetConnection(owner1, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? c1);
            pool.TryGetConnection(owner2, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? c2);
            pool.TryGetConnection(owner3, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? c3);
            Assert.NotNull(c1);
            Assert.NotNull(c2);
            Assert.NotNull(c3);
            Assert.True(WaitFor(() => pool.Count == 3), $"Expected pool to grow to 3; Count={pool.Count}.");

            // Destroy the third on return. Dropping from 3 to 2 stays at the minimum, so no
            // replenishment overshoot occurs.
            ((DoomableStubConnection)c3!).MarkDoNotPool();
            pool.ReturnInternalConnection(c3, owner3);

            Assert.True(WaitFor(() => pool.Count == 2), $"Pool did not settle at MinPoolSize; Count={pool.Count}.");
            Assert.False(
                WaitFor(() => pool.Count > 2, timeoutMs: 500),
                $"Pool overshot MinPoolSize after a destroy at the minimum; Count={pool.Count}.");
            Assert.Equal(2, pool.Count);
        }

        #endregion

        #region Test doubles

        /// <summary>
        /// A stub internal connection that can be marked non-poolable so a return destroys it.
        /// </summary>
        private sealed class DoomableStubConnection : DbConnectionInternal
        {
            internal void MarkDoNotPool() => DoNotPoolThisConnection();

            public override string ServerVersion => throw new NotImplementedException();

            public override ConnectionCapabilities Capabilities => throw new NotImplementedException();

            public override DbTransaction BeginTransaction(System.Data.IsolationLevel il)
                => throw new NotImplementedException();

            public override void EnlistTransaction(Transaction transaction)
            {
            }

            protected override void Activate(Transaction transaction)
            {
            }

            protected override void Deactivate()
            {
            }

            internal override void ResetConnection()
            {
            }
        }

        /// <summary>
        /// Produces <see cref="DoomableStubConnection"/> instances so replenishment tests can force a
        /// connection to be destroyed on return.
        /// </summary>
        private sealed class DoomableConnectionFactory : SqlConnectionFactory
        {
            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
                => new DoomableStubConnection();
        }

        /// <summary>
        /// Fails only warmup creations (identified by a null owning connection) and succeeds for real
        /// user requests. Lets tests assert that warmup failures are absorbed while on-demand user
        /// creation still works.
        /// </summary>
        private sealed class WarmupFailingConnectionFactory : SqlConnectionFactory
        {
            private int _warmupAttemptCount;

            internal int WarmupAttemptCount => Volatile.Read(ref _warmupAttemptCount);

            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
            {
                if (owningConnection is null)
                {
                    Interlocked.Increment(ref _warmupAttemptCount);
                    throw ADP.PooledOpenTimeout();
                }

                return new DoomableStubConnection();
            }
        }

        #endregion
    }
}

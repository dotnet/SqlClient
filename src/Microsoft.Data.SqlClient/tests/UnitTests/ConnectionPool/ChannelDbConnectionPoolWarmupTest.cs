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

        /// <summary>
        /// Generous upper bound for waits that guard a background handshake which, in correct code,
        /// completes in milliseconds. It is only ever hit on a genuine failure/hang, so it does not
        /// make passing runs slow or introduce timing races.
        /// </summary>
        private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Awaits background warmup passes until the pool settles at <paramref name="minPoolSize"/>.
        /// A single coalesced pass can race with concurrent removals - for example a
        /// <see cref="ChannelDbConnectionPool.Clear"/> draining the pool while replenishment refills
        /// it - so this re-requests warmup until the pool is full. Only safe with a factory whose
        /// warmup creations succeed and where the pool is not in the error state, so every pass makes
        /// forward progress and the loop terminates.
        /// </summary>
        private static async Task AwaitWarmupToMinimum(ChannelDbConnectionPool pool, int minPoolSize)
        {
            while (pool.Count < minPoolSize)
            {
                pool.RequestWarmup();
                Task? warmup = pool.WarmupLoopTask;
                if (warmup is not null)
                {
                    await warmup;
                }
            }
        }

        #endregion

        #region Story 1 - Background warmup on pool creation

        /// <summary>
        /// Warmup is a no-op when MinPoolSize == 0: no connections are ever created in the background.
        /// </summary>
        [Fact]
        public void Startup_MinPoolSizeZero_DoesNotWarmUp()
        {
            var factory = new ChannelDbConnectionPoolTest.CountingSuccessfulConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 0, maxPoolSize: 10);

            pool.Startup();

            // MinPoolSize == 0, so warmup is a no-op: no loop is ever launched (WarmupLoopTask stays
            // null) and nothing is created in the background.
            Assert.Null(pool.WarmupLoopTask);
            Assert.Equal(0, factory.CreateCount);
            Assert.Equal(0, pool.Count);
        }

        /// <summary>
        /// Warmup pre-creates connections up to MinPoolSize for various sizes (1 and N).
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(10)]
        public async Task Startup_WithMinPoolSize_WarmsUpToMinimum(int minPoolSize)
        {
            var factory = new ChannelDbConnectionPoolTest.CountingSuccessfulConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: minPoolSize, maxPoolSize: minPoolSize + 10);

            pool.Startup();

            // Awaiting the warmup loop to completion is deterministic: the single serial pass runs
            // with no competing removals, so when it finishes the pool is exactly at MinPoolSize.
            await pool.WarmupLoopTask!;

            // Warmup reaches exactly MinPoolSize (no overshoot) and pools each created connection.
            Assert.Equal(minPoolSize, pool.Count);
            Assert.Equal(minPoolSize, pool.IdleCount);
            Assert.Equal(minPoolSize, factory.CreateCount);
        }

        /// <summary>
        /// A user request during warmup is served immediately and does not wait for warmup to finish.
        /// The gated factory blocks warmup's first (serial) creation, yet a user open still completes
        /// by creating its own connection, proving warmup does not block user requests and does not
        /// create connections in parallel.
        /// </summary>
        [Fact]
        public async Task Startup_UserRequestDuringWarmup_ServedImmediately()
        {
            using var createGate = new ManualResetEventSlim(initialState: false);
            var factory = new ChannelDbConnectionPoolTest.GatedSuccessfulConnectionFactory(createGate);
            using var pool = ConstructPool(factory, minPoolSize: 3, maxPoolSize: 10);

            // Warmup begins and its single, serial creation blocks on the gate. Waiting on this
            // explicit signal (not a sleep) establishes a deterministic happens-before: warmup is now
            // parked inside its first create.
            pool.Startup();
            Assert.True(
                factory.FirstCreateStarted.Wait(HandshakeTimeout),
                "Timed out waiting for warmup to begin its first creation.");

            // Exactly one creation - warmup's - is in flight and parked on the closed gate. The
            // warmup loop is serial, so it cannot advance to a second create until the gate opens.
            Assert.Equal(1, factory.CreateCount);

            // Issue a user request while warmup is parked. Run it on a dedicated (LongRunning) thread
            // so it never competes with warmup for the same pool worker. Because warmup does not block
            // user requests, it must complete promptly by creating its own connection. A generous
            // upper bound converts a hypothetical "user blocked behind warmup" regression into a clean
            // failure instead of an indefinite hang; correct code completes in milliseconds.
            Task<DbConnectionInternal> userOpen = Task.Factory.StartNew(
                () => CheckOut(pool),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            Task completed = await Task.WhenAny(userOpen, Task.Delay(HandshakeTimeout));
            Assert.True(completed == userOpen, "User request was blocked behind warmup.");
            Assert.NotNull(await userOpen);

            // Seriality: warmup is still parked on its single in-flight create (the gate is closed),
            // so the only additional creation is the user's. CreateCount == 2 proves warmup did not
            // create connections in parallel. This is deterministic: nothing can advance warmup's
            // loop until the gate is set below.
            Assert.Equal(2, factory.CreateCount);

            createGate.Set();
        }

        #endregion

        #region Story 2 - Warmup through the shared rate limiter

        /// <summary>
        /// Warmup creates connections through the shared rate limiter and still reaches MinPoolSize.
        /// </summary>
        [Fact]
        public async Task Warmup_ThroughSharedRateLimiter_ReachesMinimum()
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

            // Warmup is the only actor here, so each serial create acquires and releases the single
            // permit in turn; the pass never sees a saturated limiter and reaches MinPoolSize.
            await pool.WarmupLoopTask!;

            Assert.Equal(3, pool.Count);
            // Every warmup creation acquired (and released) a permit from the shared limiter.
            Assert.True(rateLimiter.GetStatistics()!.TotalSuccessfulLeases >= 3);
        }

        /// <summary>
        /// When the shared rate limiter is saturated by a user request, warmup waits its turn rather
        /// than bypassing the limiter: the user request that arrives while warmup holds the only
        /// permit is denied by the same limiter.
        /// </summary>
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
                factory.FirstCreateStarted.Wait(HandshakeTimeout),
                "Timed out waiting for warmup to begin its first creation.");

            // A concurrent user request competes for the same limiter and is denied a permit,
            // proving warmup and user requests share one limiter (warmup did not bypass it).
            long failedBefore = rateLimiter.GetStatistics()!.TotalFailedLeases;
            var tcs = new TaskCompletionSource<DbConnectionInternal>();
            bool completedSynchronously = pool.TryGetConnection(
                new SqlConnection(), tcs, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? immediateConnection);

            // The async request cannot be satisfied inline: there is no idle connection and the
            // shared limiter's only permit is held by warmup. TryGetConnection therefore returns
            // false with no connection, deferring the result to the TaskCompletionSource.
            Assert.False(completedSynchronously, "Async request unexpectedly completed synchronously.");
            Assert.Null(immediateConnection);

            Assert.True(
                WaitFor(() => rateLimiter.GetStatistics()!.TotalFailedLeases > failedBefore),
                "User request was not denied by the shared rate limiter while warmup held the permit.");

            // Release warmup. With rate-limit retry removed, warmup's pass may end early if the
            // user request grabs the freed permit first - but the pool must still converge to
            // MinPoolSize: whichever creations win the permit (warmup's or the user's) fill it.
            createGate.Set();

            // Bound the wait so a regression that never completes the async request fails fast
            // instead of hanging the whole test run.
            Task<DbConnectionInternal> userRequest = tcs.Task;
            Assert.True(
                userRequest == await Task.WhenAny(userRequest, Task.Delay(HandshakeTimeout)),
                "Timed out waiting for the deferred user request to complete.");
            DbConnectionInternal? userConnection = await userRequest;
            Assert.NotNull(userConnection);

            // Once both the warmup pass and the deferred user request have settled, the pool holds
            // exactly MinPoolSize connections (a warmup idle connection plus the user's, or two
            // warmup connections one of which the user took).
            await AwaitWarmupToMinimum(pool, 2);
            Assert.Equal(2, pool.Count);
        }

        #endregion

        #region Story 3 - Warmup failure resilience

        /// <summary>
        /// Warmup creation failures are absorbed - no exception surfaces onto the thread pool and no
        /// connections are pooled - but, mirroring the legacy WaitHandle pool, a genuine open failure
        /// does enter the pool's blocking-period error state. Warmup then stops the pass rather than
        /// spinning on the persistent failure.
        /// </summary>
        [Fact]
        public async Task Warmup_AllCreationsFail_AbsorbedAndEntersErrorState()
        {
            var factory = new WarmupFailingConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 3, maxPoolSize: 10);

            pool.Startup();

            // Await the warmup pass. The first open fails: OpenNewInternalConnection enters the
            // blocking-period error state and rethrows, the warmup loop absorbs the rethrow and ends
            // the pass. Awaiting is deterministic - no exception surfaces onto the thread pool.
            await pool.WarmupLoopTask!;

            // Exactly one attempt was made: a genuine failure ends the pass rather than spinning on
            // the persistent failure, and nothing is pooled.
            Assert.Equal(1, factory.WarmupAttemptCount);
            Assert.Equal(0, pool.IdleCount);

            // Mirroring the legacy WaitHandle pool, the failed open entered the blocking-period error
            // state, exactly as an on-demand creation failure would.
            Assert.True(
                pool.ErrorOccurred,
                "Warmup failure did not enter the pool error state as the WaitHandle pool does.");
        }

        /// <summary>
        /// After warmup fails, the pool enters its blocking-period error state (mirroring the
        /// WaitHandle pool), so a user request during the blocking window fast-fails with the cached
        /// exception rather than attempting a fresh on-demand open. The pool remains operational and
        /// resumes creating on demand once the blocking period expires.
        /// </summary>
        [Fact]
        public async Task Warmup_Fails_UserRequestFastFailsDuringBlockingPeriod()
        {
            var factory = new WarmupFailingConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 2, maxPoolSize: 10);

            pool.Startup();

            // Warmup's failure drives the pool into the blocking-period error state.
            await pool.WarmupLoopTask!;
            Assert.True(pool.ErrorOccurred, "Warmup failure did not enter the pool error state.");

            // A user request while the pool is blocking fast-fails with the cached exception.
            Assert.ThrowsAny<Exception>(() => CheckOut(pool));
        }

        /// <summary>
        /// Warmup respects the pool's blocking-period error state: once a failing user request has
        /// driven the pool into the error state, invoking warmup must make it stand down (its loop
        /// condition sees ErrorOccurred) and create nothing, mirroring the legacy WaitHandle pool,
        /// which skips replenishment while the pool is blocking. Driving the error state with a user
        /// request (rather than warmup itself) keeps the two behaviors independent, and awaiting the
        /// warmup task makes the stand-down deterministic.
        /// </summary>
        [Fact]
        public async Task Warmup_RespectsErrorState_StandsDownWhileBlocking()
        {
            var factory = new UserFailingWarmupCountingConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 3, maxPoolSize: 10);

            // Drive the pool into the blocking-period error state with a failing user request. We do
            // NOT call Startup here: that would kick off a *successful* warmup and fill the pool,
            // which is not the behavior under test.
            Assert.ThrowsAny<Exception>(() => CheckOut(pool));
            Assert.True(pool.ErrorOccurred, "Failing user request did not enter the pool error state.");

            // Now request warmup directly. Because the pool is blocking, the loop's error-state guard
            // makes it stand down immediately: it creates nothing rather than piling doomed opens
            // onto a struggling server.
            pool.RequestWarmup();
            await pool.WarmupLoopTask!;

            Assert.Equal(0, factory.WarmupCreateCount);
            Assert.Equal(0, pool.IdleCount);
            Assert.True(pool.ErrorOccurred, "Warmup must not clear the error state it stood down for.");
        }

        #endregion

        #region Story 4 - Warmup cancellation on shutdown

        /// <summary>
        /// When the pool is shut down mid-warmup, warmup stops promptly: no connections created after
        /// shutdown begins, and any in-flight connection is cleaned up.
        /// </summary>
        [Fact]
        public async Task Shutdown_DuringWarmup_StopsAndCleansUp()
        {
            using var createGate = new ManualResetEventSlim(initialState: false);
            var factory = new ChannelDbConnectionPoolTest.GatedSuccessfulConnectionFactory(createGate);
            using var pool = ConstructPool(factory, minPoolSize: 3, maxPoolSize: 10);

            // Warmup begins and blocks on its first serial creation.
            pool.Startup();
            Assert.True(
                factory.FirstCreateStarted.Wait(HandshakeTimeout),
                "Timed out waiting for warmup to begin its first creation.");

            // Shut down while warmup is parked. This cancels warmup and completes the idle channel.
            pool.Shutdown();

            // Release the in-flight creation and await the loop to completion. The freshly created
            // connection cannot be published to the completed idle channel, so it is destroyed, and
            // the loop stops without creating anything further.
            createGate.Set();
            await pool.WarmupLoopTask!;

            Assert.Equal(0, pool.Count);
            Assert.Equal(1, factory.CreateCount);
        }

        #endregion

        #region Story 5 - Replenishment on any below-minimum event

        /// <summary>
        /// Destroying a connection on return (non-poolable/broken) drops the pool below MinPoolSize and
        /// triggers replenishment back to the minimum.
        /// </summary>
        [Fact]
        public async Task Replenish_AfterDoomedReturn_RefillsToMinimum()
        {
            var factory = new DoomableConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 2, maxPoolSize: 10);

            pool.Startup();
            await pool.WarmupLoopTask!;
            Assert.Equal(2, pool.Count);

            // Check out a connection, mark it non-poolable, and return it: it is destroyed on return,
            // dropping the pool to 1 and triggering replenishment through RemoveConnection.
            var owner = new SqlConnection();
            pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection);
            Assert.NotNull(connection);
            ((DoomableStubConnection)connection!).MarkDoNotPool();
            pool.ReturnInternalConnection(connection, owner);

            // The doomed return re-triggered warmup synchronously; awaiting the replenishment pass
            // (a single serial create with no competing removal) restores the pool to MinPoolSize.
            await pool.WarmupLoopTask!;
            Assert.Equal(2, pool.Count);
        }

        /// <summary>
        /// Clearing the pool drops it to zero (below MinPoolSize) and triggers replenishment back to
        /// the minimum with fresh-generation connections.
        /// </summary>
        [Fact]
        public async Task Replenish_AfterClear_RefillsToMinimum()
        {
            var factory = new ChannelDbConnectionPoolTest.CountingSuccessfulConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 3, maxPoolSize: 10);

            pool.Startup();
            await pool.WarmupLoopTask!;
            Assert.Equal(3, pool.Count);

            pool.Clear();

            // Clear drains the pool concurrently with replenishment, so a single warmup pass can race
            // with the drain; await passes until the pool settles at MinPoolSize.
            await AwaitWarmupToMinimum(pool, 3);
            Assert.Equal(3, pool.Count);
        }

        /// <summary>
        /// Destroying a connection while still at/above the minimum does NOT trigger replenishment
        /// beyond the minimum: the pool stays at MinPoolSize.
        /// </summary>
        [Fact]
        public async Task Replenish_DestroyWhileAtMinimum_NoOvershoot()
        {
            var factory = new DoomableConnectionFactory();
            using var pool = ConstructPool(factory, minPoolSize: 2, maxPoolSize: 10);

            pool.Startup();
            await pool.WarmupLoopTask!;
            Assert.Equal(2, pool.Count);

            // Grow above the minimum. Two checkouts reuse the two idle warmup connections; a third
            // finds no idle connection and creates a new one synchronously, bringing the total to 3.
            var owner1 = new SqlConnection();
            var owner2 = new SqlConnection();
            var owner3 = new SqlConnection();
            pool.TryGetConnection(owner1, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? c1);
            pool.TryGetConnection(owner2, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? c2);
            pool.TryGetConnection(owner3, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? c3);
            Assert.NotNull(c1);
            Assert.NotNull(c2);
            Assert.NotNull(c3);
            Assert.Equal(3, pool.Count);

            // Destroy the third on return. Dropping from 3 to 2 stays at the minimum, so RequestWarmup
            // hits its fast path and launches no new pass.
            Task? warmupBeforeReturn = pool.WarmupLoopTask;
            ((DoomableStubConnection)c3!).MarkDoNotPool();
            pool.ReturnInternalConnection(c3, owner3);

            // No replenishment was triggered: the exposed warmup task is unchanged (no new pass) and
            // the pool settled at MinPoolSize without overshooting.
            Assert.Same(warmupBeforeReturn, pool.WarmupLoopTask);
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

        /// <summary>
        /// Fails user requests (non-null owning connection) so the pool enters its blocking-period
        /// error state, while succeeding for and counting warmup creations (null owning connection).
        /// Lets a test drive the pool into the error state and then verify warmup stands down
        /// (creates nothing) rather than continuing to replenish.
        /// </summary>
        private sealed class UserFailingWarmupCountingConnectionFactory : SqlConnectionFactory
        {
            private int _warmupCreateCount;

            internal int WarmupCreateCount => Volatile.Read(ref _warmupCreateCount);

            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
            {
                if (owningConnection is not null)
                {
                    // User request: fail so the pool enters the blocking-period error state.
                    throw ADP.PooledOpenTimeout();
                }

                // Warmup creation: count it. In the stand-down test this must never be reached while
                // the pool is blocking.
                Interlocked.Increment(ref _warmupCreateCount);
                return new DoomableStubConnection();
            }
        }

        #endregion
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Unit tests for <see cref="PoolReclaimer"/>, the background sweep that reclaims emancipated
    /// connections in <see cref="ChannelDbConnectionPool"/> while callers are parked on the idle
    /// channel.
    /// </summary>
    public class ChannelDbConnectionPoolReclaimTimerTest
    {
        private static readonly SqlConnectionFactory ConnectionFactory =
            new ChannelDbConnectionPoolTest.SuccessfulSqlConnectionFactory();

        #region Helpers

        private static ChannelDbConnectionPool ConstructPool(
            int minPoolSize = 0,
            int maxPoolSize = 50,
            int idleTimeout = 0,
            TimeProvider? timeProvider = null)
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: minPoolSize,
                maxPoolSize: maxPoolSize,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: idleTimeout
            );
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions
            );
            return new ChannelDbConnectionPool(
                ConnectionFactory,
                dbConnectionPoolGroup,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo(),
                connectionCreationRateLimiter: null,
                timeProvider: timeProvider
            );
        }

        /// <summary>
        /// Checks out a connection and abandons its owning <see cref="SqlConnection"/>, returning
        /// only the internal connection. Marked <see cref="MethodImplOptions.NoInlining"/> so the
        /// owner's stack slot is guaranteed to be gone when the caller collects: in Debug builds
        /// locals stay alive to the end of their enclosing method, so the owner has to be confined
        /// to a frame that has already been popped.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static DbConnectionInternal CheckOutAndAbandonOwner(ChannelDbConnectionPool pool)
        {
            SqlConnection owner = new();
            bool completed = pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? connection);

            Assert.True(completed);
            Assert.NotNull(connection);
            return connection!;
        }

        /// <summary>
        /// Checks out a connection and roots its owner in a <see cref="GCHandle"/> rather than a
        /// local. The owner stays alive until the caller frees the returned handle, which gives the
        /// test exact control over when the connection becomes emancipated. A local cannot do this:
        /// in a Debug build its stack slot roots the object for the rest of the enclosing method
        /// even after it is assigned null, so the collection would never happen. The checkout runs
        /// in its own non-inlined frame so no slot in the caller's frame ever holds the owner.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static DbConnectionInternal CheckOutAndRootOwner(ChannelDbConnectionPool pool, out GCHandle ownerRoot)
        {
            SqlConnection owner = new();
            ownerRoot = GCHandle.Alloc(owner);

            bool completed = pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? connection);

            Assert.True(completed);
            Assert.NotNull(connection);
            return connection!;
        }

        /// <summary>
        /// Forces collection of an abandoned owner so its connection becomes emancipated.
        /// </summary>
        private static void CollectAbandonedOwners()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>
        /// Spins until <paramref name="condition"/> holds, failing the test if it does not happen
        /// within a generous timeout. Used to observe a background caller reaching its parked wait,
        /// which is inherently a cross-thread transition and cannot be awaited directly.
        /// </summary>
        private static void WaitFor(Func<bool> condition, string because)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!condition())
            {
                Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), because);
                Thread.Sleep(10);
            }
        }

        #endregion

        /// <summary>
        /// The reclaimer must exist for every pool configuration. A connection can be leaked
        /// regardless of sizing or idle timeout, and a fixed-size pool (where <see
        /// cref="ChannelDbConnectionPool.Pruner"/> is deliberately null) is the configuration where
        /// a permanently occupied slot hurts most.
        /// </summary>
        [Theory]
        [InlineData(0, 50, 300)]  // Growable pool with idle reclamation: pruner also present.
        [InlineData(5, 5, 300)]   // Fixed-size pool: no pruner.
        [InlineData(0, 50, 0)]    // Idle reclamation disabled: no pruner.
        public void Reclaimer_IsConstructedForEveryPoolConfiguration(int minPoolSize, int maxPoolSize, int idleTimeout)
        {
            var pool = ConstructPool(minPoolSize: minPoolSize, maxPoolSize: maxPoolSize, idleTimeout: idleTimeout);

            Assert.NotNull(pool.Reclaimer);
            Assert.False(pool.Reclaimer.IsTimerEnabled);
            Assert.Equal(0, pool.Reclaimer.ParkedWaiters);
        }

        /// <summary>
        /// The timer is demand-driven: armed by the first parked caller and disarmed by the last one
        /// to leave, so a pool with no blocked callers never wakes the process.
        /// </summary>
        [Fact]
        public void EnterAndExitParkedWait_ArmsAndDisarmsTimer()
        {
            var pool = ConstructPool();
            PoolReclaimer reclaimer = pool.Reclaimer;

            reclaimer.EnterParkedWait();
            Assert.True(reclaimer.IsTimerEnabled);
            Assert.Equal(1, reclaimer.ParkedWaiters);

            reclaimer.ExitParkedWait();
            Assert.False(reclaimer.IsTimerEnabled);
            Assert.Equal(0, reclaimer.ParkedWaiters);
        }

        /// <summary>
        /// With several callers parked, the timer stays armed until the last one leaves. Disarming
        /// on the first departure would strand the callers still waiting.
        /// </summary>
        [Fact]
        public void ExitParkedWait_WithOtherWaitersRemaining_KeepsTimerArmed()
        {
            var pool = ConstructPool();
            PoolReclaimer reclaimer = pool.Reclaimer;

            reclaimer.EnterParkedWait();
            reclaimer.EnterParkedWait();
            reclaimer.EnterParkedWait();
            Assert.Equal(3, reclaimer.ParkedWaiters);

            reclaimer.ExitParkedWait();
            Assert.True(reclaimer.IsTimerEnabled);
            reclaimer.ExitParkedWait();
            Assert.True(reclaimer.IsTimerEnabled);

            reclaimer.ExitParkedWait();
            Assert.False(reclaimer.IsTimerEnabled);
        }

        /// <summary>
        /// After the timer has been disarmed and re-armed, it must still fire. This guards the
        /// one-shot re-arm bookkeeping, where losing the armed flag would silently stop all
        /// subsequent sweeps.
        /// </summary>
        [Fact]
        public void EnterParkedWait_AfterFullDrain_ReArmsTimer()
        {
            var pool = ConstructPool();
            PoolReclaimer reclaimer = pool.Reclaimer;

            reclaimer.EnterParkedWait();
            reclaimer.ExitParkedWait();
            Assert.False(reclaimer.IsTimerEnabled);

            reclaimer.EnterParkedWait();
            Assert.True(reclaimer.IsTimerEnabled);
        }

        /// <summary>
        /// A sweep callback that was already scheduled when the last caller left must not run. The
        /// pool is no longer blocked, so there is nobody to wake and no reason to pay for the sweep.
        /// </summary>
        [Fact]
        public void OnSweepCallback_WhenDisarmed_DoesNotSweep()
        {
            var pool = ConstructPool(maxPoolSize: 1);
            DbConnectionInternal connection = CheckOutAndAbandonOwner(pool);
            CollectAbandonedOwners();
            Assert.True(connection.IsEmancipated);

            // Never armed, so this stands in for a callback that raced with the last exit.
            pool.Reclaimer.OnSweepCallback();

            Assert.Equal(0, pool.IdleCount);
            Assert.True(connection.IsEmancipated);
        }

        /// <summary>
        /// A sweep reclaims an emancipated connection and routes it back to the idle channel, which
        /// is what makes it visible to a parked caller.
        /// </summary>
        [Fact]
        public void OnSweepCallback_WhenArmed_ReclaimsEmancipatedConnection()
        {
            var pool = ConstructPool(maxPoolSize: 1);
            DbConnectionInternal connection = CheckOutAndAbandonOwner(pool);
            CollectAbandonedOwners();
            Assert.True(connection.IsEmancipated);
            Assert.Equal(0, pool.IdleCount);

            pool.Reclaimer.EnterParkedWait();
            try
            {
                pool.Reclaimer.OnSweepCallback();
            }
            finally
            {
                pool.Reclaimer.ExitParkedWait();
            }

            Assert.Equal(1, pool.IdleCount);
        }

        /// <summary>
        /// Shutting the pool down releases the timer. Otherwise a scheduled sweep would keep the
        /// pool reachable and could route a connection back into the channel after the shutdown
        /// drain had already passed it.
        /// </summary>
        [Fact]
        public void Shutdown_DisposesReclaimer()
        {
            var pool = ConstructPool();
            pool.Reclaimer.EnterParkedWait();
            Assert.True(pool.Reclaimer.IsTimerEnabled);

            pool.Shutdown();

            Assert.False(pool.Reclaimer.IsTimerEnabled);

            // Arming after disposal must be a no-op rather than resurrecting the timer.
            pool.Reclaimer.EnterParkedWait();
            Assert.False(pool.Reclaimer.IsTimerEnabled);
        }

        /// <summary>
        /// End-to-end coverage of the gap this feature closes. A caller parks on an exhausted pool,
        /// and only afterwards does the owner of the sole connection become collectable. The inline
        /// sweep the caller performed before parking could not have seen that, so without a
        /// background sweep the caller would wait out its entire timeout even though a slot was
        /// recoverable. Advancing the injected <see cref="FakeTimeProvider"/> fires the sweep, which
        /// reclaims the connection and wakes the caller.
        /// </summary>
        [Fact]
        public async Task ParkedCaller_IsWokenBySweep_WhenConnectionIsEmancipatedAfterParking()
        {
            var fakeTime = new FakeTimeProvider();
            var pool = ConstructPool(maxPoolSize: 1, timeProvider: fakeTime);

            // Occupy the pool's only slot. The owner is deliberately kept rooted until after the
            // second caller has parked: a collection triggered by a test running in parallel would
            // otherwise emancipate it early, letting the second caller's inline sweep succeed so it
            // never parks and the case under test never arises.
            DbConnectionInternal leaked = CheckOutAndRootOwner(pool, out GCHandle leakedOwnerRoot);
            Assert.Equal(1, pool.Count);

            // A second caller finds the pool exhausted and parks. Its inline sweep runs before the
            // collection below, so it finds nothing. The async path is used deliberately: the sync
            // path first takes a process-wide sync-over-async semaphore, so a sync caller could sit
            // behind unrelated tests rather than on the idle channel this test is exercising.
            SqlConnection waitingOwner = new();
            TaskCompletionSource<DbConnectionInternal> parked = new();
            pool.TryGetConnection(
                waitingOwner,
                parked,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(30)),
                out DbConnectionInternal? immediate);

            Assert.Null(immediate);
            WaitFor(() => pool.Reclaimer.ParkedWaiters == 1, "the second caller should park on the idle channel");
            Assert.True(pool.Reclaimer.IsTimerEnabled);
            Assert.False(parked.Task.IsCompleted);

            // Only now does the abandoned owner become collectable, which is precisely the case the
            // caller's own inline sweep cannot cover.
            leakedOwnerRoot.Free();
            CollectAbandonedOwners();
            Assert.True(leaked.IsEmancipated);

            // FakeTimeProvider invokes timer callbacks synchronously on Advance.
            fakeTime.Advance(PoolReclaimer.SweepInterval);

            DbConnectionInternal? result = await parked.Task;

            Assert.NotNull(result);
            Assert.Equal(leaked, result);
            Assert.Equal(0, pool.Reclaimer.ParkedWaiters);
            Assert.False(pool.Reclaimer.IsTimerEnabled);

            GC.KeepAlive(waitingOwner);
        }
    }
}

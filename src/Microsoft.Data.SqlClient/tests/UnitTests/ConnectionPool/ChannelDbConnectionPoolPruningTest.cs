// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;
using static Microsoft.Data.SqlClient.ConnectionPool.DbConnectionPoolState;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Unit tests for the pruning feature in <see cref="ChannelDbConnectionPool"/>.
    /// </summary>
    public class ChannelDbConnectionPoolPruningTest
    {
        private static readonly SqlConnectionFactory ConnectionFactory = new SuccessfulSqlConnectionFactory();

        #region Helpers

        private static ChannelDbConnectionPool ConstructPool(
            int minPoolSize = 0,
            int maxPoolSize = 50,
            int loadBalanceTimeout = 0,
            int idleTimeout = 300)
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: minPoolSize,
                maxPoolSize: maxPoolSize,
                creationTimeout: 15,
                loadBalanceTimeout: loadBalanceTimeout,
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
                new DbConnectionPoolProviderInfo()
            );
        }

        /// <summary>
        /// A checked-out connection, pairing the owning <see cref="SqlConnection"/> with its
        /// internal connection so the two can never be mismatched.
        /// </summary>
        private record BusyConnection(SqlConnection Owner, DbConnectionInternal InternalDbConnection);

        /// <summary>
        /// Checks out <paramref name="count"/> connections from the pool and leaves them in use.
        /// Returns the checked-out connections so the caller can later return them via
        /// <see cref="ReturnConnections"/>.
        /// </summary>
        private static List<BusyConnection> CheckOutConnections(
            ChannelDbConnectionPool pool, int count)
        {
            var connections = new List<BusyConnection>(count);

            for (int i = 0; i < count; i++)
            {
                var owner = new SqlConnection();
                var completed = pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out var connection);
                Assert.True(completed);
                Assert.NotNull(connection);
                connections.Add(new BusyConnection(owner, connection));
            }

            return connections;
        }

        /// <summary>
        /// Returns previously checked-out connections to the pool, making them idle.
        /// </summary>
        private static void ReturnConnections(
            ChannelDbConnectionPool pool,
            List<BusyConnection> connections)
        {
            foreach (var connection in connections)
            {
                pool.ReturnInternalConnection(connection.InternalDbConnection, connection.Owner);
            }
        }

        /// <summary>
        /// Opens <paramref name="count"/> connections and returns them to the pool so they are idle.
        /// </summary>
        private static void FillPoolWithIdleConnections(ChannelDbConnectionPool pool, int count)
        {
            var connections = CheckOutConnections(pool, count);
            ReturnConnections(pool, connections);
        }

        /// <summary>
        /// Asserts that the pool has a non-null <see cref="PoolPruner"/> and returns it.
        /// </summary>
        private static PoolPruner GetPruner(ChannelDbConnectionPool pool)
        {
            PoolPruner? pruner = pool.Pruner;
            Assert.NotNull(pruner);
            return pruner;
        }

        /// <summary>
        /// Asserts the pruner state that matters for pruning progression: the timer-enabled flag,
        /// the sample index, and the valid samples.
        /// </summary>
        /// <remarks>
        /// Only the samples in the range <c>[0, SampleIndex)</c> are meaningful and validated.
        /// Callers must pass exactly the valid samples, i.e. <paramref name="expectedSamples"/>
        /// must have <paramref name="sampleIndex"/> elements.
        /// </remarks>
        private static void AssertPrunerState(
            PoolPruner pruner,
            bool isTimerEnabled,
            int sampleIndex,
            params int[] expectedSamples)
        {
            Assert.Equal(isTimerEnabled, pruner.IsTimerEnabled);
            Assert.Equal(sampleIndex, pruner.SampleIndex);

            // Enforce that the caller validates every valid sample (and only valid samples).
            Assert.Equal(sampleIndex, expectedSamples.Length);

            for (int i = 0; i < expectedSamples.Length; i++)
            {
                Assert.Equal(expectedSamples[i], pruner.Samples[i]);
            }
        }

        #endregion

        #region Timer Creation / Configuration Tests

        [Fact]
        public void Constructor_MinPoolSizeLessThanMax_CreatesPruningTimer()
        {
            // When min < max, the pool can shrink so a pruning timer should be created.
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10);

            Assert.NotNull(pool.Pruner);
        }

        [Fact]
        public void Constructor_MinPoolSizeEqualsMax_DoesNotCreatePruningTimer()
        {
            // When min == max, the pool is fixed-size — pruning would never activate.
            using var pool = ConstructPool(minPoolSize: 10, maxPoolSize: 10);

            Assert.Null(pool.Pruner);
        }

        [Fact]
        public void Constructor_IdleTimeoutZero_DoesNotCreatePruningTimer()
        {
            // Connection Idle Timeout = 0 disables idle reclamation, so there is nothing to prune.
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10, idleTimeout: 0);

            Assert.Null(pool.Pruner);
        }

        [Fact]
        public void Constructor_PruningTimerStartsDisabled()
        {
            // Timer should be created but not armed (pool starts empty, below MinPoolSize threshold).
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10);
            var pruner = GetPruner(pool);

            AssertPrunerState(pruner, false, 0);
        }

        [Theory]
        // Short/typical idle timeouts keep the 10s floor and grow the sample count (Story 1 parity).
        [InlineData(100, 10, 10)]      // interval 10s, ceil(100/10) = 10 samples
        [InlineData(300, 10, 30)]      // default idle timeout: 10s, 30 samples
        [InlineData(60, 10, 6)]        // 10s, 6 samples
        [InlineData(15, 10, 2)]        // 10s, ceil(15/10) = 2 samples
        [InlineData(5, 10, 1)]         // sub-interval idle timeout: floored to 10s, 1 sample
        [InlineData(3000, 10, 300)]    // boundary: MaxSampleSize * 10s, still 10s cadence
        // Large idle timeouts pin the sample count at MaxSampleSize and stretch the interval so the
        // window still spans the full idle timeout.
        [InlineData(6000, 20, 300)]    // ceil(6000/300) = 20s interval, 300 samples
        [InlineData(10000, 34, 295)]   // ceil(10000/300) = 34s interval, ceil(10000/34) = 295 samples
        [InlineData(86400, 288, 300)]  // 1-day idle timeout: 288s interval, 300 samples
        // Pathological idle timeout hits the 1-day interval overflow clamp without throwing.
        [InlineData(int.MaxValue, 86400, 300)]
        public void Constructor_CalculatesIntervalAndSampleSizeFromIdleTimeout(
            int idleTimeout, int expectedIntervalSeconds, int expectedSampleSize)
        {
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10, idleTimeout: idleTimeout);
            var pruner = GetPruner(pool);

            Assert.Equal(expectedSampleSize, pruner.SampleSize);
            Assert.Equal(TimeSpan.FromSeconds(expectedIntervalSeconds), pruner.SamplingInterval);
        }

        #endregion

        #region UpdatePruningTimer Tests

        [Fact]
        public void UpdatePruningTimer_PoolGrowsBeyondMinPoolSize_EnablesTimer()
        {
            using var pool = ConstructPool(minPoolSize: 2, maxPoolSize: 10);
            var pruner = GetPruner(pool);

            // Pool starts empty, timer should be disabled
            AssertPrunerState(pruner, false, 0);

            // Add connections to grow beyond MinPoolSize
            FillPoolWithIdleConnections(pool, 3);

            // After growing beyond min, UpdatePruningTimer is called internally
            // and should enable the timer.
            AssertPrunerState(pruner, true, 0);
        }

        [Fact]
        public void UpdatePruningTimer_PoolAtMinPoolSize_TimerRemainsDisabled()
        {
            using var pool = ConstructPool(minPoolSize: 2, maxPoolSize: 10);
            var pruner = GetPruner(pool);

            // Add exactly MinPoolSize connections
            FillPoolWithIdleConnections(pool, 2);

            // Timer should stay disabled since we're at (not above) MinPoolSize
            AssertPrunerState(pruner, false, 0);
        }

        [Fact]
        public void UpdatePruningTimer_PoolShrinksBackToMin_DisablesTimerAndResetsSamples()
        {
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10);
            var pruner = GetPruner(pool);

            // Grow the pool to enable the timer
            FillPoolWithIdleConnections(pool, 3);
            AssertPrunerState(pruner, true, 0);

            // Now prune all connections back to 0 (MinPoolSize) by filling the sample buffer.
            // Each callback before the buffer fills records the current idle count (3) and
            // advances the sample index without pruning.
            int sampleSize = pruner.SampleSize;
            for (int i = 0; i < sampleSize - 1; i++)
            {
                pruner.OnPruningCallback(null);

                // After each non-final callback the timer stays enabled, the index advances,
                // and every previously recorded sample remains unchanged.
                int[] expectedSamples = new int[i + 1];
                for (int j = 0; j <= i; j++)
                {
                    expectedSamples[j] = 3;
                }
                AssertPrunerState(pruner, true, i + 1, expectedSamples);
            }

            // The final callback fills the buffer, takes the median (3), and prunes all idle
            // connections back to MinPoolSize (0). That disables the timer and resets the index.
            pruner.OnPruningCallback(null);
            AssertPrunerState(pruner, false, 0);
            Assert.Equal(0, pool.Count);
        }

        #endregion

        #region PruneIdleConnections Tests

        [Fact]
        public void PruneIdleConnections_BufferNotFull_CollectsSampleWithoutPruning()
        {
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10, idleTimeout: 30);
            var pruner = GetPruner(pool);
            // idleTimeout=30 → sample size = 30/10 = 3

            // Fill the pool so pruning timer is active
            FillPoolWithIdleConnections(pool, 5);
            int initialCount = pool.Count;

            // First invocation: records idle count in sample[0], buffer not full
            pruner.OnPruningCallback(null);

            // No connections should be pruned yet
            Assert.Equal(initialCount, pool.Count);
            AssertPrunerState(pruner, true, 1, 5);
        }

        [Fact]
        public void PruneIdleConnections_RespectsMinPoolSizeFloor()
        {
            // idleTimeout=20 → 2 samples
            using var pool = ConstructPool(minPoolSize: 5, maxPoolSize: 20, idleTimeout: 20);
            var pruner = GetPruner(pool);

            // Fill pool with 10 idle connections
            FillPoolWithIdleConnections(pool, 10);
            Assert.Equal(10, pool.Count);

            // Fill sample buffer and trigger pruning
            pruner.OnPruningCallback(null); // sample 1
            AssertPrunerState(pruner, true, 1, 10);

            pruner.OnPruningCallback(null); // sample 2 → prune
            // Pruning brings the pool down to MinPoolSize (5), so the pruner disables its timer.
            AssertPrunerState(pruner, false, 0);

            // Pool should not drop below MinPoolSize (5).
            // 10 idle connections, median of 2 samples (both 10) = 10, but floor is MinPoolSize=5.
            Assert.Equal(5, pool.Count);
        }

        [Fact]
        public void PruneIdleConnections_TimerDisabled_ReturnsEarlyWithoutPruning()
        {
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10, idleTimeout: 20);
            var pruner = GetPruner(pool);

            // Pool starts empty, timer is disabled. Calling prune should be a no-op.
            pruner.OnPruningCallback(null);

            Assert.Equal(0, pool.Count);
            AssertPrunerState(pruner, false, 0);
        }

        [Fact]
        public void PruneIdleConnections_DoesNotRemoveInUseConnections()
        {
            // idleTimeout=20 → 2 samples
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 20, idleTimeout: 20);
            var pruner = GetPruner(pool);

            // Get 5 connections and KEEP them checked out (in use)
            CheckOutConnections(pool, 5);

            // Also add 5 idle connections
            FillPoolWithIdleConnections(pool, 5);

            // Total = 10 (5 in-use + 5 idle)
            Assert.Equal(10, pool.Count);

            // Fill sample buffer and trigger pruning
            pruner.OnPruningCallback(null); // sample 1 (idle count = 5)
            AssertPrunerState(pruner, true, 1, 5);

            pruner.OnPruningCallback(null); // sample 2 → prune
            AssertPrunerState(pruner, true, 0);

            // In-use connections must not be removed.
            // 5 idle connections, median of 2 samples (both 5) = 5, all 5 idle pruned → 5 in-use remain.
            Assert.Equal(5, pool.Count);
        }

        #endregion

        #region Shutdown Tests

        [Fact]
        public void Shutdown_DisposesPruningTimer()
        {
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10);
            var pruner = GetPruner(pool);

            // Enable the timer by growing the pool beyond MinPoolSize
            FillPoolWithIdleConnections(pool, 3);
            AssertPrunerState(pruner, true, 0);

            pool.Shutdown();

            Assert.Equal(ShuttingDown, pool.State);
            // After shutdown, the timer-enabled flag must be cleared.
            AssertPrunerState(pruner, false, 0);
        }

        [Fact]
        public void Shutdown_NullTimer_DoesNotThrow()
        {
            // Fixed-size pool has no timer
            using var pool = ConstructPool(minPoolSize: 5, maxPoolSize: 5);
            Assert.Null(pool.Pruner);

            // Should not throw
            pool.Shutdown();
            Assert.Equal(ShuttingDown, pool.State);
        }

        [Fact]
        public void Shutdown_CalledMultipleTimes_IsIdempotent()
        {
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10);
            var pruner = GetPruner(pool);

            // Enable the timer so there is real state to tear down.
            FillPoolWithIdleConnections(pool, 3);
            AssertPrunerState(pruner, true, 0);

            pool.Shutdown();
            AssertPrunerState(pruner, false, 0);

            // A repeated shutdown plus an explicit pruner Dispose must be safe no-ops.
            // (The `using` block will also dispose the pool, calling Shutdown a third time.)
            pool.Shutdown();
            pruner.Dispose();

            Assert.Equal(ShuttingDown, pool.State);
            AssertPrunerState(pruner, false, 0);
        }

        #endregion

        #region DivideRoundingUp Tests

        [Theory]
        [InlineData(10, 10, 1)]
        [InlineData(11, 10, 2)]
        [InlineData(300, 10, 30)]
        [InlineData(1, 1, 1)]
        [InlineData(7, 3, 3)]  // ceil(7/3) = 3
        [InlineData(6, 3, 2)]  // exact division
        [InlineData(15, 10, 2)] // ceil(15/10) = 2
        public void DivideRoundingUp_ReturnsCorrectCeiling(int value, int divisor, int expected)
        {
            int result = PoolPruner.DivideRoundingUp(value, divisor);

            Assert.Equal(expected, result);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void PruneIdleConnections_AllIdleWithZeroMinPoolSize_PrunesEntirePool()
        {
            // Given: A pool with many idle connections and low recent usage.
            // When: The pruning interval elapses (sample buffer fills).
            // Then: The pool closes excess idle connections.

            // Use idleTimeout=20 for 2 samples (fast buffer fill)
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 50, idleTimeout: 20);
            var pruner = GetPruner(pool);

            // Simulate high load: open 20 connections
            FillPoolWithIdleConnections(pool, 20);
            Assert.Equal(20, pool.Count);

            // Pruning samples will both record idle=20, so toPrune=20.
            pruner.OnPruningCallback(null); // sample 1
            AssertPrunerState(pruner, true, 1, 20);

            pruner.OnPruningCallback(null); // sample 2 → prune
            // Pruning empties the pool to MinPoolSize (0), so the pruner disables its timer.
            AssertPrunerState(pruner, false, 0);

            // All 20 idle connections should be pruned since MinPoolSize is 0.
            // Both samples record idle=20, median=20, all pruned.
            Assert.Equal(0, pool.Count);
        }

        [Fact]
        public void PruneIdleConnections_VariedSamples_UsesMedianNotCurrentIdleCount()
        {
            // Given: A pool with high recent usage but currently many idle connections due to a brief lull.
            // When: The pruning interval elapses.
            // Then: Pruning uses sampled usage data (median) to avoid being too aggressive.

            // Use 3 samples (idleTimeout=30)
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 50, idleTimeout: 30);
            var pruner = GetPruner(pool);

            // Simulate varied usage:
            // Sample 1: 2 idle (high demand, most connections checked out)
            FillPoolWithIdleConnections(pool, 10);
            // Check out 8, leaving 2 idle
            var busyConnections = CheckOutConnections(pool, 8);
            pruner.OnPruningCallback(null); // sample[0] = 2 idle
            AssertPrunerState(pruner, true, 1, 2);
            Assert.Equal(10, pool.Count);

            // Sample 2: 2 idle (still high demand)
            pruner.OnPruningCallback(null); // sample[1] = 2 idle
            AssertPrunerState(pruner, true, 2, 2, 2);
            Assert.Equal(10, pool.Count);

            // Now return all busy connections, creating a brief lull (10 idle)
            ReturnConnections(pool, busyConnections);

            // Sample 3: 10 idle (lull). Buffer full → sorted=[2,2,10], median at index 1 = 2.
            int countBefore = pool.Count;
            pruner.OnPruningCallback(null);
            // Only 2 are pruned (10 → 8), so the pool stays above MinPoolSize and the timer
            // remains enabled; the index resets for the next window.
            AssertPrunerState(pruner, true, 0);

            // Pruning should remove exactly 2 connections (the median), not 10 (the current idle count).
            // This verifies that sampling prevents aggressive pruning during brief lulls.
            int pruned = countBefore - pool.Count;
            Assert.Equal(2, pruned);
        }

        [Fact]
        public void PruneIdleConnections_EvenSampleCountWithDistinctValues_UsesLowerMiddleSample()
        {
            // With an even number of samples the median index is the lower-middle element
            // (_medianIndex = DivideRoundingUp(sampleSize, 2) - 1). For 2 samples that is index 0.
            // Using distinct sample values proves the lower-middle is chosen rather than the
            // upper-middle or an average.

            // idleTimeout=20 → 2 samples
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 20, idleTimeout: 20);
            var pruner = GetPruner(pool);

            // Start with 8 idle connections.
            FillPoolWithIdleConnections(pool, 8);

            // Check out 4 so only 4 remain idle for the first sample.
            var busyConnections = CheckOutConnections(pool, 4);

            // Sample 1: 4 idle.
            pruner.OnPruningCallback(null);
            AssertPrunerState(pruner, true, 1, 4);
            Assert.Equal(8, pool.Count);

            // Return the 4 busy connections so 8 are idle for the second sample.
            ReturnConnections(pool, busyConnections);

            // Sample 2: 8 idle. Buffer full → sorted=[4,8], median at index 0 = 4.
            int countBefore = pool.Count;
            pruner.OnPruningCallback(null);
            AssertPrunerState(pruner, true, 0);

            // Exactly 4 (the lower-middle sample) should be pruned, not 8.
            int pruned = countBefore - pool.Count;
            Assert.Equal(4, pruned);
            Assert.Equal(4, pool.Count);
        }

        [Fact]
        public void PruneIdleConnections_SecondWindowAfterPrune_RecomputesMedianFromFreshSamples()
        {
            // After a window completes and prunes, the pruner re-arms and starts a fresh sampling
            // window. This test proves that the second window samples and prunes independently of
            // the first: it computes its own median from newly recorded samples and prunes the
            // correct amount, confirming the collect → prune → reset → collect cycle is repeatable.

            // idleTimeout=20 → 2 samples per window
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 20, idleTimeout: 20);
            var pruner = GetPruner(pool);

            // --- Window 1: produces a median of 4 ---
            FillPoolWithIdleConnections(pool, 12);
            // Check out 8, leaving 4 idle.
            var busyConnections = CheckOutConnections(pool, 8);

            pruner.OnPruningCallback(null); // sample[0] = 4 idle
            AssertPrunerState(pruner, true, 1, 4);

            pruner.OnPruningCallback(null); // buffer full [4,4], median 4, prune 4 idle
            // Window 1 done: timer stays enabled (pool still above min), index reset for window 2.
            AssertPrunerState(pruner, true, 0);
            Assert.Equal(8, pool.Count); // 8 busy remain, 0 idle

            // Return the 8 busy connections so they are idle for window 2.
            ReturnConnections(pool, busyConnections);
            Assert.Equal(8, pool.Count);

            // --- Window 2: 8 idle, produces its own median of 8 ---
            pruner.OnPruningCallback(null); // sample[0] = 8
            // The new window records a fresh sample (8), independent of window 1's median (4).
            AssertPrunerState(pruner, true, 1, 8);

            int countBefore = pool.Count;
            pruner.OnPruningCallback(null); // buffer full [8,8], median 8, prune 8
            AssertPrunerState(pruner, false, 0);

            // Window 2's median was 8 (its own samples), so all 8 idle connections are pruned.
            Assert.Equal(8, countBefore - pool.Count);
            Assert.Equal(0, pool.Count);
        }

        [Fact]
        public void PruneIdleConnections_TransientDemandSpikeWithinWindow_DoesNotUnderPrune()
        {
            // Complements VariedSamples (which proves a brief lull doesn't over-prune):
            // here a brief demand spike (a single low idle sample) must not drag the median down
            // and cause the pruner to under-prune. The sustained idle level should still be reclaimed.

            // idleTimeout=30 → 3 samples
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 50, idleTimeout: 30);
            var pruner = GetPruner(pool);

            FillPoolWithIdleConnections(pool, 10);

            // Sample 1: 10 idle (steady state).
            pruner.OnPruningCallback(null);
            AssertPrunerState(pruner, true, 1, 10);
            Assert.Equal(10, pool.Count);

            // Demand spike: check out 8, leaving only 2 idle for the middle sample.
            var busyConnections = CheckOutConnections(pool, 8);

            // Sample 2: 2 idle (transient spike).
            pruner.OnPruningCallback(null);
            AssertPrunerState(pruner, true, 2, 10, 2);
            Assert.Equal(10, pool.Count);

            // Spike subsides — all connections idle again.
            ReturnConnections(pool, busyConnections);

            // Sample 3: 10 idle. Buffer full → sorted=[2,10,10], median at index 1 = 10.
            int countBefore = pool.Count;
            pruner.OnPruningCallback(null);
            // Median prunes all 10 idle down to MinPoolSize (0), so the timer disables.
            AssertPrunerState(pruner, false, 0);

            // The transient spike (sample of 2) is ignored; the sustained level (10) is pruned.
            int pruned = countBefore - pool.Count;
            Assert.Equal(10, pruned);
            Assert.Equal(0, pool.Count);
        }

        [Fact]
        public void Pruning_PruneToMinThenRegrow_ReArmsAndPrunesAgain()
        {
            // Exercises the full prune → disable → regrow → re-arm → prune cycle. After a window
            // prunes the pool down to MinPoolSize the timer disables; opening new connections must
            // re-arm it, and the next window must sample and prune independently.

            // idleTimeout=20 → 2 samples
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 50, idleTimeout: 20);
            var pruner = GetPruner(pool);

            // --- Window 1: prune 6 idle down to min (0), disabling the timer ---
            FillPoolWithIdleConnections(pool, 6);

            pruner.OnPruningCallback(null); // sample[0] = 6
            AssertPrunerState(pruner, true, 1, 6);

            pruner.OnPruningCallback(null); // buffer full [6,6], median 6, prune 6 → Count 0
            AssertPrunerState(pruner, false, 0);
            Assert.Equal(0, pool.Count);

            // --- Regrow: opening connections must re-arm the disabled timer ---
            FillPoolWithIdleConnections(pool, 12);
            AssertPrunerState(pruner, true, 0);
            Assert.Equal(12, pool.Count);

            // --- Window 2: samples freshly and prunes 12 ---
            pruner.OnPruningCallback(null); // sample[0] = 12
            AssertPrunerState(pruner, true, 1, 12);

            int countBefore = pool.Count;
            pruner.OnPruningCallback(null); // buffer full [12,12], median 12, prune 12 → Count 0
            AssertPrunerState(pruner, false, 0);

            Assert.Equal(12, countBefore - pool.Count);
            Assert.Equal(0, pool.Count);
        }

        [Fact]
        public void OnPruningCallback_AfterShutdown_IsNoOp()
        {
            // A pruning timer callback can race with pool shutdown. Once Shutdown() has disposed the
            // pruner, a stray callback must be an inert no-op: it must not throw, re-enable the timer,
            // mutate the sample index, or resurrect connections.

            // idleTimeout=30 → 3 samples
            using var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 50, idleTimeout: 30);
            var pruner = GetPruner(pool);

            FillPoolWithIdleConnections(pool, 6);

            // Advance one sample so SampleIndex is non-zero before shutdown.
            pruner.OnPruningCallback(null);
            AssertPrunerState(pruner, true, 1, 6);

            pool.Shutdown();

            // Shutdown disposes the pruner (timer disabled) and drains all idle connections.
            // Dispose intentionally does NOT reset the sample index.
            Assert.False(pruner.IsTimerEnabled);
            Assert.Equal(0, pool.Count);
            int sampleIndexAfterShutdown = pruner.SampleIndex;

            // A stray timer callback after shutdown must be a no-op.
            Exception recorded = Record.Exception(() => pruner.OnPruningCallback(null));

            Assert.Null(recorded);
            Assert.False(pruner.IsTimerEnabled);
            Assert.Equal(sampleIndexAfterShutdown, pruner.SampleIndex);
            Assert.Equal(0, pool.Count);
        }

        #endregion

        #region Test classes

        internal class SuccessfulSqlConnectionFactory : SqlConnectionFactory
        {
            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
            {
                return new StubDbConnectionInternal();
            }
        }

        internal class StubDbConnectionInternal : DbConnectionInternal
        {
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

            internal override void ResetConnection()
            {
                return;
            }
        }

        #endregion
    }
}

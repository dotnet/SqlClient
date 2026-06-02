// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

        private ChannelDbConnectionPool ConstructPool(
            int minPoolSize = 0,
            int maxPoolSize = 50,
            int loadBalanceTimeout = 0)
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: minPoolSize,
                maxPoolSize: maxPoolSize,
                creationTimeout: 15,
                loadBalanceTimeout: loadBalanceTimeout,
                hasTransactionAffinity: true
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
        /// Opens <paramref name="count"/> connections and returns them to the pool so they are idle.
        /// </summary>
        private void FillPoolWithIdleConnections(ChannelDbConnectionPool pool, int count)
        {
            var connections = new DbConnectionInternal?[count];
            var owners = new SqlConnection[count];

            for (int i = 0; i < count; i++)
            {
                owners[i] = new SqlConnection();
                var completed = pool.TryGetConnection(owners[i], null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out connections[i]);
                Assert.True(completed);
                Assert.NotNull(connections[i]);
            }

            for (int i = 0; i < count; i++)
            {
                pool.ReturnInternalConnection(connections[i]!, owners[i]);
            }
        }

        #endregion

        #region Timer Creation / Configuration Tests

        [Fact]
        public void Constructor_MinPoolSizeLessThanMax_CreatesPruningTimer()
        {
            // When min < max, the pool can shrink so a pruning timer should be created.
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10);

            Assert.NotNull(pool.Pruner);
        }

        [Fact]
        public void Constructor_MinPoolSizeEqualsMax_DoesNotCreatePruningTimer()
        {
            // When min == max, the pool is fixed-size — pruning would never activate.
            var pool = ConstructPool(minPoolSize: 10, maxPoolSize: 10);

            Assert.Null(pool.Pruner);
        }

        [Fact]
        public void Constructor_PruningTimerStartsDisabled()
        {
            // Timer should be created but not armed (pool starts empty, below MinPoolSize threshold).
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10);

            Assert.False(pool.Pruner!.IsTimerEnabled);
        }

        [Theory]
        [InlineData(100, 10)] // 100 / 10 = 10 samples
        [InlineData(300, 30)] // 300 / 10 = 30 samples
        [InlineData(60, 6)]   // 60 / 10 = 6 samples
        [InlineData(15, 2)]   // 15 / 10 = 2 samples (rounds up)
        public void Constructor_CalculatesSampleSizeFromLoadBalanceTimeout(
            int loadBalanceTimeout, int expectedSampleSize)
        {
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10, loadBalanceTimeout: loadBalanceTimeout);

            Assert.Equal(expectedSampleSize, pool.Pruner!.SampleSize);
        }

        [Fact]
        public void Constructor_ZeroLoadBalanceTimeout_UsesDefaultLifetimeWindow()
        {
            // When LoadBalanceTimeout is 0, use DefaultLifetimeWindowSeconds (300) / 10 = 30 samples
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10, loadBalanceTimeout: 0);

            Assert.Equal(30, pool.Pruner!.SampleSize);
        }

        [Fact]
        public void Constructor_LargeLoadBalanceTimeout_ClampedToMaxPruningSampleSize()
        {
            // A very large LoadBalanceTimeout should be clamped to MaxPruningSampleSize (300)
            // to prevent excessive memory allocation. 10000 / 10 = 1000, clamped to 300.

            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10, loadBalanceTimeout: 10000);

            Assert.Equal(300, pool.Pruner!.SampleSize);
        }

        #endregion

        #region UpdatePruningTimer Tests

        [Fact]
        public void UpdatePruningTimer_PoolGrowsBeyondMinPoolSize_EnablesTimer()
        {
            var pool = ConstructPool(minPoolSize: 2, maxPoolSize: 10);

            // Pool starts empty, timer should be disabled
            Assert.False(pool.Pruner!.IsTimerEnabled);

            // Add connections to grow beyond MinPoolSize
            FillPoolWithIdleConnections(pool, 3);

            // After growing beyond min, UpdatePruningTimer is called internally
            // and should enable the timer.
            Assert.True(pool.Pruner.IsTimerEnabled);

            pool.Shutdown();
        }

        [Fact]
        public void UpdatePruningTimer_PoolAtMinPoolSize_TimerRemainsDisabled()
        {
            var pool = ConstructPool(minPoolSize: 2, maxPoolSize: 10);

            // Add exactly MinPoolSize connections
            FillPoolWithIdleConnections(pool, 2);

            // Timer should stay disabled since we're at (not above) MinPoolSize
            Assert.False(pool.Pruner!.IsTimerEnabled);
        }

        [Fact]
        public void UpdatePruningTimer_PoolShrinksBackToMin_DisablesTimerAndResetsSamples()
        {
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10);

            // Grow the pool to enable the timer
            FillPoolWithIdleConnections(pool, 3);
            Assert.True(pool.Pruner!.IsTimerEnabled);

            // Now prune all connections back to 0 (MinPoolSize)
            // Simulate by calling PruneIdleConnections enough times to fill the sample buffer
            // and then let it prune.
            int sampleSize = pool.Pruner.SampleSize;
            for (int i = 0; i < sampleSize; i++)
            {
                pool.Pruner.OnPruningCallback(null);
            }

            // After pruning removed connections back to MinPoolSize, UpdatePruningTimer
            // should have disabled the timer.
            Assert.False(pool.Pruner.IsTimerEnabled);
            Assert.Equal(0, pool.Pruner.SampleIndex);
            Assert.Equal(0, pool.Count);

            pool.Shutdown();
        }

        #endregion

        #region PruneIdleConnections Tests

        [Fact]
        public void PruneIdleConnections_BufferNotFull_CollectsSampleWithoutPruning()
        {
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10, loadBalanceTimeout: 30);
            // loadBalanceTimeout=30 → sample size = 30/10 = 3

            // Fill the pool so pruning timer is active
            FillPoolWithIdleConnections(pool, 5);
            int initialCount = pool.Count;

            // First invocation: records idle count in sample[0], buffer not full
            pool.Pruner!.OnPruningCallback(null);

            // No connections should be pruned yet
            Assert.Equal(initialCount, pool.Count);
            Assert.Equal(1, pool.Pruner.SampleIndex);

            pool.Shutdown();
        }

        [Fact]
        public void PruneIdleConnections_RespectsMinPoolSizeFloor()
        {
            // loadBalanceTimeout=20 → 2 samples
            var pool = ConstructPool(minPoolSize: 5, maxPoolSize: 20, loadBalanceTimeout: 20);

            // Fill pool with 10 idle connections
            FillPoolWithIdleConnections(pool, 10);
            Assert.Equal(10, pool.Count);

            // Fill sample buffer and trigger pruning
            pool.Pruner!.OnPruningCallback(null); // sample 1
            pool.Pruner.OnPruningCallback(null); // sample 2 → prune

            // Pool should not drop below MinPoolSize (5).
            // 10 idle connections, median of 2 samples (both 10) = 10, but floor is MinPoolSize=5.
            Assert.Equal(5, pool.Count);

            pool.Shutdown();
        }

        [Fact]
        public void PruneIdleConnections_TimerDisabled_ReturnsEarlyWithoutPruning()
        {
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10, loadBalanceTimeout: 20);

            // Pool starts empty, timer is disabled. Calling prune should be a no-op.
            pool.Pruner!.OnPruningCallback(null);

            Assert.Equal(0, pool.Count);
            Assert.Equal(0, pool.Pruner.SampleIndex);
        }

        [Fact]
        public void PruneIdleConnections_SampleBufferResetsAfterPruning()
        {
            // loadBalanceTimeout=20 → 2 samples
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 20, loadBalanceTimeout: 20);

            // Fill pool to enable pruning timer
            FillPoolWithIdleConnections(pool, 5);

            // Fill sample buffer and trigger pruning
            pool.Pruner!.OnPruningCallback(null); // sample index → 1
            pool.Pruner.OnPruningCallback(null); // buffer full, prune, reset index → 0

            // After pruning, sample index should be reset to 0
            Assert.Equal(0, pool.Pruner.SampleIndex);

            pool.Shutdown();
        }

        [Fact]
        public void PruneIdleConnections_DoesNotRemoveInUseConnections()
        {
            // loadBalanceTimeout=20 → 2 samples
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 20, loadBalanceTimeout: 20);

            // Get 5 connections and KEEP them checked out (in use)
            var owners = new SqlConnection[5];
            var connections = new DbConnectionInternal?[5];
            for (int i = 0; i < 5; i++)
            {
                owners[i] = new SqlConnection();
                var completed = pool.TryGetConnection(owners[i], null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out connections[i]);
                Assert.True(completed);
                Assert.NotNull(connections[i]);
            }

            // Also add 5 idle connections
            FillPoolWithIdleConnections(pool, 5);

            // Total = 10 (5 in-use + 5 idle)
            Assert.Equal(10, pool.Count);

            // Fill sample buffer and trigger pruning
            pool.Pruner!.OnPruningCallback(null); // sample 1 (idle count = 5)
            pool.Pruner.OnPruningCallback(null); // sample 2 → prune

            // In-use connections must not be removed.
            // 5 idle connections, median of 2 samples (both 5) = 5, all 5 idle pruned → 5 in-use remain.
            Assert.Equal(5, pool.Count);

            pool.Shutdown();
        }

        #endregion

        #region Shutdown Tests

        [Fact]
        public void Shutdown_DisposesPruningTimer()
        {
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 10);
            Assert.NotNull(pool.Pruner);

            // Enable the timer by growing the pool beyond MinPoolSize
            FillPoolWithIdleConnections(pool, 3);
            Assert.True(pool.Pruner.IsTimerEnabled);

            pool.Shutdown();

            Assert.Equal(ShuttingDown, pool.State);
            // After shutdown, the timer-enabled flag must be cleared.
            Assert.False(pool.Pruner.IsTimerEnabled);
        }

        [Fact]
        public void Shutdown_NullTimer_DoesNotThrow()
        {
            // Fixed-size pool has no timer
            var pool = ConstructPool(minPoolSize: 5, maxPoolSize: 5);
            Assert.Null(pool.Pruner);

            // Should not throw
            pool.Shutdown();
            Assert.Equal(ShuttingDown, pool.State);
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

            // Use loadBalanceTimeout=20 for 2 samples (fast buffer fill)
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 50, loadBalanceTimeout: 20);

            // Simulate high load: open 20 connections
            FillPoolWithIdleConnections(pool, 20);
            Assert.Equal(20, pool.Count);

            // Pruning samples will both record idle=20, so toPrune=20.
            // But pruning loop is bounded by pool.Count > MinPoolSize (0).
            pool.Pruner!.OnPruningCallback(null); // sample 1
            pool.Pruner.OnPruningCallback(null); // sample 2 → prune

            // All 20 idle connections should be pruned since MinPoolSize is 0.
            // Both samples record idle=20, median=20, all pruned.
            Assert.Equal(0, pool.Count);

            pool.Shutdown();
        }

        [Fact]
        public void PruneIdleConnections_VariedSamples_UsesMedianNotCurrentIdleCount()
        {
            // Given: A pool with high recent usage but currently many idle connections due to a brief lull.
            // When: The pruning interval elapses.
            // Then: Pruning uses sampled usage data (median) to avoid being too aggressive.

            // Use 3 samples (loadBalanceTimeout=30)
            var pool = ConstructPool(minPoolSize: 0, maxPoolSize: 50, loadBalanceTimeout: 30);

            // Simulate varied usage:
            // Sample 1: 2 idle (high demand, most connections checked out)
            FillPoolWithIdleConnections(pool, 10);
            // Check out 8, leaving 2 idle
            var busyOwners = new SqlConnection[8];
            var busyConns = new DbConnectionInternal?[8];
            for (int i = 0; i < 8; i++)
            {
                busyOwners[i] = new SqlConnection();
                var completed = pool.TryGetConnection(busyOwners[i], null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out busyConns[i]);
                Assert.True(completed);
                Assert.NotNull(busyConns[i]);
            }
            pool.Pruner!.OnPruningCallback(null); // sample[0] = 2 idle

            // Sample 2: 2 idle (still high demand)
            pool.Pruner.OnPruningCallback(null); // sample[1] = 2 idle

            // Now return all busy connections, creating a brief lull (10 idle)
            for (int i = 0; i < 8; i++)
            {
                pool.ReturnInternalConnection(busyConns[i]!, busyOwners[i]);
            }

            // Sample 3: 10 idle (lull). Buffer full → sorted=[2,2,10], median at index 1 = 2.
            int countBefore = pool.Count;
            pool.Pruner.OnPruningCallback(null);

            // Pruning should only remove 2 connections (the median), not 10 (the current idle count).
            // This verifies that sampling prevents aggressive pruning during brief lulls.
            int pruned = countBefore - pool.Count;
            Assert.True(pruned <= 2, $"Pruning was too aggressive: pruned {pruned} connections, " +
                                     $"expected at most 2 (median). Count before={countBefore}, after={pool.Count}");

            pool.Shutdown();
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

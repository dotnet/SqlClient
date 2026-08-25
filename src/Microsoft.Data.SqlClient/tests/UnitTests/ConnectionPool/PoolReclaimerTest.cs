// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

using static Microsoft.Data.SqlClient.UnitTests.ConnectionPool.PoolTestHarness;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Unit tests for <see cref="PoolReclaimer"/>, the demand-driven timer that sweeps emancipated
    /// connections for <see cref="ChannelDbConnectionPool"/>.
    /// </summary>
    /// <remarks>
    /// These are deliberately white-box: they drive the reclaimer's arm/disarm bookkeeping directly
    /// because it has no observable effect other than the reclamation it produces, and that is
    /// covered for both pool implementations by <see cref="DbConnectionPoolReclamationTest"/>.
    /// </remarks>
    public class PoolReclaimerTest
    {
        private static ChannelDbConnectionPool ConstructChannelPool(int maxPoolSize = 50)
            => (ChannelDbConnectionPool)ConstructPool(PoolImplementation.Channel, maxPoolSize: maxPoolSize);

        /// <summary>
        /// The timer is demand-driven: armed by the first parked caller and disarmed by the last one
        /// to leave, so a pool with no blocked callers never wakes the process.
        /// </summary>
        [Fact]
        public void EnterAndExitParkedWait_ArmsAndDisarmsTimer()
        {
            PoolReclaimer reclaimer = ConstructChannelPool().Reclaimer;

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
            PoolReclaimer reclaimer = ConstructChannelPool().Reclaimer;

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
            PoolReclaimer reclaimer = ConstructChannelPool().Reclaimer;

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
            ChannelDbConnectionPool pool = ConstructChannelPool(maxPoolSize: 1);
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
            ChannelDbConnectionPool pool = ConstructChannelPool(maxPoolSize: 1);
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
            ChannelDbConnectionPool pool = ConstructChannelPool();
            pool.Reclaimer.EnterParkedWait();
            Assert.True(pool.Reclaimer.IsTimerEnabled);

            pool.Shutdown();

            Assert.False(pool.Reclaimer.IsTimerEnabled);

            // Arming after disposal must be a no-op rather than resurrecting the timer.
            pool.Reclaimer.EnterParkedWait();
            Assert.False(pool.Reclaimer.IsTimerEnabled);
        }
    }
}

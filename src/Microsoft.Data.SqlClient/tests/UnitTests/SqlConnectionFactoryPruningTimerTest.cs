// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Unit tests for the demand-driven pruning timer in <see cref="SqlConnectionFactory"/>.
    ///
    /// The factory is a process-wide singleton, so before
    /// https://github.com/dotnet/SqlClient/issues/1881 its pruning timer was armed in the
    /// constructor and never stopped, waking the process every 30 seconds for the lifetime of the
    /// process even when there was nothing left to prune. These tests pin the arm/disarm state
    /// machine that replaced it.
    /// </summary>
    public class SqlConnectionFactoryPruningTimerTest
    {
        /// <summary>
        /// Passes needed to drain a pool group that never had a pool created in it (no test here
        /// opens a connection): 1) Active -> Idle, 2) Idle -> Disabled, which returns true in the
        /// same pass and queues the group for release, 3) released from _poolGroupsToRelease.
        ///
        /// Three rather than four because Disabled and queued happen together, and the release
        /// loop runs before the pool-group loop within a pass. Deterministic: tests drive
        /// <see cref="SqlConnectionFactory.RunPruningPass"/> synchronously.
        /// </summary>
        private const int ExpectedDrainPasses = 3;

        /// <summary>
        /// Hang guard, not an expectation: a regression that leaves the timer armed should fail an
        /// assertion rather than loop forever.
        /// </summary>
        private const int MaxPruningPasses = 10;

        #region Helpers

        /// <summary>
        /// A factory whose timer never actually fires on its own, so tests drive pruning
        /// deterministically through <see cref="SqlConnectionFactory.RunPruningPass"/> instead of
        /// racing a background thread.
        /// </summary>
        private sealed class TestSqlConnectionFactory : SqlConnectionFactory
        {
            internal TestSqlConnectionFactory()
                : base(TimeSpan.FromDays(1), TimeSpan.FromDays(1))
            {
            }

            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
                => throw new NotSupportedException("These tests never open a connection.");
        }

        private static DbConnectionPoolGroup AddPoolGroup(
            SqlConnectionFactory factory,
            string connectionString)
        {
            SqlConnectionOptions userOptions = null!;
            var key = new ConnectionPoolKey(
                connectionString,
                credential: null,
                accessToken: null,
                accessTokenCallback: null,
                sspiContextProvider: null);

            return factory.GetConnectionPoolGroup(key, poolOptions: null, ref userOptions);
        }

        /// <summary>
        /// Runs pruning passes until the timer disarms itself, or until
        /// <see cref="MaxPruningPasses"/> is exhausted.
        /// </summary>
        /// <returns>The number of passes executed.</returns>
        private static int DrainPruningWork(SqlConnectionFactory factory)
        {
            int passes = 0;
            while (factory.IsPruningTimerActive && passes < MaxPruningPasses)
            {
                factory.RunPruningPass();
                passes++;
            }

            return passes;
        }

        #endregion

        /// <summary>
        /// The timer must start disarmed. A process that creates the factory but never opens a
        /// connection should not be woken at all.
        /// </summary>
        [Fact]
        public void PruningTimer_IsDisarmed_WhenFactoryIsConstructed()
        {
            var factory = new TestSqlConnectionFactory();

            Assert.False(factory.IsPruningTimerActive);
        }

        /// <summary>
        /// Registering a pool group creates work for the pruner, so the timer must arm.
        /// </summary>
        [Fact]
        public void PruningTimer_IsArmed_WhenPoolGroupIsRegistered()
        {
            var factory = new TestSqlConnectionFactory();

            Assert.NotNull(AddPoolGroup(factory, "Data Source=localhost;"));

            Assert.True(factory.IsPruningTimerActive);
        }

        /// <summary>
        /// The regression test for #1881: the timer must stay armed for every pass of the drain so
        /// pruning cannot stall half-drained, then disarm once there is nothing left to prune
        /// instead of firing forever with nothing to do.
        /// </summary>
        [Fact]
        public void PruningTimer_StaysArmedUntilAllWorkDrains()
        {
            var factory = new TestSqlConnectionFactory();
            AddPoolGroup(factory, "Data Source=localhost;");
            Assert.True(factory.IsPruningTimerActive);

            for (int pass = 1; pass < ExpectedDrainPasses; pass++)
            {
                factory.RunPruningPass();

                Assert.True(
                    factory.IsPruningTimerActive,
                    $"Timer disarmed after pass {pass}, but the pool group had not drained yet.");
            }

            factory.RunPruningPass();

            Assert.False(factory.IsPruningTimerActive);
        }

        /// <summary>
        /// Disarming must not be a one-way door: new connection activity has to bring pruning
        /// back, otherwise the fix would simply have disabled pruning.
        /// </summary>
        [Fact]
        public void PruningTimer_IsRearmed_WhenNewPoolGroupIsRegisteredAfterDraining()
        {
            var factory = new TestSqlConnectionFactory();
            AddPoolGroup(factory, "Data Source=localhost;");
            int passes = DrainPruningWork(factory);
            Assert.False(factory.IsPruningTimerActive);
            Assert.Equal(ExpectedDrainPasses, passes);

            AddPoolGroup(factory, "Data Source=otherhost;");

            Assert.True(factory.IsPruningTimerActive);
        }
    }
}

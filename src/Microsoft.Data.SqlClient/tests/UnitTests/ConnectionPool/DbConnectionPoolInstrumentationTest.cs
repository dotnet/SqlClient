// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Diagnostics;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Extensions.Time.Testing;
using Xunit;

using static Microsoft.Data.SqlClient.UnitTests.ConnectionPool.ChannelDbConnectionPoolTest;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Verifies the diagnostic instrumentation shared by <see cref="ChannelDbConnectionPool"/> and
    /// <see cref="WaitHandleDbConnectionPool"/>: the pool metrics counters emitted across the
    /// connection lifecycle, parameterized over both pool implementations via
    /// <see cref="PoolImplementation"/> wherever their behavior is expected to match.
    /// </summary>
    public class DbConnectionPoolInstrumentationTest
    {
        /// <summary>
        /// Identifies which pool implementation a parameterized metric test exercises.
        /// </summary>
        public enum PoolImplementation
        {
            /// <summary>The legacy <see cref="WaitHandleDbConnectionPool"/>.</summary>
            WaitHandle,

            /// <summary>The <see cref="ChannelDbConnectionPool"/>.</summary>
            Channel,
        }

        /// <summary>
        /// Builds the pool group shared by both pool implementations.
        /// </summary>
        private static DbConnectionPoolGroup ConstructPoolGroup(
            string connectionString,
            int maxPoolSize,
            int minPoolSize,
            int idleTimeout)
        {
            DbConnectionPoolGroupOptions poolGroupOptions = new(
                poolByIdentity: false,
                minPoolSize: minPoolSize,
                maxPoolSize: maxPoolSize,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: idleTimeout);

            return new DbConnectionPoolGroup(
                new SqlConnectionOptions(connectionString),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions);
        }

        /// <summary>
        /// Builds the requested pool implementation behind the shared pool interface, reporting to
        /// the supplied metrics instance.
        /// </summary>
        private static IDbConnectionPool ConstructPool(
            PoolImplementation implementation,
            ISqlClientMetrics metrics,
            SqlConnectionFactory connectionFactory,
            string connectionString = "Data Source=localhost;",
            int maxPoolSize = 50,
            int minPoolSize = 0,
            int idleTimeout = 0)
        {
            DbConnectionPoolGroup poolGroup = ConstructPoolGroup(connectionString, maxPoolSize, minPoolSize, idleTimeout);

            return implementation switch
            {
                PoolImplementation.WaitHandle => new WaitHandleDbConnectionPool(
                    connectionFactory,
                    poolGroup,
                    DbConnectionPoolIdentity.NoIdentity,
                    new DbConnectionPoolProviderInfo(),
                    timeProvider: new FakeTimeProvider(),
                    metrics: metrics),

                PoolImplementation.Channel => new ChannelDbConnectionPool(
                    connectionFactory,
                    poolGroup,
                    DbConnectionPoolIdentity.NoIdentity,
                    new DbConnectionPoolProviderInfo(),
                    connectionCreationRateLimiter: null,
                    timeProvider: new FakeTimeProvider(),
                    metrics: metrics),

                _ => throw new ArgumentOutOfRangeException(nameof(implementation)),
            };
        }

        /// <summary>
        /// Asserts the exact value of every counter a pool is responsible for. Any counter not named
        /// by the caller is expected to be zero, so an unexpected emission fails the test.
        /// </summary>
        /// <remarks>
        /// <paramref name="activeConnections"/> is a parameter, not derived from the soft-connect
        /// counters, precisely because it is not guaranteed to move in lockstep with them: this is
        /// the counter https://github.com/dotnet/SqlClient/issues/3640 broke by deactivating a
        /// transacted connection twice on its way back to general circulation, which decremented
        /// activeConnections an extra time without touching softConnects/softDisconnects at all.
        /// </remarks>
        private static void AssertCounters(
            FakeSqlClientMetrics metrics,
            long hardConnects = 0,
            long hardDisconnects = 0,
            long softConnects = 0,
            long softDisconnects = 0,
            long pooledConnections = 0,
            long freeConnections = 0,
            long reclaimedConnections = 0,
            long activeConnections = 0)
        {
            (string Name, long Expected, long Actual)[] counters =
            {
                ("hardConnects", hardConnects, metrics.HardConnects),
                ("hardDisconnects", hardDisconnects, metrics.HardDisconnects),
                ("activeHardConnections", hardConnects - hardDisconnects, metrics.ActiveHardConnections),
                ("softConnects", softConnects, metrics.SoftConnects),
                ("softDisconnects", softDisconnects, metrics.SoftDisconnects),
                ("activeSoftConnections", softConnects - softDisconnects, metrics.ActiveSoftConnections),
                ("pooledConnections", pooledConnections, metrics.PooledConnections),
                ("freeConnections", freeConnections, metrics.FreeConnections),
                ("reclaimedConnections", reclaimedConnections, metrics.ReclaimedConnections),
                ("activeConnections", activeConnections, metrics.ActiveConnections),

                // Not emitted through a pool's metrics instance, so any non-zero value here means a
                // counter has moved to the pool that the tests have not accounted for.
                ("nonPooledConnections", 0, metrics.NonPooledConnections),
                ("activeConnectionPoolGroups", 0, metrics.ActiveConnectionPoolGroups),
                ("inactiveConnectionPoolGroups", 0, metrics.InactiveConnectionPoolGroups),
                ("activeConnectionPools", 0, metrics.ActiveConnectionPools),
                ("inactiveConnectionPools", 0, metrics.InactiveConnectionPools),
                ("stasisConnections", 0, metrics.StasisConnections),
            };

            // Reported as a single message listing only the counters that differ. Asserting on a
            // collection instead would be truncated by the assertion formatter, which hides the one
            // counter the test is about.
            List<string> mismatches = new();
            foreach ((string name, long expected, long actual) in counters)
            {
                if (actual != expected)
                {
                    mismatches.Add($"{name}: expected {expected}, actual {actual}");
                }
            }

            Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
        }

        /// <summary>
        /// Verifies the counters emitted when the pool creates a physical connection to satisfy a
        /// request, covering Story 2 scenario 1.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle)]
        [InlineData(PoolImplementation.Channel)]
        public void NewConnection_CountsHardConnectAndPooledConnection(PoolImplementation implementation)
        {
            // Arrange
            FakeSqlClientMetrics metrics = new();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new SuccessfulSqlConnectionFactory(metrics));
            SqlConnection owner = new();

            // Act
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));

            // Assert - the connection was handed straight to the caller, so it never became free.
            Assert.NotNull(connection);
            AssertCounters(
                metrics,
                hardConnects: 1,
                softConnects: 1,
                pooledConnections: 1,
                activeConnections: 1);
        }

        /// <summary>
        /// Verifies the counters emitted when a request is satisfied from the idle pool rather than
        /// by creating a connection, covering Story 2 scenario 3.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle)]
        [InlineData(PoolImplementation.Channel)]
        public void IdleConnectionReuse_CountsSoftConnect(PoolImplementation implementation)
        {
            // Arrange
            FakeSqlClientMetrics metrics = new();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new SuccessfulSqlConnectionFactory(metrics));
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
            Assert.NotNull(connection);
            pool.ReturnInternalConnection(connection!, owner);

            // Act
            Assert.True(pool.TryGetConnection(new SqlConnection(), null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? reused));

            // Assert - the second request was served without a second physical connect.
            Assert.Same(connection, reused);
            AssertCounters(
                metrics,
                hardConnects: 1,
                softConnects: 2,
                softDisconnects: 1,
                pooledConnections: 1,
                activeConnections: 1);
        }

        /// <summary>
        /// Verifies the counters emitted when a connection is returned to the idle pool, covering
        /// Story 2 scenario 4.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle)]
        [InlineData(PoolImplementation.Channel)]
        public void Return_CountsSoftDisconnect(PoolImplementation implementation)
        {
            // Arrange
            FakeSqlClientMetrics metrics = new();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new SuccessfulSqlConnectionFactory(metrics));
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
            Assert.NotNull(connection);

            // Act
            pool.ReturnInternalConnection(connection!, owner);

            // Assert - the connection is still pooled, and is now also free.
            AssertCounters(
                metrics,
                hardConnects: 1,
                softConnects: 1,
                softDisconnects: 1,
                pooledConnections: 1,
                freeConnections: 1);
        }

        /// <summary>
        /// Verifies the counters emitted when a physical connection is destroyed, covering Story 2
        /// scenario 2.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle)]
        [InlineData(PoolImplementation.Channel)]
        public void Destroy_CountsHardDisconnect(PoolImplementation implementation)
        {
            // Arrange
            FakeSqlClientMetrics metrics = new();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new SuccessfulSqlConnectionFactory(metrics));
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
            Assert.NotNull(connection);
            pool.ReturnInternalConnection(connection!, owner);

            // Act
            pool.Clear();

            // Assert - every gauge the connection contributed to is back to zero.
            AssertCounters(
                metrics,
                hardConnects: 1,
                hardDisconnects: 1,
                softConnects: 1,
                softDisconnects: 1);
        }

        /// <summary>
        /// Verifies the counters emitted when a checked-out connection is replaced. Both pools swap
        /// the new connection in for the old one, so the caller's checkout is unchanged: the
        /// replacement is a soft connect and retiring the old connection is a soft disconnect plus
        /// a hard disconnect.
        /// </summary>
        /// <remarks>
        /// Neither pool changes the pooled-connection gauge: the channel pool reuses the old
        /// connection's slot, and the wait handle pool swaps the replacement into the old
        /// connection's place in its object list.
        /// </remarks>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle)]
        [InlineData(PoolImplementation.Channel)]
        public void ReplaceConnection_CountsDisconnectsForDiscardedConnection(PoolImplementation implementation)
        {
            // Arrange
            FakeSqlClientMetrics metrics = new();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new SuccessfulSqlConnectionFactory(metrics));
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? oldConnection));
            Assert.NotNull(oldConnection);

            // Act
            DbConnectionInternal newConnection = pool.ReplaceConnection(owner, oldConnection!, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

            // Assert - two physical connections were opened and one was retired. The caller still
            // holds exactly one connection, so the soft connect for the replacement is balanced by
            // a soft disconnect for the connection it displaced.
            Assert.NotSame(oldConnection, newConnection);
            AssertCounters(
                metrics,
                hardConnects: 2,
                hardDisconnects: 1,
                softConnects: 2,
                softDisconnects: 1,
                pooledConnections: 1,
                activeConnections: 1);
        }

        /// <summary>
        /// Regression test for https://github.com/dotnet/SqlClient/issues/3640. Returning a
        /// connection while it is still enlisted in a transaction parks it in the transacted
        /// store rather than handing it back for reuse, but the connection is still deactivated
        /// exactly once. When the transaction later ends and the connection rejoins the idle
        /// pool, that hand-off must not deactivate it a second time, or activeConnections is
        /// decremented twice for a single logical checkout.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle)]
        [InlineData(PoolImplementation.Channel)]
        public void TransactionCommit_ReleasingParkedConnection_KeepsActiveConnectionCountBalanced(PoolImplementation implementation)
        {
            // Arrange
            FakeSqlClientMetrics metrics = new();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new SuccessfulSqlConnectionFactory(metrics));

            using (TransactionScope scope = new())
            {
                SqlConnection owner = new();
                Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
                Assert.NotNull(connection);

                // Act - the connection is still enlisted, so it is parked rather than reused.
                pool.ReturnInternalConnection(connection!, owner);

                // Assert - the park deactivates the connection exactly once.
                AssertCounters(
                    metrics,
                    hardConnects: 1,
                    softConnects: 1,
                    softDisconnects: 1,
                    pooledConnections: 1,
                    freeConnections: 1,
                    activeConnections: 0);

                scope.Complete();
            }

            // Assert - once the transaction commits and the connection rejoins the idle pool, the
            // counters must be unchanged from the park above. If the #3640 bug pattern were
            // reintroduced (deactivating on the way out of the transacted store, rather than
            // resetting), activeConnections would go negative here.
            AssertCounters(
                metrics,
                hardConnects: 1,
                softConnects: 1,
                softDisconnects: 1,
                pooledConnections: 1,
                freeConnections: 1,
                activeConnections: 0);
        }

        /// <summary>
        /// Same regression as <see cref="TransactionCommit_ReleasingParkedConnection_KeepsActiveConnectionCountBalanced"/>,
        /// but the transaction is rolled back rather than committed. Rollback and commit both end
        /// the transaction through the same completion callback, so this covers that the fix does
        /// not depend on the outcome of the transaction.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle)]
        [InlineData(PoolImplementation.Channel)]
        public void TransactionRollback_ReleasingParkedConnection_KeepsActiveConnectionCountBalanced(PoolImplementation implementation)
        {
            // Arrange
            FakeSqlClientMetrics metrics = new();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new SuccessfulSqlConnectionFactory(metrics));

            using (TransactionScope scope = new())
            {
                SqlConnection owner = new();
                Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
                Assert.NotNull(connection);

                pool.ReturnInternalConnection(connection!, owner);

                AssertCounters(
                    metrics,
                    hardConnects: 1,
                    softConnects: 1,
                    softDisconnects: 1,
                    pooledConnections: 1,
                    freeConnections: 1,
                    activeConnections: 0);

                // Act - scope disposed without Complete(), rolling the transaction back.
            }

            // Assert - same expectation as the commit case: no extra deactivation on rollback.
            AssertCounters(
                metrics,
                hardConnects: 1,
                softConnects: 1,
                softDisconnects: 1,
                pooledConnections: 1,
                freeConnections: 1,
                activeConnections: 0);
        }

        /// <summary>
        /// Verifies that a connection which opens successfully but fails to activate leaves the
        /// counters balanced. The pool counts the checkout before activating, so the soft
        /// disconnect emitted when the failed connection is returned has a matching soft connect
        /// and the active-soft-connects gauge does not go negative.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle)]
        [InlineData(PoolImplementation.Channel)]
        public void FailedActivation_LeavesCountersBalanced(PoolImplementation implementation)
        {
            // Arrange
            FakeSqlClientMetrics metrics = new();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new ActivationFailingConnectionFactory(metrics));
            SqlConnection owner = new();

            // Act
            Assert.Throws<TestConnectionCreateException>(() =>
                pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));

            // Assert - the physical connection was opened and stays in the pool, and the checkout
            // that failed is balanced by the return.
            AssertCounters(
                metrics,
                hardConnects: 1,
                softConnects: 1,
                softDisconnects: 1,
                pooledConnections: 1,
                freeConnections: 1,
                activeConnections: 0);
        }

        /// <summary>
        /// Drives concurrent checkouts and returns through the pool and verifies that every gauge
        /// settles back to a consistent resting state. This is the broad safety net for the
        /// counters: it does not assert a specific interleaving, only that nothing is leaked or
        /// double-counted once all activity has stopped.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle)]
        [InlineData(PoolImplementation.Channel)]
        public void ConcurrentCheckoutAndReturn_SettlesAllGaugesToZero(PoolImplementation implementation)
        {
            // Arrange
            const int Threads = 8;
            const int IterationsPerThread = 50;
            const int MaxPoolSize = 4;

            FakeSqlClientMetrics metrics = new();
            IDbConnectionPool pool = ConstructPool(
                implementation,
                metrics,
                new SuccessfulSqlConnectionFactory(metrics),
                maxPoolSize: MaxPoolSize);

            // Act
            Task[] workers = Enumerable.Range(0, Threads).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < IterationsPerThread; i++)
                {
                    SqlConnection owner = new();
                    Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(30)), out DbConnectionInternal? connection));
                    Assert.NotNull(connection);
                    pool.ReturnInternalConnection(connection!, owner);
                }
            })).ToArray();

            Assert.True(Task.WaitAll(workers, TimeSpan.FromSeconds(60)), "Concurrent checkout workers did not finish in time.");

            // Assert - every connection handed out came back, so no checkout is still outstanding.
            long expectedCheckouts = Threads * IterationsPerThread;
            Assert.Equal(expectedCheckouts, metrics.SoftConnects);
            Assert.Equal(expectedCheckouts, metrics.SoftDisconnects);
            Assert.Equal(0, metrics.ActiveConnections);

            // Every physical connection that was opened is either still pooled or was destroyed.
            Assert.Equal(metrics.PooledConnections, metrics.HardConnects - metrics.HardDisconnects);

            // Nothing is checked out, so every pooled connection is sitting idle.
            Assert.Equal(metrics.PooledConnections, metrics.FreeConnections);

            // The pool never grew past its configured ceiling.
            Assert.InRange(metrics.PooledConnections, 0, MaxPoolSize);
        }

        /// <summary>
        /// Verifies the counters emitted when idle connections are pruned out of the pool. Pruning
        /// destroys the physical connection, so it must decrement both the pooled and free gauges
        /// and record a hard disconnect, without touching the soft counters: pruning removes an
        /// idle connection that no caller holds.
        /// </summary>
        [Fact]
        public void PruneConnections_CountsHardDisconnectForEachEvictedConnection()
        {
            // Arrange - check out three connections so they are all open at once, then return them
            // so they sit idle and are eligible for pruning.
            FakeSqlClientMetrics metrics = new();
            ChannelDbConnectionPool pool = (ChannelDbConnectionPool)ConstructPool(
                PoolImplementation.Channel,
                metrics,
                new SuccessfulSqlConnectionFactory(metrics));

            List<(SqlConnection Owner, DbConnectionInternal Connection)> checkedOut = new();
            for (int i = 0; i < 3; i++)
            {
                SqlConnection owner = new();
                Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
                checkedOut.Add((owner, connection!));
            }

            foreach ((SqlConnection owner, DbConnectionInternal connection) in checkedOut)
            {
                pool.ReturnInternalConnection(connection, owner);
            }

            AssertCounters(
                metrics,
                hardConnects: 3,
                softConnects: 3,
                softDisconnects: 3,
                pooledConnections: 3,
                freeConnections: 3,
                activeConnections: 0);

            // Act - prune two of the three idle connections.
            pool.PruneConnections(2);

            // Assert - the two evicted connections were destroyed, and pruning did not disturb the
            // soft counters because nothing was checked out or returned.
            Assert.Equal(1, pool.Count);
            AssertCounters(
                metrics,
                hardConnects: 3,
                hardDisconnects: 2,
                softConnects: 3,
                softDisconnects: 3,
                pooledConnections: 1,
                freeConnections: 1,
                activeConnections: 0);
        }

        #region Test classes

        /// <summary>
        /// Distinctive exception type used to prove that the exact failure recorded by the pool is
        /// the one raised by the connection under test.
        /// </summary>
        internal sealed class TestConnectionCreateException : Exception
        {
            internal TestConnectionCreateException()
                : base("Simulated connection activation failure.")
            {
            }
        }

        /// <summary>
        /// Stub connection whose activation always fails, so a test can exercise the pool's
        /// behavior when a connection is successfully opened but cannot be handed to the caller.
        /// </summary>
        private sealed class ActivationFailingConnection : StubDbConnectionInternal
        {
            internal ActivationFailingConnection(ISqlClientMetrics metrics)
                : base(metrics)
            {
            }

            protected override void Activate(Transaction transaction)
                => throw new TestConnectionCreateException();
        }

        /// <summary>
        /// Connection factory whose connections open successfully but always fail to activate.
        /// </summary>
        private sealed class ActivationFailingConnectionFactory : SqlConnectionFactory
        {
            internal ActivationFailingConnectionFactory(ISqlClientMetrics metrics)
                : base(metrics)
            {
            }

            /// <inheritdoc />
            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
                => new ActivationFailingConnection(Metrics);
        }

        #endregion
    }
}

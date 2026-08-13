// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.RateLimiting;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Diagnostics;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

using static Microsoft.Data.SqlClient.UnitTests.ConnectionPool.ChannelDbConnectionPoolTest;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Verifies the diagnostic instrumentation of <see cref="ChannelDbConnectionPool"/>: the pool
    /// metrics counters emitted across the connection lifecycle.
    /// </summary>
    public class ChannelDbConnectionPoolInstrumentationTest
    {
        /// <summary>
        /// Builds a pool for instrumentation tests. Defaults mirror
        /// <see cref="ChannelDbConnectionPoolTest"/> so behavior is comparable across suites.
        /// </summary>
        /// <param name="connectionFactory">The factory used to create physical connections.</param>
        /// <param name="connectionString">Connection string backing the pool group. Tests override
        /// it to control the Pool Blocking Period.</param>
        /// <param name="maxPoolSize">Maximum pool size.</param>
        /// <param name="minPoolSize">Minimum pool size.</param>
        /// <param name="idleTimeout">Connection Idle Timeout, in seconds.</param>
        /// <param name="connectionCreationRateLimiter">Optional limiter throttling physical creates.</param>
        private static ChannelDbConnectionPool ConstructPool(
            SqlConnectionFactory connectionFactory,
            string connectionString = "Data Source=localhost;",
            int maxPoolSize = 50,
            int minPoolSize = 0,
            int idleTimeout = 0,
            ConcurrencyLimiter? connectionCreationRateLimiter = null)
            => new(
                connectionFactory,
                ConstructPoolGroup(connectionString, maxPoolSize, minPoolSize, idleTimeout),
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo(),
                connectionCreationRateLimiter);

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

        #region Metric parity

        // Each test gives its pool its own metrics instance, so the counters observe only that
        // pool's activity and can be asserted exactly. They run against both pool implementations
        // so that any divergence in what the channel pool emits shows up as a failing test rather
        // than as a silent telemetry gap.
        //
        // Counters emitted outside the pool are still process-wide: DbConnectionInternal reports
        // active-connection and stasis counts against the global instance, so those are not
        // asserted here.

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
                    timeProvider: null,
                    metrics: metrics),

                PoolImplementation.Channel => new ChannelDbConnectionPool(
                    connectionFactory,
                    poolGroup,
                    DbConnectionPoolIdentity.NoIdentity,
                    new DbConnectionPoolProviderInfo(),
                    connectionCreationRateLimiter: null,
                    timeProvider: null,
                    metrics: metrics),

                _ => throw new ArgumentOutOfRangeException(nameof(implementation)),
            };
        }

        /// <summary>
        /// Asserts the exact value of every counter a pool is responsible for. Any counter not named
        /// by the caller is expected to be zero, so an unexpected emission fails the test.
        /// </summary>
        /// <remarks>
        /// The active-connection gauges are not parameters because they are mechanically derived:
        /// each connect increments one and the matching disconnect decrements it.
        /// </remarks>
        private static void AssertCounters(
            FakeSqlClientMetrics metrics,
            long hardConnects = 0,
            long hardDisconnects = 0,
            long softConnects = 0,
            long softDisconnects = 0,
            long pooledConnections = 0,
            long freeConnections = 0,
            long reclaimedConnections = 0)
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

                // Not emitted through a pool's metrics instance, so any non-zero value here means a
                // counter has moved to the pool that the tests have not accounted for.
                ("nonPooledConnections", 0, metrics.NonPooledConnections),
                ("activeConnectionPoolGroups", 0, metrics.ActiveConnectionPoolGroups),
                ("inactiveConnectionPoolGroups", 0, metrics.InactiveConnectionPoolGroups),
                ("activeConnectionPools", 0, metrics.ActiveConnectionPools),
                ("inactiveConnectionPools", 0, metrics.InactiveConnectionPools),
                ("activeConnections", 0, metrics.ActiveConnections),
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
                pooledConnections: 1);
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
                pooledConnections: 1);
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
        /// Verifies the counters emitted when a checked-out connection is replaced. The channel pool
        /// swaps the new connection into the old connection's slot, so the pooled-connection gauge
        /// is deliberately left untouched.
        /// </summary>
        /// <remarks>
        /// This is not parameterized over the wait handle pool: that implementation disposes the
        /// replaced connection directly rather than through its destroy path, so it emits neither
        /// the hard disconnect nor the pooled-connection decrement.
        /// </remarks>
        [Fact]
        public void ReplaceConnection_CountsHardDisconnectForDiscardedConnection()
        {
            // Arrange
            FakeSqlClientMetrics metrics = new();
            IDbConnectionPool pool = ConstructPool(PoolImplementation.Channel, metrics, new SuccessfulSqlConnectionFactory(metrics));
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? oldConnection));
            Assert.NotNull(oldConnection);

            // Act
            DbConnectionInternal newConnection = pool.ReplaceConnection(owner, oldConnection!, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

            // Assert - two physical connections were opened and one was retired, leaving the pooled
            // count unchanged because the replacement inherited the slot.
            Assert.NotSame(oldConnection, newConnection);
            AssertCounters(
                metrics,
                hardConnects: 2,
                hardDisconnects: 1,
                softConnects: 2,
                pooledConnections: 1);
        }

        #endregion

        #region Test classes

        /// <summary>
        /// Distinctive exception type used to prove that the exact failure recorded by the pool is
        /// the one attached to the pooled-open timeout.
        /// </summary>
        internal sealed class TestConnectionCreateException : Exception
        {
            internal TestConnectionCreateException()
                : base("Simulated physical connection failure.")
            {
            }
        }

        /// <summary>
        /// Connection factory that always fails with <see cref="TestConnectionCreateException"/>.
        /// </summary>
        internal sealed class FailingSqlConnectionFactory : SqlConnectionFactory
        {
            /// <inheritdoc />
            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
                => throw new TestConnectionCreateException();
        }

        #endregion
    }
}

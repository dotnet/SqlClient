// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
    /// Verifies the diagnostic instrumentation of <see cref="ChannelDbConnectionPool"/>: the pooler
    /// trace events emitted across the connection lifecycle.
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

        #region Trace parity

        /// <summary>
        /// Verifies that the pool traces its own construction, so a trace capture can attribute
        /// every later pool-scoped event to a pool whose creation it observed.
        /// </summary>
        [Fact]
        public void Construction_EmitsConstructedTrace()
        {
            // Arrange
            using PoolerTraceListener listener = new();

            // Act
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory());

            // Assert
            TraceAssert.Contains("Constructed.", listener.MessagesForPool(pool.Id));
        }

        /// <summary>
        /// Verifies that creating a new physical connection is traced, covering Story 1 scenario 2.
        /// </summary>
        [Fact]
        public void NewConnection_EmitsCreationTraces()
        {
            // Arrange
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory());
            using PoolerTraceListener listener = new();

            // Act
            Assert.True(pool.TryGetConnection(
                new SqlConnection(),
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? connection));

            // Assert
            IReadOnlyList<string> messages = listener.MessagesForPool(pool.Id);
            Assert.NotNull(connection);
            TraceAssert.Contains("Getting connection.", messages);
            TraceAssert.Contains("Creating new connection.", messages);
            TraceAssert.Contains("Added to pool.", messages);
        }

        /// <summary>
        /// Verifies that retrieving a connection from the idle pool is traced, covering Story 1
        /// scenario 1.
        /// </summary>
        [Fact]
        public void IdleConnectionReuse_EmitsPoppedFromGeneralPoolTrace()
        {
            // Arrange
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory());
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
            Assert.NotNull(connection);
            pool.ReturnInternalConnection(connection!, owner);

            using PoolerTraceListener listener = new();

            // Act
            Assert.True(pool.TryGetConnection(new SqlConnection(), null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? reused));

            // Assert
            Assert.Same(connection, reused);
            TraceAssert.Contains("Popped from general pool.", listener.MessagesForPool(pool.Id));
        }

        /// <summary>
        /// Verifies that returning a connection traces both the deactivation and the routing
        /// decision that put it back into the idle pool, covering Story 1 scenario 3.
        /// </summary>
        [Fact]
        public void Return_EmitsDeactivateAndPushTraces()
        {
            // Arrange
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory());
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
            Assert.NotNull(connection);

            using PoolerTraceListener listener = new();

            // Act
            pool.ReturnInternalConnection(connection!, owner);

            // Assert
            IReadOnlyList<string> messages = listener.MessagesForPool(pool.Id);
            TraceAssert.Contains("Deactivating.", messages);
            TraceAssert.Contains("Pushing to general pool.", messages);
        }

        /// <summary>
        /// Verifies that destroying a connection traces the removal and the disposal, covering
        /// Story 1 scenario 4. Clear is used as the destruction trigger because it drains the idle
        /// channel through the same removal path as every other destroy.
        /// </summary>
        [Fact]
        public void Destroy_EmitsRemoveAndDisposeTraces()
        {
            // Arrange
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory());
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
            Assert.NotNull(connection);
            pool.ReturnInternalConnection(connection!, owner);

            using PoolerTraceListener listener = new();

            // Act
            pool.Clear();

            // Assert
            IReadOnlyList<string> messages = listener.MessagesForPool(pool.Id);
            TraceAssert.Contains("Clearing.", messages);
            TraceAssert.Contains("Removing from pool.", messages);
            TraceAssert.Contains("Removed from pool.", messages);
            TraceAssert.Contains("Disposed.", messages);
            TraceAssert.Contains("Cleared.", messages);
        }

        /// <summary>
        /// Verifies that startup and shutdown are traced with the pool identifier, covering Story 1
        /// scenario 5.
        /// </summary>
        [Fact]
        public void StartupAndShutdown_EmitTraces()
        {
            // Arrange
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory());
            using PoolerTraceListener listener = new();

            // Act
            pool.Startup();
            pool.Shutdown();

            // Assert
            IReadOnlyList<string> messages = listener.MessagesForPool(pool.Id);
            Assert.Contains(messages, m => m.IndexOf("Startup", StringComparison.Ordinal) >= 0);
            Assert.Contains(messages, m => m.IndexOf("Shutdown", StringComparison.Ordinal) >= 0);
        }

        /// <summary>
        /// Verifies that a failed physical open is traced on the pool's create path, so an operator
        /// can see why the pool stopped growing.
        /// </summary>
        [Fact]
        public void CreateFailure_EmitsCreateThrewTrace()
        {
            // Arrange
            ChannelDbConnectionPool pool = ConstructPool(new FailingSqlConnectionFactory());
            using PoolerTraceListener listener = new();

            // Act
            Assert.ThrowsAny<Exception>(() =>
                pool.TryGetConnection(new SqlConnection(), null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));

            // Assert
            TraceAssert.Contains("which threw an exception", listener.MessagesForPool(pool.Id));
        }

        /// <summary>
        /// Verifies that a connection discarded for exceeding the Connection Idle Timeout is traced
        /// with that specific reason, rather than silently disappearing from the pool.
        /// </summary>
        [Fact]
        public void IdleTimeoutEviction_EmitsReasonTrace()
        {
            // Arrange - idle-timeout eviction is opt-in; the switch defaults to legacy behavior.
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.UseLegacyIdleTimeoutBehavior = false;

            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory(), idleTimeout: 1);
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
            Assert.NotNull(connection);
            pool.ReturnInternalConnection(connection!, owner);

            // Back-date the return stamp only after the connection is parked in the idle channel:
            // the return path re-stamps it so that time spent checked out is not counted as idle.
            connection!.SetReturnedTime(DateTime.UtcNow - TimeSpan.FromMinutes(5));

            using PoolerTraceListener listener = new();

            // Act - retrieval trips the idle-expiry gate and discards the connection.
            Assert.True(pool.TryGetConnection(new SqlConnection(), null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? replacement));

            // Assert
            Assert.NotNull(replacement);
            Assert.NotSame(connection, replacement);
            TraceAssert.Contains("exceeded the connection idle timeout and removed.", listener.MessagesForPool(pool.Id));
        }

        /// <summary>
        /// Verifies that pruning traces each invocation, so idle reclamation is attributable in a
        /// trace capture even when it removes nothing (Story 3).
        /// </summary>
        [Fact]
        public void Prune_EmitsTracePerInvocation()
        {
            // Arrange
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory(), maxPoolSize: 4, idleTimeout: 300);
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
            Assert.NotNull(connection);
            pool.ReturnInternalConnection(connection!, owner);

            using PoolerTraceListener listener = new();

            // Act
            pool.PruneConnections(1);

            // Assert
            IReadOnlyList<string> messages = listener.MessagesForPool(pool.Id);
            TraceAssert.Contains("Pruning up to 1 idle connections.", messages);
            TraceAssert.Contains("Pruned 1 idle connections.", messages);
        }

        #endregion

#if NET
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
            SqlClientMetrics metrics,
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
            SqlClientMetrics metrics,
            long hardConnects = 0,
            long hardDisconnects = 0,
            long softConnects = 0,
            long softDisconnects = 0,
            long pooledConnections = 0,
            long freeConnections = 0,
            long reclaimedConnections = 0)
        {
            Dictionary<string, long> expected = new()
            {
                ["_hardConnectsRate"] = hardConnects,
                ["_hardDisconnectsRate"] = hardDisconnects,
                ["_activeHardConnections"] = hardConnects - hardDisconnects,
                ["_softConnectsRate"] = softConnects,
                ["_softDisconnectsRate"] = softDisconnects,
                ["_activeSoftConnections"] = softConnects - softDisconnects,
                ["_pooledConnections"] = pooledConnections,
                ["_freeConnections"] = freeConnections,
                ["_reclaimedConnections"] = reclaimedConnections,
            };

            // Reported as a single message listing only the counters that differ. Comparing the
            // dictionaries directly would be truncated by the assertion formatter, which hides the
            // one counter the test is about.
            List<string> mismatches = new();
            foreach (KeyValuePair<string, long> counter in expected)
            {
                long actual = MetricReader.Read(metrics, counter.Key);
                if (actual != counter.Value)
                {
                    mismatches.Add($"{counter.Key}: expected {counter.Value}, actual {actual}");
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
            SqlClientMetrics metrics = SqlClientMetrics.CreateIsolated();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new SuccessfulSqlConnectionFactory());
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
            SqlClientMetrics metrics = SqlClientMetrics.CreateIsolated();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new SuccessfulSqlConnectionFactory());
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
            SqlClientMetrics metrics = SqlClientMetrics.CreateIsolated();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new SuccessfulSqlConnectionFactory());
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
            SqlClientMetrics metrics = SqlClientMetrics.CreateIsolated();
            IDbConnectionPool pool = ConstructPool(implementation, metrics, new SuccessfulSqlConnectionFactory());
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
            SqlClientMetrics metrics = SqlClientMetrics.CreateIsolated();
            IDbConnectionPool pool = ConstructPool(PoolImplementation.Channel, metrics, new SuccessfulSqlConnectionFactory());
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
#endif

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

        /// <summary>
        /// Captures <c>PoolerTrace</c> events from the SqlClient event source.
        /// <para>
        /// Tests filter captured messages by pool id (see <see cref="MessagesForPool"/>) because the
        /// event source is process-wide: xUnit runs test classes in parallel, so traces from other
        /// pools are expected to appear in the same capture.
        /// </para>
        /// </summary>
        internal sealed class PoolerTraceListener : EventListener
        {
            private const string SqlClientEventSourceName = "Microsoft.Data.SqlClient.EventSource";

            // Mirrors SqlClientEventSource.Keywords.PoolerTrace. Duplicated as a literal because
            // that type is not visible to this assembly.
            private const EventKeywords PoolerTraceKeyword = (EventKeywords)32;

            // Lazily initialized: EventListener's base constructor invokes OnEventSourceCreated,
            // which enables events, before this class's field initializers have run. Traces can
            // therefore arrive on another thread before a plain field initializer would have
            // assigned the queue.
            private ConcurrentQueue<string>? _messages;

            private ConcurrentQueue<string> Messages =>
                LazyInitializer.EnsureInitialized(ref _messages)!;

            /// <inheritdoc />
            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == SqlClientEventSourceName)
                {
                    EnableEvents(eventSource, EventLevel.Informational, PoolerTraceKeyword);
                }
            }

            /// <inheritdoc />
            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (eventData.Payload is null)
                {
                    return;
                }

                foreach (object? payload in eventData.Payload)
                {
                    if (payload is string message)
                    {
                        Messages.Enqueue(message);
                    }
                }
            }

            /// <summary>
            /// Returns the captured messages emitted for the given pool.
            /// </summary>
            /// <param name="poolId">The <see cref="ChannelDbConnectionPool.Id"/> to filter on.</param>
            internal IReadOnlyList<string> MessagesForPool(int poolId)
            {
                // Pool-scoped traces render the id as the first substituted argument, immediately
                // after the "|CPOOL> " marker, e.g.
                //   "<prov.DbConnectionPool.Clear|RES|CPOOL> 7, Clearing."
                //   "<prov.DbConnectionPool.Shutdown|RES|INFO|CPOOL> 7"
                // The trailing boundary keeps pool 7 from matching pool 70.
                Regex pattern = new(
                    @"CPOOL> " + Regex.Escape(poolId.ToString(CultureInfo.InvariantCulture)) + @"(\D|$)",
                    RegexOptions.CultureInvariant);

                return Messages.Where(m => pattern.IsMatch(m)).ToList();
            }
        }

        #endregion
    }

#if NET
    /// <summary>
    /// Reads the private counter fields of a <see cref="SqlClientMetrics"/> instance. The counters
    /// are not otherwise observable without an EventCounter listener and its polling interval,
    /// which would make these tests slow and timing dependent.
    /// </summary>
    internal static class MetricReader
    {
        /// <summary>
        /// Reads the current value of the named counter field.
        /// </summary>
        /// <param name="metrics">The metrics instance to read from.</param>
        /// <param name="fieldName">Private field name declared on <see cref="SqlClientMetrics"/>.</param>
        internal static long Read(SqlClientMetrics metrics, string fieldName)
        {
            FieldInfo? field = typeof(SqlClientMetrics).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (long)field!.GetValue(metrics)!;
        }
    }
#endif

    /// <summary>
    /// xUnit assertion helper for substring matching over a captured trace stream.
    /// </summary>
    internal static class TraceAssert
    {
        /// <summary>
        /// Asserts that at least one captured message contains <paramref name="fragment"/>.
        /// </summary>
        internal static void Contains(string fragment, IReadOnlyList<string> messages) =>
            Assert.True(
                messages.Any(m => m.IndexOf(fragment, StringComparison.Ordinal) >= 0),
                $"Expected a trace containing \"{fragment}\". Captured:{Environment.NewLine}{string.Join(Environment.NewLine, messages)}");
    }
}

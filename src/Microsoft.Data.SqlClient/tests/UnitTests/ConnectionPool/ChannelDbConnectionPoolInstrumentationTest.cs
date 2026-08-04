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
    /// trace events emitted across the connection lifecycle, and the last-connection-create
    /// exception that is surfaced as the inner exception of a pooled-open timeout (GH#3545).
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
        {
            DbConnectionPoolGroupOptions poolGroupOptions = new(
                poolByIdentity: false,
                minPoolSize: minPoolSize,
                maxPoolSize: maxPoolSize,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: idleTimeout);

            DbConnectionPoolGroup poolGroup = new(
                new SqlConnectionOptions(connectionString),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions);

            return new ChannelDbConnectionPool(
                connectionFactory,
                poolGroup,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo(),
                connectionCreationRateLimiter);
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

        // The metric counters are process-wide and other suites run in parallel, so these tests
        // assert that a counter advanced by at least the expected amount rather than by exactly it.
        // The rate counters only ever increase, which makes that assertion stable under concurrency.

        /// <summary>
        /// Verifies that retrieving an idle connection counts a soft connect, covering Story 2
        /// scenario 3.
        /// </summary>
        [Fact]
        public void IdleConnectionReuse_CountsSoftConnect()
        {
            // Arrange
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory());
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
            Assert.NotNull(connection);
            pool.ReturnInternalConnection(connection!, owner);

            long before = MetricReader.Read("_softConnectsRate");

            // Act
            Assert.True(pool.TryGetConnection(new SqlConnection(), null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? reused));

            // Assert
            Assert.Same(connection, reused);
            Assert.True(MetricReader.Read("_softConnectsRate") >= before + 1);
        }

        /// <summary>
        /// Verifies that returning a connection to the idle pool counts a soft disconnect, covering
        /// Story 2 scenario 4.
        /// </summary>
        [Fact]
        public void Return_CountsSoftDisconnect()
        {
            // Arrange
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory());
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
            Assert.NotNull(connection);

            long before = MetricReader.Read("_softDisconnectsRate");

            // Act
            pool.ReturnInternalConnection(connection!, owner);

            // Assert
            Assert.True(MetricReader.Read("_softDisconnectsRate") >= before + 1);
        }

        /// <summary>
        /// Verifies that destroying a physical connection counts a hard disconnect, covering Story 2
        /// scenario 2.
        /// </summary>
        [Fact]
        public void Destroy_CountsHardDisconnect()
        {
            // Arrange
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory());
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));
            Assert.NotNull(connection);
            pool.ReturnInternalConnection(connection!, owner);

            long before = MetricReader.Read("_hardDisconnectsRate");

            // Act
            pool.Clear();

            // Assert
            Assert.True(MetricReader.Read("_hardDisconnectsRate") >= before + 1);
        }

        /// <summary>
        /// Verifies that replacing a connection counts a hard disconnect for the connection it
        /// discards. The channel pool swaps the new connection into the old connection's slot, so
        /// the pooled-connection gauge is deliberately left untouched.
        /// </summary>
        [Fact]
        public void ReplaceConnection_CountsHardDisconnectForDiscardedConnection()
        {
            // Arrange
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory());
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? oldConnection));
            Assert.NotNull(oldConnection);

            long beforeDisconnects = MetricReader.Read("_hardDisconnectsRate");
            long beforePooled = MetricReader.Read("_pooledConnections");

            // Act
            DbConnectionInternal newConnection = pool.ReplaceConnection(owner, oldConnection!, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

            // Assert
            Assert.NotSame(oldConnection, newConnection);
            Assert.True(MetricReader.Read("_hardDisconnectsRate") >= beforeDisconnects + 1);
            Assert.Equal(beforePooled, MetricReader.Read("_pooledConnections"));
        }

        #endregion
#endif

        #region Last connection create exception (GH#3545)

        /// <summary>
        /// Verifies that a pool that has never attempted a physical open reports no create failure,
        /// so a timeout from a genuinely saturated pool is not annotated with a stale cause.
        /// </summary>
        [Fact]
        public void LastConnectionCreateException_NoAttempts_IsNull()
        {
            // Arrange / Act
            ChannelDbConnectionPool pool = ConstructPool(new SuccessfulSqlConnectionFactory());

            // Assert
            Assert.Null(pool.LastConnectionCreateException);
        }

        /// <summary>
        /// Verifies that a failed physical open is retained on the pool and then discarded once a
        /// later open succeeds, so the recorded cause never outlives its relevance.
        /// </summary>
        [Fact]
        public void LastConnectionCreateException_RecordedOnFailure_ClearedOnSuccess()
        {
            // Arrange - NeverBlock keeps the pool out of the blocking period so the second request
            // actually attempts another physical open instead of fast-failing on cached state.
            ToggleableConnectionFactory factory = new() { ShouldFail = true };
            ChannelDbConnectionPool pool = ConstructPool(
                factory,
                connectionString: "Data Source=localhost;Pool Blocking Period=NeverBlock;");

            // Act - a failed open records the cause.
            Assert.Throws<TestConnectionCreateException>(() =>
                pool.TryGetConnection(new SqlConnection(), null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));

            // Assert
            Assert.IsType<TestConnectionCreateException>(pool.LastConnectionCreateException);

            // Act - a successful open proves the server is reachable and clears the cause.
            factory.ShouldFail = false;
            Assert.True(pool.TryGetConnection(new SqlConnection(), null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection));

            // Assert
            Assert.NotNull(connection);
            Assert.Null(pool.LastConnectionCreateException);
        }

        /// <summary>
        /// Verifies GH#3545 end to end for this pool: when a request waits for a pooled connection
        /// and times out, the most recent physical connection failure is attached as the inner
        /// exception instead of being lost behind the generic pool-exhaustion message.
        /// </summary>
        [Fact]
        public void PooledOpenTimeout_CarriesLastCreateExceptionAsInner()
        {
            // Arrange - a single-permit limiter lets the test hold the only creation permit, so the
            // second request cannot attempt an open and must wait on the idle channel until its
            // budget expires. NeverBlock keeps the pool out of the blocking period, which would
            // otherwise fast-fail the second request with the cached exception directly.
            ToggleableConnectionFactory factory = new() { ShouldFail = true };
            using ConcurrencyLimiter rateLimiter = new(
                new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });
            ChannelDbConnectionPool pool = ConstructPool(
                factory,
                connectionString: "Data Source=localhost;Pool Blocking Period=NeverBlock;",
                maxPoolSize: 4,
                connectionCreationRateLimiter: rateLimiter);

            // The first request fails its physical open, which records the cause on the pool.
            Assert.Throws<TestConnectionCreateException>(() =>
                pool.TryGetConnection(new SqlConnection(), null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));
            Assert.IsType<TestConnectionCreateException>(pool.LastConnectionCreateException);

            // Hold the only permit so no further creation can be attempted.
            using RateLimitLease lease = rateLimiter.AttemptAcquire(1);
            Assert.True(lease.IsAcquired);

            // Act
            InvalidOperationException timeout = Assert.Throws<InvalidOperationException>(() =>
                pool.TryGetConnection(new SqlConnection(), null, TimeoutTimer.StartNew(TimeSpan.FromMilliseconds(100)), out _));

            // Assert
            Assert.IsType<TestConnectionCreateException>(timeout.InnerException);
        }

        /// <summary>
        /// Verifies that a timeout with no preceding create failure still reports the plain
        /// pool-exhaustion message, so the change does not fabricate a cause.
        /// </summary>
        [Fact]
        public void PooledOpenTimeout_NoCreateFailure_HasNoInnerException()
        {
            // Arrange - hold the only creation permit up front so no open is ever attempted.
            using ConcurrencyLimiter rateLimiter = new(
                new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });
            ChannelDbConnectionPool pool = ConstructPool(
                new SuccessfulSqlConnectionFactory(),
                maxPoolSize: 4,
                connectionCreationRateLimiter: rateLimiter);

            using RateLimitLease lease = rateLimiter.AttemptAcquire(1);
            Assert.True(lease.IsAcquired);

            // Act
            InvalidOperationException timeout = Assert.Throws<InvalidOperationException>(() =>
                pool.TryGetConnection(new SqlConnection(), null, TimeoutTimer.StartNew(TimeSpan.FromMilliseconds(100)), out _));

            // Assert
            Assert.Null(timeout.InnerException);
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
        /// Connection factory whose success or failure can be flipped between requests, so a single
        /// pool can be driven through a failure and a subsequent recovery.
        /// </summary>
        internal sealed class ToggleableConnectionFactory : SqlConnectionFactory
        {
            /// <summary>
            /// When true, the next creation attempt throws <see cref="TestConnectionCreateException"/>.
            /// </summary>
            internal volatile bool ShouldFail;

            /// <inheritdoc />
            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
            {
                if (ShouldFail)
                {
                    throw new TestConnectionCreateException();
                }

                return new StubDbConnectionInternal();
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
    /// Reads the private counter fields of the process-wide <see cref="SqlClientMetrics"/> instance.
    /// The counters are not otherwise observable without an EventCounter listener and its polling
    /// interval, which would make these tests slow and timing dependent.
    /// </summary>
    internal static class MetricReader
    {
        /// <summary>
        /// Reads the current value of the named counter field.
        /// </summary>
        /// <param name="fieldName">Private field name declared on <see cref="SqlClientMetrics"/>.</param>
        internal static long Read(string fieldName)
        {
            FieldInfo? field = typeof(SqlClientMetrics).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (long)field!.GetValue(SqlClientDiagnostics.Metrics)!;
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

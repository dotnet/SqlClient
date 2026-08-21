// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Which pool implementation a test is running against. Tests that assert behavior both pools
    /// owe should be a <see cref="TheoryAttribute"/> over this, so the two cannot silently diverge.
    /// </summary>
    public enum PoolImplementation
    {
        /// <summary>The legacy <see cref="WaitHandleDbConnectionPool"/>.</summary>
        WaitHandle,

        /// <summary>The <see cref="ChannelDbConnectionPool"/>.</summary>
        Channel,
    }

    /// <summary>
    /// Pool construction shared by the connection pool test classes, written against
    /// <see cref="IDbConnectionPool"/> so a test can run against either implementation. Helpers that
    /// only a few tests need live with those tests instead.
    /// </summary>
    internal static class PoolTestHarness
    {
        /// <summary>
        /// Builds the requested pool implementation behind the shared pool interface.
        /// </summary>
        /// <param name="implementation">Which pool to construct.</param>
        /// <param name="connectionFactory">
        /// Factory the pool creates connections with. Defaults to one that always succeeds.
        /// </param>
        /// <param name="metrics">Metrics sink, or null for the pool's default.</param>
        /// <param name="creationTimeout">
        /// How long, in milliseconds, a caller waits for a pooled connection to free up. The default
        /// matches the pool group default and is short enough that a saturated wait times out almost
        /// immediately; raise it in tests that need a caller to genuinely block.
        /// </param>
        /// <param name="timeProvider">
        /// Time source, or null for a fresh <see cref="FakeTimeProvider"/>. Pass one in to drive
        /// timer-based pool behavior deterministically; see <see cref="AdvanceUntil"/>.
        /// </param>
        internal static IDbConnectionPool ConstructPool(
            PoolImplementation implementation,
            SqlConnectionFactory? connectionFactory = null,
            ISqlClientMetrics? metrics = null,
            TimeProvider? timeProvider = null,
            string connectionString = "Data Source=localhost;",
            int maxPoolSize = 50,
            int minPoolSize = 0,
            int idleTimeout = 0,
            int creationTimeout = 15)
        {
            connectionFactory ??= new ChannelDbConnectionPoolTest.SuccessfulSqlConnectionFactory();
            timeProvider ??= new FakeTimeProvider();

            DbConnectionPoolGroup poolGroup = ConstructPoolGroup(connectionString, maxPoolSize, minPoolSize, idleTimeout, creationTimeout);

            return implementation switch
            {
                PoolImplementation.WaitHandle => new WaitHandleDbConnectionPool(
                    connectionFactory,
                    poolGroup,
                    DbConnectionPoolIdentity.NoIdentity,
                    new DbConnectionPoolProviderInfo(),
                    timeProvider: timeProvider,
                    metrics: metrics),

                PoolImplementation.Channel => new ChannelDbConnectionPool(
                    connectionFactory,
                    poolGroup,
                    DbConnectionPoolIdentity.NoIdentity,
                    new DbConnectionPoolProviderInfo(),
                    connectionCreationRateLimiter: null,
                    timeProvider: timeProvider,
                    metrics: metrics),

                _ => throw new ArgumentOutOfRangeException(nameof(implementation)),
            };
        }

        /// <summary>
        /// Builds a pool group with the given sizing, for tests that construct a pool directly.
        /// </summary>
        internal static DbConnectionPoolGroup ConstructPoolGroup(
            string connectionString = "Data Source=localhost;",
            int maxPoolSize = 50,
            int minPoolSize = 0,
            int idleTimeout = 0,
            int creationTimeout = 15)
        {
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: minPoolSize,
                maxPoolSize: maxPoolSize,
                creationTimeout: creationTimeout,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: idleTimeout);

            return new DbConnectionPoolGroup(
                new SqlConnectionOptions(connectionString),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions);
        }

        /// <summary>
        /// Checks out a connection and abandons its owning <see cref="SqlConnection"/>, returning
        /// only the internal connection. Marked <see cref="MethodImplOptions.NoInlining"/> so the
        /// owner's stack slot is guaranteed to be gone when the caller collects: in Debug builds
        /// locals stay alive to the end of their enclosing method, so the owner has to be confined
        /// to a frame that has already been popped.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static DbConnectionInternal CheckOutAndAbandonOwner(IDbConnectionPool pool)
        {
            SqlConnection owner = new();
            Assert.True(pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? connection));

            Assert.NotNull(connection);
            return connection!;
        }

        /// <summary>
        /// Forces collection of an abandoned owner so its connection becomes emancipated.
        /// </summary>
        internal static void CollectAbandonedOwners()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}

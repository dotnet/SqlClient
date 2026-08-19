// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    /// Construction and lifecycle helpers shared by the connection pool test classes, written
    /// against <see cref="IDbConnectionPool"/> so a test can run against either implementation.
    /// </summary>
    internal static class PoolTestHarness
    {
        /// <summary>
        /// How long a test waits for a cross-thread transition before failing. Generous, because it
        /// is only ever reached when something is actually broken.
        /// </summary>
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

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
        /// Checks out a connection and roots its owner in a <see cref="GCHandle"/>, so the caller
        /// decides exactly when the connection becomes emancipated by freeing the handle. Same
        /// non-inlining requirement as <see cref="CheckOutAndAbandonOwner"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static DbConnectionInternal CheckOutAndRootOwner(IDbConnectionPool pool, out GCHandle ownerRoot)
        {
            SqlConnection owner = new();
            ownerRoot = GCHandle.Alloc(owner);

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

        /// <summary>
        /// Spins until <paramref name="condition"/> holds, failing the test if it does not happen
        /// within <see cref="WaitTimeout"/>. Used to observe a background caller reaching a blocking
        /// wait, which is inherently a cross-thread transition and cannot be awaited directly.
        /// </summary>
        internal static void WaitFor(Func<bool> condition, string because)
            => Assert.True(SpinWait.SpinUntil(condition, WaitTimeout), because);

        /// <summary>
        /// Advances <paramref name="time"/> until <paramref name="condition"/> holds, failing the
        /// test if it does not happen within <see cref="WaitTimeout"/>.
        /// </summary>
        /// <remarks>
        /// Lets a test assert that a pool eventually does something without knowing what drives it.
        /// The pools differ here: <see cref="WaitHandleDbConnectionPool"/> reclaims inline when a
        /// request finds it saturated, so the condition is typically already true on the first
        /// check, while <see cref="ChannelDbConnectionPool"/> reclaims from a sweep timer and needs
        /// the clock to move. Advancing a pool that did not need it is harmless.
        /// </remarks>
        internal static void AdvanceUntil(FakeTimeProvider time, Func<bool> condition, string because)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Spun in short slices rather than passing the whole timeout, so the clock only moves
            // when the condition has not been met yet.
            while (!SpinWait.SpinUntil(condition, TimeSpan.FromMilliseconds(50)))
            {
                Assert.True(stopwatch.Elapsed < WaitTimeout, because);
                time.Advance(TimeSpan.FromSeconds(1));
            }
        }
    }
}

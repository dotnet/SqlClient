// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Data.Common;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Extensions.Time.Testing;
using Xunit;

using static Microsoft.Data.SqlClient.UnitTests.ConnectionPool.PoolTestHarness;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Verifies that a pooled-open timeout describes the request-local reason for waiting and
    /// captures a cheap snapshot of pool usage for diagnosing capacity pressure and leaks.
    /// </summary>
    public sealed class PoolAcquisitionDiagnosticsTest
    {
        /// <summary>
        /// Verifies both pool implementations report that every slot is occupied, including how
        /// many connections remain checked out and how long the oldest checkout has been held.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle, false)]
        [InlineData(PoolImplementation.WaitHandle, true)]
        [InlineData(PoolImplementation.Channel, false)]
        [InlineData(PoolImplementation.Channel, true)]
        public async Task Timeout_FullPool_ReportsSaturationSnapshot(
            PoolImplementation implementation,
            bool async)
        {
            IDbConnectionPool pool = ConstructPool(
                implementation,
                timeProvider: TimeProvider.System,
                maxPoolSize: 1,
                creationTimeout: 100);
            using SqlConnection checkedOutOwner = new();
            using SqlConnection waitingOwner = new();

            Assert.True(pool.TryGetConnection(
                checkedOutOwner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(5)),
                out DbConnectionInternal? checkedOutConnection));
            Assert.NotNull(checkedOutConnection);

            checkedOutConnection!.CheckoutTime = DateTime.UtcNow - TimeSpan.FromMinutes(5);

            try
            {
                InvalidOperationException timeout = await AssertPoolTimeoutAsync(
                    pool,
                    waitingOwner,
                    async);

                Assert.Equal(
                    PoolAcquisitionWaitReason.PoolFull.ToString(),
                    timeout.Data[PoolAcquisitionDiagnostics.WaitReasonDataKey]);
                Assert.Equal(1, timeout.Data[PoolAcquisitionDiagnostics.MaxPoolSizeDataKey]);
                Assert.Equal(1, timeout.Data[PoolAcquisitionDiagnostics.ConnectionCountDataKey]);
                Assert.Equal(0, timeout.Data[PoolAcquisitionDiagnostics.IdleConnectionCountDataKey]);
                Assert.Equal(1, timeout.Data[PoolAcquisitionDiagnostics.CheckedOutConnectionCountDataKey]);
                Assert.Equal(0, timeout.Data[PoolAcquisitionDiagnostics.AbandonedConnectionCountDataKey]);
                Assert.Equal(0L, timeout.Data[PoolAcquisitionDiagnostics.ReclaimedConnectionCountDataKey]);

                TimeSpan longestCheckout =
                    Assert.IsType<TimeSpan>(
                        timeout.Data[PoolAcquisitionDiagnostics.LongestCheckoutDurationDataKey]);
                Assert.True(longestCheckout >= TimeSpan.FromMinutes(4));
            }
            finally
            {
                pool.ReturnInternalConnection(checkedOutConnection, checkedOutOwner);
            }

            GC.KeepAlive(checkedOutOwner);
        }

        /// <summary>
        /// Verifies the channel pool identifies connection-creation throttling as the timeout cause
        /// instead of incorrectly claiming that max pool size was reached.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Timeout_RateLimiterSaturated_ReportsRateLimiting(bool async)
        {
            using ConcurrencyLimiter limiter = new(
                new ConcurrencyLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 0,
                });
            using RateLimitLease heldLease = limiter.AttemptAcquire(1);
            Assert.True(heldLease.IsAcquired);

            DbConnectionPoolGroup poolGroup = ConstructPoolGroup(
                maxPoolSize: 4,
                creationTimeout: 100);
            var pool = new ChannelDbConnectionPool(
                new ChannelDbConnectionPoolTest.SuccessfulSqlConnectionFactory(),
                poolGroup,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo(),
                connectionCreationRateLimiter: limiter);
            using SqlConnection waitingOwner = new();

            InvalidOperationException timeout = await AssertPoolTimeoutAsync(
                pool,
                waitingOwner,
                async,
                TimeSpan.FromSeconds(1));

            Assert.Equal(
                PoolAcquisitionWaitReason.ConnectionCreationRateLimited.ToString(),
                timeout.Data[PoolAcquisitionDiagnostics.WaitReasonDataKey]);
            Assert.Equal(4, timeout.Data[PoolAcquisitionDiagnostics.MaxPoolSizeDataKey]);
            Assert.Equal(0, timeout.Data[PoolAcquisitionDiagnostics.ConnectionCountDataKey]);
            Assert.Equal(0, timeout.Data[PoolAcquisitionDiagnostics.CheckedOutConnectionCountDataKey]);
        }

        /// <summary>
        /// Verifies a timeout snapshot identifies a connection whose owning application connection
        /// was collected without being closed or disposed.
        /// </summary>
        [Fact]
        public void Timeout_AbandonedOwner_ReportsAbandonedConnection()
        {
            var pool = (ChannelDbConnectionPool)ConstructPool(
                PoolImplementation.Channel,
                timeProvider: TimeProvider.System,
                maxPoolSize: 1);
            DbConnectionInternal abandonedConnection =
                CheckOutAndAbandonOwner(pool);
            abandonedConnection.CheckoutTime =
                DateTime.UtcNow - TimeSpan.FromMinutes(5);
            CollectAbandonedOwners();
            Assert.True(abandonedConnection.IsEmancipated);
            var timeoutProvider = new FakeTimeProvider();
            TimeoutTimer expiredTimeout =
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(1), timeoutProvider);
            timeoutProvider.Advance(TimeSpan.FromSeconds(2));

            InvalidOperationException timeout =
                Assert.Throws<InvalidOperationException>(() =>
                    pool.TryGetConnection(
                        new SqlConnection(),
                        taskCompletionSource: null,
                        expiredTimeout,
                        out _));

            Assert.Equal(
                1,
                timeout.Data[PoolAcquisitionDiagnostics.AbandonedConnectionCountDataKey]);
            Assert.Equal(
                0,
                timeout.Data[PoolAcquisitionDiagnostics.CheckedOutConnectionCountDataKey]);

            pool.ReclaimEmancipatedConnections();
        }

        /// <summary>
        /// Requests a connection synchronously or asynchronously and returns the pooled-open timeout.
        /// </summary>
        /// <param name="pool">Pool from which to request a connection.</param>
        /// <param name="owner">Connection that would own the acquired internal connection.</param>
        /// <param name="async">Whether to use the pool's asynchronous completion path.</param>
        /// <param name="timeoutDuration">Optional timeout budget for the acquisition.</param>
        /// <returns>The pooled-open timeout surfaced to the caller.</returns>
        private static async Task<InvalidOperationException> AssertPoolTimeoutAsync(
            IDbConnectionPool pool,
            DbConnection owner,
            bool async,
            TimeSpan? timeoutDuration = null)
        {
            TimeoutTimer timeout = TimeoutTimer.StartNew(
                timeoutDuration ?? TimeSpan.FromMilliseconds(100));

            if (!async)
            {
                return Assert.Throws<InvalidOperationException>(() =>
                    pool.TryGetConnection(
                        owner,
                        taskCompletionSource: null,
                        timeout,
                        out _));
            }

            TaskCompletionSource<DbConnectionInternal> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.False(pool.TryGetConnection(
                owner,
                completion,
                timeout,
                out _));

            return await Assert.ThrowsAsync<InvalidOperationException>(
                () => completion.Task);
        }
    }
}

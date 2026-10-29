// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Diagnostics;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Verifies the single-field admission/running state of <see cref="PoolPruningGuard"/>.
    /// </summary>
    public class PoolPruningGuardTest
    {
        /// <summary>
        /// A new guard is running, admits operations, and stays running once they release.
        /// </summary>
        [Fact]
        public void NewGuard_IsRunningAndAdmits()
        {
            var guard = new PoolPruningGuard();

            Assert.True(guard.IsRunning);
            PoolPruningGuard.Lease lease = guard.TryEnter();
            Assert.True(lease.IsActive);
            lease.Dispose();
            Assert.True(guard.IsRunning);
        }

        /// <summary>
        /// Admission at the maximum count throws instead of carrying into the flag bits, and
        /// succeeds again once an outstanding lease is released.
        /// </summary>
        [Fact]
        public void TryEnter_AtMaxCount_ThrowsUntilALeaseIsReleased()
        {
            var guard = new PoolPruningGuard();
            guard.SetActiveCountForTest(PoolPruningGuard.MaxActiveCount - 1);

            PoolPruningGuard.Lease first = guard.TryEnter();
            Assert.True(first.IsActive);

            Assert.Throws<InvalidOperationException>(() => guard.TryEnter());
            Assert.True(guard.IsRunning);

            first.Dispose();
            using PoolPruningGuard.Lease next = guard.TryEnter();
            Assert.True(next.IsActive);
        }

        /// <summary>
        /// A caller refused admission must already observe the pool as not running; otherwise it
        /// could report a false pooled-open timeout instead of retrying on a new pool.
        /// </summary>
        [Fact]
        public void TryPrune_PublishesNotRunningBeforeShutdown()
        {
            var guard = new PoolPruningGuard();
            var pool = new FakePool();
            bool? runningDuringShutdown = null;
            bool? admittedDuringShutdown = null;
            pool.OnShutdown = () =>
            {
                runningDuringShutdown = guard.IsRunning;
                using PoolPruningGuard.Lease lease = guard.TryEnter();
                admittedDuringShutdown = lease.IsActive;
            };

            Assert.True(guard.TryPrune(pool));

            Assert.Equal(1, pool.ShutdownCalls);
            Assert.False(runningDuringShutdown);
            Assert.False(admittedDuringShutdown);
            Assert.False(guard.IsRunning);
            using PoolPruningGuard.Lease refused = guard.TryEnter();
            Assert.False(refused.IsActive);
        }

        /// <summary>
        /// An outstanding lease blocks pruning; once released, the guard admits again.
        /// </summary>
        [Fact]
        public void TryPrune_WithActiveLease_KeepsRunning()
        {
            var guard = new PoolPruningGuard();
            var pool = new FakePool();
            PoolPruningGuard.Lease lease = guard.TryEnter();

            Assert.False(guard.TryPrune(pool));

            Assert.Equal(0, pool.ShutdownCalls);
            Assert.True(guard.IsRunning);
            lease.Dispose();
            using PoolPruningGuard.Lease next = guard.TryEnter();
            Assert.True(next.IsActive);
        }

        /// <summary>
        /// A pool that still has connections is not pruned.
        /// </summary>
        [Fact]
        public void TryPrune_WithConnections_KeepsRunning()
        {
            var guard = new PoolPruningGuard();
            var pool = new FakePool { Count = 1 };

            Assert.False(guard.TryPrune(pool));

            Assert.Equal(0, pool.ShutdownCalls);
            Assert.True(guard.IsRunning);
        }

        /// <summary>
        /// Explicit shutdown stops admission immediately but keeps the count of already-admitted
        /// operations, so their later release is balanced.
        /// </summary>
        [Fact]
        public void TryClose_WithActiveLease_StopsAdmissionAndBalancesRelease()
        {
            var guard = new PoolPruningGuard();
            PoolPruningGuard.Lease lease = guard.TryEnter();

            Assert.True(guard.TryClose());

            Assert.False(guard.IsRunning);
            using (PoolPruningGuard.Lease refused = guard.TryEnter())
            {
                Assert.False(refused.IsActive);
            }
            lease.Dispose();
            Assert.False(guard.IsRunning);
            using PoolPruningGuard.Lease refusedAfterRelease = guard.TryEnter();
            Assert.False(refusedAfterRelease.IsActive);
        }

        /// <summary>
        /// Only the first <see cref="PoolPruningGuard.TryClose"/> closes the guard; closing is
        /// permanent.
        /// </summary>
        [Fact]
        public void TryClose_IsOneShot()
        {
            var guard = new PoolPruningGuard();

            Assert.True(guard.TryClose());
            Assert.False(guard.IsRunning);
            Assert.False(guard.TryClose());
            Assert.False(guard.IsRunning);
        }

        /// <summary>
        /// <see cref="PoolPruningGuard.TryClose"/> only closes the guard; the pool calls it from its
        /// own Shutdown, so the guard never shuts the pool down there. A later prune is refused and
        /// must not shut the pool down either.
        /// </summary>
        [Fact]
        public void TryPrune_AfterClose_RefusesWithoutShutdown()
        {
            var guard = new PoolPruningGuard();
            var pool = new FakePool();

            Assert.True(guard.TryClose());
            Assert.Equal(0, pool.ShutdownCalls);

            Assert.False(guard.TryPrune(pool));

            Assert.Equal(0, pool.ShutdownCalls);
            Assert.False(guard.IsRunning);
        }

        /// <summary>
        /// Disposing the same lease twice releases admission only once.
        /// </summary>
        [Fact]
        public void LeaseDispose_Twice_ReleasesOnce()
        {
            var guard = new PoolPruningGuard();
            var pool = new FakePool();
            PoolPruningGuard.Lease first = guard.TryEnter();
            PoolPruningGuard.Lease second = guard.TryEnter();

            first.Dispose();
            first.Dispose();

            // The second lease is still outstanding, so pruning must be refused.
            Assert.False(guard.TryPrune(pool));
            second.Dispose();
            Assert.True(guard.TryPrune(pool));
        }

        /// <summary>
        /// When a worker has claimed a queued request, a failed scheduler must not release that
        /// request's lease: the worker may be creating a connection and still needs admission.
        /// </summary>
        [Fact]
        public void PendingRequest_ClaimedByWorker_SchedulerCannotReleaseLease()
        {
            var guard = new PoolPruningGuard();
            var pool = new FakePool();
            WaitHandleDbConnectionPool.PendingGetConnection request = NewPendingRequest(guard);

            Assert.True(request.TryClaim());
            Assert.False(request.TryAbandon());

            // The worker still holds admission, so pruning is refused until it releases.
            Assert.False(guard.TryPrune(pool));
            Assert.True(guard.IsRunning);

            request.ReleaseLease();
            Assert.True(guard.TryPrune(pool));
        }

        /// <summary>
        /// When the scheduler abandons a request before any worker claims it, it releases the lease
        /// and a worker that later dequeues the request must not process it.
        /// </summary>
        [Fact]
        public void PendingRequest_AbandonedBeforeClaim_ReleasesLeaseAndBlocksWorker()
        {
            var guard = new PoolPruningGuard();
            var pool = new FakePool();
            WaitHandleDbConnectionPool.PendingGetConnection request = NewPendingRequest(guard);

            Assert.True(request.TryAbandon());
            Assert.False(request.TryClaim());
            Assert.True(guard.TryPrune(pool));
        }

        private static WaitHandleDbConnectionPool.PendingGetConnection NewPendingRequest(PoolPruningGuard guard)
        {
            PoolPruningGuard.Lease lease = guard.TryEnter();
            Assert.True(lease.IsActive);
            return new WaitHandleDbConnectionPool.PendingGetConnection(
                dueTime: 0,
                owner: null!,
                completion: new TaskCompletionSource<DbConnectionInternal>(),
                timeout: TimeoutTimer.StartNew(TimeSpan.FromSeconds(1)),
                lease: lease);
        }

        /// <summary>
        /// Minimal pool stub; the guard only reads <see cref="Count"/> and <see cref="ErrorOccurred"/>
        /// and calls <see cref="Shutdown"/>. Moq cannot proxy the internal interface.
        /// </summary>
        private sealed class FakePool : IDbConnectionPool
        {
            public int Count { get; set; }
            public bool ErrorOccurred { get; set; }
            public int ShutdownCalls { get; private set; }
            public Action? OnShutdown { get; set; }

            public void Shutdown()
            {
                ShutdownCalls++;
                OnShutdown?.Invoke();
            }

            public int Id => 0;
            public ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> AuthenticationContexts => throw new NotImplementedException();
            public SqlConnectionFactory ConnectionFactory => throw new NotImplementedException();
            public ISqlClientMetrics Metrics => throw new NotImplementedException();
            public int IdleCount => throw new NotImplementedException();
            public DbConnectionPoolIdentity Identity => throw new NotImplementedException();
            public bool IsRunning => throw new NotImplementedException();
            public TimeSpan LoadBalanceTimeout => throw new NotImplementedException();
            public DbConnectionPoolGroup PoolGroup => throw new NotImplementedException();
            public DbConnectionPoolGroupOptions PoolGroupOptions => throw new NotImplementedException();
            public DbConnectionPoolProviderInfo ProviderInfo => throw new NotImplementedException();
            public TransactedConnectionPool TransactedConnectionPool => throw new NotImplementedException();
            public bool UseLoadBalancing => throw new NotImplementedException();
            public void Clear() => throw new NotImplementedException();
            public bool TryGetConnection(DbConnection owningObject, TaskCompletionSource<DbConnectionInternal>? taskCompletionSource, TimeoutTimer timeout, out DbConnectionInternal? connection) => throw new NotImplementedException();
            public DbConnectionInternal? ReplaceConnection(DbConnection owningObject, DbConnectionInternal oldConnection, TimeoutTimer timeout) => throw new NotImplementedException();
            public void ReturnInternalConnection(DbConnectionInternal obj, DbConnection? owningObject) => throw new NotImplementedException();
            public void PutObjectFromTransactedPool(DbConnectionInternal obj) => throw new NotImplementedException();
            public void Startup() => throw new NotImplementedException();
            public bool TryPrune() => throw new NotImplementedException();
            public void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject) => throw new NotImplementedException();
        }
    }
}
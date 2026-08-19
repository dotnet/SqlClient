// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Extensions.Time.Testing;
using Xunit;

using static Microsoft.Data.SqlClient.UnitTests.ConnectionPool.PoolTestHarness;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Behavior both pool implementations owe around emancipated connections: a connection whose
    /// owning <see cref="SqlConnection"/> was collected without being closed must not permanently
    /// occupy a pool slot.
    /// </summary>
    /// <remarks>
    /// These tests are deliberately black-box. The pools reclaim by different means, and which one
    /// is used is not part of the contract: <see cref="WaitHandleDbConnectionPool"/> sweeps inline
    /// when a request finds it saturated, while <see cref="ChannelDbConnectionPool"/> sweeps from a
    /// background timer. Tests drive both through <see cref="AdvanceUntil"/> and assert only what a
    /// caller can observe. The channel pool's timer mechanics are covered separately by
    /// <see cref="PoolReclaimerTest"/>.
    /// </remarks>
    public class DbConnectionPoolReclamationTest
    {
        /// <summary>
        /// How long a saturated caller is willing to wait for a connection. The pool group default
        /// is 15ms, which is too short for a caller to observably block.
        /// </summary>
        private const int PoolWaitMs = 30_000;

        /// <summary>
        /// How long a test waits for a cross-thread transition before failing. Generous, because it
        /// is only ever reached when something is actually broken.
        /// </summary>
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The pool's only slot is held by a leaked connection, so the request can only be served by
        /// reclaiming it. Without reclamation the caller waits out its entire timeout even though a
        /// slot is recoverable.
        /// </summary>
        [Theory]
        [InlineData(PoolImplementation.WaitHandle, false)]
        [InlineData(PoolImplementation.WaitHandle, true)]
        [InlineData(PoolImplementation.Channel, false)]
        [InlineData(PoolImplementation.Channel, true)]
        public void SaturatedRequest_IsServedByReclaimingLeakedConnection(PoolImplementation implementation, bool async)
        {
            FakeSqlClientMetrics metrics = new();
            FakeTimeProvider fakeTime = new();
            IDbConnectionPool pool = ConstructPool(
                implementation,
                new ChannelDbConnectionPoolTest.SuccessfulSqlConnectionFactory(metrics),
                metrics,
                fakeTime,
                maxPoolSize: 1,
                creationTimeout: PoolWaitMs);

            DbConnectionInternal leaked = CheckOutAndAbandonOwner(pool);
            CollectAbandonedOwners();
            Assert.True(leaked.IsEmancipated);

            DbConnectionInternal? served = RequestConnection(pool, async, fakeTime, out SqlConnection waitingOwner);

            Assert.Same(leaked, served);
            Assert.Equal(1, metrics.ReclaimedConnections);
            Assert.Equal(1, metrics.HardConnects);
            GC.KeepAlive(waitingOwner);
        }

        /// <summary>
        /// The connection becomes collectable only after the caller is already waiting, so a pool
        /// that reclaims once at request time cannot see it. Reclamation has to keep looking for as
        /// long as someone is blocked.
        /// </summary>
        /// <remarks>
        /// Channel-only, because this is the gap the sweep timer closes.
        /// <see cref="WaitHandleDbConnectionPool"/> reclaims only while it is still trying to create
        /// a connection; once it gives up on the creation mutex it waits on the semaphore alone and
        /// never looks again, so the caller times out with a recoverable slot sitting there.
        /// </remarks>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WaitingCaller_IsServed_WhenConnectionIsLeakedAfterTheRequestBegins(bool async)
        {
            FakeTimeProvider fakeTime = new();
            IDbConnectionPool pool = ConstructPool(
                PoolImplementation.Channel,
                timeProvider: fakeTime,
                maxPoolSize: 1,
                creationTimeout: PoolWaitMs);

            DbConnectionInternal leaked = CheckOutAndRootOwner(pool, out GCHandle leakedOwnerRoot);

            DbConnectionInternal? served = null;
            Exception? failure = null;
            SqlConnection waitingOwner = new();
            Thread caller = new(() =>
            {
                try
                {
                    served = RequestAndWait(pool, waitingOwner, async);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            })
            {
                IsBackground = true,
                Name = nameof(WaitingCaller_IsServed_WhenConnectionIsLeakedAfterTheRequestBegins)
            };
            caller.Start();

            WaitFor(
                () => (caller.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0,
                "the caller should block waiting for a connection");

            // Only now does the abandoned owner become collectable.
            leakedOwnerRoot.Free();
            CollectAbandonedOwners();
            Assert.True(leaked.IsEmancipated);

            AdvanceUntil(fakeTime, () => caller.Join(TimeSpan.Zero), "the waiting caller should be served by reclamation");

            Assert.Null(failure);
            Assert.Same(leaked, served);
            GC.KeepAlive(waitingOwner);
        }

        /// <summary>
        /// Issues a request on a background thread and drives the clock until it completes, so the
        /// caller can block on a pool that only reclaims from a timer.
        /// </summary>
        private static DbConnectionInternal? RequestConnection(
            IDbConnectionPool pool,
            bool async,
            FakeTimeProvider fakeTime,
            out SqlConnection owner)
        {
            DbConnectionInternal? connection = null;
            Exception? failure = null;
            SqlConnection waitingOwner = new();
            Thread caller = new(() =>
            {
                try
                {
                    connection = RequestAndWait(pool, waitingOwner, async);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            })
            {
                IsBackground = true,
                Name = nameof(RequestConnection)
            };
            caller.Start();

            AdvanceUntil(fakeTime, () => caller.Join(TimeSpan.Zero), "the caller should be served by reclamation");

            Assert.Null(failure);
            owner = waitingOwner;
            return connection;
        }

        /// <summary>
        /// Checks out a connection and roots its owner in a <see cref="GCHandle"/>, so the test
        /// decides exactly when the connection becomes emancipated by freeing the handle. Same
        /// non-inlining requirement as <see cref="CheckOutAndAbandonOwner"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static DbConnectionInternal CheckOutAndRootOwner(IDbConnectionPool pool, out GCHandle ownerRoot)
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
        /// Spins until <paramref name="condition"/> holds, failing the test if it does not. Used to
        /// observe a background caller reaching a blocking wait, which is a cross-thread transition
        /// and cannot be awaited directly.
        /// </summary>
        private static void WaitFor(Func<bool> condition, string because)
            => Assert.True(SpinWait.SpinUntil(condition, WaitTimeout), because);

        /// <summary>
        /// Requests a connection over the sync or async path and blocks until it is served, so both
        /// paths present the same shape to a test.
        /// </summary>
        private static DbConnectionInternal RequestAndWait(IDbConnectionPool pool, SqlConnection owner, bool async)
        {
            TaskCompletionSource<DbConnectionInternal>? completion = async ? new() : null;

            bool completed = pool.TryGetConnection(
                owner,
                completion,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(30)),
                out DbConnectionInternal? connection);

            if (completed)
            {
                Assert.NotNull(connection);
                return connection!;
            }

            // The async path hands the request off to the completion source rather than blocking.
            Assert.True(async);
            return completion!.Task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Advances <paramref name="time"/> until <paramref name="condition"/> holds, failing the
        /// test if it does not.
        /// </summary>
        /// <remarks>
        /// Lets a test assert that a pool eventually reclaims without knowing what drives it.
        /// WaitHandle reclaims inline when a request finds the pool saturated, so the condition is
        /// typically already true on the first check; the channel pool needs its sweep timer to
        /// fire. Spun in short slices rather than passing the whole timeout, so the clock only moves
        /// while the condition is unmet.
        /// </remarks>
        private static void AdvanceUntil(FakeTimeProvider time, Func<bool> condition, string because)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!SpinWait.SpinUntil(condition, TimeSpan.FromMilliseconds(50)))
            {
                Assert.True(stopwatch.Elapsed < WaitTimeout, because);
                time.Advance(TimeSpan.FromSeconds(1));
            }
        }
    }
}

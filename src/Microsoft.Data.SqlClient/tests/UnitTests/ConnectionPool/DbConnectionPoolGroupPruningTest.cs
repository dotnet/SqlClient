// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Internal;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Exercises factory pruning while a physical connection has not yet entered pool inventory.
    /// </summary>
    public class DbConnectionPoolGroupPruningTest
    {
        /// <summary>
        /// An admitted sync or async open must keep its pool registered until creation completes.
        /// Otherwise a subsequent open loses transaction affinity by using a different pool.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task Prune_DuringPhysicalCreation_PreservesPool(bool channel, bool async)
        {
            var factory = new PruningConnectionFactory();
            IDbConnectionPool pool = CreatePool(factory, channel);
            using var owner = new SqlConnection();
            DbConnectionInternal? connection = null;
            factory.Creating = () =>
            {
                Assert.Equal(0, pool.Count);
                factory.RunPruningPass();
                Assert.True(pool.IsRunning);
                Assert.Same(pool, pool.PoolGroup.GetConnectionPool(factory));
            };

            try
            {
                connection = await Acquire(pool, owner, async);
                Assert.NotNull(connection);
                Assert.Equal(1, pool.Count);
                factory.RunPruningPass();
                Assert.Same(pool, pool.PoolGroup.GetConnectionPool(factory));
                pool.ReturnInternalConnection(connection!, owner);
                connection = null;
                factory.Creating = null;
                connection = await Acquire(pool, owner, async);
                Assert.Equal(1, factory.CreateCount);
            }
            finally
            {
                if (connection != null)
                {
                    pool.ReturnInternalConnection(connection, owner);
                }
                Drain(factory, pool);
            }
            Assert.False(factory.IsPruningTimerActive);
        }

        /// <summary>
        /// A failed create must release admission on both entry paths, allowing an empty
        /// pool without a blocking period to retire and its demand-driven timer to stop.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task Prune_AfterCreationFailure_ReleasesAdmission(bool channel, bool async)
        {
            var factory = new PruningConnectionFactory();
            IDbConnectionPool pool = CreatePool(factory, channel);
            using var owner = new SqlConnection();
            factory.Creating = () => throw new InvalidOperationException("Expected creation failure.");
            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => Acquire(pool, owner, async));
                Assert.True(SpinWait.SpinUntil(pool.TryPrune, TimeSpan.FromSeconds(10)));
                Assert.Equal(0, pool.Count);
            }
            finally
            {
                Drain(factory, pool);
            }
            Assert.False(factory.IsPruningTimerActive);
        }

        /// <summary>
        /// Cancellation completes the caller's task, not the physical worker. A concurrent
        /// pruning pass must not retire that worker's pool; its eventual failure releases admission.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Prune_CancelledOpen_ProtectsWorkerUntilItExits(bool channel)
        {
            var factory = new PruningConnectionFactory();
            IDbConnectionPool pool = CreatePool(factory, channel);
            using var owner = new SqlConnection();
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            factory.Creating = () =>
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(15)));
                throw new InvalidOperationException("Expected creation failure after cancellation.");
            };
            var completion = new TaskCompletionSource<DbConnectionInternal>();
            try
            {
                Assert.False(pool.TryGetConnection(owner, completion, TimeoutTimer.StartNew(TimeSpan.FromSeconds(10)), out _));
                Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
                completion.SetCanceled();
                Assert.Equal(0, pool.Count);
                Task pruning = Task.Run(factory.RunPruningPass);
                Assert.Same(pruning, await Task.WhenAny(pruning, Task.Delay(TimeSpan.FromSeconds(10))));
                await pruning;
                Assert.True(pool.IsRunning);
                Assert.Same(pool, pool.PoolGroup.GetConnectionPool(factory));
            }
            finally
            {
                release.Set();
                Assert.True(SpinWait.SpinUntil(pool.TryPrune, TimeSpan.FromSeconds(10)));
                Drain(factory, pool);
            }
            Assert.False(factory.IsPruningTimerActive);
        }

        /// <summary>
        /// A caller retaining an empty pool across pruning must be redirected without starting
        /// a physical open or queuing an async worker on the retired pool.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Prune_BeforeAdmission_RejectsStalePool(bool channel, bool async)
        {
            var factory = new PruningConnectionFactory();
            IDbConnectionPool pool = CreatePool(factory, channel);
            using var owner = new SqlConnection();
            var completion = async ? new TaskCompletionSource<DbConnectionInternal>() : null;
            try
            {
                factory.RunPruningPass();
                Assert.False(pool.IsRunning);
                Assert.True(pool.TryGetConnection(owner, completion, TimeoutTimer.StartNew(TimeSpan.FromSeconds(10)), out var connection));
                Assert.Null(connection);
                Assert.False(completion?.Task.IsCompleted ?? false);
                Assert.Equal(0, factory.CreateCount);
            }
            finally
            {
                Drain(factory, pool);
            }
            Assert.False(factory.IsPruningTimerActive);
        }

        /// <summary>
        /// Replacement requests also reject a stale pool, allowing the factory to select a
        /// live pool rather than creating on one already queued for release.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Prune_BeforeReplacement_RejectsStalePool(bool channel)
        {
            var factory = new PruningConnectionFactory();
            IDbConnectionPool pool = CreatePool(factory, channel);
            using var owner = new SqlConnection();
            var oldConnection = new WaitHandleDbConnectionPoolTransactionTest.MockDbConnectionInternal();
            try
            {
                factory.RunPruningPass();
                Assert.Null(pool.ReplaceConnection(owner, oldConnection, TimeoutTimer.StartNew(TimeSpan.FromSeconds(10))));
                Assert.False(pool.IsRunning);
                Assert.Equal(0, factory.CreateCount);
            }
            finally
            {
                oldConnection.Dispose();
                Drain(factory, pool);
            }
        }

        /// <summary>
        /// Background minimum-size replenishment must protect its uncounted physical creation,
        /// independently of any user acquisition.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Prune_DuringWarmup_PreservesPool(bool channel)
        {
            var factory = new PruningConnectionFactory();
            IDbConnectionPool pool = CreatePool(factory, channel, minPoolSize: 1, start: false);
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            factory.Creating = () =>
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(15)));
            };
            try
            {
                pool.Startup();
                Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
                Assert.Equal(0, pool.Count);
                Assert.False(pool.TryPrune());
                Assert.True(pool.IsRunning);
            }
            finally
            {
                release.Set();
                Assert.True(SpinWait.SpinUntil(() => pool.IdleCount == 1, TimeSpan.FromSeconds(10)));
                Drain(factory, pool);
            }
            Assert.False(factory.IsPruningTimerActive);
        }

        /// <summary>
        /// Exhausting the pool releases the timed-out request's admission so later cleanup
        /// can retire the pool. This covers both synchronous waits and async worker timeouts.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task Prune_AfterPoolTimeout_ReleasesAdmission(bool channel, bool async)
        {
            var factory = new PruningConnectionFactory();
            IDbConnectionPool pool = CreatePool(factory, channel, connectionTimeout: 1);
            using var owner = new SqlConnection();
            using var waiter = new SqlConnection();
            DbConnectionInternal? connection = await Acquire(pool, owner, async: false);
            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => Acquire(pool, waiter, async));
            }
            finally
            {
                pool.ReturnInternalConnection(connection!, owner);
                pool.Clear();
                Assert.True(SpinWait.SpinUntil(pool.TryPrune, TimeSpan.FromSeconds(10)));
                Drain(factory, pool);
            }
        }

        /// <summary>
        /// A creation failure's blocking period must continue throttling requests rather
        /// than being discarded with the empty pool.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Prune_BlockingPeriod_RetainsEmptyPool(bool channel)
        {
            var factory = new PruningConnectionFactory();
            IDbConnectionPool pool = CreatePool(factory, channel, blockingPeriod: "AlwaysBlock");
            using var owner = new SqlConnection();
            factory.Creating = () => throw new InvalidOperationException("Expected creation failure.");
            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => Acquire(pool, owner, async: false));
                Assert.True(pool.ErrorOccurred);
                factory.RunPruningPass();
                Assert.True(pool.IsRunning);
                Assert.Same(pool, pool.PoolGroup.GetConnectionPool(factory));
            }
            finally
            {
                Drain(factory, pool);
            }
        }

        /// <summary>
        /// Clears only this isolated test group and drains its factory's deferred cleanup.
        /// </summary>
        private static void Drain(PruningConnectionFactory factory, IDbConnectionPool pool)
        {
            pool.PoolGroup.Clear();
            for (int pass = 0; pass < 4; pass++)
            {
                factory.RunPruningPass();
            }
        }

        /// <summary>
        /// Registers a chosen pool implementation without changing the process-wide pool switch.
        /// </summary>
        private static IDbConnectionPool CreatePool(
            PruningConnectionFactory factory, bool channel, int minPoolSize = 0, bool start = true,
            int connectionTimeout = 10, string blockingPeriod = "NeverBlock")
        {
            string connectionString = $"Data Source=localhost;Max Pool Size=1;Min Pool Size={minPoolSize};Connect Timeout={connectionTimeout};Pool Blocking Period={blockingPeriod};";
            var key = new ConnectionPoolKey(connectionString, null, null, null, null);
            SqlConnectionOptions options = null!;
            DbConnectionPoolGroup group = factory.GetConnectionPoolGroup(key, null, ref options);
            IDbConnectionPool pool = channel
                ? new ChannelDbConnectionPool(factory, group, DbConnectionPoolIdentity.NoIdentity, new DbConnectionPoolProviderInfo())
                : new WaitHandleDbConnectionPool(factory, group, DbConnectionPoolIdentity.NoIdentity, new DbConnectionPoolProviderInfo());
            var pools = (ConcurrentDictionary<DbConnectionPoolIdentity, IDbConnectionPool>)
                typeof(DbConnectionPoolGroup).GetField("_poolCollection", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(group)!;
            Assert.True(pools.TryAdd(DbConnectionPoolIdentity.NoIdentity, pool));
            SqlClientDiagnostics.Metrics.EnterActiveConnectionPool();
            if (start)
            {
                pool.Startup();
            }
            return pool;
        }

        /// <summary>
        /// Acquires through the actual sync or queued async pool entry point with a bounded wait.
        /// </summary>
        private static async Task<DbConnectionInternal?> Acquire(IDbConnectionPool pool, SqlConnection owner, bool async)
        {
            var completion = async ? new TaskCompletionSource<DbConnectionInternal>() : null;
            if (pool.TryGetConnection(owner, completion, TimeoutTimer.StartNew(TimeSpan.FromMilliseconds(pool.PoolGroupOptions.CreationTimeout)), out var connection))
            {
                if (connection == null && pool.IsRunning)
                {
                    throw new InvalidOperationException("Pool acquisition timed out.");
                }
                return connection;
            }
            Assert.NotNull(completion);
            Assert.Same(completion!.Task, await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(15))));
            return await completion.Task;
        }

        /// <summary>
        /// Invokes a deterministic hook inside physical creation, with autonomous pruning disabled.
        /// </summary>
        private sealed class PruningConnectionFactory : SqlConnectionFactory
        {
            internal Action? Creating { get; set; }
            internal int CreateCount { get; private set; }

            internal PruningConnectionFactory() : base(TimeSpan.FromDays(1), TimeSpan.FromDays(1))
            {
            }

            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
            {
                CreateCount++;
                Creating?.Invoke();
                return new WaitHandleDbConnectionPoolTransactionTest.MockDbConnectionInternal();
            }
        }
    }
}

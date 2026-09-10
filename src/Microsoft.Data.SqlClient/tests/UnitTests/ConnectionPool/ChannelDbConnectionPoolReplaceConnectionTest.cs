// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Transactions;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Unit tests for <see cref="ChannelDbConnectionPool.ReplaceConnection(System.Data.Common.DbConnection, Microsoft.Data.ProviderBase.DbConnectionInternal, Microsoft.Data.ProviderBase.TimeoutTimer)"/>,
    /// covering idle reuse, new-connection creation, pool-slot accounting at and below capacity, and the
    /// failure paths that keep the old connection available for the caller's reconnect retry loop.
    /// </summary>
    public class ChannelDbConnectionPoolReplaceConnectionTest
    {
        /// <summary>
        /// The factory backing every pool built by these tests. xUnit constructs a fresh instance of
        /// the test class for each test, so this is not shared state across tests and each test is
        /// free to toggle its tunable flags.
        /// </summary>
        private readonly TunableSqlConnectionFactory _factory = new();

        /// <summary>
        /// Builds a <see cref="ChannelDbConnectionPool"/> for the replacement tests. A frozen
        /// <see cref="FakeTimeProvider"/> is injected by default so time-driven background
        /// maintenance (idle-timeout pruning, warmup/replenishment, blocking-period expiry)
        /// cannot advance and race the assertions. Pass an explicit <paramref name="timeProvider"/>
        /// only when a test needs to drive time forward deterministically.
        /// </summary>
        /// <remarks>
        /// The connection string pins Pool Blocking Period to AlwaysBlock rather than relying on
        /// the default (Auto). Auto derives the policy from ADP.IsAzureSqlServerEndpoint(DataSource),
        /// which reads the process-wide mutable ADP.s_azureSqlServerEndpoints list. Other tests in
        /// this assembly (e.g. ConnectionRoutingTestsAzure) register "localhost" as an Azure endpoint
        /// for the duration of their run, and xUnit executes separate collections in parallel. Under
        /// Auto that would classify our localhost pool as Azure, skip creating the blocking-period
        /// error state entirely, and make every blocking-period assertion below flaky. Pinning the
        /// policy makes these tests independent of that global state.
        /// </remarks>
        private ChannelDbConnectionPool ConstructPool(
            SqlConnectionFactory connectionFactory,
            DbConnectionPoolGroupOptions? poolGroupOptions = null,
            TimeProvider? timeProvider = null)
        {
            poolGroupOptions ??= new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 50,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: 0
            );
            var dbConnectionPoolGroup = new DbConnectionPoolGroup(
                new SqlConnectionOptions("Data Source=localhost;Pool Blocking Period=AlwaysBlock;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions
            );
            return new ChannelDbConnectionPool(
                connectionFactory,
                dbConnectionPoolGroup,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo(),
                timeProvider: timeProvider ?? new FakeTimeProvider()
            );
        }

        #region Story 1 — Transparent Replacement

        /// <summary>
        /// Verifies that <see cref="ChannelDbConnectionPool.ReplaceConnection"/> returns a
        /// non-null connection that is a different instance from the one being replaced.
        /// </summary>
        [Fact]
        public void ReplaceConnection_ReturnsNewConnection()
        {
            // Arrange
            var pool = ConstructPool(_factory);
            SqlConnection owner = new();

            pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? oldConnection);

            Assert.NotNull(oldConnection);

            // Act
            var newConnection = pool.ReplaceConnection(
                owner,
                oldConnection,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

            // Assert
            Assert.NotNull(newConnection);
            Assert.NotSame(oldConnection, newConnection);
        }

        /// <summary>
        /// Verifies that after a replacement the old connection is disposed and can no longer
        /// be pooled.
        /// </summary>
        [Fact]
        public void ReplaceConnection_OldConnectionIsDisposed()
        {
            // Arrange
            var pool = ConstructPool(_factory);
            SqlConnection owner = new();

            pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? oldConnection);

            Assert.NotNull(oldConnection);

            // Act
            pool.ReplaceConnection(
                owner,
                oldConnection,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

            // Assert — the old connection should be disposed (not poolable)
            Assert.False(oldConnection.CanBePooled);
        }

        #endregion

        #region Story 3 — Pool Capacity Preservation (new physical connection path)

        /// <summary>
        /// Verifies that replacing a connection when no idle connections are available reuses
        /// the old connection's slot so the pool's total count remains unchanged.
        /// </summary>
        [Fact]
        public void ReplaceConnection_NewPhysicalConnection_PoolCountUnchanged()
        {
            // Arrange — single connection, no idle connections available
            var pool = ConstructPool(_factory);
            SqlConnection owner = new();

            pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? oldConnection);

            Assert.NotNull(oldConnection);
            Assert.Equal(0, pool.IdleCount);
            int countBefore = pool.Count;

            // Act
            pool.ReplaceConnection(
                owner,
                oldConnection,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

            // Assert — slot was reused, count unchanged
            Assert.Equal(countBefore, pool.Count);
        }

        /// <summary>
        /// Verifies that replacing a connection in a pool that is already filled to its maximum
        /// capacity succeeds without exceeding the maximum pool size.
        /// </summary>
        [Fact]
        public void ReplaceConnection_AtMaxCapacity_PoolCountUnchanged()
        {
            // Arrange — fill pool to max capacity, no idle connections
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 3,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: 0
            );
            var pool = ConstructPool(_factory, poolGroupOptions);

            SqlConnection owner1 = new();
            SqlConnection owner2 = new();
            SqlConnection owner3 = new();

            pool.TryGetConnection(owner1, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn1);
            pool.TryGetConnection(owner2, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn2);
            pool.TryGetConnection(owner3, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn3);

            Assert.Equal(3, pool.Count);

            // Act — replace connection in a full pool
            var newConnection = pool.ReplaceConnection(
                owner1,
                conn1!,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

            // Assert — pool count must not exceed max
            Assert.NotNull(newConnection);
            Assert.NotSame(conn1, newConnection);
            Assert.Equal(3, pool.Count);
        }

        #endregion

        #region Story 4 — Replacement Failure Propagation

        /// <summary>
        /// Verifies that when creating the replacement connection fails, the exception thrown by
        /// the connection factory is propagated to the caller.
        /// </summary>
        [Fact]
        public void ReplaceConnection_CreationFails_ExceptionPropagated()
        {
            // Arrange — use a factory that succeeds initially then fails
            var pool = ConstructPool(_factory);
            SqlConnection owner = new();

            pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? oldConnection);

            Assert.NotNull(oldConnection);

            // Switch to failing mode
            _factory.FailOnCreate = true;

            // Act & Assert — exception from factory is propagated
            Assert.Throws<InvalidOperationException>(() =>
                pool.ReplaceConnection(
                    owner,
                    oldConnection,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));
        }

        /// <summary>
        /// Verifies that when creating the replacement connection fails, the old connection is left fully
        /// intact - it keeps its pool slot and stays poolable - so the caller's reconnect retry loop can reuse
        /// it on a subsequent attempt. The pool count is unchanged. The failed physical open enters the
        /// blocking-period error state (mirroring the normal acquire path and the WaitHandle pool), so the
        /// caller's retry succeeds only once that period expires.
        /// </summary>
        [Fact]
        public void ReplaceConnection_CreationFails_OldConnectionRetainedForRetry()
        {
            // Arrange — fill the pool to capacity so a leaked or prematurely released slot would be observable.
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 2,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: 0
            );
            var fakeTime = new FakeTimeProvider();
            var pool = ConstructPool(_factory, poolGroupOptions, timeProvider: fakeTime);

            SqlConnection owner1 = new();
            SqlConnection owner2 = new();
            pool.TryGetConnection(owner1, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? oldConnection);
            pool.TryGetConnection(owner2, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? otherConnection);

            Assert.NotNull(oldConnection);
            Assert.Equal(2, pool.Count);

            // Switch to failing mode so the replacement creation throws.
            _factory.FailOnCreate = true;

            // Act — replacement fails
            Assert.Throws<InvalidOperationException>(() =>
                pool.ReplaceConnection(
                    owner1,
                    oldConnection!,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));

            // Assert — the old connection is left intact so the caller can retry with it: its slot is retained
            // (no premature release) ...
            Assert.Equal(2, pool.Count);
            // ... it is not doomed, so it remains usable for the retry ...
            Assert.False(oldConnection!.IsConnectionDoomed);
            // ... it is still owned by the same caller (not released back to the pool) ...
            Assert.Same(owner1, oldConnection!.Owner);
            // ... it keeps its reference to the pool, which is what enables the caller's retry ...
            Assert.Same(pool, oldConnection!.Pool);
            // ... and the failed physical open entered the blocking period, mirroring the normal
            // acquire path and the WaitHandle pool, so subsequent opens fast-fail until it expires.
            Assert.True(pool.ErrorOccurred);

            // The reconnect retry loop reuses the SAME old connection. Advancing past the blocking
            // period fires the exit timer (FakeTimeProvider invokes it synchronously), after which a
            // subsequent successful replacement reuses the retained slot and keeps the count unchanged.
            _factory.FailOnCreate = false;
            fakeTime.Advance(TimeSpan.FromSeconds(5));
            Assert.False(pool.ErrorOccurred);
            var newConnection = pool.ReplaceConnection(
                owner1,
                oldConnection!,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));
            Assert.NotNull(newConnection);
            Assert.NotSame(oldConnection, newConnection);
            Assert.Equal(2, pool.Count);
        }

        #endregion

        #region Story 5 — Activation Failure Rollback

        /// <summary>
        /// Verifies that when activating the replacement connection fails, the exception is
        /// propagated to the caller.
        /// </summary>
        [Fact]
        public void ReplaceConnection_ActivationFails_ExceptionPropagated()
        {
            // Arrange
            var pool = ConstructPool(_factory);
            SqlConnection owner = new();

            _factory.FailOnActivate = false;
            pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? oldConnection);

            Assert.NotNull(oldConnection);

            // Now make activation fail for the replacement
            _factory.FailOnActivate = true;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                pool.ReplaceConnection(
                    owner,
                    oldConnection,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));
        }

        /// <summary>
        /// Verifies that when activating the replacement connection fails, the newly created
        /// connection is disposed (never taking a pool slot) and the old connection is left intact,
        /// so the pool's physical connection count is unchanged and nothing is leaked.
        /// </summary>
        [Fact]
        public void ReplaceConnection_ActivationFails_NewConnectionDisposed_PoolCountStable()
        {
            // Arrange
            var pool = ConstructPool(_factory);
            SqlConnection owner = new();

            _factory.FailOnActivate = false;
            pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? oldConnection);

            Assert.NotNull(oldConnection);
            int countBefore = pool.Count;

            // Make activation fail
            _factory.FailOnActivate = true;

            // Act
            Assert.Throws<InvalidOperationException>(() =>
                pool.ReplaceConnection(
                    owner,
                    oldConnection,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));

            // Assert — the new connection never took a slot and is disposed on the failure path,
            // while the old connection is left in place for the caller's reconnect retry loop, so
            // the pool's physical connection count is unchanged (nothing leaked).
            Assert.Equal(countBefore, pool.Count);
        }

        #endregion

        #region Story 6 — Prefer Idle Connection Reuse

        /// <summary>
        /// Verifies that when a live idle connection is available, replacement reuses it instead of
        /// establishing a new physical connection. The reused connection keeps its own pool slot and
        /// the replaced connection's slot is freed, so the pool's physical connection count drops by
        /// one and never exceeds the maximum.
        /// </summary>
        [Fact]
        public void ReplaceConnection_PrefersIdleOverNewConnection()
        {
            // Arrange — open two connections, then return one so it becomes an idle connection.
            var pool = ConstructPool(_factory);
            SqlConnection owner1 = new();
            SqlConnection owner2 = new();

            pool.TryGetConnection(owner1, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn1);
            pool.TryGetConnection(owner2, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn2);
            Assert.NotNull(conn1);
            Assert.NotNull(conn2);

            pool.ReturnInternalConnection(conn2!, owner2);
            Assert.Equal(1, pool.IdleCount);
            Assert.Equal(2, pool.Count);

            // Act — replace conn1. The idle conn2 should be reused rather than creating a new connection.
            var newConnection = pool.ReplaceConnection(
                owner1,
                conn1!,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

            // Assert — the replacement is the previously idle connection ...
            Assert.Same(conn2, newConnection);
            // ... the idle channel was drained ...
            Assert.Equal(0, pool.IdleCount);
            // ... the replaced connection was disposed ...
            Assert.False(conn1!.CanBePooled);
            // ... and its slot was freed, so the pool now holds a single physical connection.
            Assert.Equal(1, pool.Count);
        }

        /// <summary>
        /// Verifies that reusing an idle connection while the pool is at maximum capacity succeeds and
        /// frees the replaced connection's slot, so the pool count never exceeds the maximum.
        /// </summary>
        [Fact]
        public void ReplaceConnection_IdleReuse_AtMaxCapacity_FreesOldSlot()
        {
            // Arrange — fill the pool to max capacity, then return one connection so it is idle.
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 3,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: 0
            );
            var pool = ConstructPool(_factory, poolGroupOptions);

            SqlConnection owner1 = new();
            SqlConnection owner2 = new();
            SqlConnection owner3 = new();

            pool.TryGetConnection(owner1, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn1);
            pool.TryGetConnection(owner2, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn2);
            pool.TryGetConnection(owner3, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn3);
            Assert.Equal(3, pool.Count);

            pool.ReturnInternalConnection(conn3!, owner3);
            Assert.Equal(1, pool.IdleCount);
            Assert.Equal(3, pool.Count);

            // Act — replace conn1 while at max capacity; the idle conn3 should be reused.
            var newConnection = pool.ReplaceConnection(
                owner1,
                conn1!,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

            // Assert — the idle connection was reused and conn1's slot was freed, dropping below max.
            Assert.Same(conn3, newConnection);
            Assert.Equal(0, pool.IdleCount);
            Assert.Equal(2, pool.Count);
        }

        /// <summary>
        /// Verifies that when activating a reused idle connection fails, the connection is returned to
        /// the pool (not leaked or discarded) and the connection being replaced is left untouched, so
        /// the caller's reconnect retry loop can try again.
        /// </summary>
        [Fact]
        public void ReplaceConnection_IdleReuse_ActivationFails_ReturnedToPool()
        {
            // Arrange — open two connections, then return one so it becomes an idle connection.
            var pool = ConstructPool(_factory);
            SqlConnection owner1 = new();
            SqlConnection owner2 = new();

            _factory.FailOnActivate = false;
            pool.TryGetConnection(owner1, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn1);
            pool.TryGetConnection(owner2, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn2);
            Assert.NotNull(conn1);
            Assert.NotNull(conn2);

            pool.ReturnInternalConnection(conn2!, owner2);
            Assert.Equal(1, pool.IdleCount);
            Assert.Equal(2, pool.Count);

            // Make the idle-reuse activation fail.
            _factory.FailOnActivate = true;

            // Act — ReplaceConnection pulls the idle conn2 and fails to activate it.
            Assert.Throws<InvalidOperationException>(() =>
                pool.ReplaceConnection(
                    owner1,
                    conn1!,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));

            // Assert — the reused connection was returned to the idle pool (not leaked or discarded) ...
            Assert.Equal(1, pool.IdleCount);
            // ... nothing was removed, so both connections still hold their slots ...
            Assert.Equal(2, pool.Count);
            // ... and the connection being replaced was left untouched and still healthy.
            Assert.False(conn1!.IsConnectionDoomed);
        }

        #endregion

        #region Story 7 — New Physical Connection Fallback

        /// <summary>
        /// Verifies that when no idle connection is available, replacement creates a new
        /// physical connection distinct from the one being replaced.
        /// </summary>
        [Fact]
        public void ReplaceConnection_NoIdleConnection_CreatesNew()
        {
            // Arrange
            var pool = ConstructPool(_factory);
            SqlConnection owner = new();

            pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn1);
            Assert.NotNull(conn1);
            Assert.Equal(0, pool.IdleCount);

            // Act — no idle connections available, should create new
            var newConnection = pool.ReplaceConnection(
                owner,
                conn1,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

            // Assert
            Assert.NotNull(newConnection);
            Assert.NotSame(conn1, newConnection);
            Assert.Equal(1, pool.Count);
        }

        /// <summary>
        /// Verifies connection resiliency can replace a broken connection after its return path has
        /// already removed it from the pool and released its slot.
        /// </summary>
        [Fact]
        public void ReplaceConnection_OldConnectionAlreadyRemoved_AcquiresNewSlot()
        {
            // Arrange
            var poolGroupOptions = new DbConnectionPoolGroupOptions(
                poolByIdentity: false,
                minPoolSize: 0,
                maxPoolSize: 1,
                creationTimeout: 15,
                loadBalanceTimeout: 0,
                hasTransactionAffinity: true,
                idleTimeout: 0
            );
            var pool = ConstructPool(_factory, poolGroupOptions);
            SqlConnection owner = new();

            pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? connection);
            StubDbConnectionInternal oldConnection = Assert.IsType<StubDbConnectionInternal>(connection);
            oldConnection.Doom();
            pool.ReturnInternalConnection(oldConnection, owner);

            Assert.Equal(0, pool.Count);
            Assert.Null(oldConnection.Pool);

            // Act
            DbConnectionInternal newConnection = pool.ReplaceConnection(
                owner,
                oldConnection,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

            // Assert
            Assert.NotSame(oldConnection, newConnection);
            Assert.Equal(1, pool.Count);
            Assert.Same(pool, newConnection.Pool);
            Assert.Same(owner, newConnection.Owner);
        }

        #endregion

        #region Blocking Period

        /// <summary>
        /// Verifies that the new-physical-connection branch of
        /// <see cref="ChannelDbConnectionPool.ReplaceConnection"/> respects the pool's blocking period:
        /// while the pool is in the blocking-period error state it fast-fails with the cached exception
        /// instead of opening another physical connection, and it leaves the old connection intact for
        /// the caller's reconnect retry. Idle reuse is intentionally exempt, matching the normal acquire path.
        /// </summary>
        [Fact]
        public void ReplaceConnection_NewPhysicalConnection_RespectsBlockingPeriod()
        {
            // Arrange — ConstructPool pins Pool Blocking Period=AlwaysBlock, so the blocking period is enabled.
            var pool = ConstructPool(_factory);
            SqlConnection owner = new();

            // Check out a connection to later replace (creation succeeds).
            _factory.FailOnCreate = false;
            pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? oldConnection);
            Assert.NotNull(oldConnection);

            // Drive the pool into the blocking-period error state with a failed physical create.
            _factory.FailOnCreate = true;
            var originalException = Assert.Throws<InvalidOperationException>(() =>
                pool.TryGetConnection(new SqlConnection(), null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out _));
            Assert.True(pool.ErrorOccurred);

            // Act & Assert — a replacement that must open a new physical connection (no idle available)
            // fast-fails during the blocking period rather than hammering the unhealthy server.
            // Flipping the factory back to succeeding proves the create path was never reached: the
            // throw can only be the cached exception, which ThrowIfActive rethrows as-is for
            // non-SqlException types, so it is the very same instance captured above.
            _factory.FailOnCreate = false;
            var replaceException = Assert.Throws<InvalidOperationException>(() =>
                pool.ReplaceConnection(owner, oldConnection!, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));
            Assert.Same(originalException, replaceException);

            // The pool is still blocking and the old connection is untouched, so the caller can retry with it.
            Assert.True(pool.ErrorOccurred);
            Assert.False(oldConnection!.IsConnectionDoomed);
            Assert.Same(pool, oldConnection!.Pool);
        }

        /// <summary>
        /// Verifies that when the new-physical-connection branch of
        /// <see cref="ChannelDbConnectionPool.ReplaceConnection"/> fails to open (server unreachable),
        /// the pool enters the blocking-period error state, mirroring the normal acquire path
        /// (OpenNewInternalConnection) and the legacy WaitHandle pool's CreateObject. This lets
        /// subsequent opens fast-fail instead of hammering the unhealthy server, while the old
        /// connection is left intact for the caller's reconnect retry loop.
        /// </summary>
        [Fact]
        public void ReplaceConnection_NewPhysicalConnectionFails_EntersBlockingPeriod()
        {
            // Arrange — ConstructPool pins Pool Blocking Period=AlwaysBlock, so the blocking period is enabled.
            var pool = ConstructPool(_factory);
            SqlConnection owner = new();

            // Check out a connection to later replace (creation succeeds), leaving no idle connection
            // so the replacement is forced down the new-physical-connection branch.
            _factory.FailOnCreate = false;
            pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? oldConnection);
            Assert.NotNull(oldConnection);
            Assert.False(pool.ErrorOccurred);

            // Act — the replacement's physical open fails.
            _factory.FailOnCreate = true;
            Assert.Throws<InvalidOperationException>(() =>
                pool.ReplaceConnection(owner, oldConnection!, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));

            // Assert — the failed open poisoned the pool into the blocking period, and the old
            // connection is left intact so the caller can retry with it.
            Assert.True(pool.ErrorOccurred);
            Assert.False(oldConnection!.IsConnectionDoomed);
            Assert.Same(pool, oldConnection!.Pool);
        }

        /// <summary>
        /// Verifies that when a replacement's physical open succeeds but activation fails, the pool
        /// does NOT enter the blocking-period error state. A reachable server that fails activation
        /// is not a connectivity failure, so poisoning the pool would be wrong. This mirrors the
        /// legacy WaitHandle pool, where PrepareConnection (activation) runs outside CreateObject's
        /// error-state catch.
        /// </summary>
        [Fact]
        public void ReplaceConnection_ActivationFails_DoesNotEnterBlockingPeriod()
        {
            // Arrange
            var pool = ConstructPool(_factory);
            SqlConnection owner = new();

            _factory.FailOnActivate = false;
            pool.TryGetConnection(owner, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? oldConnection);
            Assert.NotNull(oldConnection);
            Assert.False(pool.ErrorOccurred);

            // Act — the replacement opens successfully but fails during activation.
            _factory.FailOnActivate = true;
            Assert.Throws<InvalidOperationException>(() =>
                pool.ReplaceConnection(owner, oldConnection!, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));

            // Assert — activation failure does not poison the pool: the server proved reachable.
            Assert.False(pool.ErrorOccurred);
        }

        #endregion

        #region Test Helper Classes

        /// <summary>
        /// A single tunable connection factory used by all tests in this class. Set
        /// <see cref="FailOnCreate"/> to simulate a failed physical open, and
        /// <see cref="FailOnActivate"/> to simulate a connection that opens but fails activation.
        /// Connections read <see cref="FailOnActivate"/> live at activation time, so it can be
        /// toggled after a connection has been created (as idle-reuse tests require).
        /// </summary>
        internal class TunableSqlConnectionFactory : SqlConnectionFactory
        {
            /// <summary>When true, <see cref="CreateConnection"/> throws instead of returning a connection.</summary>
            internal bool FailOnCreate { get; set; }

            /// <summary>When true, activating any connection from this factory throws.</summary>
            internal bool FailOnActivate { get; set; }

            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
            {
                if (FailOnCreate)
                {
                    throw new InvalidOperationException("Simulated connection failure");
                }

                return new StubDbConnectionInternal(this);
            }
        }

        /// <summary>
        /// A minimal <see cref="DbConnectionInternal"/> stub whose activation behaviour is driven by
        /// the <see cref="TunableSqlConnectionFactory"/> that created it. The flag is read live so a
        /// test can make activation fail on a connection that was created earlier.
        /// </summary>
        internal class StubDbConnectionInternal : DbConnectionInternal
        {
            private readonly TunableSqlConnectionFactory? _factory;

            internal StubDbConnectionInternal(TunableSqlConnectionFactory? factory = null)
            {
                _factory = factory;
            }

            public override string ServerVersion => throw new NotImplementedException();

            public override DbTransaction BeginTransaction(System.Data.IsolationLevel il)
            {
                throw new NotImplementedException();
            }

            public override void EnlistTransaction(Transaction transaction)
            {
                return;
            }

            protected override void Activate(Transaction transaction)
            {
                if (_factory?.FailOnActivate == true)
                {
                    throw new InvalidOperationException("Simulated activation failure");
                }
            }

            protected override void Deactivate()
            {
                return;
            }

            internal override void ResetConnection()
            {
                return;
            }

            internal void Doom() => DoomThisConnection();
        }

        #endregion
    }
}

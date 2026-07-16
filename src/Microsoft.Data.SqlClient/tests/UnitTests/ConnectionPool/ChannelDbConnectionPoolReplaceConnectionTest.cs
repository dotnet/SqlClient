// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
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
        private static readonly SqlConnectionFactory SuccessfulConnectionFactory = new SuccessfulSqlConnectionFactory();

        private ChannelDbConnectionPool ConstructPool(
            SqlConnectionFactory connectionFactory,
            DbConnectionPoolGroupOptions? poolGroupOptions = null)
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
                new SqlConnectionOptions("Data Source=localhost;"),
                new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
                poolGroupOptions
            );
            return new ChannelDbConnectionPool(
                connectionFactory,
                dbConnectionPoolGroup,
                DbConnectionPoolIdentity.NoIdentity,
                new DbConnectionPoolProviderInfo()
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
            var pool = ConstructPool(SuccessfulConnectionFactory);
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
            var pool = ConstructPool(SuccessfulConnectionFactory);
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
            var pool = ConstructPool(SuccessfulConnectionFactory);
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
            var pool = ConstructPool(SuccessfulConnectionFactory, poolGroupOptions);

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
            var switchableFactory = new SwitchableSqlConnectionFactory();
            var pool = ConstructPool(switchableFactory);
            SqlConnection owner = new();

            pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? oldConnection);

            Assert.NotNull(oldConnection);

            // Switch to failing mode
            switchableFactory.ShouldFail = true;

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
        /// it on a subsequent attempt. The pool count is unchanged and the pool is not left in an error state.
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
            var switchableFactory = new SwitchableSqlConnectionFactory();
            var pool = ConstructPool(switchableFactory, poolGroupOptions);

            SqlConnection owner1 = new();
            SqlConnection owner2 = new();
            pool.TryGetConnection(owner1, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? oldConnection);
            pool.TryGetConnection(owner2, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? otherConnection);

            Assert.NotNull(oldConnection);
            Assert.Equal(2, pool.Count);

            // Switch to failing mode so the replacement creation throws.
            switchableFactory.ShouldFail = true;

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
            // ... and the pool is not left in an error state.
            Assert.False(pool.ErrorOccurred);

            // The reconnect retry loop reuses the SAME old connection: a subsequent successful replacement
            // succeeds, reusing the retained slot and keeping the pool count unchanged.
            switchableFactory.ShouldFail = false;
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
            var factory = new ActivationFailSqlConnectionFactory();
            var pool = ConstructPool(factory);
            SqlConnection owner = new();

            factory.FailOnActivate = false;
            pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? oldConnection);

            Assert.NotNull(oldConnection);

            // Now make activation fail for the replacement
            factory.FailOnActivate = true;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                pool.ReplaceConnection(
                    owner,
                    oldConnection,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));
        }

        /// <summary>
        /// Verifies that when activating the replacement connection fails, the newly created
        /// connection is returned to the pool rather than leaked, keeping the pool count stable.
        /// </summary>
        [Fact]
        public void ReplaceConnection_ActivationFails_NewConnectionReturnedToPool()
        {
            // Arrange
            var factory = new ActivationFailSqlConnectionFactory();
            var pool = ConstructPool(factory);
            SqlConnection owner = new();

            factory.FailOnActivate = false;
            pool.TryGetConnection(
                owner,
                taskCompletionSource: null,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                out DbConnectionInternal? oldConnection);

            Assert.NotNull(oldConnection);
            int countBefore = pool.Count;

            // Make activation fail
            factory.FailOnActivate = true;

            // Act
            Assert.Throws<InvalidOperationException>(() =>
                pool.ReplaceConnection(
                    owner,
                    oldConnection,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));

            // Assert — the new connection was returned to pool (not leaked).
            // Pool count stays same because the new connection replaced the old one's slot
            // and was then returned to idle.
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
            var pool = ConstructPool(SuccessfulConnectionFactory);
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
            var pool = ConstructPool(SuccessfulConnectionFactory, poolGroupOptions);

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
            var factory = new ActivationFailSqlConnectionFactory();
            var pool = ConstructPool(factory);
            SqlConnection owner1 = new();
            SqlConnection owner2 = new();

            factory.FailOnActivate = false;
            pool.TryGetConnection(owner1, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn1);
            pool.TryGetConnection(owner2, null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)), out DbConnectionInternal? conn2);
            Assert.NotNull(conn1);
            Assert.NotNull(conn2);

            pool.ReturnInternalConnection(conn2!, owner2);
            Assert.Equal(1, pool.IdleCount);
            Assert.Equal(2, pool.Count);

            // Make the idle-reuse activation fail.
            factory.FailOnActivate = true;

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
            var pool = ConstructPool(SuccessfulConnectionFactory);
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

        #endregion

        #region Test Helper Classes

        internal class SuccessfulSqlConnectionFactory : SqlConnectionFactory
        {
            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
            {
                return new StubDbConnectionInternal();
            }
        }

        internal class SwitchableSqlConnectionFactory : SqlConnectionFactory
        {
            internal bool ShouldFail { get; set; }

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
                    throw new InvalidOperationException("Simulated connection failure");
                }
                return new StubDbConnectionInternal();
            }
        }

        internal class ActivationFailSqlConnectionFactory : SqlConnectionFactory
        {
            internal bool FailOnActivate { get; set; }

            protected override DbConnectionInternal CreateConnection(
                SqlConnectionOptions options,
                ConnectionPoolKey poolKey,
                DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
                IDbConnectionPool pool,
                DbConnection owningConnection,
                TimeoutTimer timeout)
            {
                return new ActivationFailDbConnectionInternal(this);
            }
        }

        internal class StubDbConnectionInternal : DbConnectionInternal
        {
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
                return;
            }

            protected override void Deactivate()
            {
                return;
            }

            internal override void ResetConnection()
            {
                return;
            }
        }

        internal class ActivationFailDbConnectionInternal : DbConnectionInternal
        {
            private readonly ActivationFailSqlConnectionFactory _factory;

            internal ActivationFailDbConnectionInternal(ActivationFailSqlConnectionFactory factory)
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
                if (_factory.FailOnActivate)
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
        }

        #endregion
    }
}

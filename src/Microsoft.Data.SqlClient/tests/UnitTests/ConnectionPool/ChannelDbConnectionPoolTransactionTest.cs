// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool;

/// <summary>
/// Tests for <see cref="ChannelDbConnectionPool"/> transaction affinity: routing a returning
/// connection to the transacted store instead of the idle channel, vending an already-enlisted
/// connection back to the same transaction, releasing the connection when the transaction ends,
/// and flowing the ambient transaction on the asynchronous open path.
/// </summary>
public class ChannelDbConnectionPoolTransactionTest : IDisposable
{
    private const int DefaultMaxPoolSize = 50;
    private const int DefaultMinPoolSize = 0;
    private const int DefaultCreationTimeoutInMilliseconds = 15000;

    private IDbConnectionPool _pool;
    private MockSqlConnectionFactory _connectionFactory = null!;

    public ChannelDbConnectionPoolTransactionTest()
    {
        _pool = CreatePool();
    }

    public void Dispose()
    {
        // A transaction entry that outlives its transaction is a leak: the connections it holds
        // are never returned to general circulation.
        Assert.Empty(_pool.TransactedConnectionPool.TransactedConnections);

        _pool.Shutdown();
        _pool.Clear();
    }

    #region Helper Methods

    /// <summary>
    /// Builds a pool for these tests. A frozen <see cref="FakeTimeProvider"/> is injected so that
    /// time-driven background maintenance (idle-timeout pruning, warmup and replenishment,
    /// blocking-period expiry) cannot advance and race the assertions about pool contents.
    /// </summary>
    private ChannelDbConnectionPool CreatePool(
        int maxPoolSize = DefaultMaxPoolSize,
        int minPoolSize = DefaultMinPoolSize,
        bool hasTransactionAffinity = true)
    {
        var poolGroupOptions = new DbConnectionPoolGroupOptions(
            poolByIdentity: false,
            minPoolSize: minPoolSize,
            maxPoolSize: maxPoolSize,
            creationTimeout: DefaultCreationTimeoutInMilliseconds,
            loadBalanceTimeout: 0,
            hasTransactionAffinity: hasTransactionAffinity,
            idleTimeout: 0
        );

        var dbConnectionPoolGroup = new DbConnectionPoolGroup(
            new SqlConnectionOptions("Data Source=localhost;"),
            new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
            poolGroupOptions
        );

        _connectionFactory = new MockSqlConnectionFactory();

        var pool = new ChannelDbConnectionPool(
            _connectionFactory,
            dbConnectionPoolGroup,
            DbConnectionPoolIdentity.NoIdentity,
            new DbConnectionPoolProviderInfo(),
            timeProvider: new FakeTimeProvider()
        );

        pool.Startup();
        return pool;
    }

    /// <summary>
    /// Tears down the pool built by the constructor and replaces it with one configured
    /// differently, for the few tests that need non-default pool options.
    /// </summary>
    private void ReplaceFixturePool(bool hasTransactionAffinity)
    {
        _pool.Shutdown();
        _pool.Clear();
        _pool = CreatePool(hasTransactionAffinity: hasTransactionAffinity);
    }

    /// <summary>
    /// Replaces the fixture's pool with one capped at <paramref name="maxPoolSize"/>, so a test can
    /// exhaust it and observe what happens to a caller that has to wait.
    /// </summary>
    private void ReplaceFixturePool(int maxPoolSize)
    {
        _pool.Shutdown();
        _pool.Clear();
        _pool = CreatePool(maxPoolSize: maxPoolSize);
    }

    /// <summary>
    /// Opens a connection synchronously. The pool reads the ambient transaction off the calling
    /// thread on this path.
    /// </summary>
    private DbConnectionInternal GetConnection(SqlConnection owner)
    {
        _pool.TryGetConnection(
            owner,
            taskCompletionSource: null,
            TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
            out DbConnectionInternal? connection);
        return connection!;
    }

    /// <summary>
    /// Opens a connection asynchronously the way SqlConnection.InternalOpenAsync does: the ambient
    /// transaction is captured here, on the caller's thread, and handed to the pool in the
    /// <see cref="TaskCompletionSource{TResult}"/>'s AsyncState. The pool must take it from there,
    /// because the open itself runs on a thread pool thread the ambient transaction may not flow
    /// to, and on a retry the pool is re-entered from a continuation on an arbitrary thread.
    /// </summary>
    /// <param name="owner">The owning connection.</param>
    /// <param name="transaction">The transaction to hand to the pool. Defaults to the caller's
    /// ambient transaction, matching what InternalOpenAsync captures.</param>
    private async Task<DbConnectionInternal> GetConnectionAsync(
        SqlConnection owner,
        Transaction? transaction = null)
    {
        var tcs = new TaskCompletionSource<DbConnectionInternal>(transaction ?? Transaction.Current);
        _pool.TryGetConnection(
            owner,
            taskCompletionSource: tcs,
            TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
            out DbConnectionInternal? connection);
        return connection ?? await tcs.Task;
    }

    private void ReturnConnection(DbConnectionInternal connection, SqlConnection owner) =>
        _pool.ReturnInternalConnection(connection, owner);

    /// <summary>
    /// Asserts the pool's accounting after a step.
    /// </summary>
    /// <param name="count">Total connections owned by the pool, checked out or not. A connection
    /// parked in the transacted store still holds its pool slot and so is counted here.</param>
    /// <param name="idleCount">Connections sitting in the idle channel, available to any caller.</param>
    /// <param name="transactedCount">Connections parked in the transacted store across all
    /// transactions. These hold a pool slot but are not available to other callers.</param>
    private void AssertPoolState(int count, int idleCount, int transactedCount)
    {
        Assert.Equal(count, _pool.Count);
        Assert.Equal(idleCount, _pool.IdleCount);
        Assert.Equal(transactedCount, TotalTransactedConnections());
    }

    private int TotalTransactedConnections()
    {
        int total = 0;
        foreach (var entry in _pool.TransactedConnectionPool.TransactedConnections)
        {
            total += entry.Value.Count;
        }
        return total;
    }

    private int TransactedConnectionsFor(Transaction transaction) =>
        _pool.TransactedConnectionPool.TransactedConnections.TryGetValue(transaction, out var connections)
            ? connections.Count
            : 0;

    /// <summary>
    /// Asserts which transaction a connection is bound to, complementing the pool-state assertions:
    /// AssertPoolState says where the connection was filed, this says what it is actually enlisted
    /// in. Pass null to assert the connection is not enlisted at all.
    /// </summary>
    /// <remarks>
    /// Compared by equality rather than reference because the EnlistedTransaction setter stores a
    /// clone, so that the connection does not hold the caller's transaction past the end of its
    /// using block.
    /// </remarks>
    private static void AssertEnlistedIn(Transaction? expected, DbConnectionInternal connection) =>
        Assert.Equal(expected, connection.EnlistedTransaction);

    #endregion

    #region Connection Return Routing

    /// <summary>
    /// A connection returned while still enlisted must be parked in the transacted store rather
    /// than the idle channel, so it cannot be vended to a caller in a different transaction. It
    /// keeps its pool slot while parked.
    /// </summary>
    [Fact]
    public void ReturnConnection_WhileEnlisted_ParksInTransactedStoreNotIdleChannel()
    {
        // Arrange
        using var scope = new TransactionScope();
        Transaction? transaction = Transaction.Current;
        Assert.NotNull(transaction);

        var owner = new SqlConnection();
        var connection = GetConnection(owner);
        Assert.NotNull(connection);
        AssertEnlistedIn(transaction, connection);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        // Act
        ReturnConnection(connection, owner);

        // Assert
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);
        Assert.Equal(1, TransactedConnectionsFor(transaction!));

        // The connection stays bound to the transaction while parked, which is what makes it
        // ineligible for a caller in any other transaction.
        AssertEnlistedIn(transaction, connection);

        scope.Complete();
    }

    /// <summary>
    /// Without an ambient transaction there is nothing to enlist in, so a returning connection
    /// goes straight back into the idle channel.
    /// </summary>
    [Fact]
    public void ReturnConnection_WithoutTransaction_ReturnsToIdleChannel()
    {
        // Arrange
        var owner = new SqlConnection();
        var connection = GetConnection(owner);
        Assert.NotNull(connection);
        AssertEnlistedIn(null, connection);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        // Act
        ReturnConnection(connection, owner);

        // Assert
        AssertEnlistedIn(null, connection);
        AssertPoolState(count: 1, idleCount: 1, transactedCount: 0);
    }

    /// <summary>
    /// A connection whose transaction has already ended must land in the idle channel, not be
    /// parked under a dead transaction where nothing would ever release it. The enlistment is
    /// dropped when the transaction completes, so by the time the connection is returned there is
    /// nothing left to park it under.
    /// </summary>
    [Fact]
    public void ReturnConnection_AfterTransactionCompleted_ReturnsToIdleChannel()
    {
        // Arrange
        var owner = new SqlConnection();
        DbConnectionInternal connection;
        using (var scope = new TransactionScope())
        {
            connection = GetConnection(owner);
            Assert.NotNull(connection);
            AssertEnlistedIn(Transaction.Current, connection);
            scope.Complete();
        }

        // The completed transaction detached itself from the connection.
        AssertEnlistedIn(null, connection);

        // Act - the transaction is fully disposed by this point.
        ReturnConnection(connection, owner);

        // Assert
        AssertPoolState(count: 1, idleCount: 1, transactedCount: 0);
    }

    /// <summary>
    /// A pool with automatic enlistment disabled must never bind a connection to the ambient
    /// transaction, so the transacted store stays out of the picture entirely.
    /// </summary>
    [Fact]
    public void ReturnConnection_WithTransactionAffinityDisabled_ReturnsToIdleChannel()
    {
        // Arrange
        ReplaceFixturePool(hasTransactionAffinity: false);

        using var scope = new TransactionScope();
        var owner = new SqlConnection();
        var connection = GetConnection(owner);
        Assert.NotNull(connection);

        // The ambient transaction exists but must not reach the connection.
        Assert.NotNull(Transaction.Current);
        AssertEnlistedIn(null, connection);

        // Act
        ReturnConnection(connection, owner);

        // Assert
        AssertEnlistedIn(null, connection);
        AssertPoolState(count: 1, idleCount: 1, transactedCount: 0);

        scope.Complete();
    }

    /// <summary>
    /// Returning to a pool that has already shut down destroys the connection instead of pooling
    /// it, and must not throw.
    /// </summary>
    [Fact]
    public void ReturnConnection_ToShutDownPool_DestroysConnection()
    {
        // Arrange
        using var scope = new TransactionScope();
        var owner = new SqlConnection();
        var connection = GetConnection(owner);
        Assert.NotNull(connection);

        _pool.Shutdown();

        // Act
        ReturnConnection(connection, owner);

        // Assert - Dispose() dooms the connection and clears its pool back-reference.
        Assert.True(connection.IsConnectionDoomed,
            "A connection returned to a shut-down pool should be destroyed, not pooled.");
        Assert.Null(connection.Pool);
        AssertPoolState(count: 0, idleCount: 0, transactedCount: 0);
    }

    /// <summary>
    /// A doomed connection is destroyed even when it is still enlisted. The doomed check runs
    /// before the transacted decision precisely so that a connection unfit for reuse is never
    /// parked in the transacted store, where it would be handed to the next caller in that same
    /// transaction.
    /// </summary>
    [Fact]
    public void ReturnConnection_WhenDoomedWhileEnlisted_DestroysWithoutParking()
    {
        // Arrange
        using var scope = new TransactionScope();
        Transaction? transaction = Transaction.Current;
        Assert.NotNull(transaction);

        var owner = new SqlConnection();
        var connection = GetConnection(owner);
        Assert.NotNull(connection);
        AssertEnlistedIn(transaction, connection);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        // The connection breaks while it is enlisted and checked out.
        ((MockDbConnectionInternal)connection).Doom();
        Assert.True(connection.IsConnectionDoomed);

        // Act
        ReturnConnection(connection, owner);

        // Assert - destroyed rather than parked under its transaction, and its slot is released.
        Assert.Equal(0, TransactedConnectionsFor(transaction!));
        AssertPoolState(count: 0, idleCount: 0, transactedCount: 0);
        Assert.Null(connection.Pool);

        scope.Complete();
    }

    #endregion

    #region Vending From The Transacted Store

    /// <summary>
    /// Round trip: a second request inside the same transaction must be served the connection that
    /// is already enlisted in it, rather than a fresh connection. Reusing it is what keeps the
    /// transaction from being promoted to a distributed one.
    /// </summary>
    [Fact]
    public void GetConnection_UnderSameTransaction_VendsTheAlreadyEnlistedConnection()
    {
        // Arrange
        using var scope = new TransactionScope();
        Transaction? transaction = Transaction.Current;
        Assert.NotNull(transaction);

        var owner1 = new SqlConnection();
        var connection1 = GetConnection(owner1);
        Assert.NotNull(connection1);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        ReturnConnection(connection1, owner1);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

        // Act
        var owner2 = new SqlConnection();
        var connection2 = GetConnection(owner2);

        // Assert
        Assert.Same(connection1, connection2);
        AssertEnlistedIn(transaction, connection2);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        ReturnConnection(connection2, owner2);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);
        Assert.Equal(1, TransactedConnectionsFor(transaction!));

        scope.Complete();
    }

    /// <summary>
    /// The asynchronous path must reuse the enlisted connection exactly as the synchronous path
    /// does, even though it runs the open on a thread pool thread.
    /// </summary>
    [Fact]
    public async Task GetConnectionAsync_UnderSameTransaction_VendsTheAlreadyEnlistedConnection()
    {
        // Arrange
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        Transaction? transaction = Transaction.Current;
        Assert.NotNull(transaction);

        var owner1 = new SqlConnection();
        var connection1 = await GetConnectionAsync(owner1, transaction);
        Assert.NotNull(connection1);
        AssertEnlistedIn(transaction, connection1);
        ReturnConnection(connection1, owner1);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

        // Act
        var owner2 = new SqlConnection();
        var connection2 = await GetConnectionAsync(owner2, transaction);

        // Assert
        Assert.Same(connection1, connection2);
        AssertEnlistedIn(transaction, connection2);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        ReturnConnection(connection2, owner2);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

        scope.Complete();
    }

    /// <summary>
    /// A connection enlisted in one transaction must never be handed to a caller in a different
    /// transaction; each transaction gets its own entry in the transacted store.
    /// </summary>
    [Fact]
    public void GetConnection_UnderDifferentTransaction_DoesNotVendTheEnlistedConnection()
    {
        // Arrange
        using var outerScope = new TransactionScope();
        Transaction? outerTransaction = Transaction.Current;
        Assert.NotNull(outerTransaction);

        var owner1 = new SqlConnection();
        var connection1 = GetConnection(owner1);
        AssertEnlistedIn(outerTransaction, connection1);
        ReturnConnection(connection1, owner1);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

        // Act - RequiresNew starts an unrelated transaction.
        Transaction? innerTransaction;
        using (var innerScope = new TransactionScope(TransactionScopeOption.RequiresNew))
        {
            innerTransaction = Transaction.Current;
            Assert.NotEqual(outerTransaction, innerTransaction);

            var owner2 = new SqlConnection();
            var connection2 = GetConnection(owner2);

            // Assert - a fresh connection, because connection1 belongs to the outer transaction.
            Assert.NotSame(connection1, connection2);
            AssertEnlistedIn(innerTransaction, connection2);
            AssertEnlistedIn(outerTransaction, connection1);
            AssertPoolState(count: 2, idleCount: 0, transactedCount: 1);

            // The store holds only the outer transaction's connection, so connection2 was newly
            // created rather than taken from the store.
            Assert.Equal(1, TransactedConnectionsFor(outerTransaction!));
            Assert.Equal(0, TransactedConnectionsFor(innerTransaction!));

            ReturnConnection(connection2, owner2);
            AssertPoolState(count: 2, idleCount: 0, transactedCount: 2);
            Assert.Equal(1, TransactedConnectionsFor(outerTransaction!));
            Assert.Equal(1, TransactedConnectionsFor(innerTransaction!));

            innerScope.Complete();
        }

        // Completing the inner transaction releases only its connection; the outer transaction's
        // connection stays parked.
        AssertPoolState(count: 2, idleCount: 1, transactedCount: 1);
        Assert.Equal(1, TransactedConnectionsFor(outerTransaction!));
        Assert.Equal(0, TransactedConnectionsFor(innerTransaction!));

        outerScope.Complete();
    }

    #endregion

    #region Transaction Completion

    /// <summary>
    /// Committing releases the parked connection back into the idle channel, where it becomes
    /// available to any caller.
    /// </summary>
    [Fact]
    public void TransactionCommit_ReturnsParkedConnectionToIdleChannel()
    {
        // Arrange
        DbConnectionInternal connection;
        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            connection = GetConnection(owner);
            ReturnConnection(connection, owner);
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

            // Act
            scope.Complete();
        }

        // Assert
        AssertPoolState(count: 1, idleCount: 1, transactedCount: 0);

        // The transaction released it, so it carries no enlistment into general circulation.
        AssertEnlistedIn(null, connection);

        // The released connection is reusable by a caller with no ambient transaction.
        var owner2 = new SqlConnection();
        var connection2 = GetConnection(owner2);
        Assert.Same(connection, connection2);
        AssertEnlistedIn(null, connection2);
        ReturnConnection(connection2, owner2);
    }

    /// <summary>
    /// Rolling back must release the parked connection just as committing does; otherwise an
    /// aborted transaction would strand its connection in the transacted store forever.
    /// </summary>
    [Fact]
    public void TransactionRollback_ReturnsParkedConnectionToIdleChannel()
    {
        // Arrange
        DbConnectionInternal connection;
        using (new TransactionScope())
        {
            var owner = new SqlConnection();
            connection = GetConnection(owner);
            ReturnConnection(connection, owner);
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

            // Act - leaving the scope without calling Complete rolls the transaction back.
        }

        // Assert
        AssertPoolState(count: 1, idleCount: 1, transactedCount: 0);
        AssertEnlistedIn(null, connection);
    }

    /// <summary>
    /// A connection parked in the transacted store survives pool shutdown, because closing it
    /// would abort a possibly distributed transaction. Once that transaction ends, the shut-down
    /// pool must destroy the connection rather than return it to circulation.
    /// </summary>
    [Fact]
    public void TransactionCompletion_AfterShutdown_DestroysConnection()
    {
        // Arrange
        DbConnectionInternal connection;
        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            connection = GetConnection(owner);
            ReturnConnection(connection, owner);
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

            // Act - the shutdown drain must leave the transacted connection alone.
            _pool.Shutdown();
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

            scope.Complete();
        }

        // Assert
        AssertPoolState(count: 0, idleCount: 0, transactedCount: 0);
        Assert.True(connection.IsConnectionDoomed,
            "A shut-down pool must destroy a connection released by its transaction.");
    }

    /// <summary>
    /// TransactionEnded on its own, with none of the surrounding lifecycle events, must be a no-op
    /// for a connection that was never parked. Only parking hands the connection to the pool, so
    /// even an enlisted connection is still checked out here, and pushing it into the idle channel
    /// would let a second caller pick it up while the first is still using it.
    /// </summary>
    [Fact]
    public void TransactionEnded_ForEnlistedConnectionThatWasNeverParked_LeavesItCheckedOut()
    {
        // Arrange
        using var scope = new TransactionScope();
        Transaction? transaction = Transaction.Current;
        Assert.NotNull(transaction);

        var owner = new SqlConnection();
        var connection = GetConnection(owner);
        Assert.NotNull(connection);

        // The connection is enlisted, but checked out rather than parked.
        AssertEnlistedIn(transaction, connection);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        // Act - the completion notification arrives on its own, with no detach and no return.
        _pool.TransactionEnded(transaction!, connection);

        // Assert - nothing to release, and the enlistment is untouched.
        AssertEnlistedIn(transaction, connection);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        // It still reaches the pool through the normal return path.
        ReturnConnection(connection, owner);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

        scope.Complete();
    }

    /// <summary>
    /// A transaction can complete while the application still holds its connection. The connection
    /// was never parked, so there is nothing for the completion to release, and pushing it into the
    /// idle channel would hand a connection that is still in use to a second caller.
    /// </summary>
    [Fact]
    public void TransactionCompletes_WhileConnectionStillCheckedOut_LeavesItCheckedOut()
    {
        // Arrange
        var owner = new SqlConnection();
        DbConnectionInternal connection;
        using (var scope = new TransactionScope())
        {
            connection = GetConnection(owner);
            Assert.NotNull(connection);
            AssertEnlistedIn(Transaction.Current, connection);
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

            // Act - the transaction completes with the connection still checked out.
            scope.Complete();
        }

        // Assert
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        // It reaches the pool only when its owner returns it.
        ReturnConnection(connection, owner);
        AssertPoolState(count: 1, idleCount: 1, transactedCount: 0);
    }

    /// <summary>
    /// Releasing a parked connection runs on the System.Transactions transaction-completion
    /// callback thread, under DbConnectionInternal.DelegatedTransactionEnded's requirement that the
    /// caller holds a lock on the connection. The pool must not poll the socket there, so the
    /// liveness probe is suppressed on that path and the connection is pooled without it.
    /// WaitHandleDbConnectionPool likewise does not probe when returning a connection from the
    /// transacted store. A connection that died during the transaction is still caught, just later:
    /// it is validated on its way back out of the idle channel, before it can be vended.
    /// </summary>
    [Fact]
    public void TransactionCommit_ReturningParkedConnection_DoesNotProbeLiveness()
    {
        // Arrange
        MockDbConnectionInternal connection;
        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            connection = Assert.IsType<MockDbConnectionInternal>(GetConnection(owner));
            ReturnConnection(connection, owner);
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

            // Report the connection as dead and ignore any probes made while arranging, so the
            // assertions below describe only what the completion path itself did.
            connection.IsDead = true;
            connection.LivenessProbeCount = 0;

            // Act
            scope.Complete();
        }

        // Assert - the completion path pooled it without asking whether it was alive.
        Assert.Equal(0, connection.LivenessProbeCount);
        AssertPoolState(count: 1, idleCount: 1, transactedCount: 0);

        // The deferred check still catches it: vending probes the connection, finds it dead, and
        // discards it rather than handing a broken connection to the caller.
        var owner2 = new SqlConnection();
        var connection2 = GetConnection(owner2);
        Assert.NotSame(connection, connection2);
        Assert.True(connection.LivenessProbeCount > 0);
        ReturnConnection(connection2, owner2);
    }

    /// <summary>
    /// The counterpart to <see cref="TransactionCommit_ReturningParkedConnection_DoesNotProbeLiveness"/>:
    /// suppressing the probe is specific to the transaction-completion path. An ordinary return
    /// runs on the caller's own thread, where blocking is acceptable, so it still probes and drops
    /// a dead connection immediately instead of leaving it to be discovered later.
    /// </summary>
    [Fact]
    public void ReturnConnection_WhenDeadAndNotEnlisted_ProbesLivenessAndDestroysIt()
    {
        // Arrange
        var owner = new SqlConnection();
        var connection = Assert.IsType<MockDbConnectionInternal>(GetConnection(owner));
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        connection.IsDead = true;
        connection.LivenessProbeCount = 0;

        // Act
        ReturnConnection(connection, owner);

        // Assert
        Assert.True(connection.LivenessProbeCount > 0);
        AssertPoolState(count: 0, idleCount: 0, transactedCount: 0);
    }

    /// <summary>
    /// A connection parked in the transacted store still holds its pool slot, so with MaxPoolSize
    /// reached and everything parked, a caller outside the transaction cannot be served and must
    /// wait. Ending the transaction has to wake it: the release path writes the connection into the
    /// idle channel, which completes the waiter. If it did not, the caller would block until its
    /// own timeout even though a connection had become available.
    /// </summary>
    [Fact]
    public async Task TransactionEnd_WithPoolExhaustedByParkedConnection_WakesWaitingCaller()
    {
        // Arrange - a pool of exactly one connection, parked under a transaction.
        ReplaceFixturePool(maxPoolSize: 1);

        DbConnectionInternal parkedConnection;
        var waiter = new TaskCompletionSource<DbConnectionInternal>(state: null);

        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            parkedConnection = GetConnection(owner);
            ReturnConnection(parkedConnection, owner);
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

            // A second caller with no ambient transaction. The pool is at MaxPoolSize and the idle
            // channel is empty, and the parked connection belongs to a transaction this caller is
            // not in, so it cannot be served now. Suppress the ambient transaction so the request
            // is genuinely unrelated rather than being handed the parked connection by affinity.
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                bool completedSynchronously = _pool.TryGetConnection(
                    new SqlConnection(),
                    waiter,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
                    out DbConnectionInternal? immediateConnection);

                Assert.False(completedSynchronously);
                Assert.Null(immediateConnection);
            }

            Assert.False(waiter.Task.IsCompleted, "The caller must wait while the only connection is parked.");

            // Act
            scope.Complete();
        }

        // Assert - the waiter was handed the released connection rather than timing out.
        DbConnectionInternal servedConnection = await waiter.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Same(parkedConnection, servedConnection);
        AssertEnlistedIn(null, servedConnection);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);
    }

    /// <summary>
    /// Clear cannot destroy a connection parked in the transacted store, because doing so would
    /// abort a possibly distributed transaction, and the parked connection is not in the idle
    /// channel that Clear drains. It is instead retired lazily: Clear bumps the generation counter,
    /// and when the transaction ends the release path sees the stale generation and destroys the
    /// connection rather than returning it to circulation. This is the channel pool's substitute
    /// for WaitHandleDbConnectionPool's eager DoNotPoolThisConnection sweep, so the lag between
    /// Clear returning and the connection actually going away is expected.
    /// </summary>
    [Fact]
    public void Clear_WhileConnectionIsParked_RetiresItOnTransactionEndInsteadOfRepooling()
    {
        // Arrange
        DbConnectionInternal connection;
        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            connection = GetConnection(owner);
            ReturnConnection(connection, owner);
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

            // Act - Clear runs while the connection is parked and its transaction is live.
            _pool.Clear();

            // Assert - it survives Clear, still parked and still holding its slot.
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);
            Assert.False(connection.IsConnectionDoomed);

            scope.Complete();
        }

        // Assert - on release the stale generation is detected, so the connection is destroyed
        // instead of being handed to the next caller.
        Assert.True(connection.IsConnectionDoomed);
        Assert.Null(connection.Pool);
        AssertPoolState(count: 0, idleCount: 0, transactedCount: 0);

        // The next caller therefore gets a freshly created connection, not the cleared one.
        var owner2 = new SqlConnection();
        var connection2 = GetConnection(owner2);
        Assert.NotSame(connection, connection2);
        ReturnConnection(connection2, owner2);
    }

    #endregion

    #region Stasis
    /// <summary>
    /// A transaction root that cannot be pooled is put in stasis rather than destroyed: closing it
    /// would orphan the root transaction with no way to commit or roll back. Stasis is not the
    /// transacted store. The connection is filed nowhere, so it is invisible to the idle channel,
    /// to <see cref="ChannelDbConnectionPool.Clear"/> and to pruning, but it still holds its pool
    /// slot. The slot comes back when the transaction ends and the connection unwinds through
    /// PutObjectFromTransactedPool.
    /// </summary>
    [Fact]
    public void ReturnConnection_NonPoolableTransactionRoot_EntersStasisThenReleasesSlotOnTransactionEnd()
    {
        // Arrange
        MockDbConnectionInternal connection;
        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            connection = Assert.IsType<MockDbConnectionInternal>(GetConnection(owner));

            // A root that cannot be pooled. Deliberately not doomed: IsConnectionDoomed would
            // short-circuit the return path before the stasis decision is ever reached.
            connection.IsRoot = true;
            connection.MarkDoNotPool();
            Assert.False(connection.CanBePooled);
            Assert.False(connection.IsConnectionDoomed);

            // Act
            ReturnConnection(connection, owner);

            // Assert - in stasis: holding its slot, but filed in neither the idle channel nor the
            // transacted store, so no other caller can reach it.
            Assert.True(connection.IsTxRootWaitingForTxEnd);
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

            // It survives a Clear that would have destroyed any pooled connection, because Clear
            // drains the idle channel and a connection in stasis is not in it.
            _pool.Clear();
            Assert.True(connection.IsTxRootWaitingForTxEnd);
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

            scope.Complete();
        }

        // Assert - the transaction ended, stasis terminated, and because the connection still
        // cannot be pooled it was destroyed rather than returned to circulation. Its slot is free.
        Assert.False(connection.IsTxRootWaitingForTxEnd);
        Assert.Null(connection.Pool);
        AssertPoolState(count: 0, idleCount: 0, transactedCount: 0);
    }

    /// <summary>
    /// The transaction-root counterpart to
    /// <see cref="ReturnConnection_ToShutDownPool_DestroysConnection"/>. A shut-down pool destroys
    /// an ordinary connection on return, but a transaction root must still go to stasis instead, or
    /// shutting down a pool would abort a live transaction. The slot stays outstanding until the
    /// transaction ends, which is why a completed Shutdown does not imply the pool owns nothing.
    /// </summary>
    [Fact]
    public void ReturnConnection_TransactionRootToShutDownPool_EntersStasisRatherThanBeingDestroyed()
    {
        // Arrange
        MockDbConnectionInternal connection;
        using (var scope = new TransactionScope())
        {
            var owner = new SqlConnection();
            connection = Assert.IsType<MockDbConnectionInternal>(GetConnection(owner));
            connection.IsRoot = true;

            _pool.Shutdown();

            // Act
            ReturnConnection(connection, owner);

            // Assert - not destroyed, unlike the non-root case.
            Assert.True(connection.IsTxRootWaitingForTxEnd);
            Assert.False(connection.IsConnectionDoomed);
            Assert.NotNull(connection.Pool);
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

            scope.Complete();
        }

        // Assert - the pool is not Running, so the connection is destroyed on the way out of
        // stasis rather than pooled, and only now does the pool's count reach zero.
        Assert.False(connection.IsTxRootWaitingForTxEnd);
        Assert.True(connection.IsConnectionDoomed);
        Assert.Null(connection.Pool);
        AssertPoolState(count: 0, idleCount: 0, transactedCount: 0);
    }

    #endregion

    #region Connection Replacement

    /// <summary>
    /// Replacing a connection mid-transaction must carry the enlistment across, so the replacement
    /// is the one that parks in the transacted store when it is returned.
    /// </summary>
    [Fact]
    public void ReplaceConnection_CarriesEnlistedTransactionToNewConnection()
    {
        // Arrange
        using var scope = new TransactionScope();
        Transaction? transaction = Transaction.Current;
        Assert.NotNull(transaction);

        var owner = new SqlConnection();
        var oldConnection = GetConnection(owner);
        Assert.NotNull(oldConnection);
        AssertEnlistedIn(transaction, oldConnection);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        // Act
        var newConnection = _pool.ReplaceConnection(
            owner,
            oldConnection,
            TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

        // Assert - a distinct connection took over the old connection's slot, carrying the
        // enlistment with it.
        Assert.NotNull(newConnection);
        Assert.NotSame(oldConnection, newConnection);
        AssertEnlistedIn(transaction, newConnection);
        Assert.True(oldConnection.IsConnectionDoomed);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        ReturnConnection(newConnection, owner);

        // The replacement inherited the transaction, so it parks in the transacted store.
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);
        Assert.Equal(1, TransactedConnectionsFor(transaction!));

        scope.Complete();
    }

    #endregion

    #region Async Ambient Transaction Flow

    /// <summary>
    /// A <see cref="TransactionScope"/> created without
    /// <see cref="TransactionScopeAsyncFlowOption.Enabled"/> -- the default -- keeps its ambient
    /// transaction in thread-static storage, so it does not flow onto the thread pool thread the
    /// pool opens on. The connection must still enlist, because the transaction is captured on the
    /// caller's thread before the open is scheduled (see SqlConnection.InternalOpenAsync).
    ///
    /// This is one of three guards on that capture. With Enabled the transaction rides an
    /// AsyncLocal and is live on the pool's worker thread, so reading Transaction.Current there
    /// would pass every other test in this class -- and silently stop enlisting for the default
    /// option, which the WaitHandle pool handles correctly. This case is the realistic shape of
    /// that regression; see also
    /// <see cref="GetConnectionAsync_EnteredFromThreadWithoutAmbientTransaction_EnlistsFromAsyncState"/>
    /// and <see cref="GetConnectionAsync_DoesNotSetAmbientTransactionOnPoolWorkerThread"/>.
    ///
    /// The open is started inside the scope but awaited outside it. That is not incidental:
    /// awaiting inside resumes the continuation on a thread pool thread, and disposing the scope
    /// there throws "A TransactionScope must be disposed on the same thread that it was created."
    /// That is the very limitation TransactionScopeAsyncFlowOption.Enabled exists to remove, so a
    /// test of the suppressed case cannot avoid it. The scope is given an explicit
    /// <see cref="CommittableTransaction"/> so that leaving the scope ends the scope without
    /// ending the transaction the pool is still enlisting in.
    /// </summary>
    [Fact]
    public async Task GetConnectionAsync_WithAsyncFlowDisabled_StillEnlistsInAmbientTransaction()
    {
        // Arrange
        using var transaction = new CommittableTransaction();
        var owner = new SqlConnection();
        Task<DbConnectionInternal> openTask;

        using (var scope = new TransactionScope(transaction))
        {
            Assert.Equal(transaction, Transaction.Current);

            // Act - starting the open captures the ambient transaction synchronously, here, and
            // hands the rest of the work to a thread pool thread.
            openTask = GetConnectionAsync(owner);
            scope.Complete();
        }

        var connection = await openTask;

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(transaction, connection.EnlistedTransaction);

        ReturnConnection(connection, owner);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);
        Assert.Equal(1, TransactedConnectionsFor(transaction));

        transaction.Rollback();
    }

    /// <summary>
    /// The pool must take the transaction from AsyncState rather than from whatever is ambient on
    /// the thread that happens to enter it. TryGetConnection is normally called synchronously from
    /// the caller's thread, where the two agree -- but on the retry path
    /// (SqlConnection.OpenAsyncRetry.Retry) the pool is re-entered from a continuation running on
    /// an arbitrary thread, which has no ambient transaction. Reading Transaction.Current there
    /// would silently drop the enlistment, or worse, pick up an unrelated transaction.
    ///
    /// No TransactionScope is opened here, so this thread already stands in for that continuation:
    /// the transaction exists but is not ambient anywhere, and AsyncState is the only channel
    /// carrying it.
    /// </summary>
    [Fact]
    public async Task GetConnectionAsync_EnteredFromThreadWithoutAmbientTransaction_EnlistsFromAsyncState()
    {
        // Arrange
        using var transaction = new CommittableTransaction();
        var owner = new SqlConnection();
        Assert.Null(Transaction.Current);

        // Act - carry the transaction only in AsyncState, the way a retry continuation does.
        var connection = await GetConnectionAsync(owner, transaction);

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(transaction, connection.EnlistedTransaction);

        ReturnConnection(connection, owner);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);
        Assert.Equal(1, TransactedConnectionsFor(transaction));

        transaction.Rollback();
    }

    /// <summary>
    /// The pool must enlist the connection in the caller's transaction without making that
    /// transaction ambient on the thread it opens on. Assigning Transaction.Current writes to
    /// thread-static storage that ExecutionContext does not unwind, so doing it on a thread pool
    /// thread would leave a stale transaction behind for unrelated work later scheduled onto that
    /// same thread -- including the login-time auto-enlistment that non-pooled connections perform
    /// against the ambient transaction. The pool must pass the transaction explicitly instead of
    /// assigning it.
    ///
    /// The connection factory runs inside the pool's own open work, so it observes what the pool
    /// made ambient there. That is an observation of the code under test rather than of whichever
    /// thread happened to run it, so it does not depend on thread identity or scheduling.
    /// </summary>
    [Fact]
    public async Task GetConnectionAsync_DoesNotSetAmbientTransactionOnPoolWorkerThread()
    {
        // Arrange - a transaction the pool must enlist in but must not make ambient.
        using var transaction = new CommittableTransaction();
        Assert.Null(Transaction.Current);

        // Act
        var owner = new SqlConnection();
        var connection = await GetConnectionAsync(owner, transaction);

        // Assert - the pool left the worker's ambient transaction alone while still enlisting the
        // connection.
        Assert.Equal(1, _connectionFactory.CreateCount);
        Assert.Null(_connectionFactory.AmbientTransactionAtCreate);
        Assert.Equal(transaction, connection.EnlistedTransaction);

        ReturnConnection(connection, owner);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

        transaction.Rollback();
    }

    /// <summary>
    /// The synchronous path runs on the caller's thread, where the ambient transaction set by a
    /// TransactionScope is directly observable and must still be honored even though no
    /// transaction is handed to the pool explicitly.
    /// </summary>
    [Fact]
    public void GetConnection_Sync_UsesAmbientTransactionFromCallersThread()
    {
        // Arrange
        using var scope = new TransactionScope();
        Transaction? transaction = Transaction.Current;
        Assert.NotNull(transaction);

        // Act
        var owner = new SqlConnection();
        var connection = GetConnection(owner);

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(transaction, connection.EnlistedTransaction);

        ReturnConnection(connection, owner);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

        scope.Complete();
    }

    /// <summary>
    /// The asynchronous equivalent of the case above. The pool cannot read the caller's ambient
    /// transaction itself, because the open runs on a thread pool thread it does not flow to, so
    /// the caller captures it and passes it in AsyncState. Even with a scope created with
    /// <see cref="TransactionScopeAsyncFlowOption.Enabled"/>, where the transaction is genuinely
    /// ambient on the calling thread, that capture is what has to carry it through.
    /// </summary>
    [Fact]
    public async Task GetConnectionAsync_UsesAmbientTransactionCapturedOnCallersThread()
    {
        // Arrange
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        Transaction? transaction = Transaction.Current;
        Assert.NotNull(transaction);

        // Act - no transaction is passed explicitly, so the helper captures the ambient one on
        // this thread exactly as SqlConnection.InternalOpenAsync does.
        var owner = new SqlConnection();
        var connection = await GetConnectionAsync(owner);

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(transaction, connection.EnlistedTransaction);

        ReturnConnection(connection, owner);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);
        Assert.Equal(1, TransactedConnectionsFor(transaction!));

        scope.Complete();
    }

    #endregion

    #region Mock Classes

    internal class MockSqlConnectionFactory : SqlConnectionFactory
    {
        /// <summary>
        /// The value of <see cref="Transaction.Current"/> observed on the thread the pool used to
        /// create the connection. On the asynchronous path that is the thread pool thread the pool
        /// runs its open work on, so this is a direct observation of whether the pool assigned the
        /// ambient transaction there.
        /// </summary>
        public Transaction? AmbientTransactionAtCreate { get; private set; }

        public int CreateCount { get; private set; }

        protected override DbConnectionInternal CreateConnection(
            SqlConnectionOptions options,
            ConnectionPoolKey poolKey,
            DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
            IDbConnectionPool pool,
            DbConnection owningConnection,
            TimeoutTimer timeout)
        {
            AmbientTransactionAtCreate = Transaction.Current;
            CreateCount++;
            return new MockDbConnectionInternal();
        }
    }

    internal class MockDbConnectionInternal : DbConnectionInternal
    {
        private static int s_nextId = 1;
        public int MockId { get; } = Interlocked.Increment(ref s_nextId);

        public override string ServerVersion => "Mock";

        public override ConnectionCapabilities Capabilities => new();

        public override DbTransaction BeginTransaction(System.Data.IsolationLevel il)
        {
            throw new NotImplementedException();
        }

        public override void EnlistTransaction(Transaction? transaction)
        {
            if (transaction != null)
            {
                EnlistedTransaction = transaction;
            }
        }

        protected override void Activate(Transaction? transaction)
        {
            EnlistedTransaction = transaction;
        }

        protected override void Deactivate()
        {
        }

        internal void Doom() => DoomThisConnection();

        /// <summary>
        /// Marks the connection unfit for pooling without dooming it. This is the distinction the
        /// pool cares about: <see cref="DbConnectionInternal.IsConnectionDoomed"/> short-circuits
        /// the return path before the transacted decision, whereas a merely non-poolable connection
        /// still reaches it and can be put in stasis.
        /// </summary>
        internal void MarkDoNotPool() => DoNotPoolThisConnection();

        /// <summary>
        /// When true, the connection reports itself as the root of a delegated transaction, which
        /// is what makes the pool put it in stasis rather than destroy it.
        /// </summary>
        internal bool IsRoot { get; set; }

        internal override bool IsTransactionRoot => IsRoot;

        /// <summary>
        /// When true, the connection reports itself as dead from
        /// <see cref="IsConnectionAlive"/>, simulating a physical connection that died while the
        /// pool was not looking.
        /// </summary>
        internal bool IsDead { get; set; }

        /// <summary>
        /// Counts calls to <see cref="IsConnectionAlive"/>. Settable so a test can zero it after
        /// arranging and observe only the probes made by the operation under test.
        /// </summary>
        internal int LivenessProbeCount { get; set; }

        internal override bool IsConnectionAlive(bool throwOnException = false)
        {
            LivenessProbeCount++;
            return !IsDead;
        }

        public override string ToString() => $"MockConnection_{MockId}";

        internal override void ResetConnection()
        {
        }
    }

    #endregion
}

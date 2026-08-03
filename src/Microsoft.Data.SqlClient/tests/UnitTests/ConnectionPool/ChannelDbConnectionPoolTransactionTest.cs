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
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        // Act
        ReturnConnection(connection, owner);

        // Assert
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);
        Assert.Equal(1, TransactedConnectionsFor(transaction!));

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
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        // Act
        ReturnConnection(connection, owner);

        // Assert
        AssertPoolState(count: 1, idleCount: 1, transactedCount: 0);
    }

    /// <summary>
    /// The pool deactivates a returning connection before reading its enlistment, and deactivation
    /// is what detaches a completed transaction. A connection returned after its transaction has
    /// already ended must therefore land in the idle channel, not be parked under a dead
    /// transaction where nothing would ever release it.
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
            scope.Complete();
        }

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

        // Act
        ReturnConnection(connection, owner);

        // Assert
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
        ReturnConnection(connection1, owner1);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

        // Act
        var owner2 = new SqlConnection();
        var connection2 = await GetConnectionAsync(owner2, transaction);

        // Assert
        Assert.Same(connection1, connection2);
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
        ReturnConnection(connection1, owner1);
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

        // Act - RequiresNew starts an unrelated transaction.
        using (var innerScope = new TransactionScope(TransactionScopeOption.RequiresNew))
        {
            Transaction? innerTransaction = Transaction.Current;
            Assert.NotEqual(outerTransaction, innerTransaction);

            var owner2 = new SqlConnection();
            var connection2 = GetConnection(owner2);

            // Assert - a fresh connection, because connection1 belongs to the outer transaction.
            Assert.NotSame(connection1, connection2);
            AssertPoolState(count: 2, idleCount: 0, transactedCount: 1);

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

        // The released connection is reusable by a caller with no ambient transaction.
        var owner2 = new SqlConnection();
        var connection2 = GetConnection(owner2);
        Assert.Same(connection, connection2);
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
        using (new TransactionScope())
        {
            var owner = new SqlConnection();
            var connection = GetConnection(owner);
            ReturnConnection(connection, owner);
            AssertPoolState(count: 1, idleCount: 0, transactedCount: 1);

            // Act - leaving the scope without calling Complete rolls the transaction back.
        }

        // Assert
        AssertPoolState(count: 1, idleCount: 1, transactedCount: 0);
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
    /// TransactionEnded for a connection that was never parked must be a no-op. It must not push a
    /// connection that is still checked out into the idle channel, where a second caller could
    /// pick it up while the first is still using it.
    /// </summary>
    [Fact]
    public void TransactionEnded_ForConnectionThatWasNeverParked_LeavesItCheckedOut()
    {
        // Arrange
        var owner = new SqlConnection();
        var connection = GetConnection(owner);
        Assert.NotNull(connection);

        using var scope = new TransactionScope();
        Transaction? transaction = Transaction.Current;
        Assert.NotNull(transaction);

        // Act
        _pool.TransactionEnded(transaction!, connection);

        // Assert
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        ReturnConnection(connection, owner);
        scope.Complete();
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
        AssertPoolState(count: 1, idleCount: 0, transactedCount: 0);

        // Act
        var newConnection = _pool.ReplaceConnection(
            owner,
            oldConnection,
            TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

        // Assert - a distinct connection took over the old connection's slot.
        Assert.NotNull(newConnection);
        Assert.NotSame(oldConnection, newConnection);
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
    /// A <see cref="TransactionScope"/> created without <see cref="TransactionScopeAsyncFlowOption.Enabled"/>
    /// keeps the ambient transaction in thread-static storage, so it is not observable from the
    /// thread pool thread the pool opens on. The pool must therefore take the transaction from the
    /// <see cref="TaskCompletionSource{TResult}"/>'s AsyncState. This test uses a transaction that
    /// is never ambient anywhere, so AsyncState is the only way the pool can learn about it.
    /// </summary>
    [Fact]
    public async Task GetConnectionAsync_AmbientTransactionNotFlowed_StillEnlistsFromAsyncState()
    {
        // Arrange
        using var transaction = new CommittableTransaction();
        Assert.Null(Transaction.Current);
        Assert.Null(await Task.Run(() => Transaction.Current));

        // Act
        var owner = new SqlConnection();
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
    /// Assigning <see cref="Transaction.Current"/> writes to thread-static storage that the
    /// ExecutionContext does not unwind, so doing it on a thread pool thread would leave a stale
    /// transaction behind for unrelated work later scheduled onto that same thread -- including
    /// the login-time auto-enlistment that non-pooled connections perform against the ambient
    /// transaction. The pool must pass the transaction explicitly instead of assigning it.
    ///
    /// The connection factory runs on exactly the thread the pool does its open work on, so it is
    /// used here to observe that thread's ambient transaction directly rather than inferring the
    /// leak from thread pool reuse.
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

        // Assert - the open really did happen off the calling thread, and the pool left that
        // thread's ambient transaction alone while still enlisting the connection.
        Assert.Equal(1, _connectionFactory.CreateCount);
        Assert.NotEqual(Environment.CurrentManagedThreadId, _connectionFactory.CreateThreadId);
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

        /// <summary>
        /// The managed thread the pool created the connection on.
        /// </summary>
        public int CreateThreadId { get; private set; }

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
            CreateThreadId = Environment.CurrentManagedThreadId;
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

        public override string ToString() => $"MockConnection_{MockId}";

        internal override void ResetConnection()
        {
        }
    }

    #endregion
}

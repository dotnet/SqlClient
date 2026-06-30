// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading;
using System.Transactions;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Extensions.Time.Testing;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool;

/// <summary>
/// End-to-end coverage of the <see cref="WaitHandleDbConnectionPool"/> blocking-period
/// behavior, exercising the full connection-acquisition path rather than the
/// <see cref="BlockingPeriodErrorState"/> state machine in isolation. Verifies the wiring
/// between a failed physical connection create and the pool's error state:
/// - A failed create enters the blocking period and surfaces the error (<c>CreateObject</c> → <c>Enter</c>).
/// - Subsequent requests fast-fail with the cached (cloned) exception without re-invoking the
///   connection factory (error wait-handle → <c>ThrowIfActive</c>).
/// - A successful create does not trip the blocking period.
/// - <see cref="PoolBlockingPeriod.NeverBlock"/> bypasses the blocking period entirely so each
///   request retries the factory.
/// - After the blocking-period exit timer fires, the next request retries the factory and a
///   successful create recovers the pool and resets the backoff (driven deterministically by an
///   injected <see cref="FakeTimeProvider"/>).
/// </summary>
public class WaitHandleDbConnectionPoolBlockingPeriodTest : IDisposable
{
    private const int DefaultMaxPoolSize = 50;
    private const int DefaultMinPoolSize = 0;
    private const int DefaultCreationTimeoutInMilliseconds = 15_000;

    private WaitHandleDbConnectionPool? _pool;

    public void Dispose()
    {
        _pool?.Shutdown();
        _pool?.Clear();
    }

    /// <summary>
    /// Builds a running <see cref="WaitHandleDbConnectionPool"/> backed by the supplied factory.
    /// The <paramref name="connectionString"/> controls the data source and
    /// <see cref="PoolBlockingPeriod"/> policy used to resolve whether blocking is enabled.
    /// When <paramref name="timeProvider"/> is supplied, the pool's
    /// <see cref="BlockingPeriodErrorState"/> uses it as its clock so the exit timer can be driven
    /// deterministically; otherwise the system clock is used.
    /// </summary>
    private WaitHandleDbConnectionPool CreatePool(
        SqlConnectionFactory connectionFactory,
        string connectionString = "Data Source=localhost;",
        TimeProvider? timeProvider = null)
    {
        var poolGroupOptions = new DbConnectionPoolGroupOptions(
            poolByIdentity: false,
            minPoolSize: DefaultMinPoolSize,
            maxPoolSize: DefaultMaxPoolSize,
            creationTimeout: DefaultCreationTimeoutInMilliseconds,
            loadBalanceTimeout: 0,
            hasTransactionAffinity: true,
            idleTimeout: 0);

        var dbConnectionPoolGroup = new DbConnectionPoolGroup(
            new SqlConnectionOptions(connectionString),
            new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
            poolGroupOptions);

        var pool = new WaitHandleDbConnectionPool(
            connectionFactory,
            dbConnectionPoolGroup,
            DbConnectionPoolIdentity.NoIdentity,
            new DbConnectionPoolProviderInfo(),
            timeProvider);

        pool.Startup();
        _pool = pool;
        return pool;
    }

    /// <summary>
    /// Synchronously requests a connection from the pool, mirroring the sync acquisition path
    /// (<c>taskCompletionSource == null</c>) used by callers of <c>SqlConnection.Open</c>.
    /// </summary>
    private static bool TryGetConnectionSync(
        WaitHandleDbConnectionPool pool,
        DbConnection owner,
        out DbConnectionInternal? connection)
    {
        TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(15));
        return pool.TryGetConnection(owner, taskCompletionSource: null, timer, out connection);
    }

    /// <summary>
    /// Verifies that when the connection factory fails, the pool enters the blocking period:
    /// the originating exception is surfaced to the caller and <see cref="WaitHandleDbConnectionPool.ErrorOccurred"/>
    /// becomes true. Guards the <c>CreateObject</c> → <see cref="BlockingPeriodErrorState.Enter"/> wiring.
    /// </summary>
    [Fact]
    public void TryGetConnection_WhenFactoryThrows_EntersBlockingPeriod()
    {
        // Arrange
        SqlException failure = SqlExceptionHelper.CreateSqlException("server unreachable");
        var factory = new ConfigurableSqlConnectionFactory(_ => throw failure);
        var pool = CreatePool(factory);
        using var owner = new SqlConnection();

        // Act
        SqlException thrown = Assert.Throws<SqlException>(
            () => TryGetConnectionSync(pool, owner, out _));

        // Assert
        Assert.Equal(failure.Message, thrown.Message);
        Assert.True(pool.ErrorOccurred);
        Assert.Equal(1, factory.CreateConnectionCallCount);
    }

    /// <summary>
    /// Verifies that once the pool is in the blocking period, a subsequent request fast-fails
    /// with the cached exception without invoking the connection factory again. The first throw
    /// rethrows the original instance; the fast-fail throw returns a clone (to avoid sharing stack
    /// traces). Guards the error wait-handle → <see cref="BlockingPeriodErrorState.ThrowIfActive"/> path.
    /// </summary>
    [Fact]
    public void TryGetConnection_WhileBlocked_FastFailsWithCachedExceptionWithoutInvokingFactory()
    {
        // Arrange
        SqlException failure = SqlExceptionHelper.CreateSqlException("server unreachable");
        var factory = new ConfigurableSqlConnectionFactory(_ => throw failure);
        var pool = CreatePool(factory);
        using var owner = new SqlConnection();

        // Act: first request enters the blocking period and rethrows the original exception.
        SqlException first = Assert.Throws<SqlException>(
            () => TryGetConnectionSync(pool, owner, out _));

        // Second request must fast-fail from the cached state without reaching the factory.
        SqlException second = Assert.Throws<SqlException>(
            () => TryGetConnectionSync(pool, owner, out _));

        // Assert
        Assert.Same(failure, first);          // original instance rethrown on entry
        Assert.NotSame(failure, second);      // cloned on fast-fail
        Assert.Equal(failure.Message, second.Message); // but message is preserved
        Assert.Equal(1, factory.CreateConnectionCallCount); // factory not re-invoked while blocked
        Assert.True(pool.ErrorOccurred);
    }

    /// <summary>
    /// Verifies that with <see cref="PoolBlockingPeriod.NeverBlock"/>, a failed create does not
    /// cache the error: <see cref="WaitHandleDbConnectionPool.ErrorOccurred"/> stays false and each
    /// request retries the connection factory. Guards the
    /// <c>IsBlockingPeriodEnabled() == false</c> bypass in <c>CreateObject</c>.
    /// </summary>
    [Fact]
    public void TryGetConnection_WithNeverBlockPolicy_DoesNotCacheErrorAndRetriesFactory()
    {
        // Arrange
        SqlException failure = SqlExceptionHelper.CreateSqlException("server unreachable");
        var factory = new ConfigurableSqlConnectionFactory(_ => throw failure);
        var pool = CreatePool(factory, "Data Source=localhost;PoolBlockingPeriod=NeverBlock");
        using var owner = new SqlConnection();

        // Act: two independent failures, neither of which should be cached.
        SqlException first = Assert.Throws<SqlException>(() => TryGetConnectionSync(pool, owner, out _));
        SqlException second = Assert.Throws<SqlException>(() => TryGetConnectionSync(pool, owner, out _));

        // Assert
        Assert.Same(failure, first);   // original instance rethrown, never cached/cloned
        Assert.Same(failure, second);  // original instance rethrown again on retry
        Assert.False(pool.ErrorOccurred);
        Assert.Equal(2, factory.CreateConnectionCallCount);
    }

    /// <summary>
    /// Verifies that a successful create returns a connection and does not trip the blocking
    /// period, confirming the normal path leaves <see cref="WaitHandleDbConnectionPool.ErrorOccurred"/>
    /// false (and exercises the <see cref="BlockingPeriodErrorState.Clear"/> fast no-op on success).
    /// </summary>
    [Fact]
    public void TryGetConnection_WhenFactorySucceeds_DoesNotEnterBlockingPeriod()
    {
        // Arrange
        var factory = new ConfigurableSqlConnectionFactory(_ => new MockDbConnectionInternal());
        var pool = CreatePool(factory);
        using var owner = new SqlConnection();

        // Act
        bool completed = TryGetConnectionSync(pool, owner, out DbConnectionInternal? connection);

        // Assert
        Assert.True(completed);
        Assert.NotNull(connection);
        Assert.False(pool.ErrorOccurred);
        Assert.Equal(1, factory.CreateConnectionCallCount);
    }

    /// <summary>
    /// Verifies that once the blocking period's exit timer fires, the next request retries the
    /// factory and a successful create recovers the pool: <see cref="WaitHandleDbConnectionPool.ErrorOccurred"/>
    /// returns to false and a connection is produced. Drives the exit timer deterministically with
    /// an injected <see cref="FakeTimeProvider"/>, guarding the
    /// <see cref="BlockingPeriodErrorState"/> timer-exit → retry → <c>Clear</c> path at the pool level.
    /// </summary>
    [Fact]
    public void TryGetConnection_AfterBlockingPeriodExpires_RetriesFactoryAndRecovers()
    {
        // Arrange: first create fails, every later create succeeds.
        SqlException failure = SqlExceptionHelper.CreateSqlException("server unreachable");
        var factory = new ConfigurableSqlConnectionFactory(
            call => call == 1 ? throw failure : new MockDbConnectionInternal());
        var fakeTime = new FakeTimeProvider();
        var pool = CreatePool(factory, timeProvider: fakeTime);
        using var owner = new SqlConnection();

        // Act: first request enters the blocking period.
        Assert.Throws<SqlException>(() => TryGetConnectionSync(pool, owner, out _));
        Assert.True(pool.ErrorOccurred);
        Assert.Equal(1, factory.CreateConnectionCallCount);

        // While blocked, a request fast-fails without reaching the factory.
        Assert.Throws<SqlException>(() => TryGetConnectionSync(pool, owner, out _));
        Assert.Equal(1, factory.CreateConnectionCallCount);

        // Advance past the initial 5s blocking period so the exit timer fires.
        fakeTime.Advance(TimeSpan.FromSeconds(5));

        // Assert: the pool has recovered and a new request reaches the factory and succeeds.
        Assert.False(pool.ErrorOccurred);
        bool completed = TryGetConnectionSync(pool, owner, out DbConnectionInternal? connection);
        Assert.True(completed);
        Assert.NotNull(connection);
        Assert.False(pool.ErrorOccurred);
        Assert.Equal(2, factory.CreateConnectionCallCount);
    }

    /// <summary>
    /// Verifies that a successful create resets the exponential backoff to the initial 5-second
    /// wait. The backoff is first allowed to grow (fail → timer-exit → fail → timer-exit, observing
    /// the doubled 10-second wait) so the reset is observable. A successful create then resets the
    /// backoff via <see cref="BlockingPeriodErrorState.Clear"/>, and a subsequent failure blocks for
    /// only the initial 5 seconds rather than the accumulated 20 seconds, confirming the success
    /// reset is wired through the pool. Drives timing deterministically with an injected
    /// <see cref="FakeTimeProvider"/>.
    /// </summary>
    [Fact]
    public void TryGetConnection_SuccessfulCreate_ResetsBackoffToInitialWait()
    {
        // Arrange: creates #1, #2 and #4 fail; #3 succeeds (recovery) to reset the backoff.
        SqlException failure = SqlExceptionHelper.CreateSqlException("server unreachable");
        var factory = new ConfigurableSqlConnectionFactory(
            call => call == 3 ? new MockDbConnectionInternal() : throw failure);
        var fakeTime = new FakeTimeProvider();
        var pool = CreatePool(factory, timeProvider: fakeTime);
        using var owner = new SqlConnection();

        // Act: first failure enters the blocking period with the initial 5s wait.
        Assert.Throws<SqlException>(() => TryGetConnectionSync(pool, owner, out _));
        Assert.Equal(1, factory.CreateConnectionCallCount);
        fakeTime.Advance(TimeSpan.FromSeconds(5)); // timer fires -> backoff doubles to 10s
        Assert.False(pool.ErrorOccurred);

        // Second failure enters with the doubled 10s wait; confirm it lasts the full 10s so the
        // backoff has demonstrably grown before we reset it.
        Assert.Throws<SqlException>(() => TryGetConnectionSync(pool, owner, out _)); // create #2 fails
        Assert.Equal(2, factory.CreateConnectionCallCount);
        fakeTime.Advance(TimeSpan.FromSeconds(9));
        Assert.True(pool.ErrorOccurred);  // still blocked at 9s -> wait is 10s, not 5s
        fakeTime.Advance(TimeSpan.FromSeconds(1)); // timer fires at 10s -> backoff would double to 20s
        Assert.False(pool.ErrorOccurred);

        // A successful create resets the backoff to the initial 5s.
        Assert.True(TryGetConnectionSync(pool, owner, out _)); // create #3 succeeds -> Clear()
        Assert.Equal(3, factory.CreateConnectionCallCount);

        // A new failure enters the blocking period again.
        Assert.Throws<SqlException>(() => TryGetConnectionSync(pool, owner, out _)); // create #4 fails
        Assert.Equal(4, factory.CreateConnectionCallCount);
        Assert.True(pool.ErrorOccurred);

        // Assert: the wait was reset to the initial 5s (not the accumulated 20s).
        fakeTime.Advance(TimeSpan.FromSeconds(4));
        Assert.True(pool.ErrorOccurred);  // still blocked just before the 5s mark
        fakeTime.Advance(TimeSpan.FromSeconds(1));
        Assert.False(pool.ErrorOccurred); // cleared exactly at 5s, proving the backoff reset
    }

    /// <summary>
    /// Verifies that a timer-driven exit alone does NOT reset the backoff: when a failure recurs
    /// after the exit timer fires (with no intervening successful create), the next blocking period
    /// uses the doubled wait (10s, not the initial 5s). Confirms that only
    /// <see cref="BlockingPeriodErrorState.Clear"/> — invoked on a successful create — resets the
    /// backoff at the pool level. Drives timing deterministically with an injected
    /// <see cref="FakeTimeProvider"/>.
    /// </summary>
    [Fact]
    public void TryGetConnection_FailingAgainAfterExitTimer_StillDoublesBackoff()
    {
        // Arrange: every create fails, so the backoff is never reset by a success.
        SqlException failure = SqlExceptionHelper.CreateSqlException("server unreachable");
        var factory = new ConfigurableSqlConnectionFactory(_ => throw failure);
        var fakeTime = new FakeTimeProvider();
        var pool = CreatePool(factory, timeProvider: fakeTime);
        using var owner = new SqlConnection();

        // Act: first failure enters the blocking period (initial 5s wait).
        Assert.Throws<SqlException>(() => TryGetConnectionSync(pool, owner, out _));
        Assert.Equal(1, factory.CreateConnectionCallCount);

        // Advance past the 5s wait so the exit timer fires (no success -> backoff not reset).
        fakeTime.Advance(TimeSpan.FromSeconds(5));
        Assert.False(pool.ErrorOccurred);

        // A new failure re-enters the blocking period using the doubled 10s wait. The factory is
        // invoked again, confirming the request reached creation rather than fast-failing.
        Assert.Throws<SqlException>(() => TryGetConnectionSync(pool, owner, out _));
        Assert.Equal(2, factory.CreateConnectionCallCount);
        Assert.True(pool.ErrorOccurred);

        // Assert: still blocked at the original 5s mark (proves the wait is not 5s)...
        fakeTime.Advance(TimeSpan.FromSeconds(5));
        Assert.True(pool.ErrorOccurred);

        // ...and not cleared until the full doubled 10s elapses.
        fakeTime.Advance(TimeSpan.FromSeconds(4));
        Assert.True(pool.ErrorOccurred);
        fakeTime.Advance(TimeSpan.FromSeconds(1));
        Assert.False(pool.ErrorOccurred); // cleared exactly at 10s, proving the backoff doubled
    }

    /// <summary>
    /// <see cref="SqlConnectionFactory"/> test double whose <c>CreateConnection</c> behavior is
    /// supplied per-call (to throw or return a mock connection) and which records how many times
    /// the factory was invoked so tests can assert fast-fail (no re-invocation) versus retry.
    /// </summary>
    internal sealed class ConfigurableSqlConnectionFactory : SqlConnectionFactory
    {
        private readonly Func<int, DbConnectionInternal> _createBehavior;
        private int _callCount;

        /// <summary>
        /// The number of times <c>CreateConnection</c> has been invoked. The argument passed to
        /// the behavior delegate is the 1-based invocation index.
        /// </summary>
        internal int CreateConnectionCallCount => Volatile.Read(ref _callCount);

        internal ConfigurableSqlConnectionFactory(Func<int, DbConnectionInternal> createBehavior)
            => _createBehavior = createBehavior;

        protected override DbConnectionInternal CreateConnection(
            SqlConnectionOptions options,
            ConnectionPoolKey poolKey,
            DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
            IDbConnectionPool pool,
            DbConnection owningConnection,
            TimeoutTimer timeout)
        {
            int call = Interlocked.Increment(ref _callCount);
            return _createBehavior(call);
        }
    }

    /// <summary>
    /// Minimal <see cref="DbConnectionInternal"/> stub used as a successful create result.
    /// Duplicated locally so this test file remains self-contained, mirroring the helpers in the
    /// sibling <c>WaitHandleDbConnectionPool</c> test files.
    /// </summary>
    internal sealed class MockDbConnectionInternal : DbConnectionInternal
    {
        public override string ServerVersion => "Mock";

        public override DbTransaction BeginTransaction(System.Data.IsolationLevel il)
            => throw new NotImplementedException();

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

        internal override void ResetConnection()
        {
        }
    }
}

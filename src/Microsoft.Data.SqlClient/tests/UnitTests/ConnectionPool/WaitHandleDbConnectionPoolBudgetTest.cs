// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool;

/// <summary>
/// Verifies that <see cref="WaitHandleDbConnectionPool"/> propagates the
/// caller's overall <see cref="TimeoutTimer"/> budget through both the pool
/// wait and the physical connection-creation factory call, mirroring the
/// budget-propagation coverage already in place for
/// <c>ChannelDbConnectionPool</c>.
/// </summary>
public class WaitHandleDbConnectionPoolBudgetTest : IDisposable
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

    private WaitHandleDbConnectionPool CreatePool(
        SqlConnectionFactory connectionFactory,
        int maxPoolSize = DefaultMaxPoolSize,
        int creationTimeoutMs = DefaultCreationTimeoutInMilliseconds)
    {
        var poolGroupOptions = new DbConnectionPoolGroupOptions(
            poolByIdentity: false,
            minPoolSize: DefaultMinPoolSize,
            maxPoolSize: maxPoolSize,
            creationTimeout: creationTimeoutMs,
            loadBalanceTimeout: 0,
            hasTransactionAffinity: true);

        var dbConnectionPoolGroup = new DbConnectionPoolGroup(
            new SqlConnectionOptions("Data Source=localhost;"),
            new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
            poolGroupOptions);

        var pool = new WaitHandleDbConnectionPool(
            connectionFactory,
            dbConnectionPoolGroup,
            DbConnectionPoolIdentity.NoIdentity,
            new DbConnectionPoolProviderInfo());

        pool.Startup();
        _pool = pool;
        return pool;
    }

    /// <summary>
    /// Verifies that the <see cref="TimeoutTimer"/> the pool hands to the
    /// connection factory on the synchronous path reports a reduced
    /// remaining-time budget when the timer's clock has advanced before the
    /// pool was entered. Mirrors
    /// <c>ChannelDbConnectionPoolTest.GetConnection_TimeoutTimerReflectsPoolWaitTime</c>.
    /// </summary>
    [Fact]
    public void GetConnection_Sync_TimeoutTimerReflectsTimeAlreadyConsumed()
    {
        // Arrange: a capturing factory and a fake-time-backed timer with a
        // 30-second budget anchored at virtual time t = 0.
        var factory = new MockSqlConnectionFactory();
        var pool = CreatePool(factory);
        var owner = new SqlConnection("Timeout=30");
        var fakeTime = new FakeTimeProvider();
        TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(30), fakeTime);

        // Act: simulate 5s of budget consumed elsewhere (e.g., higher-level
        // Open() work) before the pool is entered.
        fakeTime.Advance(TimeSpan.FromSeconds(5));
        bool completed = pool.TryGetConnection(
            owner,
            taskCompletionSource: null,
            timer,
            out DbConnectionInternal? connection);

        // Assert: factory received the same timer, and it reports the reduced
        // 25-second remaining budget rather than the original 30s or the
        // pool's static 15s CreationTimeout.
        Assert.True(completed);
        Assert.NotNull(connection);
        Assert.Same(timer, factory.CapturedTimeout);
        Assert.Equal(25_000, factory.CapturedTimeout!.MillisecondsRemainingInt);
    }

    /// <summary>
    /// Async counterpart of <see cref="GetConnection_Sync_TimeoutTimerReflectsTimeAlreadyConsumed"/>.
    /// Verifies that the async pool path also forwards the caller's
    /// already-advanced <see cref="TimeoutTimer"/> to the factory.
    /// </summary>
    [Fact]
    public async Task GetConnection_Async_TimeoutTimerReflectsTimeAlreadyConsumed()
    {
        // Arrange
        var factory = new MockSqlConnectionFactory();
        var pool = CreatePool(factory);
        var owner = new SqlConnection("Timeout=30");
        var fakeTime = new FakeTimeProvider();
        TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(30), fakeTime);
        var tcs = new TaskCompletionSource<DbConnectionInternal>();

        // Act: 5s consumed before entering the pool, then an async request.
        fakeTime.Advance(TimeSpan.FromSeconds(5));
        pool.TryGetConnection(
            owner,
            taskCompletionSource: tcs,
            timer,
            out DbConnectionInternal? connection);

        // Bound the await so a regression in the pool can't hang the suite.
        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.Same(tcs.Task, completed);
        DbConnectionInternal result = await tcs.Task;

        // Assert: factory got the caller's timer with the reduced budget.
        Assert.NotNull(result);
        Assert.Same(timer, factory.CapturedTimeout);
        Assert.Equal(25_000, factory.CapturedTimeout!.MillisecondsRemainingInt);
    }

    /// <summary>
    /// SqlConnectionFactory test double that captures the
    /// <see cref="TimeoutTimer"/> handed to <c>CreateConnection</c> so tests
    /// can assert the pool propagated the caller's budget rather than
    /// constructing a fresh timer from <c>CreationTimeout</c>.
    /// </summary>
    internal sealed class MockSqlConnectionFactory : SqlConnectionFactory
    {
        internal TimeoutTimer? CapturedTimeout { get; private set; }

        protected override DbConnectionInternal CreateConnection(
            SqlConnectionOptions options,
            ConnectionPoolKey poolKey,
            DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
            IDbConnectionPool pool,
            DbConnection owningConnection,
            TimeoutTimer timeout)
        {
            CapturedTimeout = timeout;
            return new MockDbConnectionInternal();
        }
    }

    /// <summary>
    /// Minimal <see cref="DbConnectionInternal"/> stub. Mirrors the helper in
    /// <c>WaitHandleDbConnectionPoolTransactionTest</c> but is duplicated
    /// locally so this test file remains self-contained.
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

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Tests.Common;
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
[Collection(AppContextSwitchTestCollection.Name)]
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
            hasTransactionAffinity: true,
            idleTimeout: 0);

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
    /// Ensures synchronous acquisition forwards the remaining budget to physical creation,
    /// independently of whether login will later honor it or use the compatibility opt-out.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void TryGetConnection_ConsumedBudget_ForwardsRemainingTime(bool? switchEnabled)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseOverallConnectTimeoutForPoolWait = switchEnabled,
        };
        var factory = new MockSqlConnectionFactory();
        var pool = CreatePool(factory);
        using var owner = new SqlConnection("Timeout=30");
        var fakeTime = new FakeTimeProvider();
        TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(30), fakeTime);
        fakeTime.Advance(TimeSpan.FromSeconds(5));

        // Act
        bool completed = pool.TryGetConnection(
            owner,
            taskCompletionSource: null,
            timer,
            out DbConnectionInternal? connection);

        // Assert
        Assert.True(completed);
        Assert.NotNull(connection);
        Assert.Same(timer, factory.CapturedTimeout);
        Assert.Equal(25_000, factory.CapturedTimeout!.MillisecondsRemainingInt);
    }

    /// <summary>
    /// Ensures asynchronous acquisition forwards the already-consumed budget to physical
    /// creation for the default, enabled, and compatibility settings.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TryGetConnectionAsync_ConsumedBudget_ForwardsRemainingTime(bool? switchEnabled)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseOverallConnectTimeoutForPoolWait = switchEnabled,
        };
        var factory = new MockSqlConnectionFactory();
        var pool = CreatePool(factory);
        using var owner = new SqlConnection("Timeout=30");
        var fakeTime = new FakeTimeProvider();
        TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(30), fakeTime);
        fakeTime.Advance(TimeSpan.FromSeconds(5));
        var completion = new TaskCompletionSource<DbConnectionInternal>();

        // Act
        bool completed = pool.TryGetConnection(owner, completion, timer, out _);
        Task winner = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(30)));

        // Assert
        Assert.False(completed);
        Assert.Same(completion.Task, winner);
        Assert.NotNull(await completion.Task);
        Assert.Same(timer, factory.CapturedTimeout);
        Assert.Equal(25_000, factory.CapturedTimeout!.MillisecondsRemainingInt);
    }

    /// <summary>
    /// Ensures an expired overall budget also expires a queued asynchronous pool wait,
    /// rather than starting a fresh CreationTimeout by default.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    public async Task TryGetConnectionAsync_ExpiredOverallBudget_TimesOut(bool? switchEnabled)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseOverallConnectTimeoutForPoolWait = switchEnabled,
        };
        var pool = CreatePool(new MockSqlConnectionFactory(), maxPoolSize: 1);
        using var firstOwner = new SqlConnection();
        pool.TryGetConnection(
            firstOwner, taskCompletionSource: null,
            TimeoutTimer.StartNew(TimeSpan.FromSeconds(30)), out DbConnectionInternal? held);
        using var waitingOwner = new SqlConnection();
        var completion = new TaskCompletionSource<DbConnectionInternal>();

        // Act
        bool completed = pool.TryGetConnection(
            waitingOwner, completion, TimeoutTimer.StartExpired(), out _);
        Task winner = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        Assert.NotNull(held);
        Assert.False(completed);
        Assert.Same(completion.Task, winner);
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => completion.Task);
        Assert.Contains("prior to obtaining a connection from the pool", exception.Message);
        Assert.Equal(1, pool.Count);
    }

    /// <summary>
    /// Ensures the compatibility opt-out allows an asynchronous pool wait to succeed after
    /// the caller's overall budget has expired.
    /// </summary>
    [Fact]
    public async Task TryGetConnectionAsync_OverallBudgetDisabled_UsesLegacyPoolWait()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseOverallConnectTimeoutForPoolWait = false,
        };
        var pool = CreatePool(new MockSqlConnectionFactory(), maxPoolSize: 1);
        using var firstOwner = new SqlConnection();
        pool.TryGetConnection(
            firstOwner, taskCompletionSource: null,
            TimeoutTimer.StartNew(TimeSpan.FromSeconds(30)), out DbConnectionInternal? held);
        using var waitingOwner = new SqlConnection();
        var completion = new TaskCompletionSource<DbConnectionInternal>();

        // Act
        bool completed = pool.TryGetConnection(
            waitingOwner, completion, TimeoutTimer.StartExpired(), out _);
        pool.ReturnInternalConnection(held!, firstOwner);
        Task winner = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        Assert.False(completed);
        Assert.Same(completion.Task, winner);
        Assert.Same(held, await completion.Task);
    }

    /// <summary>
    /// Identifies which kind of caller-supplied <see cref="TimeoutTimer"/> a
    /// parameterized test should construct. Used because
    /// <see cref="InlineDataAttribute"/> cannot carry a live
    /// <see cref="TimeoutTimer"/> instance.
    /// </summary>
    public enum TimerKind
    {
        Expired,
        Infinite,
    }

    /// <summary>
    /// Ensures synchronous waits use the remaining caller budget by default and when enabled,
    /// while the compatibility opt-out uses CreationTimeout. Infinite waits remain infinite.
    /// </summary>
    [Theory]
    [InlineData(null,  TimerKind.Expired,  5_000, 0u)]
    [InlineData(null,  TimerKind.Infinite, 5_000, unchecked((uint)Timeout.Infinite))]
    [InlineData(true,  TimerKind.Expired,  5_000, 0u)]
    [InlineData(true,  TimerKind.Infinite, 5_000, unchecked((uint)Timeout.Infinite))]
    [InlineData(false, TimerKind.Expired,  1_500, 1_500u)]
    [InlineData(false, TimerKind.Expired,  0,     unchecked((uint)Timeout.Infinite))]
    public void ResolvePoolWaitTimeoutMs_SwitchAndBudget_ReturnsExpectedTimeout(
        bool? switchEnabled,
        TimerKind timerKind,
        int creationTimeoutMs,
        uint expected)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseOverallConnectTimeoutForPoolWait = switchEnabled,
        };
        TimeoutTimer timer = timerKind switch
        {
            TimerKind.Expired => TimeoutTimer.StartExpired(),
            TimerKind.Infinite => TimeoutTimer.StartNew(TimeSpan.Zero),
            _ => throw new ArgumentOutOfRangeException(nameof(timerKind)),
        };

        // Act
        uint result = WaitHandleDbConnectionPool.ResolvePoolWaitTimeoutMs(
            timer,
            creationTimeoutMs);

        // Assert
        Assert.Equal(expected, result);
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

        protected override void Activate(Transaction? transaction, TimeoutTimer timeout)
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

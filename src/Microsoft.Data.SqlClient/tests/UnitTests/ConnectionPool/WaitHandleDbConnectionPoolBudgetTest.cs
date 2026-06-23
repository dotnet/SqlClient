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
    /// Verifies that the <see cref="TimeoutTimer"/> the pool hands to the
    /// connection factory reports a reduced remaining-time budget when the
    /// timer's clock has advanced before the pool was entered. Both the
    /// synchronous (<c>taskCompletionSource == null</c>) and asynchronous
    /// paths must forward the caller's already-advanced timer rather than
    /// constructing a fresh one from <c>CreationTimeout</c>. Mirrors
    /// <c>ChannelDbConnectionPoolTest.GetConnection_TimeoutTimerReflectsPoolWaitTime</c>.
    /// </summary>
    [Theory]
    [InlineData(false)] // sync
    [InlineData(true)]  // async
    public async Task GetConnection_TimeoutTimerReflectsTimeAlreadyConsumed(bool async)
    {
        // Arrange: capturing factory and a fake-time-backed timer with a
        // 30-second budget anchored at virtual time t = 0.
        var factory = new MockSqlConnectionFactory();
        var pool = CreatePool(factory);
        var owner = new SqlConnection("Timeout=30");
        var fakeTime = new FakeTimeProvider();
        TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(30), fakeTime);
        TaskCompletionSource<DbConnectionInternal>? tcs =
            async ? new TaskCompletionSource<DbConnectionInternal>() : null;

        // Act: simulate 5s of budget consumed elsewhere (e.g., higher-level
        // Open() work) before the pool is entered, then request a connection.
        fakeTime.Advance(TimeSpan.FromSeconds(5));
        bool completed = pool.TryGetConnection(
            owner,
            tcs,
            timer,
            out DbConnectionInternal? connection);

        if (async)
        {
            // Bound the await so a regression in the pool can't hang the suite.
            Task winner = await Task.WhenAny(tcs!.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            Assert.Same(tcs.Task, winner);
            connection = await tcs.Task;
        }
        else
        {
            Assert.True(completed);
        }

        // Assert: factory received the same timer, and it reports the reduced
        // 25-second remaining budget rather than the original 30s or the
        // pool's static 15s CreationTimeout.
        Assert.NotNull(connection);
        Assert.Same(timer, factory.CapturedTimeout);
        Assert.Equal(25_000, factory.CapturedTimeout!.MillisecondsRemainingInt);
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
    /// Verifies the resolution matrix for the synchronous
    /// <c>WaitHandle.WaitAny</c> timeout:
    /// <list type="bullet">
    ///   <item>switch ON  → use the caller timer's remaining budget
    ///   (expired → 0, infinite → <see cref="Timeout.Infinite"/>);</item>
    ///   <item>switch OFF → ignore the caller timer and use
    ///   <c>CreationTimeout</c>, treating <c>0</c> as
    ///   <see cref="Timeout.Infinite"/> per legacy behavior.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData(true,  TimerKind.Expired,  5_000, 0u)]
    [InlineData(true,  TimerKind.Infinite, 5_000, unchecked((uint)Timeout.Infinite))]
    [InlineData(false, TimerKind.Expired,  1_500, 1_500u)]
    [InlineData(false, TimerKind.Expired,  0,     unchecked((uint)Timeout.Infinite))]
    public void ResolvePoolWaitTimeoutMs_ReturnsExpected(
        bool switchEnabled,
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

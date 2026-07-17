// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool;

/// <summary>
/// Deterministic tests for WaitHandleDbConnectionPool idle-timeout enforcement.
/// Mirrors the corresponding tests in <see cref="ChannelDbConnectionPoolTest"/> so that the
/// retrieval-side idle-expiry behavior is covered for both pool implementations.
/// </summary>
public class WaitHandleDbConnectionPoolIdleTimeoutTest : IDisposable
{
    private const int DefaultMaxPoolSize = 50;
    private const int DefaultMinPoolSize = 0;
    private const int DefaultCreationTimeoutInMilliseconds = 15000;

    private WaitHandleDbConnectionPool _pool = null!;

    public void Dispose()
    {
        _pool?.Shutdown();
        _pool?.Clear();
    }

    private WaitHandleDbConnectionPool CreatePool(int idleTimeoutSeconds)
    {
        var poolGroupOptions = new DbConnectionPoolGroupOptions(
            poolByIdentity: false,
            minPoolSize: DefaultMinPoolSize,
            maxPoolSize: DefaultMaxPoolSize,
            creationTimeout: DefaultCreationTimeoutInMilliseconds,
            loadBalanceTimeout: 0,
            hasTransactionAffinity: true,
            idleTimeout: idleTimeoutSeconds);

        var dbConnectionPoolGroup = new DbConnectionPoolGroup(
            new SqlConnectionOptions("Data Source=localhost;"),
            new ConnectionPoolKey("TestDataSource", credential: null, accessToken: null, accessTokenCallback: null, sspiContextProvider: null),
            poolGroupOptions);

        var pool = new WaitHandleDbConnectionPool(
            new WaitHandleDbConnectionPoolTransactionTest.MockSqlConnectionFactory(),
            dbConnectionPoolGroup,
            DbConnectionPoolIdentity.NoIdentity,
            new DbConnectionPoolProviderInfo());

        pool.Startup();
        return pool;
    }

    private DbConnectionInternal GetConnection(SqlConnection owner)
    {
        _pool.TryGetConnection(
            owner,
            taskCompletionSource: null,
            TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)),
            out DbConnectionInternal? connection);
        Assert.NotNull(connection);
        return connection!;
    }

    // Forcibly rewinds a connection's ReturnedTime by the given amount so tests don't have to sleep.
    private static void BackdateReturnedTime(DbConnectionInternal connection, TimeSpan delta)
    {
        connection.ReturnedTime = DateTime.UtcNow - delta;
    }

    [Fact]
    public void IdleTimeout_StampedOnReturn()
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.UseLegacyIdleTimeoutBehavior = false;

        // Arrange - long idle timeout so the return path stamps (not evicts).
        _pool = CreatePool(idleTimeoutSeconds: 3600);
        SqlConnection owner = new();
        DbConnectionInternal connection = GetConnection(owner);

        // Backdate by a small amount that's still well inside the idle window so the return path
        // doesn't decide to evict instead of stamp.
        BackdateReturnedTime(connection, TimeSpan.FromSeconds(5));
        DateTime stampedBack = connection.ReturnedTime;

        // Act
        DateTime before = DateTime.UtcNow;
        _pool.ReturnInternalConnection(connection, owner);
        DateTime after = DateTime.UtcNow;

        // Assert: stamp falls within the return window and is strictly newer than the backdated value.
        Assert.InRange(connection.ReturnedTime, before, after);
        Assert.True(connection.ReturnedTime > stampedBack);
    }

    [Fact]
    public void IdleTimeout_Zero_DoesNotExpire()
    {
        // Arrange - pool with idle expiry disabled
        _pool = CreatePool(idleTimeoutSeconds: 0);
        SqlConnection owner = new();
        DbConnectionInternal first = GetConnection(owner);

        // Return + back-date ReturnedTime to simulate a long sit.
        _pool.ReturnInternalConnection(first, owner);
        BackdateReturnedTime(first, TimeSpan.FromHours(1));

        // Act
        SqlConnection owner2 = new();
        DbConnectionInternal second = GetConnection(owner2);

        // Assert - same instance, idle expiry disabled
        Assert.Same(first, second);
        Assert.Equal(1, _pool.Count);
    }

    [Fact]
    public void IdleTimeout_Set_ExpiresOldConnection()
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.UseLegacyIdleTimeoutBehavior = false;

        // Arrange - pool with 1-second idle timeout
        _pool = CreatePool(idleTimeoutSeconds: 1);
        SqlConnection owner = new();
        DbConnectionInternal first = GetConnection(owner);

        // Return + back-date ReturnedTime beyond the timeout.
        _pool.ReturnInternalConnection(first, owner);
        BackdateReturnedTime(first, TimeSpan.FromSeconds(5));

        // Act - request another connection
        SqlConnection owner2 = new();
        DbConnectionInternal second = GetConnection(owner2);

        // Assert - the expired one is discarded; a new one is minted.
        Assert.NotSame(first, second);
    }

    [Fact]
    public void IdleTimeout_Set_KeepsFreshConnection()
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.UseLegacyIdleTimeoutBehavior = false;

        // Arrange - 60-second idle timeout, connection just returned
        _pool = CreatePool(idleTimeoutSeconds: 60);
        SqlConnection owner = new();
        DbConnectionInternal first = GetConnection(owner);
        _pool.ReturnInternalConnection(first, owner);

        // Act - immediately request another connection
        SqlConnection owner2 = new();
        DbConnectionInternal second = GetConnection(owner2);

        // Assert - same instance reused, well within idle window
        Assert.Same(first, second);
    }

    [Fact]
    public void IdleTimeout_LegacySwitch_SuppressesEviction()
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.UseLegacyIdleTimeoutBehavior = true;

        // Arrange - 1-second idle timeout, but legacy switch suppresses the new eviction path.
        _pool = CreatePool(idleTimeoutSeconds: 1);
        SqlConnection owner = new();
        DbConnectionInternal first = GetConnection(owner);

        // Return + back-date well past the configured timeout.
        _pool.ReturnInternalConnection(first, owner);
        BackdateReturnedTime(first, TimeSpan.FromMinutes(5));

        // Act - request another connection.
        SqlConnection owner2 = new();
        DbConnectionInternal second = GetConnection(owner2);

        // Assert - with the legacy switch on, the stale connection is still reused.
        Assert.Same(first, second);
    }

    [Fact]
    public void IdleTimeout_LegacyOff_Zero_CleanupCallbackDoesNotEvict()
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.UseLegacyIdleTimeoutBehavior = false;

        // Arrange - new idle-timeout behavior enabled with IdleTimeout=0 means "disabled entirely":
        // the generational destroy/age-into-old-stack sweep in CleanupCallback must be a no-op so
        // connections above MinPoolSize are not pruned on the cleanup tick.
        _pool = CreatePool(idleTimeoutSeconds: 0);

        // Vend, then return, several connections so they sit in _stackNew.
        SqlConnection o1 = new();
        SqlConnection o2 = new();
        SqlConnection o3 = new();
        DbConnectionInternal c1 = GetConnection(o1);
        DbConnectionInternal c2 = GetConnection(o2);
        DbConnectionInternal c3 = GetConnection(o3);
        _pool.ReturnInternalConnection(c1, o1);
        _pool.ReturnInternalConnection(c2, o2);
        _pool.ReturnInternalConnection(c3, o3);

        int countBefore = _pool.Count;
        int idleBefore = _pool.IdleCount;
        Assert.Equal(3, countBefore);
        Assert.Equal(3, idleBefore);

        // Act - two cleanup cycles. Pre-fix this would move new->old then destroy old above
        // MinPoolSize (default 0), dropping Count and IdleCount to 0.
        InvokeCleanupCallback(_pool);
        InvokeCleanupCallback(_pool);

        // Assert - cleanup loops short-circuited; all three connections still pooled.
        Assert.Equal(countBefore, _pool.Count);
        Assert.Equal(idleBefore, _pool.IdleCount);
    }

    private static void InvokeCleanupCallback(WaitHandleDbConnectionPool pool)
    {
        MethodInfo cleanup = typeof(WaitHandleDbConnectionPool).GetMethod(
            "CleanupCallback",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        cleanup.Invoke(pool, new object?[] { null });
    }
}

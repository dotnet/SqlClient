// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
}

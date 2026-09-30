// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Extensions.Time.Testing;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Verifies isolation-level reset error handling against the in-process TDS server.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public sealed class IsolationLevelResetTests
{
    /// <summary>
    /// Models a session without a local Begin (such as an already-promoted enlistment).
    /// Reasserting a non-default level must track the change even when scrubbing is disabled;
    /// a subsequent activation may scrub it only when the reset switch is enabled.
    /// </summary>
    /// <param name="level">The ambient isolation level to reassert.</param>
    /// <param name="resetEnabled">Whether a later activation may scrub the session.</param>
    [Theory]
    [InlineData(System.Transactions.IsolationLevel.ReadUncommitted, false)]
    [InlineData(System.Transactions.IsolationLevel.ReadUncommitted, true)]
    [InlineData(System.Transactions.IsolationLevel.RepeatableRead, false)]
    [InlineData(System.Transactions.IsolationLevel.RepeatableRead, true)]
    [InlineData(System.Transactions.IsolationLevel.Serializable, false)]
    [InlineData(System.Transactions.IsolationLevel.Serializable, true)]
    [InlineData(System.Transactions.IsolationLevel.Snapshot, false)]
    [InlineData(System.Transactions.IsolationLevel.Snapshot, true)]
    [InlineData(System.Transactions.IsolationLevel.ReadCommitted, false)]
    [InlineData(System.Transactions.IsolationLevel.ReadCommitted, true)]
    [InlineData(System.Transactions.IsolationLevel.Unspecified, false)]
    [InlineData(System.Transactions.IsolationLevel.Unspecified, true)]
    [InlineData(System.Transactions.IsolationLevel.Chaos, false)]
    [InlineData(System.Transactions.IsolationLevel.Chaos, true)]
    public void ReassertSessionIsolationLevel_TracksChangesForOptInReset(
        System.Transactions.IsolationLevel level, bool resetEnabled)
    {
        using LocalAppContextSwitchesHelper switches = new();
        switches.EnableTransactionIsolationLevelReset = resetEnabled;
        using TdsServer server = new(new TdsServerArguments());
        server.Start();
        using SqlConnection connection = OpenConnection(server);
        SqlConnectionInternal inner = (SqlConnectionInternal)connection.InnerConnection;
        Assert.False(inner.IsolationLevelDirty);
        int batches = 0;
        server.OnSQLBatchCompleted = _ => Interlocked.Increment(ref batches);

        inner.ReassertSessionIsolationLevel(level, 15);

        bool changesLevel = level == System.Transactions.IsolationLevel.ReadUncommitted ||
            level == System.Transactions.IsolationLevel.RepeatableRead ||
            level == System.Transactions.IsolationLevel.Serializable ||
            level == System.Transactions.IsolationLevel.Snapshot;
        Assert.Equal(changesLevel ? 1 : 0, Volatile.Read(ref batches));
        Assert.Equal(changesLevel, inner.IsolationLevelDirty);

        inner.ActivateConnection(null, TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

        Assert.Equal(changesLevel ? (resetEnabled ? 2 : 1) : 0, Volatile.Read(ref batches));
        Assert.Equal(changesLevel && !resetEnabled, inner.IsolationLevelDirty);
        Assert.False(inner.IsConnectionDoomed);
    }

    /// <summary>
    /// Ensures a reset batch reaches the server and leaves the physical connection usable.
    /// </summary>
    [Fact]
    public void ResetSessionIsolationLevel_Success_KeepsConnectionUsable()
    {
        using TdsServer server = new(new TdsServerArguments());
        server.Start();
        using SqlConnection connection = OpenConnection(server);
        int batchCount = 0;
        server.OnSQLBatchCompleted = _ => Interlocked.Increment(ref batchCount);

        SqlConnectionInternal internalConnection = (SqlConnectionInternal)connection.InnerConnection;
        internalConnection.ResetSessionIsolationLevel(TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

        Assert.Equal(1, Volatile.Read(ref batchCount));
        Assert.False(internalConnection.IsConnectionDoomed);
    }

    /// <summary>
    /// Ensures a server rejection dooms the physical connection and propagates the original error,
    /// rather than handing out a session whose isolation level was not reset.
    /// </summary>
    [Fact]
    public void ResetSessionIsolationLevel_ServerRejects_DoomsConnection()
    {
        const uint errorNumber = 50000;
        using TransientTdsErrorTdsServer server = new(
            new TransientTdsErrorTdsServerArguments
            {
                ErrorClass = 16
            });
        server.Start();
        using SqlConnection connection = OpenConnection(server);
        server.SetErrorBehavior(true, errorNumber);

        SqlConnectionInternal internalConnection = (SqlConnectionInternal)connection.InnerConnection;
        SqlException exception = Assert.Throws<SqlException>(
            () => internalConnection.ResetSessionIsolationLevel(TimeoutTimer.StartNew(TimeSpan.FromSeconds(15))));

        Assert.Equal((int)errorNumber, exception.Number);
        Assert.True(internalConnection.IsConnectionDoomed);
    }

    /// <summary>
    /// Ensures an already-doomed connection does not send any SQL batch to the server.
    /// </summary>
    [Fact]
    public void ResetSessionIsolationLevel_ConnectionAlreadyDoomed_SkipsReset()
    {
        using TdsServer server = new(new TdsServerArguments());
        server.Start();
        using SqlConnection connection = OpenConnection(server);
        int batchCount = 0;
        server.OnSQLBatchCompleted = _ => Interlocked.Increment(ref batchCount);

        SqlConnectionInternal internalConnection = (SqlConnectionInternal)connection.InnerConnection;
        internalConnection.DoomThisConnection();
        internalConnection.ResetSessionIsolationLevel(TimeoutTimer.StartNew(TimeSpan.FromSeconds(15)));

        Assert.Equal(0, Volatile.Read(ref batchCount));
        Assert.True(internalConnection.IsConnectionDoomed);
    }

    /// <summary>
    /// Verifies that both pools pass the remaining checkout budget to the reset batch on their
    /// sync and async paths, independently of Command Timeout and the legacy pool-wait switch.
    /// </summary>
    /// <param name="poolV2">Whether to use the channel pool.</param>
    /// <param name="async">Whether to acquire through the async pool path.</param>
    /// <param name="overallTimeout">Whether the legacy pool wait uses the overall budget.</param>
    /// <param name="waitForConnection">Whether to consume the budget while queued behind an owner.</param>
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, true)]
    public async Task PooledReset_UsesRemainingOpenBudget(bool poolV2, bool async, bool overallTimeout, bool waitForConnection)
    {
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseConnectionPoolV2 = poolV2;
        switches.UseOverallConnectTimeoutForPoolWait = overallTimeout;
        switches.EnableTransactionIsolationLevelReset = true;
        using TdsServer server = new(new TdsServerArguments());
        server.Start();
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"127.0.0.1,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = true,
            MaxPoolSize = 1,
            ConnectTimeout = 30,
            CommandTimeout = 0,
            ApplicationName = Guid.NewGuid().ToString()
        };
        using SqlConnection first = new(builder.ConnectionString);
        first.Open();
        SqlConnectionInternal inner = (SqlConnectionInternal)first.InnerConnection;
        var pool = inner.Pool;
        // The simulated server does not implement TM_BEGIN_XACT; seed the resulting dirty state.
        inner.IsolationLevelDirty = true;
        if (!waitForConnection)
        {
            first.Close();
        }

        int observedTimeout = -1;
        int batches = 0;
        server.OnSQLBatchCompleted = _ =>
        {
            observedTimeout = inner.Parser._physicalStateObj.GetTimeoutRemaining();
            Interlocked.Increment(ref batches);
        };
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        TimeoutTimer timeout = TimeoutTimer.StartNew(TimeSpan.FromSeconds(30), clock);
        if (!waitForConnection)
        {
            clock.Advance(TimeSpan.FromMilliseconds(28_750));
        }
        using SqlConnection owner = new(builder.ConnectionString);
        TaskCompletionSource<DbConnectionInternal>? completion = async ? new() : null;
        DbConnectionInternal? acquired = null;
        try
        {
            bool completed = pool.TryGetConnection(owner, completion, timeout, out acquired);
            if (waitForConnection)
            {
                Assert.False(completed);
                Assert.False(completion!.Task.IsCompleted);
                clock.Advance(TimeSpan.FromMilliseconds(28_750));
                first.Close();
            }
            if (!completed)
            {
                Assert.NotNull(completion);
                Task winner = await Task.WhenAny(completion!.Task, Task.Delay(TimeSpan.FromSeconds(10)));
                Assert.Same(completion.Task, winner);
                acquired = await completion.Task;
            }
            Assert.Same(inner, acquired);
            Assert.Equal(1, Volatile.Read(ref batches));
            Assert.InRange(observedTimeout, 1, 1250);
        }
        finally
        {
            if (acquired is not null)
            {
                pool.ReturnInternalConnection(acquired, owner);
            }
            SqlConnection.ClearPool(first);
        }
    }

    /// <summary>
    /// Verifies finite, exhausted, and intentionally infinite Open budgets without substituting
    /// Command Timeout or rounding a subsecond remainder up to a fresh timeout.
    /// </summary>
    /// <param name="connectTimeout">The configured connection timeout in seconds.</param>
    /// <param name="commandTimeout">An unrelated user command timeout in seconds.</param>
    /// <param name="consumedMilliseconds">Time consumed before reset.</param>
    [Theory]
    [InlineData(30, 0, 29750)]
    [InlineData(30, 1, 0)]
    [InlineData(1, 30, 1000)]
    [InlineData(0, 1, 60000)]
    [InlineData(0, 0, 60000)]
    public void ResetSessionIsolationLevel_UsesOpenBudget(int connectTimeout, int commandTimeout, int consumedMilliseconds)
    {
        using TdsServer server = new(new TdsServerArguments());
        server.Start();
        using SqlConnection connection = new(new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
            ConnectTimeout = connectTimeout,
            CommandTimeout = commandTimeout
        }.ConnectionString);
        connection.Open();
        SqlConnectionInternal inner = (SqlConnectionInternal)connection.InnerConnection;
        int observedTimeout = -1;
        int batches = 0;
        server.OnSQLBatchCompleted = _ =>
        {
            observedTimeout = inner.Parser._physicalStateObj.GetTimeoutRemaining();
            Interlocked.Increment(ref batches);
        };
        var clock = new FakeTimeProvider();
        TimeoutTimer timeout = TimeoutTimer.StartNew(TimeSpan.FromSeconds(connectTimeout), clock);
        clock.Advance(TimeSpan.FromMilliseconds(consumedMilliseconds));
        if (!timeout.IsInfinite && timeout.MillisecondsRemaining == 0)
        {
            Assert.Throws<InvalidOperationException>(() => inner.ResetSessionIsolationLevel(timeout));
            Assert.True(inner.IsConnectionDoomed);
            Assert.Equal(0, Volatile.Read(ref batches));
        }
        else
        {
            inner.ResetSessionIsolationLevel(timeout);
            Assert.Equal(1, Volatile.Read(ref batches));
            if (timeout.IsInfinite)
            {
                Assert.Equal(Timeout.Infinite, observedTimeout);
            }
            else
            {
                Assert.InRange(observedTimeout, Math.Max(1, timeout.MillisecondsRemainingInt - 1000),
                    timeout.MillisecondsRemainingInt);
            }
            Assert.False(inner.IsConnectionDoomed);
        }
    }

    /// <summary>
    /// Opens a non-pooled connection to the supplied in-process TDS server.
    /// </summary>
    private static SqlConnection OpenConnection(GenericTdsServer<TdsServerArguments> server)
    {
        SqlConnection connection = new(
            new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false,
#if NETFRAMEWORK
                #pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
                TransparentNetworkIPResolution = false,
                #pragma warning restore 618
#endif
            }.ConnectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Opens a non-pooled connection to the supplied error-injecting TDS server.
    /// </summary>
    private static SqlConnection OpenConnection(
        GenericTdsServer<TransientTdsErrorTdsServerArguments> server)
    {
        SqlConnection connection = new(
            new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false,
#if NETFRAMEWORK
                #pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
                TransparentNetworkIPResolution = false,
                #pragma warning restore 618
#endif
            }.ConnectionString);
        connection.Open();
        return connection;
    }
}

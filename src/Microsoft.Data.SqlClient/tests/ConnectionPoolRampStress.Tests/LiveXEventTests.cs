// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;
using Xunit;
using Xunit.Sdk;

namespace ConnectionPoolRampStress.Tests;

/// <summary>Requires explicit permission to create temporary server-scoped XEvent sessions.</summary>
public sealed class LiveXEventFactAttribute : FactAttribute
{
    /// <summary>Skips default runs without an environment-only SQL connection and capture opt-in.</summary>
    public LiveXEventFactAttribute()
    {
        Timeout = 120000;
        if (Environment.GetEnvironmentVariable("SQLCLIENT_RAMP_XEVENT_TESTS") != "1" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQLCLIENT_RAMP_CONNECTION")))
            Skip = "Set SQLCLIENT_RAMP_CONNECTION and SQLCLIENT_RAMP_XEVENT_TESTS=1 for live XEvent tests.";
    }
}

/// <summary>Checks real server timing, preflight isolation, pool reuse, and cleanup after child termination.</summary>
public sealed class LiveXEventTests
{
    /// <summary>Full login payloads can exceed the XML limit unless consumed ring events are retired.</summary>
    [LiveXEventFact]
    public async Task RingBufferRolloverPreservesLoginTotals()
    {
        await WithSafeErrors(() =>
        {
            string input = Environment.GetEnvironmentVariable("SQLCLIENT_RAMP_CONNECTION")!;
            string application = $"ConnectionPoolRampStress_{Guid.NewGuid():N}";
            Sample sample = new(Workload.OpenClose, PoolMode.Disabled, CallerMode.SyncDedicated, PoolProfile.Default, 4, 1);
            ConnectionFactory factory = new(input, sample, Settings(), application);
            LoginXEvents capture = new(input, application);
            LoginXEventResult result;
            try
            {
                capture.Start();
                Task[] workers = Enumerable.Range(0, 4).Select(_ => Helpers.OnThread(() =>
                {
                    for (int i = 0; i < LoginXEvents.MaximumRetainedEvents; i++)
                    {
                        using IConnection connection = factory.Create();
                        connection.Open();
                    }
                    return true;
                })).ToArray();
                Task.WhenAll(workers).GetAwaiter().GetResult();
            }
            finally { result = capture.Finish(); }
            Assert.Equal("captured", result.Status);
            Assert.Equal(4 * LoginXEvents.MaximumRetainedEvents, result.CapturedEvents);
            Assert.False(result.Truncated);
            Assert.True(result.SessionDropped);
            AssertSessionAbsent(result.SessionName);
        });
    }

    /// <summary>Captures both sync and async callers under each process-isolated pool mode.</summary>
    [LiveXEventFact]
    public async Task ServerLoginsAreCapturedWithoutCountingPreflightOrObserver()
    {
        await WithSafeErrors(() =>
        {
            foreach (PoolMode pool in Enum.GetValues<PoolMode>())
            foreach (CallerMode caller in new[] { CallerMode.SyncDedicated, CallerMode.Async })
            {
                Sample sample = new(Workload.OpenClose, pool, caller, PoolProfile.Default, 1, 1);
                SupervisedSample result = new ObservedSampleRunner().Run(Settings(), sample, _ => { });
                Assert.Equal(Outcome.Success, result.Outcome);
                LoginXEventResult xe = Assert.IsType<LoginXEventResult>(result.XEvents);
                Assert.Equal("captured", xe.Status);
                Assert.True(xe.SessionDropped);
                Assert.Null(xe.Failure);
                Assert.True(xe.SuccessfulLogins.Count > 0);
                Assert.Equal(0, xe.FailedLogins.Count);
                Assert.NotEmpty(xe.Intervals);
                long opens = result.Result!.Measured.Opens.Count + result.Result.Drain.Opens.Count;
                if (pool == PoolMode.Disabled) Assert.Equal(opens, xe.SuccessfulLogins.Count);
                else
                {
                    Assert.Equal(1, xe.SuccessfulLogins.Count);
                    Assert.True(opens > xe.SuccessfulLogins.Count);
                }
                AssertSessionAbsent(xe.SessionName);
            }
        });
    }

    /// <summary>A hard-killed child cannot orphan the parent-owned server session.</summary>
    [LiveXEventFact]
    public async Task KilledChildStillDropsServerSession()
    {
        await WithSafeErrors(() =>
        {
            Settings settings = Settings() with { StartupSeconds = 3, DrainSeconds = 0.1, DeadlineSeconds = 4, CleanupSeconds = 0.2 };
            var result = new ObservedSampleRunner().Run(settings, Helpers.Sample(), _ => { }, "hang");
            Assert.Equal(Outcome.Incomplete, result.Outcome);
            Assert.True(result.Killed);
            Assert.True(result.Reaped);
            Assert.True(result.XEvents!.SessionDropped);
            Assert.Equal(0, result.XEvents.CapturedEvents);
            AssertSessionAbsent(result.XEvents.SessionName);
        });
    }

    /// <summary>Bounds small live workloads independently of the normal sustained experiment defaults.</summary>
    /// <returns>Explicitly opted-in telemetry settings.</returns>
    private static Settings Settings() => new()
    {
        XEvents = true, DurationSeconds = 0.5, IntervalSeconds = 0.1,
        ConnectTimeoutSeconds = 3, StartupSeconds = 15, DrainSeconds = 2,
        DeadlineSeconds = 20, CleanupSeconds = 1
    };

    /// <summary>Verifies cleanup using a parameterized query over server-session definitions.</summary>
    /// <param name="name">The generated capture session identifier.</param>
    private static void AssertSessionAbsent(string name)
    {
        SqlConnectionStringBuilder builder = new(Environment.GetEnvironmentVariable("SQLCLIENT_RAMP_CONNECTION")!)
        {
            ApplicationName = "ConnectionPoolRampStress-test-observer",
            Pooling = false, ConnectTimeout = 3, ConnectRetryCount = 0
        };
        using SqlConnection connection = new(builder.ConnectionString);
        connection.Open();
        using SqlCommand command = connection.CreateCommand();
        command.CommandTimeout = 5;
        command.CommandText = "SELECT COUNT(*) FROM sys.server_event_sessions WHERE name = @name";
        command.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 128).Value = name;
        Assert.Equal(0, (int)command.ExecuteScalar());
    }

    /// <summary>Prevents live SQL exception text from appearing in unattended test output.</summary>
    /// <param name="action">The live scenario whose failures are reduced to safe diagnostics.</param>
    /// <returns>A task observing bounded supervision on a dedicated thread.</returns>
    private static Task WithSafeErrors(Action action) => Helpers.OnThread(() =>
    {
        try
        {
            action();
            return true;
        }
        catch (XunitException) { throw; }
        catch (Exception exception)
        {
            throw new XunitException($"Live XEvent test failed: {Failure.Describe(exception)}");
        }
    });
}

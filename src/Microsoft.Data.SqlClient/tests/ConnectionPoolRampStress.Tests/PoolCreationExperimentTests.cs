// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ConnectionPoolRampStress.Tests;

/// <summary>Checks the opt-in constructor seam without allowing physical network creation.</summary>
public sealed class PoolCreationExperimentTests
{
    /// <summary>Rejects incompatible samples rather than treating an external gate as an internal limiter.</summary>
    [Theory]
    [InlineData(-1, 1, "v2", "cold-ramps")]
    [InlineData(4097, 1, "v2", "cold-ramps")]
    [InlineData(4, 0, "v2", "cold-ramps")]
    [InlineData(4, 1, "all", "cold-ramps")]
    [InlineData(4, 1, "legacy", "cold-ramps")]
    [InlineData(4, 1, "disabled", "cold-ramps")]
    [InlineData(4, 1, "v2", "open-close")]
    [InlineData(0, 2, "v2", "cold-ramps")]
    [InlineData(0, -1, "v2", "cold-ramps")]
    public void RejectsInvalidExperiments(int limit, int rounds, string pool, string workload) =>
        Assert.Throws<ArgumentException>(() => new Settings
        {
            PoolCreationLimit = limit, Rounds = rounds, Pool = pool, Work = workload
        }.Validate());

    /// <summary>Defaults remain unchanged and the selected constructor cap survives child JSON serialization.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(10)]
    public void CommandLineRoundTripsExperiment(int limit)
    {
        Assert.Equal(0, new Settings().Rounds);
        Assert.Equal(0, new Settings().PoolCreationLimit);
        Assert.Equal(0, CommandLine.Run(
            ["--list", "--pool", "v2", "--workload", "cold-ramps", "--rounds", "1", "--pool-creation-limit", limit.ToString()],
            (settings, list) =>
            {
                Assert.True(list);
                Settings restored = JsonSerializer.Deserialize<Settings>(
                    JsonSerializer.Serialize(settings, Wire.Json), Wire.Json)!;
                Assert.Equal(limit, restored.PoolCreationLimit);
                Assert.Equal(1, restored.Rounds);
                Assert.Equal(0, restored.MaxConcurrentOpens);
                return 0;
            }));
    }

    /// <summary>Installs the actual constructor dependency into an empty pool and owns one limiter per pool.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(10)]
    public void ConstructorRegistrationAndCleanupAreVerified(int limit)
    {
        ConnectionFactory first = Factory(limit);
        ConnectionFactory second = Factory(limit);
        try
        {
            first.StartMeasurement();
            second.StartMeasurement();
            PoolCreationAdapter a = first.CreationAdapter!;
            PoolCreationAdapter b = second.CreationAdapter!;
            Assert.NotEqual(a.Snapshot.PoolId, b.Snapshot.PoolId);
            Assert.Equal(0, a.PoolCount);
            Assert.Equal(typeof(SqlConnection).Assembly.GetName().Version!.ToString(), a.Snapshot.DriverAssemblyVersion);
            Assert.Equal(typeof(SqlConnection).Assembly.ManifestModule.ModuleVersionId.ToString(), a.Snapshot.DriverModuleId);
            Assert.EndsWith(".ChannelDbConnectionPool", a.Snapshot.PoolType);
            if (limit == 0) Assert.Null(a.Limiter);
            else
            {
                Assert.NotSame(a.Limiter, b.Limiter);
                using RateLimitLease lease = a.Limiter!.AttemptAcquire(limit);
                Assert.True(lease.IsAcquired);
                using RateLimitLease denied = a.Limiter.AttemptAcquire();
                Assert.False(denied.IsAcquired);
                Assert.Equal(limit, b.Limiter!.GetStatistics()!.CurrentAvailablePermits);
            }
            first.ObserveColdRound([]);
            first.ClearPool();
            Assert.Throws<NotSupportedException>(() => first.Create());
        }
        finally
        {
            first.StopMeasurement();
            second.StopMeasurement();
        }
        Assert.True(first.PoolCreation!.PoolCleared);
        Assert.Equal(limit == 0 ? (bool?)null : true, first.PoolCreation.LimiterDisposed);
        Assert.Equal(limit == 0 ? (long?)null : limit, first.PoolCreation.AvailablePermitsAfterDrain);
        Assert.True(second.PoolCreation!.PoolCleared);
        string json = JsonSerializer.Serialize(first.PoolCreation, Wire.Json);
        Assert.DoesNotContain("unresolved.invalid", json);
        Assert.DoesNotContain("ConnectionPoolRampStress_", json);
    }

    /// <summary>Exhausting the injected limiter parks real driver opens without entering network creation.</summary>
    [Theory]
    [InlineData(4, true)]
    [InlineData(4, false)]
    [InlineData(10, true)]
    [InlineData(10, false)]
    public async Task ActualPoolWaitsForItsConstructorPermits(int limit, bool asynchronous)
    {
        ConnectionFactory factory = Factory(limit);
        factory.StartMeasurement();
        PoolCreationAdapter adapter = factory.CreationAdapter!;
        using CancellationTokenSource cancel = new();
        using RateLimitLease heldPermits = adapter.Limiter!.AttemptAcquire(limit);
        IConnection connection = factory.Create();
        Task open = asynchronous ? connection.OpenAsync(cancel.Token) :
            Helpers.OnThread(() => { connection.Open(); return true; });
        try
        {
            await Until(() => adapter.Limiter.GetStatistics()!.TotalFailedLeases > 0);
            Assert.False(open.IsCompleted);
            Assert.Equal(0, adapter.PoolCount);
            Assert.Equal(0, adapter.Limiter.GetStatistics()!.CurrentAvailablePermits);
            if (asynchronous) cancel.Cancel();
            Exception? failure = await Record.ExceptionAsync(() => open.WaitAsync(TimeSpan.FromSeconds(5)));
            if (asynchronous) Assert.IsAssignableFrom<OperationCanceledException>(failure);
            else Assert.IsType<InvalidOperationException>(failure);
            Assert.Equal(0, adapter.PoolCount);
        }
        finally
        {
            cancel.Cancel();
            await Record.ExceptionAsync(() => open.WaitAsync(TimeSpan.FromSeconds(5)));
            connection.Dispose();
            heldPermits.Dispose();
            factory.StopMeasurement();
        }
        Assert.Equal(limit, factory.PoolCreation!.AvailablePermitsAfterDrain);
        Assert.True(factory.PoolCreation.FailedLeases > 0);
        Assert.Equal(1, factory.PoolCreation.SuccessfulLeases); // The test's lease, not a physical open.
        Assert.InRange(factory.PoolCreation.SampledPeakActivePermits!.Value, 0, limit);
        Assert.True(factory.PoolCreation.PoolCleared);
        Assert.True(factory.PoolCreation.LimiterDisposed);
    }

    /// <summary>Cleanup waits for the owning connection instead of disposing the pool's limiter during a pending call.</summary>
    [Fact]
    public async Task CancellationDefersLimiterDisposalUntilOwnerReleases()
    {
        ConnectionFactory factory = Factory(4);
        factory.StartMeasurement();
        PoolCreationAdapter adapter = factory.CreationAdapter!;
        using RateLimitLease held = adapter.Limiter!.AttemptAcquire(4);
        using CancellationTokenSource cancel = new();
        IConnection connection = factory.Create();
        Task open = connection.OpenAsync(cancel.Token);
        try
        {
            await Until(() => adapter.Limiter.GetStatistics()!.TotalFailedLeases > 0);
            factory.StopMeasurement();
            Assert.False(factory.PoolCreation!.LimiterDisposed);
            Assert.Throws<InvalidOperationException>(() => factory.Create());
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => open);
        }
        finally
        {
            cancel.Cancel();
            await Record.ExceptionAsync(() => open);
            held.Dispose();
            connection.Dispose();
            factory.StopMeasurement();
        }
        Assert.True(factory.PoolCreation!.LimiterDisposed);
        Assert.True(factory.PoolCreation.PoolCleared);
    }

    /// <summary>A physical creation lease can outlive outer cancellation, so pool clear must precede deferred limiter disposal.</summary>
    [Fact]
    public async Task ActiveCreationLeaseOutlivesPoolClear()
    {
        ConnectionFactory factory = Factory(4);
        factory.StartMeasurement();
        using RateLimitLease held = factory.CreationAdapter!.Limiter!.AttemptAcquire();
        try
        {
            factory.StopMeasurement();
            Assert.True(factory.PoolCreation!.PoolCleared);
            Assert.False(factory.PoolCreation.LimiterDisposed);
        }
        finally
        {
            held.Dispose();
            factory.StopMeasurement();
        }
        await Until(() => factory.PoolCreation!.LimiterDisposed == true);
        Assert.Equal(4, factory.PoolCreation!.AvailablePermitsAfterDrain);
    }

    /// <summary>Unsupported driver references, pre-existing pools, and identity pooling fail before measured opens.</summary>
    [Fact]
    public void UnsupportedLayoutsAndIdentityScopesFailClosed()
    {
        Assert.Throws<NotSupportedException>(() => PoolCreationAdapter.ValidateAssembly(typeof(string).Assembly));
        Assert.Throws<ArgumentException>(() => new ConnectionFactory("Server=unresolved.invalid;Integrated Security=true",
            Sample(), Options(4)));
        string input = $"Server=unresolved.invalid;Pooling=true;Min Pool Size=0;Max Pool Size=12;Application Name=AdapterTest_{Guid.NewGuid():N}";
        using PoolCreationAdapter first = PoolCreationAdapter.Install(input, 4);
        Assert.Throws<NotSupportedException>(() => PoolCreationAdapter.Install(input, 10));
        Assert.Equal(0, first.PoolCount);
    }

    /// <summary>Ordinary runs do not install any pool or limiter through the experiment seam.</summary>
    [Fact]
    public void DefaultFactoryDoesNotInstallAnExperiment()
    {
        ConnectionFactory factory = new("Server=unresolved.invalid", Sample(), new());
        factory.StartMeasurement();
        Assert.Null(factory.CreationAdapter);
        Assert.Null(factory.PoolCreation);
        factory.StopMeasurement();
    }

    /// <summary>New experiment strings cannot bypass the parent protocol's output allowlist.</summary>
    [Fact]
    public void ProtocolRejectsArbitraryExperimentMetadata()
    {
        SampleResult result = new(Outcome.Success, 0, 0, new MetricBucket().Snapshot(0),
            new MetricBucket().Snapshot(0), 1, 1, 1, 1, false, [], Trend.Calculate([]))
        {
            PoolCreation = new(4, "Microsoft.Data.SqlClient.ConnectionPool.ChannelDbConnectionPool",
                "7.0.0.0", Guid.NewGuid().ToString(), 1)
        };
        Assert.True(Supervisor.Valid(new("result", Result: result)));
        foreach (PoolCreationObservation bad in new[]
        {
            result.PoolCreation with { PoolType = "arbitrary" },
            result.PoolCreation with { DriverAssemblyVersion = "arbitrary" },
            result.PoolCreation with { DriverModuleId = "arbitrary" },
            result.PoolCreation with { AdapterContract = "arbitrary" }
        })
            Assert.False(Supervisor.Valid(new("result", Result: result with { PoolCreation = bad })));
    }

    /// <summary>Creates bounded one-round settings with the external admission gate disabled.</summary>
    internal static Settings Options(int limit) => new()
    {
        Pool = "v2", Work = "cold-ramps", Rounds = 1, PoolCreationLimit = limit,
        DurationSeconds = 10, IntervalSeconds = 0.1, DrainSeconds = 5,
        StartupSeconds = 5, ConnectTimeoutSeconds = 1, DeadlineSeconds = 30, CleanupSeconds = 1
    };

    /// <summary>Uses enough owners to distinguish pool capacity from creation concurrency.</summary>
    internal static Sample Sample() => new(Workload.ColdRamps, PoolMode.V2, CallerMode.Async, PoolProfile.Default, 12, 1);

    /// <summary>Uses an invalid hostname because these unit tests must never acquire a creation permit.</summary>
    private static ConnectionFactory Factory(int limit) => new("Server=unresolved.invalid", Sample(), Options(limit));

    /// <summary>Waits for an observable permit denial with a wall-clock bound.</summary>
    private static async Task Until(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(1, timeout.Token);
    }
}

/// <summary>Checks one-round admission and held ownership independently of SQL Server timing.</summary>
public sealed class OneRoundTests
{
    /// <summary>Completes exactly N attempts, holds successes until all settle, and never starts a reuse round.</summary>
    [Theory]
    [InlineData("Async", false)]
    [InlineData("SyncThreadpool", false)]
    [InlineData("SyncDedicated", false)]
    [InlineData("Async", true)]
    [InlineData("SyncThreadpool", true)]
    [InlineData("SyncDedicated", true)]
    public async Task SettledRoundStopsImmediatelyAndClosesOnce(string caller, bool timeout)
    {
        using FakeFactory factory = new(4) { TimeoutId = timeout ? 2 : 0 };
        Settings settings = PoolCreationExperimentTests.Options(0);
        Sample sample = PoolCreationExperimentTests.Sample() with
        {
            Caller = Enum.Parse<CallerMode>(caller), Concurrency = 4
        };
        using PhysicalCounters counters = new();
        SampleResult result = await Helpers.OnThread(() => new WorkloadRunner(settings, sample, factory, counters).Run(_ => { }))
            .WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(timeout ? Outcome.Failed : Outcome.Success, result.Outcome);
        Assert.Equal(1, result.CompletedRounds);
        Assert.Equal(1, factory.Clears);
        Assert.Equal(4, factory.Created);
        Assert.Equal(4, factory.Disposed);
        Assert.False(factory.PrematureDispose);
        Assert.False(factory.ClearWhileActive);
        Assert.False(result.IncompleteRound);
        Assert.True(result.InitialRamp!.Complete);
        Assert.Equal(4, result.InitialRamp.SettledAttempts);
        Assert.Equal(timeout ? 3 : 4, result.InitialRamp.SuccessfulOpens);
        Assert.Equal(timeout ? 3 : 4, result.InitialHeldConnections);
        Assert.Equal(timeout ? 1 : 0, result.FailedDriverOpenLatency.Count);
        Assert.Equal(0, result.OpenGateWait.Count);
        Assert.Equal(0, result.Measured.Cycles.Count + result.Drain.Cycles.Count);
        if (timeout) Assert.Equal("timeout-unclassified", Assert.Single(result.Failures).Failure.Category);
    }

    /// <summary>Bounded cancellation disposes every pending first-round owner without reusing or clearing an active pool.</summary>
    [Fact]
    public async Task CanceledFirstRoundDisposesLateOwnersWithoutStartingAnotherRound()
    {
        using FakeFactory factory = new(4) { Block = true };
        Settings settings = PoolCreationExperimentTests.Options(0) with { DrainSeconds = 0.1 };
        Sample sample = PoolCreationExperimentTests.Sample() with { Concurrency = 4 };
        using PhysicalCounters counters = new();
        WorkloadRunner runner = new(settings, sample, factory, counters);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(_ => { }));
        try
        {
            using CancellationTokenSource wait = new(TimeSpan.FromSeconds(5));
            while (factory.Created != 4) await Task.Delay(1, wait.Token);
            runner.RequestStop();
            SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(5));
            while (factory.Disposed != 4) await Task.Delay(1, wait.Token);
            Assert.Equal(Outcome.Incomplete, result.Outcome);
            Assert.Equal(0, result.CompletedRounds);
            Assert.True(result.IncompleteRound);
            Assert.Equal(4, factory.Created);
            Assert.Equal(0, factory.Clears);
            Assert.False(factory.ClearWhileActive);
            Assert.False(factory.PrematureDispose);
        }
        finally
        {
            runner.RequestStop();
            factory.Release.Set();
            await running.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}

/// <summary>Opts into fresh-process, real-physical-connection validation through the existing live SQL environment.</summary>
public sealed class LivePoolCreationExperimentTests
{
    /// <summary>Checks all held connections belong to the constructor-injected pool for unlimited, four, and ten permits.</summary>
    [LiveDatabaseFact(Timeout = 120000)]
    public async Task FreshChildrenHoldTwelveDistinctPhysicalConnections()
    {
        foreach (int limit in new[] { 0, 4, 10 })
        foreach (CallerMode caller in new[] { CallerMode.Async, CallerMode.SyncDedicated })
        {
            Settings settings = PoolCreationExperimentTests.Options(limit) with
            {
                WorkerMinimum = 64, ConnectTimeoutSeconds = 5, CooldownSeconds = 0
            };
            Sample sample = PoolCreationExperimentTests.Sample() with { Caller = caller, Profile = PoolProfile.Provisioned };
            SupervisedSample child = await Task.Run(() => new Supervisor().Run(settings, sample, _ => { }));
            Assert.True(child.Reaped);
            Assert.Equal(Outcome.Success, child.Outcome);
            SampleResult result = child.Result!;
            Assert.Equal(1, result.CompletedRounds);
            Assert.True(result.InitialRamp!.Complete);
            Assert.Equal(12, result.InitialRamp.SuccessfulOpens);
            Assert.Equal(12, result.InitialHeldConnections);
            Assert.Equal(12, result.PoolCreation!.VerifiedHeldConnections);
            Assert.Equal(12, result.PoolCreation.DistinctPhysicalConnectionIds);
            Assert.Equal(12, result.PoolCreation.PoolConnectionsAtSettlement);
            Assert.True(result.PoolCreation.PoolCleared);
            Assert.Equal(0, result.ConfiguredMaxConcurrentOpens);
            Assert.Equal(0, result.OpenGateWait.Count);
            if (limit != 0)
            {
                Assert.Equal(12, result.PoolCreation.SuccessfulLeases);
                Assert.Equal(limit, result.PoolCreation.AvailablePermitsAfterDrain);
                Assert.True(result.PoolCreation.LimiterDisposed);
                Assert.InRange(result.PoolCreation.SampledPeakActivePermits!.Value, 0, limit);
            }
        }
    }
}

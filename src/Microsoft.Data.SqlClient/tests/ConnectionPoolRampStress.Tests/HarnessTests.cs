// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ConnectionPoolRampStress.Tests;

/// <summary>Checks bounded matrix generation, safe configuration, and descriptive measurements without SQL Server.</summary>
public sealed class ConfigurationTests
{
    /// <summary>Includes a non-geometric maximum exactly once.</summary>
    [Theory]
    [InlineData(512, new[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512 })]
    [InlineData(10, new[] { 1, 2, 4, 8, 10 })]
    [InlineData(1, new[] { 1 })]
    public void LevelsIncludeMaximum(int maximum, int[] expected) =>
        Assert.Equal(expected, new Settings { Maximum = maximum }.Levels());

    /// <summary>Rejects unbounded timeouts and unsafe matrix sizes before launching a process.</summary>
    [Fact]
    public void InvalidConfigurationsAreRejected()
    {
        Settings[] invalid =
        [
            new() { Start = 0 }, new() { Maximum = 0 }, new() { Growth = 1 },
            new() { Repetitions = 0 }, new() { ConnectTimeoutSeconds = 0 },
            new() { DurationSeconds = double.NaN }, new() { IntervalSeconds = 0 },
            new() { DeadlineSeconds = 30 }, new() { DrainSeconds = double.PositiveInfinity },
            new() { Pool = "unknown" }, new() { WorkerMinimum = 0 },
            new() { DurationSeconds = 86400, IntervalSeconds = 0.05 }
        ];
        foreach (Settings settings in invalid) Assert.Throws<ArgumentException>(settings.Validate);
        new Settings().Validate();
    }

    /// <summary>Alternates pool order and stops only the affected combination while retaining repetitions.</summary>
    [Fact]
    public void MatrixAndStopRulesPreserveIndependentCombinations()
    {
        Settings settings = new() { Maximum = 2, Caller = "async", Work = "open-close", Profile = "default" };
        Sample[] matrix = settings.Matrix().ToArray();
        Assert.Equal(new[] { PoolMode.Legacy, PoolMode.V2, PoolMode.Disabled, PoolMode.V2, PoolMode.Legacy, PoolMode.Disabled },
            matrix.Take(6).Select(s => s.Pool));
        Sweep sweep = new();
        foreach (Sample sample in matrix.Where(s => s.Pool == PoolMode.Legacy && s.Concurrency == 1))
            sweep.Record(new(sample, Outcome.Success, 0, false, false, 0, null, null));
        Sample failing = matrix.First(s => s.Pool == PoolMode.Legacy && s.Concurrency == 2);
        sweep.Record(new(failing, Outcome.Failed, 3, false, false, 0, null, null));
        Assert.False(sweep.ShouldRun(failing));
        Assert.True(sweep.ShouldRun(failing with { Pool = PoolMode.V2 }));
        Boundary boundary = Assert.Single(sweep.Boundaries(settings));
        Assert.Equal(1, boundary.HighestAllRepetitionsSuccessful);
        Assert.Equal(2, boundary.FirstNonSuccessLevel);
    }

    /// <summary>Normalizes the measured pool without exposing the input string in safe settings.</summary>
    [Theory]
    [InlineData("Legacy", true)]
    [InlineData("V2", true)]
    [InlineData("Disabled", false)]
    public void NormalizationControlsPoolSize(string mode, bool pooling)
    {
        Sample sample = Helpers.Sample() with { Pool = Enum.Parse<PoolMode>(mode), Concurrency = 23 };
        var builder = ConnectionFactory.Normalize("Server=localhost;Min Pool Size=5;Max Pool Size=99;ConnectRetryCount=3", sample, new());
        Assert.Equal(pooling, builder.Pooling);
        Assert.Equal(23, builder.MaxPoolSize);
        Assert.Equal(0, builder.MinPoolSize);
        Assert.Equal(0, builder.ConnectRetryCount);
        Assert.False(builder.Enlist);
    }

    /// <summary>Round-trips allowlisted configuration but excludes environment names, output paths, and exception text.</summary>
    [Fact]
    public void ArbitraryInputsAndExceptionsAreNotSerialized()
    {
        const string marker = "test-secret-marker";
        var settings = new Settings { ConnectionEnvironment = marker, Output = marker };
        string json = JsonSerializer.Serialize(settings, Wire.Json);
        Assert.DoesNotContain(marker, json);
        foreach (Exception exception in new Exception[] { new ArgumentException(marker), new InvalidOperationException(marker), new Exception(marker) })
            Assert.DoesNotContain(marker, JsonSerializer.Serialize(Failure.Describe(exception), Wire.Json));
        var info = Supervisor.ChildStartInfo(settings, Helpers.Sample());
        Assert.DoesNotContain(marker, string.Join(" ", info.ArgumentList));
        Assert.DoesNotContain(marker, info.Environment["SQLCLIENT_RAMP_CONFIG"]);
    }

    /// <summary>Provisioning is explicit, validates limits, and preserves the runtime maximum.</summary>
    [Fact]
    public void ThreadPoolProfilesReadBackActualSettings()
    {
        ThreadPool.GetMinThreads(out int workers, out int io);
        ThreadPool.GetMaxThreads(out int maxWorkers, out int maxIo);
        try
        {
            var original = ThreadPoolSettings.Configure(PoolProfile.Default, workers + 1);
            Assert.Equal(workers, original.MinimumWorkers);
            int requested = Math.Min(maxWorkers, workers + 1);
            var provisioned = ThreadPoolSettings.Configure(PoolProfile.Provisioned, requested);
            Assert.Equal(requested, provisioned.MinimumWorkers);
            Assert.Equal(maxWorkers, provisioned.MaximumWorkers);
            Assert.Equal(maxIo, provisioned.MaximumIo);
            Assert.Throws<InvalidOperationException>(() => ThreadPoolSettings.Configure(PoolProfile.Provisioned, maxWorkers + 1));
        }
        finally { Assert.True(ThreadPool.SetMinThreads(workers, io)); }
    }

    /// <summary>Parser errors and callback failures never echo arbitrary argument or exception contents.</summary>
    [Fact]
    public void CommandLineSuppressesArbitraryDiagnostics()
    {
        const string marker = "test-redaction-marker";
        TextWriter previous = Console.Error;
        using StringWriter captured = new();
        try
        {
            Console.SetError(captured);
            Assert.Equal(2, CommandLine.Run(["--max", marker], (_, _) => 0));
            Assert.Equal(2, CommandLine.Run(["--list", "--max", "0"], (_, _) => 0));
            Assert.Equal(2, CommandLine.Run(["--list"], (_, _) => throw new IOException(marker)));
            Assert.DoesNotContain(marker, captured.ToString());
        }
        finally { Console.SetError(previous); }
    }
}

/// <summary>Checks histogram precision, interval attribution, and early/late comparison semantics.</summary>
public sealed class MetricsTests
{
    /// <summary>Uses nearest-rank bucket upper bounds and does not retain individual measurements.</summary>
    [Fact]
    public void HistogramAndThroughputHaveDefinedPrecision()
    {
        Histogram histogram = new();
        Assert.Null(histogram.Percentile(0.5));
        for (int i = 1; i <= 100000; i++) histogram.Add(i);
        Assert.Equal(100000, histogram.Count);
        Assert.InRange(histogram.Percentile(0.5)!.Value, 50000, 54000);
        Assert.InRange(histogram.Percentile(0.95)!.Value, 95000, 100000);
        Assert.Equal(100000, histogram.Maximum);
        Assert.Equal(50, MetricBucket.Rate(100, 2));
        Assert.Equal(0, MetricBucket.Rate(1, 0));
        Assert.Equal(320, Histogram.BucketCount);
    }

    /// <summary>Attributes completions, not admissions, to partial and drain intervals and keeps idle intervals.</summary>
    [Fact]
    public void ClockControlsIntervalAssignmentAndDrainSeparation()
    {
        ManualClock clock = new();
        Settings settings = new() { DurationSeconds = 2.5, IntervalSeconds = 1 };
        MetricStore store = new(settings, clock);
        var worker = store.NewWorker();
        worker.Add(Metric.Attempt);
        clock.Now = 1.2;
        worker.Add(Metric.Open, 12);
        clock.Now = 2.6;
        worker.Add(Metric.Open, 15);
        clock.Now = 3.1;
        store.Seal();
        IntervalResult[] intervals = store.Flush(3.1, Helpers.Resources(), final: true).ToArray();
        Assert.Equal(4, intervals.Length);
        Assert.Equal(0, intervals[0].Metrics.Opens.Count);
        Assert.Equal(1, intervals[1].Metrics.Opens.Count);
        Assert.True(intervals[2].Partial);
        Assert.True(intervals[3].Drain);
        Assert.True(intervals[3].Partial);
        Assert.Equal(1, store.Total(false, 2.5).Opens.Count);
        Assert.Equal(1, store.Total(true, 0.6).Opens.Count);
        Assert.Equal(1, store.Total(false, 2.5).Attempts);
    }

    /// <summary>Does not call a lone cold round a trend and excludes startup and drain samples.</summary>
    [Fact]
    public void TrendRequiresSeparateNonemptyIntervalGroups()
    {
        var intervals = new List<IntervalResult>();
        for (int i = 0; i < 7; i++)
        {
            MetricBucket bucket = new();
            for (int n = 0; n < (i < 4 ? 10 : 5); n++) bucket.Add(Metric.Open, i + 1, 0);
            intervals.Add(new(i, i, i + 1, i == 0, false, false, bucket.Snapshot(1), Helpers.Resources()));
        }
        var trend = Trend.Calculate(intervals);
        Assert.Equal("descriptive", trend.Status);
        Assert.Equal(-50, trend.ThroughputChangePercent);
        Assert.Equal("insufficient-data", Trend.Calculate(intervals.Take(2).ToArray()).Status);
    }

    /// <summary>An early failure closes a partial measured interval and starts separate drain accounting.</summary>
    [Fact]
    public void EarlyAdmissionStopSeparatesDrain()
    {
        ManualClock clock = new();
        MetricStore store = new(new Settings { DurationSeconds = 10, IntervalSeconds = 1 }, clock);
        var worker = store.NewWorker();
        clock.Now = 0.2;
        worker.Add(Metric.FailedOpen, 200);
        store.StopAdmission();
        clock.Now = 0.3;
        worker.Add(Metric.Open, 300);
        store.Seal();
        var intervals = store.Flush(0.3, Helpers.Resources(), final: true).ToArray();
        Assert.Equal(2, intervals.Length);
        Assert.True(intervals[0].Partial);
        Assert.True(intervals[1].Drain);
        Assert.Equal(0, store.Total(false, 0.2).Opens.Count);
        Assert.Equal(1, store.Total(true, 0.1).Opens.Count);
        worker.Add(Metric.Open, 10);
        Assert.Equal(1, store.Total(true, 0.1).Opens.Count);
    }

    /// <summary>Provides deterministic monotonic timestamps for aggregation tests.</summary>
    private sealed class ManualClock : IClock
    {
        public double Now;
        public double Seconds => Now;
    }
}

/// <summary>Exercises persistent callers and cleanup contracts against controlled fake connections.</summary>
public sealed class WorkloadTests
{
    /// <summary>Reuses fixed workers and keeps all successful cold connections until the round settles.</summary>
    [Theory]
    [InlineData("Async")]
    [InlineData("SyncThreadpool")]
    [InlineData("SyncDedicated")]
    public async Task ColdRoundsHoldUntilSettledAndClearBetweenRounds(string caller)
    {
        using FakeFactory factory = new(4);
        Sample sample = Helpers.Sample() with { Workload = Workload.ColdRamps, Caller = Enum.Parse<CallerMode>(caller), Concurrency = 4 };
        SampleResult result = await Helpers.Run(sample, factory);
        Assert.Equal(Outcome.Success, result.Outcome);
        Assert.True(result.CompletedRounds > 1);
        Assert.Equal(result.CompletedRounds, factory.Clears);
        Assert.False(factory.PrematureDispose);
        Assert.False(factory.ClearWhileActive);
        Assert.Equal(factory.Created, factory.Disposed);
        Assert.Equal(4, result.StartedWorkers);
        Assert.InRange(result.PeakOutstandingCalls, 1, 4);
        Assert.Equal(0, result.Measured.Cycles.Count);
        Assert.True(result.Measured.Rounds.Count > 0);
    }

    /// <summary>Continuous workers never clear the pool or exceed one outstanding operation per worker.</summary>
    [Theory]
    [InlineData("Async")]
    [InlineData("SyncThreadpool")]
    [InlineData("SyncDedicated")]
    public async Task OpenCloseLoopsCountCompletedCycles(string caller)
    {
        using FakeFactory factory = new(0);
        SampleResult result = await Helpers.Run(Helpers.Sample() with { Caller = Enum.Parse<CallerMode>(caller), Concurrency = 3 }, factory);
        Assert.Equal(Outcome.Success, result.Outcome);
        Assert.Equal(0, factory.Clears);
        Assert.Equal(factory.Created, factory.Disposed);
        Assert.Equal(3, result.StartedWorkers);
        Assert.InRange(result.PeakOutstandingCalls, 1, 3);
        Assert.Equal(result.Measured.Opens.Count + result.Drain.Opens.Count,
            result.Measured.Cycles.Count + result.Drain.Cycles.Count);
        Assert.True(result.Measured.Cycles.Count > 0);
    }

    /// <summary>One failed open stops admission, retains partial successes, and disposes failed instances.</summary>
    [Theory]
    [InlineData("Async")]
    [InlineData("SyncThreadpool")]
    [InlineData("SyncDedicated")]
    public async Task PartialFailureDrainsAndDisposesEveryInstance(string caller)
    {
        using FakeFactory factory = new(4) { FailId = 2 };
        var sample = Helpers.Sample() with { Workload = Workload.ColdRamps, Caller = Enum.Parse<CallerMode>(caller), Concurrency = 4 };
        SampleResult result = await Helpers.Run(sample, factory);
        Assert.Equal(Outcome.Failed, result.Outcome);
        Assert.Equal(4, factory.Created);
        Assert.Equal(4, factory.Disposed);
        Assert.Equal(1, result.Measured.FailedOpens.Count + result.Drain.FailedOpens.Count);
        Assert.Equal(3, result.Measured.Opens.Count + result.Drain.Opens.Count);
        Assert.Single(result.Failures);
    }

    /// <summary>Normal window expiry admits no more opens and credits an admitted late completion only to drain.</summary>
    [Fact]
    public async Task WindowExpiryDrainsWithoutFailure()
    {
        using FakeFactory factory = new(0) { Block = true };
        Settings settings = Helpers.Settings() with { DurationSeconds = 0.1, DrainSeconds = 2 };
        using PhysicalCounters counters = new();
        WorkloadRunner runner = new(settings, Helpers.Sample(), factory, counters);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(_ => { }));
        Assert.True(factory.Entered.Wait(TimeSpan.FromSeconds(5)));
        await Task.Delay(180);
        factory.Release.Set();
        SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Outcome.Success, result.Outcome);
        Assert.Equal(1, factory.Created);
        Assert.Equal(0, result.Measured.Opens.Count);
        Assert.Equal(1, result.Drain.Opens.Count);
        Assert.Equal(1, factory.Disposed);
    }

    /// <summary>Uncooperative synchronous calls cannot extend the coordinator's drain indefinitely.</summary>
    [Fact]
    public async Task UninterruptibleOpenProducesIncompletePartialResults()
    {
        using FakeFactory factory = new(0) { Block = true };
        var intervals = new List<IntervalResult>();
        using PhysicalCounters counters = new();
        var settings = Helpers.Settings() with { DurationSeconds = 0.1, DrainSeconds = 0.1, CleanupSeconds = 0.1 };
        var runner = new WorkloadRunner(settings, Helpers.Sample() with { Caller = CallerMode.SyncDedicated }, factory, counters);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(intervals.Add));
        try
        {
            SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Outcome.Incomplete, result.Outcome);
            Assert.NotEmpty(intervals);
            Assert.Contains(intervals, interval => interval.Drain);
        }
        finally
        {
            factory.Release.Set();
            Assert.True(SpinWait.SpinUntil(() => factory.Disposed == factory.Created, TimeSpan.FromSeconds(5)));
        }
    }

    /// <summary>External stop requests end admission, mark the sample incomplete, and still drain admitted work.</summary>
    [Fact]
    public async Task CooperativeStopPreservesCleanup()
    {
        using FakeFactory factory = new(0) { Block = true };
        using PhysicalCounters counters = new();
        var runner = new WorkloadRunner(Helpers.Settings() with { DurationSeconds = 3 }, Helpers.Sample(), factory, counters);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(_ => { }));
        Assert.True(factory.Entered.Wait(TimeSpan.FromSeconds(5)));
        runner.RequestStop();
        factory.Release.Set();
        SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Outcome.Incomplete, result.Outcome);
        Assert.Equal(1, factory.Created);
        Assert.Equal(1, factory.Disposed);
    }

    /// <summary>Cancellation at the drain limit disposes asynchronous failed-open instances.</summary>
    [Fact]
    public async Task DrainLimitCancelsAsyncOpen()
    {
        using FakeFactory factory = new(0) { Block = true };
        using PhysicalCounters counters = new();
        Settings settings = Helpers.Settings() with { DurationSeconds = 0.1, DrainSeconds = 0.1 };
        var runner = new WorkloadRunner(settings, Helpers.Sample(), factory, counters);
        SampleResult result = await Helpers.OnThread(() => runner.Run(_ => { })).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Outcome.Incomplete, result.Outcome);
        Assert.Equal(1, factory.Disposed);
        Assert.Contains(result.Failures, failure => failure.Failure.Category == "cancellation");
    }

    /// <summary>Close failure does not count a full open/close cycle or leak the failed instance.</summary>
    [Fact]
    public async Task CloseFailureIsNotASuccessfulCycle()
    {
        using FakeFactory factory = new(0) { FailDisposeId = 1 };
        SampleResult result = await Helpers.Run(Helpers.Sample(), factory);
        Assert.Equal(Outcome.Failed, result.Outcome);
        Assert.Equal(1, factory.Created);
        Assert.Equal(1, factory.Disposed);
        Assert.Equal(1, result.Measured.Opens.Count + result.Drain.Opens.Count);
        Assert.Equal(0, result.Measured.Cycles.Count + result.Drain.Cycles.Count);
        Assert.Equal(0, result.Measured.FailedOpens.Count + result.Drain.FailedOpens.Count);
        Assert.Single(result.Failures);
    }
}

/// <summary>Exercises real process isolation, deadline termination, and bounded stdout/stderr draining.</summary>
public sealed class SupervisorTests
{
    /// <summary>Accepts only canonical metadata values, even when stdout resembles the harness protocol.</summary>
    [Fact]
    public void ProtocolMetadataIsAllowlisted()
    {
        RuntimeMetadata metadata = new("7.0.0.0", new string('a', 40), "8.0.0", "macOS", "Arm64", 8,
            new(8, 1, 32767, 1000), "16.0.1",
            new(true, 4, 0, 15, 0, "Auto", "Mandatory", false, "managed", false));
        Assert.True(Supervisor.Valid(new("ready", Metadata: metadata)));
        Assert.False(Supervisor.Valid(new("ready", Metadata: metadata with { DriverVersion = "test-redaction-marker" })));
        Assert.False(Supervisor.Valid(new("ready", Metadata: metadata with { ServerResources = "test-redaction-marker" })));
        Assert.False(Supervisor.Valid(new("ready", Metadata: metadata with
        {
            Connection = metadata.Connection with { Sni = "test-redaction-marker" }
        })));
    }

    /// <summary>Preserves partial records and reports child outcomes without needing a database.</summary>
    [Theory]
    [InlineData("success", "Success")]
    [InlineData("failure", "Failed")]
    [InlineData("setup", "SetupFailure")]
    [InlineData("hang", "Incomplete")]
    [InlineData("noise", "Success")]
    public void FixtureChildrenAreSupervised(string fixture, string expected)
    {
        var packets = new List<Packet>();
        Settings settings = Helpers.Settings() with { StartupSeconds = 3, DeadlineSeconds = 4, CleanupSeconds = 0.2 };
        SupervisedSample result = new Supervisor().Run(settings, Helpers.Sample(), packets.Add, fixture);
        Assert.Equal(Enum.Parse<Outcome>(expected), result.Outcome);
        if (fixture != "setup") Assert.Contains(packets, p => p.Interval is not null);
        if (fixture == "hang")
        {
            Assert.True(result.Killed);
            Assert.True(result.Deadline);
            Assert.NotNull(result.ExitCode);
            var next = new Supervisor().Run(settings, Helpers.Sample() with { Pool = PoolMode.V2 }, _ => { }, "success");
            Assert.Equal(Outcome.Success, next.Outcome);
        }
        if (fixture == "noise") Assert.True(result.DiscardedOutputLines >= 2);
    }

    /// <summary>Oversized output is drained, bounded, and never forwarded.</summary>
    [Fact]
    public void BoundedReaderDiscardsOversizedLines()
    {
        using StringReader input = new(new string('s', 100000) + "\naccepted\n");
        List<string?> lines = new();
        Supervisor.ReadBoundedLines(input, lines.Add);
        Assert.Equal(new string?[] { null, "accepted" }, lines);
    }

    /// <summary>Raw stderr containing an environment value never reaches returned packets or summaries.</summary>
    [Fact]
    public void ChildOutputDoesNotLeakEnvironmentValues()
    {
        const string name = "SQLCLIENT_RAMP_FIXTURE_SECRET";
        const string marker = "test-redaction-marker";
        string? previous = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, marker);
            List<Packet> packets = new();
            var result = new Supervisor().Run(Helpers.Settings(), Helpers.Sample(), packets.Add, "noise");
            Assert.Equal(Outcome.Success, result.Outcome);
            Assert.DoesNotContain(marker, JsonSerializer.Serialize(new { result, packets }, Wire.Json));
        }
        finally { Environment.SetEnvironmentVariable(name, previous); }
    }
}

/// <summary>Provides fake operations with observable lifetime and deterministic round sequencing.</summary>
internal sealed class FakeFactory(int roundSize) : IConnectionFactory, IDisposable
{
    private readonly int _roundSize = roundSize;
    private int _created;
    private int _disposed;
    private int _completed;
    private int _active;
    private int _clears;
    public int FailId { get; init; }
    public int FailDisposeId { get; init; }
    public bool Block { get; init; }
    public bool PrematureDispose;
    public bool ClearWhileActive;
    public readonly ManualResetEventSlim Entered = new();
    public readonly ManualResetEventSlim Release = new();
    public int Created => Volatile.Read(ref _created);
    public int Disposed => Volatile.Read(ref _disposed);
    public int Clears => Volatile.Read(ref _clears);

    /// <summary>Creates a fake connection and assigns an observable attempt identifier.</summary>
    /// <returns>A new independently disposable fake connection.</returns>
    public IConnection Create() => new FakeConnection(this, Interlocked.Increment(ref _created));

    /// <summary>Checks all callers settled and connections were disposed before clearing.</summary>
    public void ClearPool()
    {
        if (Volatile.Read(ref _active) != 0 || Disposed != Created) ClearWhileActive = true;
        Interlocked.Increment(ref _clears);
    }

    /// <summary>Releases any blocked operation and cleans up test synchronization objects.</summary>
    public void Dispose()
    {
        Release.Set();
        Entered.Dispose();
        Release.Dispose();
    }

    /// <summary>Models synchronous and asynchronous establishment and observable disposal.</summary>
    private sealed class FakeConnection(FakeFactory owner, int id) : IConnection
    {
        private bool _opened;

        /// <summary>Blocks only the calling worker to exercise synchronous caller modes.</summary>
        public void Open()
        {
            Interlocked.Increment(ref owner._active);
            owner.Entered.Set();
            if (owner.Block) owner.Release.Wait();
            else Thread.Sleep(2);
            Finish();
        }

        /// <summary>Uses a genuinely asynchronous delay without scheduling a blocking Task.Run.</summary>
        /// <param name="cancellationToken">The runner's bounded-drain cancellation token.</param>
        /// <returns>The pending fake establishment operation.</returns>
        public async Task OpenAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref owner._active);
            owner.Entered.Set();
            try
            {
                if (owner.Block)
                {
                    while (!owner.Release.IsSet) await Task.Delay(1, cancellationToken);
                }
                else await Task.Delay(2, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Decrement(ref owner._active);
                Interlocked.Increment(ref owner._completed);
                throw;
            }
            Finish();
        }

        /// <summary>Completes the observable attempt before optionally failing.</summary>
        private void Finish()
        {
            Interlocked.Decrement(ref owner._active);
            Interlocked.Increment(ref owner._completed);
            if (id == owner.FailId) throw new InvalidOperationException("suppressed test failure");
            _opened = true;
        }

        /// <summary>Checks successful cold connections are held until the whole round settles.</summary>
        public void Dispose()
        {
            if (_opened && owner._roundSize > 0 && Volatile.Read(ref owner._completed) < ((id - 1) / owner._roundSize + 1) * owner._roundSize)
                owner.PrematureDispose = true;
            Interlocked.Increment(ref owner._disposed);
            if (id == owner.FailDisposeId) throw new InvalidOperationException("suppressed close failure");
        }
    }
}

/// <summary>Centralizes small test settings and dedicated-thread runner execution.</summary>
internal static class Helpers
{
    /// <summary>Returns a single-worker sample with no database dependency.</summary>
    /// <returns>A sample descriptor for fake operations.</returns>
    public static Sample Sample() => new(Workload.OpenClose, PoolMode.Legacy, CallerMode.Async, PoolProfile.Default, 1, 1);

    /// <summary>Uses short bounded windows for fake workloads.</summary>
    /// <returns>Common test settings with positive deadlines.</returns>
    public static Settings Settings() => new()
    {
        DurationSeconds = 0.15, IntervalSeconds = 0.05, DrainSeconds = 2,
        CleanupSeconds = 0.5, ConnectTimeoutSeconds = 1, StartupSeconds = 1, DeadlineSeconds = 5
    };

    /// <summary>Samples resources for deterministic metric tests.</summary>
    /// <returns>A zero-valued resource snapshot without live telemetry.</returns>
    public static Resources Resources() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null, null);

    /// <summary>Runs the coordinator outside xUnit's scheduler and enforces a test-level bound.</summary>
    /// <param name="sample">The worker and workload configuration.</param>
    /// <param name="factory">The fake connection source.</param>
    /// <returns>The completed sample result.</returns>
    public static async Task<SampleResult> Run(Sample sample, IConnectionFactory factory)
    {
        using PhysicalCounters counters = new();
        return await OnThread(() => new WorkloadRunner(Settings(), sample, factory, counters).Run(_ => { }))
            .WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>Starts one background thread and propagates its result or exception to the test.</summary>
    /// <param name="action">The coordinator operation to run.</param>
    /// <returns>A task observing the dedicated thread's result.</returns>
    public static Task<T> OnThread<T>(Func<T> action)
    {
        TaskCompletionSource<T> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        new Thread(() =>
        {
            try { source.SetResult(action()); }
            catch (Exception exception) { source.SetException(exception); }
        }) { IsBackground = true }.Start();
        return source.Task;
    }
}

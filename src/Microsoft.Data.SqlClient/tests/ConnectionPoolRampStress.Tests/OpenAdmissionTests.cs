// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Xunit;

namespace ConnectionPoolRampStress.Tests;

/// <summary>Checks the harness-only physical-open cap and its bounded, independent admission lifetime.</summary>
public sealed class OpenAdmissionTests
{
    /// <summary>Rejects caps outside the bounded range and matrix selections that mix incompatible workloads.</summary>
    [Theory]
    [InlineData(-1, "disabled", "open-close")]
    [InlineData(4097, "disabled", "open-close")]
    [InlineData(1, "all", "open-close")]
    [InlineData(1, "legacy", "open-close")]
    [InlineData(1, "v2", "open-close")]
    [InlineData(1, "disabled", "both")]
    [InlineData(1, "disabled", "cold-ramps")]
    public void InvalidCapsAreRejected(int cap, string pool, string work) =>
        Assert.Throws<ArgumentException>(() => new Settings
        {
            MaxConcurrentOpens = cap, Pool = pool, Work = work
        }.Validate());

    /// <summary>Parses and serializes the opt-in cap without changing the uncapped default matrix.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4096)]
    public void CommandLineRoundTripsCap(int cap)
    {
        Assert.Equal(0, new Settings().MaxConcurrentOpens);
        new Settings().Validate();
        bool invoked = false;
        Assert.Equal(0, CommandLine.Run(
            ["--list", "--pool", "disabled", "--workload", "open-close", "--max-concurrent-opens", cap.ToString()],
            (settings, list) =>
            {
                invoked = true;
                Assert.True(list);
                Assert.Equal(cap, settings.MaxConcurrentOpens);
                Assert.Equal(cap, JsonSerializer.Deserialize<Settings>(
                    JsonSerializer.Serialize(settings, Wire.Json), Wire.Json)!.MaxConcurrentOpens);
                return 0;
            }));
        Assert.True(invoked);
    }

    /// <summary>Equal sync and async caps bound actual opens, while uncapped and over-N caps admit all workers.</summary>
    [Theory]
    [InlineData("Async", 0, 6)]
    [InlineData("SyncDedicated", 0, 6)]
    [InlineData("Async", 2, 2)]
    [InlineData("SyncDedicated", 2, 2)]
    [InlineData("SyncThreadpool", 2, 2)]
    [InlineData("Async", 20, 6)]
    [InlineData("SyncDedicated", 20, 6)]
    public async Task WindowStopCancelsQueuedWaitsButDrainsAdmittedOpens(string caller, int cap, int expectedPeak)
    {
        const int concurrency = 6;
        ControlledClock clock = new();
        using ControlledFactory factory = new();
        using PhysicalCounters counters = new();
        Settings settings = Options(cap);
        WorkloadRunner runner = new(settings, Sample(caller, concurrency), factory, counters, clock);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(_ => { }));
        try
        {
            await Until(() => factory.Created == concurrency && factory.Active == expectedPeak &&
                runner.WaitingOpenAdmissions == concurrency - expectedPeak);
            clock.Now = settings.DurationSeconds + 0.1;
            await Until(() => factory.Disposed == concurrency - expectedPeak);
            Assert.Equal(expectedPeak, factory.Entered);
            Assert.Equal(expectedPeak, factory.Active);
            Assert.Equal(0, factory.Canceled);
            factory.Release(expectedPeak);
            SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Outcome.Success, result.Outcome);
            Assert.Empty(result.Failures);
            Assert.Equal(cap, result.ConfiguredMaxConcurrentOpens);
            Assert.Equal(cap == 0 ? 0 : expectedPeak, result.EffectiveMaxConcurrentOpens);
            Assert.Equal(Environment.ProcessorCount, result.ProcessorCount);
            Assert.Equal(expectedPeak, result.ActualPeakActiveOpens);
            Assert.Equal(expectedPeak, factory.Peak);
            Assert.Equal(expectedPeak, result.Drain.Opens.Count);
            Assert.Equal(expectedPeak, result.DriverOpenLatency.Count);
            Assert.Equal(0, result.FailedDriverOpenLatency.Count);
            Assert.Equal(0, result.Measured.Opens.Count);
            Assert.Equal(0, result.Measured.FailedOpens.Count + result.Drain.FailedOpens.Count);
            Assert.Equal(concurrency - expectedPeak, result.CanceledOpenGateWaits);
            Assert.Equal(cap == 0 ? 0 : concurrency, result.OpenGateWait.Count);
            Assert.Equal(cap == 0 ? 0 : expectedPeak, runner.AvailableOpenPermits);
            Assert.Equal(factory.Created, factory.Disposed);
            if (caller == "SyncDedicated") Assert.False(factory.WrongSyncThread);
            string json = JsonSerializer.Serialize(result, Wire.Json);
            Assert.Equal(expectedPeak, JsonSerializer.Deserialize<SampleResult>(json, Wire.Json)!.ActualPeakActiveOpens);
        }
        finally
        {
            runner.RequestStop();
            factory.Release(concurrency);
            await running.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Permit reuse supports repeated opens and includes external gate wait in successful Open latency.</summary>
    [Theory]
    [InlineData("Async")]
    [InlineData("SyncDedicated")]
    public async Task SuccessfulOpensReusePermitsAndIncludeGateWait(string caller)
    {
        ControlledClock clock = new();
        using ControlledFactory factory = new();
        using PhysicalCounters counters = new();
        WorkloadRunner runner = new(Options(2), Sample(caller, 6), factory, counters, clock);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(_ => { }));
        try
        {
            await Until(() => factory.Created == 6 && factory.Active == 2 && runner.WaitingOpenAdmissions == 4);
            clock.Now = 0.25;
            factory.Release(8);
            await Until(() => factory.Entered >= 10 && factory.Active == 2);
            clock.Now = 2.1;
            await Until(() => factory.Disposed == factory.Created - 2);
            factory.Release(2);
            SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Outcome.Success, result.Outcome);
            Assert.Equal(2, factory.Peak);
            Assert.Equal(2, runner.AvailableOpenPermits);
            Assert.True(result.Measured.Opens.Count >= 8);
            Assert.True(result.OpenGateWait.TotalMilliseconds > 0);
            Assert.True(result.Measured.Opens.MaxMilliseconds >= 250);
            Assert.Equal(factory.Created, factory.Disposed);
        }
        finally
        {
            runner.RequestStop();
            factory.Release(6);
            await running.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Separates gate wait from driver time so a successful queued Open must include both in its latency.</summary>
    [Theory]
    [InlineData("Async")]
    [InlineData("SyncDedicated")]
    public async Task QueuedOpenLatencyIncludesGateWait(string caller)
    {
        ControlledClock clock = new();
        using ControlledFactory factory = new() { PauseFirstDispose = true };
        using PhysicalCounters counters = new();
        WorkloadRunner runner = new(Options(1), Sample(caller, 2), factory, counters, clock);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(_ => { }));
        try
        {
            await Until(() => factory.Created == 2 && factory.Active == 1 && runner.WaitingOpenAdmissions == 1);
            clock.Now = 0.25;
            factory.Release(1);
            await Until(() => factory.Entered == 2 && factory.Active == 1);
            clock.Now = 2.1;
            factory.Release(1);
            factory.ResumeDispose();
            SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Outcome.Success, result.Outcome);
            Assert.Equal(1, result.Measured.Opens.Count);
            Assert.Equal(1, result.Drain.Opens.Count);
            Assert.Equal(2100, result.Drain.Opens.TotalMilliseconds, precision: 5);
            Assert.Equal(250, result.OpenGateWait.TotalMilliseconds, precision: 5);
            Assert.Equal(2, result.DriverOpenLatency.Count);
            Assert.Equal(2100, result.DriverOpenLatency.TotalMilliseconds, precision: 5);
            Assert.Equal(1850, result.DriverOpenLatency.MaxMilliseconds, precision: 5);
            Assert.Equal(0, result.FailedDriverOpenLatency.Count);
            Assert.Equal(0, result.CanceledOpenGateWaits);
        }
        finally
        {
            runner.RequestStop();
            factory.Release(2);
            factory.ResumeDispose();
            await running.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>A driver failure releases its permit and remains a failure rather than a canceled gate wait.</summary>
    [Theory]
    [InlineData("Async", false)]
    [InlineData("SyncDedicated", false)]
    [InlineData("Async", true)]
    [InlineData("SyncDedicated", true)]
    public async Task FailedOpenReleasesPermitAndPreservesFailure(string caller, bool cancelFailure)
    {
        using ControlledFactory factory = new() { FailFirst = true, CancelFailure = cancelFailure };
        using PhysicalCounters counters = new();
        WorkloadRunner runner = new(Options(1), Sample(caller, 6), factory, counters);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(_ => { }));
        try
        {
            await Until(() => factory.Created == 6 && factory.Active == 1);
            factory.Release(6);
            SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Outcome.Failed, result.Outcome);
            Assert.Equal(1, runner.AvailableOpenPermits);
            Assert.Equal(1, factory.Peak);
            Assert.Equal(0, factory.Active);
            Assert.Equal(factory.Created, factory.Disposed);
            FailureCount failure = Assert.Single(result.Failures);
            Assert.Equal(cancelFailure ? "OperationCanceledException" : "InvalidOperationException", failure.Failure.ExceptionType);
            Assert.Equal(1, failure.Count);
            Assert.Equal(1, result.Measured.FailedOpens.Count + result.Drain.FailedOpens.Count);
            Assert.Equal(1, result.FailedDriverOpenLatency.Count);
        }
        finally
        {
            runner.RequestStop();
            factory.Release(6);
            await running.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>A full gate times out within ConnectTimeout and drains the already admitted call without canceling it.</summary>
    [Theory]
    [InlineData("Async")]
    [InlineData("SyncDedicated")]
    public async Task GateTimeoutIsAnExplicitFailure(string caller)
    {
        using ControlledFactory factory = new();
        using PhysicalCounters counters = new();
        Settings settings = Options(1) with { DurationSeconds = 10, ConnectTimeoutSeconds = 1 };
        WorkloadRunner runner = new(settings, Sample(caller, 2), factory, counters);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(_ => { }));
        try
        {
            await Until(() => factory.Created == 2 && factory.Active == 1);
            await Until(() => factory.Disposed == 1);
            Assert.Equal(1, factory.Entered);
            Assert.Equal(0, factory.Canceled);
            factory.Release(1);
            SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Outcome.Failed, result.Outcome);
            Assert.Equal("TimeoutException", Assert.Single(result.Failures).Failure.ExceptionType);
            Assert.Equal(1, result.Measured.FailedOpens.Count + result.Drain.FailedOpens.Count);
            Assert.Equal(0, result.FailedDriverOpenLatency.Count);
            Assert.Equal(1, result.DriverOpenLatency.Count);
            Assert.Equal(0, result.CanceledOpenGateWaits);
            Assert.Equal(1, runner.AvailableOpenPermits);
            Assert.Equal(factory.Created, factory.Disposed);
        }
        finally
        {
            runner.RequestStop();
            factory.Release(2);
            await running.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Only admitted async driver calls receive drain cancellation after queued waits have stopped.</summary>
    [Fact]
    public async Task DrainExpiryCancelsDriverCallWithoutLeakingPermit()
    {
        ControlledClock clock = new();
        using ControlledFactory factory = new();
        using PhysicalCounters counters = new();
        WorkloadRunner runner = new(Options(1), Sample("Async", 6), factory, counters, clock);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(_ => { }));
        try
        {
            await Until(() => factory.Active == 1 && runner.WaitingOpenAdmissions == 5);
            clock.Now = 2.1;
            await Until(() => factory.Disposed == 5);
            Assert.Equal(0, factory.Canceled);
            clock.Now = 5.1;
            SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Outcome.Incomplete, result.Outcome);
            Assert.Equal(1, factory.Entered);
            Assert.Equal(1, factory.Canceled);
            Assert.Equal(5, result.CanceledOpenGateWaits);
            Assert.Equal(1, runner.AvailableOpenPermits);
            Assert.Equal("OperationCanceledException", Assert.Single(result.Failures).Failure.ExceptionType);
            Assert.Equal(1, result.FailedDriverOpenLatency.Count);
            Assert.Equal(0, result.DriverOpenLatency.Count);
            Assert.Equal(factory.Created, factory.Disposed);
        }
        finally
        {
            runner.RequestStop();
            factory.Release(6);
            await running.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>External stop cancels queued waits without canceling admitted calls before their drain finishes.</summary>
    [Theory]
    [InlineData("Async")]
    [InlineData("SyncDedicated")]
    public async Task ExternalStopDrainsAdmittedCalls(string caller)
    {
        using ControlledFactory factory = new();
        using PhysicalCounters counters = new();
        WorkloadRunner runner = new(Options(1), Sample(caller, 6), factory, counters);
        Task<SampleResult> running = Helpers.OnThread(() => runner.Run(_ => { }));
        try
        {
            await Until(() => factory.Created == 6 && factory.Active == 1);
            runner.RequestStop();
            await Until(() => factory.Disposed == 5);
            Assert.Equal(0, factory.Canceled);
            factory.Release(1);
            SampleResult result = await running.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(Outcome.Incomplete, result.Outcome);
            Assert.Empty(result.Failures);
            Assert.Equal(5, result.CanceledOpenGateWaits);
            Assert.Equal(1, runner.AvailableOpenPermits);
            Assert.Equal(factory.Created, factory.Disposed);
        }
        finally
        {
            runner.RequestStop();
            factory.Release(6);
            await running.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Provides bounded non-pooled open/close settings without touching runtime ThreadPool configuration.</summary>
    /// <param name="cap">The requested external cap, or zero to disable it.</param>
    /// <returns>Settings suitable for controlled fake opens.</returns>
    private static Settings Options(int cap) => Helpers.Settings() with
    {
        Pool = "disabled", Work = "open-close", MaxConcurrentOpens = cap,
        DurationSeconds = 2, DrainSeconds = 3, CleanupSeconds = 1, ConnectTimeoutSeconds = 5
    };

    /// <summary>Selects caller mode without changing the backend or concurrency control.</summary>
    /// <param name="caller">The caller mode enum name.</param>
    /// <param name="concurrency">The number of persistent workers.</param>
    /// <returns>A non-pooled open/close sample.</returns>
    private static Sample Sample(string caller, int concurrency) => Helpers.Sample() with
    {
        Pool = PoolMode.Disabled, Caller = Enum.Parse<CallerMode>(caller), Concurrency = concurrency
    };

    /// <summary>Waits for an observable state with a real-time bound independent of the controlled sample clock.</summary>
    /// <param name="condition">The state predicate to observe.</param>
    /// <returns>A task that fails if the state does not arrive.</returns>
    private static async Task Until(Func<bool> condition)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, deadline.Token);
    }

    /// <summary>Lets tests end measurement only after the fake operations reach their required state.</summary>
    private sealed class ControlledClock : IClock
    {
        private double _now;
        public double Now { set => Volatile.Write(ref _now, value); }
        public double Seconds => Volatile.Read(ref _now);
    }

    /// <summary>Tracks real invocation concurrency and holds opens until explicitly released or canceled.</summary>
    private sealed class ControlledFactory : IConnectionFactory, IDisposable
    {
        private readonly SemaphoreSlim _release = new(0);
        private readonly ManualResetEventSlim _disposeRelease = new();
        private int _created, _disposed, _entered, _active, _peak, _canceled;
        public bool FailFirst { get; init; }
        public bool CancelFailure { get; init; }
        public bool PauseFirstDispose { get; init; }
        public bool WrongSyncThread;
        public int Created => Volatile.Read(ref _created);
        public int Disposed => Volatile.Read(ref _disposed);
        public int Entered => Volatile.Read(ref _entered);
        public int Active => Volatile.Read(ref _active);
        public int Peak => Volatile.Read(ref _peak);
        public int Canceled => Volatile.Read(ref _canceled);

        /// <summary>Creates one observable connection instance.</summary>
        /// <returns>A fake connection owned by one runner worker.</returns>
        public IConnection Create()
        {
            Interlocked.Increment(ref _created);
            return new Connection(this);
        }

        /// <summary>Rejects pool clearing because these tests only run open/close workloads.</summary>
        public void ClearPool() => throw new InvalidOperationException();

        /// <summary>Allows a bounded number of blocked fake opens to complete.</summary>
        /// <param name="count">The number of completions to permit.</param>
        public void Release(int count) => _release.Release(count);

        /// <summary>Lets the first worker finish disposal after another worker has acquired the released gate permit.</summary>
        public void ResumeDispose() => _disposeRelease.Set();

        /// <summary>Disposes synchronization only after the test has drained its workers.</summary>
        public void Dispose()
        {
            _release.Dispose();
            _disposeRelease.Dispose();
        }

        /// <summary>Tracks sync and async calls independently of the runner's reported counts.</summary>
        private sealed class Connection(ControlledFactory owner) : IConnection
        {
            private int _id;
            /// <summary>Blocks the caller directly and records whether dedicated workers stay on their real threads.</summary>
            public void Open()
            {
                owner.WrongSyncThread |= Thread.CurrentThread.IsThreadPoolThread || Thread.CurrentThread.Name != "ramp-worker";
                int id = Enter();
                try
                {
                    if (!owner._release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                    Finish(id);
                }
                finally { Interlocked.Decrement(ref owner._active); }
            }

            /// <summary>Awaits a release without blocking or wrapping sync work in Task.Run.</summary>
            /// <param name="cancellationToken">The driver's drain cancellation token.</param>
            /// <returns>The held fake connection establishment.</returns>
            public async Task OpenAsync(CancellationToken cancellationToken)
            {
                int id = Enter();
                try
                {
                    if (!await owner._release.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken)) throw new TimeoutException();
                    Finish(id);
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Increment(ref owner._canceled);
                    throw;
                }
                finally { Interlocked.Decrement(ref owner._active); }
            }

            /// <summary>Updates active and peak invocation counts atomically.</summary>
            /// <returns>The physical invocation sequence number.</returns>
            private int Enter()
            {
                int active = Interlocked.Increment(ref owner._active);
                int previous;
                do
                {
                    previous = Volatile.Read(ref owner._peak);
                    if (active <= previous) break;
                } while (Interlocked.CompareExchange(ref owner._peak, active, previous) != previous);
                return _id = Interlocked.Increment(ref owner._entered);
            }

            /// <summary>Injects a single driver failure after its blocking operation has completed.</summary>
            /// <param name="id">The physical invocation sequence number.</param>
            private void Finish(int id)
            {
                if (!owner.FailFirst || id != 1) return;
                if (owner.CancelFailure) throw new OperationCanceledException();
                throw new InvalidOperationException();
            }

            /// <summary>Counts disposal of both admitted calls and connections canceled while queued.</summary>
            public void Dispose()
            {
                if (owner.PauseFirstDispose && _id == 1 && !owner._disposeRelease.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException();
                Interlocked.Increment(ref owner._disposed);
            }
        }
    }
}

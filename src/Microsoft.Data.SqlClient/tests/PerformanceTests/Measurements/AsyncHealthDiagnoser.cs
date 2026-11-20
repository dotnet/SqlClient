// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.EventProcessors;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Validators;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>Opts a benchmark into startup/warmup and extra-iteration async-health observation.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class AsyncHealthDiagnoserAttribute : Attribute;

/// <summary>
/// Observes BDN startup/warmup and its extra iteration separately, not timing iterations.
/// Startup includes fixture initialization; extra-iteration observation excludes fixture work.
/// </summary>
public sealed class AsyncHealthDiagnoser(
    bool poolV2, bool managedSni, bool optimizedAsync,
    string? outputDirectory = null, RunnerJob? job = null) : EventProcessor, IInProcessDiagnoser
{
    private static readonly ConcurrentDictionary<string, AsyncHealthDiagnoser> s_active = new();
    private static AsyncHealthOperationRecorder? s_operations;
    private static int s_benchmarksStarted;
    private readonly string _handlerId = Guid.NewGuid().ToString("N");
    private readonly List<AsyncHealthSummary> _summaries = [];
    private readonly string _outputDirectory =
        outputDirectory ?? Path.Combine("BenchmarkDotNet.Artifacts", "async-health");
    private readonly RunnerJob _job = job ?? new RunnerJob();
    private BenchmarkCase? _case;
    private ThreadPoolHealthObserver? _observer;
    private AsyncHealthOperationRecorder? _operations;
    private string? _caseId;
    private bool _windowCompleted;
    private bool _startupCompleted;
    private bool _firstBenchmarkInProcess;
    private string? _phase;

    internal bool HasFailed { get; private set; }
    internal bool IsObserving => _observer != null && _phase == "ExtraIteration";
    internal static AsyncHealthOperationRecorder.Invocation? BeginInvocation(int workers) =>
        Volatile.Read(ref s_operations)?.BeginInvocation(workers);
    public IEnumerable<string> Ids => [nameof(AsyncHealthDiagnoser)];
    public IEnumerable<IExporter> Exporters => [];
    public IEnumerable<IAnalyser> Analysers => [];

    internal static bool IsMarked(BenchmarkCase benchmark) =>
        Attribute.IsDefined(benchmark.Descriptor.WorkloadMethod,
            typeof(AsyncHealthDiagnoserAttribute), true) ||
        Attribute.IsDefined(benchmark.Descriptor.Type, typeof(AsyncHealthDiagnoserAttribute), true);

    public RunMode GetRunMode(BenchmarkCase benchmarkCase) =>
        IsMarked(benchmarkCase) ? RunMode.ExtraIteration : RunMode.None;

    public override void OnStartRunBenchmark(BenchmarkCase benchmarkCase)
    {
        _firstBenchmarkInProcess = Interlocked.Increment(ref s_benchmarksStarted) == 1;
        if (HasFailed)
        {
            throw new InvalidOperationException("Previous async-health benchmark failed.");
        }
        if (!IsMarked(benchmarkCase))
        {
            return;
        }
        if (_case != null)
        {
            throw new InvalidOperationException("Async-health benchmark already active.");
        }

        _case = benchmarkCase;
        _caseId = Guid.NewGuid().ToString("N");
        _windowCompleted = false;
        _startupCompleted = false;
        _operations = null;
        _phase = null;
        if (poolV2 && !Equals(typeof(SqlConnection).Assembly
            .GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches")
            ?.GetProperty("UseConnectionPoolV2",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null), true))
        {
            HasFailed = true;
            FinishRun();
            throw new InvalidOperationException(
                "The driver cannot provide the requested V2 benchmark path.");
        }
        if (!s_active.TryAdd(_handlerId, this))
        {
            throw new InvalidOperationException("Async-health handler already registered.");
        }
    }

    public override void OnEndRunBenchmark(BenchmarkCase benchmarkCase, BenchmarkReport report)
    {
        if (_case == null)
        {
            return;
        }
        HasFailed |= !report.Success;
        FinishRun();
        if (HasFailed)
        {
            throw new InvalidOperationException("Async-health benchmark failed; evidence retained.");
        }
    }

    internal void FinishRun()
    {
        if (_case == null)
        {
            return;
        }
        try
        {
            if (_observer != null)
            {
                CompleteWindow(false);
            }
            if (!_windowCompleted || !_startupCompleted || HasFailed)
            {
                HasFailed = true;
                Save(null, _operations?.Snapshot(), "Failed",
                    "Benchmark failed or produced no diagnostic iteration.");
            }
        }
        finally
        {
            s_active.TryRemove(_handlerId, out _);
            _case = null;
        }
    }

    public ValueTask HandleAsync(
        HostSignal signal, DiagnoserActionParameters parameters, CancellationToken cancellationToken)
    {
        if (signal == HostSignal.AfterAll && _observer != null)
        {
            CompleteWindow(false);
        }
        return ValueTask.CompletedTask;
    }

    public InProcessDiagnoserHandlerData GetHandlerData(BenchmarkCase benchmarkCase) =>
        IsMarked(benchmarkCase) ? new(typeof(Handler), _handlerId) : default;

    public void DeserializeResults(BenchmarkCase benchmarkCase, string serializedResults) { }
    public IEnumerable<Metric> ProcessResults(DiagnoserResults results) => [];

    public void DisplayResults(ILogger logger)
    {
        foreach (AsyncHealthSummary summary in _summaries)
        {
            logger.WriteLine(LogKind.Info,
                $"Async health: dispatch p99={summary.DispatchP99Ms:F2} ms; " +
                $"probes={summary.Samples}; missing={summary.Missing}; " +
                $"dispatch evidence={summary.DispatchEvidence}; " +
                $"peak process threads={summary.ProcessThreadsPeak?.ToString() ?? "N/A"}");
        }
        logger.WriteLine(LogKind.Info, "Raw async-health evidence: " + _outputDirectory);
    }

    public async IAsyncEnumerable<ValidationError> ValidateAsync(
        ValidationParameters validationParameters)
    {
        await Task.CompletedTask;
        foreach (BenchmarkCase benchmark in validationParameters.Benchmarks.Where(IsMarked))
        {
            if (benchmark.Job.Infrastructure.Toolchain is not InProcessEmitToolchain ||
                benchmark.Job.Run.LaunchCount != 1 || benchmark.Job.Run.InvocationCount <= 0)
            {
                yield return new ValidationError(true,
                    "Async health requires InProcessEmitToolchain, one launch and an explicit " +
                    "positive invocation count.", benchmark);
            }
        }
    }

    internal void StartWindow(string phase)
    {
        if (_case == null || _observer != null || _windowCompleted ||
            (phase == "Startup" && _startupCompleted))
        {
            throw new InvalidOperationException("Unexpected async-health iteration start.");
        }
        _phase = phase;
        _observer = new ThreadPoolHealthObserver();
        _observer.Start();
        _operations = new AsyncHealthOperationRecorder();
        if (Interlocked.CompareExchange(ref s_operations, _operations, null) != null)
        {
            throw new InvalidOperationException("An operation recorder is already active.");
        }
    }

    internal void CompleteWindow(bool completed)
    {
        using ThreadPoolHealthObserver observer = _observer ??
            throw new InvalidOperationException("Missing async-health observation.");
        _observer = null;
        Interlocked.CompareExchange(ref s_operations, null, _operations);
        try
        {
            AsyncHealthWindow window = observer.Complete();
            AsyncHealthOperationSnapshot? operations = _operations?.Snapshot();
            if (_phase == "Startup") _startupCompleted = completed;
            else _windowCompleted = completed;
            HasFailed |= !completed || window.ObserverError != null;
            if (operations?.Invocations > 0)
            {
                HasFailed |= !operations.IsComplete || operations.HasFailures;
            }
            Save(window, operations, HasFailed ? "Failed" : "Completed", window.ObserverError);
            // Continuing with queued callbacks would contaminate later benchmark cases.
            if (window.Probes.Outstanding != 0)
            {
                HasFailed = true;
            }
        }
        catch
        {
            HasFailed = true;
            throw;
        }
    }

    private void Save(AsyncHealthWindow? measurement, AsyncHealthOperationSnapshot? operations,
        string status, string? error)
    {
        BenchmarkCase benchmark = _case ??
            throw new InvalidOperationException("Missing async-health benchmark context.");
        AsyncHealthSummary? summary = measurement == null ? null :
            AsyncHealthSummary.Create(measurement, operations);
        if (summary != null)
        {
            _summaries.Add(summary);
        }
        ThreadPool.GetMinThreads(out int minimumWorkers, out _);
        ThreadPool.GetMaxThreads(out int maximumWorkers, out _);
        BurstWorkload? workload = _job is OpenAsyncBurstRunnerJob burst ?
            burst.GetWorkload(benchmark.Descriptor.WorkloadMethod.Name,
                benchmark.Parameters.Items.Single(p => p.Name == "State").Value?.ToString() ??
                    throw new InvalidOperationException("Missing burst pool state.")) : null;

        var result = new
        {
            SchemaVersion = 3, Kind = "BenchmarkAsyncHealth", Status = status, Error = error,
            CaseId = _caseId, Phase = _phase,
            Benchmark = benchmark.Descriptor.Type.FullName + "." + benchmark.Descriptor.WorkloadMethod.Name,
            Parameters = benchmark.Parameters.DisplayInfo,
            ConfiguredInvocationsPerIteration = benchmark.Job.Run.InvocationCount,
            benchmark.Descriptor.OperationsPerInvoke,
            CompletedInvocations = operations?.CompletedInvocations,
            Configuration = new
            {
                UseConnectionPoolV2 = poolV2, UseManagedSni = managedSni,
                UseOptimizedAsync = optimizedAsync, Runtime = Environment.Version.ToString(),
                OS = Environment.OSVersion.Platform.ToString(), Environment.ProcessorCount,
                MinimumWorkerThreads = minimumWorkers, MaximumWorkerThreads = maximumWorkers,
                Environment.ProcessId, DriverVersion = typeof(SqlConnection).Assembly.FullName,
                DriverSha256 = Convert.ToHexString(SHA256.HashData(
                    File.ReadAllBytes(typeof(SqlConnection).Assembly.Location))),
                DiagnosticRunMode = "StartupAndExtraIteration",
                FirstBenchmarkInProcess = _firstBenchmarkInProcess,
                RequestedProcessIsolation = _job.ProcessIsolation,
                EffectiveProcessIsolation = _job.ProcessIsolation,
                RequestedAdditionalRttMilliseconds = _job.AdditionalRttMilliseconds,
                AppliedAdditionalRttMilliseconds = BenchmarkNetworkScope.ReadAppliedRtt(_job),
                SamplingProfile = (_job as OpenAsyncBurstRunnerJob)?.SamplingProfile ?? "Default",
                ConcurrentWorkers = workload == null ? (int?)null :
                    OpenAsyncBurstRunnerJob.ConcurrentWorkers,
                WorkloadWaves = workload?.Waves,
                SteadyOperationsPerWorker = workload?.OperationsPerWorker,
                ThreadPoolHealthObserver.ProbeIntervalMilliseconds,
                ThreadPoolHealthObserver.MaximumOutstandingProbes,
                AsyncHealthSummary.MinimumP99Samples
            },
            Summary = summary, Measurement = measurement, Operations = operations
        };
        Directory.CreateDirectory(_outputDirectory);
        // A later execution failure must not overwrite the already captured failed window.
        string suffix = measurement == null ? "execution-" + status : _phase + "-" + status;
        string path = Path.Combine(_outputDirectory, $"{_caseId}-{suffix}.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(result,
            new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    public sealed class Handler : IInProcessDiagnoserHandler
    {
        private AsyncHealthDiagnoser? _owner;

        public void Initialize(string? serializedConfig)
        {
            if (serializedConfig == null || !s_active.TryGetValue(serializedConfig, out _owner))
            {
                throw new InvalidOperationException("Missing async-health handler.");
            }
        }

        public ValueTask HandleAsync(
            BenchmarkSignal signal, InProcessDiagnoserActionArgs args,
            CancellationToken cancellationToken)
        {
            AsyncHealthDiagnoser owner = _owner ??
                throw new InvalidOperationException("Async-health handler not initialized.");
            if (signal == BenchmarkSignal.BeforeEngine)
            {
                owner.StartWindow("Startup");
            }
            else if (signal == BenchmarkSignal.BeforeActualRun)
            {
                owner.CompleteWindow(true);
            }
            else if (signal == BenchmarkSignal.BeforeExtraIteration)
            {
                owner.StartWindow("ExtraIteration");
            }
            else if (signal == BenchmarkSignal.AfterExtraIteration)
            {
                owner.CompleteWindow(true);
            }
            return ValueTask.CompletedTask;
        }

        public string SerializeResults() => string.Empty;
    }
}

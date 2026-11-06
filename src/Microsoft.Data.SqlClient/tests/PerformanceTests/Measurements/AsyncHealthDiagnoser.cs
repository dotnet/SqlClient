using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
    public sealed class AsyncHealthDiagnoserAttribute : Attribute { }

    public sealed class AsyncHealthDiagnoser : EventProcessor, IInProcessDiagnoser
    {
        private static readonly ConcurrentDictionary<string, AsyncHealthDiagnoser> s_active = new();
        private readonly string _handlerId = Guid.NewGuid().ToString("N");
        private AsyncHealthCase _context;
        private ThreadPoolHealthObserver _observer;
        private int _window;
        private readonly Config _config;
        private readonly RunnerJob _job;
        private readonly int _diagnosticRepetition;

        internal bool HasFailed { get; private set; }

        public AsyncHealthDiagnoser(Config config, RunnerJob job, int diagnosticRepetition = 1)
        {
            _config = config;
            _job = job;
            _diagnosticRepetition = diagnosticRepetition;
        }

        internal static volatile bool IsObservingWorkload;

        public IEnumerable<string> Ids => new[] { nameof(AsyncHealthDiagnoser) };
        public IEnumerable<IExporter> Exporters => Array.Empty<IExporter>();
        public IEnumerable<IAnalyser> Analysers => Array.Empty<IAnalyser>();

        internal static bool IsMarked(BenchmarkCase benchmarkCase) =>
            Attribute.IsDefined(benchmarkCase.Descriptor.WorkloadMethod, typeof(AsyncHealthDiagnoserAttribute), true) ||
            Attribute.IsDefined(benchmarkCase.Descriptor.Type, typeof(AsyncHealthDiagnoserAttribute), true);

        public RunMode GetRunMode(BenchmarkCase benchmarkCase) =>
            IsMarked(benchmarkCase) ? RunMode.ExtraIteration : RunMode.None;

        public override void OnStartRunBenchmark(BenchmarkCase benchmarkCase)
        {
            if (HasFailed)
            {
                throw new InvalidOperationException("A previous async-health case failed; stopping this process.");
            }

            if (GetRunMode(benchmarkCase) != RunMode.None)
            {
                BeginCase(new AsyncHealthCase(benchmarkCase, _config, _job, _diagnosticRepetition));
            }
        }

        public override void OnEndRunBenchmark(BenchmarkCase benchmarkCase, BenchmarkReport report)
        {
            if (_context == null)
            {
                return;
            }

            if (!report.Success || _context.HarnessFailed || _context.Windows != 1)
            {
                _context.SaveFailure();
                HasFailed = true;
            }

            EndCase();
            if (HasFailed)
            {
                throw new InvalidOperationException("Async-health diagnosis failed; stopping this process.");
            }
        }

        internal void FinishRun()
        {
            if (_context != null)
            {
                _context.SaveFailure();
                HasFailed = true;
                EndCase();
            }
        }

        public ValueTask HandleAsync(HostSignal signal, DiagnoserActionParameters parameters,
            CancellationToken cancellationToken)
        {
            if (signal == HostSignal.AfterAll && _observer != null)
            {
                CompleteWindow(false);
            }

            return ValueTask.CompletedTask;
        }

        public InProcessDiagnoserHandlerData GetHandlerData(BenchmarkCase benchmarkCase) =>
            GetRunMode(benchmarkCase) == RunMode.None ? default : new(typeof(Handler), _handlerId);

        public void DeserializeResults(BenchmarkCase benchmarkCase, string serializedResults) { }

        public IEnumerable<Metric> ProcessResults(DiagnoserResults results) =>
            Array.Empty<Metric>();

        public void DisplayResults(ILogger logger)
        {
            logger.WriteLine(LogKind.Info,
                "Async-health observations: BenchmarkDotNet.Artifacts/async-health");
        }

        public async IAsyncEnumerable<ValidationError> ValidateAsync(ValidationParameters validationParameters)
        {
            await Task.CompletedTask;

            foreach (BenchmarkCase benchmark in validationParameters.Benchmarks)
            {
                if (GetRunMode(benchmark) == RunMode.None)
                {
                    continue;
                }

                _config.AsyncHealth.Validate();
                if (_config.UseNativeMemoryAndETWProfiler || _config.WaitForProfiler ||
                    _job.LaunchCount != 1 || _job.InvocationCount <= 0 ||
                    _job.IterationCount <= 0 || _job.WarmupCount < 0)
                {
                    yield return new ValidationError(true,
                        "Async-health requires one launch, positive invocation/iteration counts, " +
                        "nonnegative warmup, and no profilers.", benchmark);
                }

                if (benchmark.Job.Infrastructure.Toolchain is not InProcessEmitToolchain)
                {
                    yield return new ValidationError(true,
                        "AsyncHealthDiagnoser requires InProcessEmitToolchain.", benchmark);
                }
            }
        }

        internal void BeginCase(AsyncHealthCase context)
        {
            if (_context != null)
            {
                throw new InvalidOperationException("An async-health case is already active.");
            }

            _context = context;
            _window = 0;
            s_active.TryAdd(_handlerId, this);
        }

        internal void EndCase()
        {
            try
            {
                if (_observer != null)
                {
                    CompleteWindow(false);
                }
            }
            finally
            {
                s_active.TryRemove(_handlerId, out _);
                _context = null;
            }
        }

        private void StartWindow()
        {
            if (_observer != null)
            {
                throw new InvalidOperationException("An async-health window is already active.");
            }

            AsyncHealthCase context = _context ??
                throw new InvalidOperationException("Missing async-health case.");
            _observer = new ThreadPoolHealthObserver(context.Options);
            _observer.Start();
            IsObservingWorkload = true;
        }

        private void CompleteWindow(bool completed)
        {
            AsyncHealthCase context = _context ??
                throw new InvalidOperationException("Missing async-health case.");
            using ThreadPoolHealthObserver observer = _observer ??
                throw new InvalidOperationException("Missing async-health window.");
            _observer = null;
            IsObservingWorkload = false;

            try
            {
                AsyncHealthWindow result = observer.Complete();
                context.Save(++_window, context.Invocations, context.OperationsPerInvoke, completed,
                    completed ? null : "BenchmarkExecutionFailed", result);
                context.HarnessFailed |= !completed;

                if (completed && (result.ObserverError != null || result.Probes.Outstanding != 0))
                {
                    context.HarnessFailed = true;
                    throw new InvalidOperationException(
                        "Async-health observation failed or probes did not drain " +
                        "within the grace period. " +
                        "Stopping this process to prevent contamination of later windows.");
                }
            }
            catch
            {
                context.HarnessFailed = true;
                throw;
            }
        }

        public sealed class Handler : IInProcessDiagnoserHandler
        {
            private AsyncHealthDiagnoser _owner;

            public void Initialize(string serializedConfig)
            {
                if (!s_active.TryGetValue(serializedConfig, out _owner))
                {
                    throw new InvalidOperationException("Missing in-process async-health diagnoser.");
                }
            }

            public ValueTask HandleAsync(BenchmarkSignal signal, InProcessDiagnoserActionArgs args,
                CancellationToken cancellationToken)
            {
                if (signal == BenchmarkSignal.BeforeExtraIteration)
                {
                    _owner.StartWindow();
                }
                else if (signal == BenchmarkSignal.AfterExtraIteration)
                {
                    _owner.CompleteWindow(true);
                }

                return ValueTask.CompletedTask;
            }

            public string SerializeResults() => string.Empty;
        }
    }
}
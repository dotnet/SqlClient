using System;
using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Running;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    internal sealed class AsyncHealthCase
    {
        public AsyncHealthOptions Options { get; }
        private readonly BenchmarkCase _benchmark;
        private readonly Config _config;
        private readonly RunnerJob _job;
        private readonly string _id = Guid.NewGuid().ToString("N");
        private readonly string _runId = Environment.GetEnvironmentVariable("PERF_RUN_ID") ?? Guid.NewGuid().ToString("N");
        private readonly int _repetition;
        public int Windows { get; private set; }
        public bool HarnessFailed { get; set; }
        public long Invocations => _job.InvocationCount;
        public long OperationsPerInvoke => _benchmark.Descriptor.OperationsPerInvoke;

        public AsyncHealthCase(BenchmarkCase benchmark, Config config, RunnerJob job)
        {
            _benchmark = benchmark;
            _config = config;
            _job = job;
            Options = config.AsyncHealth;
            string repetition = Environment.GetEnvironmentVariable("PERF_PROCESS_REPETITION") ?? "1";
            if (!int.TryParse(repetition, out _repetition) || _repetition < 1)
                throw new ArgumentException("PERF_PROCESS_REPETITION must be positive.");
        }

        public void Save(int window, long invocations, long operationsPerInvoke, bool completed,
            string error, AsyncHealthWindow measurement)
        {
            Windows++;
            Write(window.ToString(), new
            {
                SchemaVersion = 4, MeasurementMode = "async-health", Status = completed ? "Completed" : "Failed",
                Benchmark = _benchmark.Descriptor.Type.FullName + "." + _benchmark.Descriptor.WorkloadMethod.Name,
                Parameters = _benchmark.Parameters.DisplayInfo,
                Unit = Environment.GetEnvironmentVariable("PERF_BENCHMARK") ?? _benchmark.Descriptor.Type.Name,
                RunId = _runId, ProcessId = Environment.ProcessId, ProcessRepetition = _repetition,
                CaseId = _id, Window = window, Invocations = invocations,
                CompletedInvocations = completed ? (long?)invocations : null, OperationsPerInvoke = operationsPerInvoke,
                Error = error, Policy = "ConfiguredInvocationBatch", Options,
                Configuration = new
                {
                    Instrumentation = nameof(AsyncHealthDiagnoser),
                    DiagnosticRunMode = "ExtraIteration",
                    _config.UseConnectionPoolV2, _config.UseManagedSniOnWindows, _config.UseOptimizedAsyncBehaviour,
                    Runtime = Environment.Version.ToString(), OS = Environment.OSVersion.Platform.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    Runner = JsonSerializer.SerializeToElement(_job, _job.GetType(),
                        new JsonSerializerOptions { IncludeFields = true }),
                    Job = _benchmark.Job.DisplayInfo
                },
                Measurement = measurement
            });
        }

        public void SaveFailure()
        {
            Write("failure", new
            {
                SchemaVersion = 4, MeasurementMode = "async-health", Status = "Failed",
                Benchmark = _benchmark.Descriptor.Type.FullName + "." + _benchmark.Descriptor.WorkloadMethod.Name,
                Parameters = _benchmark.Parameters.DisplayInfo, CaseId = _id, RunId = _runId,
                ProcessId = Environment.ProcessId, ProcessRepetition = _repetition,
                Error = "Benchmark execution failed or produced no diagnostic window; see run log."
            });
        }

        private void Write(string suffix, object result)
        {
            string directory = Path.Combine("BenchmarkDotNet.Artifacts", "async-health");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"async-health-{_id}-{suffix}.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(path + ".tmp", path);
        }
    }
}
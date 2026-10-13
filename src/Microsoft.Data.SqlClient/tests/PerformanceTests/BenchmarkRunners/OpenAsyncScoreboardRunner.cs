// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>Instrumented E1-E6 cohorts hosted by the existing BenchmarkDotNet runner.</summary>
    public class OpenAsyncScoreboardRunner
    {
        internal static OpenAsyncScoreboardCase SelectedCase;
        private Config _config;
        private string _connectionString;
        private OpenAsyncInvocation _result;
        private OpenAsyncDiagnosticResult _diagnostics;
        private int _invocation;
        private int _warmup;
        private int _repetition;
        private string _runId;
        private string _invocationId;
        private string _stage;
        private string _startedUtc;
        private string _completedUtc;

        public IEnumerable<OpenAsyncScoreboardCase> Cases =>
            SelectedCase == null
                ? throw new InvalidOperationException("Select one scoreboard case through PERF_BENCHMARK.")
                : new[] { SelectedCase };

        [ParamsSource(nameof(Cases))]
        public OpenAsyncScoreboardCase Case { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _config = Config.Load();
            if (Case.Instrumentation == "Diagnostic" &&
                Environment.GetEnvironmentVariable("PERF_DIAGNOSTIC_COLLECTOR") != "1")
            {
                throw new InvalidOperationException("Diagnostic cases require the existing perf script's external collectors.");
            }
            string repetition = Environment.GetEnvironmentVariable("PERF_PROCESS_REPETITION");
            _repetition = 1;
            if (repetition != null && (!int.TryParse(repetition, out _repetition) || _repetition <= 0))
            {
                throw new ArgumentException("PERF_PROCESS_REPETITION must be a positive integer.");
            }
            _runId = Environment.GetEnvironmentVariable("PERF_RUN_ID") ?? Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(_runId))
            {
                throw new ArgumentException("PERF_RUN_ID must not be empty.");
            }
            _connectionString = new SqlConnectionStringBuilder(_config.ConnectionString)
            {
                ApplicationName = $"{Case.CaseId}-{Guid.NewGuid():N}",
                Pooling = Case.Scenario != "E3",
                MinPoolSize = 0,
                MaxPoolSize = Math.Max(1, Case.PoolSize),
                ConnectRetryCount = 0,
                ConnectTimeout = 30,
                Enlist = false
            }.ConnectionString;
        }

        [IterationSetup]
        public void PrepareIteration()
        {
            _result = null;
            _diagnostics = null;
            ClearCasePool();
            if (Case.Scenario == "E1" || Case.Scenario == "E2" || Case.Scenario == "E5" || Case.Scenario == "E6")
            {
                var warm = new List<SqlConnection>(Case.PoolSize);
                try
                {
                    for (int i = 0; i < Case.PoolSize; i++)
                    {
                        var connection = new SqlConnection(_connectionString);
                        warm.Add(connection);
                        connection.Open();
                    }
                }
                finally
                {
                    foreach (SqlConnection connection in warm)
                    {
                        connection.Dispose();
                    }
                }
            }
        }

        [Benchmark]
        public async Task OpenCohort()
        {
            _invocationId = Guid.NewGuid().ToString("N");
            _stage = OpenAsyncMeasurementDiagnoser.IsActual ? "WorkloadActual" : "WorkloadWarmup";
            using var watchdog = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            using var collector = Case.Instrumentation == "Diagnostic" ? new OpenAsyncDiagnosticCollector() : null;
            Action<long, Func<(int Outstanding, int Owned)>> onStarted = null;
            Action onCompleted = null;
            if (collector != null)
            {
                onStarted = (epoch, snapshot) =>
                {
                    _startedUtc = DateTimeOffset.UtcNow.ToString("O");
                    WriteMarker(active: true);
                    collector.Start(epoch, snapshot);
                };
                onCompleted = () =>
                {
                    _completedUtc = DateTimeOffset.UtcNow.ToString("O");
                    WriteMarker(active: false);
                    _diagnostics = collector.Complete();
                };
            }
            _result = Case.Scenario == "E6"
                ? await OpenAsyncLoadWorkload.RunAsync(Case, () => new SqlConnection(_connectionString),
                    watchdog.Token, onStarted: onStarted, onCompleted: onCompleted).ConfigureAwait(false)
                : await OpenAsyncWorkload.RunAsync(Case, () => new SqlConnection(_connectionString),
                    watchdog.Token, query: Case.Scenario == "E5" ? ExecuteFirstQuery : null,
                    onStarted: onStarted, onCompleted: onCompleted).ConfigureAwait(false);
        }

        private static async Task ExecuteFirstQuery(DbConnection connection, CancellationToken cancellationToken)
        {
            using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (!(result is int value && value == 1))
            {
                throw new InvalidOperationException("The matched first query did not return the expected result.");
            }
        }

        [IterationCleanup]
        public void CompleteIteration()
        {
            try
            {
                if (_result == null)
                {
                    throw new InvalidOperationException("The scoreboard invocation did not complete.");
                }
                WriteResult();
                _result.EnsureValid(Case);
            }
            finally
            {
                ClearCasePool();
            }
        }

        [GlobalCleanup]
        public void Cleanup() => ClearCasePool();

        private void ClearCasePool()
        {
            if (_connectionString != null && Case.Scenario != "E3")
            {
                using var connection = new SqlConnection(_connectionString);
                SqlConnection.ClearPool(connection);
            }
        }

        private void WriteResult()
        {
            Assembly driver = typeof(SqlConnection).Assembly;
            var settings = new SqlConnectionStringBuilder(_connectionString);
            var report = new
            {
                SchemaVersion = 2,
                WorkloadVersion = 2,
                Case.CaseId,
                Case.Scenario,
                Case.Api,
                Case.Concurrency,
                Case.OperationsPerWorker,
                Case.PoolSize,
                Case.HoldMilliseconds,
                Case.LoadPhaseMilliseconds,
                Case.LoadQueueCapacity,
                Case.LoadBaseRate,
                Case.LoadOverloadRate,
                Case.Instrumentation,
                _config.UseConnectionPoolV2,
                _config.UseOptimizedAsyncBehaviour,
                _config.UseManagedSniOnWindows,
                ConnectTimeoutSeconds = 30,
                ConnectRetryCount = 0,
                Enlist = false,
                Runtime = RuntimeInformation.FrameworkDescription,
                OS = RuntimeInformation.OSDescription,
                DriverVersion = driver.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                DriverAssemblyVersion = driver.GetName().Version?.ToString(),
                Environment.ProcessorCount,
                ProcessId = Environment.ProcessId,
                ProcessRepetition = _repetition,
                RunId = _runId,
                InvocationId = _invocationId,
                StartedUtc = _startedUtc,
                CompletedUtc = _completedUtc,
                Invocation = _stage == "WorkloadActual" ? ++_invocation : ++_warmup,
                WarmupCount = _config.Benchmarks.OpenAsyncScoreboardRunnerConfig.WarmupCount,
                MeasurementStage = _stage,
                IsInstrumented = true,
                ConnectionSettings = new
                {
                    Encrypt = settings.Encrypt.ToString(),
                    settings.TrustServerCertificate,
                    settings.IntegratedSecurity,
                    Authentication = settings.Authentication.ToString(),
                    settings.MultipleActiveResultSets,
                    settings.Pooling,
                    settings.MinPoolSize,
                    settings.MaxPoolSize,
                    settings.PacketSize
                },
                ExternalDiagnosticsRequired = Case.Instrumentation == "Diagnostic"
                    ? new[] { "BlockedThreadStacks", "ServerSaturation" } : Array.Empty<string>(),
                UnavailableDiagnostics = Case.Instrumentation == "Diagnostic"
                    ? new[] { "NativeAllocationAttribution", "CompleteGcPauseTimeline", "DependencyPhaseEvidence", "UninstrumentedThroughput" }
                    : new[] { "BlockedThreadStacks", "UnrelatedWorkLatency", "ServerSaturation", "ContinuousResourceTimeline",
                        "NativeAllocationAttribution", "CompleteGcPauseTimeline", "DependencyPhaseEvidence", "UninstrumentedThroughput" },
                Diagnostics = _diagnostics,
                _result.ElapsedMilliseconds,
                _result.CpuMilliseconds,
                _result.AllocatedBytes,
                _result.Gen0Collections,
                _result.Gen1Collections,
                _result.Gen2Collections,
                _result.ProcessThreadsBefore,
                _result.ProcessThreadsAfter,
                _result.ThreadPoolThreadsBefore,
                _result.ThreadPoolThreadsAfter,
                _result.PeakOutstandingOpens,
                _result.PeakOpenConnections,
                _result.WasCanceled,
                _result.AdmissionMilliseconds,
                _result.Samples,
                _result.QuerySamples,
                _result.Load
            };
            string directory = Path.Combine("BenchmarkDotNet.Artifacts", "results");
            if (_stage != "WorkloadActual")
            {
                directory = Path.Combine(directory, "warmup");
            }
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"openasync-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void WriteMarker(bool active)
        {
            const string directory = "BenchmarkDotNet.Artifacts";
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, "diagnostic-invocation.json");
            string temporary = destination + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new
            {
                Active = active,
                InvocationId = _invocationId,
                Case.CaseId,
                Stage = _stage,
                ProcessId = Environment.ProcessId,
                StartedUtc = _startedUtc,
                CompletedUtc = active ? null : _completedUtc
            }));
            File.Move(temporary, destination, overwrite: true);
        }
    }
}

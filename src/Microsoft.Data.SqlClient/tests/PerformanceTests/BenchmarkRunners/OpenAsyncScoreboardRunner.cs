// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>Instrumented E1-E4 cohorts hosted by the existing BenchmarkDotNet runner.</summary>
    public class OpenAsyncScoreboardRunner
    {
        internal static OpenAsyncScoreboardCase SelectedCase;
        private Config _config;
        private string _connectionString;
        private OpenAsyncInvocation _result;
        private int _invocation;

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
            ClearCasePool();
            if (Case.Scenario == "E1" || Case.Scenario == "E2")
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
            using var watchdog = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            _result = await OpenAsyncWorkload.RunAsync(Case,
                () => new SqlConnection(_connectionString), watchdog.Token).ConfigureAwait(false);
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
                if (OpenAsyncMeasurementDiagnoser.IsActual)
                {
                    WriteResult();
                }
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
            var report = new
            {
                SchemaVersion = 1,
                WorkloadVersion = 1,
                Case.CaseId,
                Case.Scenario,
                Case.Api,
                Case.Concurrency,
                Case.OperationsPerWorker,
                Case.PoolSize,
                Case.HoldMilliseconds,
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
                Invocation = ++_invocation,
                MeasurementStage = "WorkloadActual",
                IsInstrumented = true,
                UnavailableDiagnostics = new[]
                {
                    "BlockedThreadStacks", "UnrelatedWorkLatency", "ServerSaturation",
                    "DependencyPhaseEvidence", "UninstrumentedThroughput"
                },
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
                _result.AdmissionMilliseconds,
                _result.Samples
            };
            string directory = Path.Combine("BenchmarkDotNet.Artifacts", "results");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"openasync-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}

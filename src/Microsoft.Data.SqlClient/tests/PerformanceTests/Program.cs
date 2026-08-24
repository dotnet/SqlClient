// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Running;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class Program
    {
        private readonly Config _config;

        /// <summary>
        /// A single benchmark "unit": the stable selector name used by the perf pipeline, the
        /// accessor for its per-benchmark <see cref="RunnerJob"/> in the runner config, and the
        /// BenchmarkDotNet runner type to execute.
        /// </summary>
        private sealed class BenchmarkUnit
        {
            public string Name { get; }
            public Func<Benchmarks, RunnerJob> Selector { get; }
            public Type RunnerType { get; }

            public BenchmarkUnit(string name, Func<Benchmarks, RunnerJob> selector, Type runnerType)
            {
                Name = name;
                Selector = selector;
                RunnerType = runnerType;
            }
        }

        /// <summary>
        /// The ordered registry of benchmark units.  The <c>Name</c> is the stable identifier the
        /// perf pipeline uses to enumerate and select benchmarks (see <c>PERF_BENCHMARK</c> /
        /// <c>PERF_LIST_BENCHMARKS</c>), so it must not change casually.
        /// </summary>
        private static readonly BenchmarkUnit[] s_units = new[]
        {
            new BenchmarkUnit("SqlConnection", b => b.SqlConnectionRunnerConfig, typeof(SqlConnectionRunner)),
            new BenchmarkUnit("SqlCommand", b => b.SqlCommandRunnerConfig, typeof(SqlCommandRunner)),
            new BenchmarkUnit("SqlBulkCopy", b => b.SqlBulkCopyRunnerConfig, typeof(SqlBulkCopyRunner)),
            new BenchmarkUnit("DataTypeReader", b => b.DataTypeReaderRunnerConfig, typeof(BenchmarkRunners.DataTypeReaderRunner.Plaintext)),
            new BenchmarkUnit("AlwaysEncryptedDataTypeReader", b => b.AlwaysEncryptedDataTypeReaderRunnerConfig, typeof(BenchmarkRunners.DataTypeReaderRunner.AlwaysEncrypted)),
            new BenchmarkUnit("LargeDataRead", b => b.LargeDataReadRunnerConfig, typeof(BenchmarkRunners.LargeDataReadRunner.Plaintext)),
            new BenchmarkUnit("AlwaysEncryptedLargeDataRead", b => b.AlwaysEncryptedLargeDataReadRunnerConfig, typeof(BenchmarkRunners.LargeDataReadRunner.AlwaysEncrypted)),
            new BenchmarkUnit("MarsOverhead", b => b.MarsOverheadRunnerConfig, typeof(MarsOverheadRunner)),
            new BenchmarkUnit("ParallelAsyncConnection", b => b.ParallelAsyncConnectionRunnerConfig, typeof(ParallelAsyncConnectionRunner)),
            new BenchmarkUnit("CancellationTokenReadAsync", b => b.CancellationTokenReadAsyncRunnerConfig, typeof(CancellationTokenReadAsyncRunner)),
            new BenchmarkUnit("SequentialXmlRead", b => b.SequentialXmlReadRunnerConfig, typeof(SequentialXmlReadRunner)),
            new BenchmarkUnit("JsonVsVarcharRead", b => b.JsonVsVarcharReadRunnerConfig, typeof(JsonVsVarcharReadRunner)),
            new BenchmarkUnit("BeginTransaction", b => b.BeginTransactionRunnerConfig, typeof(BeginTransactionRunner)),
            new BenchmarkUnit("ConnectionPoolStress", b => b.ConnectionPoolStressRunnerConfig, typeof(ConnectionPoolStressRunner)),
            new BenchmarkUnit("ConnectionPoolContention", b => b.ConnectionPoolContentionRunnerConfig, typeof(ConnectionPoolContentionRunner)),
            new BenchmarkUnit("ConnectionPoolChurn", b => b.ConnectionPoolChurnRunnerConfig, typeof(ConnectionPoolChurnRunner)),
            new BenchmarkUnit("ConnectionPoolRamp", b => b.ConnectionPoolRampRunnerConfig, typeof(ConnectionPoolRampRunner)),
            new BenchmarkUnit("ConnectionPoolThreadPoolPressure", b => b.ConnectionPoolThreadPoolPressureRunnerConfig, typeof(ConnectionPoolThreadPoolPressureRunner)),
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class,
        /// which loads the benchmark configuration and runs the benchmarks.
        /// </summary>
        public Program()
        {
            _config = Config.Load();
            SetupConfigurations();

            // Interleaving support for the perf pipeline (InternalDriverTools wiki 339 §2.2/§2.3):
            // the harness can enumerate the enabled units and run one at a time, so a candidate and
            // baseline build are measured back-to-back instead of tens of minutes apart.
            //
            //   PERF_LIST_BENCHMARKS set -> print the enabled unit names (one per line) and exit.
            //   PERF_BENCHMARK=<Name>    -> run only that unit (if enabled).
            //   neither                  -> run every enabled unit (default; unchanged behaviour).
            List<BenchmarkUnit> enabled = s_units.Where(IsEnabled).ToList();

            if (Environment.GetEnvironmentVariable("PERF_LIST_BENCHMARKS") != null)
            {
                foreach (BenchmarkUnit unit in enabled)
                {
                    Console.WriteLine(unit.Name);
                }
                return;
            }

            string only = Environment.GetEnvironmentVariable("PERF_BENCHMARK");
            IEnumerable<BenchmarkUnit> toRun = enabled;
            if (!string.IsNullOrWhiteSpace(only))
            {
                toRun = enabled
                    .Where(u => string.Equals(u.Name, only, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (!toRun.Any())
                {
                    Console.Error.WriteLine(
                        $"No enabled benchmark named '{only}'. Enabled units: " +
                        string.Join(", ", enabled.Select(u => u.Name)));
                    Environment.ExitCode = 2;
                    return;
                }
            }

            foreach (BenchmarkUnit unit in toRun)
            {
                BenchmarkRunner.Run(unit.RunnerType, BenchmarkConfig.s_instance(unit.Selector(_config.Benchmarks)));
            }

            // TODOs:
            // Prepared/Regular Parameterized queries
            // Always Encrypted
        }

        /// <summary>
        /// Returns true when the unit has a config entry that is marked Enabled.
        /// </summary>
        private bool IsEnabled(BenchmarkUnit unit)
        {
            RunnerJob job = unit.Selector(_config.Benchmarks);
            return job != null && job.Enabled;
        }

        /// <summary>
        /// Returns true when the harness is driving execution via the perf pipeline's env vars
        /// (<c>PERF_LIST_BENCHMARKS</c> to enumerate units, or <c>PERF_BENCHMARK</c> to run one).
        /// These modes run unattended, so interactive prompts (e.g. WaitForProfiler) must be skipped.
        /// </summary>
        private static bool IsHarnessControlled() =>
            Environment.GetEnvironmentVariable("PERF_LIST_BENCHMARKS") != null ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PERF_BENCHMARK"));

        private void SetupConfigurations()
        {
            // If the config file specifies to use managed SNI on Windows, 
            // enable the appropriate AppContext switch to use the managed SNI implementation.
            if (_config.UseManagedSniOnWindows)
            {
                AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            }

            // Set the UseConnectionPoolV2 AppContext switch from config (true or false).
            // This must be set before any connection is opened because the switch is read
            // and cached when a pool is first created; it cannot be changed later in the
            // process lifetime.
            AppContext.SetSwitch(
                "Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2",
                _config.UseConnectionPoolV2);

            // If the config file specifies to use optimized async behavior, 
            // enable packet multiplexing feature and other optimizations in SqlClient 
            // by setting the appropriate AppContext switches.
            if(_config.UseOptimizedAsyncBehaviour)
            {
                AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseCompatibilityAsyncBehaviour", false);
                AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseCompatibilityProcessSni", false);
            }

            // If the config file specifies to use the NativeMemoryProfiler and ETW profiler,
            // propagate the setting to BenchmarkConfig so it can attach these Windows-only
            // diagnosers when building each benchmark's ManualConfig.
            BenchmarkConfig.UseNativeMemoryAndEtwProfiler = _config.UseNativeMemoryAndETWProfiler;

            // If the config file specifies to wait for a profiler, 
            // display the process ID and wait for user input before starting the benchmarks.
            // Skipped under harness-controlled execution (PERF_LIST_BENCHMARKS / PERF_BENCHMARK):
            // those modes run unattended, so blocking on Console.ReadKey() would hang automation.
            if (_config.WaitForProfiler && !IsHarnessControlled())
            {
                int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                Console.WriteLine();
                Console.WriteLine("===========================================");
                Console.WriteLine($"  Process ID: {pid}");
                Console.WriteLine("===========================================");
                Console.WriteLine();
                Console.WriteLine("Attach your profiler now. Examples:");
                Console.WriteLine($"  dotnet-counters monitor -p {pid}");
                Console.WriteLine($"  dotnet-trace collect -p {pid}");
                Console.WriteLine();
                Console.WriteLine("Press any key to start benchmarks...");
                Console.ReadKey(intercept: true);
                Console.WriteLine();
            }
        }

        /// <summary>
        /// The main entry point for the performance tests program.
        /// </summary>
        public static void Main() => _ = new Program();
    }
}

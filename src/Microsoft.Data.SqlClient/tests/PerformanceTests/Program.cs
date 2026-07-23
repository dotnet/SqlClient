// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using BenchmarkDotNet.Running;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class Program
    {
        private readonly Config _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class, 
        /// which loads the benchmark configuration and runs the benchmarks.
        /// </summary>
        public Program()
        {
            _config = Config.Load();
            SetupConfigurations();
            
            Run_SqlConnectionBenchmark();
            Run_SqlCommandBenchmark();
            Run_SqlBulkCopyBenchmark();
            Run_DataTypeReaderBenchmark();
            Run_DataTypeReaderAsyncBenchmark();
            Run_AsyncLargeDataReadBenchmark();
            Run_MarsOverheadBenchmark();
            Run_ParallelAsyncConnectionBenchmark();
            Run_CancellationTokenReadAsyncBenchmark();
            Run_SequentialXmlReadBenchmark();
            Run_JsonVsVarcharReadBenchmark();
            Run_BeginTransactionBenchmark();
            Run_ConnectionPoolStressBenchmark();
            Run_ConnectionPoolContentionBenchmark();
            Run_ConnectionPoolChurnBenchmark();

            // TODOs:
            // Prepared/Regular Parameterized queries
            // Always Encrypted
        }

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
            if (_config.WaitForProfiler)
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

        private void Run_SqlConnectionBenchmark()
        {
            if (_config.Benchmarks.SqlConnectionRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<SqlConnectionRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.SqlConnectionRunnerConfig));
            }
        }

        private void Run_SqlCommandBenchmark()
        {
            if (_config.Benchmarks.SqlCommandRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<SqlCommandRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.SqlCommandRunnerConfig));
            }
        }

        private void Run_DataTypeReaderBenchmark()
        {
            if (_config.Benchmarks.DataTypeReaderRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<DataTypeReaderRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.DataTypeReaderRunnerConfig));
            }
        }

        private void Run_DataTypeReaderAsyncBenchmark()
        {
            if (_config.Benchmarks.DataTypeReaderAsyncRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<DataTypeReaderAsyncRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.DataTypeReaderAsyncRunnerConfig));
            }
        }

        private void Run_SqlBulkCopyBenchmark()
        {
            if (_config.Benchmarks.SqlBulkCopyRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<SqlBulkCopyRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.SqlBulkCopyRunnerConfig));
            }
        }

        private void Run_AsyncLargeDataReadBenchmark()
        {
            if (_config.Benchmarks.AsyncLargeDataReadRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<AsyncLargeDataReadRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.AsyncLargeDataReadRunnerConfig));
            }
        }

        private void Run_MarsOverheadBenchmark()
        {
            if (_config.Benchmarks.MarsOverheadRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<MarsOverheadRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.MarsOverheadRunnerConfig));
            }
        }

        private void Run_ParallelAsyncConnectionBenchmark()
        {
            if (_config.Benchmarks.ParallelAsyncConnectionRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<ParallelAsyncConnectionRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.ParallelAsyncConnectionRunnerConfig));
            }
        }

        private void Run_CancellationTokenReadAsyncBenchmark()
        {
            if (_config.Benchmarks.CancellationTokenReadAsyncRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<CancellationTokenReadAsyncRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.CancellationTokenReadAsyncRunnerConfig));
            }
        }

        private void Run_SequentialXmlReadBenchmark()
        {
            if (_config.Benchmarks.SequentialXmlReadRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<SequentialXmlReadRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.SequentialXmlReadRunnerConfig));
            }
        }

        private void Run_JsonVsVarcharReadBenchmark()
        {
            if (_config.Benchmarks.JsonVsVarcharReadRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<JsonVsVarcharReadRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.JsonVsVarcharReadRunnerConfig));
            }
        }

        private void Run_BeginTransactionBenchmark()
        {
            if (_config.Benchmarks.BeginTransactionRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<BeginTransactionRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.BeginTransactionRunnerConfig));
            }
        }

        private void Run_ConnectionPoolStressBenchmark()
        {
            if (_config.Benchmarks.ConnectionPoolStressRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<ConnectionPoolStressRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.ConnectionPoolStressRunnerConfig));
            }
        }

        private void Run_ConnectionPoolContentionBenchmark()
        {
            if (_config.Benchmarks.ConnectionPoolContentionRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<ConnectionPoolContentionRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.ConnectionPoolContentionRunnerConfig));
            }
        }

        private void Run_ConnectionPoolChurnBenchmark()
        {
            if (_config.Benchmarks.ConnectionPoolChurnRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<ConnectionPoolChurnRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.ConnectionPoolChurnRunnerConfig));
            }
        }

        /// <summary>
        /// The main entry point for the performance tests program.
        /// </summary>
        public static void Main() => _ = new Program();
    }
}

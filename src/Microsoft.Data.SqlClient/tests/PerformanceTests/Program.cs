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

        public Program()
        {
            // Load config file
            _config = Config.Load();

            // Enable managed SNI on Windows if specified in config.
            if (_config.UseManagedSniOnWindows)
            {
                AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            }

            // Enable optimized async behavior if specified in config.
            if (_config.UseOptimizedAsyncBehaviour)
            {
                AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseCompatibilityAsyncBehaviour", false);
                AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseCompatibilityProcessSni", false);
            }

            // Run benchmarks based on configuration.
            Run_SqlConnectionBenchmark();
            Run_SqlCommandBenchmark();
            Run_SqlBulkCopyBenchmark();
            Run_DataTypeReaderBenchmark();
            Run_DataTypeReaderAsyncBenchmark();
            Run_ConnectionPoolBenchmark();
            Run_PreparedStatementBenchmark();
            Run_ParameterizedQueryBenchmark();
            Run_CommandBehaviorBenchmark();
            Run_StoredProcedureBenchmark();
            Run_LargeDataTypeBenchmark();
            Run_JsonDataTypeBenchmark();
            Run_VectorDataTypeBenchmark();
            Run_TransactionBenchmark();
            Run_BatchApiBenchmark();
            Run_AlwaysEncryptedBenchmark();
            Run_AsyncLargeDataReadBenchmark();
            Run_MarsOverheadBenchmark();
            Run_ParallelAsyncConnectionBenchmark();
            Run_CancellationTokenReadAsyncBenchmark();
            Run_SequentialXmlReadBenchmark();
            Run_JsonVsVarcharReadBenchmark();
            Run_BeginTransactionBenchmark();
            Run_ConnectionPoolStressBenchmark();

            // TODOs:
            // Transactions
            // Insert/Update queries (+CRUD)
            // Prepared/Regular Parameterized queries
            // DataType Reader Max (large size / max columns / million row tables)
            // DataType conversions (Implicit)
            // MARS enabled
            // Always Encrypted
        }

        private void Run_ConnectionPoolBenchmark()
        {
            if (_config.Benchmarks.ConnectionPoolRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<ConnectionPoolRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.ConnectionPoolRunnerConfig));
            }
        }

        private void Run_PreparedStatementBenchmark()
        {
            if (_config.Benchmarks.PreparedStatementRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<PreparedStatementRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.PreparedStatementRunnerConfig));
            }
        }

        private void Run_ParameterizedQueryBenchmark()
        {
            if (_config.Benchmarks.ParameterizedQueryRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<ParameterizedQueryRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.ParameterizedQueryRunnerConfig));
            }
        }

        private void Run_CommandBehaviorBenchmark()
        {
            if (_config.Benchmarks.CommandBehaviorRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<CommandBehaviorRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.CommandBehaviorRunnerConfig));
            }
        }

        private void Run_StoredProcedureBenchmark()
        {
            if (_config.Benchmarks.StoredProcedureRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<StoredProcedureRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.StoredProcedureRunnerConfig));
            }
        }

        private void Run_LargeDataTypeBenchmark()
        {
            if (_config.Benchmarks.LargeDataTypeRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<LargeDataTypeRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.LargeDataTypeRunnerConfig));
            }
        }

        private void Run_JsonDataTypeBenchmark()
        {
            if (_config.Benchmarks.LargeDataTypeRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<JsonDataTypeRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.LargeDataTypeRunnerConfig));
            }
        }

        private void Run_VectorDataTypeBenchmark()
        {
            if (_config.Benchmarks.LargeDataTypeRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<VectorDataTypeRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.LargeDataTypeRunnerConfig));
            }
        }

        private void Run_TransactionBenchmark()
        {
            if (_config.Benchmarks.TransactionRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<TransactionRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.TransactionRunnerConfig));
            }
        }

        private void Run_BatchApiBenchmark()
        {
#if NET
            if (_config.Benchmarks.BatchApiRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<BatchApiRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.BatchApiRunnerConfig));
            }
#endif
        }

        private void Run_AlwaysEncryptedBenchmark()
        {
            if (_config.Benchmarks.AlwaysEncryptedRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<AlwaysEncryptedRunner>(BenchmarkConfig.s_instance(_config.Benchmarks.AlwaysEncryptedRunnerConfig));
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

        public static void Main()
        {
            var config = Config.Load();
            if (config.WaitForProfiler)
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

            // Run the benchmarks.
            new Program();
        }
    }
}

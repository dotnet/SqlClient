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
            if (_config.UseManagedSniOnWindows)
            {
                AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
            }
            Run_SqlConnectionBenchmark();
            Run_SqlCommandBenchmark();
            Run_SqlBulkCopyBenchmark();
            Run_DataTypeReaderBenchmark();
            Run_DataTypeReaderAsyncBenchmark();

            // TODOs:
            // Transactions
            // Insert/Update queries (+CRUD)
            // Prepared/Regular Parameterized queries
            // DataType Reader Max (large size / max columns / million row tables)
            // DataType conversions (Implicit)
            // MARS enabled
            // Always Encrypted
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

        public static void Main()
        {
            // Run the benchmarks.
            new Program();
        }
    }
}

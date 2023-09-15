// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Running;
using Newtonsoft.Json;
using System;
using System.IO;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class Program
    {
        private static Config s_config;

        public static void Main()
        {
            // Load config file
            s_config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("runnerconfig.json"));
            if (s_config.UseManagedSniOnWindows)
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

        private static void Run_SqlConnectionBenchmark()
        {
            if (s_config.Benchmarks.SqlConnectionRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<SqlConnectionRunner>(BenchmarkConfig.s_instance(s_config.Benchmarks.SqlConnectionRunnerConfig));
            }
        }

        private static void Run_SqlCommandBenchmark()
        {
            if (s_config.Benchmarks.SqlCommandRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<SqlCommandRunner>(BenchmarkConfig.s_instance(s_config.Benchmarks.SqlCommandRunnerConfig));
            }
        }

        private static void Run_DataTypeReaderBenchmark()
        {
            if (s_config.Benchmarks.DataTypeReaderRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<DataTypeReaderRunner>(BenchmarkConfig.s_instance(s_config.Benchmarks.DataTypeReaderRunnerConfig));
            }
        }

        private static void Run_DataTypeReaderAsyncBenchmark()
        {
            if (s_config.Benchmarks.DataTypeReaderAsyncRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<DataTypeReaderAsyncRunner>(BenchmarkConfig.s_instance(s_config.Benchmarks.DataTypeReaderAsyncRunnerConfig));
            }
        }

        private static void Run_SqlBulkCopyBenchmark()
        {
            if (s_config.Benchmarks.SqlBulkCopyRunnerConfig?.Enabled == true)
            {
                BenchmarkRunner.Run<SqlBulkCopyRunner>(BenchmarkConfig.s_instance(s_config.Benchmarks.SqlBulkCopyRunnerConfig));
            }
        }
    }
}

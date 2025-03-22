// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// ADO for config.json
    /// </summary>
    public class Config
    {
        public string ConnectionString;
        public bool UseManagedSniOnWindows;
        public Benchmarks Benchmarks;

        /// <summary>
        /// Load the benchmark configuration from a JSON file.
        ///
        /// If the environment variable "RUNNER_CONFIG" is set, it will be used
        /// as the path to the config file.  Otherwise, the file
        /// "runnerconfig.json" in the current working directory will be used.
        /// </summary>
        ///
        /// <returns>
        ///   The Config instance populated from the JSON config file.
        /// </returns>
        ///
        /// <exception cref="InvalidOperationException">
        ///   Thrown if the config file cannot be read or deserialized.
        /// </exception>
        ///
        public static Config Load()
        {
            return Loader.FromJsonFile<Config>(
                "runnerconfig.json", "RUNNER_CONFIG");
        }
    }

    public class Benchmarks
    {
        public RunnerJob SqlConnectionRunnerConfig;
        public RunnerJob SqlCommandRunnerConfig;
        public RunnerJob SqlBulkCopyRunnerConfig;
        public RunnerJob DataTypeReaderRunnerConfig;
        public RunnerJob DataTypeReaderAsyncRunnerConfig;
    }

    public class RunnerJob
    {
        public bool Enabled;
        public int LaunchCount;
        public int IterationCount;
        public int InvocationCount;
        public int WarmupCount;
        public long RowCount;
    }
}

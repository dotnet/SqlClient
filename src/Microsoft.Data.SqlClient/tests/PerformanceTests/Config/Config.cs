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
        public bool UseOptimizedAsyncBehaviour;

        /// <summary>
        /// When true, selects the new channel-based connection pool
        /// (<c>ChannelDbConnectionPool</c>) by enabling the
        /// <c>Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2</c> AppContext switch.
        /// When false (the default), the legacy <c>WaitHandleDbConnectionPool</c> is used.
        ///
        /// This is a process-level setting: the switch is read and cached the first time
        /// a pool is created, so it cannot be toggled per benchmark iteration. To compare
        /// the two implementations, run the benchmark once with this flag false and once
        /// with it true.
        /// </summary>
        public bool UseConnectionPoolV2;

        public bool WaitForProfiler;
        public bool UseNativeMemoryAndETWProfiler;
        public Benchmarks Benchmarks;

        /// <summary>
        /// Load the benchmark configuration from a JSON file.
        ///
        /// If the environment variable "RUNNER_CONFIG" is set, it will be used
        /// as the path to the config file.  Otherwise, the file
        /// "runnerconfig.jsonc" in the current working directory will be used.
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
                "runnerconfig.jsonc", "RUNNER_CONFIG");
        }
    }

    public class Benchmarks
    {
        public RunnerJob SqlConnectionRunnerConfig;
        public RunnerJob SqlCommandRunnerConfig;
        public RunnerJob SqlBulkCopyRunnerConfig;
        public RunnerJob DataTypeReaderRunnerConfig;
        public RunnerJob AlwaysEncryptedDataTypeReaderRunnerConfig;
        public RunnerJob LargeDataReadRunnerConfig;
        public RunnerJob AlwaysEncryptedLargeDataReadRunnerConfig;
        public RunnerJob MarsOverheadRunnerConfig;
        public RunnerJob ParallelAsyncConnectionRunnerConfig;
        public RunnerJob CancellationTokenReadAsyncRunnerConfig;
        public RunnerJob SequentialXmlReadRunnerConfig;
        public RunnerJob JsonVsVarcharReadRunnerConfig;
        public RunnerJob BeginTransactionRunnerConfig;
        public RunnerJob ConnectionPoolStressRunnerConfig;
        public RunnerJob ConnectionPoolContentionRunnerConfig;
        public RunnerJob ConnectionPoolChurnRunnerConfig;
    }

    public class RunnerJob
    {
        public bool Enabled;
        public int LaunchCount;
        public int IterationCount;
        public int InvocationCount;
        public int WarmupCount;
        public long RowCount;

        /// <summary>
        /// Per-benchmark execution timeout, in minutes, for the in-process toolchain.
        ///
        /// BenchmarkDotNet's in-process executor aborts a benchmark case that exceeds its
        /// timeout with "takes too long to run. Prefer to use out-of-process toolchains for
        /// long-running benchmarks." The built-in default is 5 minutes, which is not enough
        /// for benchmarks whose single operation is inherently slow (for example reading a
        /// 20 MB VARBINARY(MAX) value 20 times, plus the additional iterations that
        /// MemoryDiagnoser and ThreadingDiagnoser each add).
        ///
        /// Switching to an out-of-process toolchain - BenchmarkDotNet's own suggestion - is
        /// deliberately NOT an option for this suite: the AppContext switches configured in
        /// <see cref="Program.SetupConfigurations"/> (managed SNI, connection pool V2,
        /// optimized async behaviour) only apply to the process that runs Main. An
        /// out-of-process toolchain spawns a generated host where those switches revert to
        /// their defaults, so the benchmark would silently measure the wrong code paths.
        ///
        /// When 0 or negative, BenchmarkDotNet's default timeout is used.
        /// </summary>
        public int TimeoutMinutes;

        /// <summary>
        /// BenchmarkDotNet <see cref="BenchmarkDotNet.Engines.RunStrategy"/> name to use for
        /// this benchmark, for example "Throughput" or "Monitoring".
        ///
        /// "Throughput" (the default) is designed for fast operations and spends extra
        /// iterations measuring and subtracting harness overhead. For benchmarks whose single
        /// operation takes seconds, that overhead measurement is pure cost and is dwarfed by
        /// the operation itself, so "Monitoring" - which skips it and simply runs the
        /// requested iterations - is both faster and more appropriate.
        ///
        /// When null or empty, "Throughput" is used.
        /// </summary>
        public string RunStrategy;
    }
}

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

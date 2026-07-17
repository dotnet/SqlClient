// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
#if WINDOWS
using BenchmarkDotNet.Diagnostics.Windows.Configs;
#endif

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public static class BenchmarkConfig
    {
        /// <summary>
        /// When set to true, attaches the NativeMemoryProfiler and EtwProfiler diagnosers
        /// so native memory allocations and ETW traces are captured for each benchmark run.
        /// This is only supported on Windows; the value is ignored on other OSes since the
        /// underlying diagnosers are compiled out (see the "WINDOWS" compile constant, which
        /// is only set when building on Windows in the PerformanceTests.csproj file).
        /// </summary>
        public static bool UseNativeMemoryAndEtwProfiler { get; set; }

        public static ManualConfig s_instance(RunnerJob runnerJob)
        {
            ManualConfig config = DefaultConfig.Instance
                .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
                .WithOption(ConfigOptions.DontOverwriteResults, true)
                .AddDiagnoser(MemoryDiagnoser.Default)
                .AddDiagnoser(ThreadingDiagnoser.Default)
                .AddExporter(MarkdownExporter.GitHub)
                .AddJob(
                    Job.MediumRun.WithToolchain(InProcessEmitToolchain.Instance)
                    .WithLaunchCount(runnerJob.LaunchCount)
                    .WithInvocationCount(runnerJob.InvocationCount)
                    .WithIterationCount(runnerJob.IterationCount)
                    .WithWarmupCount(runnerJob.WarmupCount)
                    .WithUnrollFactor(1)
                    .WithStrategy(BenchmarkDotNet.Engines.RunStrategy.Throughput)
                    // Server GC is configured at the host-process level via the
                    // <ServerGarbageCollection> csproj property (COMPlus_gcServer is
                    // startup-only and would be ignored here under InProcessEmitToolchain).
                )
                .WithOptions(ConfigOptions.JoinSummary);

#if WINDOWS
            if (UseNativeMemoryAndEtwProfiler)
            {
                config = config
                    .AddDiagnoser(NativeMemoryProfiler.Default)
                    .AddDiagnoser(new EtwProfiler());
            }
#endif

            return config;
        }
    }
}

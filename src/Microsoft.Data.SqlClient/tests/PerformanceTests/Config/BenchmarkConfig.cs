// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.EventProcessors;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
#if WINDOWS
using BenchmarkDotNet.Diagnostics.Windows;
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

        /// <summary>
        /// Builds the in-process toolchain for a benchmark.
        ///
        /// The in-process toolchain is required (rather than merely convenient): the
        /// AppContext switches applied in <see cref="Program.SetupConfigurations"/> only
        /// affect the process that runs Main, so an out-of-process toolchain would run the
        /// benchmarks with those switches at their default values.
        /// </summary>
        private static IToolchain BuildToolchain() =>
            InProcessEmitToolchain.Default;

        /// <summary>
        /// Resolves the configured <see cref="RunStrategy"/> name, defaulting to
        /// <see cref="RunStrategy.Throughput"/> when unset or unrecognised.
        /// </summary>
        private static RunStrategy ResolveRunStrategy(RunnerJob runnerJob) =>
            !string.IsNullOrWhiteSpace(runnerJob.RunStrategy)
            && Enum.TryParse(runnerJob.RunStrategy, ignoreCase: true, out RunStrategy strategy)
                ? strategy
                : RunStrategy.Throughput;

        public static ManualConfig s_instance(RunnerJob runnerJob, Config suiteConfig = null)
        {
            Job job = Job.MediumRun.WithToolchain(BuildToolchain())
                .WithLaunchCount(runnerJob.LaunchCount)
                .WithInvocationCount(runnerJob.InvocationCount)
                .WithIterationCount(runnerJob.IterationCount)
                .WithWarmupCount(runnerJob.WarmupCount)
                .WithUnrollFactor(1)
                .WithStrategy(ResolveRunStrategy(runnerJob))
                .WithEnvironmentVariable("COMPlus_gcServer", "1");
            var diagnoser = new AsyncHealthDiagnoser(suiteConfig ?? new Config(), runnerJob);
            ManualConfig config = DefaultConfig.Instance
                .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
                .WithOption(ConfigOptions.DontOverwriteResults, true)
                .AddDiagnoser(MemoryDiagnoser.Default)
                .AddDiagnoser(ThreadingDiagnoser.Default)
                .AddDiagnoser(diagnoser)
                .AddEventProcessor(diagnoser)
                .AddEventProcessor(new ExecutionTimeout(runnerJob.TimeoutMinutes))
                .AddExporter(MarkdownExporter.GitHub)
                // Emit the BenchmarkDotNet "full" JSON report (*-report-full.json) so the perf
                // pipeline can translate results into the Kusto performance-results schema.
                .AddExporter(JsonExporter.Full)
                .AddJob(job)
                .WithOptions(ConfigOptions.JoinSummary);

#if WINDOWS
            if (UseNativeMemoryAndEtwProfiler)
            {
                config = config
                    .AddDiagnoser(new NativeMemoryProfiler())
                    .AddDiagnoser(new EtwProfiler());
            }
#endif

            return config;
        }

        internal sealed class ExecutionTimeout : EventProcessor
        {
            private readonly TimeSpan _duration;

            internal CancellationTokenSource Source { get; set; }

            internal ExecutionTimeout(int minutes)
            {
                _duration = TimeSpan.FromMinutes(minutes > 0 ? minutes : 5);
            }

            public override void OnStartRunBenchmark(BenchmarkCase benchmarkCase) =>
                Source?.CancelAfter(_duration);

            public override void OnEndRunBenchmark(BenchmarkCase benchmarkCase, BenchmarkReport report) =>
                Source?.CancelAfter(Timeout.InfiniteTimeSpan);
        }
    }
}

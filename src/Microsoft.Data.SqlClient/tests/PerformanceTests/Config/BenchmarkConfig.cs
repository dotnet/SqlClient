// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
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
        /// Builds the in-process toolchain; ExecutionTimeout enforces RunnerJob.TimeoutMinutes.
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

        public static ManualConfig s_instance(RunnerJob runnerJob, Config? suiteConfig = null)
        {
            ManualConfig config = DefaultConfig.Instance
                .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
                .WithOption(ConfigOptions.DontOverwriteResults, true)
                .AddDiagnoser(MemoryDiagnoser.Default)
                .AddDiagnoser(ThreadingDiagnoser.Default)
                .AddEventProcessor(new ExecutionTimeout(runnerJob.TimeoutMinutes))
                .AddExporter(MarkdownExporter.GitHub)
                // Emit the BenchmarkDotNet "full" JSON report (*-report-full.json) so the perf
                // pipeline can translate results into the Kusto performance-results schema.
                .AddExporter(JsonExporter.Full)
                .AddJob(
                    Job.MediumRun.WithToolchain(BuildToolchain())
                    .WithLaunchCount(runnerJob.LaunchCount)
                    .WithInvocationCount(runnerJob.InvocationCount)
                    .WithIterationCount(runnerJob.IterationCount)
                    .WithWarmupCount(runnerJob.WarmupCount)
                    // SQL jobs already configure warmup. Preview auto-tiering adds about 90
                    // full workload calls (and repeated fixtures) beyond that configuration.
                    // Skip that extra preparation, not runtime tiered compilation.
                    .WithJitTieringMode(JitTieringMode.Skip)
                    .WithUnrollFactor(1)
                    .WithStrategy(ResolveRunStrategy(runnerJob))
                    .WithEnvironmentVariable("COMPlus_gcServer", "1")
                )
                .WithOptions(ConfigOptions.JoinSummary);

            if (suiteConfig?.EnableAsyncHealth == true)
            {
                if (suiteConfig.UseNativeMemoryAndETWProfiler || suiteConfig.WaitForProfiler)
                {
                    throw new InvalidOperationException(
                        "Async health cannot be combined with interactive or native profilers.");
                }
                var diagnoser = new AsyncHealthDiagnoser(suiteConfig.UseConnectionPoolV2,
                    !OperatingSystem.IsWindows() || suiteConfig.UseManagedSniOnWindows,
                    suiteConfig.UseOptimizedAsyncBehaviour);
                config.AddDiagnoser(diagnoser).AddEventProcessor(diagnoser);
            }

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

        /// <summary>Retains per-case timeout enforcement with BDN's cancellation-based executor.</summary>
        internal sealed class ExecutionTimeout(int minutes) : EventProcessor
        {
            internal CancellationTokenSource? Source { get; set; }

            public override void OnStartRunBenchmark(BenchmarkCase benchmarkCase) =>
                Source?.CancelAfter(TimeSpan.FromMinutes(minutes > 0 ? minutes : 5));

            public override void OnEndRunBenchmark(BenchmarkCase benchmarkCase, BenchmarkReport report) =>
                Source?.CancelAfter(Timeout.InfiniteTimeSpan);
        }
    }
}

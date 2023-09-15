// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public static class BenchmarkConfig
    {
        public static ManualConfig s_instance(RunnerJob runnerJob) => 
            DefaultConfig.Instance
            .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
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
            );
    }
}

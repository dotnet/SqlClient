// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Microsoft.Data.SqlClient.PerformanceTests;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Measurements;

/// <summary>Serializes in-process BDN execution, which adjusts process state and uses static fixtures.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AsyncHealthBenchmarkCollection
{
    public const string Name = "AsyncHealthBenchmarks";
}

/// <summary>Exercises the real BDN extra-iteration lifecycle, not just mocked callbacks.</summary>
[Collection(AsyncHealthBenchmarkCollection.Name)]
public class AsyncHealthDiagnoserTests
{
    /// <summary>Startup and extra work are captured separately without recording timing invocations.</summary>
    [Fact]
    public void Run_MarkedBenchmark_SeparatesStartupAndExtraFromTiming()
    {
        // Arrange
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var diagnoser = new AsyncHealthDiagnoser(false, true, true, directory);
        AsyncHealthBenchmarkFixture.Observer = diagnoser;
        AsyncHealthBenchmarkFixture.Observed = AsyncHealthBenchmarkFixture.Unobserved = 0;
        AsyncHealthBenchmarkFixture.Setups = AsyncHealthBenchmarkFixture.Cleanups = 0;
        AsyncHealthBenchmarkFixture.StartupObserved = AsyncHealthBenchmarkFixture.TimingInvocations = 0;
        try
        {
            // Act
            Run(nameof(AsyncHealthBenchmarkFixture.AsyncWait), directory, diagnoser);

            // Assert
            Assert.False(diagnoser.HasFailed);
            Assert.Equal(1, AsyncHealthBenchmarkFixture.Observed);
            Assert.True(AsyncHealthBenchmarkFixture.Unobserved > 0);
            Assert.True(AsyncHealthBenchmarkFixture.StartupObserved > 0);
            Assert.True(AsyncHealthBenchmarkFixture.TimingInvocations > 0);
            Assert.Equal(AsyncHealthBenchmarkFixture.Setups, AsyncHealthBenchmarkFixture.Cleanups);
            using JsonDocument startup = JsonDocument.Parse(File.ReadAllText(
                Assert.Single(Directory.GetFiles(directory, "*-Startup-Completed.json"))));
            Assert.Equal("Startup", startup.RootElement.GetProperty("Phase").GetString());
            Assert.True(startup.RootElement.GetProperty("Operations")
                .GetProperty("Completed").GetInt64() > 0);
            using JsonDocument result = JsonDocument.Parse(File.ReadAllText(
                Assert.Single(Directory.GetFiles(directory, "*-ExtraIteration-Completed.json"))));
            Assert.Equal("Completed", result.RootElement.GetProperty("Status").GetString());
            Assert.Equal(1, result.RootElement.GetProperty("CompletedInvocations").GetInt64());
            Assert.Equal(1, result.RootElement.GetProperty("Operations")
                .GetProperty("Completed").GetInt64());
            Assert.Equal("Insufficient", result.RootElement.GetProperty("Summary")
                .GetProperty("Health").GetString());
            Assert.NotEmpty(result.RootElement.GetProperty("Measurement")
                .GetProperty("Probes").GetProperty("Samples").EnumerateArray());
        }
        finally
        {
            // Cleanup
            AsyncHealthBenchmarkFixture.Observer = null;
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A diagnostic-only failure must leave explicit failed evidence and stop reuse.</summary>
    [Fact]
    public void Run_FailedDiagnosticIteration_PreservesFailure()
    {
        // Arrange
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var diagnoser = new AsyncHealthDiagnoser(false, true, true, directory);
        AsyncHealthBenchmarkFixture.Observer = diagnoser;
        try
        {
            // Act
            Exception? error = Record.Exception(() =>
                Run(nameof(AsyncHealthBenchmarkFixture.DiagnosticFailure), directory, diagnoser));

            // Assert
            Assert.True(diagnoser.HasFailed);
            Assert.NotNull(error);
            Assert.NotEmpty(Directory.GetFiles(directory, "*-Failed.json"));
            string windowPath = Assert.Single(Directory.GetFiles(directory, "*-Failed.json"),
                path => !path.EndsWith("-execution-Failed.json"));
            using JsonDocument window = JsonDocument.Parse(File.ReadAllText(windowPath));
            Assert.Equal(JsonValueKind.Object,
                window.RootElement.GetProperty("Measurement").ValueKind);
            Assert.Equal(1, window.RootElement.GetProperty("Operations")
                .GetProperty("Failed").GetInt64());
            Assert.False(diagnoser.IsObserving);
        }
        finally
        {
            // Cleanup
            AsyncHealthBenchmarkFixture.Observer = null;
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Unmarked benchmarks retain their normal BDN lifecycle without health artifacts.</summary>
    [Fact]
    public void Run_UnmarkedBenchmark_DoesNotObserve()
    {
        // Arrange
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var diagnoser = new AsyncHealthDiagnoser(false, true, true, directory);
        AsyncHealthBenchmarkFixture.Observer = diagnoser;
        try
        {
            // Act
            Run(nameof(AsyncHealthBenchmarkFixture.Unmarked), directory, diagnoser);

            // Assert
            Assert.False(diagnoser.HasFailed);
            Assert.Empty(Directory.GetFiles(directory, "*-Completed.json"));
            Assert.Empty(Directory.GetFiles(directory, "*-Failed.json"));
        }
        finally
        {
            // Cleanup
            AsyncHealthBenchmarkFixture.Observer = null;
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Runs a single fixture case with a bounded real in-process BDN executor.</summary>
    /// <param name="method">Fixture method to execute.</param>
    /// <param name="directory">Temporary artifacts directory.</param>
    /// <param name="diagnoser">The diagnoser whose emitted handler is exercised.</param>
    private static void Run(string method, string directory, AsyncHealthDiagnoser diagnoser)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithArtifactsPath(directory)
            .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
            .AddJob(Job.Dry.WithToolchain(InProcessEmitToolchain.Default)
                .WithLaunchCount(1).WithWarmupCount(1).WithIterationCount(1)
                .WithInvocationCount(1).WithUnrollFactor(1).WithStrategy(RunStrategy.Monitoring))
            .AddDiagnoser(MemoryDiagnoser.Default).AddDiagnoser(ThreadingDiagnoser.Default)
            .AddDiagnoser(diagnoser).AddEventProcessor(diagnoser);
        using BenchmarkRunInfo all = BenchmarkConverter.TypeToBenchmarks(
            typeof(AsyncHealthBenchmarkFixture), config);
        using var selected = new BenchmarkRunInfo(
            all.BenchmarksCases.Where(b => b.Descriptor.WorkloadMethod.Name == method).ToArray(),
            all.Type, all.Config, all.CompositeInProcessDiagnoser);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var summaries = BenchmarkRunner.RunAsync([selected], timeout.Token).AsTask()
                .WaitAsync(timeout.Token).GetAwaiter().GetResult();
            Assert.All(summaries, summary =>
            {
                Assert.False(summary.HasCriticalValidationErrors);
                Assert.NotEmpty(summary.Reports);
                Assert.All(summary.Reports, report => Assert.True(report.Success));
            });
        }
        finally
        {
            diagnoser.FinishRun();
        }
    }
}
#endif

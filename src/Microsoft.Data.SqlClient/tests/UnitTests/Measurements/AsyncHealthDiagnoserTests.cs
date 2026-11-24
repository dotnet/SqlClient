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

/// <summary>Serializes BDN tests that share process state and static fixtures.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AsyncHealthBenchmarkCollection
{
    public const string Name = "AsyncHealthBenchmarks";
}

/// <summary>Exercises the real BDN extra-iteration lifecycle, not just mocked callbacks.</summary>
[Collection(AsyncHealthBenchmarkCollection.Name)]
public class AsyncHealthDiagnoserTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    /// <summary>Each test owns its artifacts and resets the shared fixture counters.</summary>
    public AsyncHealthDiagnoserTests()
    {
        Directory.CreateDirectory(_directory);
        AsyncHealthBenchmarkFixture.Observed = AsyncHealthBenchmarkFixture.Unobserved = 0;
        AsyncHealthBenchmarkFixture.Setups = AsyncHealthBenchmarkFixture.Cleanups = 0;
        AsyncHealthBenchmarkFixture.StartupObserved = AsyncHealthBenchmarkFixture.TimingInvocations = 0;
    }

    /// <summary>Release fixture state and artifacts after each test, including failures.</summary>
    public void Dispose()
    {
        AsyncHealthBenchmarkFixture.Observer = null;
        Directory.Delete(_directory, recursive: true);
    }

    /// <summary>Observe startup and extra work separately, without recording timing calls.</summary>
    [Fact]
    public void Run_MarkedBenchmark_SeparatesStartupAndExtraFromTiming()
    {
        // Arrange
        using BenchmarkRunInfo suite = Discover(typeof(AsyncHealthBenchmarkFixture));
        var diagnoser = Assert.Single(suite.Config.GetDiagnosers().OfType<AsyncHealthDiagnoser>());

        // Act
        Run(suite, nameof(AsyncHealthBenchmarkFixture.AsyncWait), diagnoser);

        // Assert
        Assert.False(diagnoser.HasFailed);
        Assert.Equal(1, AsyncHealthBenchmarkFixture.Observed);
        Assert.True(AsyncHealthBenchmarkFixture.Unobserved > 0);
        Assert.True(AsyncHealthBenchmarkFixture.StartupObserved > 0);
        Assert.True(AsyncHealthBenchmarkFixture.TimingInvocations > 0);
        Assert.Equal(AsyncHealthBenchmarkFixture.Setups, AsyncHealthBenchmarkFixture.Cleanups);
        using JsonDocument startup = ReadWindow("*-Startup-Completed.json");
        Assert.Equal("Startup", startup.RootElement.GetProperty("Phase").GetString());
        Assert.True(startup.RootElement.GetProperty("Operations")
            .GetProperty("Completed").GetInt64() > 0);
        using JsonDocument result = ReadWindow("*-ExtraIteration-Completed.json");
        Assert.Equal(3, result.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("Completed", result.RootElement.GetProperty("Status").GetString());
        Assert.Equal(1, result.RootElement.GetProperty("CompletedInvocations").GetInt64());
        Assert.Equal(1, result.RootElement.GetProperty("Operations")
            .GetProperty("Completed").GetInt64());
        JsonElement summary = result.RootElement.GetProperty("Summary");
        Assert.Equal("Sparse", summary.GetProperty("DispatchEvidence").GetString());
        foreach (string removed in new[]
        {
            "Health", "ProducerP99Ms", "FirstWorkerOfferToOpenP99Ms", "SuccessfulOpenP99Ms",
            "FailedOpenP99Ms", "CancelledOperationP99Ms", "CompletedOperationsPerSecond"
        })
        {
            Assert.False(summary.TryGetProperty(removed, out _));
        }
        JsonElement observation = Assert.Single(result.RootElement.GetProperty("Operations")
            .GetProperty("Observations").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, observation.GetProperty("OpenEndMs").ValueKind);
        Assert.Equal("Completed", observation.GetProperty("Outcome").GetString());
        Assert.Equal(0, result.RootElement.GetProperty("Measurement")
            .GetProperty("ResourceSamplesDropped").GetInt64());
        Assert.NotEmpty(result.RootElement.GetProperty("Measurement")
            .GetProperty("Resources").EnumerateArray());
        Assert.NotEmpty(result.RootElement.GetProperty("Measurement")
            .GetProperty("Probes").GetProperty("Samples").EnumerateArray());
    }

    /// <summary>A diagnostic failure leaves explicit evidence and stops reuse.</summary>
    [Fact]
    public void Run_FailedDiagnosticIteration_PreservesFailure()
    {
        // Arrange
        using BenchmarkRunInfo suite = Discover(typeof(AsyncHealthBenchmarkFixture));
        var diagnoser = Assert.Single(suite.Config.GetDiagnosers().OfType<AsyncHealthDiagnoser>());

        // Act
        Exception? error =
            Record.Exception(() => Run(suite, nameof(AsyncHealthBenchmarkFixture.DiagnosticFailure),
                diagnoser));

        // Assert
        Assert.True(diagnoser.HasFailed);
        Assert.NotNull(error);
        using JsonDocument window = ReadWindow("*-ExtraIteration-Failed.json");
        Assert.Equal(JsonValueKind.Object, window.RootElement.GetProperty("Measurement").ValueKind);
        Assert.Equal(1, window.RootElement.GetProperty("Operations")
            .GetProperty("Failed").GetInt64());
        Assert.Equal(0, window.RootElement.GetProperty("Operations")
            .GetProperty("OpenFailures").GetInt64());
        Assert.False(diagnoser.IsObserving);
    }

    /// <summary>Unmarked benchmarks run without observation or health artifacts.</summary>
    [Fact]
    public void Run_UnmarkedBenchmark_DoesNotObserve()
    {
        // Arrange
        using BenchmarkRunInfo suite = Discover(typeof(UnobservedBenchmarkFixture));

        // Act
        Run(suite, nameof(UnobservedBenchmarkFixture.Ordinary));

        // Assert
        Assert.Empty(suite.Config.GetDiagnosers().OfType<AsyncHealthDiagnoser>());
        Assert.Empty(Directory.GetFiles(_directory, "*-Completed.json"));
        Assert.Empty(Directory.GetFiles(_directory, "*-Failed.json"));
    }

    /// <summary>Read the one retained window matching the requested phase/outcome.</summary>
    /// <param name="pattern">Window filename pattern.</param>
    /// <returns>The parsed window, disposed by its caller.</returns>
    private JsonDocument ReadWindow(string pattern) =>
        JsonDocument.Parse(File.ReadAllText(
            Assert.Single(Directory.GetFiles(_directory, pattern))));

    /// <summary>Discovers a suite using only the ordinary shared job and diagnosers.</summary>
    /// <param name="suite">Fixture suite to discover.</param>
    /// <returns>The discovered suite, including any diagnoser declared on its type.</returns>
    private BenchmarkRunInfo Discover(Type suite)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithArtifactsPath(_directory)
            .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
            .AddJob(Job.Dry.WithToolchain(InProcessEmitToolchain.Default)
                .WithLaunchCount(1).WithWarmupCount(1).WithIterationCount(1)
                .WithInvocationCount(1).WithUnrollFactor(1).WithStrategy(RunStrategy.Monitoring))
            .AddDiagnoser(MemoryDiagnoser.Default).AddDiagnoser(ThreadingDiagnoser.Default);
        return BenchmarkConverter.TypeToBenchmarks(suite, config);
    }

    /// <summary>Runs a single fixture case with a bounded real in-process BDN executor.</summary>
    /// <param name="suite">Discovered fixture suite.</param>
    /// <param name="method">Fixture method to execute.</param>
    /// <param name="diagnoser">The suite's diagnoser, when attached.</param>
    private void Run(BenchmarkRunInfo suite, string method, AsyncHealthDiagnoser? diagnoser = null)
    {
        diagnoser?.Configure(new Config(), new RunnerJob(), _directory);
        AsyncHealthBenchmarkFixture.Observer = diagnoser;

        using var selected = new BenchmarkRunInfo(
            suite.BenchmarksCases.Where(b => b.Descriptor.WorkloadMethod.Name == method).ToArray(),
            suite.Type, suite.Config, suite.CompositeInProcessDiagnoser);
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
            diagnoser?.FinishRun();
        }
    }
}
#endif

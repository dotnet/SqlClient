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
    private readonly AsyncHealthDiagnoser _diagnoser;

    /// <summary>Each test owns its diagnoser, artifacts and static fixture counters.</summary>
    public AsyncHealthDiagnoserTests()
    {
        Directory.CreateDirectory(_directory);
        _diagnoser = new AsyncHealthDiagnoser(false, true, true, _directory);
        AsyncHealthBenchmarkFixture.Observer = _diagnoser;
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
        // Act
        Run(nameof(AsyncHealthBenchmarkFixture.AsyncWait));

        // Assert
        Assert.False(_diagnoser.HasFailed);
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
        Assert.Equal("Completed", result.RootElement.GetProperty("Status").GetString());
        Assert.Equal(1, result.RootElement.GetProperty("CompletedInvocations").GetInt64());
        Assert.Equal(1, result.RootElement.GetProperty("Operations")
            .GetProperty("Completed").GetInt64());
        Assert.Equal("Insufficient", result.RootElement.GetProperty("Summary")
            .GetProperty("Health").GetString());
        Assert.NotEmpty(result.RootElement.GetProperty("Measurement")
            .GetProperty("Probes").GetProperty("Samples").EnumerateArray());
    }

    /// <summary>A diagnostic failure leaves explicit evidence and stops reuse.</summary>
    [Fact]
    public void Run_FailedDiagnosticIteration_PreservesFailure()
    {
        // Act
        Exception? error =
            Record.Exception(() => Run(nameof(AsyncHealthBenchmarkFixture.DiagnosticFailure)));

        // Assert
        Assert.True(_diagnoser.HasFailed);
        Assert.NotNull(error);
        using JsonDocument window = ReadWindow("*-ExtraIteration-Failed.json");
        Assert.Equal(JsonValueKind.Object, window.RootElement.GetProperty("Measurement").ValueKind);
        Assert.Equal(1, window.RootElement.GetProperty("Operations")
            .GetProperty("Failed").GetInt64());
        Assert.False(_diagnoser.IsObserving);
    }

    /// <summary>Unmarked benchmarks run without observation or health artifacts.</summary>
    [Fact]
    public void Run_UnmarkedBenchmark_DoesNotObserve()
    {
        // Act
        Run(nameof(AsyncHealthBenchmarkFixture.Unmarked));

        // Assert
        Assert.False(_diagnoser.HasFailed);
        Assert.Empty(Directory.GetFiles(_directory, "*-Completed.json"));
        Assert.Empty(Directory.GetFiles(_directory, "*-Failed.json"));
    }

    /// <summary>Read the one retained window matching the requested phase/outcome.</summary>
    /// <param name="pattern">Window filename pattern.</param>
    /// <returns>The parsed window, disposed by its caller.</returns>
    private JsonDocument ReadWindow(string pattern) =>
        JsonDocument.Parse(File.ReadAllText(
            Assert.Single(Directory.GetFiles(_directory, pattern))));

    /// <summary>Runs a single fixture case with a bounded real in-process BDN executor.</summary>
    /// <param name="method">Fixture method to execute.</param>
    private void Run(string method)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithArtifactsPath(_directory)
            .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
            .AddJob(Job.Dry.WithToolchain(InProcessEmitToolchain.Default)
                .WithLaunchCount(1).WithWarmupCount(1).WithIterationCount(1)
                .WithInvocationCount(1).WithUnrollFactor(1).WithStrategy(RunStrategy.Monitoring))
            .AddDiagnoser(MemoryDiagnoser.Default).AddDiagnoser(ThreadingDiagnoser.Default)
            .AddDiagnoser(_diagnoser).AddEventProcessor(_diagnoser);
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
            _diagnoser.FinishRun();
        }
    }
}
#endif

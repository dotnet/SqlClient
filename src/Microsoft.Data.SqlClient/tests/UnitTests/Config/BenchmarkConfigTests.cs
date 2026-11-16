// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.IO;
using System.Linq;
using System.Threading;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Running;
using Microsoft.Data.SqlClient.PerformanceTests;
using Microsoft.Data.SqlClient.UnitTests.Measurements;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Config;

/// <summary>Verifies that production BDN configuration bounds preparation without reducing measurements.</summary>
[Collection(AsyncHealthBenchmarkCollection.Name)]
public class BenchmarkConfigTests
{
    /// <summary>Configured warmups and measured operations remain intact without ninety extra prep calls.</summary>
    [Fact]
    public void Run_ExplicitSqlJob_PreservesMeasurementsWithBoundedPreparation()
    {
        // Arrange
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        BenchmarkPreparationFixture.Calls = 0;
        var job = new RunnerJob
        {
            LaunchCount = 1, WarmupCount = 2, IterationCount = 3, InvocationCount = 2
        };
        var config = BenchmarkConfig.s_instance(job,
            new PerformanceTests.Config { EnableAsyncHealth = false }).WithArtifactsPath(directory);
        using BenchmarkRunInfo benchmarks = BenchmarkConverter.TypeToBenchmarks(
            typeof(BenchmarkPreparationFixture), config);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            // Act
            var summaries = BenchmarkRunner.RunAsync([benchmarks], timeout.Token).AsTask()
                .WaitAsync(timeout.Token).GetAwaiter().GetResult();
            var report = Assert.Single(Assert.Single(summaries).Reports);
            var measured = report.AllMeasurements.Where(
                item => item.IterationMode == IterationMode.Workload &&
                    item.IterationStage == IterationStage.Actual).ToArray();
            var warmup = report.AllMeasurements.Where(
                item => item.IterationMode == IterationMode.Workload &&
                    item.IterationStage == IterationStage.Warmup).ToArray();

            // Assert
            Assert.True(report.Success);
            Assert.Equal(3, measured.Length);
            Assert.All(measured, item => Assert.Equal(2, item.Operations));
            Assert.Equal(2, warmup.Length);
            Assert.All(warmup, item => Assert.Equal(2, item.Operations));
            Assert.InRange(BenchmarkPreparationFixture.Calls, 12, 20);
        }
        finally
        {
            // Cleanup
            Directory.Delete(directory, recursive: true);
        }
    }
}
#endif

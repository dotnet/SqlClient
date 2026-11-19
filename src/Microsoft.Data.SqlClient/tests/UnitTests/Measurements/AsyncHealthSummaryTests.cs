// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System.Linq;
using Microsoft.Data.SqlClient.PerformanceTests;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Measurements;

/// <summary>Verifies that censored and short BDN workloads cannot fabricate healthy evidence.</summary>
public class AsyncHealthSummaryTests
{
    /// <summary>A quick workload with a low measured tail has insufficient scheduler coverage.</summary>
    [Fact]
    public void Create_ShortWindow_ReportsInsufficient()
    {
        // Arrange
        var window = new AsyncHealthWindow
        {
            WorkloadEndMilliseconds = 20,
            Probes = new ThreadPoolProbeSnapshot
            {
                Total = 1, Posted = 1, Executed = 1,
                Samples = [new ThreadPoolProbe { ExecutedMilliseconds = 1 }]
            }
        };

        // Act
        AsyncHealthSummary result = AsyncHealthSummary.Create(window);

        // Assert
        Assert.Equal("Insufficient", result.Health);
        Assert.Equal(1, result.DispatchP99Ms);
    }

    /// <summary>Missing resource observations must not look like measured zero thread counts.</summary>
    [Fact]
    public void Create_NoThreadObservations_ReportsMissingPeaks()
    {
        // Arrange
        var window = new AsyncHealthWindow { WorkloadEndMilliseconds = 1 };

        // Act
        AsyncHealthSummary result = AsyncHealthSummary.Create(window);

        // Assert
        Assert.Null(result.ProcessThreadsPeak);
        Assert.Null(result.ThreadPoolThreadsPeak);
        Assert.Equal("Insufficient", result.Health);
    }

    /// <summary>A window that ends before periodic sampling still retains its process start count.</summary>
    [Fact]
    public void Create_OnlyStartObservation_RetainsProcessPeak()
    {
        // Arrange
        var window = new AsyncHealthWindow
        {
            WorkloadEndMilliseconds = 1,
            ProcessThreadsAtStart = 25
        };

        // Act
        AsyncHealthSummary result = AsyncHealthSummary.Create(window);

        // Assert
        Assert.Equal(25, result.ProcessThreadsPeak);
        Assert.Null(result.ThreadPoolThreadsPeak);
        Assert.Equal("Insufficient", result.Health);
    }

    /// <summary>The process peak includes the start observation and all later resource samples.</summary>
    [Theory]
    [InlineData(80, 80)]
    [InlineData(40, 70)]
    public void Create_StartAndPeriodicObservations_UsesLargestProcessCount(int start, int expected)
    {
        // Arrange
        var window = new AsyncHealthWindow
        {
            WorkloadEndMilliseconds = 50,
            ProcessThreadsAtStart = start,
            Resources =
            [
                new AsyncHealthResourceSample { ProcessThreads = 70, Threads = 0 },
                new AsyncHealthResourceSample { ProcessThreads = 60, Threads = 0 }
            ]
        };

        // Act
        AsyncHealthSummary result = AsyncHealthSummary.Create(window);

        // Assert
        Assert.Equal(expected, result.ProcessThreadsPeak);
        Assert.Equal(0, result.ThreadPoolThreadsPeak);
    }

    /// <summary>Enough uncensored probes and resource samples establish the investigative budget.</summary>
    [Fact]
    public void Create_CompleteWindow_ReportsHealthAndAllProcessThreads()
    {
        // Arrange
        var window = new AsyncHealthWindow
        {
            WorkloadEndMilliseconds = 25000,
            Probes = new ThreadPoolProbeSnapshot
            {
                Total = 1000, Posted = 1000, Executed = 1000,
                Samples = Enumerable.Range(0, 1000).Select(i => new ThreadPoolProbe
                {
                    ScheduledMilliseconds = i * 25, PostedMilliseconds = i * 25,
                    ExecutedMilliseconds = i * 25 + 2
                }).ToArray()
            },
            Resources = [new AsyncHealthResourceSample { ProcessThreads = 70, Threads = 16 }]
        };

        // Act
        AsyncHealthSummary result = AsyncHealthSummary.Create(window);

        // Assert
        Assert.Equal("Pass", result.Health);
        Assert.Equal("Adequate", result.DispatchEvidence);
        Assert.Equal(2, result.DispatchP99Ms);
        Assert.Equal(70, result.ProcessThreadsPeak);
        Assert.Equal(16, result.ThreadPoolThreadsPeak);
    }

    /// <summary>Producer loss at the end of the observation remains missing evidence.</summary>
    [Fact]
    public void Create_UnobservedCadence_ReportsMissingSlots()
    {
        // Arrange
        var window = new AsyncHealthWindow { WorkloadEndMilliseconds = 3000 };

        // Act
        AsyncHealthSummary result = AsyncHealthSummary.Create(window);

        // Assert
        Assert.Equal("Insufficient", result.Health);
        Assert.Equal(120, result.Missing);
        Assert.Null(result.DispatchP99Ms);
    }

    /// <summary>A cold wave's 64 first starts are retained but are not a precise p99 population.</summary>
    [Fact]
    public void Create_SingleWave_IdentifiesSparseMaximum()
    {
        // Arrange
        var recorder = new AsyncHealthOperationRecorder();
        var invocation = recorder.BeginInvocation(64);
        for (int worker = 0; worker < 64; worker++)
        {
            var operation = invocation.Offer(worker);
            operation.Start();
            operation.Opened();
            operation.Complete();
        }
        invocation.Complete();

        // Act
        var summary = AsyncHealthSummary.Create(new AsyncHealthWindow(), recorder.Snapshot());

        // Assert
        Assert.Equal(64, summary.FirstWorkerQueueSamples);
        Assert.Equal("Sparse", summary.FirstWorkerQueueEvidence);
        Assert.True(summary.FirstWorkerQueueP99IsMaximum);
    }

    /// <summary>First starts across all waves survive ordinary-record truncation.</summary>
    [Fact]
    public void Create_ExtendedWaves_RetainsAdequateFirstStarts()
    {
        // Arrange
        var recorder = new AsyncHealthOperationRecorder(sampleLimit: 1);
        var invocation = recorder.BeginInvocation(1024);
        for (int worker = 0; worker < 1024; worker++)
        {
            for (int operationIndex = 0; operationIndex < 2; operationIndex++)
            {
                var operation = invocation.Offer(worker);
                operation.Start();
                operation.Opened();
                operation.Complete();
            }
        }
        invocation.Complete();

        // Act
        var summary = AsyncHealthSummary.Create(new AsyncHealthWindow(), recorder.Snapshot());

        // Assert
        Assert.Equal(1024, summary.FirstWorkerQueueSamples);
        Assert.Equal("Adequate", summary.FirstWorkerQueueEvidence);
        Assert.False(summary.FirstWorkerQueueP99IsMaximum);
        Assert.Equal("Truncated", summary.OperationEvidence);
    }
}
#endif

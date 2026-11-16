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

    /// <summary>Enough uncensored probes and resource samples establish the investigative budget.</summary>
    [Fact]
    public void Create_CompleteWindow_ReportsHealthAndAllProcessThreads()
    {
        // Arrange
        var window = new AsyncHealthWindow
        {
            WorkloadEndMilliseconds = 3000,
            Probes = new ThreadPoolProbeSnapshot
            {
                Total = 120, Posted = 120, Executed = 120,
                Samples = Enumerable.Range(0, 120).Select(i => new ThreadPoolProbe
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
}
#endif

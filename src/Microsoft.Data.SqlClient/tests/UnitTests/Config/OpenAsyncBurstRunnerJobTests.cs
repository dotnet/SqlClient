// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient.PerformanceTests;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Config;

/// <summary>Protects cold burst semantics and exact steady-state work/normalization accounting.</summary>
public class OpenAsyncBurstRunnerJobTests
{
    /// <summary>Extended sampling never pads an empty-pool burst with later warm work.</summary>
    [Theory]
    [InlineData("OpenClose")]
    [InlineData("OpenQueryClose")]
    public void GetWorkload_EmptyExtended_KeepsSingleBurst(string method)
    {
        // Arrange
        var job = new OpenAsyncBurstRunnerJob { SamplingProfile = "Extended" };

        // Act
        BurstWorkload workload = job.GetWorkload(method, "Empty");

        // Assert
        Assert.Equal(1, workload.Waves);
        Assert.Equal(64, workload.OperationsPerInvoke);
        Assert.Equal(64, workload.WorkerStarts);
    }

    /// <summary>Sixteen waves produce 1,024 worker starts without changing concurrent worker count.</summary>
    [Fact]
    public void GetWorkload_ExtendedSteady_UsesFixedWorkSize()
    {
        // Arrange
        var job = new OpenAsyncBurstRunnerJob
        {
            SamplingProfile = "Extended",
            SteadyOperationsPerWorker = new Dictionary<string, int>
            {
                ["OpenQueryClose/Warm"] = 20
            }
        };

        // Act
        BurstWorkload workload = job.GetWorkload("OpenQueryClose", "Warm");

        // Assert
        Assert.Equal(1024, workload.WorkerStarts);
        Assert.Equal(20480, workload.OperationsPerInvoke);
        Assert.Equal(20, workload.OperationsPerWorker);
    }

    /// <summary>Unknown or overflowing work sizes must not silently use a different workload.</summary>
    [Theory]
    [InlineData("OpenClose/Typo", 1)]
    [InlineData("OpenClose/Warm", 0)]
    [InlineData("OpenClose/Warm", int.MaxValue)]
    public void ValidateExecution_InvalidWorkSize_Throws(string key, int operations)
    {
        // Arrange
        var job = new OpenAsyncBurstRunnerJob
        {
            SamplingProfile = "Extended",
            SteadyOperationsPerWorker = new Dictionary<string, int> { [key] = operations }
        };

        // Act / Assert
        Assert.Throws<ArgumentException>(job.ValidateExecution);
    }

    /// <summary>Uncalibrated extended cases fail rather than substituting an arbitrary operation count.</summary>
    [Fact]
    public void GetWorkload_MissingExtendedWorkSize_Throws()
    {
        // Arrange
        var job = new OpenAsyncBurstRunnerJob { SamplingProfile = "Extended" };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => job.GetWorkload("OpenClose", "Warm"));
    }

    /// <summary>Warm repeats cannot silently turn the startup cohort into mostly warm work.</summary>
    [Theory]
    [InlineData(1, 2, 1)]
    [InlineData(1, 1, 2)]
    [InlineData(2, 1, 1)]
    public void ValidateExecution_RepeatedStartupWork_Throws(int launches, int warmups, int invocations)
    {
        // Arrange
        var job = new OpenAsyncBurstRunnerJob
        {
            LaunchCount = launches, WarmupCount = warmups, InvocationCount = invocations
        };

        // Act / Assert
        Assert.Throws<ArgumentException>(job.ValidateExecution);
    }

    /// <summary>Throughput JIT/pilot work must not precede the intended cold burst.</summary>
    [Fact]
    public void ValidateExecution_ThroughputStrategy_Throws()
    {
        // Arrange
        var job = new OpenAsyncBurstRunnerJob { RunStrategy = "Throughput" };

        // Act / Assert
        Assert.Throws<ArgumentException>(job.ValidateExecution);
    }
}
#endif

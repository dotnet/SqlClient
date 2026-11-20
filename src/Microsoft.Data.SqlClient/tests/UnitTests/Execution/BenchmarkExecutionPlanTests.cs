// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Microsoft.Data.SqlClient.PerformanceTests;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Execution;

/// <summary>Verifies explicit process boundaries and exact child selection against real BDN cases.</summary>
public class BenchmarkExecutionPlanTests
{
    /// <summary>The default groups all selected cases, even when some have a diagnoser marker.</summary>
    [Fact]
    public void Create_DefaultPolicy_KeepsRunnerTogether()
    {
        // Arrange
        using BenchmarkRunInfo info = BenchmarkConverter.TypeToBenchmarks(
            typeof(ProcessPlanBenchmarkFixture), DefaultConfig.Instance);
        int[] indexes = Enumerable.Range(0, info.BenchmarksCases.Length).ToArray();

        // Act
        var plan = BenchmarkExecutionPlan.Create("Mixed", info.BenchmarksCases, indexes, new());

        // Assert
        Assert.Equal(4, Assert.Single(plan).Indexes.Length);
        Assert.Contains(info.BenchmarksCases, AsyncHealthDiagnoser.IsMarked);
        Assert.Contains(info.BenchmarksCases, benchmark => !AsyncHealthDiagnoser.IsMarked(benchmark));
    }

    /// <summary>PerCase isolates every selected parameter combination without a marker requirement.</summary>
    [Fact]
    public void Create_PerCase_SelectsOneCasePerChild()
    {
        // Arrange
        using BenchmarkRunInfo info = BenchmarkConverter.TypeToBenchmarks(
            typeof(ProcessPlanBenchmarkFixture), DefaultConfig.Instance);
        var job = new RunnerJob { ProcessIsolation = "PerCase" };

        // Act
        var plan = BenchmarkExecutionPlan.Create("Mixed", info.BenchmarksCases, [0, 3], job);
        var selected = plan.SelectMany(selection =>
            BenchmarkExecutionPlan.Select(selection, "Mixed", info.BenchmarksCases, job)).ToArray();

        // Assert
        Assert.Equal(2, plan.Length);
        Assert.All(plan, selection => Assert.Single(selection.Indexes));
        Assert.Equal([info.BenchmarksCases[0], info.BenchmarksCases[3]], selected);
        Assert.NotEqual(plan[0].Fingerprint, plan[1].Fingerprint);
    }

    /// <summary>A changed discovery result must fail rather than execute a different child case.</summary>
    [Fact]
    public void Select_ChangedWorkload_Throws()
    {
        // Arrange
        using BenchmarkRunInfo info = BenchmarkConverter.TypeToBenchmarks(
            typeof(ProcessPlanBenchmarkFixture), DefaultConfig.Instance);
        var job = new RunnerJob { ProcessIsolation = "PerCase" };
        var selection = Assert.Single(
            BenchmarkExecutionPlan.Create("Mixed", info.BenchmarksCases, [0], job));
        var changed = info.BenchmarksCases.ToArray();
        changed[0] = BenchmarkExecutionPlan.WithOperationsPerInvoke(changed[0], 1024);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() =>
            BenchmarkExecutionPlan.Select(selection, "Mixed", changed, job));
        Assert.Throws<InvalidOperationException>(() =>
            BenchmarkExecutionPlan.Select(selection, "Different", info.BenchmarksCases, job));
    }

    /// <summary>Changing normalization preserves the benchmark, parameters, job and lifecycle methods.</summary>
    [Fact]
    public void WithOperationsPerInvoke_ExpandedBatch_PreservesCase()
    {
        // Arrange
        using BenchmarkRunInfo info = BenchmarkConverter.TypeToBenchmarks(
            typeof(ProcessPlanBenchmarkFixture), DefaultConfig.Instance);
        BenchmarkCase original = info.BenchmarksCases[0];

        // Act
        BenchmarkCase updated = BenchmarkExecutionPlan.WithOperationsPerInvoke(original, 20480);

        // Assert
        Assert.Equal(20480, updated.Descriptor.OperationsPerInvoke);
        Assert.Same(original.Descriptor.WorkloadMethod, updated.Descriptor.WorkloadMethod);
        Assert.Same(original.Parameters, updated.Parameters);
        Assert.Equal(original.Job, updated.Job);
    }
}
#endif

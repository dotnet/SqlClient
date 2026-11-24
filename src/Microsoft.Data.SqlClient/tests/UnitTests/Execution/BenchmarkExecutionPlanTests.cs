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
    /// <summary>The default groups all selected cases even with async health attached.</summary>
    [Fact]
    public void Create_DefaultPolicy_KeepsRunnerTogether()
    {
        // Arrange
        using BenchmarkRunInfo info = BenchmarkConverter.TypeToBenchmarks(
            typeof(ProcessPlanBenchmarkFixture), DefaultConfig.Instance);
        int[] indexes = Enumerable.Range(0, info.BenchmarksCases.Length).ToArray();

        // Act
        var plan = BenchmarkExecutionPlan.Create("Observed", info.BenchmarksCases, indexes, new());

        // Assert
        Assert.Equal(4, Assert.Single(plan).Indexes.Length);
        Assert.Single(info.Config.GetDiagnosers().OfType<AsyncHealthDiagnoser>());
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
    public void Select_ChangedCaseAtSelectedIndex_Throws()
    {
        // Arrange
        using BenchmarkRunInfo info = BenchmarkConverter.TypeToBenchmarks(
            typeof(ProcessPlanBenchmarkFixture), DefaultConfig.Instance);
        var job = new RunnerJob { ProcessIsolation = "PerCase" };
        var selection = Assert.Single(
            BenchmarkExecutionPlan.Create("Mixed", info.BenchmarksCases, [0], job));
        var changed = info.BenchmarksCases.ToArray();
        (changed[0], changed[1]) = (changed[1], changed[0]);

        // Act
        Exception? error = Record.Exception(() =>
            BenchmarkExecutionPlan.Select(selection, "Mixed", changed, job));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
    }
}
#endif

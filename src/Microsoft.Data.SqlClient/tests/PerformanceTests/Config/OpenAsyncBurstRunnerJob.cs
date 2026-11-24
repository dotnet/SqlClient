// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Linq;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Running;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>Selects the sampling mode and validates the benchmark's BDN execution settings.</summary>
public sealed class OpenAsyncBurstRunnerJob : RunnerJob, IJsonOnDeserialized
{
    public string SamplingProfile = "Burst";

    /// <summary>Keeps the first observed workload a single cold burst without JIT/pilot repeats.</summary>
    public OpenAsyncBurstRunnerJob()
    {
        ApplyNetworkPolicy();
        LaunchCount = 1;
        WarmupCount = 1;
        InvocationCount = 1;
        IterationCount = 3;
        RunStrategy = "Monitoring";
        TimeoutMinutes = 5;
    }

    void IJsonOnDeserialized.OnDeserialized() => ApplyNetworkPolicy();

    private void ApplyNetworkPolicy() =>
        AdditionalRttMilliseconds = OperatingSystem.IsLinux() ? 100 : 0;

    internal override bool RepeatAll => SamplingProfile == "Extended";

    internal override int GetOperationsPerInvoke(BenchmarkCase benchmark)
    {
        var state = (OpenAsyncBurstRunner.PoolState)benchmark.Parameters.Items
            .Single(p => p.Name == nameof(OpenAsyncBurstRunner.State)).Value!;
        return OpenAsyncBurstRunner.GetWorkload(this, state).OperationsPerInvoke;
    }

    public override void ValidateExecution()
    {
        base.ValidateExecution();
        if (LaunchCount != 1 || WarmupCount != 1 || InvocationCount != 1 ||
            RunStrategy != "Monitoring")
        {
            throw new ArgumentException(
                "Burst sampling requires Monitoring with one launch, one warmup " +
                "and one invocation per iteration.");
        }
        if (SamplingProfile != "Burst" && SamplingProfile != "Extended")
        {
            throw new ArgumentException("SamplingProfile must be Burst or Extended.");
        }
    }
}

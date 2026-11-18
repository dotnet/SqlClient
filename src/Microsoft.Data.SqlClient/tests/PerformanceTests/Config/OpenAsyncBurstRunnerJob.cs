// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>Controls burst workload size without changing concurrency or cold-start semantics.</summary>
public sealed class OpenAsyncBurstRunnerJob : RunnerJob
{
    public const int ConcurrentWorkers = 64;
    public const int ExtendedWaves = 16;
    public string SamplingProfile = "Burst";
    public Dictionary<string, int>? SteadyOperationsPerWorker;

    /// <summary>Keeps the first observed workload a single cold burst without JIT/pilot repeats.</summary>
    public OpenAsyncBurstRunnerJob()
    {
        LaunchCount = 1;
        WarmupCount = 1;
        InvocationCount = 1;
        IterationCount = 3;
        RunStrategy = "Monitoring";
        TimeoutMinutes = 5;
    }

    public override void ValidateExecution()
    {
        base.ValidateExecution();
        if (LaunchCount != 1 || WarmupCount != 1 || InvocationCount != 1 ||
            RunStrategy != "Monitoring")
        {
            throw new ArgumentException(
                "Burst sampling requires Monitoring with one launch, one warmup " +
                "and one invocation per iteration. " +
                "Use SteadyOperationsPerWorker to extend steady work without diluting cold startup.");
        }
        if (SamplingProfile != "Burst" && SamplingProfile != "Extended")
        {
            throw new ArgumentException("SamplingProfile must be Burst or Extended.");
        }
        if (SteadyOperationsPerWorker != null)
        {
            foreach (var setting in SteadyOperationsPerWorker)
            {
                string[] parts = setting.Key.Split('/');
                if (parts.Length != 2 ||
                    (parts[0] != "OpenClose" && parts[0] != "OpenQueryClose") ||
                    (parts[1] != "Warm" && parts[1] != "Oversubscribed" && parts[1] != "NonPooled") ||
                    setting.Value <= 0 ||
                    setting.Value > int.MaxValue / (ConcurrentWorkers * ExtendedWaves))
                {
                    throw new ArgumentException(
                        "Invalid SteadyOperationsPerWorker entry: " + setting.Key);
                }
            }
        }
    }

    /// <summary>Resolves fixed work shared by case execution and BDN operation normalization.</summary>
    public BurstWorkload GetWorkload(string method, string state)
    {
        ValidateExecution();
        if (method != "OpenClose" && method != "OpenQueryClose")
        {
            throw new ArgumentException("Unknown burst method.", nameof(method));
        }
        if (state != "Empty" && state != "Warm" &&
            state != "Oversubscribed" && state != "NonPooled")
        {
            throw new ArgumentException("Unknown burst pool state.", nameof(state));
        }
        if (SamplingProfile == "Burst" || state == "Empty")
        {
            return new BurstWorkload(1, 1);
        }
        if (SteadyOperationsPerWorker?.TryGetValue(method + "/" + state, out int operations) != true)
        {
            throw new ArgumentException(
                "Extended sampling requires a fixed SteadyOperationsPerWorker value for " +
                method + "/" + state + ".");
        }
        return new BurstWorkload(ExtendedWaves, operations);
    }
}

/// <summary>One fixed workload definition shared by execution and BDN normalization.</summary>
public sealed record BurstWorkload(int Waves, int OperationsPerWorker)
{
    public int WorkerStarts => checked(OpenAsyncBurstRunnerJob.ConcurrentWorkers * Waves);
    public int OperationsPerInvoke => checked(WorkerStarts * OperationsPerWorker);
}

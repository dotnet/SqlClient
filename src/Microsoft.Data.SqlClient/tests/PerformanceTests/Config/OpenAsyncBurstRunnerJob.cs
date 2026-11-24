// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>Configures and validates the benchmark's BDN settings.</summary>
public sealed class OpenAsyncBurstRunnerJob : RunnerJob
{
    public OpenAsyncBurstRunnerJob() => RunStrategy = "Monitoring";

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
        if (IterationCount < 1)
        {
            throw new ArgumentException(
                "Set IterationCount to a positive value in the runner config.");
        }
    }
}

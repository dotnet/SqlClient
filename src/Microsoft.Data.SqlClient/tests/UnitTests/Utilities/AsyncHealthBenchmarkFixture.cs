// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.SqlClient.PerformanceTests;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities;

/// <summary>SQL-free workloads verify the actual emitted BDN diagnoser and fixture boundaries.</summary>
public class AsyncHealthBenchmarkFixture
{
    internal static AsyncHealthDiagnoser? Observer;
    internal static int Observed, Unobserved, Setups, Cleanups;
    internal static int StartupObserved, TimingInvocations;

    /// <summary>Checks that fixture setup remains outside the extra-iteration observation.</summary>
    [IterationSetup]
    public void Setup()
    {
        if (Observer!.IsObserving)
        {
            throw new InvalidOperationException("Setup overlapped extra-iteration observation.");
        }
        Interlocked.Increment(ref Setups);
    }

    /// <summary>Checks that extra-iteration observation ends before successful fixture cleanup.</summary>
    [IterationCleanup]
    public void Cleanup()
    {
        if (Observer!.IsObserving)
        {
            throw new InvalidOperationException("Cleanup overlapped extra-iteration observation.");
        }
        Interlocked.Increment(ref Cleanups);
    }

    /// <summary>Completes real asynchronous work without using any SQL environment.</summary>
    [Benchmark]
    [AsyncHealthDiagnoser]
    public async Task AsyncWait()
    {
        var invocation = AsyncHealthDiagnoser.BeginInvocation(1);
        var operation = invocation?.Offer(0);
        if (Observer!.IsObserving) Interlocked.Increment(ref Observed);
        else Interlocked.Increment(ref Unobserved);
        if (!Observer.IsObserving && invocation != null) Interlocked.Increment(ref StartupObserved);
        if (invocation == null) Interlocked.Increment(ref TimingInvocations);
        operation?.Start();
        await Task.Delay(150).ConfigureAwait(false);
        operation?.Opened();
        operation?.Complete();
        invocation?.Complete();
    }

    /// <summary>Fails inside the diagnostic window, after successful timing iterations.</summary>
    [Benchmark]
    [AsyncHealthDiagnoser]
    public async Task DiagnosticFailure()
    {
        var invocation = AsyncHealthDiagnoser.BeginInvocation(1);
        var operation = invocation?.Offer(0);
        operation?.Start();
        try
        {
            await Task.Delay(50).ConfigureAwait(false);
            if (Observer!.IsObserving)
            {
                throw new InvalidOperationException("Intentional diagnostic failure.");
            }
            operation?.Opened();
            operation?.Complete();
            invocation?.Complete();
        }
        catch (Exception error)
        {
            operation?.Fail(error);
            throw;
        }
    }

    /// <summary>An unmarked control must never activate the async-health observer.</summary>
    [Benchmark]
    public void Unmarked()
    {
        if (Observer!.IsObserving)
        {
            throw new InvalidOperationException("Unmarked benchmark observed.");
        }
        Thread.Sleep(20);
    }
}
#endif

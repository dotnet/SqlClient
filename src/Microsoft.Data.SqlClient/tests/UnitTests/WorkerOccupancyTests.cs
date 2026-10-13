// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.PerformanceTests;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Serializes process-wide thread measurements and temporary worker-floor changes.</summary>
[CollectionDefinition("Connectivity scorecard", DisableParallelization = true)]
public class ConnectivityScorecardCollection { }

/// <summary>Verifies the connectivity scorecard's time weighting, normalization and failure propagation without SQL Server.</summary>
[Collection("Connectivity scorecard")]
public class WorkerOccupancyTests
{
    /// <summary>Uneven sample intervals are weighted by actual elapsed time rather than sample count.</summary>
    [Fact]
    public void Samples_UseElapsedTimeAndNormalizePerOpen()
    {
        var occupancy = new WorkerOccupancy();
        occupancy.Sample(0, 2, 4, 10);
        occupancy.Sample(Stopwatch.Frequency, 2, 5, 12);
        occupancy.Sample(Stopwatch.Frequency * 4, 6, 8, 11);
        ConnectivityLoadResult result = occupancy.Complete(10, 0.5);

        Assert.Equal(4, result.ElapsedSeconds);
        Assert.Equal(14, result.WorkerSeconds);
        Assert.Equal(3.5, result.AverageOccupiedWorkers);
        Assert.Equal(1400, result.WorkerMillisecondsPerOpen);
        Assert.Equal(2.5, result.OpensPerSecond);
        Assert.Equal(50, result.CpuMillisecondsPerOpen);
        Assert.Equal(6, result.PeakOccupiedWorkers);
        Assert.Equal(8, result.PeakPoolThreads);
        Assert.Equal(12, result.PeakProcessThreads);
        Assert.Equal(3, result.Samples);
    }

    /// <summary>Combining invocations normalizes their totals instead of averaging differently-sized rates.</summary>
    [Fact]
    public void Invocations_CombineWeightedTotals()
    {
        var first = new ConnectivityLoadResult(1, 2, 10, 2, 4, 10, 3, 0.1);
        var second = new ConnectivityLoadResult(3, 18, 30, 6, 8, 12, 7, 0.3);
        ConnectivityLoadResult combined = first.Combine(second);

        Assert.Equal(5, combined.AverageOccupiedWorkers);
        Assert.Equal(500, combined.WorkerMillisecondsPerOpen);
        Assert.Equal(10, combined.OpensPerSecond);
        Assert.Equal(10, combined.CpuMillisecondsPerOpen);
        Assert.Equal(6, combined.PeakOccupiedWorkers);
        Assert.Equal(8, combined.PeakPoolThreads);
        Assert.Equal(12, combined.PeakProcessThreads);
        Assert.Equal(10, combined.Samples);
    }

    /// <summary>Missing observations, nonmonotonic timestamps and zero successful opens cannot produce success-shaped metrics.</summary>
    [Fact]
    public void InvalidMeasurements_FailExplicitly()
    {
        var occupancy = new WorkerOccupancy();
        Assert.Throws<InvalidOperationException>(() => occupancy.Complete(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => occupancy.Sample(0, -1, 0));
        occupancy.Sample(10, 1, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => occupancy.Sample(9, 1, 1));
        occupancy.Sample(20, 1, 1);
        Assert.Throws<InvalidOperationException>(() => occupancy.Complete(0, 0));
    }

    /// <summary>Both truly asynchronous waits and synchronous worker waits complete and are included in the sampling window.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Load_AwaitsEveryOperationAndStopsObserver(bool blocking)
    {
        long completed = 0;
        ConnectivityLoadResult result = await ConnectivityLoad.RunAsync(
            async () =>
            {
                if (blocking)
                {
                    Thread.Sleep(10);
                }
                else
                {
                    await Task.Delay(10);
                }
                Interlocked.Increment(ref completed);
            },
            4, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(5));

        Assert.Equal(Interlocked.Read(ref completed), result.SuccessfulOpens);
        Assert.True(result.SuccessfulOpens > 0);
        Assert.True(result.ElapsedSeconds >= 0.1);
        Assert.True(result.Samples >= 2);
        Assert.True(double.IsFinite(result.AverageOccupiedWorkers));
        Assert.True(double.IsFinite(result.WorkerMillisecondsPerOpen));
    }

    /// <summary>An open failure invalidates the invocation and does not become a reduced-throughput success.</summary>
    [Fact]
    public async Task Load_OpenFailurePropagates()
    {
        var error = new InvalidOperationException("open failed");
        Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ConnectivityLoad.RunAsync(
                () => Task.FromException(error),
                4, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(5)));
        Assert.Same(error, actual);
    }

    /// <summary>Stopping admissions does not stop sampling while an already-started operation is still pending.</summary>
    [Fact]
    public async Task Load_InFlightOperationIsIncludedBeyondDuration()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ConnectivityLoadResult> load = ConnectivityLoad.RunAsync(
            () =>
            {
                entered.TrySetResult(true);
                return release.Task;
            },
            1, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(5));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(150);
            Assert.False(load.IsCompleted);
        }
        finally
        {
            release.TrySetResult(true);
        }

        ConnectivityLoadResult result = await load;
        Assert.Equal(1, result.SuccessfulOpens);
        Assert.True(result.ElapsedSeconds >= 0.15);
        Assert.True(result.Samples > 2);
    }

    /// <summary>The same wait holds workers when synchronous and releases them when awaited.</summary>
    [Fact]
    public async Task Load_BlockingControlOccupiesMoreWorkersThanAsyncWait()
    {
        ThreadPool.GetMinThreads(out int originalWorkers, out int originalIo);
        Assert.True(ThreadPool.SetMinThreads(Math.Max(originalWorkers, 8), originalIo));
        try
        {
            ConnectivityLoadResult asynchronous = await ConnectivityLoad.RunAsync(
                () => Task.Delay(25),
                4, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(5));
            ConnectivityLoadResult blocking = await ConnectivityLoad.RunAsync(
                () =>
                {
                    Thread.Sleep(25);
                    return Task.CompletedTask;
                },
                4, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(5));
            Assert.True(blocking.AverageOccupiedWorkers > asynchronous.AverageOccupiedWorkers + 2,
                $"Blocking average {blocking.AverageOccupiedWorkers}, async average {asynchronous.AverageOccupiedWorkers}.");
        }
        finally
        {
            Assert.True(ThreadPool.SetMinThreads(originalWorkers, originalIo));
            ThreadPool.GetMinThreads(out int restoredWorkers, out int restoredIo);
            Assert.Equal(originalWorkers, restoredWorkers);
            Assert.Equal(originalIo, restoredIo);
        }
    }
}
#endif

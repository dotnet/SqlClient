// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Linq;

namespace Microsoft.Data.SqlClient.PerformanceTests;

internal sealed class AsyncHealthSummary
{
    public string Health { get; init; } = "Insufficient";
    public double WindowMilliseconds { get; init; }
    public double? DispatchP99Ms { get; init; }
    public double? ProducerP99Ms { get; init; }
    public int Samples { get; init; }
    public long Missing { get; init; }
    public int ProcessThreadsPeak { get; init; }
    public int ThreadPoolThreadsPeak { get; init; }
    public string OperationEvidence { get; init; } = "NotInstrumented";
    public double? FirstWorkerQueueP99Ms { get; init; }
    public double? FirstWorkerOfferToOpenP99Ms { get; init; }
    public double? SuccessfulOpenP99Ms { get; init; }
    public double? FailedOpenP99Ms { get; init; }
    public double? CancelledOperationP99Ms { get; init; }
    public double? CompletedOperationsPerSecond { get; init; }

    internal static AsyncHealthSummary Create(
        AsyncHealthWindow window, AsyncHealthOperationSnapshot? operations = null)
    {
        ThreadPoolProbeSnapshot probes = window.Probes;
        ThreadPoolProbe[] samples = probes.Samples.Where(p =>
            p.PostedMilliseconds >= window.WorkloadStartMilliseconds &&
            p.PostedMilliseconds <= window.WorkloadEndMilliseconds).ToArray();
        double[] delays = samples.Where(p => p.ExecutedMilliseconds.HasValue)
            .Select(p => p.ExecutedMilliseconds!.Value - p.PostedMilliseconds).ToArray();
        double duration = window.WorkloadEndMilliseconds - window.WorkloadStartMilliseconds;
        long missedEnd = Math.Max(0, (long)(duration / 25) - probes.Total);
        long missing = probes.Dropped + probes.Skipped + probes.Rejected +
            probes.ProducerMissed + probes.Outstanding + missedEnd;
        double? p99 = Percentile(delays);
        bool eligible = duration >= 2500 && delays.Length >= 100 &&
            missing == 0 && window.ResourceSamplesDropped == 0 && window.Resources.Length > 0;
        AsyncHealthOperationObservation[] records = operations?.Observations ?? [];
        bool instrumented = operations?.Invocations > 0;
        bool operationsComplete = instrumented && operations!.Queued == 0 &&
            operations.InFlight == 0 && operations.FirstWorkerOutcomesMissing == 0 &&
            operations.CompletedInvocations == operations.Invocations;
        return new AsyncHealthSummary
        {
            Health = window.ObserverError != null ? "Invalid" :
                p99 > 100 ? "Fail" : eligible ? "Pass" : "Insufficient",
            WindowMilliseconds = duration, DispatchP99Ms = p99,
            ProducerP99Ms = Percentile(samples.Select(
                p => p.PostedMilliseconds - p.ScheduledMilliseconds).ToArray()),
            Samples = delays.Length, Missing = missing,
            ProcessThreadsPeak = window.Resources.Select(r => r.ProcessThreads).DefaultIfEmpty().Max(),
            ThreadPoolThreadsPeak = window.Resources.Select(r => r.Threads).DefaultIfEmpty().Max(),
            OperationEvidence = !instrumented ? "NotInstrumented" :
                !operationsComplete ? "Incomplete" :
                operations!.Failed != 0 || operations.Cancelled != 0 ? "Failed" :
                operations.ObservationsDropped != 0 ? "Truncated" : "Complete",
            FirstWorkerQueueP99Ms = Percentile(records
                .Where(o => o.First && o.StartMs.HasValue)
                .Select(o => o.StartMs!.Value - o.OfferedMs).ToArray()),
            FirstWorkerOfferToOpenP99Ms = Percentile(records
                .Where(o => o.First && o.OpenEndMs.HasValue)
                .Select(o => o.OpenEndMs!.Value - o.OfferedMs).ToArray()),
            SuccessfulOpenP99Ms = Percentile(records
                .Where(o => o.StartMs.HasValue && o.OpenEndMs.HasValue)
                .Select(o => o.OpenEndMs!.Value - o.StartMs!.Value).ToArray()),
            FailedOpenP99Ms = Percentile(records
                .Where(o => o.Outcome == "Failed" && !o.OpenEndMs.HasValue)
                .Select(o => o.EndMs!.Value - o.StartMs!.Value).ToArray()),
            CancelledOperationP99Ms = Percentile(records
                .Where(o => o.Outcome == "Cancelled")
                .Select(o => o.EndMs!.Value - o.StartMs!.Value).ToArray()),
            CompletedOperationsPerSecond = operations?.CompletedPerSecond
        };
    }

    private static double? Percentile(double[] values)
    {
        if (values.Length == 0)
        {
            return null;
        }
        Array.Sort(values);
        return values[(int)Math.Ceiling(values.Length * .99) - 1];
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Linq;

namespace Microsoft.Data.SqlClient.PerformanceTests;

internal sealed class AsyncHealthSummary
{
    internal const int MinimumP99Samples = 1000;
    internal const int MinimumObservationMilliseconds = 25000;
    public double WindowMilliseconds { get; init; }
    public double? DispatchP99Ms { get; init; }
    public int Samples { get; init; }
    public long Missing { get; init; }
    public string DispatchEvidence { get; init; } = "Sparse";
    public int FirstWorkerQueueSamples { get; init; }
    public string FirstWorkerQueueEvidence { get; init; } = "Sparse";
    public bool FirstWorkerQueueP99IsMaximum { get; init; }
    public int? ProcessThreadsPeak { get; init; }
    public int? ThreadPoolThreadsPeak { get; init; }
    public string OperationEvidence { get; init; } = "NotInstrumented";
    public double? FirstWorkerQueueP99Ms { get; init; }

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
        long missedEnd = Math.Max(0,
            (long)(duration / ThreadPoolHealthObserver.ProbeIntervalMilliseconds) - probes.Total);
        long missing = probes.Dropped + probes.Skipped + probes.Rejected +
            probes.ProducerMissed + probes.Outstanding + missedEnd;
        bool dispatchEligible = duration >= MinimumObservationMilliseconds &&
            delays.Length >= MinimumP99Samples &&
            missing == 0;
        AsyncHealthOperationObservation[] records = operations?.Observations ?? [];
        bool instrumented = operations?.Invocations > 0;
        double[] firstQueue = records.Where(o => o.First && o.StartMs.HasValue)
            .Select(o => o.StartMs!.Value - o.OfferedMs).ToArray();
        return new AsyncHealthSummary
        {
            WindowMilliseconds = duration, DispatchP99Ms = Percentile(delays),
            Samples = delays.Length, Missing = missing,
            DispatchEvidence = window.ObserverError != null ? "Invalid" :
                missing != 0 ? "Censored" : dispatchEligible ? "Adequate" : "Sparse",
            FirstWorkerQueueSamples = firstQueue.Length,
            FirstWorkerQueueEvidence = !instrumented ? "NotInstrumented" :
                !operations!.IsComplete ? "Incomplete" :
                firstQueue.Length >= MinimumP99Samples ? "Adequate" : "Sparse",
            FirstWorkerQueueP99IsMaximum = firstQueue.Length > 0 && firstQueue.Length < 100,
            ProcessThreadsPeak = window.Resources.Select(r => (int?)r.ProcessThreads)
                .Append(window.ProcessThreadsAtStart > 0 ? window.ProcessThreadsAtStart : null).Max(),
            ThreadPoolThreadsPeak = window.Resources.Select(r => (int?)r.Threads).Max(),
            OperationEvidence = !instrumented ? "NotInstrumented" :
                !operations!.IsComplete ? "Incomplete" :
                operations.HasFailures ? "Failed" :
                operations.ObservationsDropped != 0 ? "Truncated" : "Complete",
            FirstWorkerQueueP99Ms = Percentile(firstQueue)
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

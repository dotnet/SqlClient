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

    internal static AsyncHealthSummary Create(AsyncHealthWindow window)
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
        return new AsyncHealthSummary
        {
            Health = window.ObserverError != null ? "Invalid" :
                p99 > 100 ? "Fail" : eligible ? "Pass" : "Insufficient",
            WindowMilliseconds = duration, DispatchP99Ms = p99,
            ProducerP99Ms = Percentile(samples.Select(
                p => p.PostedMilliseconds - p.ScheduledMilliseconds).ToArray()),
            Samples = delays.Length, Missing = missing,
            ProcessThreadsPeak = window.Resources.Select(r => r.ProcessThreads).DefaultIfEmpty().Max(),
            ThreadPoolThreadsPeak = window.Resources.Select(r => r.Threads).DefaultIfEmpty().Max()
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

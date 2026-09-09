// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace ConnectionPoolRampStress;

internal sealed record Resources(int ActiveWorkers, int InFlight, int PeakInFlight, int StartedWorkers,
    double CpuSeconds, long WorkingSetBytes, long ManagedBytes, int Gen0, int Gen1, int Gen2,
    int AvailableWorkers, int AvailableIo, int ThreadPoolThreads, long PendingWorkItems,
    double? ActivePhysicalConnections, double? HardConnects, double? CounterElapsedSeconds)
{
    public double SampleElapsedSeconds { get; init; }
}

internal sealed class PhysicalCounters : EventListener
{
    private double _active = double.NaN;
    private double _connects = double.NaN;
    private double _elapsed = double.NaN;

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
            // Counters are LogAlways events; exclude informational/verbose driver tracing.
            EnableEvents(eventSource, EventLevel.Critical, EventKeywords.None,
                new Dictionary<string, string?> { ["EventCounterIntervalSec"] = "1" });
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName != "EventCounters" || eventData.Payload?.Count != 1 ||
            eventData.Payload[0] is not IDictionary<string, object> payload ||
            !payload.TryGetValue("Name", out object? name)) return;
        if (name is "active-hard-connections" && payload.TryGetValue("Mean", out object? active))
            Volatile.Write(ref _active, Convert.ToDouble(active, System.Globalization.CultureInfo.InvariantCulture));
        if (name is "hard-connects" && payload.TryGetValue("Increment", out object? connects))
        {
            Volatile.Write(ref _connects, Convert.ToDouble(connects, System.Globalization.CultureInfo.InvariantCulture));
            if (payload.TryGetValue("IntervalSec", out object? elapsed))
                Volatile.Write(ref _elapsed, Convert.ToDouble(elapsed, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public Resources Read(int active, int inFlight, int peak, int started)
    {
        using Process process = Process.GetCurrentProcess();
        ThreadPool.GetAvailableThreads(out int workers, out int io);
        static double? Available(double value) => double.IsFinite(value) ? value : null;
        return new(active, inFlight, peak, started, process.TotalProcessorTime.TotalSeconds,
            process.WorkingSet64, GC.GetTotalMemory(false), GC.CollectionCount(0), GC.CollectionCount(1),
            GC.CollectionCount(2), workers, io, ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount,
            Available(Volatile.Read(ref _active)), Available(Volatile.Read(ref _connects)), Available(Volatile.Read(ref _elapsed)));
    }
}

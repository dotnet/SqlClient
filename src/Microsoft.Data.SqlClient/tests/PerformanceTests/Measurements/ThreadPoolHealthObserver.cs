// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Data.SqlClient.PerformanceTests;

internal sealed class AsyncHealthResourceSample
{
    public double TimestampMilliseconds { get; set; }
    public int Threads { get; set; }
    public int ProcessThreads { get; set; }
    public long QueueDepth { get; set; }
    public long AllocatedBytes { get; set; }
    public int Gen2Collections { get; set; }
}

internal sealed class AsyncHealthWindow
{
    public double WorkloadStartMilliseconds { get; set; }
    public double WorkloadEndMilliseconds { get; set; }
    public double RecoveryEndMilliseconds { get; set; }
    public int ProcessThreadsAtStart { get; set; }
    public long OutstandingAtWorkloadEnd { get; set; }
    public string? ObserverError { get; set; }
    public long ResourceSamplesDropped { get; set; }
    public ThreadPoolProbeSnapshot Probes { get; set; } = new();
    public AsyncHealthResourceSample[] Resources { get; set; } = [];
}

/// <summary>Dedicated producer: never relies on a ThreadPool timer or continuation to stop.</summary>
internal sealed class ThreadPoolHealthObserver : IDisposable
{
    private readonly ManualResetEventSlim _ready = new();
    private readonly ManualResetEventSlim _active = new();
    private readonly ManualResetEventSlim _stop = new();
    private readonly List<AsyncHealthResourceSample> _resources = [];
    private readonly ThreadPoolProbeRecorder _probes;
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly long _epoch = Stopwatch.GetTimestamp();
    private readonly int _recoveryMilliseconds;
    private Thread? _thread;
    private double _start;
    private int _processThreadsAtStart;
    private string? _failure;
    private long _resourceSamplesDropped;
    private AsyncHealthWindow? _result;

    public ThreadPoolHealthObserver()
        : this(action => ThreadPool.UnsafeQueueUserWorkItem(
            static callback => callback(), action, preferLocal: false))
    {
    }

    internal ThreadPoolHealthObserver(Action<Action> dispatch, int recoveryMilliseconds = 1000)
    {
        _recoveryMilliseconds = recoveryMilliseconds;
        _probes = new ThreadPoolProbeRecorder(64, TimeProvider.System, _epoch, dispatch);
    }

    private double Timestamp() => Stopwatch.GetElapsedTime(_epoch).TotalMilliseconds;

    public void Start()
    {
        if (_thread != null)
        {
            throw new InvalidOperationException("Observer is single-use.");
        }

        _thread = new Thread(Observe) { Name = "OpenAsync health observer", IsBackground = true };
        _thread.Start();

        if (!_ready.Wait(TimeSpan.FromSeconds(10)))
        {
            _stop.Set();
            _active.Set();
            throw new TimeoutException("Observer did not start.");
        }

        _process.Refresh();
        _processThreadsAtStart = _process.Threads.Count;
        _start = Timestamp();
        _active.Set();
    }

    private void Observe()
    {
        _ready.Set();
        _active.Wait();
        double next = _start;
        try
        {
            while (!_stop.IsSet)
            {
                double now = Timestamp();
                if (now < next)
                {
                    _stop.Wait(TimeSpan.FromMilliseconds(next - now));
                    continue;
                }

                _probes.RequestProbe(ref next, now, 25);
                if (_resources.Count == 100000)
                {
                    _resourceSamplesDropped++;
                    continue;
                }

                _process.Refresh();
                _resources.Add(new AsyncHealthResourceSample
                {
                    TimestampMilliseconds = Timestamp(),
                    Threads = ThreadPool.ThreadCount,
                    ProcessThreads = _process.Threads.Count,
                    QueueDepth = ThreadPool.PendingWorkItemCount,
                    AllocatedBytes = GC.GetTotalAllocatedBytes(),
                    Gen2Collections = GC.CollectionCount(2)
                });
            }
        }
        catch (Exception error)
        {
            _failure = error.GetType().FullName;
            _stop.Set();
        }
    }

    public AsyncHealthWindow Complete()
    {
        if (_result != null)
        {
            return _result;
        }
        if (_thread == null)
        {
            throw new InvalidOperationException("Observer has not started.");
        }

        _stop.Set();
        double end = Timestamp();
        long outstanding = _probes.Snapshot().Outstanding;
        if (!_thread.Join(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("Observer did not stop.");
        }

        double remaining = end + _recoveryMilliseconds - Timestamp();
        if (remaining > 0)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(remaining));
        }

        _result = new AsyncHealthWindow
        {
            WorkloadStartMilliseconds = _start, WorkloadEndMilliseconds = end,
            RecoveryEndMilliseconds = Timestamp(), ProcessThreadsAtStart = _processThreadsAtStart,
            OutstandingAtWorkloadEnd = outstanding, ObserverError = _failure,
            ResourceSamplesDropped = _resourceSamplesDropped,
            Probes = _probes.Snapshot(), Resources = _resources.ToArray()
        };
        return _result;
    }

    public void Dispose()
    {
        if (_thread != null && _result == null)
        {
            Complete();
        }
        _ready.Dispose();
        _active.Dispose();
        _stop.Dispose();
        _process.Dispose();
    }
}

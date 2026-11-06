// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public sealed class AsyncHealthResourceSample
    {
        public double TimestampMilliseconds { get; set; }
        public bool Recovery { get; set; }
        public int Threads { get; set; }
        public int ProcessThreads { get; set; }
        public int BusyWorkerProxy { get; set; }
        public long QueueDepth { get; set; }
        public long CompletedWorkItems { get; set; }
        public long AllocatedBytes { get; set; }
        public int Gen2Collections { get; set; }
    }

    public sealed class AsyncHealthWindow
    {
        public double WorkloadStartMilliseconds { get; set; }
        public double WorkloadEndMilliseconds { get; set; }
        public double RecoveryEndMilliseconds { get; set; }
        public int ProcessThreadsAtStart { get; set; }
        public long OutstandingAtWorkloadEnd { get; set; }
        public string? ObserverError { get; set; }
        public int RetainedSampleLimit { get; set; } = 100000;
        public long ResourceSamplesDropped { get; set; }
        public ThreadPoolProbeSnapshot Probes { get; set; } = new ThreadPoolProbeSnapshot();
        public AsyncHealthResourceSample[] Resources { get; set; } =
            Array.Empty<AsyncHealthResourceSample>();
    }

    /// <summary>
    /// Dedicated producer; never uses a ThreadPool timer or waits on pool continuations.
    /// </summary>
    public sealed class ThreadPoolHealthObserver : IDisposable
    {
        private readonly AsyncHealthOptions _options;
        private readonly ManualResetEventSlim _ready = new ManualResetEventSlim();
        private readonly ManualResetEventSlim _active = new ManualResetEventSlim();
        private readonly ManualResetEventSlim _stop = new ManualResetEventSlim();
        private readonly List<AsyncHealthResourceSample> _resources =
            new List<AsyncHealthResourceSample>();
        private readonly ThreadPoolProbeRecorder _probes;
        private readonly Process _process = Process.GetCurrentProcess();
        private readonly TimeProvider _timeProvider;
        private readonly long _epoch;
        private int _processThreadsAtStart;
        private Thread? _thread;
        private double _start;
        private double _end;
        private string? _failure;
        private long _resourceSamplesDropped;
        private AsyncHealthWindow? _result;
        private bool _disposed;

        public ThreadPoolHealthObserver(AsyncHealthOptions options)
            : this(options, action => ThreadPool.UnsafeQueueUserWorkItem(
                static callback => callback(), action, preferLocal: false))
        {
        }

        internal ThreadPoolHealthObserver(
            AsyncHealthOptions options, Action<Action> dispatch, TimeProvider? timeProvider = null)
        {
            options.Validate();

            _options = options;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _epoch = _timeProvider.GetTimestamp();

            _probes = new ThreadPoolProbeRecorder(
                options.ProbeCapacity, _timeProvider, _epoch, dispatch);
        }

        private double Timestamp() => _timeProvider.GetElapsedTime(_epoch).TotalMilliseconds;

        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_thread != null)
            {
                throw new InvalidOperationException("Observer is single-use.");
            }

            // Start the observer in a dedicated thread so it doesn't interfere with the threadpool.
            _thread = new Thread(Observe) { Name = "Async-health observer", IsBackground = true };
            _thread.Start();

            // Wait for the observer thread to signal readiness before continuing.
            if (!_ready.Wait(TimeSpan.FromSeconds(10)))
            {
                _stop.Set();
                _active.Set();
                throw new TimeoutException("Async-health observer did not start.");
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

                    // Do not enqueue a burst of stale callbacks after producer descheduling.
                    _probes.RequestProbe(ref next, now, _options.CadenceMilliseconds);
                    Capture();
                }
            }
            catch (Exception error)
            {
                // A background-thread exception must survive in the exported result, not crash
                // the process before the measurement and its outstanding probes can be saved.
                _failure = error.GetType().FullName;
                _stop.Set();
            }
        }

        private void Capture()
        {
            if (_resources.Count >= 100000)
            {
                _resourceSamplesDropped++;
                return;
            }

            ThreadPool.GetMaxThreads(out int maximum, out _);
            ThreadPool.GetAvailableThreads(out int available, out _);

            _process.Refresh();
            int processThreads = _process.Threads.Count;

            _resources.Add(new AsyncHealthResourceSample
            {
                TimestampMilliseconds = Timestamp(),
                Threads = ThreadPool.ThreadCount,
                BusyWorkerProxy = maximum - available,
                ProcessThreads = processThreads,
                QueueDepth = ThreadPool.PendingWorkItemCount,
                CompletedWorkItems = ThreadPool.CompletedWorkItemCount,
                AllocatedBytes = GC.GetTotalAllocatedBytes(),
                Gen2Collections = GC.CollectionCount(2)
            });
        }

        public AsyncHealthWindow Complete()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_result != null)
            {
                return _result;
            }

            if (_thread == null)
            {
                throw new InvalidOperationException("Observer has not started.");
            }

            _stop.Set();

            // Probes posted after the _end timestamp are excluded from the final workload window results.
            _end = Timestamp();
            long outstanding = _probes.Outstanding;

            if (!_thread.Join(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Async-health observer did not stop.");
            }

            // Wait for outstanding probes to complete before taking the final snapshot.
            double remaining = _end + _options.RecoveryMilliseconds - Timestamp();
            if (remaining > 0)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(remaining));
            }

            ThreadPoolProbeSnapshot snapshot = _probes.Snapshot();
            _result = new AsyncHealthWindow
            {
                WorkloadStartMilliseconds = _start,
                WorkloadEndMilliseconds = _end,
                ProcessThreadsAtStart = _processThreadsAtStart,
                RecoveryEndMilliseconds = Timestamp(),
                OutstandingAtWorkloadEnd = outstanding,
                ObserverError = _failure,
                ResourceSamplesDropped = _resourceSamplesDropped,
                Probes = snapshot,
                Resources = _resources.ToArray()
            };

            return _result;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_thread != null && _result == null)
            {
                Complete();
            }

            _disposed = true;
            _ready.Dispose();
            _active.Dispose();
            _stop.Dispose();
            _process.Dispose();
        }
    }
}

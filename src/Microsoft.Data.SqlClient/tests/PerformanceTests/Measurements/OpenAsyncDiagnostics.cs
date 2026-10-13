// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public sealed class OpenAsyncDiagnosticSample
    {
        public double TimestampMilliseconds { get; set; }
        public bool AfterWorkload { get; set; }
        public int OutstandingOpens { get; set; }
        public int OwnedConnections { get; set; }
        public int WorkerThreads { get; set; }
        public int BusyWorkerThreads { get; set; }
        public long PendingWorkItems { get; set; }
        public long CompletedWorkItems { get; set; }
        public int ProcessThreads { get; set; }
        public double CpuMilliseconds { get; set; }
        public long WorkingSetBytes { get; set; }
        public long PrivateMemoryBytes { get; set; }
        public long ManagedHeapBytes { get; set; }
        public long HeapSizeAfterLastGcBytes { get; set; }
        public long FragmentedBytes { get; set; }
        public long TotalAllocatedBytes { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
    }

    public sealed class OpenAsyncGcObservation
    {
        public long Index { get; set; }
        public int Generation { get; set; }
        public double ObservedMilliseconds { get; set; }
        public double[] PauseMilliseconds { get; set; } = Array.Empty<double>();
    }

    public sealed class OpenAsyncProbeSample
    {
        public double ScheduledMilliseconds { get; set; }
        public double PostedMilliseconds { get; set; }
        public double ExecutedMilliseconds { get; set; }
    }

    public sealed class OpenAsyncProbeResult
    {
        public int Capacity { get; set; }
        public int Scheduled { get; set; }
        public int Posted { get; set; }
        public int Executed { get; set; }
        public int Dropped { get; set; }
        public int OutstandingAtStop { get; set; }
        public double DrainMilliseconds { get; set; }
        public OpenAsyncProbeSample[] Samples { get; set; } = Array.Empty<OpenAsyncProbeSample>();
    }

    public sealed class OpenAsyncDiagnosticResult
    {
        public int SampleIntervalMilliseconds { get; set; }
        public double WindowEndMilliseconds { get; set; }
        public string GcObservationMethod { get; set; } = "Last-GC polling; intermediate collections can be missed";
        public long UnobservedGcCount { get; set; }
        public OpenAsyncDiagnosticSample[] Timeline { get; set; } = Array.Empty<OpenAsyncDiagnosticSample>();
        public OpenAsyncGcObservation[] GcObservations { get; set; } = Array.Empty<OpenAsyncGcObservation>();
        public OpenAsyncProbeResult UnrelatedWork { get; set; } = new OpenAsyncProbeResult();
    }

    /// <summary>
    /// Diagnostic-only independent observer. ThreadPool starvation must not stop the producer of
    /// unrelated probes; a bounded queue records dropped probes rather than hiding overload.
    /// </summary>
    public sealed class OpenAsyncDiagnosticCollector : IDisposable
    {
        private readonly int _intervalMilliseconds;
        private readonly int _probeCapacity;
        private readonly Action<Action> _dispatch;
        private readonly ManualResetEventSlim _stop = new ManualResetEventSlim();
        private readonly ManualResetEventSlim _ready = new ManualResetEventSlim();
        private readonly TaskCompletionSource<bool> _drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<OpenAsyncDiagnosticSample> _timeline = new List<OpenAsyncDiagnosticSample>();
        private readonly List<OpenAsyncGcObservation> _collections = new List<OpenAsyncGcObservation>();
        private readonly ConcurrentQueue<OpenAsyncProbeSample> _probes = new ConcurrentQueue<OpenAsyncProbeSample>();
        private Thread? _thread;
        private Func<(int Outstanding, int Owned)>? _snapshot;
        private Exception? _failure;
        private OpenAsyncDiagnosticResult? _result;
        private long _epoch;
        private long _lastGcIndex;
        private long _unobservedGc;
        private int _scheduled, _posted, _executed, _dropped, _pending;
        private int _offeringStopped;
        private bool _disposed;

        public OpenAsyncDiagnosticCollector(int intervalMilliseconds = 10, int probeCapacity = 64)
            : this(intervalMilliseconds, probeCapacity,
                action => ThreadPool.UnsafeQueueUserWorkItem(static callback => callback(), action, preferLocal: false))
        {
        }

        internal OpenAsyncDiagnosticCollector(int intervalMilliseconds, int probeCapacity, Action<Action> dispatch)
        {
            if (intervalMilliseconds <= 0 || probeCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds),
                    "Diagnostic interval and probe capacity must be positive.");
            }
            _intervalMilliseconds = intervalMilliseconds;
            _probeCapacity = probeCapacity;
            _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        }

        public void Start(long epoch, Func<(int Outstanding, int Owned)> snapshot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_thread != null)
            {
                throw new InvalidOperationException("The diagnostic collector can only start once.");
            }
            _epoch = epoch;
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _lastGcIndex = GC.GetGCMemoryInfo().Index;
            _thread = new Thread(Observe) { IsBackground = true, Name = "OpenAsync diagnostic observer" };
            _thread.Start();
            if (!_ready.Wait(TimeSpan.FromSeconds(10)))
            {
                _stop.Set();
                throw new TimeoutException("The independent diagnostic observer did not start.");
            }
            ThrowIfFailed();
        }

        private double Timestamp() => Stopwatch.GetElapsedTime(_epoch).TotalMilliseconds;

        private void Observe()
        {
            try
            {
                using Process process = Process.GetCurrentProcess();
                double next = Timestamp();
                _ready.Set();
                while (!_stop.IsSet)
                {
                    double now = Timestamp();
                    if (now < next)
                    {
                        _stop.Wait(TimeSpan.FromMilliseconds(next - now));
                        continue;
                    }
                    Capture(process);
                    // Account for every elapsed tick, including observer scheduling delays.
                    do
                    {
                        PostProbe(next);
                        next += _intervalMilliseconds;
                    } while (next <= now && !_stop.IsSet);
                }
            }
            catch (Exception error) when (error is InvalidOperationException || error is Win32Exception ||
                error is NotSupportedException)
            {
                _failure = error;
                _ready.Set();
                _stop.Set();
            }
        }

        private void Capture(Process process, bool afterWorkload = false)
        {
            process.Refresh();
            GCMemoryInfo gc = GC.GetGCMemoryInfo();
            var activity = _snapshot!();
            ThreadPool.GetMaxThreads(out int maximumWorkers, out _);
            ThreadPool.GetAvailableThreads(out int availableWorkers, out _);
            double timestamp = Timestamp();
            _timeline.Add(new OpenAsyncDiagnosticSample
            {
                TimestampMilliseconds = timestamp,
                AfterWorkload = afterWorkload,
                OutstandingOpens = activity.Outstanding,
                OwnedConnections = activity.Owned,
                WorkerThreads = ThreadPool.ThreadCount,
                BusyWorkerThreads = maximumWorkers - availableWorkers,
                PendingWorkItems = ThreadPool.PendingWorkItemCount,
                CompletedWorkItems = ThreadPool.CompletedWorkItemCount,
                ProcessThreads = process.Threads.Count,
                CpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds,
                WorkingSetBytes = process.WorkingSet64,
                PrivateMemoryBytes = process.PrivateMemorySize64,
                ManagedHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
                HeapSizeAfterLastGcBytes = gc.HeapSizeBytes,
                FragmentedBytes = gc.FragmentedBytes,
                TotalAllocatedBytes = GC.GetTotalAllocatedBytes(),
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            });
            if (gc.Index > _lastGcIndex)
            {
                _unobservedGc += gc.Index - _lastGcIndex - 1;
                double[] pauses = new double[gc.PauseDurations.Length];
                for (int i = 0; i < pauses.Length; i++)
                {
                    pauses[i] = gc.PauseDurations[i].TotalMilliseconds;
                }
                _collections.Add(new OpenAsyncGcObservation
                {
                    Index = gc.Index,
                    Generation = gc.Generation,
                    ObservedMilliseconds = timestamp,
                    PauseMilliseconds = pauses
                });
                _lastGcIndex = gc.Index;
            }
        }

        private void PostProbe(double scheduled)
        {
            _scheduled++;
            if (Volatile.Read(ref _pending) >= _probeCapacity)
            {
                _dropped++;
                return;
            }
            var probe = new OpenAsyncProbeSample
            {
                ScheduledMilliseconds = scheduled,
                PostedMilliseconds = Timestamp()
            };
            Interlocked.Increment(ref _pending);
            _posted++;
            _probes.Enqueue(probe);
            _dispatch(() =>
            {
                probe.ExecutedMilliseconds = Timestamp();
                Interlocked.Increment(ref _executed);
                if (Interlocked.Decrement(ref _pending) == 0 && Volatile.Read(ref _offeringStopped) != 0)
                {
                    _drained.TrySetResult(true);
                }
            });
        }

        /// <summary>Stops offering probes, then drains them outside the measured workload interval.</summary>
        public OpenAsyncDiagnosticResult Complete()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_result != null)
            {
                return _result;
            }
            if (_thread == null)
            {
                throw new InvalidOperationException("The diagnostic collector has not started.");
            }
            double end = Timestamp();
            _stop.Set();
            int outstanding = Volatile.Read(ref _pending);
            if (!_thread.Join(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("The diagnostic observer did not stop.");
            }
            long drainStart = Stopwatch.GetTimestamp();
            Volatile.Write(ref _offeringStopped, 1);
            if (Volatile.Read(ref _pending) == 0)
            {
                _drained.TrySetResult(true);
            }
            if (!_drained.Task.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("Unrelated diagnostic work did not drain after the workload.");
            }
            ThrowIfFailed();
            using (Process process = Process.GetCurrentProcess())
            {
                Capture(process, afterWorkload: true);
            }
            if (_scheduled != _posted + _dropped || _posted != _executed)
            {
                throw new InvalidOperationException("Unrelated-work diagnostic accounting did not reconcile.");
            }
            _result = new OpenAsyncDiagnosticResult
            {
                SampleIntervalMilliseconds = _intervalMilliseconds,
                WindowEndMilliseconds = end,
                UnobservedGcCount = _unobservedGc,
                Timeline = _timeline.ToArray(),
                GcObservations = _collections.ToArray(),
                UnrelatedWork = new OpenAsyncProbeResult
                {
                    Capacity = _probeCapacity,
                    Scheduled = _scheduled,
                    Posted = _posted,
                    Executed = _executed,
                    Dropped = _dropped,
                    OutstandingAtStop = outstanding,
                    DrainMilliseconds = Stopwatch.GetElapsedTime(drainStart).TotalMilliseconds,
                    Samples = _probes.ToArray()
                }
            };
            return _result;
        }

        private void ThrowIfFailed()
        {
            if (_failure != null)
            {
                throw new InvalidOperationException("OpenAsync diagnostic observation failed.", _failure);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            try
            {
                if (_thread != null && _result == null)
                {
                    Complete();
                }
            }
            finally
            {
                _disposed = true;
                if (_thread == null || !_thread.IsAlive)
                {
                    _stop.Dispose();
                    _ready.Dispose();
                }
            }
        }
    }
}

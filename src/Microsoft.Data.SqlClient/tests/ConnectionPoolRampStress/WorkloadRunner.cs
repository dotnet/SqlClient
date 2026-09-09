// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ConnectionPoolRampStress;

internal sealed record FailureCount(SafeFailure Failure, long Count);
internal sealed record SampleResult(Outcome Outcome, double MeasurementSeconds, double DrainSeconds,
    Measurements Measured, Measurements Drain, int RequestedWorkers, int StartedWorkers,
    int PeakOutstandingCalls, long CompletedRounds, bool IncompleteRound, IReadOnlyList<FailureCount> Failures,
    Trend Trend)
{
    public InitialRamp? InitialRamp { get; init; }
}

internal sealed record InitialRamp(int SettledAttempts, int SuccessfulOpens, bool Complete,
    double Seconds, double OpensPerSecond);

internal sealed class WorkloadRunner
{
    private readonly Settings _settings;
    private readonly Sample _sample;
    private readonly IConnectionFactory _factory;
    private readonly IClock _clock;
    private readonly MetricStore _metrics;
    private readonly MetricStore.WorkerMetrics _coordinatorMetrics;
    private readonly PhysicalCounters _counters;
    private readonly CancellationTokenSource _cancel = new();
    private readonly AutoResetEvent _roundCompleted = new(false);
    private readonly object _failureGate = new();
    private readonly Dictionary<SafeFailure, long> _failures = new();
    private readonly Worker[] _workers;
    private volatile bool _stop;
    private volatile bool _shutdown;
    private volatile bool _externalStop;
    private volatile bool _measurementStarted;
    private int _pending;
    private int _finished;
    private int _active;
    private int _started;
    private int _inFlight;
    private int _peak;
    private int _failed;
    private long _roundOpens;
    private long _rounds;
    private double _roundGate;
    private double _lastCompletion;
    private int _initialSettled;
    private int _initialSuccesses;
    private double _initialLastCompletion;

    public WorkloadRunner(Settings settings, Sample sample, IConnectionFactory factory,
        PhysicalCounters counters, IClock? clock = null)
    {
        _settings = settings;
        _sample = sample;
        _factory = factory;
        _counters = counters;
        _clock = clock ?? new MonotonicClock();
        _metrics = new(settings, _clock);
        _coordinatorMetrics = _metrics.NewWorker();
        _workers = Enumerable.Range(0, sample.Concurrency)
            .Select(_ => new Worker(_metrics.NewWorker())).ToArray();
    }

    // Called by the dedicated stdin reader. It must not invoke cancellation callbacks.
    public void RequestStop()
    {
        _externalStop = true;
        if (_measurementStarted) _metrics.StopAdmission();
        _stop = true;
    }

    private bool Admitting => !_stop && _metrics.Elapsed < _metrics.MeasurementEnd;

    public SampleResult Run(Action<IntervalResult> progress, Action? ready = null)
    {
        bool incomplete = false;
        bool started = false;
        bool roundActive = false;
        double stopAt = double.NaN;
        double end = 0;
        double nextReport = _settings.IntervalSeconds;
        Thread? cancellationThread = null;
        try
        {
            PrepareWorkers();
            ready?.Invoke();
            _metrics.Origin = _clock.Seconds;
            _roundGate = _clock.Seconds;
            started = true;
            _measurementStarted = true;
            if (!Admitting)
            {
                _shutdown = true;
                foreach (Worker worker in _workers) worker.Gate.Release();
            }
            else if (_sample.Workload == Workload.ColdRamps)
            {
                DispatchRound();
                roundActive = true;
            }
            else
            {
                foreach (Worker worker in _workers) worker.Gate.Release();
            }

            while (true)
            {
                double elapsed = _metrics.Elapsed;
                if (!Admitting && double.IsNaN(stopAt))
                {
                    _stop = true;
                    _metrics.StopAdmission();
                    stopAt = _metrics.MeasurementEnd;
                }
                if (_sample.Workload == Workload.ColdRamps && roundActive && Volatile.Read(ref _pending) == 0)
                {
                    double rampSeconds = Math.Max(0, Volatile.Read(ref _lastCompletion) - _roundGate);
                    _coordinatorMetrics.Add(Metric.Ramp, rampSeconds * 1000, _roundOpens);
                    CloseRound();
                    _coordinatorMetrics.Add(Metric.Round, (_clock.Seconds - _roundGate) * 1000);
                    _rounds++;
                    roundActive = false;
                    if (Admitting)
                    {
                        DispatchRound();
                        roundActive = true;
                    }
                }
                elapsed = _metrics.Elapsed;
                if (elapsed >= nextReport)
                {
                    foreach (IntervalResult interval in _metrics.Flush(elapsed, ReadResources()))
                        progress(interval with { CumulativeFailures = SnapshotFailures() });
                    nextReport = elapsed + _settings.IntervalSeconds;
                }
                bool drained = _sample.Workload == Workload.ColdRamps
                    ? !roundActive
                    : Volatile.Read(ref _finished) == _workers.Length;
                if (!double.IsNaN(stopAt) && drained) break;
                if (!double.IsNaN(stopAt) && elapsed >= stopAt + _settings.DrainSeconds)
                {
                    incomplete = true;
                    break;
                }
                // Wake immediately on round completion, with bounded deadline/report checks.
                _roundCompleted.WaitOne(5);
            }
        }
        catch (Exception exception)
        {
            RecordFailure(exception, setup: !started);
            incomplete = true;
        }
        finally
        {
            _stop = true;
            _shutdown = true;
            if (incomplete || _externalStop)
            {
                // Cancellation callbacks may block. Never run them on the timing thread.
                cancellationThread = new Thread(() =>
                {
                    try { _cancel.Cancel(); }
                    catch (Exception exception) { RecordFailure(exception); }
                }) { IsBackground = true, Name = "ramp-cancel" };
                cancellationThread.Start();
            }
            foreach (Worker worker in _workers) worker.Gate.Release();
            if (!roundActive)
            {
                // Bounded worker shutdown, outside the measured window.
                double until = _clock.Seconds + _settings.CleanupSeconds;
                while (Volatile.Read(ref _finished) != _workers.Length && _clock.Seconds < until) Thread.Sleep(5);
                incomplete |= Volatile.Read(ref _finished) != _workers.Length;
            }
            else
            {
                // Only settled workers may be disposed here. In-flight instances remain owned
                // by their workers and the isolated process is the final containment boundary.
                foreach (Worker worker in _workers)
                    if (Volatile.Read(ref worker.Settled)) DisposeHeld(worker);
            }
            if (!started) _metrics.Origin = _clock.Seconds;
            end = _metrics.Elapsed;
            if (Volatile.Read(ref _finished) == _workers.Length)
            {
                foreach (Worker worker in _workers) worker.Gate.Dispose();
                _roundCompleted.Dispose();
                if (cancellationThread is null || cancellationThread.Join(0)) _cancel.Dispose();
            }
        }
        incomplete |= _externalStop;
        _metrics.Seal();
        foreach (IntervalResult interval in _metrics.Flush(end, ReadResources(), final: true))
            progress(interval with { CumulativeFailures = SnapshotFailures() });
        double measurement = Math.Min(end, _metrics.MeasurementEnd);
        double drain = Math.Max(0, end - _metrics.MeasurementEnd);
        Outcome outcome = !started ? Outcome.SetupFailure : incomplete ? Outcome.Incomplete : _failed != 0 ? Outcome.Failed : Outcome.Success;
        return new(outcome, measurement, drain, _metrics.Total(false, measurement), _metrics.Total(true, drain),
            _workers.Length, _started, _peak, _rounds, roundActive, SnapshotFailures(), Trend.Calculate(_metrics.Intervals))
        {
            InitialRamp = new(_initialSettled, _initialSuccesses, _initialSettled == _workers.Length,
                Math.Max(0, _initialLastCompletion - _metrics.Origin),
                MetricBucket.Rate(_initialSuccesses, _initialLastCompletion - _metrics.Origin))
        };
    }

    private void PrepareWorkers()
    {
        foreach (Worker worker in _workers)
        {
            if (_sample.Caller == CallerMode.SyncDedicated)
            {
                new Thread(() => DedicatedWorker(worker)) { IsBackground = true, Name = "ramp-worker" }.Start();
            }
            else
            {
                // One logical task per worker, not one Task.Run per open or cold round.
                _ = LogicalWorker(worker);
            }
        }
        if (_sample.Caller == CallerMode.SyncDedicated)
        {
            double until = _clock.Seconds + _settings.StartupSeconds;
            while (_workers.Any(w => !Volatile.Read(ref w.Ready)))
            {
                if (_clock.Seconds >= until) throw new TimeoutException();
                Thread.Sleep(5);
            }
        }
    }

    private void DispatchRound()
    {
        _roundOpens = 0;
        _pending = _workers.Length;
        _roundGate = _clock.Seconds;
        _lastCompletion = _roundGate;
        foreach (Worker worker in _workers)
        {
            worker.Settled = false;
            worker.Gate.Release();
        }
    }

    private async Task LogicalWorker(Worker worker)
    {
        try
        {
            while (true)
            {
                await worker.Gate.WaitAsync().ConfigureAwait(false);
                if (_shutdown) break;
                StartWorker(worker);
                if (_sample.Workload == Workload.ColdRamps)
                {
                    await Attempt(worker, _sample.Caller == CallerMode.Async, hold: true).ConfigureAwait(false);
                    if (_shutdown) DisposeHeld(worker);
                    Interlocked.Decrement(ref _active);
                    Volatile.Write(ref worker.Settled, true);
                    if (Interlocked.Decrement(ref _pending) == 0) _roundCompleted.Set();
                }
                else
                {
                    while (Admitting)
                        await Attempt(worker, _sample.Caller == CallerMode.Async, hold: false).ConfigureAwait(false);
                    Interlocked.Decrement(ref _active);
                    break;
                }
            }
        }
        catch (Exception exception) { RecordFailure(exception); }
        finally { Interlocked.Increment(ref _finished); }
    }

    private void DedicatedWorker(Worker worker)
    {
        worker.Ready = true;
        try
        {
            while (true)
            {
                worker.Gate.Wait();
                if (_shutdown) break;
                StartWorker(worker);
                if (_sample.Workload == Workload.ColdRamps)
                {
                    Attempt(worker, asynchronous: false, hold: true).GetAwaiter().GetResult();
                    if (_shutdown) DisposeHeld(worker);
                    Interlocked.Decrement(ref _active);
                    Volatile.Write(ref worker.Settled, true);
                    if (Interlocked.Decrement(ref _pending) == 0) _roundCompleted.Set();
                }
                else
                {
                    while (Admitting) Attempt(worker, asynchronous: false, hold: false).GetAwaiter().GetResult();
                    Interlocked.Decrement(ref _active);
                    break;
                }
            }
        }
        catch (Exception exception) { RecordFailure(exception); }
        finally { Interlocked.Increment(ref _finished); }
    }

    private void StartWorker(Worker worker)
    {
        if (!worker.Started)
        {
            worker.Started = true;
            Interlocked.Increment(ref _started);
        }
        Interlocked.Increment(ref _active);
        worker.Metrics.Add(Metric.WorkerStart, (_clock.Seconds - _roundGate) * 1000);
    }

    private async Task Attempt(Worker worker, bool asynchronous, bool hold)
    {
        double cycleStart = _clock.Seconds;
        double openStart = cycleStart;
        IConnection? connection = null;
        bool opened = false;
        bool calling = false;
        double openCompleted = cycleStart;
        worker.Metrics.Add(Metric.Attempt);
        try
        {
            connection = _factory.Create();
            int calls = Interlocked.Increment(ref _inFlight);
            calling = true;
            int previous;
            do
            {
                previous = Volatile.Read(ref _peak);
                if (calls <= previous) break;
            } while (Interlocked.CompareExchange(ref _peak, calls, previous) != previous);
            openStart = _clock.Seconds;
            try
            {
                if (asynchronous) await connection.OpenAsync(_cancel.Token).ConfigureAwait(false);
                else connection.Open();
                opened = true;
            }
            finally
            {
                openCompleted = _clock.Seconds;
                Interlocked.Decrement(ref _inFlight);
                calling = false;
            }
            double completed = openCompleted;
            worker.Metrics.Add(Metric.Open, (completed - openStart) * 1000);
            worker.Metrics.Add(Metric.GateCompletion, (completed - _roundGate) * 1000);
            if (hold)
            {
                worker.Held = connection;
                connection = null;
                Interlocked.Increment(ref _roundOpens);
            }
        }
        catch (Exception exception)
        {
            if (!calling && openCompleted == cycleStart) openCompleted = _clock.Seconds;
            worker.Metrics.Add(Metric.FailedOpen, (openCompleted - openStart) * 1000);
            RecordFailure(exception);
        }
        finally
        {
            if (calling) Interlocked.Decrement(ref _inFlight);
            double completed = openCompleted;
            double previous;
            do
            {
                previous = Volatile.Read(ref _lastCompletion);
                if (completed <= previous) break;
            } while (Interlocked.CompareExchange(ref _lastCompletion, completed, previous) != previous);
            if (!worker.FirstAttemptSettled)
            {
                worker.FirstAttemptSettled = true;
                if (opened) Interlocked.Increment(ref _initialSuccesses);
                double initialPrevious;
                do
                {
                    initialPrevious = Volatile.Read(ref _initialLastCompletion);
                    if (completed <= initialPrevious) break;
                } while (Interlocked.CompareExchange(ref _initialLastCompletion, completed, initialPrevious) != initialPrevious);
                Interlocked.Increment(ref _initialSettled);
            }
            bool disposed = true;
            if (connection is not null)
            {
                try { connection.Dispose(); }
                catch (Exception exception) { disposed = false; RecordFailure(exception); }
            }
            if (!hold && opened && disposed)
                worker.Metrics.Add(Metric.Cycle, (_clock.Seconds - cycleStart) * 1000);
        }
    }

    private void CloseRound()
    {
        double start = _clock.Seconds;
        foreach (Worker worker in _workers) DisposeHeld(worker);
        try { _factory.ClearPool(); }
        catch (Exception exception) { RecordFailure(exception); }
        _coordinatorMetrics.Add(Metric.Cleanup, (_clock.Seconds - start) * 1000);
    }

    private void DisposeHeld(Worker worker)
    {
        IConnection? held = Interlocked.Exchange(ref worker.Held, null);
        if (held is null) return;
        try { held.Dispose(); }
        catch (Exception exception) { RecordFailure(exception); }
    }

    private void RecordFailure(Exception exception, bool setup = false)
    {
        Interlocked.Increment(ref _failed);
        _metrics.StopAdmission();
        _stop = true;
        lock (_failureGate)
        {
            SafeFailure failure = Failure.Describe(exception, setup);
            if (_failures.Count >= 64 && !_failures.ContainsKey(failure))
                failure = new("OtherException", null, "unclassified");
            _failures.TryGetValue(failure, out long count);
            _failures[failure] = count + 1;
        }
    }

    private Resources ReadResources() => _counters.Read(Volatile.Read(ref _active), Volatile.Read(ref _inFlight),
        Volatile.Read(ref _peak), Volatile.Read(ref _started)) with { SampleElapsedSeconds = _metrics.Elapsed };

    private List<FailureCount> SnapshotFailures()
    {
        lock (_failureGate)
            return _failures.Select(pair => new FailureCount(pair.Key, pair.Value)).ToList();
    }

    private sealed class Worker(MetricStore.WorkerMetrics metrics)
    {
        public readonly SemaphoreSlim Gate = new(0);
        public readonly MetricStore.WorkerMetrics Metrics = metrics;
        public IConnection? Held;
        public bool Started;
        public bool Ready;
        public bool Settled;
        public bool FirstAttemptSettled;
    }
}

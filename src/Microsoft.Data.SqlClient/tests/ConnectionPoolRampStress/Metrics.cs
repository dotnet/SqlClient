// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace ConnectionPoolRampStress;

internal interface IClock
{
    double Seconds { get; }
}

internal sealed class MonotonicClock : IClock
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    public double Seconds => _watch.Elapsed.TotalSeconds;
}

internal sealed record Distribution(long Count, double? P50Milliseconds, double? P95Milliseconds,
    double? P99Milliseconds, double MaxMilliseconds)
{
    public double TotalMilliseconds { get; init; }
    public double? MeanMilliseconds => Count == 0 ? null : TotalMilliseconds / Count;
}

internal sealed class Histogram
{
    // 1 microsecond floor, 8% logarithmic buckets, overflow in the last bucket.
    internal const int BucketCount = 320;
    private readonly long[] _counts = new long[BucketCount];
    public long Count { get; private set; }
    public double Maximum { get; private set; }
    public double Sum { get; private set; }

    public void Add(double milliseconds)
    {
        milliseconds = Math.Max(0, milliseconds);
        int index = milliseconds <= 0.001 ? 0 :
            Math.Min(BucketCount - 1, (int)Math.Ceiling(Math.Log(milliseconds / 0.001, 1.08)));
        _counts[index]++;
        Count++;
        Maximum = Math.Max(Maximum, milliseconds);
        Sum += milliseconds;
    }

    public void Merge(Histogram other)
    {
        for (int i = 0; i < BucketCount; i++) _counts[i] += other._counts[i];
        Count += other.Count;
        Maximum = Math.Max(Maximum, other.Maximum);
        Sum += other.Sum;
    }

    public double? Percentile(double percentile)
    {
        if (percentile <= 0 || percentile > 1) throw new ArgumentOutOfRangeException(nameof(percentile));
        if (Count == 0) return null;
        long rank = (long)Math.Ceiling(Count * percentile);
        long cumulative = 0;
        for (int i = 0; i < BucketCount; i++)
        {
            cumulative += _counts[i];
            if (cumulative >= rank) return i == BucketCount - 1 ? Maximum : Math.Min(Maximum, 0.001 * Math.Pow(1.08, i));
        }
        return Maximum;
    }

    public Distribution Snapshot() => new(Count, Percentile(0.5), Percentile(0.95), Percentile(0.99), Maximum)
    {
        TotalMilliseconds = Sum
    };
}

internal enum Metric { Attempt, Open, FailedOpen, Cycle, WorkerStart, GateCompletion, Ramp, Round, Cleanup }

internal sealed class MetricBucket
{
    public long Attempts;
    public readonly Histogram Opens = new();
    public readonly Histogram FailedOpens = new();
    public readonly Histogram Cycles = new();
    public readonly Histogram WorkerStarts = new();
    public readonly Histogram GateCompletions = new();
    public readonly Histogram Ramps = new();
    public readonly Histogram Rounds = new();
    public readonly Histogram Cleanup = new();
    public long RoundOpens;
    public double RampSeconds;

    public void Add(Metric metric, double milliseconds, long roundOpens)
    {
        switch (metric)
        {
            case Metric.Attempt: Attempts++; break;
            case Metric.Open: Opens.Add(milliseconds); break;
            case Metric.FailedOpen: FailedOpens.Add(milliseconds); break;
            case Metric.Cycle: Cycles.Add(milliseconds); break;
            case Metric.WorkerStart: WorkerStarts.Add(milliseconds); break;
            case Metric.GateCompletion: GateCompletions.Add(milliseconds); break;
            case Metric.Ramp:
                Ramps.Add(milliseconds);
                RoundOpens += roundOpens;
                RampSeconds += milliseconds / 1000;
                break;
            case Metric.Round: Rounds.Add(milliseconds); break;
            case Metric.Cleanup: Cleanup.Add(milliseconds); break;
        }
    }

    public void Merge(MetricBucket other)
    {
        Attempts += other.Attempts;
        Opens.Merge(other.Opens);
        FailedOpens.Merge(other.FailedOpens);
        Cycles.Merge(other.Cycles);
        WorkerStarts.Merge(other.WorkerStarts);
        GateCompletions.Merge(other.GateCompletions);
        Ramps.Merge(other.Ramps);
        Rounds.Merge(other.Rounds);
        Cleanup.Merge(other.Cleanup);
        RoundOpens += other.RoundOpens;
        RampSeconds += other.RampSeconds;
    }

    public Measurements Snapshot(double seconds) => new(Attempts, Opens.Snapshot(), FailedOpens.Snapshot(),
        Cycles.Snapshot(), WorkerStarts.Snapshot(), GateCompletions.Snapshot(), Ramps.Snapshot(), Rounds.Snapshot(),
        Cleanup.Snapshot(), Rate(Opens.Count, seconds), Rate(Cycles.Count, seconds),
        Rate(RoundOpens, RampSeconds), RampSeconds) { RoundsPerSecond = Rate(Rounds.Count, seconds) };

    public static double Rate(long count, double seconds) => seconds > 0 ? count / seconds : 0;
}

internal sealed record Measurements(long Attempts, Distribution Opens, Distribution FailedOpens,
    Distribution Cycles, Distribution WorkerStartDelay, Distribution GateToCompletion,
    Distribution Ramps, Distribution Rounds, Distribution CloseClear, double OpensPerSecond, double CyclesPerSecond,
    double RampOpensPerSecond, double RampSeconds)
{
    public double RoundsPerSecond { get; init; }
}

internal sealed record IntervalResult(int Index, double StartSeconds, double EndSeconds, bool Startup,
    bool Partial, bool Drain, Measurements Metrics, Resources Resources)
{
    public IReadOnlyList<FailureCount> CumulativeFailures { get; init; } = [];
}

internal sealed record Trend(string Status, double? EarlyOpensPerSecond, double? LateOpensPerSecond,
    double? ThroughputChangePercent, double? EarlyP95Milliseconds, double? LateP95Milliseconds)
{
    public static Trend Calculate(IReadOnlyList<IntervalResult> intervals)
    {
        var complete = intervals.Where(i => !i.Startup && !i.Partial && !i.Drain).ToArray();
        int size = Math.Min(3, complete.Length / 2);
        if (size == 0) return new("insufficient-data", null, null, null, null, null);
        var early = complete.Take(size).ToArray();
        var late = complete.TakeLast(size).ToArray();
        if (early.Sum(i => i.Metrics.Opens.Count) == 0 || late.Sum(i => i.Metrics.Opens.Count) == 0)
            return new("insufficient-data", null, null, null, null, null);
        double first = early.Average(i => i.Metrics.OpensPerSecond);
        double last = late.Average(i => i.Metrics.OpensPerSecond);
        return new("descriptive", first, last, first == 0 ? null : 100 * (last / first - 1),
            early.Average(i => i.Metrics.Opens.P95Milliseconds), late.Average(i => i.Metrics.Opens.P95Milliseconds));
    }
}

internal sealed class MetricStore
{
    private readonly object _gate = new();
    private readonly Dictionary<int, MetricBucket> _closed = new();
    private readonly List<WorkerMetrics> _workers = new();
    private readonly Settings _settings;
    private readonly IClock _clock;
    private int _next;
    private volatile bool _sealed;
    private double _measurementEnd;
    private int MeasurementSlots => (int)Math.Ceiling(MeasurementEnd / _settings.IntervalSeconds);
    public double Origin { get; set; }
    public List<IntervalResult> Intervals { get; } = new();
    public void Seal() => _sealed = true;
    public double MeasurementEnd => Volatile.Read(ref _measurementEnd);
    public void StopAdmission()
    {
        double elapsed = Elapsed;
        double previous;
        do
        {
            previous = MeasurementEnd;
            if (elapsed >= previous) return;
        } while (Interlocked.CompareExchange(ref _measurementEnd, elapsed, previous) != previous);
    }

    public MetricStore(Settings settings, IClock clock)
    {
        _settings = settings;
        _clock = clock;
        _measurementEnd = settings.DurationSeconds;
    }

    public WorkerMetrics NewWorker()
    {
        WorkerMetrics worker = new(this);
        _workers.Add(worker);
        return worker;
    }

    public double Elapsed => Math.Max(0, _clock.Seconds - Origin);
    public int Index(double elapsed) => elapsed < MeasurementEnd
        ? (int)(elapsed / _settings.IntervalSeconds)
        : MeasurementSlots + (int)((elapsed - MeasurementEnd) / _settings.IntervalSeconds);
    public double Start(int index) => index < MeasurementSlots
        ? index * _settings.IntervalSeconds
        : MeasurementEnd + (index - MeasurementSlots) * _settings.IntervalSeconds;
    private double End(int index) => index < MeasurementSlots
        ? Math.Min(MeasurementEnd, Start(index) + _settings.IntervalSeconds)
        : Start(index) + _settings.IntervalSeconds;

    private void Merge(int index, MetricBucket bucket)
    {
        lock (_gate)
        {
            if (!_closed.TryGetValue(index, out MetricBucket? combined))
                _closed.Add(index, combined = new());
            combined.Merge(bucket);
        }
    }

    public IEnumerable<IntervalResult> Flush(double elapsed, Resources resources, bool final = false)
    {
        // Flush under each worker's lock. Completion timestamps are assigned inside that lock,
        // so an already published interval can never receive a late completion.
        int exclusive = Index(elapsed);
        foreach (WorkerMetrics worker in _workers) worker.FlushBefore(exclusive, final);
        int limit = final && elapsed > Start(exclusive) ? exclusive + 1 : exclusive;
        for (; _next < limit; _next++)
        {
            MetricBucket bucket;
            lock (_gate)
            {
                bucket = _closed.Remove(_next, out MetricBucket? found) ? found : new();
            }
            double end = Math.Min(elapsed, End(_next));
            IntervalResult result = new(_next, Start(_next), end, _next == 0,
                end - Start(_next) < _settings.IntervalSeconds - 0.000001,
                _next >= MeasurementSlots, bucket.Snapshot(end - Start(_next)), resources);
            Intervals.Add(result);
            yield return result;
        }
    }

    public Measurements Total(bool drain, double seconds)
    {
        MetricBucket result = new();
        foreach (WorkerMetrics worker in _workers) worker.MergeTotal(result, drain);
        return result.Snapshot(seconds);
    }

    internal sealed class WorkerMetrics
    {
        private readonly MetricStore _store;
        private readonly object _gate = new();
        private MetricBucket _interval = new();
        private readonly MetricBucket _measured = new();
        private readonly MetricBucket _drain = new();
        private int _index = -1;

        public WorkerMetrics(MetricStore store) => _store = store;

        public void Add(Metric metric, double milliseconds = 0, long roundOpens = 0)
        {
            lock (_gate)
            {
                if (_store._sealed) return;
                double elapsed = _store.Elapsed;
                int index = _store.Index(elapsed);
                if (index != _index)
                {
                    if (_index >= 0)
                    {
                        _store.Merge(_index, _interval);
                        _interval = new();
                    }
                    _index = index;
                }
                _interval.Add(metric, milliseconds, roundOpens);
                (elapsed < _store.MeasurementEnd ? _measured : _drain).Add(metric, milliseconds, roundOpens);
            }
        }

        public void FlushBefore(int index, bool final)
        {
            lock (_gate)
            {
                if (_index >= 0 && (_index < index || final))
                {
                    _store.Merge(_index, _interval);
                    _interval = new();
                    _index = -1;
                }
            }
        }

        public void MergeTotal(MetricBucket result, bool drain)
        {
            lock (_gate) result.Merge(drain ? _drain : _measured);
        }
    }
}

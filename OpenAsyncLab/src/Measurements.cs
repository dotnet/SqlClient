using System.Diagnostics;

namespace OpenAsyncLab;

internal sealed record Sample(double Time, Dictionary<string, double?> Values, bool Changed);

internal sealed class Measurements
{
    private readonly object _gate = new();
    private readonly Histogram _open = new();
    private readonly Histogram _cycle = new();
    private readonly Histogram _dispatch = new();
    private readonly Histogram _producer = new();
    private long _success, _errors, _probes, _dropped, _missed;
    private bool _changed;
    private double _lastTime;
    private string? _error;
    private int _inFlight;

    public string? Error
    {
        get
        {
            lock (_gate)
            {
                return _error;
            }
        }
    }

    public void Enter() => Interlocked.Increment(ref _inFlight);
    public void Leave() => Interlocked.Decrement(ref _inFlight);

    public void Opened(double elapsed)
    {
        lock (_gate)
        {
            _success++;
            _open.Record(elapsed);
        }
    }

    public void Completed(double elapsed)
    {
        lock (_gate)
        {
            _cycle.Record(elapsed);
        }
    }

    public void Failed(string error)
    {
        lock (_gate)
        {
            _errors++;
            _error = error;
        }
    }

    public void Probe(double dispatch, double producer)
    {
        lock (_gate)
        {
            _probes++;
            _dispatch.Record(dispatch);
            _producer.Record(producer);
        }
    }

    public void Lost(long dropped, long missed)
    {
        lock (_gate)
        {
            _dropped += dropped;
            _missed += missed;
        }
    }

    public void MarkChange()
    {
        lock (_gate)
        {
            _changed = true;
        }
    }

    public Sample Take(double now, int outstanding, double oldest, int workers, int processThreads)
    {
        lock (_gate)
        {
            double elapsed = now - _lastTime;
            if (elapsed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(now));
            }
            ThreadPool.GetMaxThreads(out int maximum, out _);
            ThreadPool.GetAvailableThreads(out int available, out _);
            var values = new Dictionary<string, double?>
            {
                ["Opens/s"] = _success / elapsed,
                ["Open p50 ~ms"] = _open.Percentile(0.50),
                ["Open p99 ~ms"] = _open.Percentile(0.99),
                ["Cycle p99 ~ms"] = _cycle.Percentile(0.99),
                ["Errors/s"] = _errors / elapsed,
                ["SQL in flight"] = Volatile.Read(ref _inFlight),
                ["Active async workers"] = workers,
                ["Dispatch p99 ~ms"] = _dispatch.Percentile(0.99),
                ["Producer p99 ~ms"] = _producer.Percentile(0.99),
                ["Probes completed/s"] = _probes / elapsed,
                ["Probes outstanding"] = outstanding,
                ["Oldest probe ms"] = oldest,
                ["Probes dropped/s"] = _dropped / elapsed,
                ["Producer missed/s"] = _missed / elapsed,
                ["ThreadPool threads"] = ThreadPool.ThreadCount,
                ["Busy workers (proxy)"] = maximum - available,
                ["ThreadPool queued"] = ThreadPool.PendingWorkItemCount,
                ["Process threads"] = processThreads
            };
            var sample = new Sample(now, values, _changed);
            _lastTime = now;
            _success = _errors = _probes = _dropped = _missed = 0;
            _changed = false;
            _open.Clear();
            _cycle.Clear();
            _dispatch.Clear();
            _producer.Clear();
            return sample;
        }
    }
}

internal sealed class ProbePump : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<long, double> _pending = new();
    private readonly ManualResetEventSlim _stop = new();
    private readonly Measurements _measurements;
    private readonly Stopwatch _clock;
    private readonly Thread _thread;
    private readonly Func<Action, bool> _dispatch;
    private long _sequence;
    private string? _failure;

    public ProbePump(
        Measurements measurements, Stopwatch clock, Func<Action, bool>? dispatch = null)
    {
        _measurements = measurements;
        _clock = clock;
        _dispatch = dispatch ?? (callback => ThreadPool.UnsafeQueueUserWorkItem(
            static action => action(), callback, preferLocal: false));
        _thread = new Thread(Run) { IsBackground = true, Name = "OpenAsyncLab probes" };
        _thread.Start();
    }

    public string? Failure => Volatile.Read(ref _failure);

    public (int Outstanding, double Oldest) Pending()
    {
        lock (_gate)
        {
            return (_pending.Count, _pending.Count == 0 ? 0
                : _clock.Elapsed.TotalMilliseconds - _pending.Values.Min());
        }
    }

    private void Run()
    {
        double next = _clock.Elapsed.TotalMilliseconds;
        while (!_stop.IsSet)
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            if (now < next)
            {
                _stop.Wait(TimeSpan.FromMilliseconds(next - now));
                continue;
            }
            long missed = (long)((now - next) / 25);
            _measurements.Lost(0, missed);
            double scheduled = next;
            next += (missed + 1) * 25;
            long id;
            lock (_gate)
            {
                if (_pending.Count >= 64)
                {
                    _measurements.Lost(1, 0);
                    continue;
                }
                id = ++_sequence;
                _pending.Add(id, now);
            }
            bool queued = _dispatch(() =>
            {
                double executed = _clock.Elapsed.TotalMilliseconds;
                lock (_gate)
                {
                    _pending.Remove(id);
                }
                _measurements.Probe(executed - now, now - scheduled);
            });
            if (!queued)
            {
                lock (_gate)
                {
                    _pending.Remove(id);
                }
                Volatile.Write(ref _failure, "ThreadPool rejected a health probe.");
                return;
            }
        }
    }

    public void Dispose()
    {
        _stop.Set();
        _thread.Join();
        _stop.Dispose();
    }
}

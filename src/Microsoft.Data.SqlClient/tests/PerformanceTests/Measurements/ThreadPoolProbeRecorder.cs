// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Data.SqlClient.PerformanceTests;

internal sealed class ThreadPoolProbe
{
    public double ScheduledMilliseconds { get; set; }
    public double PostedMilliseconds { get; set; }
    public double? ExecutedMilliseconds { get; set; }
}

internal sealed class ThreadPoolProbeSnapshot
{
    public long Total { get; set; }
    public long Posted { get; set; }
    public long Executed { get; set; }
    public long Dropped { get; set; }
    public long Rejected { get; set; }
    public long Skipped { get; set; }
    public long ProducerMissed { get; set; }
    public long Outstanding { get; set; }
    public ThreadPoolProbe[] Samples { get; set; } = [];
}

/// <summary>Bounded probe accounting; late callbacks cannot mutate exported snapshots.</summary>
internal sealed class ThreadPoolProbeRecorder
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly int _sampleLimit;
    private readonly TimeProvider _time;
    private readonly long _origin;
    private readonly Action<Action> _dispatch;
    private readonly List<ThreadPoolProbe> _samples = [];
    private long _total, _posted, _executed, _dropped, _rejected, _skipped, _missed;

    public ThreadPoolProbeRecorder(
        int capacity, TimeProvider time, long origin, Action<Action> dispatch,
        int sampleLimit = 100000)
    {
        if (capacity <= 0 || sampleLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
        _sampleLimit = sampleLimit;
        _time = time;
        _origin = origin;
        _dispatch = dispatch;
    }

    public void RequestProbe(ref double next, double now, int intervalMilliseconds)
    {
        if (now < next)
        {
            return;
        }

        // A delayed producer records missed slots rather than dispatching a catch-up burst.
        long missed = (long)((now - next) / intervalMilliseconds);
        lock (_gate)
        {
            _total += missed;
            _missed += missed;
        }
        PostProbe(next);
        next += (missed + 1) * intervalMilliseconds;
    }

    public void PostProbe(double scheduled)
    {
        ThreadPoolProbe probe;
        lock (_gate)
        {
            _total++;
            if (_samples.Count >= _sampleLimit)
            {
                _skipped++;
                return;
            }
            if (_posted - _executed >= _capacity)
            {
                _dropped++;
                return;
            }

            probe = new ThreadPoolProbe
            {
                ScheduledMilliseconds = scheduled,
                PostedMilliseconds = _time.GetElapsedTime(_origin).TotalMilliseconds
            };
            _samples.Add(probe);
            _posted++;
        }

        try
        {
            _dispatch(() =>
            {
                lock (_gate)
                {
                    probe.ExecutedMilliseconds = _time.GetElapsedTime(_origin).TotalMilliseconds;
                    _executed++;
                }
            });
        }
        catch
        {
            lock (_gate)
            {
                _samples.Remove(probe);
                _posted--;
                _rejected++;
            }
            throw;
        }
    }

    public ThreadPoolProbeSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new ThreadPoolProbeSnapshot
            {
                Total = _total, Posted = _posted, Executed = _executed, Dropped = _dropped,
                Rejected = _rejected, Skipped = _skipped, ProducerMissed = _missed,
                Outstanding = _posted - _executed,
                Samples = _samples.Select(p => new ThreadPoolProbe
                {
                    ScheduledMilliseconds = p.ScheduledMilliseconds,
                    PostedMilliseconds = p.PostedMilliseconds,
                    ExecutedMilliseconds = p.ExecutedMilliseconds
                }).ToArray()
            };
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public sealed class ThreadPoolProbe
    {
        /// <summary>
        /// When the probe was originally scheduled to run.
        /// Could be before PostedMilliseconds if the observer was delayed in requesting the probe.
        /// </summary>
        public double ScheduledMilliseconds { get; set; }
        /// <summary>
        /// When the probe was posted to the thread pool
        /// </summary>
        public double PostedMilliseconds { get; set; }
        /// <summary>
        /// When the probe was actually executed
        /// </summary>
        public double? ExecutedMilliseconds { get; set; }
    }

    public sealed class ThreadPoolProbeSnapshot
    {
        public long Total { get; set; }
        public long Posted { get; set; }
        public long Executed { get; set; }
        public long Dropped { get; set; }
        public long Rejected { get; set; }
        public long Skipped { get; set; }
        public long ProducerMissed { get; set; }
        public long Outstanding { get; set; }
        public ThreadPoolProbe[] Samples { get; set; } = Array.Empty<ThreadPoolProbe>();
    }

    /// <summary>
    /// Shared bounded probe accounting. Snapshots are immutable copies, including callbacks
    /// still outstanding after a drain deadline. TimeProvider and the dispatcher allow deterministic tests.
    /// </summary>
    internal sealed class ThreadPoolProbeRecorder
    {
        private readonly object _gate = new object();
        private readonly int _capacity, _sampleLimit;
        private readonly TimeProvider _timeProvider;
        private readonly long _timestampOrigin;
        private readonly Action<Action> _dispatch;
        private readonly List<ThreadPoolProbe> _samples = new List<ThreadPoolProbe>();

        // All probe opportunities, including missed, dropped, skipped, and rejected probes.
        private long _total;

        // Admitted probes, including both outstanding and executed callbacks; rolled back if dispatch fails.
        private long _posted;

        // Probe callbacks that have recorded an execution timestamp.
        private long _executed;

        // Probes not dispatched because outstanding capacity was reached.
        private long _dropped;

        // Probes whose dispatch threw; removed from posted counts and retained samples.
        private long _rejected;

        // Probes not dispatched because the retained sample limit was reached.
        private long _skipped;

        // Cadence slots skipped because the producer woke after their deadlines.
        private long _missed;

        public ThreadPoolProbeRecorder(int capacity, TimeProvider timeProvider, long timestampOrigin, Action<Action> dispatch,
            int sampleLimit = 100000)
        {
            if (capacity <= 0 || sampleLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
            _sampleLimit = sampleLimit;
            _timeProvider = timeProvider;
            _timestampOrigin = timestampOrigin;
            _dispatch = dispatch;
        }

        public long Outstanding { get { lock (_gate) { return _posted - _executed; } } }

        public void RequestProbe(ref double next, double now, int intervalMilliseconds)
        {
            if (now < next)
            {
                return;
            }

            // Calculate how many intervals have been missed since the last scheduled time.
            long missedIntervals = (long)((now - next) / intervalMilliseconds);
            // Update counters for missed intervals. No-op if missed is zero.
            UpdateCountsForMissedIntervals(missedIntervals);

            PostProbe(next);

            // Skip missed intervals
            next += missedIntervals * intervalMilliseconds;
            next += intervalMilliseconds;
        }

        public void UpdateCountsForMissedIntervals(long count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            lock (_gate)
            {
                _total += count;
                _missed += count;
            }
        }

        public void PostProbe(double scheduled)
        {
            ThreadPoolProbe probe;
            lock (_gate)
            {
                _total++;

                // Limit concurrent probe capacity so that we don't overwhelm the code under test.
                if (_samples.Count >= _sampleLimit)
                {
                    _skipped++;
                    return;
                }

                // Limit the total number of samples to avoid excessive memory usage.
                if (_posted - _executed >= _capacity)
                {
                    _dropped++;
                    return;
                }

                probe = new ThreadPoolProbe
                {
                    ScheduledMilliseconds = scheduled,
                    PostedMilliseconds = _timeProvider.GetElapsedTime(_timestampOrigin).TotalMilliseconds
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
                        probe.ExecutedMilliseconds = _timeProvider.GetElapsedTime(_timestampOrigin).TotalMilliseconds;
                        Interlocked.Increment(ref _executed);
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
                    Total = _total,
                    Posted = _posted,
                    Executed = _executed,
                    Dropped = _dropped,
                    Rejected = _rejected,
                    Skipped = _skipped,
                    ProducerMissed = _missed,
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
}

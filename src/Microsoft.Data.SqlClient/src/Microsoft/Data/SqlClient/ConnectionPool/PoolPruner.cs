// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Threading;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Internal;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// Encapsulates idle connection pruning logic for <see cref="ChannelDbConnectionPool"/>.
    /// Periodically samples the idle connection count and, once enough samples are collected,
    /// computes the median and prunes that many idle connections from the pool.
    /// <para>
    /// This type is only instantiated when the pool can shrink (MinPoolSize &lt; MaxPoolSize) and
    /// idle-timeout based reclamation is enabled (Connection Idle Timeout &gt; 0). When the pool is
    /// fixed-size or idle reclamation is disabled, the pool holds no <see cref="PoolPruner"/> instance.
    /// </para>
    /// </summary>
    internal sealed class PoolPruner : IDisposable
    {
        /// <summary>
        /// Maximum number of idle-count samples collected per pruning window. Bounds the sample
        /// buffer size and the per-window sort cost regardless of how large the configured idle
        /// timeout is. Once a window would need more than this many samples at
        /// <see cref="MinIntervalSeconds"/>, the sampling interval is stretched instead of growing
        /// the buffer, keeping the sample count bounded (the interval itself is separately capped by
        /// <see cref="MaxIntervalSeconds"/>, so for very large idle timeouts the effective window can
        /// be shorter than the configured timeout).
        /// At the <see cref="MinIntervalSeconds"/> floor this supports idle timeouts up to
        /// <c>MaxSampleSize * MinIntervalSeconds</c> = 3000 seconds (50 minutes) before stretching.
        /// </summary>
        internal const int MaxSampleSize = 300;

        /// <summary>
        /// Minimum (default) sampling interval, in seconds. Acts as a floor so the pruning timer
        /// never fires more often than this and preserves the Story 1 cadence for short/typical
        /// idle timeouts (e.g. the default 300s idle timeout yields a 10s interval and 30 samples).
        /// </summary>
        internal const int MinIntervalSeconds = 10;

        /// <summary>
        /// Maximum sampling interval, in seconds (1 day). Purely a defensive guard so the derived
        /// interval handed to <see cref="Timer.Change(TimeSpan, TimeSpan)"/> stays well under its
        /// ~49.7-day limit for pathological idle timeouts (the <c>Connection Idle Timeout</c> keyword
        /// accepts up to <see cref="int.MaxValue"/> seconds). It only engages above ~300-day idle
        /// timeouts, so it never affects any realistic configuration.
        /// </summary>
        internal const int MaxIntervalSeconds = 24 * 60 * 60;

        /// <summary>
        /// The owning connection pool whose idle connections are pruned.
        /// </summary>
        private readonly ChannelDbConnectionPool _pool;

        /// <summary>
        /// One-shot timer that triggers pruning evaluation. Re-armed manually after each
        /// pruning operation to prevent overlapping invocations.
        /// </summary>
        private readonly Timer _timer;

        /// <summary>
        /// The interval between pruning samples/evaluations.
        /// </summary>
        private readonly TimeSpan _samplingInterval;

        /// <summary>
        /// Number of idle count samples to collect before computing the median and pruning.
        /// Equals <c>ceil(idleTimeout / samplingInterval)</c>, bounded by <see cref="MaxSampleSize"/>
        /// (the interval is stretched rather than exceeding the sample cap for large idle timeouts).
        /// </summary>
        private readonly int _sampleSize;

        /// <summary>
        /// Buffer of idle count snapshots, one recorded per timer tick.
        /// Sorted in-place when full to compute the median, then reset for the next window.
        /// </summary>
        private readonly int[] _samples;

        /// <summary>
        /// The 0-based index into the sorted <see cref="_samples"/> array that represents the median.
        /// Always >= 0 because <see cref="DivideRoundingUp"/> returns >= 1.
        /// </summary>
        private readonly int _medianIndex;

        /// <summary>
        /// Whether the pruning timer is currently armed and firing.
        /// Volatile because it is read outside the lock as a fast pre-check.
        /// </summary>
        private volatile bool _timerEnabled;

        /// <summary>
        /// Current write position in the <see cref="_samples"/> buffer.
        /// Only accessed under the <see cref="_timer"/> lock.
        /// </summary>
        private int _sampleIndex;

        /// <summary>
        /// Creates a new pruner for the given pool.
        /// </summary>
        /// <param name="pool">The owning connection pool.</param>
        /// <param name="idleTimeout">
        /// The configured Connection Idle Timeout, used as the pruning window. The sampling interval
        /// and sample count are derived from it so the window covers the idle timeout for typical
        /// values, while the interval stays bounded by <see cref="MaxIntervalSeconds"/> and the sample
        /// count by <see cref="MaxSampleSize"/> (see the constructor body for the exact formula). For
        /// very large idle timeouts both are capped, so the effective window can be shorter than the
        /// configured timeout. The pool only constructs a pruner when idle-timeout based reclamation is
        /// enabled (idleTimeout &gt; 0); a defensive floor keeps the sizing math valid.
        /// </param>
        internal PoolPruner(ChannelDbConnectionPool pool, TimeSpan idleTimeout)
        {
            _pool = pool;

            // The pool only constructs a pruner when IdleTimeout > 0, but floor at one second so
            // the sizing math below always sees a positive window even under unexpected inputs.
            int idleTimeoutSeconds = Math.Max(1, (int)idleTimeout.TotalSeconds);

            // Derive the interval so the window covers the idle timeout with at most MaxSampleSize
            // samples. Short/typical timeouts keep the MinIntervalSeconds cadence (e.g. 300s -> 10s,
            // 30 samples); large timeouts stretch the interval and pin the count at MaxSampleSize. The
            // MaxIntervalSeconds cap is a defensive Timer.Change overflow guard (engages >~300 days).
            // Math.Clamp is unavailable on net462, so clamp explicitly with Min/Max.
            int intervalForMaxSamples = DivideRoundingUp(idleTimeoutSeconds, MaxSampleSize);
            int flooredInterval = Math.Max(intervalForMaxSamples, MinIntervalSeconds);
            int intervalSeconds = Math.Min(flooredInterval, MaxIntervalSeconds);

            int sampleSize = Math.Min(MaxSampleSize, DivideRoundingUp(idleTimeoutSeconds, intervalSeconds));

            if (intervalSeconds != MinIntervalSeconds)
            {
                // The interval was stretched (or clamped) away from the default. Surface the derived
                // cadence so operators can tell why a very large Connection Idle Timeout samples less
                // frequently than the default 10-second interval.
                SqlClientEventSource.Log.TryPoolerTraceEvent(
                    "<prov.PoolPruner|RES|INFO|CPOOL> Idle timeout {0}s derived a pruning interval of {1}s with {2} samples (max {3}).",
                    idleTimeoutSeconds, intervalSeconds, sampleSize, MaxSampleSize);
            }

            _samplingInterval = TimeSpan.FromSeconds(intervalSeconds);
            _sampleSize = sampleSize;

            // Subtract 1 to convert from length to 0-based index. Safe because
            // DivideRoundingUp always returns >= 1, so _sampleSize >= 1.
            _medianIndex = DivideRoundingUp(_sampleSize, 2) - 1;
            _samples = new int[_sampleSize];

            _timer = ADP.UnsafeCreateTimer(OnPruningCallback, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        #region Internal test surface

        /// <summary>Whether the pruning timer is currently armed. Exposed for unit tests.</summary>
        internal bool IsTimerEnabled => _timerEnabled;

        /// <summary>Current write position in the sample buffer. Exposed for unit tests.</summary>
        internal int SampleIndex => _sampleIndex;

        /// <summary>Total number of samples collected per pruning window. Exposed for unit tests.</summary>
        internal int SampleSize => _sampleSize;

        /// <summary>The derived interval between pruning samples/evaluations. Exposed for unit tests.</summary>
        internal TimeSpan SamplingInterval => _samplingInterval;

        /// <summary>Read-only view of the sample buffer contents. Exposed for unit tests.</summary>
        internal ReadOnlySpan<int> Samples => _samples;

        #endregion

        /// <summary>
        /// Enables or disables the pruning timer based on the current pool size relative to MinPoolSize.
        /// Called by the pool after connections are opened or closed.
        /// </summary>
        internal void UpdateTimer()
        {
            if (!_pool.IsRunning)
            {
                return;
            }

            lock (_timer)
            {
                // Re-check after acquiring lock — Shutdown() may have disposed the timer.
                if (!_pool.IsRunning)
                {
                    return;
                }

                int numConnections = _pool.Count;

                if (numConnections > _pool.PoolGroupOptions.MinPoolSize && !_timerEnabled)
                {
                    // Pool grew beyond min — start collecting samples.
                    _timerEnabled = true;
                    _timer.Change(_samplingInterval, Timeout.InfiniteTimeSpan);
                }
                else if (numConnections <= _pool.PoolGroupOptions.MinPoolSize && _timerEnabled)
                {
                    // Pool shrunk back to min — stop pruning, reset sample buffer.
                    _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    _sampleIndex = 0;
                    _timerEnabled = false;
                }
            }
        }

        /// <summary>
        /// Timer callback that samples the idle count and, once enough samples are collected,
        /// prunes idle connections based on the median of recent samples.
        /// </summary>
        internal void OnPruningCallback(object? state)
        {
            int toPrune;

            lock (_timer)
            {
                // Guard against races with Dispose or UpdateTimer disabling the timer.
                if (!_timerEnabled)
                {
                    return;
                }

                // Record the current idle count as a sample.
                _samples[_sampleIndex] = _pool.IdleCount;

                if (_sampleIndex != _sampleSize - 1)
                {
                    // Buffer not full yet — keep collecting, re-arm timer.
                    _sampleIndex++;
                    _timer.Change(_samplingInterval, Timeout.InfiniteTimeSpan);
                    return;
                }

                // Buffer full — sort to find the median, then reset and re-arm.
                // Array.Sort is O(n log n) but acceptable here since _sampleSize is capped at 300.
                Array.Sort(_samples);
                toPrune = _samples[_medianIndex];
                _sampleIndex = 0;
                _timer.Change(_samplingInterval, Timeout.InfiniteTimeSpan);
            }

            // Prune outside the lock to avoid holding it during I/O.
            _pool.PruneConnections(toPrune);
        }

        /// <summary>
        /// Stops the pruning timer and releases resources. Safe to call multiple times;
        /// <see cref="Timer.Dispose()"/> is idempotent and does not throw.
        /// </summary>
        public void Dispose()
        {
            lock (_timer)
            {
                _timerEnabled = false;
                _timer.Dispose();
            }
        }

        /// <summary>
        /// Returns the ceiling of <paramref name="value"/> / <paramref name="divisor"/>
        /// using integer arithmetic. Always returns >= 1 for positive inputs.
        /// </summary>
        internal static int DivideRoundingUp(int value, int divisor) => 1 + (value - 1) / divisor;
    }
}

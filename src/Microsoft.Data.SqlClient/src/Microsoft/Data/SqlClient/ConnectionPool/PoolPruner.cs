// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Threading;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// Encapsulates idle connection pruning logic for <see cref="ChannelDbConnectionPool"/>.
    /// Periodically samples the idle connection count and, once enough samples are collected,
    /// computes the median and prunes that many idle connections from the pool.
    /// <para>
    /// This type is only instantiated when the pool can shrink (MinPoolSize &lt; MaxPoolSize).
    /// When the pool is fixed-size, the pool holds no <see cref="PoolPruner"/> instance.
    /// </para>
    /// </summary>
    internal sealed class PoolPruner : IDisposable
    {
        /// <summary>
        /// Maximum allowed sample buffer size to prevent excessive memory allocation
        /// from very large lifetime values. With a 10-second interval this supports
        /// idle lifetimes up to 3000 seconds (50 minutes).
        /// </summary>
        internal const int MaxSampleSize = 300;

        /// <summary>
        /// Default sampling interval.
        /// </summary>
        private static readonly TimeSpan DefaultSamplingInterval = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Default lifetime window used for sample size calculation when
        /// LoadBalanceTimeout (Connection Lifetime) is zero.
        /// </summary>
        private static readonly TimeSpan DefaultLifetimeWindow = TimeSpan.FromSeconds(300);

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
        /// Equals lifetime / interval (rounded up), clamped to <see cref="MaxSampleSize"/>.
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
        /// <param name="lifetimeWindow">
        /// The connection lifetime window used to compute sample count.
        /// When zero or negative, <see cref="DefaultLifetimeWindow"/> is used.
        /// </param>
        internal PoolPruner(ChannelDbConnectionPool pool, TimeSpan lifetimeWindow)
        {
            _pool = pool;
            _samplingInterval = DefaultSamplingInterval;

            int lifetimeSeconds = (int)lifetimeWindow.TotalSeconds;
            if (lifetimeSeconds <= 0)
            {
                lifetimeSeconds = (int)DefaultLifetimeWindow.TotalSeconds;
            }

            int intervalSeconds = (int)_samplingInterval.TotalSeconds;
            _sampleSize = Math.Min(
                DivideRoundingUp(lifetimeSeconds, intervalSeconds),
                MaxSampleSize);

            // Subtract 1 to convert from length to 0-based index. Safe because
            // DivideRoundingUp always returns >= 1, so _sampleSize >= 1.
            _medianIndex = DivideRoundingUp(_sampleSize, 2) - 1;
            _samples = new int[_sampleSize];

            // Suppress ExecutionContext flow to avoid capturing AsyncLocals onto the timer,
            // which would keep them alive for the lifetime of the pool.
            using (ExecutionContext.SuppressFlow())
            {
                _timer = new Timer(OnPruningCallback, null, Timeout.Infinite, Timeout.Infinite);
            }
        }

        #region Internal test surface

        /// <summary>Whether the pruning timer is currently armed. Exposed for unit tests.</summary>
        internal bool IsTimerEnabled => _timerEnabled;

        /// <summary>Current write position in the sample buffer. Exposed for unit tests.</summary>
        internal int SampleIndex => _sampleIndex;

        /// <summary>Total number of samples collected per pruning window. Exposed for unit tests.</summary>
        internal int SampleSize => _sampleSize;

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
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
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
        /// Stops the pruning timer and releases resources.
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

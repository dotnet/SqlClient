// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Measures the raw per-checkout overhead of the connection pool: a single thread
    /// repeatedly opens and closes a connection against a warm pool, with no contention.
    ///
    /// With only one caller and a pre-warmed pool, every open is a pure pool checkout
    /// (no physical connect, no waiting), so this benchmark isolates the CPU and
    /// allocation cost of the pool's acquire/return path itself. It is the low-noise
    /// counterpart to the parallel and contention runners and is the most sensitive
    /// measure of per-operation allocations (watch the Allocated / Gen0 columns) — a key
    /// concern for the new ChannelDbConnectionPool, which aims to avoid extra allocations
    /// on the hot path (issue #3356).
    ///
    /// This overlaps <see cref="ConnectionPoolStressRunner.RapidFireOpenCloseAsync"/> in shape —
    /// the inner loop is identical — but not in purpose, and the two are not
    /// interchangeable. Being single-threaded, this runner has no scheduling or wake-up
    /// component, which makes it far more sensitive: its sync and async variants have
    /// agreed to within 0.3 percentage points, whereas the concurrent runner's own
    /// duplicate-workload parameter pairs have disagreed by around 20. Use this one to
    /// decide whether per-checkout cost or allocation moved, and the concurrent runner to
    /// decide whether hand-off between threads moved. A regression there with a flat result
    /// here points at scheduling rather than at the checkout path.
    ///
    /// Adding a Parallelism=1 case to that runner would not replace this one. Its
    /// <c>[Params]</c> are class-wide, so a single-threaded row would also be generated for
    /// benchmarks it makes meaningless (<c>MixedSyncAsyncContention</c> would have no sync
    /// worker left to mix in), and this runner's higher operation count and separate
    /// iteration settings are what keep the signal clean.
    ///
    /// The pool implementation (legacy vs V2) is a process-level choice — see the remarks
    /// on <see cref="ConnectionPoolStressRunner"/>. Run twice (UseConnectionPoolV2 false
    /// then true) to compare.
    ///
    /// Related issue: #3356
    /// </summary>
    public class ConnectionPoolChurnRunner : BaseRunner
    {
        /// <summary>
        /// Number of sequential open/close operations performed per invocation. Kept high
        /// so each measured invocation captures many pool checkouts, yielding a stable
        /// per-operation cost.
        /// </summary>
        [Params(1000)]
        public int OpsPerInvocation { get; set; }

        /// <summary>
        /// How many idle connections the pool holds while the single caller churns against it.
        /// </summary>
        /// <remarks>
        /// This is not a redundant axis, because the two pools order idle connections
        /// differently: the legacy pool pops from a <c>ConcurrentStack</c> (LIFO) while the V2
        /// pool reads from an unbounded <c>Channel</c> (FIFO). At depth 1 that difference is
        /// invisible, since both hand back the only connection there is. At a realistic depth
        /// the legacy pool keeps returning the connection just released — one hot object — while
        /// the V2 pool cycles through every idle connection in turn, touching all of their
        /// buffers and parser state. Depth is therefore the axis that exposes reuse locality,
        /// and measuring only depth 1 would hide it entirely.
        /// </remarks>
        [Params(1, 100)]
        public int PoolDepth { get; set; }

        private string _connectionString;

        [GlobalSetup]
        public void Setup()
        {
            Console.WriteLine(
                "[ConnectionPoolChurnRunner] Pool implementation: " +
                (s_config.UseConnectionPoolV2
                    ? "ChannelDbConnectionPool (V2)"
                    : "WaitHandleDbConnectionPool (legacy)"));

            var builder = new SqlConnectionStringBuilder(s_config.ConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 100,
                // Pin the floor at the requested depth so pruning cannot shrink the pool back
                // down mid-run and change what the benchmark is measuring.
                MinPoolSize = PoolDepth,
                // Matches ConnectionPoolStressRunner. At the larger PoolDepth, setup establishes
                // a hundred physical connections back to back, and the default 15s is tight for
                // that against a loaded remote server.
                ConnectTimeout = 60
            };
            _connectionString = builder.ConnectionString;

            PrewarmPool();
        }

        /// <summary>
        /// Fills the pool with <see cref="PoolDepth"/> idle connections before measurement, so
        /// every measured open is a pure checkout.
        /// </summary>
        /// <remarks>
        /// All connections are opened before any is released. Releasing as we go would let the
        /// pool hand the same idle connection straight back and create only one, which would
        /// silently collapse every depth to 1. <c>MinPoolSize</c> alone is not enough either:
        /// both pools backfill it on a background task, so the first measured iteration would
        /// otherwise still be racing that warm-up.
        /// </remarks>
        private void PrewarmPool()
        {
            var warm = new SqlConnection[PoolDepth];
            try
            {
                for (int i = 0; i < warm.Length; i++)
                {
                    warm[i] = new SqlConnection(_connectionString);
                    warm[i].Open();
                }
            }
            finally
            {
                foreach (var conn in warm)
                {
                    conn?.Dispose();
                }
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            using var conn = new SqlConnection(_connectionString);
            SqlConnection.ClearPool(conn);
        }

        [Benchmark]
        public void RapidOpenCloseSingleThread()
        {
            for (int i = 0; i < OpsPerInvocation; i++)
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                // Dispose returns the connection to the pool.
            }
        }

        [Benchmark]
        public async Task RapidOpenCloseSingleThreadAsync()
        {
            for (int i = 0; i < OpsPerInvocation; i++)
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                // Dispose returns the connection to the pool.
            }
        }
    }
}

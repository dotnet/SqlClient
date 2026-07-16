// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Measures the throughput of opening connections in parallel — the core scenario
    /// the channel-based connection pool (ChannelDbConnectionPool / UseConnectionPoolV2)
    /// is designed to improve (see issue #3356). The design goals are parallel connection
    /// opening, reduced thread contention, and lower managed threadpool pressure, so this
    /// runner sweeps the dimensions that expose those characteristics:
    ///
    /// - <see cref="PoolIsWarm"/>: cold pool (physical connects dominate) vs. warm pool
    ///   (checkout throughput dominates). The warm-pool case is where the new pool shows
    ///   the largest wins.
    /// - <see cref="Async"/>: sync vs. async callers. The new pool follows async best
    ///   practices, so async checkout is a primary target for improvement.
    /// - <see cref="Pooling"/>: pooling on vs. off. Pooling off is the no-pool baseline.
    /// - <see cref="NumConnectionsToOpen"/>: how many connections are opened in parallel.
    ///
    /// The pool implementation itself (legacy WaitHandleDbConnectionPool vs. new
    /// ChannelDbConnectionPool) is NOT a benchmark parameter. The UseConnectionPoolV2
    /// AppContext switch is read and cached the first time a pool is created, and every
    /// benchmark case runs in the same process under the in-process toolchain, so the
    /// implementation cannot be toggled per iteration. It is selected once per process via
    /// the "UseConnectionPoolV2" flag in runnerconfig.jsonc. To compare the two pools, run
    /// the benchmark twice: once with UseConnectionPoolV2=false (legacy) and once with
    /// UseConnectionPoolV2=true (new). Likewise, native vs. managed SNI is selected once
    /// per process via the "UseManagedSniOnWindows" config flag (Windows-only).
    ///
    /// Pair this with the ThreadingDiagnoser (enabled in BenchmarkConfig) to observe
    /// threadpool completed-work-item counts and lock contention across the two pools.
    ///
    /// Related issues: #601, #979, #3356
    /// </summary>
    public class ConnectionPoolStressRunner : BaseRunner
    {
        public enum AsyncBehavior
        {
            Sync,
            Async
        }

        /// <summary>
        /// Whether the pool is pre-warmed (connections opened and returned) before the
        /// measured run. A warm pool isolates checkout/return throughput; a cold pool
        /// includes the cost of establishing physical connections.
        /// </summary>
        [ParamsAllValues]
        public bool PoolIsWarm { get; set; }

        /// <summary>
        /// Whether callers open connections synchronously or asynchronously.
        /// </summary>
        [ParamsAllValues]
        public AsyncBehavior Async { get; set; }

        /// <summary>
        /// Whether connection pooling is enabled. When disabled, every open establishes a
        /// new physical connection — the no-pool baseline.
        /// </summary>
        [Params(true, false)]
        public bool Pooling { get; set; }

        /// <summary>
        /// Number of connections opened in parallel per measured invocation.
        /// </summary>
        [Params(100)]
        public int NumConnectionsToOpen { get; set; }

        private string _connectionString;
        private IProducerConsumerCollection<SqlConnection> _connections = new ConcurrentBag<SqlConnection>();

        [GlobalSetup]
        public void Setup()
        {
            // Report which pool implementation is active for this process so the results
            // summary is self-describing (the implementation is a process-level choice,
            // not a benchmark parameter — see the class remarks).
            Console.WriteLine(
                "[ConnectionPoolStressRunner] Pool implementation: " +
                (s_config.UseConnectionPoolV2
                    ? "ChannelDbConnectionPool (V2)"
                    : "WaitHandleDbConnectionPool (legacy)"));
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _connections = new ConcurrentBag<SqlConnection>();

            // A fresh WorkstationID per iteration produces a distinct connection string,
            // and therefore a fresh, isolated pool — so a "cold" iteration truly starts
            // with an empty pool and iterations don't share pooled connections.
            var builder = new SqlConnectionStringBuilder(s_config.ConnectionString)
            {
                MaxPoolSize = 1000,
                WorkstationID = Guid.NewGuid().ToString(),
                Pooling = Pooling
            };
            _connectionString = builder.ConnectionString;

            if (PoolIsWarm && Pooling)
            {
                // Open and return NumConnectionsToOpen connections so the measured run is
                // served from a warm pool.
                OpenConnectionsCore(_connectionString, NumConnectionsToOpen, returnToPool: true);
            }
        }

        [IterationCleanup]
        public void Cleanup()
        {
            // Close every connection opened during setup and the measured run, then clear
            // the pool so pooled connections are physically closed before the next iteration.
            if (_connections.TryTake(out var first))
            {
                first.Close();

                while (_connections.TryTake(out var conn))
                {
                    conn.Close();
                }

                SqlConnection.ClearPool(first);
            }
        }

        /// <summary>
        /// Opens <see cref="NumConnectionsToOpen"/> connections in parallel and holds them
        /// open (does not return them to the pool) so the measurement captures the cost of
        /// acquiring that many connections concurrently.
        /// </summary>
        [Benchmark]
        public int OpenConnectionsInParallel()
        {
            return OpenConnectionsCore(_connectionString, NumConnectionsToOpen, returnToPool: false);
        }

        private int OpenConnectionsCore(
            string connectionString,
            int numConnectionsToOpen,
            bool returnToPool)
        {
            var tasks = new Task[numConnectionsToOpen];

            for (int i = 0; i < numConnectionsToOpen; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    var conn = new SqlConnection(connectionString);

                    if (Async is AsyncBehavior.Async)
                    {
                        await conn.OpenAsync(SqlConnectionOverrides.OpenWithoutRetry, CancellationToken.None);
                    }
                    else
                    {
                        conn.Open(SqlConnectionOverrides.OpenWithoutRetry);
                    }

                    _connections.TryAdd(conn);

                    if (returnToPool)
                    {
                        conn.Close();
                    }
                });
            }

            Task.WaitAll(tasks);
            return tasks.Length;
        }
    }
}

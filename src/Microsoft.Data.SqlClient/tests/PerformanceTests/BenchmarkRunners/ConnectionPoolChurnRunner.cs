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
    /// The pool implementation (legacy vs V2) is a process-level choice — see the remarks
    /// on <see cref="ConnectionPoolStressRunner"/>. Run twice (UseConnectionPoolV2 false
    /// then true) to compare.
    ///
    /// Related issue: #3356
    /// </summary>
    public class ConnectionPoolChurnRunner : BaseRunner
    {
        public enum AsyncBehavior
        {
            Sync,
            Async
        }

        /// <summary>
        /// Whether the connection is opened synchronously or asynchronously.
        /// </summary>
        [ParamsAllValues]
        public AsyncBehavior Async { get; set; }

        /// <summary>
        /// Number of sequential open/close operations performed per invocation. Kept high
        /// so each measured invocation captures many pool checkouts, yielding a stable
        /// per-operation cost.
        /// </summary>
        [Params(1000)]
        public int OpsPerInvocation { get; set; }

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
                // Pre-warm a single connection so the very first checkout is served from
                // the pool rather than establishing a physical connection.
                MinPoolSize = 1
            };
            _connectionString = builder.ConnectionString;

            // Force the pool to exist and hold at least one idle connection.
            using var warm = new SqlConnection(_connectionString);
            warm.Open();
            warm.Close();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            using var conn = new SqlConnection(_connectionString);
            SqlConnection.ClearPool(conn);
        }

        [Benchmark]
        public async Task RapidOpenCloseSingleThread()
        {
            for (int i = 0; i < OpsPerInvocation; i++)
            {
                using var conn = new SqlConnection(_connectionString);

                if (Async is AsyncBehavior.Async)
                {
                    await conn.OpenAsync();
                }
                else
                {
                    conn.Open();
                }
                // Dispose returns the connection to the pool.
            }
        }
    }
}

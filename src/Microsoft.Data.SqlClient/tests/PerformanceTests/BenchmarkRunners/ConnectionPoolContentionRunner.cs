// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Measures steady-state pool throughput under concurrent load — the hot path for
    /// real applications such as web servers, where many workers repeatedly check a
    /// connection out of a warm pool, do a small unit of work, and return it.
    ///
    /// This is where the legacy WaitHandleDbConnectionPool and the new
    /// ChannelDbConnectionPool differ most (issue #3356):
    ///
    /// - When the pool is large enough to serve every worker, the benchmark isolates
    ///   checkout/return overhead and lock contention on the hot path.
    /// - When the pool is smaller than the worker count (<see cref="MaxPoolSize"/> less
    ///   than <see cref="Parallelism"/>), workers must wait for a connection to be
    ///   returned. This exercises the waiter wake path: the legacy pool parks on
    ///   WaitHandle.WaitAny (a blocking wait that consumes a threadpool thread for async
    ///   callers), while the new pool awaits the idle channel asynchronously. The
    ///   ThreadingDiagnoser's "Completed Work Items" and "Lock Contentions" columns make
    ///   the difference visible.
    ///
    /// The pool implementation (legacy vs V2) and SNI are process-level choices — see the
    /// remarks on <see cref="ConnectionPoolStressRunner"/>. Run twice (UseConnectionPoolV2
    /// false then true) to compare.
    ///
    /// Related issues: #601, #979, #3356
    /// </summary>
    public class ConnectionPoolContentionRunner : BaseRunner
    {
        /// <summary>
        /// Number of concurrent workers competing for pooled connections.
        /// </summary>
        [Params(50)]
        public int Parallelism { get; set; }

        /// <summary>
        /// Maximum pool size, exercised across three regimes relative to
        /// <see cref="Parallelism"/>: larger than the worker count (idle spare
        /// connections, no contention), equal to it (fully subscribed, no contention), and
        /// smaller than it (pool exhaustion forces workers to wait for a connection to be
        /// returned — back-pressure).
        /// </summary>
        [Params(100, 50, 10)]
        public int MaxPoolSize { get; set; }

        /// <summary>
        /// Number of open/query/close operations each worker performs per invocation.
        /// </summary>
        [Params(20)]
        public int OpsPerWorker { get; set; }

        private string _connectionString;

        [GlobalSetup]
        public void Setup()
        {
            Console.WriteLine(
                "[ConnectionPoolContentionRunner] Pool implementation: " +
                (s_config.UseConnectionPoolV2
                    ? "ChannelDbConnectionPool (V2)"
                    : "WaitHandleDbConnectionPool (legacy)"));

            var builder = new SqlConnectionStringBuilder(s_config.ConnectionString)
            {
                Pooling = true,
                MaxPoolSize = MaxPoolSize,
                MinPoolSize = 0
            };
            _connectionString = builder.ConnectionString;
        }

        [IterationSetup]
        public void IterationSetup()
        {
            // Warm the pool up to MaxPoolSize so the measured run reflects steady-state
            // checkout/return rather than first-time physical connection establishment.
            WarmPool(MaxPoolSize);
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            using var conn = new SqlConnection(_connectionString);
            SqlConnection.ClearPool(conn);
        }

        [Benchmark]
        public Task SteadyStateOpenQueryClose()
        {
            var tasks = new Task[Parallelism];
            for (int i = 0; i < Parallelism; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int op = 0; op < OpsPerWorker; op++)
                    {
                        using var conn = new SqlConnection(_connectionString);
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT 1";
                        _ = cmd.ExecuteScalar();
                        // Dispose returns the connection to the pool.
                    }
                });
            }

            return Task.WhenAll(tasks);
        }

        [Benchmark]
        public Task SteadyStateOpenQueryCloseAsync()
        {
            var tasks = new Task[Parallelism];
            for (int i = 0; i < Parallelism; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (int op = 0; op < OpsPerWorker; op++)
                    {
                        using var conn = new SqlConnection(_connectionString);
                        await conn.OpenAsync();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT 1";
                        _ = await cmd.ExecuteScalarAsync();
                        // Dispose returns the connection to the pool.
                    }
                });
            }

            return Task.WhenAll(tasks);
        }

        private void WarmPool(int count)
        {
            var conns = new SqlConnection[count];
            try
            {
                for (int i = 0; i < count; i++)
                {
                    conns[i] = new SqlConnection(_connectionString);
                    conns[i].Open();
                }
            }
            finally
            {
                // Close and dispose every connection so they return to the pool and
                // are not retained until GC (which would add allocation/GC noise).
                for (int i = 0; i < count; i++)
                {
                    conns[i]?.Close();
                    conns[i]?.Dispose();
                }
            }
        }
    }
}

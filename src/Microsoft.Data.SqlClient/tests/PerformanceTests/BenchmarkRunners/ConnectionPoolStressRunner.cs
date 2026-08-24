// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Stress-tests the connection pool with randomized parallel access patterns:
    /// - Massive concurrent open/close churn
    /// - Randomized hold durations simulating real workloads
    /// - Mixed sync/async callers competing for pooled connections
    /// - Connection reuse with interleaved queries
    /// - Pool exhaustion and recovery under pressure
    ///
    /// Related issues: #601, #979, #3356
    /// </summary>
    public class ConnectionPoolStressRunner : BaseRunner
    {
        private string _connectionString;
        private string _tableName;

        /// <summary>
        /// Number of concurrent tasks hammering the pool.
        /// </summary>
        [Params(10, 20, 25)]
        public int Parallelism { get; set; }

        /// <summary>
        /// Max pool size — controls how many physical connections the pool can hold.
        /// </summary>
        /// <remarks>
        /// Note that every <see cref="Parallelism"/> value is below every value here, so this
        /// limit is only actually reached by <see cref="PoolExhaustionRecovery"/>, which
        /// deliberately oversubscribes it. For the other benchmarks in this class the pool
        /// never saturates and this parameter is close to inert: the V2 pool's idle channel is
        /// unbounded, so a capacity that is never reached changes no behaviour. Treat a spread
        /// between two MaxPoolSize values at the same Parallelism as a noise estimate rather
        /// than a real effect.
        /// </remarks>
        [Params(50, 100)]
        public int MaxPoolSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _connectionString = s_config.ConnectionString +
                $";Pooling=True;Max Pool Size={MaxPoolSize};Min Pool Size=5;Connect Timeout=60";

            // Create a small table for query workloads.
            // Hash the machine name instead of using it verbatim: hostnames can be long enough
            // to push the identifier past SQL Server's 128-character limit. Cast to uint rather
            // than using Math.Abs, which throws OverflowException when the hash is int.MinValue.
            string machineHash = ((uint)Environment.MachineName.GetHashCode()).ToString("x8");
            _tableName = $"[perf_PoolStress_{machineHash}_{Guid.NewGuid():N}]";
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(
                $"CREATE TABLE {_tableName} (Id INT IDENTITY PRIMARY KEY, Val INT)", conn);
            cmd.ExecuteNonQuery();

            // Seed a few rows so SELECT queries return data
            using var insert = new SqlCommand(
                $"INSERT INTO {_tableName} (Val) VALUES (1),(2),(3),(4),(5)", conn);
            insert.ExecuteNonQuery();
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            SqlConnection.ClearAllPools();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand($"DROP TABLE IF EXISTS {_tableName}", conn);
            cmd.ExecuteNonQuery();
            conn.Close();
            SqlConnection.ClearAllPools();
        }

        /// <summary>
        /// Pure open/close churn — every task opens a pooled connection, immediately closes it,
        /// and repeats.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is a <em>scheduling</em> benchmark, not a measure of raw checkout throughput.
        /// Because nothing happens between checkout and return, the tasks stay phase-locked and
        /// the idle channel oscillates around empty, so the inline <c>TryRead</c> fast path
        /// misses and most checkouts park in <c>ReadAsync</c> and resume on a threadpool
        /// continuation. What is measured is dominated by that wake-up cost plus
        /// <see cref="Task.Run(Action)"/> overhead: a pooled checkout costs single-digit
        /// microseconds, while this benchmark attributes tens of microseconds to each one.
        /// </para>
        /// <para>
        /// For the per-checkout cost of the pool's acquire/return path itself, use
        /// <see cref="ConnectionPoolChurnRunner"/>, which runs this same loop single-threaded
        /// against a warm pool. For concurrent behaviour under a realistic workload — where a
        /// connection is held for the duration of a query, so returns and checkouts decorrelate
        /// and the fast path hits — use <see cref="ConnectionPoolContentionRunner"/>. A
        /// regression here alongside flat or improved results in those two indicates a change in
        /// wake-up scheduling, not in checkout cost.
        /// </para>
        /// <para>
        /// Also note that <see cref="IterationCleanup"/> drops the pool between iterations, so
        /// each iteration amortises a cold-start burst of physical connects (a few percent of
        /// the mean). That largely cancels between baseline and current, but it adds variance.
        /// </para>
        /// </remarks>
        [Benchmark]
        public async Task RapidFireOpenClose()
        {
            // NOTE: the Math.Max floor dominates for most [Params] combinations, so total
            // checkouts do NOT scale cleanly with pool capacity — at Parallelism 20 and 25 both
            // MaxPoolSize values run an identical workload. Kept as-is so results stay
            // comparable with previously published runs; see the redesign issue before relying
            // on the MaxPoolSize axis here.
            int iterationsPerTask = Math.Max(20, MaxPoolSize / Math.Max(1, Parallelism) * 4);
            var tasks = new Task[Parallelism];
            for (int i = 0; i < Parallelism; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < iterationsPerTask; j++)
                    {
                        using var conn = new SqlConnection(_connectionString);
                        await conn.OpenAsync();
                        // immediate return to pool
                    }
                });
            }
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Randomized hold — each task opens a connection, holds it for a random duration
        /// (0-50ms), optionally runs a query, then returns it. Simulates realistic mixed
        /// workloads where some connections are held briefly and others longer.
        /// </summary>
        [Benchmark]
        public async Task RandomizedHoldAndQuery()
        {
            var tasks = new Task[Parallelism];
            for (int i = 0; i < Parallelism; i++)
            {
                int seed = i;
                tasks[i] = Task.Run(async () =>
                {
                    var rng = new Random(seed);
                    for (int j = 0; j < 10; j++)
                    {
                        using var conn = new SqlConnection(_connectionString);
                        await conn.OpenAsync();

                        // ~50% of the time, execute a lightweight query while holding the connection
                        if (rng.Next(2) == 0)
                        {
                            using var cmd = new SqlCommand($"SELECT TOP 1 Val FROM {_tableName}", conn);
                            _ = await cmd.ExecuteScalarAsync();
                        }

                        // Random hold time: 0-50ms
                        int holdMs = rng.Next(51);
                        if (holdMs > 0)
                        {
                            await Task.Delay(holdMs);
                        }
                    }
                });
            }
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Mixed sync and async — half the tasks use sync Open/ExecuteReader,
        /// the other half use async. Stresses the pool lock paths that differ
        /// between sync and async checkout.
        /// </summary>
        [Benchmark]
        public async Task MixedSyncAsyncContention()
        {
            var tasks = new Task[Parallelism];
            for (int i = 0; i < Parallelism; i++)
            {
                bool useAsync = i % 2 == 0;
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        using var conn = new SqlConnection(_connectionString);
                        if (useAsync)
                        {
                            await conn.OpenAsync();
                            using var cmd = new SqlCommand($"SELECT COUNT(*) FROM {_tableName}", conn);
                            _ = await cmd.ExecuteScalarAsync();
                        }
                        else
                        {
                            conn.Open();
                            using var cmd = new SqlCommand($"SELECT COUNT(*) FROM {_tableName}", conn);
                            _ = cmd.ExecuteScalar();
                        }
                    }
                });
            }
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Connection reuse with multiple commands — each task opens one connection and
        /// executes many sequential queries before returning it. Measures pool efficiency
        /// when connections are held for multi-step operations (like EF SaveChanges).
        /// </summary>
        [Benchmark]
        public async Task MultiCommandReuse()
        {
            var tasks = new Task[Parallelism];
            for (int i = 0; i < Parallelism; i++)
            {
                int seed = i;
                tasks[i] = Task.Run(async () =>
                {
                    var rng = new Random(seed);
                    using var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync();

                    // Execute a burst of 5-15 commands on the same connection
                    int commandCount = rng.Next(5, 16);
                    for (int c = 0; c < commandCount; c++)
                    {
                        using var cmd = new SqlCommand($"SELECT Val FROM {_tableName} WHERE Id = @id", conn);
                        cmd.Parameters.AddWithValue("@id", rng.Next(1, 6));
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync()) { }
                    }
                });
            }
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Pool exhaustion and recovery — spawns more tasks than MaxPoolSize so some must
        /// wait. Measures how well the pool handles back-pressure when all connections are
        /// checked out and callers are queued.
        /// </summary>
        [Benchmark]
        public async Task PoolExhaustionRecovery()
        {
            // Ensure we exceed pool capacity
            int taskCount = Math.Max(Parallelism, MaxPoolSize * 2);
            var tasks = new Task[taskCount];
            for (int i = 0; i < taskCount; i++)
            {
                int seed = i;
                tasks[i] = Task.Run(async () =>
                {
                    var rng = new Random(seed);
                    using var conn = new SqlConnection(_connectionString);
                    await conn.OpenAsync();

                    // Hold the connection for 10-100ms to create pool pressure
                    using var cmd = new SqlCommand($"SELECT TOP 1 Val FROM {_tableName}", conn);
                    _ = await cmd.ExecuteScalarAsync();

                    await Task.Delay(rng.Next(10, 101));
                });
            }
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Bursty traffic pattern — sends waves of connections with pauses between bursts,
        /// simulating real web server traffic patterns where requests cluster. Each burst
        /// spins up <see cref="Parallelism"/> concurrent tasks so the configured parallelism
        /// level actually drives the observed concurrency.
        /// </summary>
        [Benchmark]
        public async Task BurstyTrafficPattern()
        {
            const int burstCount = 5;

            for (int burst = 0; burst < burstCount; burst++)
            {
                var tasks = new Task[Parallelism];
                for (int i = 0; i < Parallelism; i++)
                {
                    int seed = burst * Parallelism + i;
                    tasks[i] = Task.Run(async () =>
                    {
                        var rng = new Random(seed);
                        using var conn = new SqlConnection(_connectionString);
                        await conn.OpenAsync();

                        // Each connection in the burst does 1-5 queries
                        int queryCount = rng.Next(1, 6);
                        for (int q = 0; q < queryCount; q++)
                        {
                            using var cmd = new SqlCommand($"SELECT Val FROM {_tableName}", conn);
                            using var reader = await cmd.ExecuteReaderAsync();
                            while (await reader.ReadAsync()) { }
                        }
                    });
                }
                await Task.WhenAll(tasks);

                // Brief pause between bursts (simulates request clustering)
                await Task.Delay(5);
            }
        }
    }
}

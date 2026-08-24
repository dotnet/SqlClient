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
    /// - Massive concurrent open/close churn, both sync and async
    /// - Randomized hold durations simulating real workloads
    /// - Mixed sync/async callers competing for pooled connections
    /// - Connection reuse with interleaved queries
    /// - Pool exhaustion and recovery under pressure
    ///
    /// The pool is pre-warmed to full capacity in <see cref="Setup"/> and is deliberately
    /// <em>not</em> cleared between iterations, so these benchmarks measure steady-state
    /// checkout and return rather than physical connection establishment. Establishing
    /// connections costs orders of magnitude more than a pooled checkout (milliseconds versus
    /// microseconds), so leaving any creation in the measured body swamps the pool cost this
    /// class exists to measure. Cold-start behaviour is covered separately and deliberately by
    /// <see cref="ConnectionPoolRampRunner"/>.
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
        /// The pool is pre-warmed to this many connections and pinned there by an equal
        /// <c>Min Pool Size</c>, so this is the number of connections actually resident for the
        /// whole run rather than just a ceiling. Note that every <see cref="Parallelism"/> value
        /// is below every value here, so the limit itself is only reached by
        /// <see cref="PoolExhaustionRecovery"/>, which deliberately oversubscribes it. For the
        /// other benchmarks the pool never saturates, so this parameter mostly varies how many
        /// idle connections the checkout path is choosing among. Treat a spread between two
        /// MaxPoolSize values at the same Parallelism as a noise estimate rather than a real
        /// effect.
        /// </remarks>
        [Params(50, 100)]
        public int MaxPoolSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            // Pin Min Pool Size to Max Pool Size so the pool holds full capacity for the whole
            // run: pruning cannot shrink it back down, and no benchmark body has to establish a
            // physical connection.
            _connectionString = s_config.ConnectionString +
                $";Pooling=True;Max Pool Size={MaxPoolSize};Min Pool Size={MaxPoolSize};Connect Timeout=60";

            // Create a small table for query workloads.
            // Hash the machine name instead of using it verbatim: hostnames can be long enough
            // to push the identifier past SQL Server's 128-character limit. Cast to uint rather
            // than using Math.Abs, which throws OverflowException when the hash is int.MinValue.
            string machineHash = ((uint)Environment.MachineName.GetHashCode()).ToString("x8");
            _tableName = $"[perf_PoolStress_{machineHash}_{Guid.NewGuid():N}]";

            // Scoped so this connection is back in the pool before PrewarmPool runs: that method
            // holds MaxPoolSize connections at once, so an extra live one would hit the cap and
            // block until Connect Timeout.
            {
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

            PrewarmPool();
        }

        /// <summary>
        /// Fills the pool to <see cref="MaxPoolSize"/> live connections before any measurement
        /// starts, so benchmark bodies only ever exercise checkout and return.
        /// </summary>
        /// <remarks>
        /// Every connection is opened before any is released. Releasing as we go would let the
        /// pool hand the same idle connection back repeatedly and create only one, which is the
        /// whole failure this is meant to avoid. <c>Min Pool Size</c> alone is not enough either:
        /// it is backfilled lazily, so the first measured iteration would still pay for creation.
        /// </remarks>
        private void PrewarmPool()
        {
            var warm = new SqlConnection[MaxPoolSize];
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
                // Returns them all to the pool; Min Pool Size keeps them resident from here on.
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
            conn.Open();
            using var cmd = new SqlCommand($"DROP TABLE IF EXISTS {_tableName}", conn);
            cmd.ExecuteNonQuery();
            conn.Close();
            SqlConnection.ClearAllPools();
        }

        /// <summary>
        /// Pure open/close churn — every task opens a pooled connection, immediately closes it,
        /// and repeats. With the pool pre-warmed to full capacity, every open is a pure checkout,
        /// so this measures the pool's acquire/return path under concurrency.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Because nothing happens between checkout and return, the tasks stay phase-locked and
        /// the idle channel oscillates around empty even though the pool is full. That makes this
        /// benchmark unusually sensitive to how a returned connection is handed to a waiting
        /// caller: under the V2 pool an inline <c>TryRead</c> miss parks the caller in
        /// <c>ReadAsync</c>, so it resumes on a threadpool continuation. Read it as a
        /// zero-hold-time worst case for wake-up scheduling, not as typical application
        /// behaviour: real callers do some work while holding a connection, which decorrelates
        /// returns from checkouts and lets the fast path hit.
        /// </para>
        /// <para>
        /// Compare against <see cref="ConnectionPoolChurnRunner"/> for the same loop with no
        /// concurrency, and <see cref="ConnectionPoolContentionRunner"/> for concurrency with a
        /// realistic hold time. A regression here alongside flat or improved results in those two
        /// indicates a change in wake-up scheduling rather than in checkout cost.
        /// </para>
        /// </remarks>
        [Benchmark]
        public async Task RapidFireOpenClose()
        {
            // NOTE: the Math.Max floor dominates for most [Params] combinations, so total
            // checkouts do NOT scale cleanly with pool capacity — at Parallelism 20 and 25 both
            // MaxPoolSize values run an identical workload, which makes the spread between them
            // a useful read on this benchmark's own noise floor.
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
        /// Sync counterpart to <see cref="RapidFireOpenClose"/>: the same zero-hold churn, but
        /// every checkout goes through the blocking <c>Open()</c> path.
        /// </summary>
        /// <remarks>
        /// Worth measuring separately because the two paths diverge inside the pool. The V2 pool
        /// has no synchronous channel read, so a sync caller that misses the inline fast path has
        /// to run the async wait synchronously, which is a materially different cost from
        /// awaiting it. With the pool pre-warmed and never saturated by these
        /// <see cref="Parallelism"/> values, this stays on the fast path and so measures
        /// concurrent sync checkout without the wake-up behaviour that
        /// <see cref="ConnectionPoolContentionRunner"/> covers under saturation.
        ///
        /// Workers run on threadpool threads deliberately: sync database calls in ASP.NET run
        /// there, so that is the configuration whose behaviour actually matters.
        /// </remarks>
        [Benchmark]
        public async Task RapidFireOpenCloseSync()
        {
            int iterationsPerTask = Math.Max(20, MaxPoolSize / Math.Max(1, Parallelism) * 4);
            var tasks = new Task[Parallelism];
            for (int i = 0; i < Parallelism; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < iterationsPerTask; j++)
                    {
                        using var conn = new SqlConnection(_connectionString);
                        conn.Open();
                        // immediate return to pool
                    }
                });
            }
            await Task.WhenAll(tasks);
        }
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
        /// <remarks>
        /// <see cref="Parallelism"/> sets how far the pool is oversubscribed, and so how deep
        /// the queue of waiting callers gets. It is the queue depth rather than the concurrency
        /// level here, because saturating the pool already takes <see cref="MaxPoolSize"/>
        /// tasks before any caller has to wait at all.
        /// </remarks>
        [Benchmark]
        public async Task PoolExhaustionRecovery()
        {
            // Saturate the pool, then oversubscribe it by Parallelism so exactly that many
            // callers are queued waiting for a connection to come back.
            int taskCount = MaxPoolSize + Parallelism;
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

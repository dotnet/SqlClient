// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
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
        /// When Parallelism exceeds this, tasks must wait for a free connection.
        /// </summary>
        [Params(50, 100)]
        public int MaxPoolSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _connectionString = s_config.ConnectionString +
                $";Pooling=True;Max Pool Size={MaxPoolSize};Min Pool Size=5;Connect Timeout=60";

            // Create a small table for query workloads.
            // Hash the machine name instead of using it verbatim: hostnames can be long enough
            // to push the identifier past SQL Server's 128-character limit.
            string machineHash = Math.Abs(Environment.MachineName.GetHashCode()).ToString("x8");
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
        /// and repeats. Measures raw pool checkout/return throughput under contention.
        /// </summary>
        [Benchmark]
        public async Task RapidFireOpenClose()
        {
            var tasks = new Task[Parallelism];
            for (int i = 0; i < Parallelism; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < 20; j++)
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
        /// simulating real web server traffic patterns where requests cluster.
        /// </summary>
        [Benchmark]
        public async Task BurstyTrafficPattern()
        {
            int burstCount = 5;
            int tasksPerBurst = Parallelism / burstCount;
            if (tasksPerBurst < 1)
            {
                tasksPerBurst = 1;
            }

            for (int burst = 0; burst < burstCount; burst++)
            {
                var tasks = new Task[tasksPerBurst];
                for (int i = 0; i < tasksPerBurst; i++)
                {
                    int seed = burst * tasksPerBurst + i;
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

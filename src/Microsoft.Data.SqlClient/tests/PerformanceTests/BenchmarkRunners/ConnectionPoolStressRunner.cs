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
    /// Captures benchmarks for SqlConnection pool checkout throughput across a matrix of:
    /// - Warm vs. cold pools
    /// - Sync vs. async callers
    /// - Native vs. managed SNI (Windows-only distinction)
    /// - Disabled, new (V2), and legacy pooling implementations
    ///
    /// Related issues: #601, #979, #3356
    /// </summary>
    public class ConnectionPoolStressRunner : BaseRunner
    {
        public enum PoolBehavior
        {
            Disabled,
            New,
            Legacy
        }

        public enum AsyncBehavior
        {
            Sync,
            Async
        }

        public enum SniBehavior
        {
            Native,
            Managed
        }

        /// <summary>
        /// Whether the pool is pre-warmed before the measured run.
        /// </summary>
        [ParamsAllValues]
        public bool PoolIsWarm { get; set; }

        /// <summary>
        /// Whether callers open connections synchronously or asynchronously.
        /// </summary>
        [ParamsAllValues]
        public AsyncBehavior Async { get; set; }

        /// <summary>
        /// Whether to use native or managed SNI. Only meaningful on Windows;
        /// managed SNI is always used on non-Windows platforms.
        /// </summary>
        [ParamsAllValues]
        public SniBehavior Sni { get; set; }

        /// <summary>
        /// Number of connections opened in parallel per iteration.
        /// </summary>
        [Params(100)]
        public int NumConnectionsToOpen { get; set; }

        /// <summary>
        /// Which pooling implementation to exercise.
        /// </summary>
        [ParamsAllValues]
        public PoolBehavior Pooling { get; set; }

        private string _connectionString;
        private SqlConnectionStringBuilder _connectionStringBuilder = new();
        private IProducerConsumerCollection<SqlConnection> _connections = new ConcurrentBag<SqlConnection>();

        [GlobalSetup]
        public void Setup()
        {
            _connectionString = s_config.ConnectionString;
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _connections = new ConcurrentBag<SqlConnection>();
            _connectionStringBuilder = new SqlConnectionStringBuilder(_connectionString)
            {
                MaxPoolSize = 1000,
                WorkstationID = Guid.NewGuid().ToString(),
                Pooling = Pooling is not PoolBehavior.Disabled
            };

            // PoolBehavior.New opts into the ChannelDbConnectionPool (V2) implementation;
            // the default (false) uses the legacy pool.
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2", Pooling is PoolBehavior.New);
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", Sni is SniBehavior.Managed);

            if (PoolIsWarm)
            {
                OpenConnectionsInParallel(_connectionStringBuilder.ConnectionString, NumConnectionsToOpen);
            }
        }

        [IterationCleanup]
        public void Cleanup()
        {
            // Shut down the pool so that connections are physically closed
            // when they are returned to the pool.
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

        [Benchmark]
        public int ConnectionPoolWarmupBenchmark()
        {
            return OpenConnectionsInParallel(
                _connectionStringBuilder.ConnectionString,
                NumConnectionsToOpen,
                runCommand: false,
                returnToPool: false);
        }

        private int OpenConnectionsInParallel(
            string connectionString,
            int numConnectionsToOpen,
            bool runCommand = false,
            bool returnToPool = true)
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

                    if (runCommand)
                    {
                        using var command = conn.CreateCommand();
                        command.CommandText = "SELECT 1";
                        command.CommandType = System.Data.CommandType.Text;

                        int result = Async is AsyncBehavior.Async
                            ? (int)(await command.ExecuteScalarAsync() ?? 0)
                            : (int)(command.ExecuteScalar() ?? 0);

                        if (result != 1)
                        {
                            throw new Exception("Unexpected result from command");
                        }
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

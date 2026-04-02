// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class ConnectionPoolRunner : BaseRunner
    {
        private ConcurrentBag<SqlConnection> _connections;
        private SqlConnectionStringBuilder _builder;

        /// <summary>
        /// Number of concurrent threads opening connections.
        /// </summary>
        [Params(10, 50, 100)]
        public int Threads { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _builder = new SqlConnectionStringBuilder(s_config.ConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 200,
                ConnectTimeout = 60
            };
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _connections = new ConcurrentBag<SqlConnection>();
            SqlConnection.ClearAllPools();
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            while (_connections.TryTake(out var conn))
            {
                conn.Close();
            }
            SqlConnection.ClearAllPools();
        }

        [Benchmark]
        public void ConcurrentPooledOpen()
        {
            var tasks = new Task[Threads];
            for (int i = 0; i < Threads; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    var conn = new SqlConnection(_builder.ConnectionString);
                    conn.Open();
                    _connections.Add(conn);
                });
            }
            Task.WaitAll(tasks);
        }

        [Benchmark]
        public async Task ConcurrentPooledOpenAsync()
        {
            var tasks = new Task[Threads];
            for (int i = 0; i < Threads; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    var conn = new SqlConnection(_builder.ConnectionString);
                    await conn.OpenAsync();
                    _connections.Add(conn);
                });
            }
            await Task.WhenAll(tasks);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            SqlConnection.ClearAllPools();
        }

        [Benchmark]
        public void ConcurrentOpenCloseCycle()
        {
            var tasks = new Task[Threads];
            for (int i = 0; i < Threads; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    using var conn = new SqlConnection(_builder.ConnectionString);
                    conn.Open();
                });
            }
            Task.WaitAll(tasks);
        }
    }
}

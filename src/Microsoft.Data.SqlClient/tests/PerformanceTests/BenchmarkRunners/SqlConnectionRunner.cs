// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class SqlConnectionRunner : BaseRunner
    {
        [GlobalCleanup]
        public void Dispose() => SqlConnection.ClearAllPools();

        /// <summary>
        /// Whether MARS is enabled or disabled on connection string
        /// </summary>
        [Params(true, false)]
        public bool MARS { get; set; }

        /// <summary>
        /// Whether Connection Pooling is enabled or disabled on connection string
        /// </summary>
        [Params(true, false)]
        public bool Pooling { get; set; }

        private string ConnStr => s_config.ConnectionString + $";Pooling={Pooling};MultipleActiveResultSets={MARS}";

        [Benchmark]
        public void OpenConnection()
        {
            using var sqlConnection = new SqlConnection(ConnStr);
            sqlConnection.Open();
        }

        [Benchmark]
        public async Task OpenAsyncConnection()
        {
            using var sqlConnection = new SqlConnection(ConnStr);
            await sqlConnection.OpenAsync();
        }

        [Benchmark]
        public void SequentialOpenCloseCycle()
        {
            for (int i = 0; i < 100; i++)
            {
                using var sqlConnection = new SqlConnection(ConnStr);
                sqlConnection.Open();
            }
        }

        /// <summary>
        /// Saturate the thread pool with concurrent async connection opens.
        /// Measures how OpenAsync behaves when the thread pool is under
        /// heavy contention from many in-flight I/O completions.
        /// </summary>
        [Benchmark]
        public async Task ConcurrentAsyncOpenStarvation()
        {
            const int concurrency = 64;
            var tasks = new Task[concurrency];
            for (int i = 0; i < concurrency; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    using var conn = new SqlConnection(ConnStr);
                    await conn.OpenAsync();
                });
            }
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Simulate sync-over-async starvation: blocking threads that are
        /// waiting for connections forces the thread pool to grow and
        /// exposes contention in the pool and SNI layers.
        /// </summary>
        [Benchmark]
        public void SyncOverAsyncOpenStarvation()
        {
            const int concurrency = 64;
            var tasks = new Task[concurrency];
            for (int i = 0; i < concurrency; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    using var conn = new SqlConnection(ConnStr);
                    conn.Open();
                });
            }
            Task.WaitAll(tasks);
        }

        /// <summary>
        /// Mix sync and async connection opens to starve the pool with
        /// blocked threads while async completions compete for the same
        /// thread pool threads.
        /// </summary>
        [Benchmark]
        public async Task MixedSyncAsyncOpenStarvation()
        {
            const int concurrency = 64;
            var tasks = new Task[concurrency];
            for (int i = 0; i < concurrency; i++)
            {
                if (i % 2 == 0)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        using var conn = new SqlConnection(ConnStr);
                        conn.Open();
                    });
                }
                else
                {
                    tasks[i] = Task.Run(async () =>
                    {
                        using var conn = new SqlConnection(ConnStr);
                        await conn.OpenAsync();
                    });
                }
            }
            await Task.WhenAll(tasks);
        }
    }
}

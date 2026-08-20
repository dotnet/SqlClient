// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Benchmarks for concurrent async connection opens to surface pool contention.
    /// Reproduces issues #601 and #979.
    /// </summary>
    public class ParallelAsyncConnectionRunner : BaseRunner
    {
        /// <summary>
        /// Number of concurrent connections to open.
        /// </summary>
        [Params(10, 50, 100)]
        public int Concurrency { get; set; }

        /// <summary>
        /// Whether connection pooling is enabled.
        /// </summary>
        [Params(true, false)]
        public bool Pooling { get; set; }

        private string ConnectionString => s_config.ConnectionString + $";Pooling={Pooling};Max Pool Size=200";

        [IterationCleanup]
        public void IterationCleanup()
        {
            SqlConnection.ClearAllPools();
        }

        [Benchmark]
        public async Task OpenConnectionsConcurrently()
        {
            var tasks = new Task[Concurrency];
            for (int i = 0; i < Concurrency; i++)
            {
                tasks[i] = OpenAndCloseConnectionAsync();
            }
            await Task.WhenAll(tasks);
        }

        private async Task OpenAndCloseConnectionAsync()
        {
            using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
        }
    }
}

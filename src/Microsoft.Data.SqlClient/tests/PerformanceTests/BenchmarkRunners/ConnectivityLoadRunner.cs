// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>Measures sustained OpenAsync/open-close throughput and worker occupancy against the configured SQL Server.</summary>
    [Config(typeof(ConnectivityLoadConfig))]
    public class ConnectivityLoadRunner : BaseRunner
    {
        [ParamsSource(nameof(ConcurrencyValues))]
        public int Concurrency { get; set; }

        [ParamsSource(nameof(PoolingValues))]
        public bool Pooling { get; set; }

        [ParamsSource(nameof(DurationValues))]
        public int DurationSeconds { get; set; }

        [ParamsSource(nameof(SampleIntervalValues))]
        public int SampleIntervalMilliseconds { get; set; }

        public IEnumerable<int> ConcurrencyValues => LoadJob.Concurrency;
        public IEnumerable<bool> PoolingValues => LoadJob.Pooling;
        public IEnumerable<int> DurationValues =>
            new[] { LoadJob.DurationSeconds };

        public IEnumerable<int> SampleIntervalValues =>
            new[] { LoadJob.SampleIntervalMilliseconds };

        private static ConnectivityLoadJob LoadJob => s_config.Benchmarks.ConnectivityLoadRunnerConfig;
        private string _connectionString;

        [GlobalSetup]
        public void Setup()
        {
            if (Concurrency <= 0 || DurationSeconds <= 0 || SampleIntervalMilliseconds <= 0
                || SampleIntervalMilliseconds > DurationSeconds * 1000L)
            {
                throw new InvalidOperationException("Connectivity load requires positive concurrency, duration and a sampling interval within it.");
            }

            var builder = new SqlConnectionStringBuilder(s_config.ConnectionString)
            {
                Pooling = Pooling,
                MinPoolSize = 0,
                MaxPoolSize = Math.Max(Concurrency, 200)
            };
            if (builder.ConnectTimeout == 0)
            {
                throw new InvalidOperationException("Connectivity load requires a finite connection timeout.");
            }
            _connectionString = builder.ConnectionString;
        }

        [IterationSetup]
        public void ClearPool()
        {
            using var connection = new SqlConnection(_connectionString);
            SqlConnection.ClearPool(connection);
        }

        /// <summary>Runs concurrent awaited loops, including operation drain in the occupancy and throughput denominator.</summary>
        [Benchmark]
        public async Task OpenAsyncLoop()
        {
            ConnectivityLoadResult result = await ConnectivityLoad.RunAsync(
                OpenAndCloseAsync,
                Concurrency,
                TimeSpan.FromSeconds(DurationSeconds),
                TimeSpan.FromMilliseconds(SampleIntervalMilliseconds));
            ConnectivityLoadDiagnoser.Instance.Record(result);
        }

        [GlobalCleanup]
        public void Cleanup() => ClearPool();

        private async Task OpenAndCloseAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
        }
    }
}

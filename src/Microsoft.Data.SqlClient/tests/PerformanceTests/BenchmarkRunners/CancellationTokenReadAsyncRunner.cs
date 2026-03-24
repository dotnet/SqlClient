// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Benchmarks measuring CancellationToken overhead during ReadAsync iteration.
    /// Reproduces issue #2408.
    /// </summary>
    public class CancellationTokenReadAsyncRunner : BaseRunner
    {
        private static long s_rowCount;
        private string _tableName;
        private string _connectionString;
        private string _query;

        /// <summary>
        /// Whether to pass a CancellationToken to ReadAsync.
        /// </summary>
        [Params(true, false)]
        public bool UseCancellationToken { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            s_rowCount = s_config.Benchmarks.CancellationTokenReadAsyncRunnerConfig.RowCount;
            _connectionString = s_config.ConnectionString;
            _tableName = $"[perf_CancelToken_{Environment.MachineName}_{Guid.NewGuid():N}]";

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var createCmd = new SqlCommand(
                $"CREATE TABLE {_tableName} (Id INT IDENTITY PRIMARY KEY, Value INT)", conn);
            createCmd.ExecuteNonQuery();

            // Bulk insert rows
            using var bulkCopy = new SqlBulkCopy(conn)
            {
                DestinationTableName = _tableName,
                BatchSize = 10000
            };

            var dt = new System.Data.DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Value", typeof(int));
            for (int i = 0; i < s_rowCount; i++)
            {
                dt.Rows.Add(i, i * 2);
            }
            bulkCopy.WriteToServer(dt);

            _query = $"SELECT Id, Value FROM {_tableName}";
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand($"DROP TABLE IF EXISTS {_tableName}", conn);
            cmd.ExecuteNonQuery();
            SqlConnection.ClearAllPools();
        }

        [Benchmark]
        public async Task ReadAsyncWithOrWithoutToken()
        {
            using var cts = UseCancellationToken ? new CancellationTokenSource() : null;
            var token = cts?.Token ?? CancellationToken.None;

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(token);
            using var cmd = new SqlCommand(_query, conn);
            using var reader = await cmd.ExecuteReaderAsync(token);

            if (UseCancellationToken)
            {
                while (await reader.ReadAsync(token))
                { }
            }
            else
            {
                while (await reader.ReadAsync())
                { }
            }
        }
    }
}

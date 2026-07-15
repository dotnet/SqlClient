// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Benchmarks comparing read performance of JSON vs VARCHAR(MAX) columns.
    /// Reproduces issue #3499.
    /// Note: JSON column type requires SQL Server 2025+ or Azure SQL.
    /// This runner is disabled by default in runnerconfig.jsonc.
    /// </summary>
    public class JsonVsVarcharReadRunner : BaseRunner
    {
        private static long s_rowCount;
        private string _jsonTableName;
        private string _varcharTableName;
        private string _connectionString;

        /// <summary>
        /// Which column type to read from: "JSON" or "VARCHAR".
        /// </summary>
        [Params("JSON", "VARCHAR")]
        public string ColumnType { get; set; }

        private string ActiveTableName => ColumnType == "JSON" ? _jsonTableName : _varcharTableName;

        [GlobalSetup]
        public void Setup()
        {
            s_rowCount = s_config.Benchmarks.JsonVsVarcharReadRunnerConfig.RowCount;
            _connectionString = s_config.ConnectionString;

            string machineHash = ((uint)Environment.MachineName.GetHashCode()).ToString("x8");
            string suffix = $"{machineHash}_{Guid.NewGuid():N}";
            _jsonTableName = $"[perf_Json_{suffix}]";
            _varcharTableName = $"[perf_Varchar_{suffix}]";

            string sampleJson = "{\"id\":1,\"name\":\"test\",\"values\":[1,2,3],\"nested\":{\"key\":\"value\"}}";

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            // Create JSON table
            using (var cmd = new SqlCommand(
                $"CREATE TABLE {_jsonTableName} (Id INT IDENTITY PRIMARY KEY, Data JSON)", conn))
            {
                cmd.ExecuteNonQuery();
            }

            // Create VARCHAR(MAX) table
            using (var cmd = new SqlCommand(
                $"CREATE TABLE {_varcharTableName} (Id INT IDENTITY PRIMARY KEY, Data VARCHAR(MAX))", conn))
            {
                cmd.ExecuteNonQuery();
            }

            // Bulk insert identical data into both tables
            var dt = new System.Data.DataTable();
            dt.Columns.Add("Data", typeof(string));
            for (int i = 0; i < s_rowCount; i++)
            {
                dt.Rows.Add(sampleJson);
            }

            using (var bulkCopy = new SqlBulkCopy(conn) { DestinationTableName = _jsonTableName, BatchSize = 10000 })
            {
                bulkCopy.ColumnMappings.Add("Data", "Data");
                bulkCopy.WriteToServer(dt);
            }

            using (var bulkCopy = new SqlBulkCopy(conn) { DestinationTableName = _varcharTableName, BatchSize = 10000 })
            {
                bulkCopy.ColumnMappings.Add("Data", "Data");
                bulkCopy.WriteToServer(dt);
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using (var cmd = new SqlCommand($"DROP TABLE IF EXISTS {_jsonTableName}", conn))
            {
                cmd.ExecuteNonQuery();
            }
            
            using (var cmd = new SqlCommand($"DROP TABLE IF EXISTS {_varcharTableName}", conn))
            {
                cmd.ExecuteNonQuery();
            }
            
            SqlConnection.ClearAllPools();
        }

        [Benchmark]
        public void ReadDataSync()
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand($"SELECT Data FROM {ActiveTableName}", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                _ = reader.GetString(0);
            }
        }

        [Benchmark]
        public async Task ReadDataAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand($"SELECT Data FROM {ActiveTableName}", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                _ = await reader.GetFieldValueAsync<string>(0);
            }
        }
    }
}

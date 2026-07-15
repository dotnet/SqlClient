// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Benchmarks for sync vs async reading of large VARBINARY(MAX) values.
    /// Reproduces issues #593 and #1562.
    /// </summary>
    public class AsyncLargeDataReadRunner : BaseRunner
    {
        private string _tableName;
        private string _connectionString;

        /// <summary>
        /// Size of the data to read in bytes.
        /// </summary>
        [Params(1_048_576, 5_242_880, 10_485_760, 20_971_520)]
        public int DataSizeBytes { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _connectionString = s_config.ConnectionString;
            _tableName = $"[perf_AsyncLargeData_{Environment.MachineName}_{Guid.NewGuid():N}]";

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var createCmd = new SqlCommand(
                $"CREATE TABLE {_tableName} (Id INT IDENTITY PRIMARY KEY, Data VARBINARY(MAX))", conn);
            createCmd.ExecuteNonQuery();

            // Insert a single row with random bytes of the specified size
            byte[] data = new byte[DataSizeBytes];
            Random.Shared.NextBytes(data);

            using var insertCmd = new SqlCommand(
                $"INSERT INTO {_tableName} (Data) VALUES (@data)", conn);
            insertCmd.Parameters.Add("@data", SqlDbType.VarBinary, -1).Value = data;
            insertCmd.ExecuteNonQuery();
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
        public void ReadLargeDataSync()
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand($"SELECT Data FROM {_tableName}", conn);
            using var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess);
            while (reader.Read())
            {
                byte[] buffer = new byte[8192];
                long offset = 0;
                long bytesRead;
                do
                {
                    bytesRead = reader.GetBytes(0, offset, buffer, 0, buffer.Length);
                    offset += bytesRead;
                } while (bytesRead > 0);
            }
        }

        [Benchmark]
        public async Task ReadLargeDataAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand($"SELECT Data FROM {_tableName}", conn);
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            while (await reader.ReadAsync())
            {
                using var stream = reader.GetStream(0);
                byte[] buffer = new byte[8192];
                int bytesRead;
                do
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                } while (bytesRead > 0);
            }
        }
    }
}

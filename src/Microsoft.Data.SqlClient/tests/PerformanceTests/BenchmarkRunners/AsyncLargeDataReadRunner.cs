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

        /// <summary>
        /// Size of the client-side read buffer used to drain the VARBINARY(MAX) column.
        /// Kept small (8 KB) and large (1 MB) to observe whether buffer size relative to
        /// the payload materially changes throughput.
        /// </summary>
        [Params(8_192, 1_048_576)]
        public int ReadBufferBytes { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _connectionString = s_config.ConnectionString;
            string machineHash = ((uint)Environment.MachineName.GetHashCode()).ToString("x8");
            _tableName = $"[perf_AsyncLargeData_{machineHash}_{Guid.NewGuid():N}]";

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var createCmd = new SqlCommand(
                $"CREATE TABLE {_tableName} (Id INT IDENTITY PRIMARY KEY, Data VARBINARY(MAX))", conn);
            createCmd.ExecuteNonQuery();

            // Generate the payload entirely server-side via CRYPT_GEN_RANDOM so we don't
            // allocate a multi-megabyte byte[] on the client and don't ship the payload
            // over the wire just to seed the benchmark.
            using var insertCmd = new SqlCommand(
                $@"INSERT INTO {_tableName} (Data)
                   SELECT SUBSTRING(
                       CONVERT(
                           varbinary(max),
                           REPLICATE(
                               CONVERT(varchar(max), CRYPT_GEN_RANDOM(8000), 2),
                               (@dataSizeBytes + 7999) / 8000
                           ),
                           2
                       ),
                       1,
                       @dataSizeBytes
                   );", conn);
            insertCmd.Parameters.Add("@dataSizeBytes", SqlDbType.Int).Value = DataSizeBytes;
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
                byte[] buffer = new byte[ReadBufferBytes];
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
                byte[] buffer = new byte[ReadBufferBytes];
                int bytesRead;
                do
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                } while (bytesRead > 0);
            }
        }
    }
}

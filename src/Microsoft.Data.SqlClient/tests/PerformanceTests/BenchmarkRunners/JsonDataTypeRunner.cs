// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Benchmarks for the JSON data type (SQL Server 2025+).
    /// Requires a SQL Server instance that supports the JSON data type.
    /// </summary>
    public class JsonDataTypeRunner : BaseRunner
    {
        private SqlConnection _connection;
        private SqlCommand _readCommand;
        private string _tableName;
        private bool _isSupported;

        /// <summary>
        /// Number of rows to insert and read back.
        /// </summary>
        [Params(100, 1_000, 10_000, 100_000)]
        public int RowCount { get; set; }

        /// <summary>
        /// Number of key-value pairs in each JSON object,
        /// controlling payload size.
        /// </summary>
        [Params(5, 50, 200)]
        public int JsonKeyCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _connection = new SqlConnection(s_config.ConnectionString);
            _connection.Open();

            // Check if JSON type is supported by trying to create a table
            _tableName = "PerfTest_Json_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                using (var cmd = new SqlCommand(
                    $"CREATE TABLE [{_tableName}] (Id INT PRIMARY KEY, JsonCol JSON)", _connection))
                {
                    cmd.ExecuteNonQuery();
                }
                _isSupported = true;
            }
            catch (SqlException)
            {
                // JSON type not supported on this SQL Server version
                _isSupported = false;
                return;
            }

            // Build JSON value with JsonKeyCount key-value pairs
            string jsonValue = GenerateJsonValue(JsonKeyCount);

            // Bulk-insert test data
            for (int i = 0; i < RowCount; i++)
            {
                using var insertCmd = new SqlCommand(
                    $"INSERT INTO [{_tableName}] (Id, JsonCol) VALUES (@id, @json)", _connection);
                insertCmd.Parameters.AddWithValue("@id", i);
                insertCmd.Parameters.AddWithValue("@json", jsonValue);
                insertCmd.ExecuteNonQuery();
            }

            _readCommand = new SqlCommand($"SELECT JsonCol FROM [{_tableName}]", _connection);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            try
            {
                _readCommand?.Dispose();
                if (_connection?.State == System.Data.ConnectionState.Open)
                {
                    using var cmd = new SqlCommand($"DROP TABLE IF EXISTS [{_tableName}]", _connection);
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                _connection?.Close();
                SqlConnection.ClearAllPools();
            }
        }

        [Benchmark]
        public int ReadJsonSync()
        {
            if (!_isSupported) return 0;
            using var reader = _readCommand.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                _ = reader.GetString(0);
                count++;
            }
            return count;
        }

        [Benchmark]
        public async Task<int> ReadJsonAsync()
        {
            if (!_isSupported) return 0;
            using var reader = await _readCommand.ExecuteReaderAsync();
            int count = 0;
            while (await reader.ReadAsync())
            {
                _ = await reader.GetFieldValueAsync<string>(0);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Generates a JSON object with the specified number of key-value pairs.
        /// Example (keyCount=3): {"key0":"value0","key1":"value1","key2":"value2"}
        /// </summary>
        private static string GenerateJsonValue(int keyCount)
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < keyCount; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"\"key{i}\":\"value{i}\"");
            }
            sb.Append('}');
            return sb.ToString();
        }
    }
}

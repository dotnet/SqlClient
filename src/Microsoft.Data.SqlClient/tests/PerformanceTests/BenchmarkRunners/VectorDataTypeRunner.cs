// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Benchmarks for the Vector data type (SQL Server 2025+).
    /// Requires a SQL Server instance that supports the VECTOR data type.
    /// </summary>
    public class VectorDataTypeRunner : BaseRunner
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
        /// Number of dimensions in each vector.
        /// </summary>
        [Params(5, 50, 200)]
        public int Dimensions { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _connection = new SqlConnection(s_config.ConnectionString);
            _connection.Open();

            _tableName = "PerfTest_Vector_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                using (var cmd = new SqlCommand(
                    $"CREATE TABLE [{_tableName}] (Id INT PRIMARY KEY, VectorCol VECTOR({Dimensions}))", _connection))
                {
                    cmd.ExecuteNonQuery();
                }
                _isSupported = true;
            }
            catch (SqlException)
            {
                // Vector type not supported on this SQL Server version
                _isSupported = false;
                return;
            }

            // Build a vector literal with the configured number of dimensions
            string vectorValue = GenerateVectorLiteral(Dimensions);

            // Insert test data
            for (int i = 0; i < RowCount; i++)
            {
                using var insertCmd = new SqlCommand(
                    $"INSERT INTO [{_tableName}] (Id, VectorCol) VALUES (@id, '{vectorValue}')", _connection);
                insertCmd.Parameters.AddWithValue("@id", i);
                insertCmd.ExecuteNonQuery();
            }

            _readCommand = new SqlCommand($"SELECT VectorCol FROM [{_tableName}]", _connection);
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
        public int ReadVectorSync()
        {
            if (!_isSupported) return 0;
            using var reader = _readCommand.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                _ = reader.GetValue(0);
                count++;
            }
            return count;
        }

        [Benchmark]
        public async Task<int> ReadVectorAsync()
        {
            if (!_isSupported) return 0;
            using var reader = await _readCommand.ExecuteReaderAsync();
            int count = 0;
            while (await reader.ReadAsync())
            {
                _ = await reader.GetFieldValueAsync<object>(0);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Generates a vector literal string with the specified number of dimensions.
        /// Example (dimensions=3): [0.1, 0.2, 0.3]
        /// </summary>
        private static string GenerateVectorLiteral(int dimensions)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < dimensions; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(((i + 1) * 0.1).ToString("F1"));
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}

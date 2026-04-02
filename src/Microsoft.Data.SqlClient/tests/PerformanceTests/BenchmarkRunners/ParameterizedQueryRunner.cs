// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class ParameterizedQueryRunner : BaseRunner
    {
        private SqlConnection _connection;
        private string _tableName;

        [GlobalSetup]
        public void Setup()
        {
            _connection = new SqlConnection(s_config.ConnectionString);
            _connection.Open();

            _tableName = "PerfTest_Param_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            using var createCmd = new SqlCommand(
                $"CREATE TABLE [{_tableName}] (Id INT NOT NULL, Name VARCHAR(100))", _connection);
            createCmd.ExecuteNonQuery();

            using var insertCmd = new SqlCommand(
                $"INSERT INTO [{_tableName}] (Id, Name) VALUES (1, 'TestData')", _connection);
            insertCmd.ExecuteNonQuery();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            try
            {
                using var cmd = new SqlCommand($"DROP TABLE IF EXISTS [{_tableName}]", _connection);
                cmd.ExecuteNonQuery();
            }
            finally
            {
                _connection?.Close();
                SqlConnection.ClearAllPools();
            }
        }

        [Benchmark]
        public int NonParameterizedQuery()
        {
            using var cmd = new SqlCommand($"SELECT Name FROM [{_tableName}] WHERE Id = 1", _connection);
            using var reader = cmd.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                _ = reader.GetString(0);
                count++;
            }
            return count;
        }

        [Benchmark]
        public int ParameterizedQuery()
        {
            using var cmd = new SqlCommand($"SELECT Name FROM [{_tableName}] WHERE Id = @Id", _connection);
            cmd.Parameters.AddWithValue("@Id", 1);
            using var reader = cmd.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                _ = reader.GetString(0);
                count++;
            }
            return count;
        }

        [Benchmark]
        public async Task<int> NonParameterizedQueryAsync()
        {
            using var cmd = new SqlCommand($"SELECT Name FROM [{_tableName}] WHERE Id = 1", _connection);
            using var reader = await cmd.ExecuteReaderAsync();
            int count = 0;
            while (await reader.ReadAsync())
            {
                _ = reader.GetString(0);
                count++;
            }
            return count;
        }

        [Benchmark]
        public async Task<int> ParameterizedQueryAsync()
        {
            using var cmd = new SqlCommand($"SELECT Name FROM [{_tableName}] WHERE Id = @Id", _connection);
            cmd.Parameters.AddWithValue("@Id", 1);
            using var reader = await cmd.ExecuteReaderAsync();
            int count = 0;
            while (await reader.ReadAsync())
            {
                _ = reader.GetString(0);
                count++;
            }
            return count;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class StoredProcedureRunner : BaseRunner
    {
        private SqlConnection _connection;
        private string _tableName;
        private string _spName;

        [GlobalSetup]
        public void Setup()
        {
            _connection = new SqlConnection(s_config.ConnectionString);
            _connection.Open();

            var suffix = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            _tableName = "PerfTest_SP_" + suffix;
            _spName = "PerfTest_GetData_" + suffix;

            using (var cmd = new SqlCommand(
                $"CREATE TABLE [{_tableName}] (Id INT, Value VARCHAR(100))", _connection))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqlCommand(
                $"INSERT INTO [{_tableName}] (Id, Value) VALUES (1, 'Alpha')", _connection))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqlCommand(
                $"CREATE PROCEDURE [{_spName}] AS SELECT * FROM [{_tableName}]", _connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            try
            {
                using (var cmd = new SqlCommand($"DROP PROCEDURE IF EXISTS [{_spName}]", _connection))
                {
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new SqlCommand($"DROP TABLE IF EXISTS [{_tableName}]", _connection))
                {
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
        public void InlineSql()
        {
            using var cmd = new SqlCommand($"SELECT * FROM [{_tableName}]", _connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) { }
        }

        [Benchmark]
        public void StoredProcedure()
        {
            using var cmd = new SqlCommand($"[{_spName}]", _connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) { }
        }

        [Benchmark]
        public async Task InlineSqlAsync()
        {
            using var cmd = new SqlCommand($"SELECT * FROM [{_tableName}]", _connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { }
        }

        [Benchmark]
        public async Task StoredProcedureAsync()
        {
            using var cmd = new SqlCommand($"[{_spName}]", _connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { }
        }
    }
}

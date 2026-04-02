// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class PreparedStatementRunner : BaseRunner
    {
        private SqlConnection _connection;
        private string _tableName;
        private const string InsertSql = "INSERT INTO [{0}] (Name) VALUES (@name)";

        [GlobalSetup]
        public void Setup()
        {
            _connection = new SqlConnection(s_config.ConnectionString);
            _connection.Open();

            _tableName = "PerfTest_PrepStmt_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            using var cmd = new SqlCommand(
                $"CREATE TABLE [{_tableName}] (Id INT IDENTITY, Name VARCHAR(100))", _connection);
            cmd.ExecuteNonQuery();
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

        [IterationSetup]
        public void IterationSetup()
        {
            using var cmd = new SqlCommand($"TRUNCATE TABLE [{_tableName}]", _connection);
            cmd.ExecuteNonQuery();
        }

        [Benchmark]
        public void NonPreparedInsert()
        {
            for (int i = 0; i < 10; i++)
            {
                using var cmd = new SqlCommand(string.Format(InsertSql, _tableName), _connection);
                cmd.Parameters.AddWithValue("@name", "User_" + i);
                cmd.ExecuteNonQuery();
            }
        }

        [Benchmark]
        public void PreparedInsert()
        {
            using var cmd = new SqlCommand(string.Format(InsertSql, _tableName), _connection);
            cmd.Parameters.Add("@name", SqlDbType.VarChar, 100);
            cmd.Prepare();

            for (int i = 0; i < 10; i++)
            {
                cmd.Parameters["@name"].Value = "User_" + i;
                cmd.ExecuteNonQuery();
            }
        }

        [Benchmark]
        public async Task NonPreparedInsertAsync()
        {
            for (int i = 0; i < 10; i++)
            {
                using var cmd = new SqlCommand(string.Format(InsertSql, _tableName), _connection);
                cmd.Parameters.AddWithValue("@name", "User_" + i);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        [Benchmark]
        public async Task PreparedInsertAsync()
        {
            using var cmd = new SqlCommand(string.Format(InsertSql, _tableName), _connection);
            cmd.Parameters.Add("@name", SqlDbType.VarChar, 100);
            cmd.Prepare();

            for (int i = 0; i < 10; i++)
            {
                cmd.Parameters["@name"].Value = "User_" + i;
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}

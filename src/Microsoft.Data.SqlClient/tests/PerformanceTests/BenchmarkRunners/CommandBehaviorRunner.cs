// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class CommandBehaviorRunner : BaseRunner
    {
        private SqlConnection _connection;
        private SqlCommand _command;
        private string _tableName;

        [Params(
            CommandBehavior.Default,
            CommandBehavior.SingleResult,
            CommandBehavior.SingleRow,
            CommandBehavior.SequentialAccess)]
        public CommandBehavior Behavior { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _connection = new SqlConnection(s_config.ConnectionString);
            _connection.Open();

            _tableName = "PerfTest_CmdBhvr_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            using var createCmd = new SqlCommand(
                $"CREATE TABLE [{_tableName}] (Id INT, Data VARCHAR(100))", _connection);
            createCmd.ExecuteNonQuery();

            // Insert test rows
            for (int i = 0; i < 100; i++)
            {
                using var insertCmd = new SqlCommand(
                    $"INSERT INTO [{_tableName}] (Id, Data) VALUES ({i}, 'TestData_{i}')", _connection);
                insertCmd.ExecuteNonQuery();
            }

            _command = new SqlCommand($"SELECT [Id], [Data] FROM [{_tableName}]", _connection);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            try
            {
                _command?.Dispose();
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
        public int ExecuteReaderWithBehavior()
        {
            using var reader = _command.ExecuteReader(Behavior);
            int count = 0;
            while (reader.Read())
            {
                count++;
            }
            return count;
        }

        [Benchmark]
        public async Task<int> ExecuteReaderWithBehaviorAsync()
        {
            using var reader = await _command.ExecuteReaderAsync(Behavior);
            int count = 0;
            while (await reader.ReadAsync())
            {
                count++;
            }
            return count;
        }
    }
}

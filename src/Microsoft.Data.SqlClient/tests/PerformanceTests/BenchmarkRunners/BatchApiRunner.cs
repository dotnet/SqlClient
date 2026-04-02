// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Benchmarks comparing SqlBatch API vs individual SqlCommand loop execution.
    /// SqlBatch is available only on .NET (not .NET Framework).
    /// </summary>
    public class BatchApiRunner : BaseRunner
    {
        private SqlConnection _connection;
        private string _tableName;

        [Params(5, 10, 50, 100)]
        public int BatchCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _connection = new SqlConnection(s_config.ConnectionString);
            _connection.Open();

            _tableName = "PerfTest_Batch_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            using var cmd = new SqlCommand(
                $"CREATE TABLE [{_tableName}] (Id INT IDENTITY, Name VARCHAR(100), Value INT)", _connection);
            cmd.ExecuteNonQuery();
        }

        [IterationSetup]
        public void IterationSetup()
        {
            using var cmd = new SqlCommand($"TRUNCATE TABLE [{_tableName}]", _connection);
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

        [Benchmark]
        public void BatchInsert()
        {
            using var batch = new SqlBatch(_connection);
            for (int i = 0; i < BatchCount; i++)
            {
                var batchCmd = new SqlBatchCommand(
                    $"INSERT INTO [{_tableName}] (Name, Value) VALUES ('Item_{i}', {i})");
                batch.BatchCommands.Add(batchCmd);
            }
            batch.ExecuteNonQuery();
        }

        [Benchmark]
        public void IndividualInsert()
        {
            for (int i = 0; i < BatchCount; i++)
            {
                using var cmd = new SqlCommand(
                    $"INSERT INTO [{_tableName}] (Name, Value) VALUES ('Item_{i}', {i})", _connection);
                cmd.ExecuteNonQuery();
            }
        }

        [Benchmark]
        public async Task BatchInsertAsync()
        {
            using var batch = new SqlBatch(_connection);
            for (int i = 0; i < BatchCount; i++)
            {
                var batchCmd = new SqlBatchCommand(
                    $"INSERT INTO [{_tableName}] (Name, Value) VALUES ('Item_{i}', {i})");
                batch.BatchCommands.Add(batchCmd);
            }
            await batch.ExecuteNonQueryAsync();
        }

        [Benchmark]
        public async Task BatchSelectAsync()
        {
            // Insert some data first
            for (int i = 0; i < 10; i++)
            {
                using var insertCmd = new SqlCommand(
                    $"INSERT INTO [{_tableName}] (Name, Value) VALUES ('Select_{i}', {i})", _connection);
                insertCmd.ExecuteNonQuery();
            }

            using var batch = new SqlBatch(_connection);
            for (int i = 0; i < BatchCount; i++)
            {
                var batchCmd = new SqlBatchCommand($"SELECT * FROM [{_tableName}]");
                batch.BatchCommands.Add(batchCmd);
            }
            using var reader = await batch.ExecuteReaderAsync();
            do
            {
                while (await reader.ReadAsync()) { }
            } while (await reader.NextResultAsync());
        }

        [Benchmark]
        public void MixedOperationBatch()
        {
            // Seed a row for update/delete
            using (var seedCmd = new SqlCommand(
                $"INSERT INTO [{_tableName}] (Name, Value) VALUES ('Seed', 0)", _connection))
            {
                seedCmd.ExecuteNonQuery();
            }

            using var batch = new SqlBatch(_connection);
            batch.BatchCommands.Add(new SqlBatchCommand(
                $"INSERT INTO [{_tableName}] (Name, Value) VALUES ('Mixed_Insert', 1)"));
            batch.BatchCommands.Add(new SqlBatchCommand(
                $"UPDATE [{_tableName}] SET Value = 99 WHERE Name = 'Seed'"));
            batch.BatchCommands.Add(new SqlBatchCommand(
                $"DELETE FROM [{_tableName}] WHERE Name = 'Seed'"));
            batch.ExecuteNonQuery();
        }
    }
}

#endif

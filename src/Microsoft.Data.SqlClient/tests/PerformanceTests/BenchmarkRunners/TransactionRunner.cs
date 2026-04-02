// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class TransactionRunner : BaseRunner
    {
        private SqlConnection _connection;
        private string _tableName;

        [Params(
            IsolationLevel.ReadCommitted,
            IsolationLevel.RepeatableRead,
            IsolationLevel.Serializable,
            IsolationLevel.Snapshot)]
        public IsolationLevel IsoLevel { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _connection = new SqlConnection(s_config.ConnectionString);
            _connection.Open();

            _tableName = "PerfTest_Txn_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            using (var cmd = new SqlCommand(
                $"CREATE TABLE [{_tableName}] (Id INT PRIMARY KEY, Data VARCHAR(50))", _connection))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqlCommand(
                $"INSERT INTO [{_tableName}] (Id, Data) VALUES (1, 'TransactionData')", _connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Enable snapshot isolation on the database for Snapshot level tests
            try
            {
                string dbName = _connection.Database;
                using var alterCmd = new SqlCommand(
                    $"ALTER DATABASE [{dbName}] SET ALLOW_SNAPSHOT_ISOLATION ON", _connection);
                alterCmd.ExecuteNonQuery();
            }
            catch (SqlException)
            {
                // Ignore if already enabled or insufficient permissions
            }
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
        public void BeginUpdateRollback()
        {
            using var txn = _connection.BeginTransaction(IsoLevel);
            using var cmd = new SqlCommand(
                $"UPDATE [{_tableName}] SET Data='Updated' WHERE Id=1", _connection, txn);
            cmd.ExecuteNonQuery();
            txn.Rollback();
        }

        [Benchmark]
        public async Task BeginUpdateRollbackAsync()
        {
            DbTransaction txn;
#if NET
            txn = await _connection.BeginTransactionAsync(IsoLevel);
#else
            txn = _connection.BeginTransaction(IsoLevel);
#endif
            using var cmd = new SqlCommand(
                $"UPDATE [{_tableName}] SET Data='Updated' WHERE Id=1",
                _connection, (SqlTransaction)txn);
            await cmd.ExecuteNonQueryAsync();
#if NET
            await txn.RollbackAsync();
#else
            txn.Rollback();
#endif
        }

        [Benchmark]
        public void BeginInsertCommit()
        {
            using var txn = _connection.BeginTransaction(IsolationLevel.ReadCommitted);
            using var cmd = new SqlCommand(
                $"INSERT INTO [{_tableName}] (Id, Data) VALUES (2, 'CommitTest'); DELETE FROM [{_tableName}] WHERE Id=2;",
                _connection, txn);
            cmd.ExecuteNonQuery();
            txn.Commit();
        }

        [Benchmark]
        public void BeginInsertRollback()
        {
            using var txn = _connection.BeginTransaction(IsolationLevel.ReadCommitted);
            using var cmd = new SqlCommand(
                $"INSERT INTO [{_tableName}] (Id, Data) VALUES (3, 'RollbackTest')",
                _connection, txn);
            cmd.ExecuteNonQuery();
            txn.Rollback();
        }

        [Benchmark]
        public void EnlistTrueOpen()
        {
            using var conn = new SqlConnection(s_config.ConnectionString + ";Enlist=true");
            conn.Open();
        }

        [Benchmark]
        public void EnlistFalseOpen()
        {
            using var conn = new SqlConnection(s_config.ConnectionString + ";Enlist=false");
            conn.Open();
        }
    }
}

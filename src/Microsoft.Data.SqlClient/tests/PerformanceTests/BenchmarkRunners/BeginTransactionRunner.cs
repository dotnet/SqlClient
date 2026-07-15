// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Benchmarks measuring BeginTransaction round-trip latency.
    /// Reproduces issue #1554.
    /// </summary>
    public class BeginTransactionRunner : BaseRunner
    {
        private SqlConnection _connection;
        private string _tableName;

        [GlobalSetup]
        public void Setup()
        {
            _connection = new SqlConnection(s_config.ConnectionString);
            _connection.Open();

            _tableName = $"[perf_TxnBench_{Environment.MachineName}_{Guid.NewGuid():N}]";

            using var cmd = new SqlCommand(
                $"CREATE TABLE {_tableName} (Id INT IDENTITY PRIMARY KEY, Value INT)", _connection);
            cmd.ExecuteNonQuery();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            using var cmd = new SqlCommand($"DROP TABLE IF EXISTS {_tableName}", _connection);
            cmd.ExecuteNonQuery();
            _connection.Close();
            _connection.Dispose();
            SqlConnection.ClearAllPools();
        }

        // Truncate the table between iterations so every iteration inserts into an empty
        // table. Without this, the table grows across iterations (and across benchmarks,
        // since they share the same connection/table), which would skew the measured
        // BeginTransaction overhead as row count increases.
        [IterationCleanup]
        public void IterationCleanup()
        {
            using var cmd = new SqlCommand($"TRUNCATE TABLE {_tableName}", _connection);
            cmd.ExecuteNonQuery();
        }

        [Benchmark]
        public void WithTransaction()
        {
            using var txn = _connection.BeginTransaction();
            using var cmd = new SqlCommand($"INSERT INTO {_tableName} (Value) VALUES (1)", _connection, txn);
            cmd.ExecuteNonQuery();
            txn.Commit();
        }

        [Benchmark]
        public void WithoutTransaction()
        {
            using var cmd = new SqlCommand($"INSERT INTO {_tableName} (Value) VALUES (1)", _connection);
            cmd.ExecuteNonQuery();
        }

        [Benchmark]
        public async Task WithTransactionAsync()
        {
            using var txn = (SqlTransaction)await _connection.BeginTransactionAsync();
            using var cmd = new SqlCommand($"INSERT INTO {_tableName} (Value) VALUES (1)", _connection, txn);
            await cmd.ExecuteNonQueryAsync();
            await txn.CommitAsync();
        }

        [Benchmark]
        public async Task WithoutTransactionAsync()
        {
            using var cmd = new SqlCommand($"INSERT INTO {_tableName} (Value) VALUES (1)", _connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using System.Xml;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>Measures command entry points over stable, focused SQL batch and RPC workloads.</summary>
    public class SqlCommandRunner : CommandRunnerBase
    {
        // These local command executions take only a few hundred microseconds. Batch them
        // sequentially so the configured iterations exceed BenchmarkDotNet's recommended
        // 100 ms on the local database and scheduling interruptions have less influence.
        // OperationsPerInvoke keeps reported time and allocations normalized to one command.
        private const int OperationsPerBatch = 512;

        /// <summary>Distinguishes ordinary batches, sp_executesql, and stored-procedure RPCs.</summary>
        public enum ExecutionPath { Text, Parameterized, StoredProcedure }

        [Params(ExecutionPath.Text, ExecutionPath.Parameterized, ExecutionPath.StoredProcedure)]
        public ExecutionPath Path { get; set; }

        private SqlCommand _nonQuery;
        private SqlCommand _scalar;
        private SqlCommand _reader;
        private SqlCommand _xml;

        protected override CommandRunnerJob Settings => s_config.Benchmarks.SqlCommandRunnerConfig;

        /// <summary>Creates session-local fixtures and checks all API paths outside measurement.</summary>
        protected override void SetupCommands()
        {
            CreateCommand("CREATE TABLE #CommandRow (Id int PRIMARY KEY, Value int NOT NULL); INSERT INTO #CommandRow VALUES (1, 123);")
                .ExecuteNonQuery();

            _nonQuery = BuildCommand("NonQuery", "UPDATE #CommandRow SET Value = 123 WHERE Id = @id;");
            _scalar = BuildCommand("Scalar", "SELECT Value FROM #CommandRow WHERE Id = @id;");
            _reader = BuildCommand("Reader", "SELECT Id, Value FROM #CommandRow WHERE Id = @id;");
            _xml = BuildCommand("Xml", "SELECT Id, Value FROM #CommandRow WHERE Id = @id FOR XML PATH('row'), ROOT('rows');");

            if (ExecuteNonQuery() != OperationsPerBatch || ExecuteNonQueryAsync().GetAwaiter().GetResult() != OperationsPerBatch ||
                (int)ExecuteScalar() != 123 || (int)ExecuteScalarAsync().GetAwaiter().GetResult() != 123 ||
                ExecuteReader() != 124 * OperationsPerBatch || ExecuteReaderAsync().GetAwaiter().GetResult() != 124 * OperationsPerBatch ||
                ExecuteXmlReader() != 10 * OperationsPerBatch || ExecuteXmlReaderAsync().GetAwaiter().GetResult() != 10 * OperationsPerBatch)
            {
                throw new InvalidOperationException("Command fixture returned unexpected results.");
            }
        }

        /// <summary>Constructs one batch or RPC command, leaving parameters stable between invocations.</summary>
        /// <param name="name">Session-local procedure suffix.</param>
        /// <param name="sql">The workload SQL with an integer @id parameter.</param>
        /// <returns>The reusable command.</returns>
        private SqlCommand BuildCommand(string name, string sql)
        {
            SqlCommand command;
            if (Path == ExecutionPath.StoredProcedure)
            {
                string procedure = "#Command" + name;
                CreateCommand($"CREATE PROCEDURE {procedure} @id int AS {sql}").ExecuteNonQuery();
                command = CreateCommand(procedure);
                command.CommandType = CommandType.StoredProcedure;
            }
            else
            {
                command = CreateCommand(Path == ExecutionPath.Text ? sql.Replace("@id", "1") : sql);
            }
            if (Path != ExecutionPath.Text)
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = 1;
            }
            return command;
        }

        /// <summary>Executes a reusable reader command and accesses every returned field.</summary>
        [Benchmark(OperationsPerInvoke = OperationsPerBatch)]
        public int ExecuteReader()
        {
            int sum = 0;
            for (int operation = 0; operation < OperationsPerBatch; operation++)
            {
                using SqlDataReader reader = _reader.ExecuteReader();
                while (reader.Read())
                {
                    sum += reader.GetInt32(0) + reader.GetInt32(1);
                }
            }
            return sum;
        }

        /// <summary>Executes and advances asynchronously, then accesses the current row's typed fields.</summary>
        [Benchmark(OperationsPerInvoke = OperationsPerBatch)]
        public async Task<int> ExecuteReaderAsync()
        {
            int sum = 0;
            for (int operation = 0; operation < OperationsPerBatch; operation++)
            {
                await using SqlDataReader reader = await _reader.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    sum += reader.GetInt32(0) + reader.GetInt32(1);
                }
            }
            return sum;
        }

        /// <summary>Measures first-value execution without command allocation or unused result rows.</summary>
        [Benchmark(OperationsPerInvoke = OperationsPerBatch)]
        public object ExecuteScalar()
        {
            object value = null;
            for (int operation = 0; operation < OperationsPerBatch; operation++)
            {
                value = _scalar.ExecuteScalar();
            }
            return value;
        }

        /// <summary>Measures the corresponding asynchronous first-value execution path.</summary>
        [Benchmark(OperationsPerInvoke = OperationsPerBatch)]
        public async Task<object> ExecuteScalarAsync()
        {
            object value = null;
            for (int operation = 0; operation < OperationsPerBatch; operation++)
            {
                value = await _scalar.ExecuteScalarAsync();
            }
            return value;
        }

        /// <summary>Updates the same row to the same value without growing the workload.</summary>
        [Benchmark(OperationsPerInvoke = OperationsPerBatch)]
        public int ExecuteNonQuery()
        {
            int affectedRows = 0;
            for (int operation = 0; operation < OperationsPerBatch; operation++)
            {
                affectedRows += _nonQuery.ExecuteNonQuery();
            }
            return affectedRows;
        }

        /// <summary>Measures asynchronous nonquery execution over the same stable update.</summary>
        [Benchmark(OperationsPerInvoke = OperationsPerBatch)]
        public async Task<int> ExecuteNonQueryAsync()
        {
            int affectedRows = 0;
            for (int operation = 0; operation < OperationsPerBatch; operation++)
            {
                affectedRows += await _nonQuery.ExecuteNonQueryAsync();
            }
            return affectedRows;
        }

        /// <summary>Executes an XML result and consumes all XML nodes.</summary>
        [Benchmark(OperationsPerInvoke = OperationsPerBatch)]
        public int ExecuteXmlReader()
        {
            int nodes = 0;
            for (int operation = 0; operation < OperationsPerBatch; operation++)
            {
                using XmlReader reader = _xml.ExecuteXmlReader();
                while (reader.Read())
                {
                    nodes++;
                }
            }
            return nodes;
        }

        /// <summary>Executes and fully consumes XML through the actual async APIs.</summary>
        [Benchmark(OperationsPerInvoke = OperationsPerBatch)]
        public async Task<int> ExecuteXmlReaderAsync()
        {
            int nodes = 0;
            for (int operation = 0; operation < OperationsPerBatch; operation++)
            {
                using XmlReader reader = await _xml.ExecuteXmlReaderAsync();
                while (await reader.ReadAsync())
                {
                    nodes++;
                }
            }
            return nodes;
        }
    }
}

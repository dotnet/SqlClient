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

            if (ExecuteNonQuery() != 1 || ExecuteNonQueryAsync().GetAwaiter().GetResult() != 1 ||
                (int)ExecuteScalar() != 123 || (int)ExecuteScalarAsync().GetAwaiter().GetResult() != 123 ||
                ExecuteReader() != 124 || ExecuteReaderAsync().GetAwaiter().GetResult() != 124 ||
                ExecuteXmlReader() != 10 || ExecuteXmlReaderAsync().GetAwaiter().GetResult() != 10)
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
        [Benchmark]
        public int ExecuteReader()
        {
            using SqlDataReader reader = _reader.ExecuteReader();
            int sum = 0;
            while (reader.Read())
            {
                sum += reader.GetInt32(0) + reader.GetInt32(1);
            }
            return sum;
        }

        /// <summary>Executes and advances asynchronously, then accesses the current row's typed fields.</summary>
        [Benchmark]
        public async Task<int> ExecuteReaderAsync()
        {
            using SqlDataReader reader = await _reader.ExecuteReaderAsync();
            int sum = 0;
            while (await reader.ReadAsync())
            {
                sum += reader.GetInt32(0) + reader.GetInt32(1);
            }
            return sum;
        }

        /// <summary>Measures first-value execution without command allocation or unused result rows.</summary>
        [Benchmark]
        public object ExecuteScalar() => _scalar.ExecuteScalar();

        /// <summary>Measures the corresponding asynchronous first-value execution path.</summary>
        [Benchmark]
        public Task<object> ExecuteScalarAsync() => _scalar.ExecuteScalarAsync();

        /// <summary>Updates the same row to the same value without growing the workload.</summary>
        [Benchmark]
        public int ExecuteNonQuery() => _nonQuery.ExecuteNonQuery();

        /// <summary>Measures asynchronous nonquery execution over the same stable update.</summary>
        [Benchmark]
        public Task<int> ExecuteNonQueryAsync() => _nonQuery.ExecuteNonQueryAsync();

        /// <summary>Executes an XML result and consumes all XML nodes.</summary>
        [Benchmark]
        public int ExecuteXmlReader()
        {
            using XmlReader reader = _xml.ExecuteXmlReader();
            int nodes = 0;
            while (reader.Read())
            {
                nodes++;
            }
            return nodes;
        }

        /// <summary>Executes and fully consumes XML through the actual async APIs.</summary>
        [Benchmark]
        public async Task<int> ExecuteXmlReaderAsync()
        {
            using XmlReader reader = await _xml.ExecuteXmlReaderAsync();
            int nodes = 0;
            while (await reader.ReadAsync())
            {
                nodes++;
            }
            return nodes;
        }
    }
}

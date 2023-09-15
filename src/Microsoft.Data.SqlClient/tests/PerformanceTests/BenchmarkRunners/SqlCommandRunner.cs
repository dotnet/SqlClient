// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.Xml;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public class SqlCommandRunner : BaseRunner
    {
        private static SqlConnection s_sqlConnection;
        private Table _table;
        private string _query;

        [GlobalSetup]
        public void Setup()
        {
            s_sqlConnection = new(s_config.ConnectionString);
            s_sqlConnection.Open();

            _table = TablePatterns.TableAll25Columns(s_datatypes, nameof(SqlCommandRunner))
                    .CreateTable(s_sqlConnection)
                    .InsertBulkRows(s_config.Benchmarks.SqlCommandRunnerConfig.RowCount, s_sqlConnection);

            _query = $"SELECT * FROM {_table.Name}";
        }

        [GlobalCleanup]
        public void Dispose()
        {
            _table.DropTable(s_sqlConnection);
            s_sqlConnection.Close();
            SqlConnection.ClearAllPools();
        }

        [Benchmark]
        public void ExecuteReader()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            using SqlDataReader reader = sqlCommand.ExecuteReader();
            while (reader.Read())
            { }
        }

        [Benchmark]
        public async Task ExecuteReaderAsync()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            using SqlDataReader reader = await sqlCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            { }
        }

        [Benchmark]
        public void ExecuteScalar()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            _ = sqlCommand.ExecuteScalar();
        }

        [Benchmark]
        public async Task ExecuteScalarAsync()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            _ = await sqlCommand.ExecuteScalarAsync();
        }

        [Benchmark]
        public void ExecuteNonQuery()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            sqlCommand.ExecuteNonQuery();
        }

        [Benchmark]
        public async Task ExecuteNonQueryAsync()
        {
            using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            await sqlCommand.ExecuteNonQueryAsync();
        }

        [Benchmark]
        public void ExecuteXmlReader()
        {
            using SqlCommand sqlCommand = new(_query + " FOR XML AUTO, BINARY BASE64", s_sqlConnection);
            using XmlReader reader = sqlCommand.ExecuteXmlReader();
            while (reader.Read())
            { }
        }

        [Benchmark]
        public async Task ExecuteXmlReaderAsync()
        {
            using SqlCommand sqlCommand = new(_query + " FOR XML AUTO, BINARY BASE64", s_sqlConnection);
            using XmlReader reader = await sqlCommand.ExecuteXmlReaderAsync();
            while (await reader.ReadAsync())
            { }
        }
    }
}

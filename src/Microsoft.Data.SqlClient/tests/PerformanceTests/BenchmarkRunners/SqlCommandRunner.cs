// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
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
        private readonly string _query_data_varbinary = "SELECT foo FROM data_varbinary";
        private readonly string _query_data_varchar = "SELECT foo FROM data_varchar";

        [GlobalSetup]
        public void Setup()
        {
            s_sqlConnection = new(s_config.ConnectionString);
            s_sqlConnection.Open();

            _table = TablePatterns.TableAll25Columns(s_datatypes, nameof(SqlCommandRunner))
                    .CreateTable(s_sqlConnection)
                    .InsertBulkRows(s_config.Benchmarks.SqlCommandRunnerConfig.RowCount, s_sqlConnection);

            _query = $"SELECT * FROM {_table.Name}";

            using var cmd = s_sqlConnection.CreateCommand();
            cmd.CommandText = @"
IF OBJECT_ID('dbo.data_varbinary', 'U') IS NOT NULL
DROP TABLE dbo.data_varbinary; 
CREATE TABLE dbo.data_varbinary (id INT, foo VARBINARY(MAX))
";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO dbo.data_varbinary (id, foo) VALUES (@id, @foo)";
            cmd.Parameters.AddWithValue("id", 1);
            cmd.Parameters.AddWithValue("foo", new byte[1024 * 1024 * 10]);
            cmd.ExecuteNonQuery();

            using var cmd2 = s_sqlConnection.CreateCommand();
            cmd2.CommandText = @"
IF OBJECT_ID('dbo.data_varchar', 'U') IS NOT NULL
DROP TABLE dbo.data_varchar; 
CREATE TABLE dbo.data_varchar (id INT, foo VARCHAR(MAX))
";
            cmd2.ExecuteNonQuery();

            cmd2.CommandText = "INSERT INTO dbo.data_varchar (id, foo) VALUES (@id, @foo)";
            cmd2.Parameters.AddWithValue("id", 1);
            cmd2.Parameters.AddWithValue("foo",  Encoding.UTF8.GetString(new byte[1024 * 1024 * 10]));
            cmd2.ExecuteNonQuery();
        }

        [GlobalCleanup]
        public void Dispose()
        {
            _table.DropTable(s_sqlConnection);
            using var cmd = s_sqlConnection.CreateCommand();
            cmd.CommandText = @"IF OBJECT_ID('dbo.data_varbinary', 'U') IS NOT NULL
                DROP TABLE data_varbinary;";
            cmd.ExecuteNonQuery();
            using var cmd2 = s_sqlConnection.CreateCommand();
            cmd2.CommandText = @"IF OBJECT_ID('dbo.data_varchar', 'U') IS NOT NULL
                DROP TABLE data_varchar;";
            cmd2.ExecuteNonQuery();
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
            await using SqlCommand sqlCommand = new(_query, s_sqlConnection);
            await using SqlDataReader reader = await sqlCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            { }
        }

        [Benchmark]
        public void VARBINARY_MAX_ExecuteReader()
        {
            using SqlCommand sqlCommand = new(_query_data_varbinary, s_sqlConnection);
            using SqlDataReader reader = sqlCommand.ExecuteReader();
            while (reader.Read())
            { }
        }

        [Benchmark]
        public async Task VARBINARY_MAX_ExecuteReaderAsync()
        {
            await using SqlCommand sqlCommand = new(_query_data_varbinary, s_sqlConnection);
            await using SqlDataReader reader = await sqlCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            { }
        }

        [Benchmark]
        public void VARCHAR_MAX_ExecuteReader()
        {
            using SqlCommand sqlCommand = new(_query_data_varchar, s_sqlConnection);
            using SqlDataReader reader = sqlCommand.ExecuteReader();
            while (reader.Read())
            { }
        }

        [Benchmark]
        public async Task VARCHAR_MAX_ExecuteReaderAsync()
        {
            await using SqlCommand sqlCommand = new(_query_data_varchar, s_sqlConnection);
            await using SqlDataReader reader = await sqlCommand.ExecuteReaderAsync();
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
            await using SqlCommand sqlCommand = new(_query, s_sqlConnection);
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
            await using SqlCommand sqlCommand = new(_query, s_sqlConnection);
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
            await using SqlCommand sqlCommand = new(_query + " FOR XML AUTO, BINARY BASE64", s_sqlConnection);
            using XmlReader reader = await sqlCommand.ExecuteXmlReaderAsync();
            while (await reader.ReadAsync())
            { }
        }

        [Benchmark]
        public void VARBINARY_MAX_ExecuteScalar()
        {
            using var cmd = new SqlCommand(_query_data_varbinary, s_sqlConnection);

            _ = cmd.ExecuteScalar();
            
        }

        [Benchmark]
        public async Task VARBINARY_MAX_ExecuteScalarAsync()
        {
            await using var cmd = new SqlCommand(_query_data_varbinary, s_sqlConnection);

            _ = await cmd.ExecuteScalarAsync();
        }

        [Benchmark]
        public void VARCHAR_MAX_ExecuteScalar()
        {
            using var cmd = new SqlCommand(_query_data_varchar, s_sqlConnection);

            _ = cmd.ExecuteScalar();
            
        }

        [Benchmark]
        public async Task VARCHAR_MAX_ExecuteScalarAsync()
        {
            await using var cmd = new SqlCommand(_query_data_varchar, s_sqlConnection);

            _ = await cmd.ExecuteScalarAsync();
        }
        
    }
}

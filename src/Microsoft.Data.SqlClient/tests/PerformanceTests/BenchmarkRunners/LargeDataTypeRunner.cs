// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    [MemoryDiagnoser]
    public class LargeDataTypeRunner : BaseRunner
    {
        private SqlConnection _connection;
        private SqlCommand _readCommand;
        private string _tableName;

        /// <summary>
        /// Amount of data (in MB) to insert into the large columns.
        /// </summary>
        [Params(1, 2, 5)]
        public int DataSizeMB { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            int dataSizeBytes = DataSizeMB * 1024 * 1024;

            _connection = new SqlConnection(s_config.ConnectionString);
            _connection.Open();

            _tableName = "PerfTest_LargeDT_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            string createSql = $@"
                CREATE TABLE [{_tableName}] (
                    [ID] INT PRIMARY KEY,
                    [VarcharMaxCol] VARCHAR(MAX),
                    [NvarcharMaxCol] NVARCHAR(MAX),
                    [VarbinaryMaxCol] VARBINARY(MAX),
                    [XmlCol] XML
                )";
            using (var cmd = new SqlCommand(createSql, _connection))
            {
                cmd.ExecuteNonQuery();
            }

            string largeString = new string('s', dataSizeBytes);
            byte[] largeBinary = new byte[dataSizeBytes];
            new System.Random(42).NextBytes(largeBinary);
            string largeXml = $"<root>{new string('x', dataSizeBytes / 2)}</root>";

            string insertSql = $@"
                INSERT INTO [{_tableName}] ([ID], [VarcharMaxCol], [NvarcharMaxCol], [VarbinaryMaxCol], [XmlCol])
                VALUES (@id, @varchar, @nvarchar, @varbinary, @xml)";
            using var insertCmd = new SqlCommand(insertSql, _connection);
            insertCmd.Parameters.AddWithValue("@id", DataSizeMB);
            insertCmd.Parameters.Add("@varchar", SqlDbType.VarChar).Value = largeString;
            insertCmd.Parameters.Add("@nvarchar", SqlDbType.NVarChar).Value = largeString;
            insertCmd.Parameters.Add("@varbinary", SqlDbType.VarBinary).Value = largeBinary;
            insertCmd.Parameters.Add("@xml", SqlDbType.Xml).Value = largeXml;
            insertCmd.ExecuteNonQuery();

            _readCommand = new SqlCommand(
                $"SELECT [VarcharMaxCol], [NvarcharMaxCol], [VarbinaryMaxCol], [XmlCol] FROM [{_tableName}] WHERE [ID] = @id",
                _connection);
            _readCommand.Parameters.AddWithValue("@id", DataSizeMB);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            try
            {
                _readCommand?.Dispose();
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
        public int ReadAllColumnsSync()
        {
            using var reader = _readCommand.ExecuteReader();
            if (reader.Read())
            {
                var varchar = reader.GetString(0);
                var nvarchar = reader.GetString(1);
                var varbinary = (byte[])reader.GetValue(2);
                var xml = reader.GetString(3);

                return varchar.Length + nvarchar.Length + varbinary.Length + xml.Length;
            }
            return 0;
        }

        [Benchmark]
        public async Task<int> ReadAllColumnsAsync()
        {
            using var reader = await _readCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var varchar = await reader.GetFieldValueAsync<string>(0);
                var nvarchar = await reader.GetFieldValueAsync<string>(1);
                var varbinary = await reader.GetFieldValueAsync<byte[]>(2);
                var xml = await reader.GetFieldValueAsync<string>(3);

                return varchar.Length + nvarchar.Length + varbinary.Length + xml.Length;
            }
            return 0;
        }
    }
}

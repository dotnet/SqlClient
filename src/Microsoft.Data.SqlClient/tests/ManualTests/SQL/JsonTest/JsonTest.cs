// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.Text.Json;
using Microsoft.Data.SqlTypes;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class JsonTest
    {
        private const string JsonDataString = "[{\"name\":\"Dave\",\"skills\":[\"Python\"]},{\"name\":\"Ron\",\"surname\":\"Peter\"}]";

        private readonly ITestOutputHelper _output;

        public JsonTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private void ValidateRowsAffected(int rowsAffected)
        {
            _output.WriteLine($"Rows affected: {rowsAffected}");
            Assert.Equal(1, rowsAffected);
        }

        private void ValidateRows(SqlDataReader reader)
        {
            while (reader.Read())
            {
                string jsonData = reader.GetString(0);
                _output.WriteLine(jsonData);
                Assert.Equal(JsonDataString, jsonData);
            }
        }

        private async Task ValidateRowsAsync(SqlDataReader reader)
        {
            while (await reader.ReadAsync())
            {
                string jsonData = reader.GetString(0);
                _output.WriteLine(jsonData);
                Assert.Equal(JsonDataString, jsonData);
            }
        }

        private void ValidateSchema(SqlDataReader reader)
        {
            System.Collections.ObjectModel.ReadOnlyCollection<DbColumn> schema = reader.GetColumnSchema();
            foreach (DbColumn column in schema)
            {
                _output.WriteLine("Column Name is " + column.ColumnName);
                _output.WriteLine("Column DataType is " + column?.DataType.ToString());
                _output.WriteLine("Column DataTypeName is " + column.DataTypeName);
                Assert.Equal("json", column.DataTypeName);
            }
        }

        private void ValidateNullJson(SqlDataReader reader)
        {
            while (reader.Read())
            {
                bool IsNull = reader.IsDBNull(0);
                _output.WriteLine(IsNull ? "null" : "not null");
                Assert.True(IsNull);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestJsonWrite()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            using Table jsonTable = new(connection, nameof(TestJsonWrite), "(data json)");
            string tableInsert = $"INSERT INTO {jsonTable.Name} VALUES (@jsonData)";
            using StoredProcedure insertJsonProcedure = new(connection, nameof(TestJsonWrite), $"(@jsonData json) AS {tableInsert}");

            using SqlCommand command = connection.CreateCommand();

            command.CommandText = tableInsert;
            var parameter = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json);
            command.Parameters.Add(parameter);

            //Test 1
            //Write json value using a parameterized query
            parameter.Value = JsonDataString;
            int rowsAffected = command.ExecuteNonQuery();
            ValidateRowsAffected(rowsAffected);

            //Test 2 
            //Write a SqlString type as json
            parameter.Value = new SqlString(JsonDataString);
            int rowsAffected2 = command.ExecuteNonQuery();
            ValidateRowsAffected(rowsAffected2);

            //Test 3
            //Write json value using SP
            using (SqlCommand command2 = connection.CreateCommand())
            {
                command2.CommandText = insertJsonProcedure.Name;
                command2.CommandType = CommandType.StoredProcedure;
                command2.Parameters.Add(new SqlParameter("@jsonData", SqlDbTypeExtensions.Json) { Value = JsonDataString });
                int rowsAffected3 = command2.ExecuteNonQuery();
                ValidateRowsAffected(rowsAffected3);
            }

            //Test 4
            // Write json value using a parameterized query with SqlJson type
            parameter.Value = new SqlJson(JsonDataString);
            int rowsAffected4 = command.ExecuteNonQuery();
            ValidateRowsAffected(rowsAffected4);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public async Task TestJsonWriteAsync()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();

            using Table jsonTable = new(connection, nameof(TestJsonWriteAsync), "(data json)");
            string tableInsert = $"INSERT INTO {jsonTable.Name} VALUES (@jsonData)";
            using StoredProcedure insertJsonProcedure = new(connection, nameof(TestJsonWriteAsync), $"(@jsonData json) AS {tableInsert}");

            using SqlCommand command = connection.CreateCommand();
            command.CommandText = tableInsert;
            SqlParameter parameter = new("@jsonData", SqlDbTypeExtensions.Json);
            command.Parameters.Add(parameter);

            //Test 1
            //Write json value using a parameterized query
            parameter.Value = JsonDataString;
            int rowsAffected = await command.ExecuteNonQueryAsync();
            ValidateRowsAffected(rowsAffected);

            //Test 2 
            //Write a SqlString type as json
            parameter.Value = new SqlString(JsonDataString);
            int rowsAffected2 = await command.ExecuteNonQueryAsync();
            ValidateRowsAffected(rowsAffected2);

            //Test 3
            //Write json value using SP
            using (SqlCommand command2 = connection.CreateCommand())
            {
                command2.CommandText = insertJsonProcedure.Name;
                command2.CommandType = CommandType.StoredProcedure;
                command2.Parameters.Add(new SqlParameter("@jsonData", SqlDbTypeExtensions.Json) { Value = JsonDataString });
                int rowsAffected3 = await command.ExecuteNonQueryAsync();
                ValidateRowsAffected(rowsAffected3);
            }

            //Test 4
            // Write json value using a parameterized query with SqlJson type
            parameter.Value = new SqlJson(JsonDataString);
            int rowsAffected4 = await command.ExecuteNonQueryAsync();
            ValidateRowsAffected(rowsAffected4);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestJsonRead()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            using Table jsonTable = new(connection, nameof(TestJsonRead), "(data json)");
            string tableInsert = $"INSERT INTO {jsonTable.Name} VALUES (@jsonData)";
            string tableRead = $"SELECT * FROM {jsonTable.Name}";
            using StoredProcedure readJsonProcedure = new(connection, nameof(TestJsonRead), $"AS {tableRead}");

            using SqlCommand command = connection.CreateCommand();

            //Insert sample json data
            //This will be used for reading
            command.CommandText = tableInsert;
            command.Parameters.Add(new SqlParameter("@jsonData", SqlDbTypeExtensions.Json) { Value = JsonDataString });
            command.ExecuteNonQuery();

            //Test 1
            //Read json value using query
            command.CommandText = tableRead;
            using (SqlDataReader reader = command.ExecuteReader())
            {
                ValidateRows(reader);

                //Test 2
                //Read the column metadata
                ValidateSchema(reader);
            }

            //Test 3
            //Read json value using SP
            using SqlCommand command2 = connection.CreateCommand();
            command2.CommandText = readJsonProcedure.Name;
            command2.CommandType = CommandType.StoredProcedure;
            using SqlDataReader reader2 = command2.ExecuteReader();
            ValidateRows(reader2);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public async Task TestJsonReadAsync()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();

            using Table jsonTable = new(connection, nameof(TestJsonReadAsync), "(data json)");
            string tableInsert = $"INSERT INTO {jsonTable.Name} VALUES (@jsonData)";
            string tableRead = $"SELECT * FROM {jsonTable.Name}";
            using StoredProcedure readJsonProcedure = new(connection, nameof(TestJsonRead), $"AS {tableRead}");

            using SqlCommand command = connection.CreateCommand();

            //Insert sample json data
            //This will be used for reading
            command.CommandText = tableInsert;
            command.Parameters.Add(new SqlParameter("@jsonData", SqlDbTypeExtensions.Json) { Value = JsonDataString });
            await command.ExecuteNonQueryAsync();

            //Test 1
            //Read json value using query
            command.CommandText = tableRead;
            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                await ValidateRowsAsync(reader);

                //Test 2
                //Read the column metadata
                ValidateSchema(reader);
            }

            //Test 3
            //Read json value using SP
            using SqlCommand command2 = connection.CreateCommand();
            command2.CommandText = readJsonProcedure.Name;
            command2.CommandType = CommandType.StoredProcedure;
            using SqlDataReader reader2 = await command2.ExecuteReaderAsync();
            await ValidateRowsAsync(reader2);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestNullJson()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            using Table jsonTable = new(connection, nameof(TestNullJson), "(data json)");
            string tableInsert = $"INSERT INTO {jsonTable.Name} VALUES (@jsonData)";
            string tableRead = $"SELECT * FROM {jsonTable.Name}";

            using SqlCommand command = connection.CreateCommand();

            //Insert Null value
            command.CommandText = tableInsert;
            command.Parameters.Add(new SqlParameter("@jsonData", SqlDbTypeExtensions.Json) { Value = DBNull.Value });
            command.ExecuteNonQuery();

            //Query the table
            command.CommandText = tableRead;
            using SqlDataReader reader = command.ExecuteReader();
            ValidateNullJson(reader);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestJsonAPIs()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            using Table jsonTable = new(connection, nameof(TestJsonAPIs), "(data json)");
            string tableInsert = $"INSERT INTO {jsonTable.Name} VALUES (@jsonData)";
            string tableRead = $"SELECT * FROM {jsonTable.Name}";

            using SqlCommand command = connection.CreateCommand();
            //Insert
            command.CommandText = tableInsert;
            command.Parameters.Add(new SqlParameter("@jsonData", SqlDbTypeExtensions.Json) { Value = JsonDataString });
            command.ExecuteNonQuery();

            // Query the table
            command.CommandText = tableRead;
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string data = reader.GetFieldValue<string>(0);
                Assert.Equal(JsonDataString, data);
                JsonDocument jsonDocument = reader.GetFieldValue<JsonDocument>(0);
                Assert.Equal(JsonDataString, jsonDocument.RootElement.ToString());
                Assert.Equal("json", reader.GetDataTypeName(0));
                Assert.Equal("System.String", reader.GetFieldType(0).ToString());
                Assert.Equal(JsonDataString, reader.GetSqlJson(0).Value);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestJsonWithMARS()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString + "MultipleActiveResultSets=True;");
            connection.Open();

            using Table jsonTable1 = new(connection, nameof(TestJsonWithMARS), "(Data json)");
            using Table jsonTable2 = new(connection, nameof(TestJsonWithMARS), "(Id int, Data json)");

            // Insert Data
            string table1Insert = $"INSERT INTO {jsonTable1.Name} VALUES ('{JsonDataString}')";
            string table2Insert = $"INSERT INTO {jsonTable2.Name} VALUES (1,'{JsonDataString}')";
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = table1Insert;
                command.ExecuteNonQuery();
                command.CommandText = table2Insert;
                command.ExecuteNonQuery();
            }

            // Read Data
            using SqlCommand command1 = new($"select * from {jsonTable1.Name}", connection);
            using SqlCommand command2 = new($"select * from {jsonTable2.Name}", connection);

            using (SqlDataReader reader1 = command1.ExecuteReader())
            {
                while (reader1.Read())
                {
                    Assert.Equal(JsonDataString, reader1["data"]);
                }

                using SqlDataReader reader2 = command2.ExecuteReader();
                while (reader2.Read())
                {
                    Assert.Equal(1, reader2["Id"]);
                    Assert.Equal(JsonDataString, reader2["data"]);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestJsonSPParams()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            using Table jsonTable = new(connection, nameof(TestJsonSPParams), "(Id int, Data json)");
            using StoredProcedure readJsonProcedure = new(connection, nameof(TestJsonRead), $@"
                @id int,
                @jsonData json OUTPUT
                AS
                BEGIN
                    SELECT @jsonData = (SELECT Data FROM {jsonTable.Name} WHERE Id = @id)
                END;");
            string tableInsert = $"INSERT INTO {jsonTable.Name} VALUES (@id, @jsonData)";

            // Insert Data
            using SqlCommand command = new(tableInsert, connection);
            command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = 1 });
            command.Parameters.Add(new SqlParameter("@jsonData", SqlDbTypeExtensions.Json) { Value = JsonDataString });
            command.ExecuteNonQuery();

            // Execute Stored Procedure
            using SqlCommand spCommand = new(readJsonProcedure.Name, connection);

            spCommand.CommandType = CommandType.StoredProcedure;
            spCommand.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Direction = ParameterDirection.Input, Value = 1 });
            SqlParameter outputParam = new("@jsonData", SqlDbTypeExtensions.Json) { Direction = ParameterDirection.Output };
            spCommand.Parameters.Add(outputParam);
            spCommand.ExecuteNonQuery();
            Assert.Equal(JsonDataString, (string)outputParam.Value);
        }
    }
}

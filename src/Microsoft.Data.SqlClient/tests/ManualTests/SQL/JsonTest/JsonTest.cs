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

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class JsonTest
    {
        private readonly ITestOutputHelper _output;

        public JsonTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private static readonly string JsonDataString = "[{\"name\":\"Dave\",\"skills\":[\"Python\"]},{\"name\":\"Ron\",\"surname\":\"Peter\"}]";

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
            string tableName = DataTestUtility.GenerateObjectName();
            string spName = DataTestUtility.GenerateObjectName();

            string tableInsert = "INSERT INTO " + tableName + " VALUES (@jsonData)";
            string spCreate = "CREATE PROCEDURE " + spName + " (@jsonData json) AS " + tableInsert;

            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    //Create Table
                    DataTestUtility.CreateTable(connection, tableName, "(data json)");

                    //Create SP for writing json values
                    DataTestUtility.DropStoredProcedure(connection, spName);
                    command.CommandText = spCreate;
                    command.ExecuteNonQuery();

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
                        command2.CommandText = spName;
                        command2.CommandType = CommandType.StoredProcedure;
                        var parameter2 = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json);
                        parameter2.Value = JsonDataString;
                        command2.Parameters.Add(parameter2);
                        int rowsAffected3 = command2.ExecuteNonQuery();
                        ValidateRowsAffected(rowsAffected3);
                    }

                    //Test 4
                    // Write json value using a parameterized query with SqlJson type
                    parameter.Value = new SqlJson(JsonDataString);
                    int rowsAffected4 = command.ExecuteNonQuery();
                    ValidateRowsAffected(rowsAffected4);

                    DataTestUtility.DropTable(connection, tableName);
                    DataTestUtility.DropStoredProcedure(connection, spName);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public async Task TestJsonWriteAsync()
        {
            string tableName = DataTestUtility.GenerateObjectName();
            string spName = DataTestUtility.GenerateObjectName();

            string tableInsert = "INSERT INTO " + tableName + " VALUES (@jsonData)";
            string spCreate = "CREATE PROCEDURE " + spName + " (@jsonData json) AS " + tableInsert;

            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();

                using (SqlCommand command = connection.CreateCommand())
                {
                    //Create Table
                    DataTestUtility.CreateTable(connection, tableName, "(data json)");

                    //Create SP for writing json values
                    DataTestUtility.DropStoredProcedure(connection, spName);
                    command.CommandText = spCreate;
                    await command.ExecuteNonQueryAsync();

                    command.CommandText = tableInsert;
                    var parameter = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json);
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
                        command2.CommandText = spName;
                        command2.CommandType = CommandType.StoredProcedure;
                        var parameter2 = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json);
                        parameter2.Value = JsonDataString;
                        command2.Parameters.Add(parameter2);
                        int rowsAffected3 = await command.ExecuteNonQueryAsync();
                        ValidateRowsAffected(rowsAffected3);
                    }

                    //Test 4
                    // Write json value using a parameterized query with SqlJson type
                    parameter.Value = new SqlJson(JsonDataString);
                    int rowsAffected4 = await command.ExecuteNonQueryAsync();
                    ValidateRowsAffected(rowsAffected4);

                    DataTestUtility.DropTable(connection, tableName);
                    DataTestUtility.DropStoredProcedure(connection, spName);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestJsonRead()
        {
            string tableName = DataTestUtility.GenerateObjectName();
            string spName = DataTestUtility.GenerateObjectName();

            string tableInsert = "INSERT INTO " + tableName + " VALUES (@jsonData)";
            string tableRead = "SELECT * FROM " + tableName;
            string spCreate = "CREATE PROCEDURE " + spName + " AS " + tableRead;

            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    //Create Table
                    DataTestUtility.CreateTable(connection, tableName, "(data json)");

                    //Create SP for reading from json column
                    DataTestUtility.DropStoredProcedure(connection, spName);
                    command.CommandText = spCreate;
                    command.ExecuteNonQuery();

                    //Insert sample json data
                    //This will be used for reading
                    command.CommandText = tableInsert;
                    var parameter = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json);
                    parameter.Value = JsonDataString;
                    command.Parameters.Add(parameter);
                    command.ExecuteNonQuery();

                    //Test 1
                    //Read json value using query
                    command.CommandText = tableRead;
                    var reader = command.ExecuteReader();
                    ValidateRows(reader);

                    //Test 2
                    //Read the column metadata
                    ValidateSchema(reader);
                    reader.Close();

                    //Test 3
                    //Read json value using SP
                    using (SqlCommand command2 = connection.CreateCommand())
                    {
                        command2.CommandText = spName;
                        command2.CommandType = CommandType.StoredProcedure;
                        var reader2 = command2.ExecuteReader();
                        ValidateRows(reader2);
                        reader2.Close();
                    }

                    DataTestUtility.DropTable(connection, tableName);
                    DataTestUtility.DropStoredProcedure(connection, spName);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public async Task TestJsonReadAsync()
        {
            string tableName = DataTestUtility.GenerateObjectName();
            string spName = DataTestUtility.GenerateObjectName();

            string tableInsert = "INSERT INTO " + tableName + " VALUES (@jsonData)";
            string tableRead = "SELECT * FROM " + tableName;
            string spCreate = "CREATE PROCEDURE " + spName + " AS " + tableRead;

            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = connection.CreateCommand())
                {
                    //Create Table
                    DataTestUtility.CreateTable(connection, tableName, "(data json)");

                    //Create SP for reading from json column
                    DataTestUtility.DropStoredProcedure(connection, spName);
                    command.CommandText = spCreate;
                    await command.ExecuteNonQueryAsync();

                    //Insert sample json data
                    //This will be used for reading
                    command.CommandText = tableInsert;
                    var parameter = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json);
                    parameter.Value = JsonDataString;
                    command.Parameters.Add(parameter);
                    await command.ExecuteNonQueryAsync();

                    //Test 1
                    //Read json value using query
                    command.CommandText = tableRead;
                    var reader = await command.ExecuteReaderAsync();
                    await ValidateRowsAsync(reader);

                    //Test 2
                    //Read the column metadata
                    ValidateSchema(reader);
                    reader.Close();

                    //Test 3
                    //Read json value using SP
                    using (SqlCommand command2 = connection.CreateCommand())
                    {
                        command2.CommandText = spName;
                        command2.CommandType = CommandType.StoredProcedure;
                        var reader2 = await command2.ExecuteReaderAsync();
                        await ValidateRowsAsync(reader2);
                        reader2.Close();
                    }

                    DataTestUtility.DropTable(connection, tableName);
                    DataTestUtility.DropStoredProcedure(connection, spName);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestNullJson()
        {
            string tableName = DataTestUtility.GenerateObjectName();

            string tableInsert = "INSERT INTO " + tableName + " VALUES (@jsonData)";
            string tableRead = "SELECT * FROM " + tableName;

            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();
            using SqlCommand command = connection.CreateCommand();

            //Create Table
            DataTestUtility.CreateTable(connection, tableName, "(Data json)");

            //Insert Null value
            command.CommandText = tableInsert;
            var parameter = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json);
            parameter.Value = DBNull.Value;
            command.Parameters.Add(parameter);
            command.ExecuteNonQuery();

            //Query the table
            command.CommandText = tableRead;
            var reader = command.ExecuteReader();
            ValidateNullJson(reader);

            reader.Close();
            DataTestUtility.DropTable(connection, tableName);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestJsonAPIs()
        {
            string tableName = DataTestUtility.GenerateObjectName();
            string tableInsert = "INSERT INTO " + tableName + " VALUES (@jsonData)";
            string tableRead = "SELECT * FROM " + tableName;

            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    try
                    {
                        // Create Table
                        DataTestUtility.CreateTable(connection, tableName, "(Data json)");

                        //Insert
                        command.CommandText = tableInsert;
                        var parameter = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json);
                        parameter.Value = JsonDataString;
                        command.Parameters.Add(parameter);
                        command.ExecuteNonQuery();

                        // Query the table
                        command.CommandText = tableRead;
                        using (var reader = command.ExecuteReader())
                        {
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
                    }
                    finally
                    {
                        DataTestUtility.DropTable(connection, tableName);
                    }
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestJsonWithMARS()
        {
            string table1Name = DataTestUtility.GenerateObjectName();
            string table2Name = DataTestUtility.GenerateObjectName();

            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString + "MultipleActiveResultSets=True;"))
            {
                connection.Open();
                try
                {
                    // Create Table
                    DataTestUtility.CreateTable(connection, table1Name, "(Data json)");
                    DataTestUtility.CreateTable(connection, table2Name, "(Id int, Data json)");

                    // Insert Data
                    string table1Insert = "INSERT INTO " + table1Name + " VALUES ('" + JsonDataString + "')";
                    string table2Insert = "INSERT INTO " + table2Name + " VALUES (1,'" + JsonDataString + "')";
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = table1Insert;
                        command.ExecuteNonQuery();
                        command.CommandText = table2Insert;
                        command.ExecuteNonQuery();
                    }

                    // Read Data
                    using (SqlCommand command1 = new SqlCommand("select * from " + table1Name, connection))
                    using (SqlCommand command2 = new SqlCommand("select * from " + table2Name, connection))
                    {
                        using (SqlDataReader reader1 = command1.ExecuteReader())
                        {
                            while (reader1.Read())
                            {
                                Assert.Equal(JsonDataString, reader1["data"]);
                            }

                            using (SqlDataReader reader2 = command2.ExecuteReader())
                            {
                                while (reader2.Read())
                                {
                                    Assert.Equal(1, reader2["Id"]);
                                    Assert.Equal(JsonDataString, reader2["data"]);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    DataTestUtility.DropTable(connection, table1Name);
                    DataTestUtility.DropTable(connection, table2Name);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAzureServer), nameof(DataTestUtility.IsNotManagedInstance))]
        public void TestJsonSPParams()
        {
            string tableName = DataTestUtility.GenerateObjectName();
            string procName = DataTestUtility.GenerateObjectName();
            string tableInsert = $"INSERT INTO {tableName} VALUES (@id, @jsonData)";
            string tableRead = $"SELECT * FROM {tableName}";

            using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                try
                {
                    // Create Table
                    DataTestUtility.CreateTable(connection, tableName, "(Id int, Data json)");

                    // Create Stored Procedure
                    string createSP = $@"
                @id int,
                @jsonData json OUTPUT
                AS
                BEGIN
                    SELECT @jsonData = (SELECT Data FROM {tableName} WHERE Id = @id)
                END;";
                    DataTestUtility.CreateSP(connection, procName, createSP);

                    // Insert Data
                    using (SqlCommand command = new SqlCommand(tableInsert, connection))
                    {
                        command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = 1 });
                        command.Parameters.Add(new SqlParameter("@jsonData", SqlDbTypeExtensions.Json) { Value = JsonDataString });
                        command.ExecuteNonQuery();
                    }

                    // Execute Stored Procedure
                    using (SqlCommand spCommand = new SqlCommand(procName, connection))
                    {
                        spCommand.CommandType = CommandType.StoredProcedure;
                        spCommand.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Direction = ParameterDirection.Input, Value = 1 });
                        SqlParameter outputParam = new SqlParameter("@jsonData", SqlDbTypeExtensions.Json) { Direction = ParameterDirection.Output };
                        spCommand.Parameters.Add(outputParam);
                        spCommand.ExecuteNonQuery();
                        Assert.Equal(JsonDataString, (string)outputParam.Value);
                    }
                }
                finally
                {
                    DataTestUtility.DropTable(connection, tableName);
                }
            }
        }
    }
}

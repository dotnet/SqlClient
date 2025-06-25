// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest
{
    public sealed class VectorTypeBackwardCompatibilityTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private static readonly string s_connectionString = ManualTesting.Tests.DataTestUtility.TCPConnectionString;
        private static readonly string s_tableName = DataTestUtility.GetUniqueName("VectorTestTable");
        private static readonly string s_bulkCopySrcTableName = DataTestUtility.GetUniqueName("VectorBulkCopyTestTable");
        private static readonly string s_bulkCopySrcTableDef = $@"(Id INT PRIMARY KEY IDENTITY, VectorData varchar(max) NULL)";
        private static readonly string s_tableDefinition = $@"(Id INT PRIMARY KEY IDENTITY, VectorData vector(3) NULL)";
        private static readonly string s_selectCmdString = $"SELECT VectorData FROM {s_tableName} ORDER BY Id DESC";
        private static readonly string s_insertCmdString = $"INSERT INTO {s_tableName} (VectorData) VALUES (@VectorData)";
        private static readonly string s_vectorParamName = $"@VectorData";
        private static readonly string s_storedProcName = DataTestUtility.GetUniqueName("VectorsAsVarcharSp");
        private static readonly string s_storedProcBody = $@"
                @InputVectorJson VARCHAR(MAX),   -- Input: Serialized float[] as JSON string
                @OutputVectorJson VARCHAR(MAX) OUTPUT  -- Output: Echoed back from latest inserted row
                AS
                BEGIN
                SET NOCOUNT ON;

                -- Insert into vector table
                INSERT INTO {s_tableName} (VectorData)
                VALUES (@InputVectorJson);

                -- Retrieve latest entry (assumes auto-incrementing ID)
                SELECT TOP 1 @OutputVectorJson = VectorData
                FROM {s_tableName}
                ORDER BY Id DESC;
                END;";

        public VectorTypeBackwardCompatibilityTests(ITestOutputHelper output)
        {
            _output = output;
            using var connection = new SqlConnection(s_connectionString);
            connection.Open();
            DataTestUtility.CreateTable(connection, s_tableName, s_tableDefinition);
            DataTestUtility.CreateTable(connection, s_bulkCopySrcTableName, s_bulkCopySrcTableDef);
            DataTestUtility.CreateSP(connection, s_storedProcName, s_storedProcBody);
        }

        public void Dispose()
        {
            using var connection = new SqlConnection(s_connectionString);
            connection.Open();
            DataTestUtility.DropTable(connection, s_tableName);
            DataTestUtility.DropTable(connection, s_bulkCopySrcTableName);
            DataTestUtility.DropStoredProcedure(connection, s_storedProcName);
        }

        private void ValidateInsertedData(SqlConnection connection, float[] expectedData)
        {
            using var selectCmd = new SqlCommand(s_selectCmdString, connection);
            using var reader = selectCmd.ExecuteReader();
            Assert.True(reader.Read(), "No data found in the table.");

            if (!reader.IsDBNull(0))
            {
                string jsonFromDb = reader.GetString(0);
                float[] deserialized = JsonSerializer.Deserialize<float[]>(jsonFromDb)!;
                Assert.Equal(expectedData, deserialized);
            }
            else
            {
                Assert.Null(expectedData);
                var val = reader.GetValue(0);
                Assert.Equal(DBNull.Value, val);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        public void TestVectorDataInsertionAsVarchar()
        {
            float[] data = { 1.1f, 2.2f, 3.3f };
            string json = JsonSerializer.Serialize(data);

            using var conn = new SqlConnection(s_connectionString);
            conn.Open();

            using var insertCmd = new SqlCommand(s_insertCmdString, conn);

            // Pattern 1: Default constructor + property setters
            var p1 = new SqlParameter();
            p1.ParameterName = s_vectorParamName;
            p1.SqlDbType = SqlDbType.VarChar;
            p1.Size = -1; //varchar(max)
            p1.Value = json;
            insertCmd.Parameters.Add(p1);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();
            ValidateInsertedData(conn, data);

            var nullp1 = new SqlParameter();
            nullp1.ParameterName = s_vectorParamName;
            nullp1.SqlDbType = SqlDbType.VarChar;
            nullp1.Size = -1; //varchar(max)
            nullp1.Value = DBNull.Value;
            insertCmd.Parameters.Add(nullp1);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();
            ValidateInsertedData(conn, null);

            // Pattern 2: Name + value constructor
            var p2 = new SqlParameter(s_vectorParamName, json);
            insertCmd.Parameters.Add(p2);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();
            ValidateInsertedData(conn, data);

            var nullp2 = new SqlParameter(s_vectorParamName, DBNull.Value);
            insertCmd.Parameters.Add(nullp2);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();
            ValidateInsertedData(conn, null);

            // Pattern 3: Name + SqlDbType constructor
            var p3 = new SqlParameter(s_vectorParamName, SqlDbType.VarChar) { Value = json };
            insertCmd.Parameters.Add(p3);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();
            ValidateInsertedData(conn, data);

            var nullp3 = new SqlParameter(s_vectorParamName, SqlDbType.VarChar) { Value = DBNull.Value };
            insertCmd.Parameters.Add(nullp3);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();
            ValidateInsertedData(conn, null);

            // Pattern 4: Name + SqlDbType + Size constructor
            var p4 = new SqlParameter(s_vectorParamName, SqlDbType.VarChar, -1) { Value = json };
            insertCmd.Parameters.Add(p4);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();
            ValidateInsertedData(conn, data);

            var nullp4 = new SqlParameter(s_vectorParamName, SqlDbType.VarChar, -1) { Value = DBNull.Value };
            insertCmd.Parameters.Add(nullp4);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();
            ValidateInsertedData(conn, null);
        }

        private async Task ValidateInsertedDataAsync(SqlConnection connection, float[] expectedData)
        {
            using var selectCmd = new SqlCommand(s_selectCmdString, connection);
            using var reader = await selectCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "No data found in the table.");

            if (!await reader.IsDBNullAsync(0))
            {
                string jsonFromDb = reader.GetString(0);
                float[] deserialized = JsonSerializer.Deserialize<float[]>(jsonFromDb)!;
                Assert.Equal(expectedData, deserialized);
            }
            else
            {
                Assert.Null(expectedData);
                var val = reader.GetValue(0);
                Assert.Equal(DBNull.Value, val);
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        public async Task TestVectorParameterInitializationAsync()
        {
            float[] data = { 1.1f, 2.2f, 3.3f };
            string json = JsonSerializer.Serialize(data);

            using var conn = new SqlConnection(s_connectionString);
            await conn.OpenAsync();

            using var insertCmd = new SqlCommand(s_insertCmdString, conn);

            // Pattern 1: Default constructor + property setters
            var p1 = new SqlParameter();
            p1.ParameterName = s_vectorParamName;
            p1.SqlDbType = SqlDbType.VarChar;
            p1.Size = -1;
            p1.Value = json;
            insertCmd.Parameters.Add(p1);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();
            await ValidateInsertedDataAsync(conn, data);

            var nullp1 = new SqlParameter();
            nullp1.ParameterName = s_vectorParamName;
            nullp1.SqlDbType = SqlDbType.VarChar;
            nullp1.Size = -1;
            nullp1.Value = DBNull.Value;
            insertCmd.Parameters.Add(nullp1);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();
            await ValidateInsertedDataAsync(conn, null);

            // Pattern 2: Name + value constructor
            var p2 = new SqlParameter(s_vectorParamName, json);
            insertCmd.Parameters.Add(p2);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();
            await ValidateInsertedDataAsync(conn, data);

            var nullp2 = new SqlParameter(s_vectorParamName, DBNull.Value);
            insertCmd.Parameters.Add(nullp2);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();
            await ValidateInsertedDataAsync(conn, null);

            // Pattern 3: Name + SqlDbType constructor
            var p3 = new SqlParameter(s_vectorParamName, SqlDbType.VarChar) { Value = json };
            insertCmd.Parameters.Add(p3);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();
            await ValidateInsertedDataAsync(conn, data);

            var nullp3 = new SqlParameter(s_vectorParamName, SqlDbType.VarChar) { Value = DBNull.Value };
            insertCmd.Parameters.Add(nullp3);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();
            await ValidateInsertedDataAsync(conn, null);

            // Pattern 4: Name + SqlDbType + Size constructor
            var p4 = new SqlParameter(s_vectorParamName, SqlDbType.VarChar, -1) { Value = json };
            insertCmd.Parameters.Add(p4);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();
            await ValidateInsertedDataAsync(conn, data);

            var nullp4 = new SqlParameter(s_vectorParamName, SqlDbType.VarChar, -1) { Value = DBNull.Value };
            insertCmd.Parameters.Add(nullp4);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();
            await ValidateInsertedDataAsync(conn, null);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        public void TestVectorDataReadsAsVarchar()
        {
            float[] data = { 1.1f, 2.2f, 3.3f };
            string dataAsJson = JsonSerializer.Serialize(data);

            using var conn = new SqlConnection(s_connectionString);
            conn.Open();

            //Insert non-null values and validate APIs for reading vector data as varchar(max)
            using var insertCmd = new SqlCommand(s_insertCmdString, conn);
            var p1 = new SqlParameter(s_vectorParamName, SqlDbType.VarChar, -1) { Value = dataAsJson };
            insertCmd.Parameters.Add(p1);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());

            //Validate Reader
            using SqlCommand verifyCommand = new SqlCommand(s_selectCmdString, conn);
            var reader = verifyCommand.ExecuteReader();
            Assert.True(reader.Read(), "No data found in the table.");

            //Read using GetString
            string result = reader.GetString(0);
            float[] dbData = JsonSerializer.Deserialize<float[]>(result)!;
            Assert.Equal(data, dbData);

            //Read using GetSqlString
            result = reader.GetSqlString(0).Value;
            dbData = JsonSerializer.Deserialize<float[]>(result)!;
            Assert.Equal(data, dbData);

            //Read using GetValue.ToString()
            result = reader.GetValue(0).ToString()!;
            dbData = JsonSerializer.Deserialize<float[]>(result)!;
            Assert.Equal(data, dbData);

            //Read using GetFieldValue<string>
            result = reader.GetFieldValue<string>(0);
            dbData = JsonSerializer.Deserialize<float[]>(result)!;
            Assert.Equal(data, dbData);

            reader.Close();

            // Validate For Null Value
            insertCmd.Parameters.Clear();
            p1.Value = DBNull.Value;
            insertCmd.Parameters.Add(p1);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());

            //Validate Reader for null value
            reader = verifyCommand.ExecuteReader();
            Assert.True(reader.Read(), "No data found in the table.");

            //Read using GetString
            Assert.Throws<SqlNullValueException>(() => reader.GetString(0));

            //Read using GetSqlString
            Assert.Throws<SqlNullValueException>(() => reader.GetString(0));

            //Read using GetValue.ToString()
            result = reader.GetValue(0).ToString();
            Assert.Equal(string.Empty, result);

            //Read using GetFieldValue<string>
            Assert.Throws<SqlNullValueException>(() => reader.GetFieldValue<string>(0));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        public async Task TestVectorDataReadsAsVarcharAsync()
        {
            float[] data = { 1.1f, 2.2f, 3.3f };
            string dataAsJson = JsonSerializer.Serialize(data);

            using var conn = new SqlConnection(s_connectionString);
            await conn.OpenAsync();

            //Insert non-null values and validate APIs for reading vector data as varchar(max)
            using var insertCmd = new SqlCommand(s_insertCmdString, conn);
            var p1 = new SqlParameter(s_vectorParamName, SqlDbType.VarChar, -1) { Value = dataAsJson };
            insertCmd.Parameters.Add(p1);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());

            //Validate Reader
            using SqlCommand verifyCommand = new SqlCommand(s_selectCmdString, conn);
            using var reader = await verifyCommand.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "No data found in the table.");

            //Read using GetString
            string result = reader.GetString(0);
            float[] dbData = JsonSerializer.Deserialize<float[]>(result)!;
            Assert.Equal(data, dbData);

            //Read using GetSqlString
            result = reader.GetSqlString(0).Value;
            dbData = JsonSerializer.Deserialize<float[]>(result)!;
            Assert.Equal(data, dbData);

            //Read using GetValue.ToString()
            result = reader.GetValue(0).ToString()!;
            dbData = JsonSerializer.Deserialize<float[]>(result)!;
            Assert.Equal(data, dbData);

            //Read using GetFieldValue<string>
            result = await reader.GetFieldValueAsync<string>(0);
            dbData = JsonSerializer.Deserialize<float[]>(result)!;
            Assert.Equal(data, dbData);

            reader.Close();

            // Validate For Null Value
            insertCmd.Parameters.Clear();
            p1.Value = DBNull.Value;
            insertCmd.Parameters.Add(p1);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());

            //Validate Reader for null value
            var reader2 = await verifyCommand.ExecuteReaderAsync();
            Assert.True(await reader2.ReadAsync(), "No data found in the table.");

            //Read using GetString
            Assert.Throws<SqlNullValueException>(() => reader2.GetString(0));

            //Read using GetSqlString
            Assert.Throws<SqlNullValueException>(() => reader2.GetString(0));

            //Read using GetValue.ToString()
            result = reader2.GetValue(0).ToString();
            Assert.Equal(string.Empty, result);

            //Read using GetFieldValueAsync<string>
            await Assert.ThrowsAsync<SqlNullValueException>(async () => await reader2.GetFieldValueAsync<string>(0));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        public void TestStoredProcParamsForVectorAsVarchar()
        {
            // Test data
            float[] data = { 7.1f, 8.2f, 9.3f };
            string dataAsJson = JsonSerializer.Serialize(data);

            //Create SP for test
            using var conn = new SqlConnection(s_connectionString);
            conn.Open();
            DataTestUtility.CreateSP(conn, s_storedProcName, s_storedProcBody);
            using var command = new SqlCommand(s_storedProcName, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Set input and output parameters
            var inputParam = new SqlParameter("@InputVectorJson", SqlDbType.VarChar, -1);
            inputParam.Value = dataAsJson;
            command.Parameters.Add(inputParam);
            var outputParam = new SqlParameter("@OutputVectorJson", SqlDbType.VarChar, -1)
            {
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outputParam);

            // Execute the stored procedure
            command.ExecuteNonQuery();

            // Validate the output parameter
            var dbDataAsJson = outputParam.Value as string;
            float[] dbData = JsonSerializer.Deserialize<float[]>(dbDataAsJson)!;
            Assert.NotNull(dbDataAsJson);
            Assert.Equal(data, dbData);

            // Test with null value
            command.Parameters.Clear();
            inputParam.Value = DBNull.Value;
            command.Parameters.Add(inputParam);
            command.Parameters.Add(outputParam);
            command.ExecuteNonQuery();

            // Validate output paramter for null value
            Assert.True(outputParam.Value == DBNull.Value);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        public async Task TestStoredProcParamsForVectorAsVarcharAsync()
        {
            // Test data
            float[] data = { 7.1f, 8.2f, 9.3f };
            string dataAsJson = JsonSerializer.Serialize(data);

            // Create SP for test
            using var conn = new SqlConnection(s_connectionString);
            await conn.OpenAsync();
            DataTestUtility.CreateSP(conn, s_storedProcName, s_storedProcBody);

            using var command = new SqlCommand(s_storedProcName, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Set input and output parameters
            var inputParam = new SqlParameter("@InputVectorJson", SqlDbType.VarChar, -1)
            {
                Value = dataAsJson
            };
            command.Parameters.Add(inputParam);

            var outputParam = new SqlParameter("@OutputVectorJson", SqlDbType.VarChar, -1)
            {
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outputParam);

            // Execute the stored procedure
            await command.ExecuteNonQueryAsync();

            // Validate the output parameter
            var dbDataAsJson = outputParam.Value as string;
            float[] dbData = JsonSerializer.Deserialize<float[]>(dbDataAsJson)!;
            Assert.NotNull(dbDataAsJson);
            Assert.Equal(data, dbData);

            // Test with null value
            command.Parameters.Clear();
            inputParam.Value = DBNull.Value;
            command.Parameters.Add(inputParam);
            command.Parameters.Add(outputParam);

            await command.ExecuteNonQueryAsync();

            // Validate output parameter for null value
            Assert.True(outputParam.Value == DBNull.Value);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        public void TestSqlBulkCopyForVectorAsVarchar()
        {
            //Setup source with test data and create destination table for bulkcopy.
            SqlConnection sourceConnection = new SqlConnection(s_connectionString);
            sourceConnection.Open();
            SqlConnection destinationConnection = new SqlConnection(s_connectionString);
            destinationConnection.Open();
            float[] testData = { 1.1f, 2.2f, 3.3f };
            string testDataAsJson = JsonSerializer.Serialize(testData);
            using var insertCmd = new SqlCommand($"insert into {s_bulkCopySrcTableName} values (@VectorData)", sourceConnection);
            var varcharVectorParam = new SqlParameter(s_vectorParamName, SqlDbType.VarChar, -1) { Value = testDataAsJson };
            insertCmd.Parameters.Add(varcharVectorParam);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();
            varcharVectorParam.Value = DBNull.Value;
            insertCmd.Parameters.Add(varcharVectorParam);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());

            //Bulkcopy from sql server table to destination table
            using SqlCommand sourceDataCommand = new SqlCommand($"SELECT Id, VectorData FROM {s_bulkCopySrcTableName}", sourceConnection);
            using SqlDataReader reader = sourceDataCommand.ExecuteReader();

            // Verify that the destination table is empty before bulk copy
            using SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {s_tableName}", destinationConnection);
            Assert.Equal(0, Convert.ToInt16(countCommand.ExecuteScalar()));

            // Initialize bulk copy configuration
            using SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection)
            {
                DestinationTableName = s_tableName,
            };

            try
            {
                // Perform bulk copy from source to destination table
                bulkCopy.WriteToServer(reader);
            }
            catch (Exception ex)
            {
                // If bulk copy fails, fail the test with the exception message
                Assert.Fail($"Bulk copy failed: {ex.Message}");
            }

            // Verify that the 2 rows from the source table have been copied into the destination table.
            Assert.Equal(2, Convert.ToInt16(countCommand.ExecuteScalar()));

            // Read the data from destination table as varbinary to verify the UTF-8 byte sequence
            using SqlCommand verifyCommand = new SqlCommand($"SELECT VectorData from {s_tableName}", destinationConnection);
            using SqlDataReader verifyReader = verifyCommand.ExecuteReader();

            // Verify that we have data in the destination table
            Assert.True(verifyReader.Read(), "No data found in destination table after bulk copy.");

            // Validate first non-null value.
            Assert.True(!verifyReader.IsDBNull(0), "First row in the table is null.");
            Assert.Equal(testData, JsonSerializer.Deserialize<float[]>(verifyReader.GetString(0)));

            // Verify that we have another row
            Assert.True(verifyReader.Read(), "Second row not found in the table");

            // Verify that we have encountered null.
            Assert.True(verifyReader.IsDBNull(0));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        public async Task TestSqlBulkCopyForVectorAsVarcharAsync()
        {
            //Setup source with test data and create destination table for bulkcopy.
            SqlConnection sourceConnection = new SqlConnection(s_connectionString);
            await sourceConnection.OpenAsync();
            SqlConnection destinationConnection = new SqlConnection(s_connectionString);
            await destinationConnection.OpenAsync();
            float[] testData = { 1.1f, 2.2f, 3.3f };
            string testDataAsJson = JsonSerializer.Serialize(testData);
            using var insertCmd = new SqlCommand($"insert into {s_bulkCopySrcTableName} values (@VectorData)", sourceConnection);
            var varcharVectorParam = new SqlParameter(s_vectorParamName, SqlDbType.VarChar, -1) { Value = testDataAsJson };
            insertCmd.Parameters.Add(varcharVectorParam);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();
            varcharVectorParam.Value = DBNull.Value;
            insertCmd.Parameters.Add(varcharVectorParam);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());

            //Bulkcopy from sql server table to destination table
            using SqlCommand sourceDataCommand = new SqlCommand($"SELECT Id, VectorData FROM {s_bulkCopySrcTableName}", sourceConnection);
            using SqlDataReader reader = await sourceDataCommand.ExecuteReaderAsync();

            // Verify that the destination table is empty before bulk copy
            using SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {s_tableName}", destinationConnection);
            Assert.Equal(0, Convert.ToInt16(await countCommand.ExecuteScalarAsync()));

            // Initialize bulk copy configuration
            using SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection)
            {
                DestinationTableName = s_tableName,
            };

            try
            {
                // Perform bulk copy from source to destination table
                await bulkCopy.WriteToServerAsync(reader);
            }
            catch (Exception ex)
            {
                // If bulk copy fails, fail the test with the exception message
                Assert.Fail($"Bulk copy failed: {ex.Message}");
            }

            // Verify that the 2 rows from the source table have been copied into the destination table.
            Assert.Equal(2, Convert.ToInt16(await countCommand.ExecuteScalarAsync()));

            // Read the data from destination table as varbinary to verify the UTF-8 byte sequence
            using SqlCommand verifyCommand = new SqlCommand($"SELECT VectorData from {s_tableName}", destinationConnection);
            using SqlDataReader verifyReader = await verifyCommand.ExecuteReaderAsync();

            // Verify that we have data in the destination table
            Assert.True(await verifyReader.ReadAsync(), "No data found in destination table after bulk copy.");

            // Validate first non-null value.
            Assert.True(!verifyReader.IsDBNull(0), "First row in the table is null.");
            Assert.Equal(testData, JsonSerializer.Deserialize<float[]>(verifyReader.GetString(0)));

            // Verify that we have another row
            Assert.True(await verifyReader.ReadAsync(), "Second row not found in the table");

            // Verify that we have encountered null.
            Assert.True(await verifyReader.IsDBNullAsync(0));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        public void TestInsertVectorsAsVarcharWithPrepare()
        {
            SqlConnection conn = new SqlConnection(s_connectionString);
            conn.Open();
            SqlCommand command = new SqlCommand(s_insertCmdString, conn);
            SqlParameter vectorParam = new SqlParameter("@VectorData", SqlDbType.VarChar, -1);
            command.Parameters.Add(vectorParam);
            command.Prepare();
            for (int i = 0; i < 10; i++)
            {
                vectorParam.Value = JsonSerializer.Serialize<float[]>(new float[] { i + 0.1f, i + 0.2f, i + 0.3f });
                command.ExecuteNonQuery();
            }
            SqlCommand validateCommand = new SqlCommand($"SELECT VectorData FROM {s_tableName}", conn);
            using SqlDataReader reader = validateCommand.ExecuteReader();
            int rowcnt = 0;
            while (reader.Read())
            {
                float[] expectedData = new float[] { rowcnt + 0.1f, rowcnt + 0.2f, rowcnt + 0.3f };
                float[] dbData = JsonSerializer.Deserialize<float[]>(reader.GetString(0))!;
                Assert.Equal(expectedData, dbData);
                rowcnt++;
            }
            Assert.Equal(10, rowcnt);
        }
    }
}

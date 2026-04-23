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
    /// <summary>
    /// Provides parameterized test data for backward compatibility tests that exchange
    /// vector data as varchar(max) JSON strings.
    /// </summary>
    public static class VarcharVectorTestData
    {
        public static readonly float[] TestData = { 1.1f, 2.2f, 3.3f };

        /// <summary>
        /// Generates test cases for all 4 SqlParameter construction patterns x 2 value types (non-null + null).
        /// Each case yields: [int pattern, string jsonOrNull, float[] expectedData]
        /// where jsonOrNull is null when testing DBNull insertion.
        /// </summary>
        public static IEnumerable<object[]> GetVarcharVectorInsertTestData()
        {
            string json = JsonSerializer.Serialize(TestData);

            // Pattern 1-4 with non-null JSON value
            yield return new object[] { 1, json, TestData };
            yield return new object[] { 2, json, TestData };
            yield return new object[] { 3, json, TestData };
            yield return new object[] { 4, json, TestData };

            // Pattern 1-4 with null value
            yield return new object[] { 1, null, null };
            yield return new object[] { 2, null, null };
            yield return new object[] { 3, null, null };
            yield return new object[] { 4, null, null };
        }
    }

    public sealed class VectorTypeBackwardCompatibilityTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private static readonly string s_connectionString = DataTestUtility.TCPConnectionString;
        private static readonly string s_tableName = DataTestUtility.GetShortName("VectorTestTable");
        private static readonly string s_bulkCopySrcTableName = DataTestUtility.GetShortName("VectorBulkCopyTestTable");
        private static readonly string s_bulkCopySrcTableDef = $@"(Id INT PRIMARY KEY IDENTITY, VectorData varchar(max) NULL)";
        private static readonly string s_tableDefinition = $@"(Id INT PRIMARY KEY IDENTITY, VectorData vector(3) NULL)";
        private static readonly string s_selectCmdString = $"SELECT VectorData FROM {s_tableName} ORDER BY Id DESC";
        private static readonly string s_insertCmdString = $"INSERT INTO {s_tableName} (VectorData) VALUES (@VectorData)";
        private static readonly string s_vectorParamName = $"@VectorData";
        private static readonly string s_storedProcName = DataTestUtility.GetShortName("VectorsAsVarcharSp");
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

        /// <summary>
        /// Creates a SqlParameter with varchar(max) type based on the specified pattern.
        /// Mirrors the 4 parameter construction patterns tested in NativeVectorFloat32Tests
        /// but uses SqlDbType.VarChar instead of SqlDbTypeExtensions.Vector.
        /// </summary>
        private static SqlParameter CreateVarcharParameter(int pattern, string jsonValue)
        {
            object value = jsonValue != null ? (object)jsonValue : DBNull.Value;

            return pattern switch
            {
                // Pattern 1: Default constructor + property setters
                1 => new SqlParameter
                {
                    ParameterName = s_vectorParamName,
                    SqlDbType = SqlDbType.VarChar,
                    Size = -1, // varchar(max)
                    Value = value
                },
                // Pattern 2: Name + value constructor
                2 => new SqlParameter(s_vectorParamName, value),
                // Pattern 3: Name + SqlDbType constructor
                3 => new SqlParameter(s_vectorParamName, SqlDbType.VarChar) { Value = value },
                // Pattern 4: Name + SqlDbType + Size constructor
                4 => new SqlParameter(s_vectorParamName, SqlDbType.VarChar, -1) { Value = value },
                _ => throw new ArgumentOutOfRangeException(nameof(pattern), $"Unsupported pattern: {pattern}")
            };
        }

        private void ValidateInsertedData(SqlConnection connection, float[] expectedData)
        {
            using var selectCmd = new SqlCommand(s_selectCmdString, connection);
            using var reader = selectCmd.ExecuteReader();
            Assert.True(reader.Read(), "No data found in the table.");

            if (expectedData != null)
            {
                // Validate non-null data through all string read APIs
                string result = reader.GetString(0);
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(result));

                result = reader.GetSqlString(0).Value;
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(result));

                result = reader.GetFieldValue<string>(0);
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(result));
            }
            else
            {
                Assert.True(reader.IsDBNull(0), "Expected null but got non-null value.");
                Assert.Equal(DBNull.Value, reader.GetValue(0));
                Assert.Throws<SqlNullValueException>(() => reader.GetString(0));
                Assert.Throws<SqlNullValueException>(() => reader.GetSqlString(0).Value);
                Assert.Throws<SqlNullValueException>(() => reader.GetFieldValue<string>(0));
            }
        }

        private async Task ValidateInsertedDataAsync(SqlConnection connection, float[] expectedData)
        {
            using var selectCmd = new SqlCommand(s_selectCmdString, connection);
            using var reader = await selectCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "No data found in the table.");

            if (expectedData != null)
            {
                // Validate non-null data through all string read APIs
                string result = reader.GetString(0);
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(result));

                result = reader.GetSqlString(0).Value;
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(result));

                result = await reader.GetFieldValueAsync<string>(0);
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(result));
            }
            else
            {
                Assert.True(await reader.IsDBNullAsync(0), "Expected null but got non-null value.");
                Assert.Equal(DBNull.Value, reader.GetValue(0));
                Assert.Throws<SqlNullValueException>(() => reader.GetString(0));
                Assert.Throws<SqlNullValueException>(() => reader.GetSqlString(0).Value);
                await Assert.ThrowsAsync<SqlNullValueException>(async () => await reader.GetFieldValueAsync<string>(0));
            }
        }

        #region Insert Tests

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        [MemberData(nameof(VarcharVectorTestData.GetVarcharVectorInsertTestData), MemberType = typeof(VarcharVectorTestData), DisableDiscoveryEnumeration = true)]
        public void TestVectorDataInsertionAsVarchar(int pattern, string jsonValue, float[] expectedData)
        {
            using var conn = new SqlConnection(s_connectionString);
            conn.Open();

            using var insertCmd = new SqlCommand(s_insertCmdString, conn);
            SqlParameter param = CreateVarcharParameter(pattern, jsonValue);
            insertCmd.Parameters.Add(param);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();

            ValidateInsertedData(conn, expectedData);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        [MemberData(nameof(VarcharVectorTestData.GetVarcharVectorInsertTestData), MemberType = typeof(VarcharVectorTestData), DisableDiscoveryEnumeration = true)]
        public async Task TestVectorDataInsertionAsVarcharAsync(int pattern, string jsonValue, float[] expectedData)
        {
            using var conn = new SqlConnection(s_connectionString);
            await conn.OpenAsync();

            using var insertCmd = new SqlCommand(s_insertCmdString, conn);
            SqlParameter param = CreateVarcharParameter(pattern, jsonValue);
            insertCmd.Parameters.Add(param);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();

            await ValidateInsertedDataAsync(conn, expectedData);
        }

        #endregion

        #region Stored Procedure Tests

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        public void TestStoredProcParamsForVectorAsVarchar()
        {
            float[] data = VarcharVectorTestData.TestData;
            string dataAsJson = JsonSerializer.Serialize(data);

            using var conn = new SqlConnection(s_connectionString);
            conn.Open();

            using var command = new SqlCommand(s_storedProcName, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Test with non-null value
            var inputParam = new SqlParameter("@InputVectorJson", SqlDbType.VarChar, -1) { Value = dataAsJson };
            command.Parameters.Add(inputParam);
            var outputParam = new SqlParameter("@OutputVectorJson", SqlDbType.VarChar, -1)
            {
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outputParam);

            command.ExecuteNonQuery();

            var dbDataAsJson = outputParam.Value as string;
            Assert.NotNull(dbDataAsJson);
            Assert.Equal(data, JsonSerializer.Deserialize<float[]>(dbDataAsJson));

            // Test with null value
            command.Parameters.Clear();
            inputParam.Value = DBNull.Value;
            command.Parameters.Add(inputParam);
            command.Parameters.Add(outputParam);
            command.ExecuteNonQuery();

            Assert.Equal(DBNull.Value, outputParam.Value);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        public async Task TestStoredProcParamsForVectorAsVarcharAsync()
        {
            float[] data = VarcharVectorTestData.TestData;
            string dataAsJson = JsonSerializer.Serialize(data);

            using var conn = new SqlConnection(s_connectionString);
            await conn.OpenAsync();

            using var command = new SqlCommand(s_storedProcName, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Test with non-null value
            var inputParam = new SqlParameter("@InputVectorJson", SqlDbType.VarChar, -1) { Value = dataAsJson };
            command.Parameters.Add(inputParam);
            var outputParam = new SqlParameter("@OutputVectorJson", SqlDbType.VarChar, -1)
            {
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outputParam);

            await command.ExecuteNonQueryAsync();

            var dbDataAsJson = outputParam.Value as string;
            Assert.NotNull(dbDataAsJson);
            Assert.Equal(data, JsonSerializer.Deserialize<float[]>(dbDataAsJson));

            // Test with null value
            command.Parameters.Clear();
            inputParam.Value = DBNull.Value;
            command.Parameters.Add(inputParam);
            command.Parameters.Add(outputParam);

            await command.ExecuteNonQueryAsync();

            Assert.Equal(DBNull.Value, outputParam.Value);
        }

        #endregion

        #region Bulk Copy Tests

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        [InlineData(1)]
        [InlineData(2)]
        public void TestSqlBulkCopyForVectorAsVarchar(int bulkCopySourceMode)
        {
            float[] testData = VarcharVectorTestData.TestData;
            string testDataAsJson = JsonSerializer.Serialize(testData);

            using var sourceConnection = new SqlConnection(s_connectionString);
            sourceConnection.Open();
            using var destinationConnection = new SqlConnection(s_connectionString);
            destinationConnection.Open();

            DataTable table = null;
            switch (bulkCopySourceMode)
            {
                case 1:
                    // Use SQL Server table as source with varchar(max) data
                    using (var insertCmd = new SqlCommand($"insert into {s_bulkCopySrcTableName} values (@VectorData)", sourceConnection))
                    {
                        var varcharVectorParam = new SqlParameter(s_vectorParamName, SqlDbType.VarChar, -1) { Value = testDataAsJson };
                        insertCmd.Parameters.Add(varcharVectorParam);
                        Assert.Equal(1, insertCmd.ExecuteNonQuery());
                        insertCmd.Parameters.Clear();
                        varcharVectorParam.Value = DBNull.Value;
                        insertCmd.Parameters.Add(varcharVectorParam);
                        Assert.Equal(1, insertCmd.ExecuteNonQuery());
                    }
                    break;
                case 2:
                    // Use DataTable as source
                    table = new DataTable(s_bulkCopySrcTableName);
                    table.Columns.Add("Id", typeof(int));
                    table.Columns.Add("VectorData", typeof(string));
                    table.Rows.Add(1, testDataAsJson);
                    table.Rows.Add(2, DBNull.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bulkCopySourceMode), $"Unsupported bulk copy source mode: {bulkCopySourceMode}");
            }

            // Verify that the destination table is empty before bulk copy
            using SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {s_tableName}", destinationConnection);
            Assert.Equal(0, Convert.ToInt16(countCommand.ExecuteScalar()));

            // Initialize bulk copy configuration
            using SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection)
            {
                DestinationTableName = s_tableName,
            };

            switch (bulkCopySourceMode)
            {
                case 1:
                    using (SqlCommand sourceDataCommand = new SqlCommand($"SELECT Id, VectorData FROM {s_bulkCopySrcTableName}", sourceConnection))
                    using (SqlDataReader reader = sourceDataCommand.ExecuteReader())
                    {
                        bulkCopy.WriteToServer(reader);
                    }
                    break;
                case 2:
                    bulkCopy.WriteToServer(table);
                    break;
            }

            // Verify that 2 rows were copied
            Assert.Equal(2, Convert.ToInt16(countCommand.ExecuteScalar()));

            // Read data from destination table and verify
            using SqlCommand verifyCommand = new SqlCommand($"SELECT VectorData from {s_tableName}", destinationConnection);
            using SqlDataReader verifyReader = verifyCommand.ExecuteReader();

            Assert.True(verifyReader.Read(), "No data found in destination table after bulk copy.");
            Assert.False(verifyReader.IsDBNull(0), "First row in the table is null.");
            Assert.Equal(testData, JsonSerializer.Deserialize<float[]>(verifyReader.GetString(0)));

            Assert.True(verifyReader.Read(), "Second row not found in the table");
            Assert.True(verifyReader.IsDBNull(0));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
        [InlineData(1)]
        [InlineData(2)]
        public async Task TestSqlBulkCopyForVectorAsVarcharAsync(int bulkCopySourceMode)
        {
            float[] testData = VarcharVectorTestData.TestData;
            string testDataAsJson = JsonSerializer.Serialize(testData);

            using var sourceConnection = new SqlConnection(s_connectionString);
            await sourceConnection.OpenAsync();
            using var destinationConnection = new SqlConnection(s_connectionString);
            await destinationConnection.OpenAsync();

            DataTable table = null;
            switch (bulkCopySourceMode)
            {
                case 1:
                    // Use SQL Server table as source with varchar(max) data
                    using (var insertCmd = new SqlCommand($"insert into {s_bulkCopySrcTableName} values (@VectorData)", sourceConnection))
                    {
                        var varcharVectorParam = new SqlParameter(s_vectorParamName, SqlDbType.VarChar, -1) { Value = testDataAsJson };
                        insertCmd.Parameters.Add(varcharVectorParam);
                        Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
                        insertCmd.Parameters.Clear();
                        varcharVectorParam.Value = DBNull.Value;
                        insertCmd.Parameters.Add(varcharVectorParam);
                        Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
                    }
                    break;
                case 2:
                    // Use DataTable as source
                    table = new DataTable(s_bulkCopySrcTableName);
                    table.Columns.Add("Id", typeof(int));
                    table.Columns.Add("VectorData", typeof(string));
                    table.Rows.Add(1, testDataAsJson);
                    table.Rows.Add(2, DBNull.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bulkCopySourceMode), $"Unsupported bulk copy source mode: {bulkCopySourceMode}");
            }

            // Verify that the destination table is empty before bulk copy
            using SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {s_tableName}", destinationConnection);
            Assert.Equal(0, Convert.ToInt16(await countCommand.ExecuteScalarAsync()));

            // Initialize bulk copy configuration
            using SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection)
            {
                DestinationTableName = s_tableName,
            };

            switch (bulkCopySourceMode)
            {
                case 1:
                    using (SqlCommand sourceDataCommand = new SqlCommand($"SELECT Id, VectorData FROM {s_bulkCopySrcTableName}", sourceConnection))
                    using (SqlDataReader reader = await sourceDataCommand.ExecuteReaderAsync())
                    {
                        await bulkCopy.WriteToServerAsync(reader);
                    }
                    break;
                case 2:
                    await bulkCopy.WriteToServerAsync(table);
                    break;
            }

            // Verify that 2 rows were copied
            Assert.Equal(2, Convert.ToInt16(await countCommand.ExecuteScalarAsync()));

            // Read data from destination table and verify
            using SqlCommand verifyCommand = new SqlCommand($"SELECT VectorData from {s_tableName}", destinationConnection);
            using SqlDataReader verifyReader = await verifyCommand.ExecuteReaderAsync();

            Assert.True(await verifyReader.ReadAsync(), "No data found in destination table after bulk copy.");
            Assert.False(verifyReader.IsDBNull(0), "First row in the table is null.");
            Assert.Equal(testData, JsonSerializer.Deserialize<float[]>(verifyReader.GetString(0)));

            Assert.True(await verifyReader.ReadAsync(), "Second row not found in the table");
            Assert.True(await verifyReader.IsDBNullAsync(0));
        }

        #endregion

        #region Prepared Statement Tests

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSqlVectorSupported))]
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

        #endregion
    }
}

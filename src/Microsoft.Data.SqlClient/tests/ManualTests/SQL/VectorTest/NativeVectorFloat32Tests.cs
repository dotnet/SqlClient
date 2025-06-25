using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlTypes;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest
{
    public static class VectorFloat32TestData
    {
        public const int VectorHeaderSize = 8;
        public static float[] testData = new float[] { 1.1f, 2.2f, 3.3f };
        public static int sizeInbytes = VectorHeaderSize + testData.Length * sizeof(float);
        public static int vectorColumnLength = testData.Length;
        public static IEnumerable<object[]> GetVectorFloat32TestData()
        {
            // Pattern 1-4 with SqlVectorFloat32(values: testData)
            yield return new object[] { 1, new SqlVectorFloat32(testData), testData, sizeInbytes, vectorColumnLength };
            yield return new object[] { 2, new SqlVectorFloat32(testData), testData, sizeInbytes, vectorColumnLength };
            yield return new object[] { 3, new SqlVectorFloat32(testData), testData, sizeInbytes, vectorColumnLength };
            yield return new object[] { 4, new SqlVectorFloat32(testData), testData, sizeInbytes, vectorColumnLength };

            // Pattern 1–4 with SqlVectorFloat32(n)  
            yield return new object[] { 1, new SqlVectorFloat32(vectorColumnLength), Array.Empty<float>(), sizeInbytes, vectorColumnLength };
            yield return new object[] { 2, new SqlVectorFloat32(vectorColumnLength), Array.Empty<float>(), sizeInbytes, vectorColumnLength };
            yield return new object[] { 3, new SqlVectorFloat32(vectorColumnLength), Array.Empty<float>(), sizeInbytes, vectorColumnLength };
            yield return new object[] { 4, new SqlVectorFloat32(vectorColumnLength), Array.Empty<float>(), sizeInbytes, vectorColumnLength };

            // Pattern 1–4 with DBNull  
            yield return new object[] { 1, DBNull.Value, Array.Empty<float>(), sizeInbytes, vectorColumnLength };
            yield return new object[] { 2, DBNull.Value, Array.Empty<float>(), sizeInbytes, vectorColumnLength };
            yield return new object[] { 3, DBNull.Value, Array.Empty<float>(), sizeInbytes, vectorColumnLength };
            yield return new object[] { 4, DBNull.Value, Array.Empty<float>(), sizeInbytes, vectorColumnLength };

            // Pattern 1–4 with SqlVectorFloat32.Null  
            yield return new object[] { 1, SqlVectorFloat32.Null, Array.Empty<float>(), sizeInbytes, vectorColumnLength };
            
            // Following scenario is not supported in SqlClient.
            // This can only be fixed with a behavior change that SqlParameter.Value is internally set to DBNull.Value if it is set to null.
            //yield return new object[] { 2, SqlVectorFloat32.Null, Array.Empty<float>(), sizeInbytes, vectorColumnLength };
            
            yield return new object[] { 3, SqlVectorFloat32.Null, Array.Empty<float>(), sizeInbytes, vectorColumnLength };
            yield return new object[] { 4, SqlVectorFloat32.Null, Array.Empty<float>(), sizeInbytes, vectorColumnLength };
        }
    }

    public sealed class NativeVectorFloat32Tests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private static readonly string s_connectionString = ManualTesting.Tests.DataTestUtility.TCPConnectionString;
        private static readonly string s_tableName = DataTestUtility.GetUniqueName("VectorTestTable");
        private static readonly string s_bulkCopySrcTableName = DataTestUtility.GetUniqueName("VectorBulkCopyTestTable");
        private static readonly string s_bulkCopySrcTableDef = $@"(Id INT PRIMARY KEY IDENTITY, VectorData vector(3) NULL)";
        private static readonly string s_tableDefinition = $@"(Id INT PRIMARY KEY IDENTITY, VectorData vector(3) NULL)";
        private static readonly string s_selectCmdString = $"SELECT VectorData FROM {s_tableName} ORDER BY Id DESC";
        private static readonly string s_insertCmdString = $"INSERT INTO {s_tableName} (VectorData) VALUES (@VectorData)";
        private static readonly string s_vectorParamName = $"@VectorData";
        private static readonly string s_outputVectorParamName = $"@OutputVectorData";
        private static readonly string s_storedProcName = DataTestUtility.GetUniqueName("VectorsAsVarcharSp");
        private static readonly string s_storedProcBody = $@"
                {s_vectorParamName} vector(3),   -- Input: Serialized float[] as JSON string
                {s_outputVectorParamName} vector(3) OUTPUT  -- Output: Echoed back from latest inserted row
                AS
                BEGIN
                SET NOCOUNT ON;

                -- Insert into vector table
                INSERT INTO {s_tableName} (VectorData)
                VALUES ({s_vectorParamName});

                -- Retrieve latest entry (assumes auto-incrementing ID)
                SELECT TOP 1 {s_outputVectorParamName} = VectorData
                FROM {s_tableName}
                ORDER BY Id DESC;
                END;";

        public NativeVectorFloat32Tests(ITestOutputHelper output)
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

        private void ValidateSqlVectorFloat32Object(bool isNull, SqlVectorFloat32 sqlVectorFloat32, float[] expectedData, int expectedSize, int expectedLength)
        {
            Assert.Equal(expectedData, sqlVectorFloat32.Values.ToArray());
            Assert.Equal(expectedSize, sqlVectorFloat32.Size);
            Assert.Equal(expectedLength, sqlVectorFloat32.Length);
            if (!isNull)
            {
                Assert.False(sqlVectorFloat32.IsNull, "IsNull set to true for a non-null value");
            }
            else
            {
                Assert.True(sqlVectorFloat32.IsNull, "IsNull set to false for a null value");
            }
        }

        private void ValidateInsertedData(SqlConnection connection, float[] expectedData, int expectedSize, int expectedLength)
        {
            using var selectCmd = new SqlCommand(s_selectCmdString, connection);
            using var reader = selectCmd.ExecuteReader();
            Assert.True(reader.Read(), "No data found in the table.");

            //For both null and non-null cases, validate the SqlVectorFloat32 object
            ValidateSqlVectorFloat32Object(reader.IsDBNull(0), (SqlVectorFloat32)reader.GetSqlVectorFloat32(0), expectedData, expectedSize, expectedLength);
            ValidateSqlVectorFloat32Object(reader.IsDBNull(0), reader.GetFieldValue<SqlVectorFloat32>(0), expectedData, expectedSize, expectedLength);
            ValidateSqlVectorFloat32Object(reader.IsDBNull(0), (SqlVectorFloat32)reader.GetSqlValue(0), expectedData, expectedSize, expectedLength);

            if (!reader.IsDBNull(0))
            {                
                ValidateSqlVectorFloat32Object(reader.IsDBNull(0), (SqlVectorFloat32)reader.GetValue(0), expectedData, expectedSize, expectedLength);
                ValidateSqlVectorFloat32Object(reader.IsDBNull(0), (SqlVectorFloat32)reader[0], expectedData, expectedSize, expectedLength);
                ValidateSqlVectorFloat32Object(reader.IsDBNull(0), (SqlVectorFloat32)reader["VectorData"], expectedData, expectedSize, expectedLength);
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(reader.GetString(0)));
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(reader.GetSqlString(0).Value));
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(reader.GetFieldValue<string>(0)));
            }
            else
            {
                Assert.Equal(DBNull.Value, reader.GetValue(0));
                Assert.Equal(DBNull.Value, reader[0]);
                Assert.Equal(DBNull.Value, reader["VectorData"]);
                Assert.Throws<SqlNullValueException>(() => reader.GetString(0));
                Assert.Throws<SqlNullValueException>(() => reader.GetSqlString(0).Value);
                Assert.Throws<SqlNullValueException>(() => reader.GetFieldValue<string>(0));
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        [MemberData(nameof(VectorFloat32TestData.GetVectorFloat32TestData), MemberType = typeof(VectorFloat32TestData))]
        public void TestSqlVectorFloat32ParameterInsertionAndReads(
        int pattern,
        object value,
        float[] expectedValues,
        int expectedSize,
        int expectedLength)
        {
            using var conn = new SqlConnection(s_connectionString);
            conn.Open();

            using var insertCmd = new SqlCommand(s_insertCmdString, conn);

            SqlParameter param = pattern switch
            {
                1 => new SqlParameter
                {
                    ParameterName = s_vectorParamName,
                    SqlDbType = SqlDbTypeExtensions.Vector,
                    Value = value
                },
                2 => new SqlParameter(s_vectorParamName, value),
                3 => new SqlParameter(s_vectorParamName, SqlDbTypeExtensions.Vector) { Value = value },
                4 => new SqlParameter(s_vectorParamName, SqlDbTypeExtensions.Vector, new SqlVectorFloat32(3).Size) { Value = value },
                _ => throw new ArgumentOutOfRangeException(nameof(pattern), $"Unsupported pattern: {pattern}")
            };

            insertCmd.Parameters.Add(param);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();

            ValidateInsertedData(conn, expectedValues, expectedSize, expectedLength);
        }

        private async Task ValidateInsertedDataAsync(SqlConnection connection, float[] expectedData, int expectedSize, int expectedLength)
        {
            using var selectCmd = new SqlCommand(s_selectCmdString, connection);
            using var reader = await selectCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "No data found in the table.");

            //For both null and non-null cases, validate the SqlVectorFloat32 object
            ValidateSqlVectorFloat32Object(await reader.IsDBNullAsync(0), (SqlVectorFloat32)reader.GetSqlVectorFloat32(0), expectedData, expectedSize, expectedLength);
            ValidateSqlVectorFloat32Object(await reader.IsDBNullAsync(0), await reader.GetFieldValueAsync<SqlVectorFloat32>(0), expectedData, expectedSize, expectedLength);
            ValidateSqlVectorFloat32Object(await reader.IsDBNullAsync(0), (SqlVectorFloat32)reader.GetSqlValue(0), expectedData, expectedSize, expectedLength);

            if (!await reader.IsDBNullAsync(0))
            {
                ValidateSqlVectorFloat32Object(await reader.IsDBNullAsync(0), (SqlVectorFloat32)reader.GetValue(0), expectedData, expectedSize, expectedLength);
                ValidateSqlVectorFloat32Object(await reader.IsDBNullAsync(0), (SqlVectorFloat32)reader[0], expectedData, expectedSize, expectedLength);
                ValidateSqlVectorFloat32Object(await reader.IsDBNullAsync(0), (SqlVectorFloat32)reader["VectorData"], expectedData, expectedSize, expectedLength);
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(reader.GetString(0)));
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(reader.GetSqlString(0).Value));
                Assert.Equal(expectedData, JsonSerializer.Deserialize<float[]>(await reader.GetFieldValueAsync<string>(0)));
            }
            else
            {
                Assert.Equal(DBNull.Value, reader.GetValue(0));
                Assert.Equal(DBNull.Value, reader[0]);
                Assert.Equal(DBNull.Value, reader["VectorData"]);
                Assert.Throws<SqlNullValueException>(() => reader.GetString(0));
                Assert.Throws<SqlNullValueException>(() => reader.GetSqlString(0).Value);
                await Assert.ThrowsAsync<SqlNullValueException>(async () => await reader.GetFieldValueAsync<string>(0));
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        [MemberData(nameof(VectorFloat32TestData.GetVectorFloat32TestData), MemberType = typeof(VectorFloat32TestData))]
        public async Task TestSqlVectorFloat32ParameterInsertionAndReadsAsync(
        int pattern,
        object value,
        float[] expectedValues,
        int expectedSize,
        int expectedLength)
        {
            using var conn = new SqlConnection(s_connectionString);
            await conn.OpenAsync();

            using var insertCmd = new SqlCommand(s_insertCmdString, conn);

            SqlParameter param = pattern switch
            {
                1 => new SqlParameter
                {
                    ParameterName = s_vectorParamName,
                    SqlDbType = (SqlDbType)36, // SqlDbTypeExtension.Vector
                    Value = value
                },
                2 => new SqlParameter(s_vectorParamName, value),
                3 => new SqlParameter(s_vectorParamName, SqlDbTypeExtensions.Vector) { Value = value },
                4 => new SqlParameter(s_vectorParamName, SqlDbTypeExtensions.Vector, new SqlVectorFloat32(3).Size) { Value = value },
                _ => throw new ArgumentOutOfRangeException(nameof(pattern), $"Unsupported pattern: {pattern}")
            };

            insertCmd.Parameters.Add(param);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();

            await ValidateInsertedDataAsync(conn, expectedValues, expectedSize, expectedLength);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        [MemberData(nameof(VectorFloat32TestData.GetVectorFloat32TestData), MemberType = typeof(VectorFloat32TestData))]
        public void TestStoredProcParamsForVectorFloat32(
        int pattern,
        object value,
        float[] expectedValues,
        int expectedSize,
        int expectedLength)
        {
            //Create SP for test
            using var conn = new SqlConnection(s_connectionString);
            conn.Open();
            DataTestUtility.CreateSP(conn, s_storedProcName, s_storedProcBody);
            using var command = new SqlCommand(s_storedProcName, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Set input and output parameters
            SqlParameter inputParam = pattern switch
            {
                1 => new SqlParameter
                {
                    ParameterName = s_vectorParamName,
                    SqlDbType = SqlDbTypeExtensions.Vector, // SqlDbTypeExtension.Vector
                    Value = value
                },
                2 => new SqlParameter(s_vectorParamName, value),
                3 => new SqlParameter(s_vectorParamName, SqlDbTypeExtensions.Vector) { Value = value },
                4 => new SqlParameter(s_vectorParamName, SqlDbTypeExtensions.Vector, new SqlVectorFloat32(3).Size) { Value = value },
                _ => throw new ArgumentOutOfRangeException(nameof(pattern), $"Unsupported pattern: {pattern}")
            };
            command.Parameters.Add(inputParam);

            var outputParam = new SqlParameter
            {
                ParameterName = s_outputVectorParamName,
                SqlDbType = SqlDbTypeExtensions.Vector,
                Direction = ParameterDirection.Output,
                Value = new SqlVectorFloat32(3)
            };
            command.Parameters.Add(outputParam);

            // Execute the stored procedure
            command.ExecuteNonQuery();

            // Validate the output parameter
            var vector = outputParam.Value as SqlVectorFloat32;
            ValidateSqlVectorFloat32Object(vector.IsNull, vector, expectedValues, expectedSize, expectedLength);

            // Validate error for conventional way of setting output parameters
            command.Parameters.Clear();
            command.Parameters.Add(inputParam);
            var outputParamWithoutVal = new SqlParameter(s_outputVectorParamName, SqlDbTypeExtensions.Vector, new SqlVectorFloat32(3).Size) { Direction = ParameterDirection.Output };
            command.Parameters.Add(outputParamWithoutVal);
            Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        [MemberData(nameof(VectorFloat32TestData.GetVectorFloat32TestData), MemberType = typeof(VectorFloat32TestData))]
        public async Task TestStoredProcParamsForVectorFloat32Async(
        int pattern,
        object value,
        float[] expectedValues,
        int expectedSize,
        int expectedLength)
        {
            //Create SP for test
            using var conn = new SqlConnection(s_connectionString);
            await conn.OpenAsync();
            DataTestUtility.CreateSP(conn, s_storedProcName, s_storedProcBody);
            using var command = new SqlCommand(s_storedProcName, conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Set input and output parameters
            SqlParameter inputParam = pattern switch
            {
                1 => new SqlParameter
                {
                    ParameterName = s_vectorParamName,
                    SqlDbType = SqlDbTypeExtensions.Vector, // SqlDbTypeExtension.Vector
                    Value = value
                },
                2 => new SqlParameter(s_vectorParamName, value),
                3 => new SqlParameter(s_vectorParamName, SqlDbTypeExtensions.Vector) { Value = value },
                4 => new SqlParameter(s_vectorParamName, SqlDbTypeExtensions.Vector, new SqlVectorFloat32(3).Size) { Value = value },
                _ => throw new ArgumentOutOfRangeException(nameof(pattern), $"Unsupported pattern: {pattern}")
            };
            command.Parameters.Add(inputParam);

            var outputParam = new SqlParameter
            {
                ParameterName = s_outputVectorParamName,
                SqlDbType = SqlDbTypeExtensions.Vector,
                Direction = ParameterDirection.Output,
                Value = new SqlVectorFloat32(3)
            };
            command.Parameters.Add(outputParam);

            // Execute the stored procedure
            await command.ExecuteNonQueryAsync();

            // Validate the output parameter
            var vector = outputParam.Value as SqlVectorFloat32;
            ValidateSqlVectorFloat32Object(vector.IsNull, vector, expectedValues, expectedSize, expectedLength);

            // Validate error for conventional way of setting output parameters
            command.Parameters.Clear();
            command.Parameters.Add(inputParam);
            var outputParamWithoutVal = new SqlParameter(s_outputVectorParamName, SqlDbTypeExtensions.Vector, new SqlVectorFloat32(3).Size) { Direction = ParameterDirection.Output };
            command.Parameters.Add(outputParamWithoutVal);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await command.ExecuteNonQueryAsync());
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        [InlineData(1)]
        [InlineData(2)]
        public void TestBulkCopyFromSqlTable(int bulkCopySourceMode)
        {
            //Setup source with test data and create destination table for bulkcopy.
            SqlConnection sourceConnection = new SqlConnection(s_connectionString);
            sourceConnection.Open();
            SqlConnection destinationConnection = new SqlConnection(s_connectionString);
            destinationConnection.Open();
            DataTable table = null;
            switch (bulkCopySourceMode)
            {
            
            case 1:
                    // Use SqlServer table as source
                    var insertCmd = new SqlCommand($"insert into {s_bulkCopySrcTableName} values (@VectorData)", sourceConnection);
                    var vectorParam = new SqlParameter(s_vectorParamName, new SqlVectorFloat32(VectorFloat32TestData.testData));

                    // Insert 2 rows with one non-null and null value
                    insertCmd.Parameters.Add(vectorParam);
                    Assert.Equal(1, insertCmd.ExecuteNonQuery());
                    insertCmd.Parameters.Clear();
                    vectorParam.Value = DBNull.Value;
                    insertCmd.Parameters.Add(vectorParam);
                    Assert.Equal(1, insertCmd.ExecuteNonQuery());
                    insertCmd.Parameters.Clear();
                    break;
                case 2:
                    table = new DataTable(s_bulkCopySrcTableName);
                    table.Columns.Add("Id", typeof(int));
                    table.Columns.Add("VectorData", typeof(SqlVectorFloat32));
                    table.Rows.Add(1, new SqlVectorFloat32(VectorFloat32TestData.testData));
                    table.Rows.Add(2, DBNull.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bulkCopySourceMode), $"Unsupported bulk copy source mode: {bulkCopySourceMode}");
            }

            
            
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
                switch (bulkCopySourceMode)
                {
                    case 1:
                        bulkCopy.WriteToServer(reader);
                        break;
                    case 2:
                        bulkCopy.WriteToServer(table);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(bulkCopySourceMode), $"Unsupported bulk copy source mode: {bulkCopySourceMode}");
                }
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
            Assert.Equal(VectorFloat32TestData.testData, ((SqlVectorFloat32)verifyReader.GetSqlVectorFloat32(0)).Values.ToArray());
            Assert.Equal(VectorFloat32TestData.testData.Length, ((SqlVectorFloat32)verifyReader.GetSqlVectorFloat32(0)).Length);
            Assert.Equal(VectorFloat32TestData.sizeInbytes, ((SqlVectorFloat32)verifyReader.GetSqlVectorFloat32(0)).Size);

            // Verify that we have another row
            Assert.True(verifyReader.Read(), "Second row not found in the table");

            // Verify that we have encountered null.
            Assert.True(verifyReader.IsDBNull(0));
            Assert.Equal(Array.Empty<float>(), ((SqlVectorFloat32)verifyReader.GetSqlVectorFloat32(0)).Values.ToArray());
            Assert.Equal(VectorFloat32TestData.testData.Length, ((SqlVectorFloat32)verifyReader.GetSqlVectorFloat32(0)).Length);
            Assert.Equal(VectorFloat32TestData.sizeInbytes, ((SqlVectorFloat32)verifyReader.GetSqlVectorFloat32(0)).Size);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        [InlineData(1)]
        [InlineData(2)]
        public async Task TestBulkCopyFromSqlTableAsync(int bulkCopySourceMode)
        {
            //Setup source with test data and create destination table for bulkcopy.
            SqlConnection sourceConnection = new SqlConnection(s_connectionString);
            await sourceConnection.OpenAsync();
            SqlConnection destinationConnection = new SqlConnection(s_connectionString);
            await destinationConnection.OpenAsync();

            DataTable table = null;
            switch (bulkCopySourceMode)
            {

                case 1:
                    // Use SqlServer table as source
                    var insertCmd = new SqlCommand($"insert into {s_bulkCopySrcTableName} values (@VectorData)", sourceConnection);
                    var vectorParam = new SqlParameter(s_vectorParamName, new SqlVectorFloat32(VectorFloat32TestData.testData));

                    // Insert 2 rows with one non-null and null value
                    insertCmd.Parameters.Add(vectorParam);
                    Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
                    insertCmd.Parameters.Clear();
                    vectorParam.Value = DBNull.Value;
                    insertCmd.Parameters.Add(vectorParam);
                    Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
                    insertCmd.Parameters.Clear();
                    break;
                case 2:
                    table = new DataTable(s_bulkCopySrcTableName);
                    table.Columns.Add("Id", typeof(int));
                    table.Columns.Add("VectorData", typeof(SqlVectorFloat32));
                    table.Rows.Add(1, new SqlVectorFloat32(VectorFloat32TestData.testData));
                    table.Rows.Add(2, DBNull.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bulkCopySourceMode), $"Unsupported bulk copy source mode: {bulkCopySourceMode}");
            }

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
            {   // Perform bulkcopy
                switch (bulkCopySourceMode)
                {
                    case 1:
                        await bulkCopy.WriteToServerAsync(reader);
                        break;
                    case 2:
                        await bulkCopy.WriteToServerAsync(table);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(bulkCopySourceMode), $"Unsupported bulk copy source mode: {bulkCopySourceMode}");
                }
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
            Assert.True(!await verifyReader.IsDBNullAsync(0), "First row in the table is null.");
            var vector = await verifyReader.GetFieldValueAsync<SqlVectorFloat32>(0);
            Assert.Equal(VectorFloat32TestData.testData, vector.Values.ToArray());
            Assert.Equal(VectorFloat32TestData.testData.Length, vector.Length);
            Assert.Equal(VectorFloat32TestData.sizeInbytes, vector.Size);

            // Verify that we have another row
            Assert.True(await verifyReader.ReadAsync(), "Second row not found in the table");

            // Verify that we have encountered null.
            Assert.True(await verifyReader.IsDBNullAsync(0));
            vector = await verifyReader.GetFieldValueAsync<SqlVectorFloat32>(0);
            Assert.Equal(Array.Empty<float>(), vector.Values.ToArray());
            Assert.Equal(VectorFloat32TestData.testData.Length, vector.Length);
            Assert.Equal(VectorFloat32TestData.sizeInbytes, vector.Size);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsVectorSupported))]
        public void TestInsertVectorsFloat32WithPrepare()
        {
            SqlConnection conn = new SqlConnection(s_connectionString);
            conn.Open();
            SqlCommand command = new SqlCommand(s_insertCmdString, conn);
            SqlParameter vectorParam = new SqlParameter("@VectorData", SqlDbTypeExtensions.Vector, new SqlVectorFloat32(3).Size);
            command.Parameters.Add(vectorParam);
            command.Prepare();
            for (int i = 0; i < 10; i++)
            {
                vectorParam.Value = new SqlVectorFloat32(new float[] { i + 0.1f, i + 0.2f, i + 0.3f });
                command.ExecuteNonQuery();
            }
            SqlCommand validateCommand = new SqlCommand($"SELECT VectorData FROM {s_tableName}", conn);
            using SqlDataReader reader = validateCommand.ExecuteReader();
            int rowcnt = 0;
            while (reader.Read())
            {
                float[] expectedData = new float[] { rowcnt + 0.1f, rowcnt + 0.2f, rowcnt + 0.3f };
                float[] dbData = reader.GetSqlVectorFloat32(0).Values.ToArray();
                Assert.Equal(expectedData, dbData);
                rowcnt++;
            }
            Assert.Equal(10, rowcnt);
        }
    }
}

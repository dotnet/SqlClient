// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    public abstract class NativeVectorTestDataBase<TElement>
        where TElement : unmanaged
    {
        public const int VectorHeaderSize = 8;
        public abstract TElement[] SampleScalarData { get; }

        public abstract TElement[,] SampleDataSet { get; }

        public int ValidSampleScalarDataLength => SampleScalarData.Length;
        // Incorrect size for SqlParameter.Size
        public abstract int IncorrectScalarDataParameterSize { get; }

        public abstract bool IsSupported { get; }

        public abstract string SqlServerTypeName { get; }

        public IEnumerable<object[]> TestData =>
        [
            // Pattern 1-4 with SqlVector<TElement>(values: SampleScalarData)
            [ 1, new SqlVector<TElement>(SampleScalarData), SampleScalarData, ValidSampleScalarDataLength ],
            [ 2, new SqlVector<TElement>(SampleScalarData), SampleScalarData, ValidSampleScalarDataLength ],
            [ 3, new SqlVector<TElement>(SampleScalarData), SampleScalarData, ValidSampleScalarDataLength ],
            [ 4, new SqlVector<TElement>(SampleScalarData), SampleScalarData, ValidSampleScalarDataLength ],

            // Pattern 1-4 with SqlVector<TElement>(n)
            [ 1, SqlVector<TElement>.CreateNull(ValidSampleScalarDataLength), Array.Empty<TElement>(), ValidSampleScalarDataLength ],
            [ 2, SqlVector<TElement>.CreateNull(ValidSampleScalarDataLength), Array.Empty<TElement>(), ValidSampleScalarDataLength ],
            [ 3, SqlVector<TElement>.CreateNull(ValidSampleScalarDataLength), Array.Empty<TElement>(), ValidSampleScalarDataLength ],
            [ 4, SqlVector<TElement>.CreateNull(ValidSampleScalarDataLength), Array.Empty<TElement>(), ValidSampleScalarDataLength ],

            // Pattern 1-4 with DBNull
            [ 1, DBNull.Value, Array.Empty<TElement>(), ValidSampleScalarDataLength ],
            [ 2, DBNull.Value, Array.Empty<TElement>(), ValidSampleScalarDataLength ],
            [ 3, DBNull.Value, Array.Empty<TElement>(), ValidSampleScalarDataLength ],
            [ 4, DBNull.Value, Array.Empty<TElement>(), ValidSampleScalarDataLength ],

            // Pattern 1-4 with SqlVector<TElement>.Null
            [ 1, SqlVector<TElement>.Null, Array.Empty<TElement>(), ValidSampleScalarDataLength ],

            // Following scenario is not supported in SqlClient.
            // This can only be fixed with a behavior change that SqlParameter.Value is internally set to DBNull.Value if it is set to null.
            // [ 2, SqlVector<TElement>.Null, Array.Empty<TElement>(), vectorColumnLength ],

            [ 3, SqlVector<TElement>.Null, Array.Empty<TElement>(), ValidSampleScalarDataLength ],
            [ 4, SqlVector<TElement>.Null, Array.Empty<TElement>(), ValidSampleScalarDataLength ]
        ];
    }

    public abstract class NativeVectorTestsBase<TElement, TTestData> : IDisposable
        where TElement : unmanaged
        where TTestData : NativeVectorTestDataBase<TElement>, new()
    {
        private const string VectorColumnName = "VectorData";
        private const string VectorParameterName = "@VectorData";
        private const string VectorOutputParameterName = "@OutputVectorData";

        private static readonly string s_connectionString = ManualTesting.Tests.DataTestUtility.TCPConnectionString;
        private static readonly string s_tableName = DataTestUtility.GetShortName("VectorTestTable");
        private static readonly string s_bulkCopySrcTableName = DataTestUtility.GetShortName("VectorBulkCopyTestTable");
        private static readonly int s_vectorDimensions = TestDataInstance.ValidSampleScalarDataLength;
        private static readonly string s_bulkCopySrcTableDef = $@"(Id INT PRIMARY KEY IDENTITY, {VectorColumnName} vector({s_vectorDimensions}, {TestDataInstance.SqlServerTypeName}) NULL)";
        private static readonly string s_tableDefinition = $@"(Id INT PRIMARY KEY IDENTITY, {VectorColumnName} vector({s_vectorDimensions}, {TestDataInstance.SqlServerTypeName}) NULL)";
        private static readonly string s_selectCmdString = $"SELECT {VectorColumnName} FROM {s_tableName} ORDER BY Id DESC";
        private static readonly string s_insertCmdString = $"INSERT INTO {s_tableName} ({VectorColumnName}) VALUES ({VectorParameterName})";
        private static readonly string s_storedProcName = DataTestUtility.GetShortName("VectorsAsVarcharSp");
        private static readonly string s_storedProcBody = $@"
                {VectorParameterName} vector({s_vectorDimensions}, {TestDataInstance.SqlServerTypeName}),   -- Input: Serialized TElement[] as JSON string
                {VectorOutputParameterName} vector({s_vectorDimensions}, {TestDataInstance.SqlServerTypeName}) OUTPUT  -- Output: Echoed back from latest inserted row
                AS
                BEGIN
                SET NOCOUNT ON;

                -- Insert into vector table
                INSERT INTO {s_tableName} ({VectorColumnName})
                VALUES ({VectorParameterName});

                -- Retrieve latest entry (assumes auto-incrementing ID)
                SELECT TOP 1 {VectorOutputParameterName} = {VectorColumnName}
                FROM {s_tableName}
                ORDER BY Id DESC;
                END;";

        // xUnit only allows MemberData for a test to point to static methods, properties and variables.
        // This presents a problem when the sample data needs to change based upon the element type of
        // the SqlVector, so this compromises: it instantiates a class derived from NativeVectorTestDataBase,
        // then projects the relevant fields from it as static properties in this base class.
        private static TTestData TestDataInstance =>
            field ??= new();

        public static bool IsSupported => TestDataInstance.IsSupported;

        public static IEnumerable<object[]> TestData => TestDataInstance.TestData;

        public NativeVectorTestsBase()
        {
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

        private static SqlParameter GetParameterByPattern(int pattern, object value) =>
            pattern switch
            {
                1 => new SqlParameter
                {
                    ParameterName = VectorParameterName,
                    SqlDbType = SqlDbTypeExtensions.Vector,
                    Value = value
                },
                2 => new SqlParameter(VectorParameterName, value),
                3 => new SqlParameter(VectorParameterName, SqlDbTypeExtensions.Vector) { Value = value },
                // Even if size is specified, the actual size is determined by the value passed and specified size is ignored.
                4 => new SqlParameter(VectorParameterName, SqlDbTypeExtensions.Vector, TestDataInstance.IncorrectScalarDataParameterSize) { Value = value },
                _ => throw new ArgumentOutOfRangeException(nameof(pattern), $"Unsupported pattern: {pattern}")
            };

        private static void ValidateSqlVectorObject(bool isNull, SqlVector<TElement> sqlVector, TElement[] expectedData, int expectedLength)
        {
            Assert.Equal(expectedData, sqlVector.Memory.ToArray());
            Assert.Equal(expectedLength, sqlVector.Length);
            if (!isNull)
            {
                Assert.False(sqlVector.IsNull, "IsNull set to true for a non-null value");
            }
            else
            {
                Assert.True(sqlVector.IsNull, "IsNull set to false for a null value");
            }
        }

        private void ValidateInsertedData(SqlConnection connection, TElement[] expectedData, int expectedLength)
        {
            using var selectCmd = new SqlCommand(s_selectCmdString, connection);
            using var reader = selectCmd.ExecuteReader();
            Assert.True(reader.Read(), "No data found in the table.");

            //For both null and non-null cases, validate the SqlVector<TElement> object
            ValidateSqlVectorObject(reader.IsDBNull(0), (SqlVector<TElement>)reader.GetSqlVector<TElement>(0), expectedData, expectedLength);
            ValidateSqlVectorObject(reader.IsDBNull(0), reader.GetFieldValue<SqlVector<TElement>>(0), expectedData, expectedLength);
            ValidateSqlVectorObject(reader.IsDBNull(0), (SqlVector<TElement>)reader.GetSqlValue(0), expectedData, expectedLength);

            if (!reader.IsDBNull(0))
            {
                ValidateSqlVectorObject(reader.IsDBNull(0), (SqlVector<TElement>)reader.GetValue(0), expectedData, expectedLength);
                ValidateSqlVectorObject(reader.IsDBNull(0), (SqlVector<TElement>)reader[0], expectedData, expectedLength);
                ValidateSqlVectorObject(reader.IsDBNull(0), (SqlVector<TElement>)reader[VectorColumnName], expectedData, expectedLength);
                Assert.Equal(expectedData, JsonSerializer.Deserialize<TElement[]>(reader.GetString(0)));
                Assert.Equal(expectedData, JsonSerializer.Deserialize<TElement[]>(reader.GetSqlString(0).Value));
                Assert.Equal(expectedData, JsonSerializer.Deserialize<TElement[]>(reader.GetFieldValue<string>(0)));
            }
            else
            {
                Assert.Equal(DBNull.Value, reader.GetValue(0));
                Assert.Equal(DBNull.Value, reader[0]);
                Assert.Equal(DBNull.Value, reader[VectorColumnName]);
                Assert.Throws<SqlNullValueException>(() => reader.GetString(0));
                Assert.Throws<SqlNullValueException>(() => reader.GetSqlString(0).Value);
                Assert.Throws<SqlNullValueException>(() => reader.GetFieldValue<string>(0));
            }
        }

        [ConditionalTheory(nameof(IsSupported))]
        [MemberData(nameof(TestData), DisableDiscoveryEnumeration = true)]
        public void TestSqlVectorParameterInsertionAndReads(
            int pattern,
            object value,
            TElement[] expectedValues,
            int expectedLength)
        {
            using var conn = new SqlConnection(s_connectionString);
            conn.Open();

            using var insertCmd = new SqlCommand(s_insertCmdString, conn);
            SqlParameter param = GetParameterByPattern(pattern, value);

            insertCmd.Parameters.Add(param);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();

            ValidateInsertedData(conn, expectedValues, expectedLength);
        }

        private async Task ValidateInsertedDataAsync(SqlConnection connection, TElement[] expectedData, int expectedLength)
        {
            using var selectCmd = new SqlCommand(s_selectCmdString, connection);
            using var reader = await selectCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "No data found in the table.");

            //For both null and non-null cases, validate the SqlVector<TElement> object
            ValidateSqlVectorObject(await reader.IsDBNullAsync(0), (SqlVector<TElement>)reader.GetSqlVector<TElement>(0), expectedData, expectedLength);
            ValidateSqlVectorObject(await reader.IsDBNullAsync(0), await reader.GetFieldValueAsync<SqlVector<TElement>>(0), expectedData, expectedLength);
            ValidateSqlVectorObject(await reader.IsDBNullAsync(0), (SqlVector<TElement>)reader.GetSqlValue(0), expectedData, expectedLength);

            if (!await reader.IsDBNullAsync(0))
            {
                ValidateSqlVectorObject(await reader.IsDBNullAsync(0), (SqlVector<TElement>)reader.GetValue(0), expectedData, expectedLength);
                ValidateSqlVectorObject(await reader.IsDBNullAsync(0), (SqlVector<TElement>)reader[0], expectedData, expectedLength);
                ValidateSqlVectorObject(await reader.IsDBNullAsync(0), (SqlVector<TElement>)reader[VectorColumnName], expectedData, expectedLength);
                Assert.Equal(expectedData, JsonSerializer.Deserialize<TElement[]>(reader.GetString(0)));
                Assert.Equal(expectedData, JsonSerializer.Deserialize<TElement[]>(reader.GetSqlString(0).Value));
                Assert.Equal(expectedData, JsonSerializer.Deserialize<TElement[]>(await reader.GetFieldValueAsync<string>(0)));
            }
            else
            {
                Assert.Equal(DBNull.Value, reader.GetValue(0));
                Assert.Equal(DBNull.Value, reader[0]);
                Assert.Equal(DBNull.Value, reader[VectorColumnName]);
                Assert.Throws<SqlNullValueException>(() => reader.GetString(0));
                Assert.Throws<SqlNullValueException>(() => reader.GetSqlString(0).Value);
                await Assert.ThrowsAsync<SqlNullValueException>(async () => await reader.GetFieldValueAsync<string>(0));
            }
        }

        [ConditionalTheory(nameof(IsSupported))]
        [MemberData(nameof(TestData), DisableDiscoveryEnumeration = true)]
        public async Task TestSqlVectorParameterInsertionAndReadsAsync(
            int pattern,
            object value,
            TElement[] expectedValues,
            int expectedLength)
        {
            using var conn = new SqlConnection(s_connectionString);
            await conn.OpenAsync();

            using var insertCmd = new SqlCommand(s_insertCmdString, conn);
            SqlParameter param = GetParameterByPattern(pattern, value);

            insertCmd.Parameters.Add(param);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();

            await ValidateInsertedDataAsync(conn, expectedValues, expectedLength);
        }

        [ConditionalTheory(nameof(IsSupported))]
        [MemberData(nameof(TestData), DisableDiscoveryEnumeration = true)]
        public void TestStoredProcParamsForVector(
            int pattern,
            object value,
            TElement[] expectedValues,
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
            SqlParameter inputParam = GetParameterByPattern(pattern, value);
            command.Parameters.Add(inputParam);

            var outputParam = new SqlParameter
            {
                ParameterName = VectorOutputParameterName,
                SqlDbType = SqlDbTypeExtensions.Vector,
                Direction = ParameterDirection.Output,
                Value = SqlVector<TElement>.CreateNull(TestDataInstance.ValidSampleScalarDataLength)
            };
            command.Parameters.Add(outputParam);

            // Execute the stored procedure
            command.ExecuteNonQuery();

            // Validate the output parameter
            var vector = (SqlVector<TElement>)outputParam.Value;
            ValidateSqlVectorObject(vector.IsNull, vector, expectedValues, expectedLength);

            // Validate error for conventional way of setting output parameters
            command.Parameters.Clear();
            command.Parameters.Add(inputParam);
            var outputParamWithoutVal = new SqlParameter(VectorOutputParameterName, SqlDbTypeExtensions.Vector, TestDataInstance.IncorrectScalarDataParameterSize) { Direction = ParameterDirection.Output };
            command.Parameters.Add(outputParamWithoutVal);
            Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());
        }

        [ConditionalTheory(nameof(IsSupported))]
        [MemberData(nameof(TestData), DisableDiscoveryEnumeration = true)]
        public async Task TestStoredProcParamsForVectorAsync(
            int pattern,
            object value,
            TElement[] expectedValues,
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
            SqlParameter inputParam = GetParameterByPattern(pattern, value);
            command.Parameters.Add(inputParam);

            var outputParam = new SqlParameter
            {
                ParameterName = VectorOutputParameterName,
                SqlDbType = SqlDbTypeExtensions.Vector,
                Direction = ParameterDirection.Output,
                Value = SqlVector<TElement>.CreateNull(TestDataInstance.ValidSampleScalarDataLength)
            };
            command.Parameters.Add(outputParam);

            // Execute the stored procedure
            await command.ExecuteNonQueryAsync();

            // Validate the output parameter
            var vector = (SqlVector<TElement>)outputParam.Value;
            ValidateSqlVectorObject(vector.IsNull, vector, expectedValues, expectedLength);

            // Validate error for conventional way of setting output parameters
            command.Parameters.Clear();
            command.Parameters.Add(inputParam);
            var outputParamWithoutVal = new SqlParameter(VectorOutputParameterName, SqlDbTypeExtensions.Vector, TestDataInstance.IncorrectScalarDataParameterSize) { Direction = ParameterDirection.Output };
            command.Parameters.Add(outputParamWithoutVal);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await command.ExecuteNonQueryAsync());
        }

        [ConditionalTheory(nameof(IsSupported))]
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
                    var insertCmd = new SqlCommand($"insert into {s_bulkCopySrcTableName} values ({VectorParameterName})", sourceConnection);
                    var vectorParam = new SqlParameter(VectorParameterName, new SqlVector<TElement>(TestDataInstance.SampleScalarData));

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
                    table.Columns.Add(VectorColumnName, typeof(SqlVector<TElement>));
                    table.Rows.Add(1, new SqlVector<TElement>(TestDataInstance.SampleScalarData));
                    table.Rows.Add(2, DBNull.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bulkCopySourceMode), $"Unsupported bulk copy source mode: {bulkCopySourceMode}");
            }



            //Bulkcopy from sql server table to destination table
            using SqlCommand sourceDataCommand = new SqlCommand($"SELECT Id, {VectorColumnName} FROM {s_bulkCopySrcTableName}", sourceConnection);
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
            using SqlCommand verifyCommand = new SqlCommand($"SELECT {VectorColumnName} from {s_tableName}", destinationConnection);
            using SqlDataReader verifyReader = verifyCommand.ExecuteReader();

            // Verify that we have data in the destination table
            Assert.True(verifyReader.Read(), "No data found in destination table after bulk copy.");

            // Validate first non-null value.
            Assert.True(!verifyReader.IsDBNull(0), "First row in the table is null.");
            Assert.Equal(TestDataInstance.SampleScalarData, ((SqlVector<TElement>)verifyReader.GetSqlVector<TElement>(0)).Memory.ToArray());
            Assert.Equal(TestDataInstance.SampleScalarData.Length, ((SqlVector<TElement>)verifyReader.GetSqlVector<TElement>(0)).Length);

            // Verify that we have another row
            Assert.True(verifyReader.Read(), "Second row not found in the table");

            // Verify that we have encountered null.
            Assert.True(verifyReader.IsDBNull(0));
            Assert.Equal(Array.Empty<TElement>(), ((SqlVector<TElement>)verifyReader.GetSqlVector<TElement>(0)).Memory.ToArray());
            Assert.Equal(TestDataInstance.SampleScalarData.Length, ((SqlVector<TElement>)verifyReader.GetSqlVector<TElement>(0)).Length);
        }

        [ConditionalTheory(nameof(IsSupported))]
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
                    var insertCmd = new SqlCommand($"insert into {s_bulkCopySrcTableName} values ({VectorParameterName})", sourceConnection);
                    var vectorParam = new SqlParameter(VectorParameterName, new SqlVector<TElement>(TestDataInstance.SampleScalarData));

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
                    table.Columns.Add(VectorColumnName, typeof(SqlVector<TElement>));
                    table.Rows.Add(1, new SqlVector<TElement>(TestDataInstance.SampleScalarData));
                    table.Rows.Add(2, DBNull.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bulkCopySourceMode), $"Unsupported bulk copy source mode: {bulkCopySourceMode}");
            }

            //Bulkcopy from sql server table to destination table
            using SqlCommand sourceDataCommand = new SqlCommand($"SELECT Id, {VectorColumnName} FROM {s_bulkCopySrcTableName}", sourceConnection);
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
            using SqlCommand verifyCommand = new SqlCommand($"SELECT {VectorColumnName} from {s_tableName}", destinationConnection);
            using SqlDataReader verifyReader = await verifyCommand.ExecuteReaderAsync();

            // Verify that we have data in the destination table
            Assert.True(await verifyReader.ReadAsync(), "No data found in destination table after bulk copy.");

            // Validate first non-null value.
            Assert.True(!await verifyReader.IsDBNullAsync(0), "First row in the table is null.");
            var vector = await verifyReader.GetFieldValueAsync<SqlVector<TElement>>(0);
            Assert.Equal(TestDataInstance.SampleScalarData, vector.Memory.ToArray());
            Assert.Equal(TestDataInstance.SampleScalarData.Length, vector.Length);

            // Verify that we have another row
            Assert.True(await verifyReader.ReadAsync(), "Second row not found in the table");

            // Verify that we have encountered null.
            Assert.True(await verifyReader.IsDBNullAsync(0));
            vector = await verifyReader.GetFieldValueAsync<SqlVector<TElement>>(0);
            Assert.Equal(Array.Empty<TElement>(), vector.Memory.ToArray());
            Assert.Equal(TestDataInstance.SampleScalarData.Length, vector.Length);
        }

        [ConditionalFact(nameof(IsSupported))]
        public void TestGetFieldTypeReturnsSqlVectorForVectorColumn()
        {
            using var connection = new SqlConnection(s_connectionString);
            connection.Open();

            // Insert a row so we can query it
            using (var insertCmd = new SqlCommand(s_insertCmdString, connection))
            {
                var param = insertCmd.Parameters.Add(VectorParameterName, SqlDbTypeExtensions.Vector);
                param.Value = new SqlVector<TElement>(TestDataInstance.SampleScalarData);
                insertCmd.ExecuteNonQuery();
            }

            using var selectCmd = new SqlCommand(s_selectCmdString, connection);
            using var reader = selectCmd.ExecuteReader();

            // Verify GetFieldType returns SqlVector<TElement> for the vector column
            Assert.Equal(typeof(SqlVector<TElement>), reader.GetFieldType(0));

            // Verify GetProviderSpecificFieldType also returns SqlVector<TElement>
            Assert.Equal(typeof(SqlVector<TElement>), reader.GetProviderSpecificFieldType(0));

            // Verify that GetValue returns an instance consistent with GetFieldType
            Assert.True(reader.Read(), "No data found in the table.");
            object value = reader.GetValue(0);
            Assert.IsType<SqlVector<TElement>>(value);
            Assert.Equal(TestDataInstance.SampleScalarData, ((SqlVector<TElement>)value).Memory.ToArray());

            // Verify GetFieldValue<SqlVector<TElement>> returns the correct typed value
            SqlVector<TElement> typedValue = reader.GetFieldValue<SqlVector<TElement>>(0);
            Assert.IsType<SqlVector<TElement>>(typedValue);
            Assert.Equal(TestDataInstance.SampleScalarData, typedValue.Memory.ToArray());
        }

        [ConditionalFact(nameof(IsSupported))]
        public void TestInsertVectorsWithPrepare()
        {
            SqlConnection conn = new SqlConnection(s_connectionString);
            conn.Open();
            SqlCommand command = new SqlCommand(s_insertCmdString, conn);
            SqlParameter vectorParam = new SqlParameter(VectorParameterName, SqlDbTypeExtensions.Vector);
            command.Parameters.Add(vectorParam);
            command.Prepare();

            TElement[,] sampleDataSet = TestDataInstance.SampleDataSet;
            for (int i = 0; i < sampleDataSet.GetLength(0); i++)
            {
                TElement[] rowData = GetMultidimensionalArraySlice(sampleDataSet, i);
                vectorParam.Value = new SqlVector<TElement>(rowData);
                command.ExecuteNonQuery();
            }

            SqlCommand validateCommand = new SqlCommand($"SELECT {VectorColumnName} FROM {s_tableName}", conn);
            using SqlDataReader reader = validateCommand.ExecuteReader();
            int rowcnt = 0;
            while (reader.Read())
            {
                TElement[] expectedData = GetMultidimensionalArraySlice(sampleDataSet, rowcnt);
                TElement[] dbData = reader.GetSqlVector<TElement>(0).Memory.ToArray();
                Assert.Equal(expectedData, dbData);
                rowcnt++;
            }
            Assert.Equal(10, rowcnt);

            static TElement[] GetMultidimensionalArraySlice(TElement[,] sourceArray, int dimension)
            {
                TElement[] dst = new TElement[sourceArray.GetLength(1)];

                for (int i = 0; i < dst.Length; i++)
                {
                    dst[i] = sourceArray[dimension, i];
                }
                return dst;
            }
        }
    }
}

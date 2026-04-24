// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest
{
    /// <summary>
    /// Abstract base class for vector backward compatibility tests that exchange
    /// vector data as varchar(max) JSON strings. Eliminates duplication between
    /// float32 and float16 test classes while allowing each to specify its own
    /// ConditionalFact/ConditionalTheory predicates.
    /// </summary>
    public abstract class VectorBackwardCompatTestBase : IDisposable
    {
        private static readonly string s_connectionString = DataTestUtility.TCPConnectionString;
        private static readonly string s_vectorParamName = "@VectorData";

        private readonly SqlConnection _connection;
        private readonly Table _vectorTable;
        private readonly Table _bulkCopySrcTable;
        private readonly StoredProcedure _storedProc;

        private readonly string _selectCmdString;
        private readonly string _insertCmdString;

        protected ITestOutputHelper Output { get; }

        /// <summary>
        /// Generates the test data values to use with Prepare tests.
        /// Float32 uses fractional values (e.g. i+0.1f), float16 uses whole numbers (e.g. i+1).
        /// </summary>
        protected abstract float[] GetPrepareTestValues(int i);

        protected VectorBackwardCompatTestBase(
            ITestOutputHelper output,
            string columnDefinition,
            string namePrefix)
        {
            Output = output;
            _connection = new SqlConnection(s_connectionString);
            _connection.Open();

            _vectorTable = new Table(_connection, namePrefix + "TestTable",
                $"(Id INT PRIMARY KEY IDENTITY, VectorData {columnDefinition} NULL)");

            _bulkCopySrcTable = new Table(_connection, namePrefix + "BulkCopyTestTable",
                "(Id INT PRIMARY KEY IDENTITY, VectorData varchar(max) NULL)");

            string storedProcBody = $@"
                @InputVectorJson VARCHAR(MAX),   -- Input: Serialized float[] as JSON string
                @OutputVectorJson VARCHAR(MAX) OUTPUT  -- Output: Echoed back from latest inserted row
                AS
                BEGIN
                SET NOCOUNT ON;

                -- Insert into vector table
                INSERT INTO {_vectorTable.Name} (VectorData)
                VALUES (@InputVectorJson);

                -- Retrieve latest entry (assumes auto-incrementing ID)
                SELECT TOP 1 @OutputVectorJson = VectorData
                FROM {_vectorTable.Name}
                ORDER BY Id DESC;
                END;";

            _storedProc = new StoredProcedure(_connection, namePrefix + "AsVarcharSp", storedProcBody);

            _selectCmdString = $"SELECT VectorData FROM {_vectorTable.Name} ORDER BY Id DESC";
            _insertCmdString = $"INSERT INTO {_vectorTable.Name} (VectorData) VALUES (@VectorData)";
        }

        public void Dispose()
        {
            // RAII objects drop themselves on Dispose in reverse order.
            _storedProc?.Dispose();
            _bulkCopySrcTable?.Dispose();
            _vectorTable?.Dispose();
            _connection?.Dispose();
        }

        #region Shared Helpers

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
            using var selectCmd = new SqlCommand(_selectCmdString, connection);
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
            using var selectCmd = new SqlCommand(_selectCmdString, connection);
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

        #endregion

        #region Test Implementations

        protected void InsertAndValidateAsVarchar(int pattern, string jsonValue, float[] expectedData)
        {
            using var conn = new SqlConnection(s_connectionString);
            conn.Open();

            using var insertCmd = new SqlCommand(_insertCmdString, conn);
            SqlParameter param = CreateVarcharParameter(pattern, jsonValue);
            insertCmd.Parameters.Add(param);
            Assert.Equal(1, insertCmd.ExecuteNonQuery());
            insertCmd.Parameters.Clear();

            ValidateInsertedData(conn, expectedData);
        }

        protected async Task InsertAndValidateAsVarcharAsync(int pattern, string jsonValue, float[] expectedData)
        {
            using var conn = new SqlConnection(s_connectionString);
            await conn.OpenAsync();

            using var insertCmd = new SqlCommand(_insertCmdString, conn);
            SqlParameter param = CreateVarcharParameter(pattern, jsonValue);
            insertCmd.Parameters.Add(param);
            Assert.Equal(1, await insertCmd.ExecuteNonQueryAsync());
            insertCmd.Parameters.Clear();

            await ValidateInsertedDataAsync(conn, expectedData);
        }

        protected void StoredProcRoundTrip(float[] data)
        {
            string dataAsJson = JsonSerializer.Serialize(data);

            using var conn = new SqlConnection(s_connectionString);
            conn.Open();

            using var command = new SqlCommand(_storedProc.Name, conn)
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

        protected async Task StoredProcRoundTripAsync(float[] data)
        {
            string dataAsJson = JsonSerializer.Serialize(data);

            using var conn = new SqlConnection(s_connectionString);
            await conn.OpenAsync();

            using var command = new SqlCommand(_storedProc.Name, conn)
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

        protected void BulkCopyRoundTrip(int bulkCopySourceMode, float[] testData)
        {
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
                    using (var insertCmd = new SqlCommand($"insert into {_bulkCopySrcTable.Name} values (@VectorData)", sourceConnection))
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
                    table = new DataTable(_bulkCopySrcTable.Name);
                    table.Columns.Add("Id", typeof(int));
                    table.Columns.Add("VectorData", typeof(string));
                    table.Rows.Add(1, testDataAsJson);
                    table.Rows.Add(2, DBNull.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bulkCopySourceMode), $"Unsupported bulk copy source mode: {bulkCopySourceMode}");
            }

            // Verify that the destination table is empty before bulk copy
            using SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {_vectorTable.Name}", destinationConnection);
            Assert.Equal(0, Convert.ToInt16(countCommand.ExecuteScalar()));

            // Initialize bulk copy configuration
            using SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection)
            {
                DestinationTableName = _vectorTable.Name,
            };

            switch (bulkCopySourceMode)
            {
                case 1:
                    using (SqlCommand sourceDataCommand = new SqlCommand($"SELECT Id, VectorData FROM {_bulkCopySrcTable.Name}", sourceConnection))
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
            using SqlCommand verifyCommand = new SqlCommand($"SELECT VectorData from {_vectorTable.Name}", destinationConnection);
            using SqlDataReader verifyReader = verifyCommand.ExecuteReader();

            Assert.True(verifyReader.Read(), "No data found in destination table after bulk copy.");
            Assert.False(verifyReader.IsDBNull(0), "First row in the table is null.");
            Assert.Equal(testData, JsonSerializer.Deserialize<float[]>(verifyReader.GetString(0)));

            Assert.True(verifyReader.Read(), "Second row not found in the table");
            Assert.True(verifyReader.IsDBNull(0));
        }

        protected async Task BulkCopyRoundTripAsync(int bulkCopySourceMode, float[] testData)
        {
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
                    using (var insertCmd = new SqlCommand($"insert into {_bulkCopySrcTable.Name} values (@VectorData)", sourceConnection))
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
                    table = new DataTable(_bulkCopySrcTable.Name);
                    table.Columns.Add("Id", typeof(int));
                    table.Columns.Add("VectorData", typeof(string));
                    table.Rows.Add(1, testDataAsJson);
                    table.Rows.Add(2, DBNull.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bulkCopySourceMode), $"Unsupported bulk copy source mode: {bulkCopySourceMode}");
            }

            // Verify that the destination table is empty before bulk copy
            using SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {_vectorTable.Name}", destinationConnection);
            Assert.Equal(0, Convert.ToInt16(await countCommand.ExecuteScalarAsync()));

            // Initialize bulk copy configuration
            using SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection)
            {
                DestinationTableName = _vectorTable.Name,
            };

            switch (bulkCopySourceMode)
            {
                case 1:
                    using (SqlCommand sourceDataCommand = new SqlCommand($"SELECT Id, VectorData FROM {_bulkCopySrcTable.Name}", sourceConnection))
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
            using SqlCommand verifyCommand = new SqlCommand($"SELECT VectorData from {_vectorTable.Name}", destinationConnection);
            using SqlDataReader verifyReader = await verifyCommand.ExecuteReaderAsync();

            Assert.True(await verifyReader.ReadAsync(), "No data found in destination table after bulk copy.");
            Assert.False(verifyReader.IsDBNull(0), "First row in the table is null.");
            Assert.Equal(testData, JsonSerializer.Deserialize<float[]>(verifyReader.GetString(0)));

            Assert.True(await verifyReader.ReadAsync(), "Second row not found in the table");
            Assert.True(await verifyReader.IsDBNullAsync(0));
        }

        protected void PreparedInsertRoundTrip()
        {
            using SqlConnection conn = new SqlConnection(s_connectionString);
            conn.Open();
            using SqlCommand command = new SqlCommand(_insertCmdString, conn);
            SqlParameter vectorParam = new SqlParameter("@VectorData", SqlDbType.VarChar, -1);
            command.Parameters.Add(vectorParam);
            command.Prepare();
            for (int i = 0; i < 10; i++)
            {
                vectorParam.Value = JsonSerializer.Serialize(GetPrepareTestValues(i));
                command.ExecuteNonQuery();
            }
            using SqlCommand validateCommand = new SqlCommand($"SELECT VectorData FROM {_vectorTable.Name}", conn);
            using SqlDataReader reader = validateCommand.ExecuteReader();
            int rowcnt = 0;
            while (reader.Read())
            {
                float[] expectedData = GetPrepareTestValues(rowcnt);
                float[] dbData = JsonSerializer.Deserialize<float[]>(reader.GetString(0))!;
                Assert.Equal(expectedData, dbData);
                rowcnt++;
            }
            Assert.Equal(10, rowcnt);
        }

        #endregion
    }
}

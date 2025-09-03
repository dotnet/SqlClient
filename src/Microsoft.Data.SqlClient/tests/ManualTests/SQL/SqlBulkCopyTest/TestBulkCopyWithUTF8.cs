// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.SqlBulkCopyTest
{
    /// <summary>
    /// Validates SqlBulkCopy functionality when working with UTF-8 encoded data.
    /// Ensures that data copied from a UTF-8 source table to a destination table retains its encoding and content integrity.
    /// </summary>
    public sealed class TestBulkCopyWithUtf8 : IDisposable
    {
        private static string s_sourceTable = DataTestUtility.GetUniqueName("SourceTableForUTF8Data");
        private static string s_destinationTable = DataTestUtility.GetUniqueName("DestinationTableForUTF8Data");
        private static string s_testValue = "test";
        private static byte[] s_testValueInUtf8Bytes = new byte[] { 0x74, 0x65, 0x73, 0x74 };
        private static readonly string s_insertQuery = $"INSERT INTO {s_sourceTable} VALUES('{s_testValue}')";

        /// <summary>
        /// Constructor: Initializes and populates source and destination tables required for the tests.
        /// </summary>
        public TestBulkCopyWithUtf8()
        {
            using SqlConnection sourceConnection = new SqlConnection(GetConnectionString(true));
            sourceConnection.Open();
            SetupTables(sourceConnection, s_sourceTable, s_destinationTable, s_insertQuery);
        }

        /// <summary>
        /// Cleanup method to drop tables after test completion.
        /// </summary>
        public void Dispose()
        {
            using SqlConnection connection = new SqlConnection(GetConnectionString(true));
            connection.Open();
            DataTestUtility.DropTable(connection, s_sourceTable);
            DataTestUtility.DropTable(connection, s_destinationTable);
            connection.Close();
        }

        /// <summary>
        /// Builds a connection string with or without Multiple Active Result Sets (MARS) property.
        /// </summary>
        private string GetConnectionString(bool enableMars)
        {
            return new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                MultipleActiveResultSets = enableMars
            }.ConnectionString;
        }

        /// <summary>
        /// Creates source and destination tables with a varchar(max) column with a collation setting
        /// that stores the data in UTF8 encoding and inserts the data in the source table. 
        /// </summary>
        private void SetupTables(SqlConnection connection, string sourceTable, string destinationTable, string insertQuery)
        {
            string columnDefinition = "(str_col varchar(max) COLLATE Latin1_General_100_CS_AS_KS_WS_SC_UTF8)";
            DataTestUtility.CreateTable(connection, sourceTable, columnDefinition);
            DataTestUtility.CreateTable(connection, destinationTable, columnDefinition);
            using SqlCommand insertCommand = connection.CreateCommand();
            insertCommand.CommandText = insertQuery;
            Helpers.TryExecute(insertCommand, insertQuery);
        }

        /// <summary>
        /// Synchronous test case: Validates that data copied using SqlBulkCopy matches UTF-8 byte sequence for test value.
        /// Tested with MARS enabled and disabled, and with streaming enabled and disabled.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility),
         nameof(DataTestUtility.AreConnStringsSetup),
         nameof(DataTestUtility.IsNotAzureServer),
         nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void BulkCopy_Utf8Data_ShouldMatchSource(bool isMarsEnabled, bool enableStreaming)
        {
            // Setup connections for source and destination tables
            string connectionString = GetConnectionString(isMarsEnabled);
            using SqlConnection sourceConnection = new SqlConnection(connectionString);
            sourceConnection.Open();
            using SqlConnection destinationConnection = new SqlConnection(connectionString);
            destinationConnection.Open();

            // Read data from source table
            using SqlCommand sourceDataCommand = new SqlCommand($"SELECT str_col FROM {s_sourceTable}", sourceConnection);
            using SqlDataReader reader = sourceDataCommand.ExecuteReader(CommandBehavior.SequentialAccess);

            // Verify that the destination table is empty before bulk copy
            using SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {s_destinationTable}", destinationConnection);
            Assert.Equal(0, Convert.ToInt16(countCommand.ExecuteScalar()));

            // Initialize bulk copy configuration
            using SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection)
            {
                EnableStreaming = enableStreaming,
                DestinationTableName = s_destinationTable
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

            // Verify that the 1 row from the source table has been copied into our destination table.
            Assert.Equal(1, Convert.ToInt16(countCommand.ExecuteScalar()));

            // Read the data from destination table as varbinary to verify the UTF-8 byte sequence
            using SqlCommand verifyCommand = new SqlCommand($"SELECT cast(str_col as varbinary) FROM {s_destinationTable}", destinationConnection);
            using SqlDataReader verifyReader = verifyCommand.ExecuteReader(CommandBehavior.SequentialAccess);

            // Verify that we have data in the destination table
            Assert.True(verifyReader.Read(), "No data found in destination table after bulk copy.");

            // Read the value of the column as SqlBinary.
            byte[] actualBytes = verifyReader.GetSqlBinary(0).Value;

            // Verify that the byte array matches the expected UTF-8 byte sequence
            Assert.Equal(s_testValueInUtf8Bytes.Length, actualBytes.Length);
            Assert.Equal(s_testValueInUtf8Bytes, actualBytes);
        }

        /// <summary>
        /// Asynchronous version of the testcase BulkCopy_Utf8Data_ShouldMatchSource
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility),
         nameof(DataTestUtility.AreConnStringsSetup),
         nameof(DataTestUtility.IsNotAzureServer),
         nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public async Task BulkCopy_Utf8Data_ShouldMatchSource_Async(bool isMarsEnabled, bool enableStreaming)
        {
            // Setup connections for source and destination tables
            string connectionString = GetConnectionString(isMarsEnabled);
            using SqlConnection sourceConnection = new SqlConnection(connectionString);
            await sourceConnection.OpenAsync();
            using SqlConnection destinationConnection = new SqlConnection(connectionString);
            await destinationConnection.OpenAsync();

            // Read data from source table
            using SqlCommand sourceDataCommand = new SqlCommand($"SELECT str_col FROM {s_sourceTable}", sourceConnection);
            using SqlDataReader reader = await sourceDataCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

            // Verify that the destination table is empty before bulk copy
            using SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {s_destinationTable}", destinationConnection);
            Assert.Equal(0, Convert.ToInt16(await countCommand.ExecuteScalarAsync()));

            // Initialize bulk copy configuration
            using SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection)
            {
                EnableStreaming = enableStreaming,
                DestinationTableName = s_destinationTable
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

            // Verify that the 1 row from the source table has been copied into our destination table.
            Assert.Equal(1, Convert.ToInt16(await countCommand.ExecuteScalarAsync()));

            // Read the data from destination table as varbinary to verify the UTF-8 byte sequence
            using SqlCommand verifyCommand = new SqlCommand($"SELECT cast(str_col as varbinary) FROM {s_destinationTable}", destinationConnection);
            using SqlDataReader verifyReader = await verifyCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

            // Verify that we have data in the destination table
            Assert.True(await verifyReader.ReadAsync(), "No data found in destination table after bulk copy.");

            // Read the value of the column as SqlBinary.
            byte[] actualBytes = verifyReader.GetSqlBinary(0).Value;

            // Verify that the byte array matches the expected UTF-8 byte sequence
            Assert.Equal(s_testValueInUtf8Bytes.Length, actualBytes.Length);
            Assert.Equal(s_testValueInUtf8Bytes, actualBytes);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class TestBulkCopyWithUtf8
    {
        private readonly string _connectionString;

        public TestBulkCopyWithUtf8()
        {
            _connectionString = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString){MultipleActiveResultSets = true}.ConnectionString;
        }

        private void SetupTables(SqlConnection connection, string sourceTable, string destinationTable, string insertQuery)
        {
            string columnDefinition = "(str_col varchar(max) COLLATE Latin1_General_100_CS_AS_KS_WS_SC_UTF8)";
            DataTestUtility.CreateTable(connection, sourceTable, columnDefinition);
            DataTestUtility.CreateTable(connection, destinationTable, columnDefinition);

            using SqlCommand insertCommand = connection.CreateCommand();
            insertCommand.CommandText = insertQuery;
            Helpers.TryExecute(insertCommand, insertQuery);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void BulkCopy_Utf8Data_ShouldMatchSource()
        {
            string sourceTable = DataTestUtility.GetUniqueName("SrcUtf8DataTable");
            string destinationTable = DataTestUtility.GetUniqueName("DstUtf8DataTable");
            string insertQuery = $"INSERT INTO {sourceTable} VALUES('test')";

            using SqlConnection sourceConnection = new SqlConnection(_connectionString);
            sourceConnection.Open();
            SetupTables(sourceConnection, sourceTable, destinationTable, insertQuery);

            using SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {destinationTable}", sourceConnection);
            long initialCount = Convert.ToInt64(countCommand.ExecuteScalar());

            using SqlCommand sourceDataCommand = new SqlCommand($"SELECT str_col FROM {sourceTable}", sourceConnection);
            using SqlDataReader reader = sourceDataCommand.ExecuteReader(CommandBehavior.SequentialAccess);

            using SqlConnection destinationConnection = new SqlConnection(_connectionString);
            destinationConnection.Open();

            using SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection)
            {
                EnableStreaming = true,
                DestinationTableName = destinationTable
            };

            try
            {
                bulkCopy.WriteToServer(reader);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Bulk copy failed: {ex.Message}");
            }

            long finalCount = Convert.ToInt64(countCommand.ExecuteScalar());
            Assert.Equal(1, finalCount - initialCount);

            using SqlCommand verifyCommand = new SqlCommand($"SELECT str_col FROM {destinationTable}", destinationConnection);
            using SqlDataReader verifyReader = verifyCommand.ExecuteReader(CommandBehavior.SequentialAccess);

            byte[] expectedBytes = Encoding.UTF8.GetBytes("test");

            Assert.True(verifyReader.Read(), "No data found in destination table after bulk copy.");

            byte[] actualBytes = Encoding.UTF8.GetBytes(verifyReader.GetString(0));
            Assert.Equal(expectedBytes.Length, actualBytes.Length);
            Assert.Equal(expectedBytes, actualBytes);
        }
    }
}

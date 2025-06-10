// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public sealed class TestBulkCopyWithUtf8 : IDisposable
    {
        private static string sourceTable = DataTestUtility.GetUniqueName("SrcUtf8DataTable");
        private static string destinationTable = DataTestUtility.GetUniqueName("DstUtf8DataTable");
        private static readonly string insertQuery = $"INSERT INTO {sourceTable} VALUES('test')";

        public TestBulkCopyWithUtf8()
        {
            using SqlConnection sourceConnection = new SqlConnection(GetConnectionString(true));
            sourceConnection.Open();
            SetupTables(sourceConnection, sourceTable, destinationTable, insertQuery);
        }

        private string GetConnectionString(bool enableMars)
        {
            return new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                MultipleActiveResultSets = enableMars
            }.ConnectionString;
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
            using SqlConnection sourceConnection = new SqlConnection(GetConnectionString(isMarsEnabled));
            sourceConnection.Open();
            using SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {destinationTable}", sourceConnection);
            long initialCount = Convert.ToInt64(countCommand.ExecuteScalar());
            using SqlCommand sourceDataCommand = new SqlCommand($"SELECT str_col FROM {sourceTable}", sourceConnection);
            using SqlDataReader reader = sourceDataCommand.ExecuteReader(CommandBehavior.SequentialAccess);
            using SqlConnection destinationConnection = new SqlConnection(GetConnectionString(isMarsEnabled));
            destinationConnection.Open();
            
            using SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection)
            {
                EnableStreaming = enableStreaming,
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
            
            reader.Close();
            long finalCount = Convert.ToInt64(countCommand.ExecuteScalar());
            Assert.Equal(1, finalCount - initialCount);
            using SqlCommand verifyCommand = new SqlCommand($"SELECT cast(str_col as varbinary) FROM {destinationTable}", destinationConnection);
            using SqlDataReader verifyReader = verifyCommand.ExecuteReader(CommandBehavior.SequentialAccess);
            byte[] expectedBytes = new byte[] { 0x74, 0x65, 0x73, 0x74 };
            Assert.True(verifyReader.Read(), "No data found in destination table after bulk copy.");
            byte[] actualBytes = verifyReader.GetSqlBinary(0).Value;
            Assert.Equal(expectedBytes.Length, actualBytes.Length);
            Assert.Equal(expectedBytes, actualBytes);
        }


        [ConditionalTheory(typeof(DataTestUtility),
         nameof(DataTestUtility.AreConnStringsSetup),
         nameof(DataTestUtility.IsNotAzureServer),
         nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(true,true)]
        [InlineData(false,true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public async Task BulkCopy_Utf8Data_ShouldMatchSource_Async(bool isMarsEnabled, bool enableStreaming)
        {
            string connectionString = GetConnectionString(isMarsEnabled);
            using SqlConnection sourceConnection = new SqlConnection(connectionString);
            await sourceConnection.OpenAsync();
            using SqlCommand countCommand = new SqlCommand($"SELECT COUNT(*) FROM {destinationTable}", sourceConnection);
            long initialCount = Convert.ToInt64(await countCommand.ExecuteScalarAsync());
            using SqlCommand sourceDataCommand = new SqlCommand($"SELECT str_col FROM {sourceTable}", sourceConnection);
            using SqlDataReader reader = await sourceDataCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            using SqlConnection destinationConnection = new SqlConnection(connectionString);
            await destinationConnection.OpenAsync();

            using SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection)
            {
                EnableStreaming = enableStreaming,
                DestinationTableName = destinationTable
            };

            try
            {
                await bulkCopy.WriteToServerAsync(reader);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Bulk copy failed: {ex.Message}");
            }

            reader.Close();
            long finalCount = Convert.ToInt64(await countCommand.ExecuteScalarAsync());
            Assert.Equal(1, finalCount - initialCount);
            using SqlCommand verifyCommand = new SqlCommand($"SELECT cast(str_col as varbinary) FROM {destinationTable}", destinationConnection);
            using SqlDataReader verifyReader = await verifyCommand.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            byte[] expectedBytes = new byte[] { 0x74, 0x65, 0x73, 0x74 };
            Assert.True(await verifyReader.ReadAsync(), "No data found in destination table after bulk copy.");
            byte[] actualBytes = verifyReader.GetSqlBinary(0).Value;
            Assert.Equal(expectedBytes.Length, actualBytes.Length);
            Assert.Equal(expectedBytes, actualBytes);
        }

        public void Dispose()
        {
            using SqlConnection connection = new SqlConnection(GetConnectionString(true));
            connection.Open();
            DataTestUtility.DropTable(connection, sourceTable);
            DataTestUtility.DropTable(connection, destinationTable);
            connection.Close();
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class WriteToServerTest
    {
        private readonly string _connectionString = null;
        private readonly string _tableName1 = DataTestUtility.GetShortName("Bulk1");
        private readonly string _tableName2 = DataTestUtility.GetShortName("Bulk2");

        public WriteToServerTest()
        {
            _connectionString = (new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = true }).ConnectionString;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        public async Task WriteToServerWithDbReaderFollowedByWriteToServerWithDataRowsShouldSucceed()
        {
            try
            {
                SetupTestTables();

                DataRow[] dataRows = WriteToServerTest.CreateDataRows();
                Assert.Equal(4, dataRows.Length); // Verify the number of rows created

                DoBulkCopy(dataRows);
                await DoBulkCopyAsync(dataRows);
            }
            finally
            {
                RemoveTestTables();
            }
        }

        private void SetupTestTables()
        {
            // Create the source table and insert some data
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            DataTestUtility.DropTable(connection, _tableName1);
            DataTestUtility.DropTable(connection, _tableName2);

            using SqlCommand command = connection.CreateCommand();

            Helpers.TryExecute(command, $"create table {_tableName1} (Id int identity primary key, FirstName nvarchar(50), LastName nvarchar(50))");
            Helpers.TryExecute(command, $"create table {_tableName2} (Id int identity primary key, FirstName nvarchar(50), LastName nvarchar(50))");

            Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('John', 'Doe')");
            Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('Johnny', 'Smith')");
            Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('Jenny', 'Doe')");
            Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('Jane', 'Smith')");
        }

        private static DataRow[] CreateDataRows()
        {
            DataTable table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("FirstName", typeof(string));
            table.Columns.Add("LastName", typeof(string));

            table.Rows.Add(null, "Aaron", "Washington");
            table.Rows.Add(null, "Barry", "Mannilow");
            table.Rows.Add(null, "Charles", "Babage");
            table.Rows.Add(null, "Dean", "Snipes");

            return table.Select();
        }

        private void RemoveTestTables()
        {
            // Simplify the using statement in a small block of code
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            DataTestUtility.DropTable(connection, _tableName1);
            DataTestUtility.DropTable(connection, _tableName2);
        }

        private void DoBulkCopy(DataRow[] dataRows)
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            connection.Open();

            using SqlCommand command = connection.CreateCommand();
            command.CommandText = $"select * from {_tableName1}";

            using IDataReader reader = command.ExecuteReader();

            using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);

            bulkCopy.DestinationTableName = _tableName2;

            BulkCopy(bulkCopy, reader, dataRows);
        }

        private async Task DoBulkCopyAsync(DataRow[] dataRows)
        {
            // Test should be run with MARS enabled
            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using SqlCommand command = connection.CreateCommand();
            command.CommandText = $"select * from {_tableName1}";

            using IDataReader reader = await command.ExecuteReaderAsync();

            using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);

            bulkCopy.DestinationTableName = _tableName2;

            await BulkCopyAsync(bulkCopy, reader, dataRows);
        }

        private static void BulkCopy(SqlBulkCopy bulkCopy, IDataReader reader, DataRow[] dataRows)
        {
            bulkCopy.WriteToServer(reader);
            Assert.Equal(dataRows.Length, bulkCopy.RowsCopied); // Verify the number of rows copied from the reader
            bulkCopy.WriteToServer(dataRows);
            Assert.Equal(dataRows.Length, bulkCopy.RowsCopied); // Verify the number of rows copied from the reader
        }

        private static async Task BulkCopyAsync(SqlBulkCopy bulkCopy, IDataReader reader, DataRow[] dataRows)
        {
            await bulkCopy.WriteToServerAsync(reader);
            Assert.Equal(dataRows.Length, bulkCopy.RowsCopied); // Verify the number of rows copied from the reader
            await bulkCopy.WriteToServerAsync(dataRows);
            Assert.Equal(dataRows.Length, bulkCopy.RowsCopied); // Verify the number of rows copied from the reader
        }
    }
}

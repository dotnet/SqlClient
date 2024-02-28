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
        private string _connectionString = null;

        public WriteToServerTest()
        {
            _connectionString = (new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = true }).ConnectionString;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public async Task WriteToServerWithDbReaderFollowedByWriteToServerWithDataRowsShouldSucceed()
        {
            try
            {
                SetupTestTables();

                DataRow[] dataRows = CreateDataRows();

                bool result = DoBulkCopy(dataRows);
                Assert.True(result, "WriteToServer with IDataReader followed by WriteToServer with data rows test failed.");
                Assert.True(GetCopiedRecordsCount() == 8, "WriteToServer with IDataReader followed by WriteToServer with data rows test failed.");  

                CleanupTestTable2();

                result = await DoBulkCopyAsync(dataRows);
                Assert.True(result, "WriteToServerAsync with IDataReader followed by WriteToServerAsync with data rows test failed.");
                Assert.True(GetCopiedRecordsCount() == 8, "WriteToServerAsync with IDataReader followed by WriteToServerAsync with data rows test failed.");
            }
            finally
            {
                RemoveTestTables();
            }
        }

        private DataRow[] CreateDataRows()
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

        private int GetCopiedRecordsCount()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand("select count(*) from TestTable2", connection))
                {
                    return (int)command.ExecuteScalar();
                }
            }
        }

        private void CleanupTestTable2()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                SqlCommand command = connection.CreateCommand();

                Helpers.TryExecute(command, "delete from TestTable2");
            }
        }

        private void SetupTestTables()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                SqlCommand command = connection.CreateCommand();

                Helpers.TryExecute(command, "drop table if exists TestTable1");
                Helpers.TryExecute(command, "drop table if exists TestTable2");

                Helpers.TryExecute(command, "create table TestTable1 (Id int identity primary key, FirstName nvarchar(50), LastName nvarchar(50))");
                Helpers.TryExecute(command, "create table TestTable2 (Id int identity primary key, FirstName nvarchar(50), LastName nvarchar(50))");

                Helpers.TryExecute(command, "insert into TestTable1 (Firstname, LastName) values ('John', 'Doe')");
                Helpers.TryExecute(command, "insert into TestTable1 (Firstname, LastName) values ('Johnny', 'Smith')");
                Helpers.TryExecute(command, "insert into TestTable1 (Firstname, LastName) values ('Jenny', 'Doe')");
                Helpers.TryExecute(command, "insert into TestTable1 (Firstname, LastName) values ('Jane', 'Smith')");
            }
        }

        private void RemoveTestTables()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                SqlCommand command = connection.CreateCommand();

                Helpers.TryExecute(command, "drop table if exists TestTable1");
                Helpers.TryExecute(command, "drop table if exists TestTable2");
            }
        }

        private bool DoBulkCopy(DataRow[] dataRows)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string sql = "select * from TestTable1";
                SqlCommand command = new SqlCommand(sql, connection);
                IDataReader reader = command.ExecuteReader();
                SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);
                bulkCopy.DestinationTableName = "TestTable2";

                return BulkCopy(bulkCopy, reader, dataRows);
            }
        }

        private async Task<bool> DoBulkCopyAsync(DataRow[] dataRows)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                SqlCommand command = connection.CreateCommand();

                string sql = "select * from TestTable1";
                command = new SqlCommand(sql, connection);
                IDataReader reader = await command.ExecuteReaderAsync();
                SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);
                bulkCopy.DestinationTableName = "TestTable2";

                return await BulkCopyAsync(bulkCopy, reader, dataRows);
            }
        }

        private bool BulkCopy(SqlBulkCopy bulkCopy, IDataReader reader, DataRow[] dataRows)
        {
            try
            {
                using (reader)
                {
                    bulkCopy.WriteToServer(reader);
                }

                bulkCopy.WriteToServer(dataRows);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> BulkCopyAsync(SqlBulkCopy bulkCopy, IDataReader reader, DataRow[] dataRows)
        {
            try
            {
                using (reader)
                {
                    await bulkCopy.WriteToServerAsync(reader);
                }

                await bulkCopy.WriteToServerAsync(dataRows);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

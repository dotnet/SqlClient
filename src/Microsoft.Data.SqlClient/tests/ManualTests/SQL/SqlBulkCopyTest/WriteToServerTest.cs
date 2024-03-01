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
        private readonly string _tableName1 = DataTestUtility.GetUniqueName("Bulk1");
        private readonly string _tableName2 = DataTestUtility.GetUniqueName("Bulk2");
        private static int _copiedRecordCount;

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

                DataRow[] dataRows = WriteToServerTest.CreateDataRows();
                Assert.Equal(4, dataRows.Length);

                _copiedRecordCount = 0;
                bool result = DoBulkCopy(dataRows);
                Assert.True(result, "WriteToServer with IDataReader followed by WriteToServer with data rows test failed.");
                Assert.Equal(8, _copiedRecordCount);

                CleanupTestTable2();

                _copiedRecordCount = 0;
                result = await DoBulkCopyAsync(dataRows);
                Assert.True(result, "WriteToServerAsync with IDataReader followed by WriteToServerAsync with data rows test failed.");
                Assert.Equal(8, _copiedRecordCount);
            }
            finally
            {
                RemoveTestTables();
            }
        }

        private void SetupTestTables()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    DataTestUtility.DropTable(connection, _tableName1);
                    DataTestUtility.DropTable(connection, _tableName2);

                    Helpers.TryExecute(command, $"create table {_tableName1} (Id int identity primary key, FirstName nvarchar(50), LastName nvarchar(50))");
                    Helpers.TryExecute(command, $"create table {_tableName2} (Id int identity primary key, FirstName nvarchar(50), LastName nvarchar(50))");

                    Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('John', 'Doe')");
                    Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('Johnny', 'Smith')");
                    Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('Jenny', 'Doe')");
                    Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('Jane', 'Smith')");
                }
            }
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

        private void CleanupTestTable2()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    Helpers.TryExecute(command, $"delete from {_tableName2}");
                }
            }
        }

        private void RemoveTestTables()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                DataTestUtility.DropTable(connection, _tableName1);
                DataTestUtility.DropTable(connection, _tableName2);
            }
        }

        private bool DoBulkCopy(DataRow[] dataRows)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = $"select * from {_tableName1}";

                    using IDataReader reader = command.ExecuteReader();

                    using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);

                    bulkCopy.DestinationTableName = _tableName2;

                    bool result = BulkCopy(bulkCopy, reader, dataRows);

                    return result;
                }
            }
        }

        private async Task<bool> DoBulkCopyAsync(DataRow[] dataRows)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = $"select * from {_tableName1}";

                    using IDataReader reader = await command.ExecuteReaderAsync();

                    using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);

                    bulkCopy.DestinationTableName = _tableName2;

                    bool result = await BulkCopyAsync(bulkCopy, reader, dataRows);

                    return result;
                }
            }
        }

        private static bool BulkCopy(SqlBulkCopy bulkCopy, IDataReader reader, DataRow[] dataRows)
        {
            try
            {
                bulkCopy.WriteToServer(reader);
                _copiedRecordCount = bulkCopy.RowsCopied;
                bulkCopy.WriteToServer(dataRows);
                _copiedRecordCount += bulkCopy.RowsCopied;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static async Task<bool> BulkCopyAsync(SqlBulkCopy bulkCopy, IDataReader reader, DataRow[] dataRows)
        {
            try
            {
                await bulkCopy.WriteToServerAsync(reader);
                _copiedRecordCount = bulkCopy.RowsCopied;
                await bulkCopy.WriteToServerAsync(dataRows);
                _copiedRecordCount += bulkCopy.RowsCopied;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

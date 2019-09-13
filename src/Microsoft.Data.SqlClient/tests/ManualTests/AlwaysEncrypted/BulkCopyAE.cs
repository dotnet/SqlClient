// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    /// <summary>
    /// Always Encrypted public API Manual tests.
    /// TODO: These tests are marked as Windows only for now but should be run for all platforms once the Master Key is accessible to this app from Azure Key Vault.
    /// </summary>
    [PlatformSpecific(TestPlatforms.Windows)]
    public class BulkCopyAE : IClassFixture<SQLSetupStrategy>, IDisposable
    {
        private SQLSetupStrategy fixture;

        private readonly string tableName;

        public BulkCopyAE(SQLSetupStrategy fixture)
        {
            this.fixture = fixture;
            tableName = fixture.BulkCopyAETestTable.Name;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void TestBulkCopyString()
        {
            var dataTable = new DataTable();
            dataTable.Columns.Add("c1", typeof(string));

            var dataRow = dataTable.NewRow();
            String result = "stringValue";
            dataRow["c1"] = result;
            dataTable.Rows.Add(dataRow);
            dataTable.AcceptChanges();

            using (var connection = new SqlConnection(string.Concat(DataTestUtility.TcpConnStr, " Column Encryption Setting = Enabled;")))
            using (var bulkCopy = new SqlBulkCopy(connection)
            {
                EnableStreaming = true,
                BatchSize = 1,
                DestinationTableName = "[" + tableName + "]"
            })
            {
                connection.Open();
                bulkCopy.WriteToServer(dataTable);

                string queryString = "SELECT * FROM [" + tableName + "];";
                SqlCommand command = new SqlCommand(queryString, connection);
                SqlDataReader reader = command.ExecuteReader();
                reader.Read();
                Assert.Equal(result, reader.GetString(0));
            }
        }

        public void Dispose()
        {
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                Table.DeleteData(fixture.BulkCopyAETestTable.Name, sqlConnection);
            }
        }
    }
}

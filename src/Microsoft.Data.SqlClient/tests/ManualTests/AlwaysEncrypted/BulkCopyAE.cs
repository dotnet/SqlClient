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
    /// </summary>
    public class BulkCopyAE : IClassFixture<PlatformSpecificTestContext>, IDisposable
    {
        private SQLSetupStrategy fixture;

        private readonly string tableName;

        public BulkCopyAE(PlatformSpecificTestContext context)
        {
            fixture = context.Fixture;
            tableName = fixture.BulkCopyAETestTable.Name;
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void TestBulkCopyString(string connectionString)
        {
            var dataTable = new DataTable();
            dataTable.Columns.Add("c1", typeof(string));

            var dataRow = dataTable.NewRow();
            string result = "stringValue";
            dataRow["c1"] = result;
            dataTable.Rows.Add(dataRow);
            dataTable.AcceptChanges();

            var encryptionEnabledConnectionString = new SqlConnectionStringBuilder(connectionString)
            {
                ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled
            }.ConnectionString;

            using (var connection = new SqlConnection(encryptionEnabledConnectionString))
            using (var bulkCopy = new SqlBulkCopy(connection)
            {
                EnableStreaming = true,
                BatchSize = 1,
                DestinationTableName = "[" + tableName + "]"
            })
            {
                connection.Open();
                Table.DeleteData(tableName, connection);

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
            foreach (string connection in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connection))
                {
                    sqlConnection.Open();
                    Table.DeleteData(fixture.BulkCopyAETestTable.Name, sqlConnection);
                }
            }
        }
    }
}

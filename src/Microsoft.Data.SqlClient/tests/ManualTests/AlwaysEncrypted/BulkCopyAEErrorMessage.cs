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
    public class BulkCopyAEErrorMessage : IClassFixture<SQLSetupStrategyCertStoreProvider>, IDisposable
    {
        private SQLSetupStrategyCertStoreProvider fixture;
        
        private readonly string _tableName;
        private readonly string _columnName;

        public BulkCopyAEErrorMessage(SQLSetupStrategyCertStoreProvider fixture)
        {
            this.fixture = fixture;
            _tableName = fixture.BulkCopyAEErrorMessageTestTable.Name;
            _columnName = "c1";
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void TextToIntErrorMessageTest(string connectionString)
        {
            string value = "stringValue";
            DataTable dataTable = CreateTable(value);

            Assert.True(StringToIntTest(connectionString, _tableName, dataTable, value, dataTable.Rows.Count), "Did not get any exceptions for DataTable when converting data from 'string' to 'int' datatype!");
            Assert.True(StringToIntTest(connectionString, _tableName, dataTable.Select(), value, dataTable.Rows.Count),"Did not get any exceptions for DataRow[] when converting data from 'string' to 'int' datatype!");
        }

        private DataTable CreateTable(string value)
        {
            var dataTable = new DataTable();
            dataTable.Columns.Add(_columnName, typeof(string));

            var dataRow = dataTable.NewRow();            
            dataRow[_columnName] = value;
            dataTable.Rows.Add(dataRow);
            dataTable.AcceptChanges();

            return dataTable;
        }

        private bool StringToIntTest(string connectionString, string targetTable, object dataSet, string value, int rowNo, string targetType = "int")
        {
            var encryptionEnabledConnectionString = new SqlConnectionStringBuilder(connectionString)
            {
                ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled
            }.ConnectionString;

            bool hitException = false;
            try
            {
                using (var connection = new SqlConnection(encryptionEnabledConnectionString))
                using (var bulkCopy = new SqlBulkCopy(connection)
                {
                    EnableStreaming = true,
                    BatchSize = 1,
                    DestinationTableName = "[" + _tableName + "]"
                })
                {
                    connection.Open();
                    bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(0, 0));

                    if (dataSet as DataTable != null)
                        bulkCopy.WriteToServer((DataTable)dataSet);
                    if (dataSet as DataRow[] != null)
                        bulkCopy.WriteToServer((DataRow[])dataSet);

                    bulkCopy.Close();
                }
            }
            catch (Exception ex)
            {
                object[] args =
                    new object[] { string.Empty, value.GetType().Name, targetType, 0, _columnName, rowNo};
                string expectedErrorMsg = string.Format(SystemDataResourceManager.Instance.SQL_BulkLoadCannotConvertValue, args);
                Assert.True(ex.Message.Contains(expectedErrorMsg), "Unexpected error message: " + ex.Message);
                hitException = true;
            }
            return hitException;
        }

        public void Dispose()
        {
            foreach (string connection in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connection))
                {
                    sqlConnection.Open();
                    Table.DeleteData(_tableName, sqlConnection);
                }
            }
        }
    }
}

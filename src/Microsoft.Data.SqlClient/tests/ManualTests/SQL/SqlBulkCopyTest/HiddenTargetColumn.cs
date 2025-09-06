// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SqlBulkCopyTests
{
    public class HiddenTargetColumn
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void WriteToServer_CopyToHiddenTargetColumn_ThrowsSqlException()
        {
            string connectionString = DataTestUtility.TCPConnectionString;
            string destinationTable = DataTestUtility.GetUniqueNameForSqlServer("HiddenTargetColumn");
            string destinationHistoryTable = DataTestUtility.GetUniqueNameForSqlServer("HiddenTargetColumn_History");

            using (SqlConnection dstConn = new SqlConnection(connectionString))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();

                try
                {
                    DataTestUtility.CreateTable(dstConn, destinationTable, $"""
(
    Column1 int primary key not null,
    Column2 nvarchar(10) not null,
    [Employee's First Name] varchar(max) null,
    ValidFrom datetime2 generated always as row start hidden not null,
    ValidTo datetime2 generated always as row end hidden not null,
    period for system_time (ValidFrom, ValidTo)
)
with (system_versioning = on(history_table = dbo.{destinationHistoryTable}));
""");

                    using (SqlConnection srcConn = new SqlConnection(connectionString))
                    using (SqlCommand srcCmd = new SqlCommand("select top 5 EmployeeID, FirstName, LastName, HireDate, sysdatetime() as CurrentDate from employees", srcConn))
                    {
                        srcConn.Open();

                        using (DbDataReader reader = srcCmd.ExecuteReader())
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = destinationTable;
                            SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;

                            ColumnMappings.Add("EmployeeID", "Column1");
                            ColumnMappings.Add("LastName", "Column2");
                            ColumnMappings.Add("FirstName", "Employee's First Name");
                            ColumnMappings.Add("HireDate", "ValidFrom");
                            ColumnMappings.Add("CurrentDate", "ValidTo");

                            SqlException sqlEx = Assert.Throws<SqlException>(() => bulkcopy.WriteToServer(reader));

                            Assert.Equal(13536, sqlEx.Number);
                            Assert.StartsWith($"Cannot insert an explicit value into a GENERATED ALWAYS column in table '{dstConn.Database}.dbo.{destinationTable.Replace("[", "").Replace("]", "")}'.", sqlEx.Message);
                        }
                    }
                }
                finally
                {
                    DataTestUtility.RunNonQuery(connectionString, $"""
alter table {destinationTable} set (system_versioning = off);
alter table {destinationTable} drop period for system_time;
""");
                    DataTestUtility.DropTable(dstConn, destinationTable);
                    DataTestUtility.DropTable(dstConn, destinationHistoryTable);
                }
            }
        }
    }
}

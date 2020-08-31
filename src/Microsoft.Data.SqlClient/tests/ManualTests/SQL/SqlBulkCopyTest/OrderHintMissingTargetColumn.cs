// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class OrderHintMissingTargetColumn
    {
        private static readonly string sourceTable = "Customers";
        private static readonly string initialQueryTemplate = "create table {0} (CustomerID nvarchar(50), CompanyName nvarchar(50), ContactName nvarchar(50))";
        private static readonly string sourceQueryTemplate = "SELECT CustomerID, CompanyName, ContactName FROM {0}";

        public static void Test(string connStr, string dstTable)
        {
            string initialQuery = string.Format(initialQueryTemplate, dstTable);
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);

            using (SqlConnection dstConn = new SqlConnection(connStr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();
                Helpers.TryExecute(dstCmd, initialQuery);
                using (SqlConnection srcConn = new SqlConnection(connStr))
                using (SqlCommand srcCmd = new SqlCommand(sourceQuery, srcConn))
                {
                    srcConn.Open();
                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            try
                            {
                                bulkcopy.DestinationTableName = dstTable;
                                const string nonexistentColumn = "nonexistent column";
                                const string sourceColumn = "CustomerID";
                                const string destColumn = "ContactName";

                                // column does not exist in destination table
                                bulkcopy.ColumnOrderHints.Add(nonexistentColumn, SortOrder.Ascending);

                                string expectedErrorMsg = string.Format(
                                    SystemDataResourceManager.Instance.SQL_BulkLoadOrderHintInvalidColumn, nonexistentColumn);
                                DataTestUtility.AssertThrowsWrapper<InvalidOperationException>(
                                    () => bulkcopy.WriteToServer(reader),
                                    exceptionMessage: expectedErrorMsg);

                                // column does not exist in destination table because of user-defined mapping
                                bulkcopy.ColumnMappings.Add(sourceColumn, destColumn);
                                bulkcopy.ColumnOrderHints.RemoveAt(0);
                                bulkcopy.ColumnOrderHints.Add(sourceColumn, SortOrder.Ascending);
                                Assert.True(bulkcopy.ColumnOrderHints.Count == 1, "Error adding a column order hint");

                                expectedErrorMsg = string.Format(
                                    SystemDataResourceManager.Instance.SQL_BulkLoadOrderHintInvalidColumn, sourceColumn);
                                DataTestUtility.AssertThrowsWrapper<InvalidOperationException>(
                                    () => bulkcopy.WriteToServer(reader),
                                    exceptionMessage: expectedErrorMsg);
                            }
                            finally
                            {
                                DataTestUtility.DropTable(dstConn, dstTable);
                            }
                        }
                    }
                }
            }
        }
    }
}

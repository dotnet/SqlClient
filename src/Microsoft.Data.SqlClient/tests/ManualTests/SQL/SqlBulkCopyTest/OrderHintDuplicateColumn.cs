// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class OrderHintDuplicateColumn
    {
        private static readonly string sourceTable = "Customers";
        private static readonly string initialQueryTemplate = "create table {0} (CustomerID nvarchar(50), CompanyName nvarchar(50), ContactName nvarchar(50))";
        private static readonly string sourceQueryTemplate = "SELECT CustomerID, CompanyName, ContactName FROM {0}";
        private static readonly string getRowCountQueryTemplate = "SELECT COUNT(*) FROM {0}";

        public static void Test(string connStr, string dstTable)
        {
            string initialQuery = string.Format(initialQueryTemplate, dstTable);
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);
            string getRowCountQuery = string.Format(getRowCountQueryTemplate, sourceTable);

            using (SqlConnection dstConn = new SqlConnection(connStr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();
                Helpers.TryExecute(dstCmd, initialQuery);
                using (SqlConnection srcConn = new SqlConnection(connStr))
                using (SqlCommand srcCmd = new SqlCommand(getRowCountQuery, srcConn))
                {
                    srcConn.Open();
                    try
                    {
                        int nRowsInSource = Convert.ToInt32(srcCmd.ExecuteScalar());
                        srcCmd.CommandText = sourceQuery;
                        using (DbDataReader reader = srcCmd.ExecuteReader())
                        {
                            using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                            {
                                bulkcopy.DestinationTableName = dstTable;
                                const string destColumn = "CompanyName";
                                const string destColumn2 = "ContactName";
                                bulkcopy.ColumnOrderHints.Add(destColumn, SortOrder.Ascending);

                                string expectedErrorMsg = string.Format(
                                    SystemDataResourceManager.Instance.SQL_BulkLoadOrderHintDuplicateColumn, destColumn);
                                DataTestUtility.AssertThrowsWrapper<InvalidOperationException>(
                                    () => bulkcopy.ColumnOrderHints.Add(destColumn, SortOrder.Ascending),
                                    exceptionMessage: expectedErrorMsg);

                                bulkcopy.ColumnOrderHints.Add(destColumn2, SortOrder.Ascending);
                                Assert.Equal(2, bulkcopy.ColumnOrderHints.Count);

                                bulkcopy.WriteToServer(reader);
                                Helpers.VerifyResults(dstConn, dstTable, 3, nRowsInSource);
                            }
                        }
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

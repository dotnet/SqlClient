// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class OrderHintIdentityColumn
    {
        private static readonly string sourceTable = "Customers";
        private static readonly string initialQueryTemplate = "create table {0} (CustomerID int IDENTITY NOT NULL, CompanyName nvarchar(50), ContactName nvarchar(50))";
        private static readonly string sourceQueryTemplate = "SELECT CustomerId, CompanyName, ContactName FROM {0}";
        private static readonly string getRowCountQueryTemplate = "SELECT COUNT(*) FROM {0}";

        public static void Test(string connStr, string dstTable)
        {
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);
            string initialQuery = string.Format(initialQueryTemplate, dstTable);
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
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = dstTable;

                            // no mapping
                            using (DbDataReader reader = srcCmd.ExecuteReader())
                            {
                                bulkcopy.ColumnOrderHints.Add("CustomerID", SortOrder.Ascending);
                                bulkcopy.WriteToServer(reader);
                                Helpers.VerifyResults(dstConn, dstTable, 3, nRowsInSource);
                            }

                            // with mapping
                            using (DbDataReader reader = srcCmd.ExecuteReader())
                            {
                                bulkcopy.ColumnMappings.Add(0, 1);
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

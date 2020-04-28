// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class OrderHintDuplicateColumn
    {
        private static readonly string destinationTable = null;
        private static readonly string sourceTable = "Customers";
        private static readonly string initialQueryTemplate = "create table {0} (CustomerID nvarchar(50), CompanyName nvarchar(50), ContactName nvarchar(50))";
        private static readonly string sourceQueryTemplate = "SELECT CustomerID, CompanyName, ContactName FROM {0}";

        public static void Test(string srcConstr, string dstConstr, string dstTable)
        {
            dstTable = destinationTable != null ? destinationTable : dstTable;
            string initialQuery = string.Format(initialQueryTemplate, dstTable);
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);

            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();
                Helpers.TryExecute(dstCmd, initialQuery);
                using (SqlConnection srcConn = new SqlConnection(srcConstr))
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
                                string destColumn = "CompanyName";
                                bulkcopy.ColumnOrderHints.Add(destColumn, SortOrder.Ascending);
                                bulkcopy.ColumnOrderHints.Add(destColumn, SortOrder.Descending);

                                string expectedErrorMsg = string.Format(
                                    SystemDataResourceManager.Instance.SQL_BulkLoadOrderHintDuplicateColumn, destColumn);
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

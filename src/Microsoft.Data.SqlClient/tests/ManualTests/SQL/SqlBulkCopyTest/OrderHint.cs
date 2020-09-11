// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class OrderHint
    {
        private static readonly string sourceTable = "Customers";
        private static readonly string sourceTable2 = "Employees";
        private static readonly string initialQueryTemplate = "create table {0} (CustomerID nvarchar(50), CompanyName nvarchar(50), ContactName nvarchar(50)); CREATE CLUSTERED INDEX IX_Test_Table_Customer_ID ON {0} (CustomerID DESC)";
        private static readonly string initialQueryTemplate2 = "create table {0} (LastName nvarchar(50), FirstName nvarchar(50))";
        private static readonly string sourceQueryTemplate = "SELECT CustomerID, CompanyName, ContactName FROM {0}";
        private static readonly string sourceQueryTemplate2 = "SELECT LastName, FirstName FROM {0}";
        private static readonly string getRowCountQueryTemplate = "SELECT COUNT(*) FROM {0}";

        public static void Test(string connStr, string dstTable, string dstTable2)
        {
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);
            string sourceQuery2 = string.Format(sourceQueryTemplate2, sourceTable2);
            string initialQuery = string.Format(initialQueryTemplate, dstTable);
            string initialQuery2 = string.Format(initialQueryTemplate2, dstTable2);
            string getRowCountQuery = string.Format(getRowCountQueryTemplate, sourceTable);
            string getRowCountQuery2 = string.Format(getRowCountQueryTemplate, sourceTable2);

            using (SqlConnection dstConn = new SqlConnection(connStr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();
                Helpers.TryExecute(dstCmd, initialQuery);
                Helpers.TryExecute(dstCmd, initialQuery2);
                using (SqlConnection srcConn = new SqlConnection(connStr))
                using (SqlCommand srcCmd = new SqlCommand(getRowCountQuery, srcConn))
                {
                    srcConn.Open();
                    try
                    {
                        int nRowsInSource = Convert.ToInt32(srcCmd.ExecuteScalar());
                        srcCmd.CommandText = getRowCountQuery2;
                        int nRowsInSource2 = Convert.ToInt32(srcCmd.ExecuteScalar());
                        srcCmd.CommandText = sourceQuery;
                        using (SqlBulkCopy bulkCopy = new SqlBulkCopy(dstConn))
                        {
                            bulkCopy.DestinationTableName = dstTable;

                            // no hints
                            using (DbDataReader reader = srcCmd.ExecuteReader())
                            {
                                bulkCopy.WriteToServer(reader);
                            }

                            // hint for 1 of 3 columns
                            using (DbDataReader reader = srcCmd.ExecuteReader())
                            {
                                bulkCopy.ColumnOrderHints.Add("CustomerID", SortOrder.Ascending);
                                bulkCopy.WriteToServer(reader);
                            }

                            // hints for all 3 columns
                            // order of hints is not the same as column order in table
                            using (DbDataReader reader = srcCmd.ExecuteReader())
                            {
                                bulkCopy.ColumnOrderHints.Add("ContactName", SortOrder.Descending);
                                bulkCopy.ColumnOrderHints.Add("CompanyName", SortOrder.Ascending);
                                bulkCopy.WriteToServer(reader);
                            }

                            // add column mappings
                            using (DbDataReader reader = srcCmd.ExecuteReader())
                            {
                                bulkCopy.ColumnMappings.Add(0, 1);
                                bulkCopy.ColumnMappings.Add(1, 2);
                                bulkCopy.ColumnMappings.Add(2, 0);
                                bulkCopy.WriteToServer(reader);
                            }

                            // WriteToServer DataTable overload
                            using (SqlDataAdapter dataAdapter = new SqlDataAdapter(srcCmd))
                            {
                                DataTable dataTable = new DataTable();
                                dataAdapter.Fill(dataTable);
                                bulkCopy.WriteToServer(dataTable);
                            }

                            // WriteToServer DataRow[] overload
                            using (SqlDataAdapter dataAdapter = new SqlDataAdapter(srcCmd))
                            {
                                DataTable dataTable = new DataTable();
                                dataAdapter.Fill(dataTable);
                                bulkCopy.WriteToServer(dataTable.Select());
                            }

                            // WriteToServer DataTable, DataRowState overload
                            using (SqlDataAdapter dataAdapter = new SqlDataAdapter(srcCmd))
                            {
                                DataTable dataTable = new DataTable();
                                dataAdapter.Fill(dataTable);
                                DataRow[] x = dataTable.Select();
                                bulkCopy.WriteToServer(dataTable, DataRowState.Unchanged);
                            }
                        }

                        // add copy options
                        SqlBulkCopyOptions copyOptions =
                            SqlBulkCopyOptions.AllowEncryptedValueModifications |
                            SqlBulkCopyOptions.FireTriggers |
                            SqlBulkCopyOptions.KeepNulls;
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn, copyOptions, null))
                        {
                            bulkcopy.DestinationTableName = dstTable;
                            bulkcopy.ColumnOrderHints.Add("CustomerID", SortOrder.Ascending);
                            bulkcopy.ColumnOrderHints.Add("CompanyName", SortOrder.Ascending);
                            bulkcopy.ColumnOrderHints.Add("ContactName", SortOrder.Descending);
                            using (DbDataReader reader = srcCmd.ExecuteReader())
                            {
                                bulkcopy.WriteToServer(reader);
                            }

                            const int nWriteToServerCalls = 8;
                            Helpers.VerifyResults(dstConn, dstTable, 3, nRowsInSource * nWriteToServerCalls);

                            // different tables
                            srcCmd.CommandText = sourceQuery2;
                            using (DbDataReader reader = srcCmd.ExecuteReader())
                            {
                                bulkcopy.DestinationTableName = dstTable2;
                                bulkcopy.ColumnOrderHints.Clear();
                                bulkcopy.ColumnOrderHints.Add("LastName", SortOrder.Descending);
                                bulkcopy.WriteToServer(reader);
                                Helpers.VerifyResults(dstConn, dstTable2, 2, nRowsInSource2);
                            }
                        }
                    }
                    finally
                    {
                        DataTestUtility.DropTable(dstConn, dstTable);
                        DataTestUtility.DropTable(dstConn, dstTable2);
                    }
                }
            }
        }
    }
}

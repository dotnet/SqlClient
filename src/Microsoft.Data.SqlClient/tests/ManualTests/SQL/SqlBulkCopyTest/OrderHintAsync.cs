// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class OrderHintAsync
    {
        private static readonly string destinationTable = null;
        private static readonly string sourceTable = "Customers";
        private static readonly string initialQueryTemplate = "create table {0} (CustomerID nvarchar(50), CompanyName nvarchar(50), ContactName nvarchar(50))";
        private static readonly string sourceQueryTemplate = "SELECT CustomerId, CompanyName, ContactName FROM {0}";
        private static readonly string getRowCountQueryTemplate = "SELECT COUNT(*) FROM {0}";

        public static void Test(string srcConstr, string dstConstr, string dstTable)
        {
            Task t = TestAsync(srcConstr, dstConstr, dstTable);
            t.Wait();
            Assert.True(t.IsCompleted, "Task did not complete! Status: " + t.Status);
        }

        public static async Task TestAsync(string srcConstr, string dstConstr, string dstTable)
        {
            dstTable = destinationTable != null ? destinationTable : dstTable;
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);
            string initialQuery = string.Format(initialQueryTemplate, dstTable);
            string getRowCountQuery = string.Format(getRowCountQueryTemplate, sourceTable);

            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                await dstConn.OpenAsync();
                Helpers.TryExecute(dstCmd, initialQuery);
                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand srcCmd = new SqlCommand(getRowCountQuery, srcConn))
                {
                    await srcConn.OpenAsync();
                    try
                    {
                        int nRowsInSource = Convert.ToInt32(await srcCmd.ExecuteScalarAsync());
                        srcCmd.CommandText = sourceQuery;
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = dstTable;

                            // no hints
                            using (DbDataReader reader = await srcCmd.ExecuteReaderAsync())
                            {
                                await bulkcopy.WriteToServerAsync(reader);
                                Helpers.VerifyResults(dstConn, dstTable, 3, nRowsInSource);
                            }

                            // hint for 1 of 3 columns
                            using (DbDataReader reader = await srcCmd.ExecuteReaderAsync())
                            {
                                bulkcopy.ColumnOrderHints.Add("CustomerID", SortOrder.Ascending);
                                await bulkcopy.WriteToServerAsync(reader);
                                Helpers.VerifyResults(dstConn, dstTable, 3, nRowsInSource * 2);
                            }

                            // hints for all 3 columns
                            // order of hints is not the same as column order in table
                            using (DbDataReader reader = await srcCmd.ExecuteReaderAsync())
                            {
                                bulkcopy.ColumnOrderHints.Add("ContactName", SortOrder.Descending);
                                bulkcopy.ColumnOrderHints.Add("CompanyName", SortOrder.Ascending);
                                await bulkcopy.WriteToServerAsync(reader);
                                Helpers.VerifyResults(dstConn, dstTable, 3, nRowsInSource * 3);
                            }

                            // add column mappings
                            using (DbDataReader reader = await srcCmd.ExecuteReaderAsync())
                            {
                                bulkcopy.ColumnMappings.Add(0, 1);
                                bulkcopy.ColumnMappings.Add(1, 2);
                                bulkcopy.ColumnMappings.Add(2, 0);
                                await bulkcopy.WriteToServerAsync(reader);
                                Helpers.VerifyResults(dstConn, dstTable, 3, nRowsInSource * 4);
                            }
                        }

                        // add copy options
                        SqlBulkCopyOptions copyOptions = SqlBulkCopyOptions.AllowEncryptedValueModifications
                            | SqlBulkCopyOptions.FireTriggers
                            | SqlBulkCopyOptions.KeepNulls;
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn, copyOptions, null))
                        {
                            bulkcopy.DestinationTableName = dstTable;
                            bulkcopy.ColumnOrderHints.Add("CustomerID", SortOrder.Ascending);
                            bulkcopy.ColumnOrderHints.Add("CompanyName", SortOrder.Ascending);
                            bulkcopy.ColumnOrderHints.Add("ContactName", SortOrder.Descending);
                            using (DbDataReader reader = await srcCmd.ExecuteReaderAsync())
                            {
                                await bulkcopy.WriteToServerAsync(reader);
                                Helpers.VerifyResults(dstConn, dstTable, 3, nRowsInSource * 5);
                            }
                        }
                    }
                    finally
                    {
                        Helpers.TryExecute(dstCmd, "drop table " + dstTable);
                    }
                }
            }
        }
    }
}

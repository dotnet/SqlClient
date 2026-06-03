// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    public class OrderHintTransaction
    {
        private static readonly string sourceTable = "Customers";
        private static readonly string initialQueryTemplate = "create table {0} (CustomerID nvarchar(50), CompanyName nvarchar(50), ContactName nvarchar(50))";
        private static readonly string sourceQueryTemplate = "SELECT CustomerID, CompanyName, ContactName FROM {0}";
        // Synapse: 111212;Operation cannot be performed within a transaction.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void Test()
        {
            string connStr = DataTestUtility.TCPConnectionString;
            string dstTable = DataTestUtility.GetShortName("SqlBulkCopyTest_OrderHintTransaction", false);
            string initialQuery = string.Format(initialQueryTemplate, dstTable);
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);

            using (SqlConnection dstConn = new SqlConnection(connStr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();
                SqlTransaction txn = dstConn.BeginTransaction();
                dstCmd.Transaction = txn;
                Helpers.TryExecute(dstCmd, initialQuery);

                using (SqlConnection srcConn = new SqlConnection(connStr))
                using (SqlCommand srcCmd = new SqlCommand(sourceQuery, srcConn))
                {
                    srcConn.Open();
                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(
                            dstConn, SqlBulkCopyOptions.CheckConstraints, txn))
                        {
                            try
                            {
                                bulkcopy.DestinationTableName = dstTable;
                                bulkcopy.ColumnMappings.Add(0, 2);
                                bulkcopy.ColumnMappings.Add(2, 0);
                                bulkcopy.ColumnOrderHints.Add("CustomerID", SortOrder.Ascending);
                                bulkcopy.ColumnOrderHints.Add("ContactName", SortOrder.Descending);
                                bulkcopy.WriteToServer(reader);
                            }
                            finally
                            {
                                txn.Rollback();
                            }
                        }
                    }
                }
            }
        }
    }
}

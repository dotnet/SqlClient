// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class Transaction
    {
        [ConditionalFact(typeof(SqlBulkCopyTest), nameof(SqlBulkCopyTest.AreConnectionStringsSetup), nameof(SqlBulkCopyTest.IsNotAzureServer))]
        public void Test()
        {
            string srcConstr = SqlBulkCopyTest.ConnectionString;
            string dstConstr = SqlBulkCopyTest.ConnectionString;
            string dstTable = SqlBulkCopyTest.AddGuid("SqlBulkCopyTest_Transaction0");
            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();

                try
                {
                    Helpers.TryExecute(dstCmd, "create table " + dstTable + " (col1 int, col2 nvarchar(20), col3 nvarchar(10))");

                    using (SqlConnection srcConn = new SqlConnection(srcConstr))
                    using (SqlCommand srcCmd = new SqlCommand("select top 5 EmployeeID, LastName, FirstName from employees", srcConn))
                    {
                        srcConn.Open();

                        using (DbDataReader reader = srcCmd.ExecuteReader())
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn, SqlBulkCopyOptions.UseInternalTransaction, null))
                        {
                            bulkcopy.DestinationTableName = dstTable;
                            SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;

                            SqlTransaction myTrans = dstConn.BeginTransaction();
                            try
                            {
                                DataTestUtility.AssertThrowsWrapper<InvalidOperationException>(() => bulkcopy.WriteToServer(reader));
                            }
                            finally
                            {
                                myTrans.Rollback();
                            }
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

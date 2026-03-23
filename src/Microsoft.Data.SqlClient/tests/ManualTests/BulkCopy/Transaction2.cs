// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    public class Transaction2
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string srcConstr = SqlBulkCopyTest.ConnectionString;
            string dstConstr = SqlBulkCopyTest.ConnectionString;
            string dstTable = SqlBulkCopyTest.AddGuid("SqlBulkCopyTest_Transaction2");
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
                        {
                            SqlTransaction myTrans = dstConn.BeginTransaction();
                            using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn, SqlBulkCopyOptions.Default, myTrans))
                            {
                                bulkcopy.DestinationTableName = dstTable;
                                SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;

                                try
                                {
                                    bulkcopy.WriteToServer(reader);
                                    SqlCommand myCmd = dstConn.CreateCommand();
                                    myCmd.CommandText = "select * from " + dstTable;
                                    myCmd.Transaction = myTrans;
                                    using (DbDataReader reader1 = myCmd.ExecuteReader())
                                    {
                                        Assert.True(reader1.HasRows, "Expected reader to have rows.");
                                    }
                                }
                                finally
                                {
                                    myTrans.Rollback();
                                }
                            }

                            Helpers.CheckTableRows(dstConn, dstTable, false);
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

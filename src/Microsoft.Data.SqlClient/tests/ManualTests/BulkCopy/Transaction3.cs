// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    public class Transaction3
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string srcConstr = DataTestUtility.TCPConnectionString;
            string dstConstr = DataTestUtility.TCPConnectionString;
            string dstTable = DataTestUtility.GetShortName("SqlBulkCopyTest_Transaction3", false);
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
                        using (SqlConnection conn3 = new SqlConnection(srcConstr))
                        {
                            conn3.Open();
                            // Start a local transaction on the wrong connection.
                            SqlTransaction myTrans = conn3.BeginTransaction();
                            using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn, SqlBulkCopyOptions.Default, myTrans))
                            {
                                SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;
                                bulkcopy.DestinationTableName = dstTable;

                                string exceptionMsg = SystemDataResourceManager.Instance.ADP_TransactionConnectionMismatch;
                                DataTestUtility.AssertThrowsWrapper<InvalidOperationException>(() => bulkcopy.WriteToServer(reader), exceptionMessage: exceptionMsg);

                                SqlCommand myCmd = dstConn.CreateCommand();
                                myCmd.CommandText = "select * from " + dstTable;
                                myCmd.Transaction = myTrans;

                                DataTestUtility.AssertThrowsWrapper<InvalidOperationException>(() => myCmd.ExecuteReader(), exceptionMessage: exceptionMsg);
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

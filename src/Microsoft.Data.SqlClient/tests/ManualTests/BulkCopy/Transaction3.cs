// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    [Trait("Set", "2")]
    public class Transaction3
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string srcConstr = DataTestUtility.TCPConnectionString;
            string dstConstr = DataTestUtility.TCPConnectionString;
            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();

                using Table dstTable = new Table(dstConn, "SqlBulkCopyTest_Transaction3", "(col1 int, col2 nvarchar(20), col3 nvarchar(10))");

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
                            bulkcopy.DestinationTableName = dstTable.Name;

                            string exceptionMsg = SystemDataResourceManager.Instance.ADP_TransactionConnectionMismatch;
                            DataTestUtility.AssertThrows<InvalidOperationException>(() => bulkcopy.WriteToServer(reader), exceptionMessage: exceptionMsg);

                            SqlCommand myCmd = dstConn.CreateCommand();
                            myCmd.CommandText = "select * from " + dstTable.Name;
                            myCmd.Transaction = myTrans;

                            DataTestUtility.AssertThrows<InvalidOperationException>(() => myCmd.ExecuteReader(), exceptionMessage: exceptionMsg);
                        }
                    }
                }
            }
        }
    }
}

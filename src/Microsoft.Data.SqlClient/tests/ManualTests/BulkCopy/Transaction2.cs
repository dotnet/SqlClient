// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    [Trait("Set", "2")]
    public class Transaction2
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string srcConstr = DataTestUtility.TCPConnectionString;
            string dstConstr = DataTestUtility.TCPConnectionString;
            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            {
                dstConn.Open();

                using Table dstTable = new Table(dstConn, "SqlBulkCopyTest_Transaction2", "(col1 int, col2 nvarchar(20), col3 nvarchar(10))");

                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand srcCmd = new SqlCommand("select top 5 EmployeeID, LastName, FirstName from employees", srcConn))
                {
                    srcConn.Open();

                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {
                        SqlTransaction myTrans = dstConn.BeginTransaction();
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn, SqlBulkCopyOptions.Default, myTrans))
                        {
                            bulkcopy.DestinationTableName = dstTable.Name;
                            SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;

                            try
                            {
                                bulkcopy.WriteToServer(reader);
                                SqlCommand myCmd = dstConn.CreateCommand();
                                myCmd.CommandText = "select * from " + dstTable.Name;
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

                        Helpers.CheckTableRows(dstConn, dstTable.Name, false);
                    }
                }
            }
        }
    }
}

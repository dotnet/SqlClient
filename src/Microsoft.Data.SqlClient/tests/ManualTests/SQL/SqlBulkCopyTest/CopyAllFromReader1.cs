// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class CopyAllFromReader1
    {
        public static void Test(string srcConstr, string dstConstr, string dstTable)
        {
            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();
                try
                {
                    Helpers.TryExecute(dstCmd, "create table " + dstTable + " (col1 int, col2 nvarchar(20), col3 nvarchar(10))");

                    using (SqlConnection srcConn = new SqlConnection(srcConstr))
                    using (SqlCommand srcCmd = new SqlCommand("select top 5 * from employees", srcConn))
                    {
                        srcConn.Open();
                        using (DbDataReader reader = srcCmd.ExecuteReader())
                        {
                            using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                            {
                                bulkcopy.DestinationTableName = dstTable;
                                SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;

                                ColumnMappings.Add("EmployeeID", "col1");
                                ColumnMappings.Add("LastName", "col2");
                                ColumnMappings.Add("FirstName", "col3");

                                bulkcopy.WriteToServer(reader);
                                
                                DataTestUtility.AssertEqualsWithDescription(bulkcopy.RowsCopied, 5, "Unexpected number of rows.");
                                DataTestUtility.AssertEqualsWithDescription(bulkcopy.RowsCopied64, (long)5, "Unexpected number of rows.");
                            }
                            Helpers.VerifyResults(dstConn, dstTable, 3, 5);
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

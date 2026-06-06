// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    public class CopyMultipleReaders
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string srcConstr = DataTestUtility.TCPConnectionString;
            string dstConstr = DataTestUtility.TCPConnectionString;
            string dstTable = DataTestUtility.GetShortName("SqlBulkCopyTest_CopyMultipleReaders", false);
            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();

                try
                {
                    Helpers.TryExecute(dstCmd, "create table " + dstTable + " (col1 int, col2 nvarchar(20), col3 nvarchar(10))");

                    using (SqlConnection srcConn = new SqlConnection(srcConstr))
                    using (SqlCommand srcCmd = srcConn.CreateCommand())
                    {
                        srcConn.Open();

                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = dstTable;
                            srcCmd.CommandText = "select EmployeeID, LastName from employees where LastName < 'E%'";
                            using (DbDataReader reader = srcCmd.ExecuteReader())
                            {
                                bulkcopy.WriteToServer(reader);
                            }
                            DataTestUtility.AssertEqualsWithDescription(0, bulkcopy.ColumnMappings.Count, "Unexpected ColumnMappings count.");

                            srcCmd.CommandText = "select EmployeeID, LastName, FirstName from employees where LastName > 'D%'";
                            using (DbDataReader reader = srcCmd.ExecuteReader())
                            {
                                bulkcopy.WriteToServer(reader);
                            }
                            DataTestUtility.AssertEqualsWithDescription(0, bulkcopy.ColumnMappings.Count, "Unexpected ColumnMappings count.");

                            srcCmd.CommandText = "select EmployeeID, FirstName from employees where LastName < 'E%'";
                            using (DbDataReader reader = srcCmd.ExecuteReader())
                            {
                                bulkcopy.WriteToServer(reader);
                            }
                            DataTestUtility.AssertEqualsWithDescription(0, bulkcopy.ColumnMappings.Count, "Unexpected ColumnMappings count.");

                            Helpers.VerifyResults(dstConn, dstTable, 3, 15);
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

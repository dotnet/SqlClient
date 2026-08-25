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
    public class CopyMultipleReaders
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

                using Table dstTable = new Table(dstConn, "SqlBulkCopyTest_CopyMultipleReaders", "(col1 int, col2 nvarchar(20), col3 nvarchar(10))");

                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand srcCmd = srcConn.CreateCommand())
                {
                    srcConn.Open();

                    using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                    {
                        bulkcopy.DestinationTableName = dstTable.Name;
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

                        Helpers.VerifyResults(dstConn, dstTable.Name, 3, 15);
                    }
                }
            }
        }
    }
}

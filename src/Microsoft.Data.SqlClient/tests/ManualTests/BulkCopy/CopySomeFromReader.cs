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
    public class CopySomeFromReader
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

                using Table dstTable = new Table(dstConn, "SqlBulkCopyTest_CopySomeFromReader", "(col1 int, col2 nvarchar(20), col3 nvarchar(10), col4 datetime)");

                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand srcCmd = new SqlCommand("select * from employees", srcConn))
                {
                    srcConn.Open();

                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = dstTable.Name;
                            bulkcopy.BatchSize = 6;

                            SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;

                            ColumnMappings.Add("EmployeeID", "col1");
                            ColumnMappings.Add("BirthDate", "col4");
                            ColumnMappings.Add("FirstName", "col2");
                            ColumnMappings.Add("LastName", "col3");

                            bulkcopy.WriteToServer(reader);
                        }
                        Helpers.VerifyResults(dstConn, dstTable.Name, 4, 9);
                    }
                }
            }
        }
    }
}

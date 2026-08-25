// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    [Trait("Set", "2")]
    public class CopyAllFromReaderConnectionClosedAsync
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void Test()
        {
            string srcConstr = DataTestUtility.TCPConnectionString;
            string dstConstr = DataTestUtility.TCPConnectionString;
            Task t = TestAsync(srcConstr, dstConstr);
            DataTestUtility.AssertThrowsInner<AggregateException, InvalidOperationException>(() => t.Wait());
            Assert.True(t.IsCompleted, "Task did not complete! Status: " + t.Status);
        }

        private static async Task TestAsync(string srcConstr, string dstConstr)
        {
            string tableDefinition = "(col1 int, col2 nvarchar(20), col3 nvarchar(10))";
            string sourceQueryTemplate = "select top 5 EmployeeID, LastName, FirstName from {0}";
            string sourceTable = "employees";

            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);

            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();

                using Table dstTable = new(dstConn, "SqlBulkCopyTest_AsyncTest6", tableDefinition);

                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand srcCmd = new SqlCommand(sourceQuery, srcConn))
                {
                    srcConn.Open();

                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                    {
                        bulkcopy.DestinationTableName = dstTable.Name;
                        dstConn.Close();
                        await bulkcopy.WriteToServerAsync(reader);
                    }
                }
            }
        }
    }
}

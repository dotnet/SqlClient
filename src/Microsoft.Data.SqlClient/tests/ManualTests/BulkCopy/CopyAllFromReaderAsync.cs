// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    [Trait("Set", "2")]
    public class CopyAllFromReaderAsync
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string srcConstr = DataTestUtility.TCPConnectionString;
            string dstConstr = DataTestUtility.TCPConnectionString;
            // Use this semaphore to ensure that results are written to the log in the correct order
            SemaphoreSlim outputSemaphore = new SemaphoreSlim(0, 1);

            Task t = TestAsync(srcConstr, dstConstr, outputSemaphore);
            outputSemaphore.Release();
            t.Wait();
            Assert.True(t.IsCompleted, "Task did not complete! Status: " + t.Status);
        }

        private static async Task TestAsync(string srcConstr, string dstConstr, SemaphoreSlim outputSemaphore)
        {
            string sourceQueryTemplate = "select top 5 EmployeeID, LastName, FirstName from {0}";
            string srcTable = "employees";

            string sourceQuery = string.Format(sourceQueryTemplate, srcTable);

            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();
                using Table dstTable = new Table(dstConn, "SqlBulkCopyTest_AsyncTest1", "(col1 int, col2 nvarchar(20), col3 nvarchar(10))");

                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand srcCmd = new SqlCommand(sourceQuery, srcConn))
                {
                    srcConn.Open();
                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = dstTable.Name;

                            await bulkcopy.WriteToServerAsync(reader);
                            await outputSemaphore.WaitAsync();

                            DataTestUtility.AssertEqualsWithDescription(bulkcopy.RowsCopied, 5, "Unexpected number of rows.");
                            DataTestUtility.AssertEqualsWithDescription(bulkcopy.RowsCopied64, (long)5, "Unexpected number of rows.");
                        }
                        Helpers.VerifyResults(dstConn, dstTable.Name, 3, 5);
                    }
                }
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    [Trait("Set", "2")]
    public class CopySomeFromRowArrayAsync
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
            DataSet dataset;
            SqlDataAdapter adapter;
            DataTable datatable;
            DataRow[] rows;

            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            {
                dstConn.Open();

                using Table dstTable = new Table(dstConn, "SqlBulkCopyTest_AsyncTest2", "(col1 int, col2 nvarchar(20), col3 nvarchar(10), col4 datetime)");

                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand srcCmd = new SqlCommand("select * from employees", srcConn))
                {
                    srcConn.Open();

                    dataset = new DataSet("MyDataSet");
                    adapter = new SqlDataAdapter(srcCmd);
                    adapter.Fill(dataset);
                    datatable = dataset.Tables[0];
                    rows = new DataRow[datatable.Rows.Count];
                    for (int i = 0; i < rows.Length; i++)
                    {
                        rows[i] = datatable.Rows[i];
                    }

                    using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                    {
                        bulkcopy.DestinationTableName = dstTable.Name;
                        bulkcopy.BatchSize = 4;

                        SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;

                        ColumnMappings.Add(0, "col1");
                        ColumnMappings.Add(2, "col3");

                        await bulkcopy.WriteToServerAsync(rows);
                        bulkcopy.Close();
                    }
                    await outputSemaphore.WaitAsync();
                    Helpers.VerifyResults(dstConn, dstTable.Name, 4, 9);
                }
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.IO;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    [Trait("Set", "2")]
    public class CopyAllFromReaderConnectionClosedOnEventAsync
    {
        [Trait("Category", "flaky")] // Hangs and crashes on occasion
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string srcConstr = DataTestUtility.TCPConnectionString;
            string dstConstr = DataTestUtility.TCPConnectionString;
#if DEBUG
            string tableDefinition = "(col1 int, col2 nvarchar(20), col3 nvarchar(10), col4 varchar(8000))";
            string sourceQuery = "select EmployeeID, LastName, FirstName, REPLICATE('a', 8000) from employees";

            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            {
                dstConn.Open();


                using Table dstTable = new(dstConn, "SqlBulkCopyTest_AsyncTest7", tableDefinition);

                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand srcCmd = new SqlCommand(sourceQuery, srcConn))
                {
                    srcConn.Open();

                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = dstTable.Name;

                            // Close the bulk copy's connection when it notifies us
                            bulkcopy.NotifyAfter = 1;
                            bulkcopy.SqlRowsCopied += (sender, e) =>
                            {
                                dstConn.Close();
                            };

                            using (AsyncDebugScope debugScope = new AsyncDebugScope())
                            {
                                // Force all writes to pend, this will guarantee that we will go through the correct code path
                                debugScope.ForceAsyncWriteDelay = 1;

                                // Check that the copying fails
                                string message = string.Format(SystemDataResourceManager.Instance.ADP_OpenConnectionRequired, "WriteToServer", SystemDataResourceManager.Instance.ADP_ConnectionStateMsg_Closed);
                                DataTestUtility.AssertThrowsInnerWithAlternate<AggregateException, InvalidOperationException, IOException>(() => bulkcopy.WriteToServerAsync(reader).Wait(5000), innerExceptionMessage: message);
                            }
                        }
                    }
                }
            }
#endif
        }
    }
}

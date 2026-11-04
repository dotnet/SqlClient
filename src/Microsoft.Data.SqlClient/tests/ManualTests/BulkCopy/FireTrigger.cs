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
    public class FireTrigger
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string srcConstr = DataTestUtility.TCPConnectionString;
            string dstConstr = DataTestUtility.TCPConnectionString;
            string sourceTable = "employees";
            string sourceQueryTemplate = "select top 5 EmployeeID, LastName, FirstName from {0}";
            string sourceQuery = string.Format(sourceQueryTemplate, sourceTable);

            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();

                // Dropping a table also drops the triggers defined on it, so the trigger below
                //   needs no separate cleanup. dstTable is declared last so that it - and its
                //   trigger - are dropped before the table the trigger writes into.
                using Table dstTable1 = new(dstConn, "SqlBulkCopyTest_FireTrigger_1", "(col1 int)");
                using Table dstTable = new(dstConn, "SqlBulkCopyTest_FireTrigger", "(col1 int, col2 nvarchar(20), col3 nvarchar(10))");

                Helpers.TryExecute(dstCmd,
                    "create trigger " + DataTestUtility.GetShortName("SqlBulkCopyTest_FireTrigger_2", false) +
                    " on " + dstTable.Name + " for INSERT as insert into " + dstTable1.Name + " values (333)");

                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand srcCmd = new SqlCommand(sourceQuery, srcConn))
                {
                    srcConn.Open();

                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {
                        SqlBulkCopyOptions option = SqlBulkCopyOptions.FireTriggers;

                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn, option, null))
                        {
                            bulkcopy.DestinationTableName = dstTable.Name;
                            bulkcopy.WriteToServer(reader);
                        }
                    }

                    dstCmd.CommandText = "select top 2 * from " + dstTable1.Name;
                    using (DbDataReader reader2 = dstCmd.ExecuteReader())
                    {
                        Assert.True(reader2.Read(), "Failed to read!");

                        Assert.True(reader2[0] is int, "Unexpected Field(0) type: " + reader2[0].GetType());

                        Assert.True((int)(reader2[0]) == 333, "Unexpected Field(0) value: " + reader2[0]);
                    }
                }
            }
        }
    }
}

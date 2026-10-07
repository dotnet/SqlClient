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
    public class DestinationTableNameWithSpecialChar
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void Test()
        {
            string constr = DataTestUtility.TCPConnectionString;
            string dstTable = DataTestUtility.GetShortName("SqlBulkCopyTest_DestinationTableNameWithSpecialChar", false);
            string srctable = "[" + dstTable + "src]";
            string dsttable = "@" + dstTable;       // a tablename that cannot be created without brackets (e.g., @sometablename)
            string[] dsttablecombo =
            {
                dsttable,                           // @sometablename
                "[" + dsttable + "]",               // [@sometablename]
                "dbo." + dsttable,                  // dbo.@sometablename
                "[dbo]." + "[" + dsttable + "]",    // [dbo].[@sometablename]
            };

            using (SqlConnection dstConn = new SqlConnection(constr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();

                using Table srcTableObject = Table.WithName(dstConn, srctable, "([col1] int)");
                using Table dstTableObject = Table.WithName(dstConn, "[" + dsttable + "]", "([col1] int)");

                Helpers.TryExecute(dstCmd, "insert into " + srctable + " values (33)");

                using (SqlConnection srcConn = new SqlConnection(constr))
                using (SqlCommand srcCmd = new SqlCommand(string.Format("select * from {0} ", srctable), srcConn))
                {
                    srcConn.Open();

                    int expRows = 1;
                    foreach (string dsttablename in dsttablecombo)
                    {
                        using (DbDataReader reader = srcCmd.ExecuteReader())
                        {
                            using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                            {
                                bulkcopy.DestinationTableName = dsttablename;
                                bulkcopy.WriteToServer(reader);
                            }
                            Helpers.VerifyResults(dstConn, "[" + dsttable + "]", 1, expRows);
                        }
                        expRows++;
                    }
                }
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
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

            string[] epilogue =
            {
                "create table " + srctable + "([col1] int)",
                "insert into " + srctable + " values (33)",
                "create table [" + dsttable + "]([col1] int)",
            };

            string[] prologue =
            {
                "drop table " + srctable,
                "drop table [" + dsttable + "]",
            };

            using (SqlConnection dstConn = new SqlConnection(constr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();
                try
                {
                    Helpers.ProcessCommandBatch(typeof(SqlConnection), constr, epilogue);

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
                finally
                {
                    Helpers.ProcessCommandBatch(typeof(SqlConnection), constr, prologue);
                }
            }
        }
    }
}

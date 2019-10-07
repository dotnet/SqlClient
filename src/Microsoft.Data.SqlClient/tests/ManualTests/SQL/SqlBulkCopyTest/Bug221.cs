// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data.Common;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class Bug221
    {
        public static void Test(string constr, string dstTable)
        {
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
                "create table [" + dstTable + "]([col1] int)",
            };

            string[] prologue =
            {
                "drop table " + srctable,
                "drop table [" + dstTable + "]",
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
                        using (DbDataReader reader = srcCmd.ExecuteReader())
                        {
                            foreach (string dsttablename in dsttablecombo)
                            {
                                using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                                {
                                    bulkcopy.DestinationTableName = dsttablename;
                                    bulkcopy.WriteToServer(reader);
                                }
                                Helpers.VerifyResults(dstConn, dstTable, expRows, 1);
                                expRows++;
                            }
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

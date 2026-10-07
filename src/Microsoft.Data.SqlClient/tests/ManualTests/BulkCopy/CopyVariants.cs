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
    public class CopyVariants
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string constr = DataTestUtility.TCPConnectionString;

            using (SqlConnection dstConn = new SqlConnection(constr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();

                using Table srcTable = new(dstConn, "SqlBulkCopyTest_Variants_src", "(col_1 int primary key, col_2 sql_variant)");
                using Table dstTable = new(dstConn, "SqlBulkCopyTest_Variants_dst", "(col_1 int primary key, col_2 sql_variant)");

                string[] prologue =
                {
                    "insert into " + srcTable.Name + " values (0, null)",
                    "insert into " + srcTable.Name + " values (1, convert(int, 0))",
                    "insert into " + srcTable.Name + " values (2, convert(smallint, -32768))",
                    "insert into " + srcTable.Name + " values (3, convert(real, 2.2))",
                    "insert into " + srcTable.Name + " values (4, convert(float, -3303.33303))",
                    "insert into " + srcTable.Name + " values (5, convert(decimal(28,4), 44404.4404))",
                    "insert into " + srcTable.Name + " values (6, convert(money, $555505.5505) )",
                    "insert into " + srcTable.Name + " values (7, convert(smallmoney, $-6.6606) )",
                    "insert into " + srcTable.Name + " values (8, convert(bit, 1) )",
                    "insert into " + srcTable.Name + " values (9, convert(tinyint, 8) )",
                    "insert into " + srcTable.Name + " values (10, convert(uniqueidentifier, '00000000-0000-0000-0000-000000000009') )",
                    "insert into " + srcTable.Name + " values (11, convert(varbinary(756), 0xAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA0A) )",
                    "insert into " + srcTable.Name + " values (12, convert(varchar(756), '111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111101') )",
                    "insert into " + srcTable.Name + " values (13, convert(nvarchar(756), N'???a???????????????????????????????üböuaäZßABCÄboÜOUÖvrhÃã??z?????????????????z?????????A?????a???????????????????????????????üböuaäZßABCÄboÜOUÖvrhÃã??z?????????????????z?????????A?????a???????????????????????????????üböuaäZßABCÄboÜOUÖvrhÃã??z?????????????????z?????????A?????a???????????????????????????????üböuaäZßABCÄboÜOUÖvrhÃã??z?????????????????z?????????A?????a?????') )",
                    "insert into " + srcTable.Name + " values (14, convert(datetime, {ts '2003-01-11 12:54:01.133'}) )",
                    "insert into " + srcTable.Name + " values (15, convert(bigint, 444444444444404) )",
                    "insert into " + srcTable.Name + " values (16, convert(int, -555505) )",
                    "insert into " + srcTable.Name + " values (17, convert(smallint, 16) )",
                    "insert into " + srcTable.Name + " values (18, convert(real, 777707.7) )",
                    "insert into " + srcTable.Name + " values (19, convert(float, -888888808.88018) )",
                    "insert into " + srcTable.Name + " values (20, convert(decimal(28,4), 99999999999999999909.9019) )",

                };

                foreach (string cmdtext in prologue)
                {
                    Helpers.TryExecute(dstCmd, cmdtext);
                }
                using (SqlConnection srcConn = new SqlConnection(constr))
                using (SqlCommand srcCmd = new SqlCommand("select * from " + srcTable.Name, srcConn))
                {
                    srcConn.Open();

                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = dstTable.Name;
                            bulkcopy.WriteToServer(reader);
                        }
                        Helpers.VerifyResults(dstConn, dstTable.Name, 2, 21);
                    }
                }
            }
        }
    }
}

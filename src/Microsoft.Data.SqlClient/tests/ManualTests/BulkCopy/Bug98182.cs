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
    public class Bug98182
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string constr = DataTestUtility.TCPConnectionString;
            string dstTable = DataTestUtility.GetShortName("@SqlBulkCopyTest_Bug98182 ", false);
            string srctable = "[" + dstTable + " src]";
            dstTable = "[" + dstTable + "]";

            using (SqlConnection dstConn = new SqlConnection(constr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();

                using Table srcTableObject = Table.WithName(dstConn, srctable, "([col 1] int primary key, [col 2] text)");
                using Table dstTableObject = Table.WithName(dstConn, dstTable, "([col 1] int primary key, [col 2] text)");

                Helpers.TryExecute(dstCmd, "insert into " + srctable + " values (33, 'Michael')");

                using (SqlConnection srcConn = new SqlConnection(constr))
                using (SqlCommand srcCmd = new SqlCommand(string.Format("select * from {0} ", srctable), srcConn))
                {
                    srcConn.Open();

                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = dstTable;

                            SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;
                            ColumnMappings.Add("[col 1]", "col 1");
                            ColumnMappings.Add("col 2", "[col 2]");

                            bulkcopy.WriteToServer(reader);

                            DataTestUtility.AssertEqualsWithDescription(bulkcopy.RowsCopied, 1, "Unexpected number of rows.");
                        }
                        Helpers.VerifyResults(dstConn, dstTable, 2, 1);
                    }
                }
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    [Trait("Set", "2")]
    public class SpecialCharacterNames
    {
        private static string EscapeIdentifier(string name)
        {
            return "[" + name.Replace("]", "]]") + "]";
        }
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string srcConstr = DataTestUtility.TCPConnectionString;
            string dstConstr = DataTestUtility.TCPConnectionString;
            string dstPrefix = DataTestUtility.GetShortName("@SqlBulkCopyTest_SpecialCharacterNames", false);
            // create schema and table names with special characters, with ] character escaped.
            string dstschema = dstPrefix + "_Schema'-]['']";
            dstschema = EscapeIdentifier(dstschema);

            string dstTable = dstPrefix + "_Table'-]['']";
            dstTable = EscapeIdentifier(dstTable);

            using (SqlConnection dstConn = new SqlConnection(dstConstr))
            {
                dstConn.Open();

                // The table is not currently created inside the schema, but it is declared second so
                // that it is dropped first regardless: a schema cannot be dropped while it still
                // contains objects.
                using Schema schema = Schema.WithName(dstConn, dstschema);
                using Table table = Table.WithName(dstConn, dstTable, "(orderid int, customerid nchar(5))");

                using (SqlConnection srcConn = new SqlConnection(srcConstr))
                using (SqlCommand srcCmd = new SqlCommand("select top 2 orderid, customerid from orders", srcConn))
                {
                    srcConn.Open();

                    using (SqlDataReader srcreader = srcCmd.ExecuteReader())
                    {
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn))
                        {
                            bulkcopy.DestinationTableName = dstTable;

                            bulkcopy.WriteToServer(srcreader);
                        }
                    }
                    Helpers.VerifyResults(dstConn, dstTable, 2, 2);
                }
            }
        }
    }
}

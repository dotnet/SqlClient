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
    public class CheckConstraints
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string constr = DataTestUtility.TCPConnectionString;
            using (SqlConnection dstConn = new SqlConnection(constr))
            using (SqlCommand dstCmd = dstConn.CreateCommand())
            {
                dstConn.Open();

                using Table srctable = new(dstConn, "SqlBulkCopyTest_Extensionsrc", "(col1 int , col2 int, col3 text)");
                using Table dstTable = new(dstConn, "SqlBulkCopyTest_Extensiondst", "(col1 int primary key, col2 int CHECK (col2 < 500), col3 text)");

                Helpers.TryExecute(dstCmd, "insert into " + srctable.Name + " values (33, 498, 'Michael')");
                Helpers.TryExecute(dstCmd, "insert into " + srctable.Name + " values (34, 499, 'Astrid')");
                Helpers.TryExecute(dstCmd, "insert into " + srctable.Name + " values (65, 500, 'alles Käse')");

                using (SqlConnection srcConn = new SqlConnection(constr))
                using (SqlCommand srcCmd = new SqlCommand("select * from " + srctable.Name, srcConn))
                {
                    srcConn.Open();
                    using (DbDataReader reader = srcCmd.ExecuteReader())
                    {
                        try
                        {
                            using (SqlBulkCopy bulkcopy = new SqlBulkCopy(dstConn, SqlBulkCopyOptions.CheckConstraints, null))
                            {
                                bulkcopy.DestinationTableName = dstTable.Name;
                                SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;

                                ColumnMappings.Add("col1", "col1");
                                ColumnMappings.Add("col2", "col2");
                                ColumnMappings.Add("col3", "col3");
                                bulkcopy.WriteToServer(reader);
                            }
                        }
                        catch (SqlException sqlEx)
                        {
                            // Error 547 == The %ls statement conflicted with the %ls constraint "%.*ls".
                            DataTestUtility.AssertEqualsWithDescription(547, sqlEx.Number, "Unexpected error number.");
                        }
                    }
                }
            }
        }
    }
}

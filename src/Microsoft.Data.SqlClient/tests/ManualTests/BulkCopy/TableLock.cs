// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    [Trait("Set", "2")]
    public class TableLock
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void Test()
        {
            string srcconstr = DataTestUtility.TCPConnectionString;
            string dstconstr = DataTestUtility.TCPConnectionString;
            string srctable = DataTestUtility.GetShortName("SqlBulkCopyTest_TableLock0", false);
            string dsttable = DataTestUtility.GetShortName("SqlBulkCopyTest_TableLock1", false);
            using SqlConnection destConn = new(dstconstr);
            destConn.Open();

            using SqlCommand dstcmd = destConn.CreateCommand();

            // NOTE: Setup runs inside the try so that a failure part way through (for example while
            //   creating the destination table) still drops the objects created before it. The table
            //   names embed a GUID, so anything left behind stays in the shared test database forever.
            try
            {
                Helpers.TryExecute(dstcmd, "create table " + srctable + " (col1 int, col2 text, col3 text)");
                Helpers.TryExecute(dstcmd, "insert into " + srctable + "(col1, col3) values (1, 'Michael')");
                Helpers.TryExecute(dstcmd, "insert into " + srctable + "(col1, col2, col3) values (2, 'Quark', 'Astrid')");
                Helpers.TryExecute(dstcmd, "insert into " + srctable + "(col1, col2) values (66, 'K�se');");

                Helpers.TryExecute(dstcmd, "create table " + dsttable + " (col1 int identity(1,1), col2 text default 'Jogurt', col3 text)");

                using SqlConnection sourceConn = new(srcconstr);
                sourceConn.Open();

                using SqlCommand srccmd = new SqlCommand("select * from " + srctable, sourceConn);
                using IDataReader reader = srccmd.ExecuteReader();

                using SqlBulkCopy bulkcopy = new(destConn, SqlBulkCopyOptions.TableLock, null);
                bulkcopy.DestinationTableName = dsttable;
                SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;
                ColumnMappings.Add("col1", "col1");
                ColumnMappings.Add("col2", "col2");
                ColumnMappings.Add("col3", "col3");

                bulkcopy.WriteToServer(reader);
                Helpers.VerifyResults(destConn, dsttable, 3, 3);
            }
            finally
            {
                Helpers.DropTables(dstconstr, srctable, dsttable);
            }
        }
    }
}

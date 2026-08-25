// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
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
            using SqlConnection destConn = new(dstconstr);
            destConn.Open();

            using SqlCommand dstcmd = destConn.CreateCommand();

            using Table srctable = new(destConn, "SqlBulkCopyTest_TableLock0", "(col1 int, col2 text, col3 text)");
            using Table dsttable = new(destConn, "SqlBulkCopyTest_TableLock1", "(col1 int identity(1,1), col2 text default 'Jogurt', col3 text)");

            Helpers.TryExecute(dstcmd, "insert into " + srctable.Name + "(col1, col3) values (1, 'Michael')");
            Helpers.TryExecute(dstcmd, "insert into " + srctable.Name + "(col1, col2, col3) values (2, 'Quark', 'Astrid')");
            Helpers.TryExecute(dstcmd, "insert into " + srctable.Name + "(col1, col2) values (66, 'K�se');");

            using SqlConnection sourceConn = new(srcconstr);
            sourceConn.Open();

            using SqlCommand srccmd = new SqlCommand("select * from " + srctable.Name, sourceConn);
            using IDataReader reader = srccmd.ExecuteReader();

            using SqlBulkCopy bulkcopy = new(destConn, SqlBulkCopyOptions.TableLock, null);
            bulkcopy.DestinationTableName = dsttable.Name;
            SqlBulkCopyColumnMappingCollection ColumnMappings = bulkcopy.ColumnMappings;
            ColumnMappings.Add("col1", "col1");
            ColumnMappings.Add("col2", "col2");
            ColumnMappings.Add("col3", "col3");

            bulkcopy.WriteToServer(reader);
            Helpers.VerifyResults(destConn, dsttable.Name, 3, 3);
        }
    }
}

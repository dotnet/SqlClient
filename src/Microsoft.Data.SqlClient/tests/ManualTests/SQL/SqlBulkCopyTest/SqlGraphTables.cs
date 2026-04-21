// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SqlBulkCopyTests
{
    public class SqlGraphTables
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsAtLeastSQL2017))]
        public void WriteToServer_CopyToSqlGraphNodeTable_Succeeds()
        {
            string connectionString = DataTestUtility.TCPConnectionString;

            using SqlConnection dstConn = new SqlConnection(connectionString);
            using DataTable nodes = new DataTable()
            {
                Columns = { new DataColumn("Name", typeof(string)) }
            };

            dstConn.Open();

            for (int i = 0; i < 5; i++)
            {
                nodes.Rows.Add($"Name {i}");
            }

            using Table dstNodeTable = new(dstConn, "SqlGraphNodeTable", "(Id INT PRIMARY KEY IDENTITY(1,1), [Name] VARCHAR(100)) AS NODE");
            using SqlBulkCopy nodeCopy = new SqlBulkCopy(dstConn);

            nodeCopy.DestinationTableName = dstNodeTable.Name;
            nodeCopy.ColumnMappings.Add("Name", "Name");
            nodeCopy.WriteToServer(nodes);
        }
    }
}

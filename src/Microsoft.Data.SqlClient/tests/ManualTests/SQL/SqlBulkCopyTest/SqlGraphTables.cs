// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SqlBulkCopyTests
{
    public class SqlGraphTables
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void WriteToServer_CopyToSqlGraphNodeTable_Succeeds()
        {
            string connectionString = DataTestUtility.TCPConnectionString;
            string destinationTable = DataTestUtility.GetShortName("SqlGraphNodeTable");

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

            try
            {
                DataTestUtility.CreateTable(dstConn, destinationTable, "(Id INT PRIMARY KEY IDENTITY(1,1), [Name] VARCHAR(100)) AS NODE");

                using SqlBulkCopy nodeCopy = new SqlBulkCopy(dstConn);

                nodeCopy.DestinationTableName = destinationTable;
                nodeCopy.ColumnMappings.Add("Name", "Name");
                nodeCopy.WriteToServer(nodes);
            }
            finally
            {
                DataTestUtility.DropTable(dstConn, destinationTable);
            }
        }
    }
}

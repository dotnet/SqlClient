// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlGraphTables
    {
        public static void Test(string dstConnectionString, string dstNodeTable)
        {
            using SqlConnection dstConn = new SqlConnection(dstConnectionString);
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
                DataTestUtility.CreateTable(dstConn, dstNodeTable, "(Id INT PRIMARY KEY IDENTITY(1,1), [Name] VARCHAR(100)) AS NODE");

                using SqlBulkCopy nodeCopy = new SqlBulkCopy(dstConn);

                nodeCopy.DestinationTableName = dstNodeTable;
                nodeCopy.ColumnMappings.Add("Name", "Name");
                nodeCopy.WriteToServer(nodes);
            }
            finally
            {
                DataTestUtility.DropTable(dstConn, dstNodeTable);
            }
        }
    }
}

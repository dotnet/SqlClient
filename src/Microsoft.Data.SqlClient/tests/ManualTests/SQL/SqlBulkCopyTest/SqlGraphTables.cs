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

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsAtLeastSQL2017))]
        public void WriteToServer_CopyToAliasedColumnName_Succeeds()
        {
            string connectionString = DataTestUtility.TCPConnectionString;

            using SqlConnection dstConn = new SqlConnection(connectionString);
            using DataTable edges = new DataTable()
            {
                Columns = { new DataColumn("To_ID", typeof(string)), new DataColumn("From_ID", typeof(string)), new DataColumn("Description", typeof(string)) }
            };

            dstConn.Open();

            using Table srcNodeTable = new(dstConn, "SqlGraph_NodeByAlias", "(Id INT PRIMARY KEY IDENTITY(1,1), [Name] VARCHAR(100)) AS NODE");
            using Table dstEdgeTable = new(dstConn, "SqlGraph_EdgeByAlias", "([Description] VARCHAR(100)) AS EDGE");

            string sampleNodeDataCommand = @$"INSERT INTO {srcNodeTable.Name} ([Name]) SELECT LEFT([name], 100) FROM sys.sysobjects";
            using (SqlCommand insertSampleNodes = new(sampleNodeDataCommand, dstConn))
            {
                insertSampleNodes.ExecuteNonQuery();
            }

            using (SqlCommand nodeQuery = new SqlCommand($"SELECT $node_id FROM {srcNodeTable.Name}", dstConn))
            using (SqlDataReader reader = nodeQuery.ExecuteReader())
            {
                bool firstRead = reader.Read();
                string toId;
                string fromId;

                Assert.True(firstRead);
                toId = reader.GetString(0);

                while (reader.Read())
                {
                    fromId = reader.GetString(0);

                    edges.Rows.Add(toId, fromId, "Test Description");
                    toId = fromId;
                }
            }

            using SqlBulkCopy edgeCopy = new SqlBulkCopy(dstConn);

            edgeCopy.DestinationTableName = dstEdgeTable.Name;
            edgeCopy.ColumnMappings.Add("To_ID", "$to_id");
            edgeCopy.ColumnMappings.Add("From_ID", "$from_id");
            edgeCopy.ColumnMappings.Add("Description", "Description");

            edgeCopy.WriteToServer(edges);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsAtLeastSQL2017))]
        public void WriteToServer_CopyToTableWithSameNameAsColumnAlias_Succeeds()
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

            using Table dstGraphTable = new(dstConn, "SqlGraph_NodeWithAlias", "(Id INT PRIMARY KEY IDENTITY(1,1), [Name] VARCHAR(100), [$node_id] VARCHAR(100)) AS NODE");
            using Table dstNormalTable = new(dstConn, "NonGraph_NodeWithAlias", "(Id INT PRIMARY KEY IDENTITY(1,1), [Name] VARCHAR(100), [$node_id] VARCHAR(100))");

            using (SqlBulkCopy nodeCopy = new SqlBulkCopy(dstConn))
            {
                nodeCopy.DestinationTableName = dstGraphTable.Name;
                nodeCopy.ColumnMappings.Add("Name", "Name");
                nodeCopy.ColumnMappings.Add("Name", "$node_id");
                nodeCopy.WriteToServer(nodes);

                nodeCopy.DestinationTableName = dstNormalTable.Name;
                nodeCopy.WriteToServer(nodes);
            }

            // Read the values back, ensuring that we haven't overwritten the $node_id alias with the contents of the [$node_id] column.
            // SELECTing $node_id will read the SQL Graph's node ID, SELECTing [$node_id] will read the column named $node_id.
            using (SqlCommand graphVerificationCommand = new SqlCommand($"SELECT Id, $node_id, [$node_id], Name FROM {dstGraphTable.Name}", dstConn))
            using (SqlDataReader reader = graphVerificationCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    string aliasNodeId = reader.GetString(1);
                    string physicalNodeId = reader.GetString(2);
                    string name = reader.GetString(3);

                    Assert.NotEqual(physicalNodeId, aliasNodeId);
                    Assert.Equal(name, physicalNodeId);
                }
            }

            using (SqlCommand normalVerificationCommand = new SqlCommand($"SELECT [$node_id], Name FROM {dstNormalTable.Name}", dstConn))
            using (SqlDataReader reader = normalVerificationCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    string physicalNodeId = reader.GetString(0);
                    string name = reader.GetString(1);

                    Assert.Equal(name, physicalNodeId);
                }
            }
        }
    }
}

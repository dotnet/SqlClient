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
    public class SqlGraphTables
    {
        /// <summary>
        /// Verifies that copying to a SQL Graph table without graph aliases in the mappings does not
        /// require alias resolution.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsAtLeastSQL2017))]
        public void WriteToServer_CopyToSqlGraphNodeTableBySourceOrdinal_Succeeds()
        {
            string connectionString = DataTestUtility.TCPConnectionString;

            using SqlConnection dstConn = new(connectionString);
            using DataTable nodes = new()
            {
                Columns = { new DataColumn("Name", typeof(string)) }
            };

            dstConn.Open();

            for (int i = 0; i < 5; i++)
            {
                nodes.Rows.Add($"Name {i}");
            }

            using Table dstNodeTable = new(dstConn, "SqlGraphNodeTableByOrdinal", "([Name] VARCHAR(100)) AS NODE");
            using SqlBulkCopy nodeCopy = new(dstConn);

            nodeCopy.DestinationTableName = dstNodeTable.Name;
            nodeCopy.ColumnMappings.Add(0, "Name");
            nodeCopy.WriteToServer(nodes);

            using SqlCommand verifyCommand = new($"SELECT COUNT(*) FROM {dstNodeTable.Name}", dstConn);
            Assert.Equal(5, (int)verifyCommand.ExecuteScalar());
        }

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

            using (SqlBulkCopy edgeCopy = new(dstConn))
            {
                edgeCopy.DestinationTableName = dstEdgeTable.Name;
                edgeCopy.ColumnMappings.Add("To_ID", "$to_id");
                edgeCopy.ColumnMappings.Add("From_ID", "$from_id");
                edgeCopy.ColumnMappings.Add("Description", "Description");

                edgeCopy.WriteToServer(edges);
            }

            // Read the values back, comparing to the source DataTable
            using SqlCommand dstVerificationCommand = new($"SELECT $to_id, $from_id, [Description] FROM {dstEdgeTable.Name} ORDER BY $to_id ASC", dstConn);
            using SqlDataReader dstVerificationReader = dstVerificationCommand.ExecuteReader();
            int currentRow = 0;
            DataRow[] sortedRows = edges.Select(filterExpression: null, sort: "To_ID ASC");

            while (dstVerificationReader.Read())
            {
                string toId = dstVerificationReader.GetString(0);
                string fromId = dstVerificationReader.GetString(1);
                string description = dstVerificationReader.GetString(2);
                DataRow currSourceRow = sortedRows[currentRow];

                Assert.Equal(currSourceRow["To_ID"], toId);
                Assert.Equal(currSourceRow["From_ID"], fromId);
                Assert.Equal(currSourceRow["Description"], description);

                currentRow++;
            }
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

        /// <summary>
        /// Reuses one SqlBulkCopy after graph alias mappings have been resolved, then changes those
        /// mappings to ordinary destination columns. This guards against stale resolved graph
        /// canonical names being reused on the next operation.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsAtLeastSQL2017))]
        public void WriteToServer_ReuseAfterAliasMappingsThenOrdinaryMappings_Succeeds()
        {
            string connectionString = DataTestUtility.TCPConnectionString;

            using SqlConnection dstConn = new(connectionString);
            dstConn.Open();

            using Table srcNodeTable = new(dstConn, "SqlGraph_ReuseAliasFirstNodes", "(Id INT PRIMARY KEY IDENTITY(1,1), [Name] VARCHAR(100)) AS NODE");
            using Table dstEdgeTable = new(dstConn, "SqlGraph_ReuseAliasFirstEdges", "([Description] VARCHAR(100)) AS EDGE");
            using Table dstNormalTable = new(dstConn, "SqlGraph_ReuseAliasFirstNormal", "([Description] VARCHAR(100), [ToId] NVARCHAR(MAX) NOT NULL, [FromId] NVARCHAR(MAX) NOT NULL)");
            using DataTable edges = CreateEdgeSourceData(dstConn, srcNodeTable.Name);

            using SqlBulkCopy edgeCopy = new(dstConn);
            edgeCopy.ColumnMappings.Add("Description", "Description");
            edgeCopy.ColumnMappings.Add("ToId", "$to_id");
            edgeCopy.ColumnMappings.Add("FromId", "$from_id");

            edgeCopy.DestinationTableName = dstEdgeTable.Name;
            edgeCopy.WriteToServer(edges);

            edgeCopy.ColumnMappings[0].SourceColumn = "Description";
            edgeCopy.ColumnMappings[1].DestinationColumn = "ToId";
            edgeCopy.ColumnMappings[2].DestinationColumn = "FromId";
            edgeCopy.DestinationTableName = dstNormalTable.Name;
            edgeCopy.WriteToServer(edges);

            VerifyNormalEdgeData(dstConn, dstNormalTable.Name, edges);
        }

        /// <summary>
        /// Reuses one SqlBulkCopy after ordinary mappings, then changes those mappings to graph
        /// aliases. This ensures alias-resolution bypass state is recomputed for each operation.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsAtLeastSQL2017))]
        public void WriteToServer_ReuseAfterOrdinaryMappingsThenAliasMappings_Succeeds()
        {
            string connectionString = DataTestUtility.TCPConnectionString;

            using SqlConnection dstConn = new(connectionString);
            dstConn.Open();

            using Table srcNodeTable = new(dstConn, "SqlGraph_ReuseOrdinaryFirstNodes", "(Id INT PRIMARY KEY IDENTITY(1,1), [Name] VARCHAR(100)) AS NODE");
            using Table dstNormalTable = new(dstConn, "SqlGraph_ReuseOrdinaryFirstNormal", "([Description] VARCHAR(100), [ToId] NVARCHAR(MAX) NOT NULL, [FromId] NVARCHAR(MAX) NOT NULL)");
            using Table dstEdgeTable = new(dstConn, "SqlGraph_ReuseOrdinaryFirstEdges", "([Description] VARCHAR(100)) AS EDGE");
            using DataTable edges = CreateEdgeSourceData(dstConn, srcNodeTable.Name);

            using SqlBulkCopy edgeCopy = new(dstConn);
            edgeCopy.ColumnMappings.Add("Description", "Description");
            edgeCopy.ColumnMappings.Add("ToId", "ToId");
            edgeCopy.ColumnMappings.Add("FromId", "FromId");

            edgeCopy.DestinationTableName = dstNormalTable.Name;
            edgeCopy.WriteToServer(edges);

            edgeCopy.ColumnMappings[0].SourceColumn = "Description";
            edgeCopy.ColumnMappings[1].DestinationColumn = "$to_id";
            edgeCopy.ColumnMappings[2].DestinationColumn = "$from_id";
            edgeCopy.DestinationTableName = dstEdgeTable.Name;
            edgeCopy.WriteToServer(edges);

            VerifyGraphEdgeData(dstConn, dstEdgeTable.Name, edges);
        }

        /// <summary>
        /// Exercises CacheMetadata with graph alias mappings against a real SQL Graph edge table,
        /// including reuse of the cached alias result set on a later operation.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsAtLeastSQL2017))]
        public void WriteToServer_CacheMetadataWithSqlGraphAliasMappings_Succeeds()
        {
            string connectionString = DataTestUtility.TCPConnectionString;

            using SqlConnection dstConn = new(connectionString);
            dstConn.Open();

            using Table srcNodeTable = new(dstConn, "SqlGraph_CacheAliasNodes", "(Id INT PRIMARY KEY IDENTITY(1,1), [Name] VARCHAR(100)) AS NODE");
            using Table dstEdgeTable = new(dstConn, "SqlGraph_CacheAliasEdges", "([Description] VARCHAR(100)) AS EDGE");
            using DataTable edges = CreateEdgeSourceData(dstConn, srcNodeTable.Name);

            using SqlBulkCopy cachedCopy = new(dstConn, SqlBulkCopyOptions.CacheMetadata, null);

            cachedCopy.DestinationTableName = dstEdgeTable.Name;
            cachedCopy.ColumnMappings.Add("Description", "Description");
            cachedCopy.ColumnMappings.Add("ToId", "$to_id");
            cachedCopy.ColumnMappings.Add("FromId", "$from_id");
            cachedCopy.WriteToServer(edges);
            VerifyGraphEdgeData(dstConn, dstEdgeTable.Name, edges);

            cachedCopy.ColumnMappings[0].SourceColumn = "Description";
            cachedCopy.ColumnMappings[1].DestinationColumn = "$to_id";
            cachedCopy.ColumnMappings[2].DestinationColumn = "$from_id";
            cachedCopy.WriteToServer(edges);
            VerifyGraphEdgeRowCount(dstConn, dstEdgeTable.Name, edges.Rows.Count * 2);
        }

        private static DataTable CreateEdgeSourceData(SqlConnection connection, string nodeTableName)
        {
            using SqlCommand insertSampleNodes = new($"INSERT INTO {nodeTableName} ([Name]) VALUES ('A'), ('B'), ('C')", connection);
            insertSampleNodes.ExecuteNonQuery();

            DataTable edges = new()
            {
                Columns =
                {
                    new DataColumn("Description", typeof(string)),
                    new DataColumn("ToId", typeof(string)),
                    new DataColumn("FromId", typeof(string))
                }
            };

            using SqlCommand nodeQuery = new($"SELECT $node_id FROM {nodeTableName} ORDER BY Id", connection);
            using SqlDataReader reader = nodeQuery.ExecuteReader();
            Assert.True(reader.Read());
            string firstNodeId = reader.GetString(0);

            Assert.True(reader.Read());
            string secondNodeId = reader.GetString(0);
            edges.Rows.Add("First edge", firstNodeId, secondNodeId);

            Assert.True(reader.Read());
            string thirdNodeId = reader.GetString(0);
            edges.Rows.Add("Second edge", secondNodeId, thirdNodeId);

            return edges;
        }

        private static void VerifyGraphEdgeData(SqlConnection connection, string edgeTableName, DataTable expectedEdges)
        {
            using SqlCommand verificationCommand = new($"SELECT [Description], $to_id, $from_id FROM {edgeTableName} ORDER BY [Description]", connection);
            using SqlDataReader reader = verificationCommand.ExecuteReader();

            foreach (DataRow expectedRow in expectedEdges.Select(filterExpression: null, sort: "Description"))
            {
                Assert.True(reader.Read());
                Assert.Equal(expectedRow["Description"], reader.GetString(0));
                Assert.Equal(expectedRow["ToId"], reader.GetString(1));
                Assert.Equal(expectedRow["FromId"], reader.GetString(2));
            }

            Assert.False(reader.Read());
        }

        private static void VerifyNormalEdgeData(SqlConnection connection, string tableName, DataTable expectedEdges)
        {
            using SqlCommand verificationCommand = new($"SELECT [Description], [ToId], [FromId] FROM {tableName} ORDER BY [Description]", connection);
            using SqlDataReader reader = verificationCommand.ExecuteReader();

            foreach (DataRow expectedRow in expectedEdges.Select(filterExpression: null, sort: "Description"))
            {
                Assert.True(reader.Read());
                Assert.Equal(expectedRow["Description"], reader.GetString(0));
                Assert.Equal(expectedRow["ToId"], reader.GetString(1));
                Assert.Equal(expectedRow["FromId"], reader.GetString(2));
            }

            Assert.False(reader.Read());
        }

        private static void VerifyGraphEdgeRowCount(SqlConnection connection, string edgeTableName, int expectedRows)
        {
            using SqlCommand countCommand = new($"SELECT COUNT(*) FROM {edgeTableName}", connection);
            Assert.Equal(expectedRows, (int)countCommand.ExecuteScalar());
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    /// <summary>
    /// Verifies SqlBulkCopy when the UseLegacyBulkCopyMetadataDiscovery switch selects the pre-6.0
    /// destination metadata query.
    /// </summary>
    [Trait("Set", "2")]
    public class LegacyMetadataDiscovery
    {
        private const string TableDefinition =
            "(Id INT NOT NULL, Name NVARCHAR(50) COLLATE Latin1_General_CI_AS NULL, " +
            "Amount DECIMAL(10, 2))";

        private const string NodeDefinition = "([Name] VARCHAR(100)) AS NODE";

        private const string EdgeDefinition = "([Description] VARCHAR(100)) AS EDGE";

        /// <summary>
        /// Verifies that the legacy query discovers metadata and copies rows into a regular table.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void WriteToServer_LegacyDiscovery_CopiesRows()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseLegacyBulkCopyMetadataDiscovery = true;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using Table table = new(connection, "LegacyMetadataDiscovery", TableDefinition);
            using DataTable source = CreateSource(5);
            using SqlBulkCopy copy = new(connection) { DestinationTableName = table.Name };

            // Act
            copy.WriteToServer(source);

            // Assert
            AssertRows(connection, table.Name, 5);
        }

        /// <summary>
        /// Verifies that the legacy query discovers metadata and copies rows asynchronously.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public async Task WriteToServerAsync_LegacyDiscovery_CopiesRows()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseLegacyBulkCopyMetadataDiscovery = true;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();
            using Table table = new(connection, "LegacyMetadataDiscoveryAsync", TableDefinition);
            using DataTable source = CreateSource(5);
            using SqlBulkCopy copy = new(connection) { DestinationTableName = table.Name };

            // Act
            await copy.WriteToServerAsync(source);

            // Assert
            AssertRows(connection, table.Name, 5);
        }

        /// <summary>
        /// Verifies that the legacy query reads collations from tempdb for a temp table.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void WriteToServer_LegacyDiscoveryTempTable_CopiesRows()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseLegacyBulkCopyMetadataDiscovery = true;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            string tableName = "#LegacyMetadataDiscovery";
            string createSql = $"CREATE TABLE {tableName} {TableDefinition}";
            using (SqlCommand create = new(createSql, connection))
            {
                create.ExecuteNonQuery();
            }
            using DataTable source = CreateSource(3);
            using SqlBulkCopy copy = new(connection) { DestinationTableName = tableName };

            // Act
            copy.WriteToServer(source);

            // Assert
            AssertRows(connection, tableName, 3);
        }

        /// <summary>
        /// Verifies that the legacy query cannot discover hidden temporal columns, so mapping to
        /// one fails on the client as it did before 6.0.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public void WriteToServer_LegacyDiscoveryHiddenColumn_ThrowsInvalidOperation()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseLegacyBulkCopyMetadataDiscovery = true;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using TemporalTable table = CreateTemporalTable(connection, "LegacyHidden");
            using DataTable source = CreateTemporalSource();
            using SqlBulkCopy copy = CreateTemporalCopy(connection, table.Name);

            // Act
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => copy.WriteToServer(source));

            // Assert
            Assert.Contains("ValidFrom", ex.Message);
        }

        /// <summary>
        /// Verifies that the legacy query cannot discover hidden temporal columns when copying
        /// asynchronously.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public async Task WriteToServerAsync_LegacyDiscoveryHiddenColumn_ThrowsInvalidOperation()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseLegacyBulkCopyMetadataDiscovery = true;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();
            using TemporalTable table = CreateTemporalTable(connection, "LegacyHiddenAsync");
            using DataTable source = CreateTemporalSource();
            using SqlBulkCopy copy = CreateTemporalCopy(connection, table.Name);

            // Act
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => copy.WriteToServerAsync(source));

            // Assert
            Assert.Contains("ValidFrom", ex.Message);
        }

        /// <summary>
        /// Verifies that the default query still discovers hidden temporal columns, so the server
        /// rejects the mapping instead of the client.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public void WriteToServer_DefaultDiscoveryHiddenColumn_ThrowsSqlException()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseLegacyBulkCopyMetadataDiscovery = false;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using TemporalTable table = CreateTemporalTable(connection, "DefaultHidden");
            using DataTable source = CreateTemporalSource();
            using SqlBulkCopy copy = CreateTemporalCopy(connection, table.Name);

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => copy.WriteToServer(source));

            // Assert
            Assert.Equal(13536, ex.Number);
        }

        /// <summary>
        /// Verifies that graph alias mappings fall back to the catalog-based query when the
        /// legacy switch is enabled.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse),
            nameof(DataTestUtility.IsAtLeastSQL2017))]
        public void WriteToServer_LegacyDiscoveryGraphAlias_CopiesRows()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseLegacyBulkCopyMetadataDiscovery = true;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using Table node = new(connection, "LegacyGraphNode", NodeDefinition);
            using Table edge = new(connection, "LegacyGraphEdge", EdgeDefinition);
            using DataTable source = CreateEdgeSource(connection, node.Name);
            using SqlBulkCopy copy = CreateEdgeCopy(connection, edge.Name);

            // Act
            copy.WriteToServer(source);

            // Assert
            AssertRows(connection, edge.Name, 1);
        }

        /// <summary>
        /// Verifies that graph alias mappings fall back to the catalog-based query when copying
        /// asynchronously with the legacy switch enabled.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse),
            nameof(DataTestUtility.IsAtLeastSQL2017))]
        public async Task WriteToServerAsync_LegacyDiscoveryGraphAlias_CopiesRows()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseLegacyBulkCopyMetadataDiscovery = true;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();
            using Table node = new(connection, "LegacyGraphNodeAsync", NodeDefinition);
            using Table edge = new(connection, "LegacyGraphEdgeAsync", EdgeDefinition);
            using DataTable source = CreateEdgeSource(connection, node.Name);
            using SqlBulkCopy copy = CreateEdgeCopy(connection, edge.Name);

            // Act
            await copy.WriteToServerAsync(source);

            // Assert
            AssertRows(connection, edge.Name, 1);
        }

        /// <summary>
        /// Verifies that metadata cached from the legacy query is reused across repeated copies.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void WriteToServer_LegacyDiscoveryCacheMetadata_CopiesRepeatedly()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseLegacyBulkCopyMetadataDiscovery = true;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using Table table = new(connection, "LegacyMetadataDiscoveryCache", TableDefinition);
            using DataTable source = CreateSource(2);
            using SqlBulkCopy copy = new(connection, SqlBulkCopyOptions.CacheMetadata, null)
            {
                DestinationTableName = table.Name
            };

            // Act
            copy.WriteToServer(source);
            copy.WriteToServer(source);
            copy.WriteToServer(source);

            // Assert
            AssertRows(connection, table.Name, 6);
        }

        private static DataTable CreateSource(int rows)
        {
            DataTable table = new()
            {
                Columns =
                {
                    new DataColumn("Id", typeof(int)),
                    new DataColumn("Name", typeof(string)),
                    new DataColumn("Amount", typeof(decimal))
                }
            };
            for (int i = 0; i < rows; i++)
            {
                table.Rows.Add(i, $"Name {i}", i * 1.5m);
            }
            return table;
        }

        private static TemporalTable CreateTemporalTable(SqlConnection connection, string prefix) =>
            new(connection, prefix, prefix + "_History", """
(
    Id int primary key not null,
    ValidFrom datetime2 generated always as row start hidden not null,
    ValidTo datetime2 generated always as row end hidden not null,
    period for system_time (ValidFrom, ValidTo)
)
""");

        private static DataTable CreateTemporalSource()
        {
            DataTable table = new()
            {
                Columns =
                {
                    new DataColumn("Id", typeof(int)),
                    new DataColumn("ValidFrom", typeof(DateTime))
                }
            };
            table.Rows.Add(1, DateTime.UtcNow);
            return table;
        }

        private static SqlBulkCopy CreateTemporalCopy(SqlConnection connection, string tableName)
        {
            SqlBulkCopy copy = new(connection) { DestinationTableName = tableName };
            copy.ColumnMappings.Add("Id", "Id");
            copy.ColumnMappings.Add("ValidFrom", "ValidFrom");
            return copy;
        }

        private static DataTable CreateEdgeSource(SqlConnection connection, string nodeTable)
        {
            using (SqlCommand insert = new(
                $"INSERT INTO {nodeTable} ([Name]) VALUES ('a'), ('b')", connection))
            {
                insert.ExecuteNonQuery();
            }

            DataTable table = new()
            {
                Columns =
                {
                    new DataColumn("From_ID", typeof(string)),
                    new DataColumn("To_ID", typeof(string)),
                    new DataColumn("Description", typeof(string))
                }
            };
            using SqlCommand select = new(
                $"SELECT $node_id FROM {nodeTable} ORDER BY [Name]", connection);
            using SqlDataReader reader = select.ExecuteReader();
            Assert.True(reader.Read());
            string fromId = reader.GetString(0);
            Assert.True(reader.Read());
            string toId = reader.GetString(0);
            table.Rows.Add(fromId, toId, "edge");
            return table;
        }

        private static SqlBulkCopy CreateEdgeCopy(SqlConnection connection, string edgeTable)
        {
            SqlBulkCopy copy = new(connection) { DestinationTableName = edgeTable };
            copy.ColumnMappings.Add("From_ID", "$from_id");
            copy.ColumnMappings.Add("To_ID", "$to_id");
            copy.ColumnMappings.Add("Description", "Description");
            return copy;
        }

        private static void AssertRows(SqlConnection connection, string tableName, int expected)
        {
            using SqlCommand count = new($"SELECT COUNT(*) FROM {tableName}", connection);
            Assert.Equal(expected, (int)count.ExecuteScalar());
        }
    }
}

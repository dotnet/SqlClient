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
    /// Verifies SqlBulkCopy with the pre-6.0 destination metadata query, used when the
    /// UseBulkCopyCatalogMetadataDiscovery switch is disabled.
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
        /// Verifies that each discovery mode copies rows into a regular table.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        public void WriteToServer_DiscoveryMode_CopiesRows(bool? mode)
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseBulkCopyCatalogMetadataDiscovery = mode;
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
        /// Verifies that each discovery mode copies rows asynchronously.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WriteToServerAsync_DiscoveryMode_CopiesRows(bool? mode)
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseBulkCopyCatalogMetadataDiscovery = mode;
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
            switches.UseBulkCopyCatalogMetadataDiscovery = false;
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
            switches.UseBulkCopyCatalogMetadataDiscovery = false;
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
            switches.UseBulkCopyCatalogMetadataDiscovery = false;
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
        /// Verifies that catalog discovery finds hidden temporal columns, so the server rejects
        /// the mapping instead of the client.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public void WriteToServer_CatalogDiscoveryHiddenColumn_ThrowsSqlException()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseBulkCopyCatalogMetadataDiscovery = true;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using TemporalTable table = CreateTemporalTable(connection, "CatalogHidden");
            using DataTable source = CreateTemporalSource();
            using SqlBulkCopy copy = CreateTemporalCopy(connection, table.Name);

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => copy.WriteToServer(source));

            // Assert
            Assert.Equal(13536, ex.Number);
        }

        /// <summary>Verifies automatic fallback copies writable hidden columns.</summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public void WriteToServer_AutomaticHiddenColumns_CopiesRows()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseBulkCopyCatalogMetadataDiscovery = null;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using Table table = CreateWritableHiddenTable(connection, "AutomaticHidden");
            using DataTable source = CreateWritableHiddenSource();
            using SqlBulkCopy copy = CreateWritableHiddenCopy(connection, table.Name);

            // Act
            copy.WriteToServer(source);

            // Assert
            AssertHiddenValues(connection, table.Name, source);
        }

        /// <summary>Verifies automatic fallback copies writable hidden columns asynchronously.</summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public async Task WriteToServerAsync_AutomaticHiddenColumns_CopiesRows()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseBulkCopyCatalogMetadataDiscovery = null;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();
            using Table table = CreateWritableHiddenTable(connection, "AutomaticHiddenAsync");
            using DataTable source = CreateWritableHiddenSource();
            using SqlBulkCopy copy = CreateWritableHiddenCopy(connection, table.Name);

            // Act
            await copy.WriteToServerAsync(source);

            // Assert
            AssertHiddenValues(connection, table.Name, source);
        }

        /// <summary>Verifies fallback replaces a visible-column cache and reuses catalog metadata.</summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public void WriteToServer_AutomaticHiddenMappingAfterCache_ReplacesCachedMetadata()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseBulkCopyCatalogMetadataDiscovery = null;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using Table table = CreateWritableHiddenTable(connection, "AutomaticHiddenCache");
            using DataTable source = CreateWritableHiddenSource();
            using SqlBulkCopy copy = new(connection, SqlBulkCopyOptions.CacheMetadata, null)
            {
                DestinationTableName = table.Name
            };
            copy.ColumnMappings.Add("Id", "Id");
            copy.WriteToServer(source);
            Assert.False(copy.CachedMetadata.UsesCatalogMetadata);
            copy.ColumnMappings.Add("ValidFrom", "ValidFrom");
            copy.ColumnMappings.Add("ValidTo", "ValidTo");

            // Act
            copy.WriteToServer(source);
            BulkCopySimpleResultSet catalogMetadata = copy.CachedMetadata;
            copy.WriteToServer(source);

            // Assert
            Assert.True(catalogMetadata.UsesCatalogMetadata);
            Assert.Same(catalogMetadata, copy.CachedMetadata);
            AssertRows(connection, table.Name, 3);
        }

        /// <summary>Verifies graph aliases work in automatic and forced catalog modes.</summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse),
            nameof(DataTestUtility.IsAtLeastSQL2017))]
        [InlineData(null)]
        [InlineData(true)]
        public void WriteToServer_CatalogGraphAliases_CopiesRows(bool? mode)
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseBulkCopyCatalogMetadataDiscovery = mode;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using Table node = new(connection, "CatalogGraphNode", NodeDefinition);
            using Table edge = new(connection, "CatalogGraphEdge", EdgeDefinition);
            using DataTable source = CreateEdgeSource(connection, node.Name);
            using SqlBulkCopy copy = CreateEdgeCopy(connection, edge.Name);

            // Act
            copy.WriteToServer(source);

            // Assert
            AssertRows(connection, edge.Name, 1);
        }

        /// <summary>Verifies graph aliases work asynchronously in both catalog modes.</summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse),
            nameof(DataTestUtility.IsAtLeastSQL2017))]
        [InlineData(null)]
        [InlineData(true)]
        public async Task WriteToServerAsync_CatalogGraphAliases_CopiesRows(bool? mode)
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseBulkCopyCatalogMetadataDiscovery = mode;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();
            using Table node = new(connection, "CatalogGraphNodeAsync", NodeDefinition);
            using Table edge = new(connection, "CatalogGraphEdgeAsync", EdgeDefinition);
            using DataTable source = CreateEdgeSource(connection, node.Name);
            using SqlBulkCopy copy = CreateEdgeCopy(connection, edge.Name);

            // Act
            await copy.WriteToServerAsync(source);

            // Assert
            AssertRows(connection, edge.Name, 1);
        }

        /// <summary>
        /// Verifies that the legacy query cannot resolve SQL Graph aliases, so mapping to one fails
        /// on the client.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse),
            nameof(DataTestUtility.IsAtLeastSQL2017))]
        public void WriteToServer_LegacyDiscoveryGraphAlias_ThrowsInvalidOperation()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseBulkCopyCatalogMetadataDiscovery = false;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using Table node = new(connection, "LegacyGraphNode", NodeDefinition);
            using Table edge = new(connection, "LegacyGraphEdge", EdgeDefinition);
            using DataTable source = CreateEdgeSource(connection, node.Name);
            using SqlBulkCopy copy = CreateEdgeCopy(connection, edge.Name);

            // Act
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => copy.WriteToServer(source));

            // Assert
            Assert.Contains("$from_id", ex.Message);
            AssertRows(connection, edge.Name, 0);
        }

        /// <summary>
        /// Verifies that the legacy query cannot resolve SQL Graph aliases when copying
        /// asynchronously.
        /// </summary>
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse),
            nameof(DataTestUtility.IsAtLeastSQL2017))]
        public async Task WriteToServerAsync_LegacyDiscoveryGraphAlias_ThrowsInvalidOperation()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseBulkCopyCatalogMetadataDiscovery = false;
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();
            using Table node = new(connection, "LegacyGraphNodeAsync", NodeDefinition);
            using Table edge = new(connection, "LegacyGraphEdgeAsync", EdgeDefinition);
            using DataTable source = CreateEdgeSource(connection, node.Name);
            using SqlBulkCopy copy = CreateEdgeCopy(connection, edge.Name);

            // Act
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => copy.WriteToServerAsync(source));

            // Assert
            Assert.Contains("$from_id", ex.Message);
            AssertRows(connection, edge.Name, 0);
        }

        /// <summary>
        /// Verifies that metadata cached from the legacy query is reused across repeated copies.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void WriteToServer_LegacyDiscoveryCacheMetadata_CopiesRepeatedly()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switches = new();
            switches.UseBulkCopyCatalogMetadataDiscovery = false;
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

        private static Table CreateWritableHiddenTable(SqlConnection connection, string prefix)
        {
            Table table = new(connection, prefix, """
(
    Id int not null,
    ValidFrom datetime2 generated always as row start hidden not null
        default '2000-01-01',
    ValidTo datetime2 generated always as row end hidden not null
        default '9999-12-31',
    period for system_time (ValidFrom, ValidTo)
)
""");
            try
            {
                using SqlCommand command = new(
                    $"ALTER TABLE {table.Name} DROP PERIOD FOR SYSTEM_TIME", connection);
                command.ExecuteNonQuery();
                return table;
            }
            catch
            {
                table.Dispose();
                throw;
            }
        }

        private static DataTable CreateWritableHiddenSource()
        {
            DataTable source = CreateTemporalSource();
            source.Columns.Add("ValidTo", typeof(DateTime));
            source.Rows[0]["ValidTo"] = new DateTime(2099, 1, 1);
            return source;
        }

        private static SqlBulkCopy CreateWritableHiddenCopy(
            SqlConnection connection, string tableName)
        {
            SqlBulkCopy copy = CreateTemporalCopy(connection, tableName);
            copy.ColumnMappings.Add("ValidTo", "ValidTo");
            return copy;
        }

        private static void AssertHiddenValues(
            SqlConnection connection, string tableName, DataTable source)
        {
            using SqlCommand command = new($"SELECT ValidFrom, ValidTo FROM {tableName}", connection);
            using SqlDataReader reader = command.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal((DateTime)source.Rows[0]["ValidFrom"], reader.GetDateTime(0));
            Assert.Equal((DateTime)source.Rows[0]["ValidTo"], reader.GetDateTime(1));
            Assert.False(reader.Read());
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

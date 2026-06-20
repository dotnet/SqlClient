// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SqlBulkCopyTests;

#nullable enable

/// <summary>
/// Some unprivileged users may not have permissions to query sys.all_columns, which is used to
/// handle column aliases. These tests verify that bulk copy operations can handle such situations
/// gracefully, falling back and ignoring column aliases.
/// </summary>
[Trait("Set", "2")]
public sealed class UnprivilegedLogin : IDisposable
{
    private readonly SqlConnection? _managementConnection;
    private readonly ServerLogin? _unprivilegedLogin;
    private readonly DatabaseUser? _unprivilegedMasterUser;
    private readonly DatabaseUser? _unprivilegedAppUser;

    private readonly string? _unprivilegedConnectionString;

    public static bool CanRunTests =>
        DataTestUtility.AreConnStringsSetup() && DataTestUtility.IsNotAzureServer()
            && DataTestUtility.CanCreateLogins && DataTestUtility.CanUseSqlAuthentication;

    public UnprivilegedLogin()
    {
        // xUnit will instantiate the class before evaluating the test condition - make sure that we don't
        // attempt to make use of these capabilities if the server doesn't support them.
        // This has the expected nullability implications, so AssertEnvironmentCreated needs to be called
        // before any test logic. The compiler asserts these for us.
        if (!CanRunTests)
        {
            return;
        }

        // We require two connections: a "management" connection which has permissions to create logins,
        // create users and modify permissions; and an "unprivileged" connection, which is used to perform
        // the actual tests. The user associated with the latter connection will be denied SELECT permissions
        // over master.sys.all_columns.
        _managementConnection = new SqlConnection(DataTestUtility.TCPConnectionString);
        _managementConnection.Open();

        _unprivilegedLogin = new ServerLogin(_managementConnection, nameof(UnprivilegedLogin), _managementConnection.Database);
        _unprivilegedAppUser = new DatabaseUser(_managementConnection, _managementConnection.Database, _unprivilegedLogin);
        _unprivilegedMasterUser = new DatabaseUser(_managementConnection, "master", _unprivilegedLogin);

        using (SqlCommand permissionsModificationCommand = _managementConnection.CreateCommand())
        {
            permissionsModificationCommand.CommandText = $"DENY SELECT ON [master].[sys].[all_columns] TO {_unprivilegedMasterUser.Name}";
            permissionsModificationCommand.ExecuteNonQuery();

            permissionsModificationCommand.CommandText = $"DENY SELECT ON [{_managementConnection.Database}].[sys].[all_columns] TO {_unprivilegedAppUser.Name}";
            permissionsModificationCommand.ExecuteNonQuery();
        }

        SqlConnectionStringBuilder tcpConnectionBuilder = new(DataTestUtility.TCPConnectionString)
        {
            IntegratedSecurity = false,
            UserID = _unprivilegedLogin.UnescapedName,
            Password = _unprivilegedLogin.Password,
            // Disable connection pooling - we'll be dropping this login once the tests complete, and this can only happen once all connections
            // using it are closed.
            Pooling = false
        };

        _unprivilegedConnectionString = tcpConnectionBuilder.ConnectionString;
    }

    /// <summary>
    /// This method enables nullability assertions to operate as expected. It'll always pass if CanRunTests is
    /// true - the constructor will have initialized the relevant fields.
    /// </summary>
    [MemberNotNull(nameof(_managementConnection),
        nameof(_unprivilegedLogin),
        nameof(_unprivilegedMasterUser),
        nameof(_unprivilegedAppUser),
        nameof(_unprivilegedConnectionString))]
    private void AssertEnvironmentCreated()
    {
        Assert.NotNull(_managementConnection);
        Assert.NotNull(_unprivilegedLogin);
        Assert.NotNull(_unprivilegedMasterUser);
        Assert.NotNull(_unprivilegedAppUser);
        Assert.NotNull(_unprivilegedConnectionString);
    }

    /// <summary>
    /// Verifies that a bulk copy operation succeeds when performed by a user with only SELECT and INSERT permissions,
    /// without requiring access to metadata views.
    /// </summary>
    [ConditionalFact(nameof(CanRunTests))]
    public void BulkCopyWithoutMetadataPermission_Succeeds()
    {
        AssertEnvironmentCreated();

        const int BulkCopyRowCount = 5;

        using DataTable srcDataTable = new()
        {
            Columns = { new DataColumn("Description", typeof(string)) }
        };
        using Table dstTable = new(_managementConnection, nameof(BulkCopyWithoutMetadataPermission_Succeeds), "([Description] VARCHAR(100))");
        using (SqlCommand permissionsConfigurationCommand = new($"GRANT SELECT, INSERT ON {dstTable.Name} TO {_unprivilegedAppUser.Name}", _managementConnection))
        {
            permissionsConfigurationCommand.ExecuteNonQuery();
        }

        using SqlBulkCopy nodeCopy = new(_unprivilegedConnectionString);

        for (int i = 0; i < BulkCopyRowCount; i++)
        {
            srcDataTable.Rows.Add($"Description {i}");
        }

        nodeCopy.DestinationTableName = dstTable.Name;
        nodeCopy.ColumnMappings.Add("Description", "Description");
        nodeCopy.WriteToServer(srcDataTable);

        int resultantRowCount;
        using (SqlCommand rowCountQuery = new($"SELECT COUNT(*) FROM {dstTable.Name}", _managementConnection))
        {
            resultantRowCount = (int)rowCountQuery.ExecuteScalar();
        }

        Assert.Equal(BulkCopyRowCount, resultantRowCount);
    }

    [ConditionalFact(nameof(CanRunTests))]
    public void BulkCopyWithoutMetadataPermission_FailsWhenUsingAliases()
    {
        AssertEnvironmentCreated();

        using DataTable edges = new DataTable()
        {
            Columns = { new DataColumn("To_ID", typeof(string)), new DataColumn("From_ID", typeof(string)), new DataColumn("Description", typeof(string)) }
        };

        // Use the management connection to create the source and the destination tables, grant the
        // unprivileged user permissions over them and insert some sample data into the source table.
        using Table srcNodeTable = new(_managementConnection, nameof(BulkCopyWithoutMetadataPermission_FailsWhenUsingAliases), "([Name] VARCHAR(100)) AS NODE");
        using Table dstEdgeTable = new(_managementConnection, nameof(BulkCopyWithoutMetadataPermission_FailsWhenUsingAliases), "([Description] VARCHAR(100)) AS EDGE");
        using (SqlCommand permissionsConfigurationCommand = _managementConnection.CreateCommand())
        {
            permissionsConfigurationCommand.CommandText = $"GRANT SELECT, INSERT ON {srcNodeTable.Name} TO {_unprivilegedAppUser.Name}";
            permissionsConfigurationCommand.ExecuteNonQuery();

            permissionsConfigurationCommand.CommandText = $"GRANT SELECT, INSERT ON {dstEdgeTable.Name} TO {_unprivilegedAppUser.Name}";
            permissionsConfigurationCommand.ExecuteNonQuery();
        }

        string sampleNodeDataCommand = @$"INSERT INTO {srcNodeTable.Name} ([Name]) SELECT LEFT([name], 100) FROM sys.sysobjects";
        using (SqlCommand insertSampleNodes = new(sampleNodeDataCommand, _managementConnection))
        {
            insertSampleNodes.ExecuteNonQuery();
        }

        using (SqlCommand nodeQuery = new($"SELECT $node_id FROM {srcNodeTable.Name}", _managementConnection))
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

        // With all source data populated, try to use the unprivileged connection to perform a bulk copy
        // using aliases in the column mappings. This should fail - the permissions error will be caught,
        // and SqlBulkCopy should simply report that the destination column doesn't exist.
        using SqlBulkCopy edgeCopy = new(_unprivilegedConnectionString);

        edgeCopy.DestinationTableName = dstEdgeTable.Name;
        edgeCopy.ColumnMappings.Add("To_ID", "$to_id");
        edgeCopy.ColumnMappings.Add("From_ID", "$from_id");
        edgeCopy.ColumnMappings.Add("Description", "Description");

        Action failingEdgeCopy = () => edgeCopy.WriteToServer(edges);
        InvalidOperationException missingColumnException = Assert.Throws<InvalidOperationException>(failingEdgeCopy);

        Assert.Contains("'$to_id,$from_id'", missingColumnException.Message);
    }

    public void Dispose()
    {
        _unprivilegedAppUser?.Dispose();
        _unprivilegedMasterUser?.Dispose();
        _unprivilegedLogin?.Dispose();
        _managementConnection?.Dispose();
    }
}

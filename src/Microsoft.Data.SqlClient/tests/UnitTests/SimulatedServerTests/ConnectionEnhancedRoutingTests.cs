// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Tests connection routing using the enhanced routing feature extension and envchange token
/// </summary>
[Collection("SimulatedServerTests")]
public class ConnectionEnhancedRoutingTests
{
    [Fact]
    public void RoutedConnection()
    {
        // Arrange
        using TdsServer server = new(new());
        server.Start();

        string routingDatabaseName = Guid.NewGuid().ToString();
        bool clientProvidedCorrectDatabase = false;
        server.OnLogin7Validated = loginToken =>
        {
            clientProvidedCorrectDatabase = routingDatabaseName == loginToken.Database;
        };

        RoutingTdsServer router = new(
            new RoutingTdsServerArguments()
            {
                RoutingTCPHost = "localhost",
                RoutingTCPPort = (ushort)server.EndPoint.Port,
                RoutingDatabaseName = routingDatabaseName,
                RequireReadOnly = false
            });
        router.Start();
        router.EnhancedRoutingBehavior = FeatureExtensionBehavior.Enabled;

        string connectionString = (new SqlConnectionStringBuilder()
        {
            DataSource = $"localhost,{router.EndPoint.Port}",
            Encrypt = false,
            ConnectTimeout = 10000
        }).ConnectionString;

        // Act
        using SqlConnection connection = new(connectionString);
        connection.Open();

        // Assert
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal($"localhost,{server.EndPoint.Port}", ((SqlInternalConnectionTds)connection.InnerConnection).RoutingDestination);
        Assert.Equal(routingDatabaseName, connection.Database);
        Assert.True(clientProvidedCorrectDatabase);

        Assert.Equal(1, router.PreLoginCount);
        Assert.Equal(1, server.PreLoginCount);
    }

    [Fact]
    public async Task RoutedAsyncConnection()
    {
        // Arrange
        using TdsServer server = new(new());
        server.Start();

        string routingDatabaseName = Guid.NewGuid().ToString();
        bool clientProvidedCorrectDatabase = false;
        server.OnLogin7Validated = loginToken =>
        {
            clientProvidedCorrectDatabase = routingDatabaseName == loginToken.Database;
        };

        RoutingTdsServer router = new(
            new RoutingTdsServerArguments()
            {
                RoutingTCPHost = "localhost",
                RoutingTCPPort = (ushort)server.EndPoint.Port,
                RoutingDatabaseName = routingDatabaseName,
                RequireReadOnly = false
            });
        router.Start();
        router.EnhancedRoutingBehavior = FeatureExtensionBehavior.Enabled;

        string connectionString = (new SqlConnectionStringBuilder()
        {
            DataSource = $"localhost,{router.EndPoint.Port}",
            Encrypt = false,
            ConnectTimeout = 10000
        }).ConnectionString;

        // Act
        using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();

        // Assert
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal($"localhost,{server.EndPoint.Port}", ((SqlInternalConnectionTds)connection.InnerConnection).RoutingDestination);
        Assert.Equal(routingDatabaseName, connection.Database);
        Assert.True(clientProvidedCorrectDatabase);

        Assert.Equal(1, router.PreLoginCount);
        Assert.Equal(1, server.PreLoginCount);
    }

    [Fact]
    public void ServerIgnoresEnhancedRoutingRequest()
    {
        // Arrange
        using TdsServer server = new(new());
        server.Start();

        string routingDatabaseName = Guid.NewGuid().ToString();

        RoutingTdsServer router = new(
            new RoutingTdsServerArguments()
            {
                RoutingTCPHost = "localhost",
                RoutingTCPPort = (ushort)server.EndPoint.Port,
                RoutingDatabaseName = routingDatabaseName,
                RequireReadOnly = false
            });
        router.Start();
        router.EnhancedRoutingBehavior = FeatureExtensionBehavior.DoNotAcknowledge;

        string connectionString = new SqlConnectionStringBuilder()
        {
            DataSource = $"localhost,{router.EndPoint.Port}",
            Encrypt = false,
            ConnectTimeout = 10000
        }.ConnectionString;

        // Act
        using SqlConnection connection = new(connectionString);
        connection.Open();

        // Assert
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Null(((SqlInternalConnectionTds)connection.InnerConnection).RoutingDestination);
        Assert.Equal("master", connection.Database);

        Assert.Equal(1, router.PreLoginCount);
        Assert.Equal(0, server.PreLoginCount);
    }

    [Fact]
    public void ServerRejectsEnhancedRoutingRequest()
    {
        // Arrange
        using TdsServer server = new(new());
        server.Start();

        string routingDatabaseName = Guid.NewGuid().ToString();

        RoutingTdsServer router = new(
            new RoutingTdsServerArguments()
            {
                RoutingTCPHost = "localhost",
                RoutingTCPPort = (ushort)server.EndPoint.Port,
                RoutingDatabaseName = routingDatabaseName,
                RequireReadOnly = false
            });
        router.Start();
        router.EnhancedRoutingBehavior = FeatureExtensionBehavior.Disabled;

        string connectionString = new SqlConnectionStringBuilder()
        {
            DataSource = $"localhost,{router.EndPoint.Port}",
            Encrypt = false,
            ConnectTimeout = 10000
        }.ConnectionString;

        // Act
        using SqlConnection connection = new(connectionString);
        connection.Open();

        // Assert
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Null(((SqlInternalConnectionTds)connection.InnerConnection).RoutingDestination);
        Assert.Equal("master", connection.Database);

        Assert.Equal(1, router.PreLoginCount);
        Assert.Equal(0, server.PreLoginCount);
    }
}

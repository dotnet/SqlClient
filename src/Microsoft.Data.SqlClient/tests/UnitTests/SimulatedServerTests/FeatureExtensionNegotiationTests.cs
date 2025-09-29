// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Login7;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

[Collection("SimulatedServerTests")]
public class FeatureExtensionNegotiationTests : IClassFixture<SimulatedServerFixture>
{
    TdsServer server;
    string connectionString;

    public FeatureExtensionNegotiationTests(SimulatedServerFixture fixture)
    {
        server = fixture.TdsServer;
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false
        };
        connectionString = builder.ConnectionString;
    }

    [Fact]
    public void EnhancedRouting_EnabledByServer_ShouldBeEnabled()
    {
        // Arrange
        server.EnableEnhancedRouting = FeatureExtensionEnablementTriState.Enabled;

        bool clientFeatureExtensionFound = false;
        server.OnLogin7Validated = loginToken =>
        {
            var token = loginToken.FeatureExt
                                  .OfType<TDSLogin7GenericOptionToken>()
                                  .FirstOrDefault(t => t.FeatureID == TDSFeatureID.EnhancedRoutingSupport);


            // Test should fail if no UserAgent FE token is found
            Assert.NotNull(token);

            Assert.Equal((byte)TDSFeatureID.EnhancedRoutingSupport, (byte)token.FeatureID);

            clientFeatureExtensionFound = true;
        };

        using SqlConnection sqlConnection = new(connectionString);

        // Act
        sqlConnection.Open();


        // Assert
        Assert.True(clientFeatureExtensionFound, "Client did not request the Enhanced Routing feature extension.");
        Assert.True(((SqlInternalConnectionTds)sqlConnection.InnerConnection).IsEnhancedRoutingSupportEnabled, 
            "Enhanced Routing should be enabled when the server supports it.");
    }

    [Fact]
    public void EnhancedRouting_DisabledByServer_ShouldBeDisabled()
    {
        // Arrange
        server.EnableEnhancedRouting = FeatureExtensionEnablementTriState.Disabled;

        using SqlConnection sqlConnection = new(connectionString);

        // Act
        sqlConnection.Open();


        // Assert
        Assert.False(((SqlInternalConnectionTds)sqlConnection.InnerConnection).IsEnhancedRoutingSupportEnabled,
            "Enhanced Routing should be disabled when the server does not support it.");
    }

    [Fact]
    public void EnhancedRouting_NotAcknowledgedByServer_ShouldBeDisabled()
    {
        // Arrange
        server.EnableEnhancedRouting = FeatureExtensionEnablementTriState.DoNotAcknowledge;

        using SqlConnection sqlConnection = new(connectionString);

        // Act
        sqlConnection.Open();


        // Assert
        Assert.False(((SqlInternalConnectionTds)sqlConnection.InnerConnection).IsEnhancedRoutingSupportEnabled,
            "Enhanced Routing should be disabled when the server does not acknowledge it.");
    }
}

public class SimulatedServerFixture : IDisposable
{
    public SimulatedServerFixture() { 
        TdsServer = new TdsServer();
        TdsServer.Start();
    }

    public void Dispose()
    {
        TdsServer.Dispose();
    }

    public TdsServer TdsServer { get; private set; }
}

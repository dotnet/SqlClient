// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Login7;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Verifies registered application attribution when physical connections are pooled.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public class RegisteredApplicationPoolTests
{
    /// <summary>
    /// Verifies application identity does not split pools and a reused physical connection keeps
    /// the identity reported when that physical connection was created.
    /// </summary>
    [Fact]
    public void DifferentRegisteredApplications_ReusePhysicalConnection_WithOriginalIdentity()
    {
        using TdsServer server = new();
        server.EnableUserAgentFeatureExt = true;
        var payloads = new ConcurrentQueue<byte[]>();
        server.OnLogin7Validated = login => payloads.Enqueue(GetUserAgentPayload(login));
        server.Start();

        string connectionString = new SqlConnectionStringBuilder
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = true,
        }.ConnectionString;

        using var firstConnection = new SqlConnection(connectionString)
        {
            RegisteredApplication = RegisteredApplication.EntityFrameworkCore,
        };

        try
        {
            firstConnection.Open();
            firstConnection.Close();

            using var secondConnection = new SqlConnection(connectionString)
            {
                RegisteredApplication = RegisteredApplication.SemanticKernel,
            };
            secondConnection.Open();

            Assert.Equal(1, server.Login7Count);
            Assert.Single(payloads);
            Assert.True(payloads.TryDequeue(out byte[]? payload));
            Assert.Equal(UserAgent.GetUcs2Bytes(RegisteredApplication.EntityFrameworkCore).ToArray(), payload);
        }
        finally
        {
            SqlConnection.ClearPool(firstConnection);
        }
    }

    /// <summary>
    /// Verifies a physical connection created by channel-pool background warmup reports the
    /// unknown application because no logical connection owns the creation.
    /// </summary>
    [Fact]
    public async Task MinPoolSizeBackgroundConnection_ReportsUnknownApplication()
    {
        using TdsServer server = new();
        server.EnableUserAgentFeatureExt = true;
        var payloads = new ConcurrentQueue<byte[]>();
        server.OnLogin7Validated = login => payloads.Enqueue(GetUserAgentPayload(login));
        server.Start();

        string connectionString = new SqlConnectionStringBuilder
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = true,
            MinPoolSize = 1,
            MaxPoolSize = 1,
        }.ConnectionString;
        var connectionOptions = new SqlConnectionOptions(connectionString);
        var poolOptions = new DbConnectionPoolGroupOptions(
            poolByIdentity: false,
            minPoolSize: 1,
            maxPoolSize: 1,
            creationTimeout: 15,
            loadBalanceTimeout: 0,
            hasTransactionAffinity: true,
            idleTimeout: 0);
        var poolKey = new ConnectionPoolKey(
            connectionString,
            credential: null,
            accessToken: null,
            accessTokenCallback: null,
            sspiContextProvider: null);
        var poolGroup = new DbConnectionPoolGroup(connectionOptions, poolKey, poolOptions);
        using var pool = new ChannelDbConnectionPool(
            SqlConnectionFactory.Instance,
            poolGroup,
            DbConnectionPoolIdentity.NoIdentity,
            new SqlConnectionPoolProviderInfo());

        pool.Startup();
        await pool.WarmupLoopTask!;

        Assert.Equal(1, server.Login7Count);
        Assert.Single(payloads);
        Assert.True(payloads.TryDequeue(out byte[]? payload));
        Assert.Equal(UserAgent.GetUcs2Bytes(RegisteredApplication.Unknown).ToArray(), payload);
    }

    /// <summary>
    /// Extracts the USERAGENT extension payload from a validated LOGIN7 token.
    /// </summary>
    /// <param name="login">The validated LOGIN7 token.</param>
    /// <returns>The USERAGENT payload bytes.</returns>
    private static byte[] GetUserAgentPayload(TDSLogin7Token login) =>
        login.FeatureExt
            .OfType<TDSLogin7GenericOptionToken>()
            .Single(option => option.FeatureID == TDSFeatureID.UserAgentSupport)
            .Data
            .ToArray();
}

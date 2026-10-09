// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.PerformanceTests;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>Checks the sustained workload's successful-open accounting against ordinary, ungated TDS logins.</summary>
[Collection(SimulatedServerTestCollection.Name)]
public class ConnectivityLoadTests
{
    /// <summary>Non-pooled operations each perform a physical login; pooled operations may reuse connections.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenAsyncLoop_CountsCompletedOpens(bool pooling)
    {
        using var server = new TdsServer();
        server.Start();
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = pooling,
            Enlist = false,
            ConnectTimeout = 10
        };
        string connectionString = builder.ConnectionString;
        long completed = 0;
        try
        {
            ConnectivityLoadResult result = await ConnectivityLoad.RunAsync(
                async () =>
                {
                    using var connection = new SqlConnection(connectionString);
                    await connection.OpenAsync();
                    Interlocked.Increment(ref completed);
                },
                2, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(10));

            Assert.Equal(completed, result.SuccessfulOpens);
            Assert.True(completed > 0);
            Assert.True(server.Login7Count > 0);
            if (pooling)
            {
                Assert.True(server.Login7Count <= completed);
            }
            else
            {
                Assert.Equal(completed, server.Login7Count);
            }
        }
        finally
        {
            using var connection = new SqlConnection(connectionString);
            SqlConnection.ClearPool(connection);
        }
    }
}
#endif

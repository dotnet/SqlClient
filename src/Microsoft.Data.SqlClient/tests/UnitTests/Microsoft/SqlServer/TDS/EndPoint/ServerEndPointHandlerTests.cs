// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies the simulator listener can be stopped without an incoming connection.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public class ServerEndPointHandlerTests
{
    /// <summary>
    /// Closing an idle listener must interrupt its accept operation on every platform.
    /// </summary>
    [Fact]
    public async Task Stop_IdleListener_Completes()
    {
        // Arrange
        using TdsServer server = new();
        IdleEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();

        // Act
        Task stopping = Task.Run(endpoint.Stop);
        Task completed = await Task.WhenAny(stopping, Task.Delay(TimeSpan.FromSeconds(10)));

        // Assert
        Assert.Same(stopping, completed);
        await stopping;
    }

    /// <summary>
    /// Rejects unexpected clients so the test only exercises listener shutdown.
    /// </summary>
    private sealed class IdleEndPoint : ServerEndPointHandler<ServerEndPointConnection>
    {
        public IdleEndPoint(TdsServer server) : base(server)
        {
        }

        public override ServerEndPointConnection CreateConnection(TcpClient newConnection) =>
            throw new InvalidOperationException("The idle listener must not receive a connection.");
    }
}

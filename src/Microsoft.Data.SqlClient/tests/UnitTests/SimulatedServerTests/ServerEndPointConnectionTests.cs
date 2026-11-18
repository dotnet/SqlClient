// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Verifies ownership of the simulator transport during worker startup and shutdown.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public class ServerEndPointConnectionTests
{
    /// <summary>
    /// Shutdown must not close a transport while its worker is still initializing it.
    /// </summary>
    [Fact]
    public void Dispose_StartupInProgress_WaitsBeforeClosingTransport()
    {
        // Arrange
        using TdsServer server = new();
        using GatedEndPoint endpoint = new(server)
        {
            ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
        };
        endpoint.Start();
        using TcpClient peer = new();
        peer.Connect(IPAddress.Loopback, endpoint.ServerEndPoint.Port);
        Assert.True(endpoint.Created.Task.Wait(TimeSpan.FromSeconds(30)),
            "The listener did not create the connection.");
        GatedConnection connection = endpoint.Created.Task.Result;
        using ManualResetEventSlim startupEntered = connection.StartupEntered;
        using ManualResetEventSlim resumeStartup = connection.ResumeStartup;
        Exception? disposalError = null;
        Thread disposing = new(() =>
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception error)
            {
                disposalError = error;
            }
        }) { IsBackground = true };

        try
        {
            Assert.True(connection.StartupEntered.Wait(TimeSpan.FromSeconds(30)),
                "The worker did not enter transport initialization.");
            Assert.True(SpinWait.SpinUntil(() => connection.HasProcessorTask,
                TimeSpan.FromSeconds(30)), "The listener did not publish the worker task.");

            // Act
            disposing.Start();
            // Both the old task wait and the repaired startup lock block this thread.
            // Observe that boundary rather than assuming it ran after a timed delay.
            Assert.True(SpinWait.SpinUntil(
                () => (disposing.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(10)), "Disposal did not reach a blocking wait.");

            // Assert
            Assert.True(connection.StartupStream!.CanRead,
                "Disposal closed the transport before initialization completed.");
        }
        finally
        {
            // Cleanup
            connection.ResumeStartup.Set();
            if ((disposing.ThreadState & ThreadState.Unstarted) == 0)
            {
                Assert.True(disposing.Join(TimeSpan.FromSeconds(30)),
                    "Disposal did not drain the connection worker.");
            }
        }

        Assert.Null(disposalError);
        Assert.True(connection.Closed.Task.IsCompleted,
            "Disposal returned before the connection closed.");
    }

    /// <summary>
    /// Exposes the accepted connection without changing the listener's ownership protocol.
    /// </summary>
    private sealed class GatedEndPoint : ServerEndPointHandler<GatedConnection>
    {
        public TaskCompletionSource<GatedConnection> Created { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public GatedEndPoint(ITDSServer server) : base(server)
        {
        }

        public override GatedConnection CreateConnection(TcpClient client)
        {
            GatedConnection connection = new(TDSServer, client);
            Created.SetResult(connection);
            return connection;
        }
    }

    /// <summary>
    /// Holds the real worker at its existing transport-initialization boundary.
    /// </summary>
    private sealed class GatedConnection : ServerEndPointConnection
    {
        public ManualResetEventSlim StartupEntered { get; } = new();
        public ManualResetEventSlim ResumeStartup { get; } = new();
        public TaskCompletionSource<bool> Closed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Stream? StartupStream { get; private set; }
        public bool HasProcessorTask => ProcessorTask != null;

        public GatedConnection(ITDSServer server, TcpClient client) : base(server, client)
        {
            OnConnectionClosed += (_, _) => Closed.TrySetResult(true);
        }

        public override void PrepareForProcessingData(Stream rawStream)
        {
            StartupStream = rawStream;
            StartupEntered.Set();
            if (!ResumeStartup.Wait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("Transport initialization was not released.");
            }
        }

        public override void ProcessData(Stream rawStream)
        {
            throw new InvalidOperationException("The idle peer must not send protocol data.");
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

#nullable enable

using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManagedSni;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Verifies that parallel SQL Browser requests do not depend on the caller's scheduler.
    /// Runs without other collections so unrelated blocking work cannot starve UDP completions
    /// during the transport's one-second receive timeout.
    /// </summary>
    [Collection(SimulatedServerTestCollection.Name)]
    public sealed class SsrpClientTests
    {
        /// <summary>
        /// Parallel requests must return the peer's response when their caller occupies the
        /// only worker of an exclusive scheduler, for both single- and multiple-address queries.
        /// </summary>
        /// <param name="addressCount">The number of loopback addresses to query in parallel.</param>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task SendUDPRequest_SingleWorkerScheduler_ReturnsResponse(int addressCount)
        {
            // Arrange
            using UdpClient server = new(new IPEndPoint(IPAddress.Loopback, 0));
            server.Client.ReceiveTimeout = 10000;
            int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
            IPAddress[] addresses = new IPAddress[addressCount];
            Array.Fill(addresses, IPAddress.Loopback);
            byte[] requestPacket = [0x04, 0x00];
            byte[] responsePacket = [0x05, 0x01, 0x00, 0x42];
            // Keep the responder independent of the client's blocking thread-pool workers.
            Task reply = Task.Factory.StartNew(() =>
            {
                IPEndPoint peer = new(IPAddress.Any, 0);
                server.Receive(ref peer);
                server.Send(responsePacket, responsePacket.Length, peer);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            ConcurrentExclusiveSchedulerPair schedulerPair = new(
                TaskScheduler.Default, maxConcurrencyLevel: 1);

            try
            {
                // Act
                Task<(byte[]? ResponsePacket, Exception? Error)> request = Task.Factory.StartNew(
                    () => SendParallelRequest(addresses, port, requestPacket),
                    CancellationToken.None, TaskCreationOptions.None,
                    schedulerPair.ExclusiveScheduler);
                (byte[]? response, Exception? error) = await request.WaitAsync(
                    TimeSpan.FromSeconds(10));

                // Assert
                Assert.Null(error);
                Assert.Equal(responsePacket, response);
                await reply;
            }
            finally
            {
                // Cleanup
                schedulerPair.Complete();
            }
        }

        /// <summary>
        /// A silent SQL Browser peer must let the request time out even when the caller occupies
        /// the only worker of an exclusive scheduler.
        /// </summary>
        [Fact]
        public async Task SendUDPRequest_SingleWorkerSchedulerSilentPeer_ReturnsNoResponse()
        {
            // Arrange
            using UdpClient server = new(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
            ConcurrentExclusiveSchedulerPair schedulerPair = new(
                TaskScheduler.Default, maxConcurrencyLevel: 1);

            try
            {
                // Act
                Task<(byte[]? ResponsePacket, Exception? Error)> request = Task.Factory.StartNew(
                    () => SendParallelRequest([IPAddress.Loopback], port, [0x04, 0x00]),
                    CancellationToken.None, TaskCreationOptions.None,
                    schedulerPair.ExclusiveScheduler);
                (byte[]? response, Exception? error) = await request.WaitAsync(
                    TimeSpan.FromSeconds(10));

                // Assert
                Assert.Null(error);
                Assert.Null(response);
            }
            finally
            {
                // Cleanup
                schedulerPair.Complete();
            }
        }

        /// <summary>
        /// Calls the parallel transport overload so the peer can use an ephemeral UDP port
        /// without binding the system SQL Browser port or changing production visibility.
        /// </summary>
        /// <param name="addresses">The peer addresses to query.</param>
        /// <param name="port">The peer's ephemeral UDP port.</param>
        /// <param name="requestPacket">The datagram to send.</param>
        /// <returns>The response and any transport error reported by SSRP.</returns>
        private static (byte[]? ResponsePacket, Exception? Error) SendParallelRequest(
            IPAddress[] addresses, int port, byte[] requestPacket)
        {
            MethodInfo method = typeof(SsrpClient).GetMethod(
                "SendUDPRequest", BindingFlags.Static | BindingFlags.NonPublic, binder: null,
                types: [typeof(IPAddress[]), typeof(int), typeof(byte[]), typeof(bool)],
                modifiers: null) ?? throw new MissingMethodException(
                    typeof(SsrpClient).FullName, "SendUDPRequest");
            object result = method.Invoke(null, [addresses, port, requestPacket, true])
                ?? throw new InvalidOperationException("SSRP returned no transport result.");
            FieldInfo responseField = result.GetType().GetField("ResponsePacket")
                ?? throw new MissingFieldException(result.GetType().FullName, "ResponsePacket");
            FieldInfo errorField = result.GetType().GetField("Error")
                ?? throw new MissingFieldException(result.GetType().FullName, "Error");
            return ((byte[]?)responseField.GetValue(result),
                (Exception?)errorField.GetValue(result));
        }
    }
}

#endif

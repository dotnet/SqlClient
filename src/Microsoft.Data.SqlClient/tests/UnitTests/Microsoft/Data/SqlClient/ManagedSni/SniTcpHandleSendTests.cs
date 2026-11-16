// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ManagedSni;
using Microsoft.Data.SqlClient.Parser;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Verifies TCP sends retain their error and packet ownership contracts during teardown.
    /// </summary>
    public sealed class SniTcpHandleSendTests
    {
        /// <summary>
        /// A send ordered after transport disposal must report closure instead of dereferencing
        /// the cleared stream, including the out-of-band path used by attention packets.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Send_AfterDispose_ReturnsConnectionError(bool outOfBand)
        {
            // Arrange
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            SniTcpHandle handle = CreateHandle(listener);
            using Socket peer = listener.AcceptSocket();
            SniPacket packet = handle.RentPacket(0, 1);
            packet.AppendData(new byte[] { 42 }, 1);
            packet.IsOutOfBand = outOfBand;
            handle.Dispose();

            try
            {
                // Act
                uint result = handle.Send(packet);

                // Assert
                Assert.Equal(TdsEnums.SNI_ERROR, result);
                Assert.Equal(SniProviders.TCP_PROV, SniLoadHandle.LastError.provider);
                Assert.IsType<ObjectDisposedException>(SniLoadHandle.LastError.exception);
                Assert.False(packet.IsInvalid);
            }
            finally
            {
                // Cleanup
                handle.ReturnPacket(packet);
                handle.Dispose();
            }
        }

        /// <summary>
        /// A connected send still transmits its bytes and leaves the packet owned by its caller.
        /// </summary>
        [Fact]
        public void Send_Connected_WritesPacket()
        {
            // Arrange
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            SniTcpHandle handle = CreateHandle(listener);
            using Socket peer = listener.AcceptSocket();
            peer.ReceiveTimeout = 5000;
            SniPacket packet = handle.RentPacket(0, 1);
            packet.AppendData(new byte[] { 42 }, 1);

            try
            {
                // Act
                uint result = handle.Send(packet);

                // Assert
                Assert.Equal(TdsEnums.SNI_SUCCESS, result);
                byte[] received = new byte[1];
                Assert.Equal(1, peer.Receive(received));
                Assert.Equal(42, received[0]);
                Assert.False(packet.IsInvalid);
            }
            finally
            {
                // Cleanup
                handle.ReturnPacket(packet);
                handle.Dispose();
            }
        }

        /// <summary>
        /// Connects a managed TCP handle to a local listener without a SQL Server handshake.
        /// </summary>
        /// <param name="listener">The listening loopback endpoint.</param>
        /// <returns>The connected handle owned by the caller.</returns>
        private static SniTcpHandle CreateHandle(TcpListener listener)
        {
            SQLDNSInfo? dnsInfo = null;
            SniTcpHandle handle = new(
                IPAddress.Loopback.ToString(),
                ((IPEndPoint)listener.LocalEndpoint).Port,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(5)),
                parallel: false,
                SqlConnectionIPAddressPreference.IPv4First,
                cachedFQDN: nameof(SniTcpHandleSendTests),
                ref dnsInfo,
                tlsFirst: false,
                hostNameInCertificate: null,
                serverCertificateFilename: null);
            Assert.Equal(TdsEnums.SNI_SUCCESS, handle.Status);
            return handle;
        }
    }
}

#endif

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ManagedSni;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Tests for <see cref="SniTcpHandle.ParallelConnect"/>, used when MultiSubnetFailover is enabled.
    /// </summary>
    public class SniTcpHandleParallelConnectTests
    {
        // TCP connect to a multicast or broadcast address fails immediately instead of
        // completing asynchronously; 2001:db8::/32 is a documentation-only range.
        [Theory]
        [InlineData("224.0.0.1", true)]
        [InlineData("224.0.0.1", false)]
        [InlineData("255.255.255.255", true)]
        [InlineData("2001:db8::1", true)]
        public void ParallelConnect_OneBadAddress_ConnectsToGoodAddress(string badAddress, bool badAddressFirst)
        {
            IPAddress bad = IPAddress.Parse(badAddress);
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            IPAddress[] addresses = badAddressFirst
                ? [bad, IPAddress.Loopback]
                : [IPAddress.Loopback, bad];

            SQLDNSInfo? dnsInfo = null;
            using Socket socket = SniTcpHandle.ParallelConnect(
                addresses, port, TimeoutTimer.StartNew(TimeSpan.FromSeconds(10)), "localhost", ref dnsInfo);

            Assert.NotNull(socket);
            Assert.True(socket.Connected);
            Assert.Equal(IPAddress.Loopback, ((IPEndPoint)socket.RemoteEndPoint!).Address);
            Assert.NotNull(dnsInfo);
            Assert.Equal(IPAddress.Loopback.ToString(), dnsInfo.AddrIPv4);
        }

        // When no address accepts the connection, no socket is returned and no DNS info is
        // recorded, so the caller reports a connection failure.
        [Fact]
        public void ParallelConnect_AllAddressesFail_ReturnsNoSocket()
        {
            // Bind and release a port so nothing is listening on it.
            int port;
            using (TcpListener listener = new(IPAddress.Loopback, 0))
            {
                listener.Start();
                port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
            }

            SQLDNSInfo? dnsInfo = null;
            Socket? socket = null;
            Exception? ex = Record.Exception(() => socket = SniTcpHandle.ParallelConnect(
                [IPAddress.Loopback], port, TimeoutTimer.StartNew(TimeSpan.FromSeconds(10)), "localhost", ref dnsInfo));

            // Depending on the platform, a refused connection either throws or returns null;
            // the caller treats a null socket as a connection failure.
            if (ex is not null)
            {
                Assert.IsType<SocketException>(ex);
            }
            Assert.Null(socket);
            Assert.Null(dnsInfo);
        }
    }
}

#endif

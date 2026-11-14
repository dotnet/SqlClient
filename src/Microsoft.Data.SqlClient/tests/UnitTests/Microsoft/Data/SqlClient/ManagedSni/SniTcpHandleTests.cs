// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ManagedSni;
using Microsoft.Data.SqlClient.Parser;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Covers the liveness check of a managed TCP handle, run on every connection taken from the pool.
    /// </summary>
    public sealed class SniTcpHandleTests
    {
        /// <summary>
        /// The check on an open, idle connection succeeds without waiting. A positive poll timeout is
        /// rounded up to the timer tick on Windows (15.6 ms by default), so twenty checks that waited
        /// would take about 300 ms instead of well under the bound.
        /// </summary>
        [Fact]
        public void CheckConnection_IdleOpenConnection_ReturnsSuccessWithoutWaiting()
        {
            // Arrange
            using LocalConnection connection = new();
            const int Checks = 20;
            uint[] results = new uint[Checks];

            // Act
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < Checks; i++)
            {
                results[i] = connection.Handle.CheckConnection();
            }
            stopwatch.Stop();

            // Assert
            Assert.All(results, result => Assert.Equal(TdsEnums.SNI_SUCCESS, result));
            Assert.True(
                stopwatch.ElapsedMilliseconds < 100,
                $"{Checks} checks took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// The check reports a connection the peer has closed, so the pool discards it instead of
        /// handing it out. The peer's close reaches the client asynchronously, so the check is repeated
        /// until it fails or a deadline passes.
        /// </summary>
        [Fact]
        public void CheckConnection_PeerClosed_ReturnsError()
        {
            // Arrange
            using LocalConnection connection = new();
            connection.Peer.Close();

            // Act
            uint result = TdsEnums.SNI_SUCCESS;
            Stopwatch deadline = Stopwatch.StartNew();
            while (result == TdsEnums.SNI_SUCCESS && deadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                result = connection.Handle.CheckConnection();
                if (result == TdsEnums.SNI_SUCCESS)
                {
                    Thread.Sleep(10);
                }
            }

            // Assert
            Assert.Equal(TdsEnums.SNI_ERROR, result);
        }

        /// <summary>
        /// Opens a managed TCP handle to a local listener and owns both ends.
        /// </summary>
        private sealed class LocalConnection : IDisposable
        {
            public SniTcpHandle Handle { get; }

            public Socket Peer { get; }

            public LocalConnection()
            {
                using TcpListener listener = new(IPAddress.Loopback, 0);
                listener.Start();
                SQLDNSInfo? pendingDnsInfo = null;
                Handle = new SniTcpHandle(
                    IPAddress.Loopback.ToString(),
                    ((IPEndPoint)listener.LocalEndpoint).Port,
                    TimeoutTimer.StartNew(TimeSpan.FromSeconds(5)),
                    parallel: false,
                    SqlConnectionIPAddressPreference.IPv4First,
                    cachedFQDN: nameof(SniTcpHandleTests),
                    ref pendingDnsInfo,
                    tlsFirst: false,
                    hostNameInCertificate: null,
                    serverCertificateFilename: null);
                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
                Peer = listener.AcceptSocketAsync(timeout.Token).AsTask().GetAwaiter().GetResult();
                Assert.Equal(TdsEnums.SNI_SUCCESS, Handle.Status);
            }

            /// <summary>
            /// Closes the client handle and the accepted socket.
            /// </summary>
            public void Dispose()
            {
                Handle.Dispose();
                Peer.Dispose();
            }
        }
    }
}

#endif

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ManagedSni;
using Microsoft.Data.SqlClient.Parser;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Tests for <see cref="SniTcpHandle"/>.
    /// </summary>
    public sealed class SniTcpHandleTests
    {
        /// <summary>
        /// Upper bound for EnableSsl to return. Generous, so it only separates "honors the token"
        /// from "blocks until the operating system gives up on the socket".
        /// </summary>
        private static readonly TimeSpan HandshakeBudget = TimeSpan.FromSeconds(30);

        /// <summary>
        /// When the peer accepts the TLS ClientHello and never answers, EnableSsl must stop the
        /// handshake once its token is cancelled and report an SNI timeout error, for both TLS
        /// inside TDS and TLS-first (strict encryption). This is what bounds the handshake by
        /// Connect Timeout (GitHub issues #4834 and #5045).
        /// </summary>
        /// <param name="tlsFirst">Whether the handle negotiates TLS before any TDS traffic.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task EnableSsl_PeerNeverAnswersHandshake_ReturnsTimeoutErrorWhenTokenIsCancelled(bool tlsFirst)
        {
            // Arrange
            // Connect the handle to a local peer that will stay silent.
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            SQLDNSInfo? pendingDnsInfo = null;
            SniTcpHandle handle = new(
                IPAddress.Loopback.ToString(),
                ((IPEndPoint)listener.LocalEndpoint).Port,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(5)),
                parallel: false,
                SqlConnectionIPAddressPreference.IPv4First,
                cachedFQDN: nameof(SniTcpHandleTests),
                ref pendingDnsInfo,
                tlsFirst,
                hostNameInCertificate: null,
                serverCertificateFilename: null);
            using CancellationTokenSource acceptTimeout = new(TimeSpan.FromSeconds(5));
            using Socket peer = await listener.AcceptSocketAsync(acceptTimeout.Token);
            Assert.Equal(TdsEnums.SNI_SUCCESS, handle.Status);
            using CancellationTokenSource handshakeTimeout = new(TimeSpan.FromMilliseconds(500));

            try
            {
                // Act
                // SNI errors are thread-static, so read the error on the thread that ran EnableSsl.
                Task<(uint Result, SniError Error)> enableSsl = Task.Run(
                    () => (handle.EnableSsl(0, handshakeTimeout.Token), SniLoadHandle.LastError));
                Task settled = await Task.WhenAny(enableSsl, Task.Delay(HandshakeBudget));

                // Assert
                Assert.True(
                    ReferenceEquals(enableSsl, settled),
                    $"EnableSsl did not return within {HandshakeBudget.TotalSeconds:N0}s after its token was cancelled.");
                (uint result, SniError error) = await enableSsl;
                Assert.Equal(TdsEnums.SNI_ERROR, result);
                Assert.Equal(SniProviders.TCP_PROV, error.provider);
                Assert.Equal((uint)SniCommon.ConnTimeoutError, error.sniError);
                Assert.Equal((int)TdsEnums.SNI_WAIT_TIMEOUT, error.nativeError);
            }
            finally
            {
                // Cleanup
                // Disposing the handle also releases a handshake that ignored the token.
                handle.Dispose();
            }
        }

        /// <summary>
        /// The liveness check reports a connection the peer has closed, so the pool discards it
        /// instead of handing it out. The peer's close reaches the client asynchronously, so the
        /// check is repeated until it fails or a deadline passes.
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

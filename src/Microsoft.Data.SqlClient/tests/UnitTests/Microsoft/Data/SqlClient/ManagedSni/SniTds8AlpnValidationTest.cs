// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManagedSni;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// With Encrypt=Strict (TDS 8.0) the client offers the ALPN protocol "tds/8.0" in its
    /// TLS ClientHello. These tests verify the managed SNI layer rejects a handshake in which
    /// the server did not select "tds/8.0", instead of silently continuing over TLS without
    /// the protocol binding TDS 8.0 depends on.
    /// </summary>
    /// <remarks>
    /// The end-to-end cases force the process-wide UseManagedNetworking switch on Windows, so this
    /// class runs in the non-parallel AppContext switch collection to avoid affecting concurrent
    /// connection tests.
    /// </remarks>
    [Collection(AppContextSwitchTestCollection.Name)]
    public class SniTds8AlpnValidationTest
    {
        private static readonly SslApplicationProtocol s_tds8 = new(TdsEnums.TDS8_Protocol);

        /// <summary>
        /// Accepts the exact protocol identifier offered by strict-mode managed SNI.
        /// </summary>
        [Fact]
        public void ValidateNegotiatedApplicationProtocol_Tds8_DoesNotThrow()
        {
            // Act
            Exception exception = Record.Exception(
                () => SniHandle.ValidateNegotiatedApplicationProtocol(s_tds8));

            // Assert
            Assert.Null(exception);
        }

        /// <summary>
        /// Rejects other protocols, including case variants, with lossless hex diagnostics.
        /// </summary>
        [Theory]
        [InlineData("h2")]
        [InlineData("http/1.1")]
        [InlineData("tds/7.4")]
        [InlineData("TDS/8.0")]
        public void ValidateNegotiatedApplicationProtocol_OtherProtocol_Throws(string protocol)
        {
            // Arrange
            SslApplicationProtocol negotiated = new(protocol);

            // Act
            AuthenticationException ex = Assert.Throws<AuthenticationException>(
                () => SniHandle.ValidateNegotiatedApplicationProtocol(negotiated));

            // Assert
            Assert.Contains($"'{Convert.ToHexString(negotiated.Protocol.Span)}'", ex.Message);
        }

        public static TheoryData<byte[], string> OpaqueProtocols => new()
        {
            { Array.Empty<byte>(), "" },
            { new byte[] { 0 }, "00" },
            { new byte[] { 13, 10, 9 }, "0D0A09" },
            { new byte[] { 0x80, 0xFF }, "80FF" },
            { new byte[] { 0x74, 0x64, 0x73, 0, 0x38 }, "7464730038" },
        };

        /// <summary>
        /// Rejects opaque ALPN identifiers without injecting control bytes or lossy text into errors.
        /// </summary>
        [Theory]
        [MemberData(nameof(OpaqueProtocols))]
        public void ValidateNegotiatedApplicationProtocol_OpaqueBytes_ThrowsWithHexMessage(
            byte[] protocol, string expectedHex)
        {
            // Arrange
            SslApplicationProtocol negotiated = protocol.Length == 0
                ? default
                : new SslApplicationProtocol(protocol);

            // Act
            AuthenticationException ex = Assert.Throws<AuthenticationException>(
                () => SniHandle.ValidateNegotiatedApplicationProtocol(negotiated));

            // Assert
            Assert.Contains("'7464732F382E30'", ex.Message);
            Assert.Contains($"'{expectedHex}'", ex.Message);
            Assert.DoesNotContain("\0", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("\n", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("\t", ex.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Open sends PRELOGIN after the server selects TDS 8.0, without waiting for a timeout.
        /// </summary>
        [Fact]
        public async Task Open_Tds8Negotiated_SendsPrelogin()
        {
            // Arrange
            using TlsFirstServer server = new(selectTds8: true);
            using LocalAppContextSwitchesHelper switches = new() { UseManagedNetworking = true };
            using SqlConnection connection = server.CreateConnection();

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());
            int firstByte = await server.FirstByte;

            // Assert
            // The server closes after PRELOGIN, so Open fails for a reason other than ALPN.
            Assert.Equal(0x12, firstByte);
            Assert.DoesNotContain("7464732F382E30", ex.ToString());
        }

        /// <summary>
        /// OpenAsync sends PRELOGIN after the server selects TDS 8.0.
        /// </summary>
        [Fact]
        public async Task OpenAsync_Tds8Negotiated_SendsPrelogin()
        {
            // Arrange
            using TlsFirstServer server = new(selectTds8: true);
            using LocalAppContextSwitchesHelper switches = new() { UseManagedNetworking = true };
            using SqlConnection connection = server.CreateConnection();

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());
            int firstByte = await server.FirstByte;

            // Assert
            Assert.Equal(0x12, firstByte);
            Assert.DoesNotContain("7464732F382E30", ex.ToString());
        }

        /// <summary>
        /// Open rejects a TLS handshake without ALPN before sending any TDS bytes.
        /// </summary>
        [Fact]
        public async Task Open_NoAlpn_ThrowsBeforePrelogin()
        {
            // Arrange
            using TlsFirstServer server = new(selectTds8: false);
            using LocalAppContextSwitchesHelper switches = new() { UseManagedNetworking = true };
            using SqlConnection connection = server.CreateConnection();

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());
            int firstByte = await server.FirstByte;

            // Assert
            Assert.Equal(-1, firstByte);
            AuthenticationException inner = Assert.IsType<AuthenticationException>(ex.InnerException);
            Assert.Contains("'7464732F382E30'", inner.Message);
        }

        /// <summary>
        /// OpenAsync rejects a TLS handshake without ALPN before sending any TDS bytes.
        /// </summary>
        [Fact]
        public async Task OpenAsync_NoAlpn_ThrowsBeforePrelogin()
        {
            // Arrange
            using TlsFirstServer server = new(selectTds8: false);
            using LocalAppContextSwitchesHelper switches = new() { UseManagedNetworking = true };
            using SqlConnection connection = server.CreateConnection();

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());
            int firstByte = await server.FirstByte;

            // Assert
            Assert.Equal(-1, firstByte);
            AuthenticationException inner = Assert.IsType<AuthenticationException>(ex.InnerException);
            Assert.Contains("'7464732F382E30'", inner.Message);
        }

        /// <summary>
        /// Accepts one connection, performs the server side of a TLS-first handshake, and
        /// returns the first byte the client sends afterwards (-1 if it sent nothing), then closes
        /// the socket so the client need not wait for its connection timeout.
        /// </summary>
        /// <param name="listener">The loopback listener owned by the test server.</param>
        /// <param name="cert">The server certificate pinned by the client.</param>
        /// <param name="selectTds8">Whether the server offers the TDS 8.0 protocol.</param>
        /// <param name="token">Bounds accepting, handshaking and reading.</param>
        /// <returns>The first decrypted byte, or -1 if the client closed without sending data.</returns>
        private static async Task<int> RunTlsFirstServerAsync(
            TcpListener listener, X509Certificate2 cert, bool selectTds8, CancellationToken token)
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(token);
            using SslStream ssl = new(client.GetStream(), leaveInnerStreamOpen: false);
            SslServerAuthenticationOptions options = new()
            {
                ServerCertificate = cert,
                ClientCertificateRequired = false,
            };
            if (selectTds8)
            {
                options.ApplicationProtocols = new() { s_tds8 };
            }

            await ssl.AuthenticateAsServerAsync(options, token);
            Assert.Equal(selectTds8 ? s_tds8 : default, ssl.NegotiatedApplicationProtocol);
            try
            {
                byte[] buffer = new byte[1];
                int read = await ssl.ReadAsync(buffer, token);
                return read == 0 ? -1 : buffer[0];
            }
            catch (IOException)
            {
                // The rejected client may reset the socket instead of sending TLS close_notify.
                return -1;
            }
        }

        /// <summary>
        /// Owns a loopback TLS-first endpoint, certificate pin and bounded server task for one test.
        /// </summary>
        private sealed class TlsFirstServer : IDisposable
        {
            private readonly X509Certificate2 _certificate = TestServerCertificate.Create();
            private readonly TempCertFile _pin;
            private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(10));

            public Task<int> FirstByte { get; }

            /// <summary>Starts the server with the requested ALPN offer.</summary>
            /// <param name="selectTds8">Whether to offer TDS 8.0 instead of omitting ALPN.</param>
            public TlsFirstServer(bool selectTds8)
            {
                _pin = new(_certificate);
                _listener.Start();
                FirstByte = RunTlsFirstServerAsync(
                    _listener, _certificate, selectTds8, _timeout.Token);
            }

            /// <summary>Creates a nonpooled strict connection pinned to this server.</summary>
            /// <returns>A connection owned by the caller.</returns>
            public SqlConnection CreateConnection()
            {
                int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                SqlConnectionStringBuilder builder = new()
                {
                    DataSource = $"tcp:127.0.0.1,{port}",
                    Encrypt = SqlConnectionEncryptOption.Strict,
                    HostNameInCertificate = "localhost",
                    ServerCertificate = _pin.Path,
                    ConnectTimeout = 5,
                    ConnectRetryCount = 0,
                    Pooling = false,
                };
                return new(builder.ConnectionString);
            }

            /// <summary>Stops the endpoint and releases the temporary certificate resources.</summary>
            public void Dispose()
            {
                _timeout.Cancel();
                _listener.Stop();
                _timeout.Dispose();
                _certificate.Dispose();
                _pin.Dispose();
            }
        }
    }
}

#endif

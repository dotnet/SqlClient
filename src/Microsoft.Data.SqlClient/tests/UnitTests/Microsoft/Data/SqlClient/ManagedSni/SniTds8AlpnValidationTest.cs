// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManagedSni;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// With Encrypt=Strict (TDS 8.0) the client offers the ALPN protocol "tds/8.0" in its
    /// TLS ClientHello. These tests verify the managed SNI layer rejects a handshake in which
    /// the server did not select "tds/8.0", instead of silently continuing over TLS without
    /// the protocol binding TDS 8.0 depends on.
    /// </summary>
    public class SniTds8AlpnValidationTest
    {
        private static readonly SslApplicationProtocol s_tds8 = new(TdsEnums.TDS8_Protocol);

        [Fact]
        public void ValidateNegotiatedApplicationProtocol_Tds8_DoesNotThrow()
        {
            SniHandle.ValidateNegotiatedApplicationProtocol(s_tds8);
        }

        [Fact]
        public void ValidateNegotiatedApplicationProtocol_None_Throws()
        {
            AuthenticationException ex = Assert.Throws<AuthenticationException>(
                () => SniHandle.ValidateNegotiatedApplicationProtocol(default));
            Assert.Contains(TdsEnums.TDS8_Protocol, ex.Message);
        }

        [Theory]
        [InlineData("h2")]
        [InlineData("http/1.1")]
        [InlineData("tds/7.4")]
        [InlineData("TDS/8.0")]
        public void ValidateNegotiatedApplicationProtocol_OtherProtocol_Throws(string protocol)
        {
            AuthenticationException ex = Assert.Throws<AuthenticationException>(
                () => SniHandle.ValidateNegotiatedApplicationProtocol(new SslApplicationProtocol(protocol)));
            Assert.Contains(protocol, ex.Message);
        }

        /// <summary>
        /// End-to-end: a TLS-first server that does or does not select "tds/8.0".
        /// When it does, the client must complete the handshake and send its PRELOGIN
        /// packet. When it does not, the client must abort before sending any TDS data.
        /// </summary>
        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public async Task StrictConnection_EnforcesNegotiatedAlpn(bool serverSelectsTds8, bool useAsync)
        {
            using X509Certificate2 serverCert = CreateServerCertificate();
            string pinPath = Path.Combine(Path.GetTempPath(), $"alpn-{Guid.NewGuid():N}.cer");
            File.WriteAllBytes(pinPath, serverCert.Export(X509ContentType.Cert));

            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            Task<int> serverTask = RunTlsFirstServerAsync(listener, serverCert, serverSelectsTds8);

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"tcp:127.0.0.1,{port}",
                Encrypt = SqlConnectionEncryptOption.Strict,
                HostNameInCertificate = "localhost",
                ServerCertificate = pinPath,
                ConnectTimeout = 5,
                ConnectRetryCount = 0,
                Pooling = false,
            };

            try
            {
                LocalAppContextSwitchesHelper? switches = null;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    switches = new LocalAppContextSwitchesHelper { UseManagedNetworking = true };
                }

                SqlException ex;
                using (switches)
                using (SqlConnection connection = new(builder.ConnectionString))
                {
                    ex = useAsync
                        ? await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync())
                        : Assert.Throws<SqlException>(() => connection.Open());
                }

                int firstByteAfterHandshake = await serverTask.WaitAsync(TimeSpan.FromSeconds(30));

                if (serverSelectsTds8)
                {
                    // 0x12 is the TDS PRELOGIN packet type; the client accepted the handshake.
                    Assert.Equal(0x12, firstByteAfterHandshake);
                    Assert.DoesNotContain(TdsEnums.TDS8_Protocol, ex.ToString());
                }
                else
                {
                    Assert.Equal(-1, firstByteAfterHandshake);
                    AuthenticationException inner = Assert.IsType<AuthenticationException>(ex.InnerException);
                    Assert.Contains(TdsEnums.TDS8_Protocol, inner.Message);
                }
            }
            finally
            {
                listener.Stop();
                File.Delete(pinPath);
            }
        }

        /// <summary>
        /// Accepts one connection, performs the server side of a TLS-first handshake, and
        /// returns the first byte the client sends afterwards (-1 if it sent nothing).
        /// </summary>
        private static async Task<int> RunTlsFirstServerAsync(TcpListener listener, X509Certificate2 cert, bool selectTds8)
        {
            using TcpClient client = await listener.AcceptTcpClientAsync();
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

            try
            {
                using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
                await ssl.AuthenticateAsServerAsync(options, cts.Token);
                byte[] buffer = new byte[1];
                int read = await ssl.ReadAsync(buffer, cts.Token);
                return read == 0 ? -1 : buffer[0];
            }
            catch (Exception e) when (e is IOException or AuthenticationException or OperationCanceledException)
            {
                return -1;
            }
        }

        private static X509Certificate2 CreateServerCertificate()
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder san = new();
            san.AddDnsName("localhost");
            request.CertificateExtensions.Add(san.Build());
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

            using X509Certificate2 ephemeral = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));

            // SChannel cannot use ephemeral keys for a server credential, so round-trip via PFX.
            byte[] pfx = ephemeral.Export(X509ContentType.Pkcs12);
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.Exportable);
#else
            return new X509Certificate2(pfx, (string?)null, X509KeyStorageFlags.Exportable);
#endif
        }
    }
}

#endif

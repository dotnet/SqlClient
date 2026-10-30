// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.IO;
using System.Net.Security;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    /// <summary>
    /// Provides untrusted self-signed server certificates for the managed SNI certificate capture tests.
    /// </summary>
    public sealed class ManagedSniCaptureCertificateFixture : CertificateFixtureBase
    {
        public ManagedSniCaptureCertificateFixture()
        {
            Localhost = CreateCertificate("localhost", new[] { "localhost" }, new[] { "127.0.0.1" });
            OtherHost = CreateCertificate("other.contoso.com", Array.Empty<string>(), Array.Empty<string>());
        }

        /// <summary>A certificate whose names match the "localhost" data source.</summary>
        public X509Certificate2 Localhost { get; }

        /// <summary>A certificate whose names do not match the "localhost" data source.</summary>
        public X509Certificate2 OtherHost { get; }
    }

    /// <summary>
    /// End-to-end verification, against the simulated TDS server over TLS, that managed SNI publishes the
    /// presented server certificate, SqlClient's effective validation verdict, and the callback chain
    /// status to <see cref="SqlConnection.EncryptionInfo"/> and <see cref="SqlException.EncryptionInfo"/>.
    /// Managed SNI is forced on Windows via the <c>UseManagedNetworkingOnWindows</c> switch.
    /// </summary>
    [Collection(SimulatedServerTestCollection.Name)]
    public class ManagedSniCertificateCaptureTests : IClassFixture<ManagedSniCaptureCertificateFixture>
    {
        private readonly ManagedSniCaptureCertificateFixture _fixture;

        public ManagedSniCertificateCaptureTests(ManagedSniCaptureCertificateFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_TrustServerCertificate_PublishesCertificateWithoutVerdict(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, trustServerCertificate: true));
            await OpenAsync(connection, async);

            SqlConnectionEncryptionInfo info = connection.EncryptionInfo;
            Assert.Equal(SqlConnectionEncryptionMode.Full, info.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, info.NegotiatedEncryptionProtocol);
            AssertCertificate(_fixture.Localhost, info);
            // The certificate was presented but SqlClient did not evaluate it.
            Assert.Null(info.ValidationErrors);
            Assert.Null(info.ChainStatus);
            // The published cipher suite is the one the client SslStream negotiated.
            Assert.NotNull(info.NegotiatedCipherSuiteId);
            Assert.Equal(GetClientSslStreamCipherSuite(connection), info.NegotiatedCipherSuiteId);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_MatchingServerCertificate_PublishesCertificateAndNoErrors(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);
            using TempCertFile pin = new(_fixture.Localhost);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));
            await OpenAsync(connection, async);

            SqlConnectionEncryptionInfo info = connection.EncryptionInfo;
            Assert.Equal(SqlConnectionEncryptionMode.Full, info.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, info.NegotiatedEncryptionProtocol);
            AssertCertificate(_fixture.Localhost, info);
            Assert.Equal(SqlServerCertificateValidationErrors.None, info.ValidationErrors);
            // The chain is not consulted when a ServerCertificate pin decides trust.
            Assert.Null(info.ChainStatus);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_UntrustedCertificate_ExceptionCarriesChainErrorAndCertificate(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost, expectTlsHandshakeFailure: true);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            SqlConnectionEncryptionInfo info = ex.EncryptionInfo;
            AssertCertificate(_fixture.Localhost, info);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, info.ValidationErrors);
            Assert.NotNull(info.ChainStatus);
            Assert.NotEqual(X509ChainStatusFlags.NoError, info.ChainStatus);
            // Certificate validation failed after the protocol was agreed; the observed values are kept.
            AssertObservedNegotiation(info);
            Assert.True(connection.EncryptionInfo.IsEmpty);
            AssertNoLeak(ex, info);

            // The suite read inside the validation callback is the real one: a trusted handshake with the
            // same server and protocol negotiates the same suite.
            using SqlConnection trusted = new(BuildConnectionString(server.EndPoint.Port, trustServerCertificate: true));
            await OpenAsync(trusted, async);
            Assert.Equal(GetClientSslStreamCipherSuite(trusted), info.NegotiatedCipherSuiteId);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_HostNameMismatch_ExceptionCarriesHostNameMismatch(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.OtherHost, expectTlsHandshakeFailure: true);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            SqlConnectionEncryptionInfo info = ex.EncryptionInfo;
            AssertCertificate(_fixture.OtherHost, info);
            Assert.Equal(
                SqlServerCertificateValidationErrors.ChainError | SqlServerCertificateValidationErrors.HostNameMismatch,
                info.ValidationErrors);
            Assert.NotNull(info.ChainStatus);
            AssertObservedNegotiation(info);
            AssertNoLeak(ex, info);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_HostNameInCertificateOverride_ReportsEffectiveVerdict(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.OtherHost, expectTlsHandshakeFailure: true);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, hostNameInCertificate: "other.contoso.com"));
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            // The platform reported a name mismatch, but HostNameInCertificate resolves it; only the
            // untrusted chain remains in SqlClient's effective verdict.
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, ex.EncryptionInfo.ValidationErrors);
            AssertCertificate(_fixture.OtherHost, ex.EncryptionInfo);
            AssertObservedNegotiation(ex.EncryptionInfo);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_ServerCertificateMismatch_ExceptionCarriesPinnedMismatch(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost, expectTlsHandshakeFailure: true);
            using TempCertFile pin = new(_fixture.OtherHost);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            SqlConnectionEncryptionInfo info = ex.EncryptionInfo;
            AssertCertificate(_fixture.Localhost, info);
            Assert.Equal(SqlServerCertificateValidationErrors.PinnedCertificateMismatch, info.ValidationErrors);
            Assert.Null(info.ChainStatus);
            AssertObservedNegotiation(info);
            AssertNoLeak(ex, info);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_FailureThenSuccess_DoesNotCarryPreviousAttemptCertificate(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer otherServer = StartServer(_fixture.OtherHost, expectTlsHandshakeFailure: true);
            using TdsServer server = StartServer(_fixture.Localhost);
            using TempCertFile pin = new(_fixture.Localhost);

            using SqlConnection failing = new(BuildConnectionString(otherServer.EndPoint.Port, serverCertificate: pin.Path));
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(failing, async));
            AssertCertificate(_fixture.OtherHost, ex.EncryptionInfo);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));
            await OpenAsync(connection, async);
            AssertCertificate(_fixture.Localhost, connection.EncryptionInfo);
            Assert.Equal(SqlServerCertificateValidationErrors.None, connection.EncryptionInfo.ValidationErrors);
        }

        #region Helpers

        /// <summary>
        /// Managed SNI records the protocol and cipher suite agreed for the handshake inside the validation
        /// callback, so a certificate-validation failure still reports them and the mode being negotiated.
        /// </summary>
        private static void AssertObservedNegotiation(SqlConnectionEncryptionInfo info)
        {
            Assert.Equal(SqlConnectionEncryptionMode.Full, info.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, info.NegotiatedEncryptionProtocol);
            Assert.NotNull(info.NegotiatedCipherSuiteId);
            Assert.InRange(info.NegotiatedCipherSuiteId!.Value, 1, 0xFFFF);
        }

        /// <summary>
        /// Reads the cipher suite directly from the client SslStream of an open managed SNI connection,
        /// independently of the encryption info capture.
        /// </summary>
        private static int GetClientSslStreamCipherSuite(SqlConnection connection)
        {
            global::Microsoft.Data.SqlClient.ManagedSni.SniHandle handle = connection.Parser._physicalStateObj.SessionHandle.ManagedHandle;
            Assert.NotNull(handle);
            FieldInfo? field = handle.GetType().GetField("_sslStream", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            SslStream? sslStream = field.GetValue(handle) as SslStream;
            Assert.NotNull(sslStream);
            return (int)sslStream.NegotiatedCipherSuite;
        }

        private static LocalAppContextSwitchesHelper? ForceManagedSni()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Managed SNI is the only implementation off Windows.
                return null;
            }

            LocalAppContextSwitchesHelper switches = new();
            switches.UseManagedNetworking = true;
            return switches;
        }

        /// <summary>
        /// Starts a TLS 1.2 simulated server that requires encryption.
        /// </summary>
        /// <param name="certificate">The certificate the server presents.</param>
        /// <param name="expectTlsHandshakeFailure">
        /// True when the client is expected to reject the certificate, so the server treats the resulting
        /// TLS handshake failure as expected rather than reporting it as a fault when it is stopped.
        /// </param>
        private static TdsServer StartServer(X509Certificate2 certificate, bool expectTlsHandshakeFailure = false)
        {
            TdsServer server = new(new TdsServerArguments
            {
                Encryption = TDSPreLoginTokenEncryptionType.Required,
                EncryptionCertificate = certificate,
                EncryptionProtocols = SslProtocols.Tls12,
                ExpectTlsHandshakeFailure = expectTlsHandshakeFailure,
            });
            server.Start();
            return server;
        }

        private static string BuildConnectionString(
            int port,
            bool trustServerCertificate = false,
            string? serverCertificate = null,
            string? hostNameInCertificate = null)
        {
            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + port,
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                TrustServerCertificate = trustServerCertificate,
                Pooling = false,
                ConnectTimeout = 15,
                ConnectRetryCount = 0,
            };
            if (serverCertificate is not null)
            {
                builder.ServerCertificate = serverCertificate;
            }
            if (hostNameInCertificate is not null)
            {
                builder.HostNameInCertificate = hostNameInCertificate;
            }
            return builder.ConnectionString;
        }

        private static Task OpenAsync(SqlConnection connection, bool async)
        {
            if (async)
            {
                return connection.OpenAsync();
            }

            connection.Open();
            return Task.CompletedTask;
        }

        private static void AssertCertificate(X509Certificate2 expected, SqlConnectionEncryptionInfo info)
        {
            Assert.NotNull(info.PresentedServerCertificateRawData);
            Assert.Equal(expected.RawData, info.PresentedServerCertificateRawData.Value.ToArray());

            using X509Certificate2? certificate = info.GetServerCertificate();
            Assert.NotNull(certificate);
            Assert.Equal(expected.RawData, certificate.RawData);
            Assert.False(certificate.HasPrivateKey);
        }

        private static void AssertNoLeak(SqlException ex, SqlConnectionEncryptionInfo info)
        {
            byte[] der = info.PresentedServerCertificateRawData!.Value.ToArray();
            string[] encodings = { Convert.ToHexString(der), Convert.ToBase64String(der), BitConverter.ToString(der), Convert.ToHexString(SHA256.HashData(der)) };
            foreach (string encoding in encodings)
            {
                Assert.DoesNotContain(encoding, ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(encoding, ex.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }

        private sealed class TempCertFile : IDisposable
        {
            public TempCertFile(X509Certificate2 certificate)
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SqlClient_CapturePin_" + Guid.NewGuid().ToString("N") + ".cer");
                File.WriteAllBytes(Path, certificate.Export(X509ContentType.Cert));
            }

            public string Path { get; }

            public void Dispose()
            {
                try
                {
                    File.Delete(Path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        #endregion
    }
}

#endif

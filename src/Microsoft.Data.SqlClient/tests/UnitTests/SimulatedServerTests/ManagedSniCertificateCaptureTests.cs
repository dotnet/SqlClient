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
    /// presented server certificate hash, SqlClient's effective validation verdict, and the callback chain
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
        public async Task Open_TrustServerCertificate_PublishesHashWithoutVerdict(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, trustServerCertificate: true));
            await OpenAsync(connection, async);

            SqlConnectionEncryptionInfo info = connection.EncryptionInfo;
            Assert.Equal(SqlConnectionEncryptionMode.Full, info.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, info.NegotiatedEncryptionProtocol);
            AssertHash(_fixture.Localhost, info);
            // The certificate was presented but SqlClient did not evaluate it.
            Assert.Null(info.ValidationErrors);
            Assert.Null(info.ChainStatus);
            // The published cipher suite is the one the client SslStream negotiated.
            Assert.NotNull(info.NegotiatedCipherSuite);
            Assert.Equal(GetClientSslStreamCipherSuite(connection), info.NegotiatedCipherSuite);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_MatchingServerCertificate_PublishesHashAndNoErrors(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);
            using TempCertFile pin = new(_fixture.Localhost);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));
            await OpenAsync(connection, async);

            SqlConnectionEncryptionInfo info = connection.EncryptionInfo;
            Assert.Equal(SqlConnectionEncryptionMode.Full, info.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, info.NegotiatedEncryptionProtocol);
            AssertHash(_fixture.Localhost, info);
            Assert.Equal(SqlServerCertificateValidationErrors.None, info.ValidationErrors);
            // The chain is not consulted when a ServerCertificate pin decides trust.
            Assert.Null(info.ChainStatus);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_UntrustedCertificate_ExceptionCarriesChainErrorAndHash(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            SqlConnectionEncryptionInfo info = ex.EncryptionInfo;
            AssertHash(_fixture.Localhost, info);
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
            Assert.Equal(GetClientSslStreamCipherSuite(trusted), info.NegotiatedCipherSuite);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_HostNameMismatch_ExceptionCarriesHostNameMismatch(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.OtherHost);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            SqlConnectionEncryptionInfo info = ex.EncryptionInfo;
            AssertHash(_fixture.OtherHost, info);
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
            using TdsServer server = StartServer(_fixture.OtherHost);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, hostNameInCertificate: "other.contoso.com"));
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            // The platform reported a name mismatch, but HostNameInCertificate resolves it; only the
            // untrusted chain remains in SqlClient's effective verdict.
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, ex.EncryptionInfo.ValidationErrors);
            AssertHash(_fixture.OtherHost, ex.EncryptionInfo);
            AssertObservedNegotiation(ex.EncryptionInfo);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_ServerCertificateMismatch_ExceptionCarriesPinnedMismatch(bool async)
        {
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);
            using TempCertFile pin = new(_fixture.OtherHost);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            SqlConnectionEncryptionInfo info = ex.EncryptionInfo;
            AssertHash(_fixture.Localhost, info);
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
            using TdsServer otherServer = StartServer(_fixture.OtherHost);
            using TdsServer server = StartServer(_fixture.Localhost);
            using TempCertFile pin = new(_fixture.Localhost);

            using SqlConnection failing = new(BuildConnectionString(otherServer.EndPoint.Port, serverCertificate: pin.Path));
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(failing, async));
            AssertHash(_fixture.OtherHost, ex.EncryptionInfo);

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));
            await OpenAsync(connection, async);
            AssertHash(_fixture.Localhost, connection.EncryptionInfo);
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
            Assert.NotNull(info.NegotiatedCipherSuite);
            Assert.InRange(info.NegotiatedCipherSuite.Value, 1, 0xFFFF);
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

        private static TdsServer StartServer(X509Certificate2 certificate)
        {
            TdsServer server = new(new TdsServerArguments
            {
                Encryption = TDSPreLoginTokenEncryptionType.Required,
                EncryptionCertificate = certificate,
                EncryptionProtocols = SslProtocols.Tls12,
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

        private static void AssertHash(X509Certificate2 expected, SqlConnectionEncryptionInfo info)
        {
            Assert.NotNull(info.PresentedServerCertificateHash256);
            Assert.Equal(SHA256.HashData(expected.RawData), info.PresentedServerCertificateHash256.Value.ToArray());
        }

        private static void AssertNoLeak(SqlException ex, SqlConnectionEncryptionInfo info)
        {
            byte[] hash = info.PresentedServerCertificateHash256!.Value.ToArray();
            string[] encodings = { Convert.ToHexString(hash), Convert.ToBase64String(hash), BitConverter.ToString(hash) };
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

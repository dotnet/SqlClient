// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    /// <summary>
    /// Verifies the certificate details that Native SNI reports on
    /// <see cref="SqlException.EncryptionInfo"/> when the server certificate is untrusted.
    /// </summary>
    [Collection(SimulatedServerTestCollection.Name)]
    public class NativeSniEncryptionInfoTests : IClassFixture<EncryptionInfoCertificateFixture>
    {
        private readonly X509Certificate2 _certificate;

        /// <summary>Creates the tests with the self-signed certificate the simulated server presents.</summary>
        /// <param name="fixture">The fixture that owns the certificate.</param>
        public NativeSniEncryptionInfoTests(EncryptionInfoCertificateFixture fixture)
        {
            _certificate = fixture.Certificate;
        }

        /// <summary>Gets whether connections use Native SNI.</summary>
        public static bool IsNativeSni =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !LocalAppContextSwitches.UseManagedNetworking;

        /// <summary>Verifies a synchronous open reports the untrusted certificate details.</summary>
        [ConditionalFact(nameof(IsNativeSni))]
        public void Open_UntrustedServerCertificate_ExceptionReportsCertificateDetails()
        {
            // Arrange
            using TdsServer server = StartUntrustedServer();
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertCertificateDetails(ex.EncryptionInfo);
            EncryptionInfoTestData.AssertEmpty(connection.EncryptionInfo);
        }

        /// <summary>Verifies an asynchronous open reports the untrusted certificate details.</summary>
        [ConditionalFact(nameof(IsNativeSni))]
        public async Task OpenAsync_UntrustedServerCertificate_ExceptionReportsCertificateDetails()
        {
            // Arrange
            using TdsServer server = StartUntrustedServer();
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertCertificateDetails(ex.EncryptionInfo);
            EncryptionInfoTestData.AssertEmpty(connection.EncryptionInfo);
        }

        private TdsServer StartUntrustedServer()
        {
            // The fixture certificate is self-signed and not installed in any trusted store, so
            // Native SNI completes the handshake and then reports a deferred validation failure.
            TdsServer server = new(new TdsServerArguments
            {
                Encryption = TDSPreLoginTokenEncryptionType.Required,
                EncryptionCertificate = _certificate,
                EncryptionProtocols = SslProtocols.Tls12,
                ExpectTlsHandshakeFailure = true,
            });
            server.Start();
            return server;
        }

        private static string BuildConnectionString(int port)
        {
            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + port,
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                TrustServerCertificate = false,
                Pooling = false,
                ConnectRetryCount = 0,
#if NETFRAMEWORK
                #pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
                TransparentNetworkIPResolution = false,
                #pragma warning restore 618
#endif
            };
            return builder.ConnectionString;
        }

        private void AssertCertificateDetails(SqlConnectionEncryptionInfo info)
        {
            SniNativeCertificateCapture capture = SniNativeCertificateCapture.Default;
            if (capture.IsValidationInfoUnavailable)
            {
                // An sni.dll without the validation info export reports no certificate details.
                Assert.Null(info.ValidationErrors);
                Assert.Null(info.ChainStatus);
                Assert.Null(info.GetServerCertificate());
                return;
            }

            // The certificate name matches localhost, so only the chain is rejected.
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, info.ValidationErrors);
            X509ChainStatusFlags chainStatus = Assert.NotNull(info.ChainStatus);
            Assert.True((chainStatus & X509ChainStatusFlags.UntrustedRoot) != 0);

            using X509Certificate2? presented = info.GetServerCertificate();
            if (capture.IsCertificateUnavailable)
            {
                // An sni.dll without the certificate export keeps the other fields.
                Assert.Null(presented);
            }
            else
            {
                Assert.NotNull(presented);
                Assert.Equal(_certificate.RawData, presented.RawData);
            }
        }
    }
}

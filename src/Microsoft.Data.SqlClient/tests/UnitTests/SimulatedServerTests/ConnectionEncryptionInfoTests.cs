// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    /// <summary>
    /// Provides a self-signed server certificate that the simulated TDS server uses for TLS.
    /// </summary>
    public sealed class EncryptionInfoCertificateFixture : CertificateFixtureBase
    {
        public EncryptionInfoCertificateFixture()
        {
            Certificate = CreateCertificate("localhost", new[] { "localhost" }, new[] { "127.0.0.1" });
        }

        public X509Certificate2 Certificate { get; }
    }

    /// <summary>
    /// Verifies that the negotiated encryption mode and TLS protocol are published to
    /// <see cref="SqlConnection.EncryptionInfo"/> and <see cref="SqlException.EncryptionInfo"/>.
    /// </summary>
    [Collection(SimulatedServerTestCollection.Name)]
    public class ConnectionEncryptionInfoTests : IClassFixture<EncryptionInfoCertificateFixture>
    {
        private readonly X509Certificate2 _certificate;

        public ConnectionEncryptionInfoTests(EncryptionInfoCertificateFixture fixture)
        {
            _certificate = fixture.Certificate;
        }

        public enum ServerEncryption
        {
            // Server does not support encryption: no TLS at all.
            NotSupported,
            // Server supports encryption but does not require it: login-only TLS.
            Off,
            // Server requires encryption: full TLS.
            Required,
        }

        #region Success

        [Theory]
        [InlineData(ServerEncryption.NotSupported, false)]
        [InlineData(ServerEncryption.NotSupported, true)]
        [InlineData(ServerEncryption.Off, false)]
        [InlineData(ServerEncryption.Off, true)]
        [InlineData(ServerEncryption.Required, false)]
        [InlineData(ServerEncryption.Required, true)]
        public async Task Open_Success_PublishesNegotiatedModeAndProtocol(ServerEncryption serverEncryption, bool async)
        {
            using TdsServer server = new(CreateArguments<TdsServerArguments>(serverEncryption));
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            Assert.True(connection.EncryptionInfo.IsEmpty);

            await OpenAsync(connection, async);

            Assert.Equal(ConnectionState.Open, connection.State);
            AssertNegotiated(connection.EncryptionInfo, serverEncryption);
            AssertBypassedCertificateFields(connection.EncryptionInfo, serverEncryption);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_EncryptMandatory_ServerOff_PublishesFull(bool async)
        {
            using TdsServer server = new(CreateArguments<TdsServerArguments>(ServerEncryption.Off));
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, SqlConnectionEncryptOption.Mandatory));

            await OpenAsync(connection, async);

            Assert.Equal(SqlConnectionEncryptionMode.Full, connection.EncryptionInfo.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, connection.EncryptionInfo.NegotiatedEncryptionProtocol);
            AssertBypassedCertificateFields(connection.EncryptionInfo, ServerEncryption.Required);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Close_ResetsToEmpty_HeldSnapshotUnchanged_ReopenPublishesAgain(bool async)
        {
            using TdsServer server = new(CreateArguments<TdsServerArguments>(ServerEncryption.Off));
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            await OpenAsync(connection, async);

            SqlConnectionEncryptionInfo held = connection.EncryptionInfo;
            AssertNegotiated(held, ServerEncryption.Off);

            connection.Close();

            Assert.True(connection.EncryptionInfo.IsEmpty);
            Assert.Same(SqlConnectionEncryptionInfo.Empty, connection.EncryptionInfo);
            // The snapshot the caller already holds is immutable.
            AssertNegotiated(held, ServerEncryption.Off);

            await OpenAsync(connection, async);

            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.Off);

            connection.Dispose();
            Assert.True(connection.EncryptionInfo.IsEmpty);
            AssertNegotiated(held, ServerEncryption.Off);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PooledReuse_PublishesPhysicalConnectionSnapshot(bool async)
        {
            using TdsServer server = new(CreateArguments<TdsServerArguments>(ServerEncryption.Off));
            server.Start();

            string connectionString = BuildConnectionString(
                server.EndPoint.Port,
                pooling: true,
                applicationName: $"EncryptionInfoPool_{Guid.NewGuid():N}");

            SqlConnectionEncryptionInfo first;
            using (SqlConnection connection = new(connectionString))
            {
                await OpenAsync(connection, async);
                first = connection.EncryptionInfo;
                AssertNegotiated(first, ServerEncryption.Off);
            }

            int preLoginCount = server.PreLoginCount;

            try
            {
                using SqlConnection reused = new(connectionString);
                Assert.True(reused.EncryptionInfo.IsEmpty);

                await OpenAsync(reused, async);

                // The physical connection came from the pool; no new login took place.
                Assert.Equal(preLoginCount, server.PreLoginCount);
                Assert.Same(first, reused.EncryptionInfo);

                reused.Close();
                Assert.True(reused.EncryptionInfo.IsEmpty);
                AssertNegotiated(first, ServerEncryption.Off);
            }
            finally
            {
                SqlConnection.ClearPool(new SqlConnection(connectionString));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Mars_SurfacesPhysicalConnectionValues(bool async)
        {
            using TdsServer server = new(CreateArguments<TdsServerArguments>(ServerEncryption.Required));
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, mars: true));

            await OpenAsync(connection, async);

            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.Required);
        }

        #endregion

        #region Failure

        [Theory]
        [InlineData(ServerEncryption.NotSupported, false)]
        [InlineData(ServerEncryption.NotSupported, true)]
        [InlineData(ServerEncryption.Off, false)]
        [InlineData(ServerEncryption.Off, true)]
        [InlineData(ServerEncryption.Required, false)]
        [InlineData(ServerEncryption.Required, true)]
        public async Task Open_LoginFailureAfterPreLogin_AttachesNegotiatedValuesToException(ServerEncryption serverEncryption, bool async)
        {
            // A routing server that requires read-only intent rejects a read-write login with error 18456
            // after pre-login (and any TLS handshake) has completed.
            RoutingTdsServerArguments arguments = CreateArguments<RoutingTdsServerArguments>(serverEncryption);
            arguments.RequireReadOnly = true;
            arguments.RoutingTCPHost = "localhost";
            arguments.RoutingTCPPort = 1;
            using RoutingTdsServer server = new(arguments);
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));

            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            Assert.Equal(18456, ex.Number);
            AssertNegotiated(ex.EncryptionInfo, serverEncryption);
            AssertBypassedCertificateFields(ex.EncryptionInfo, serverEncryption);
            Assert.True(connection.EncryptionInfo.IsEmpty);

            // Values are not surfaced through message text.
            Assert.DoesNotContain("Tls12", ex.Message);
            Assert.DoesNotContain("LoginOnly", ex.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_FailureBeforePreLogin_ExceptionEncryptionInfoIsEmpty(bool async)
        {
            int port = GetUnusedPort();

            using SqlConnection connection = new(BuildConnectionString(port, connectTimeout: 2));

            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            Assert.True(ex.EncryptionInfo.IsEmpty);
            Assert.True(connection.EncryptionInfo.IsEmpty);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Open_ServerRejectsEncryptionInPreLogin_ExceptionEncryptionInfoIsEmpty(bool async)
        {
            // The server answers ENCRYPT_NOT_SUP while the client demands encryption, so pre-login fails
            // before an encryption mode is negotiated.
            using TdsServer server = new(new TdsServerArguments { Encryption = TDSPreLoginTokenEncryptionType.NotSupported });
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, SqlConnectionEncryptOption.Mandatory));

            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            Assert.True(ex.EncryptionInfo.IsEmpty);
        }

        [Theory]
        [InlineData(ServerEncryption.Off, false)]
        [InlineData(ServerEncryption.Off, true)]
        [InlineData(ServerEncryption.Required, false)]
        [InlineData(ServerEncryption.Required, true)]
        public async Task Open_UntrustedServerCertificate_ExceptionReportsOnlyObservedModeAndProtocol(ServerEncryption serverEncryption, bool async)
        {
            // The fixture certificate is self-signed and not installed in any trusted store, so
            // certificate validation fails. Native SNI (Schannel) defers that verdict until the LOGIN7
            // write and keeps the mode and protocol observed during the completed handshake. Managed SNI
            // fails inside the handshake but records the protocol agreed before the validation callback.
            // The mode is never reported without an observed protocol. Certificate fields are not asserted here.
            using TdsServer server = new(CreateArguments<TdsServerArguments>(serverEncryption));
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(
                server.EndPoint.Port,
                SqlConnectionEncryptOption.Mandatory,
                trustServerCertificate: false));

            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            Assert.True(server.PreLoginCount >= 1);
            if (ex.EncryptionInfo.NegotiatedEncryptionProtocol is null)
            {
                Assert.Null(ex.EncryptionInfo.NegotiatedEncryptionMode);
            }
            else
            {
                Assert.Equal(SqlConnectionEncryptionMode.Full, ex.EncryptionInfo.NegotiatedEncryptionMode);
            }

            if (LocalAppContextSwitches.UseManagedNetworking)
            {
                // SslStream agrees the protocol version before raising the validation callback.
                Assert.NotNull(ex.EncryptionInfo.NegotiatedEncryptionProtocol);
            }

            Assert.True(connection.EncryptionInfo.IsEmpty);
        }

        #endregion

        #region Attempt isolation

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Routing_PublishesTerminalAttemptValuesOnly(bool async)
        {
            // Gateway negotiates login-only TLS, the routed target negotiates no TLS. Only the
            // terminal (routed) attempt's values may be published.
            using TdsServer target = new(CreateArguments<TdsServerArguments>(ServerEncryption.NotSupported));
            target.Start();

            RoutingTdsServerArguments routerArguments = CreateArguments<RoutingTdsServerArguments>(ServerEncryption.Off);
            routerArguments.RoutingTCPHost = "localhost";
            routerArguments.RoutingTCPPort = (ushort)target.EndPoint.Port;
            using RoutingTdsServer router = new(routerArguments);
            router.Start();

            using SqlConnection connection = new(BuildConnectionString(router.EndPoint.Port, readOnlyIntent: true));

            await OpenAsync(connection, async);

            Assert.Equal(1, target.PreLoginCount);
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.NotSupported);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Routing_TargetUnreachable_ExceptionDoesNotCarryGatewayValues(bool async)
        {
            // The gateway attempt negotiated login-only TLS, but the terminal (routed) attempt fails
            // before pre-login, so nothing is known about the terminal attempt.
            RoutingTdsServerArguments routerArguments = CreateArguments<RoutingTdsServerArguments>(ServerEncryption.Off);
            routerArguments.RoutingTCPHost = "localhost";
            routerArguments.RoutingTCPPort = (ushort)GetUnusedPort();
            using RoutingTdsServer router = new(routerArguments);
            router.Start();

            using SqlConnection connection = new(BuildConnectionString(router.EndPoint.Port, readOnlyIntent: true, connectTimeout: 5));

            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => OpenAsync(connection, async));

            Assert.True(router.PreLoginCount >= 1);
            Assert.True(ex.EncryptionInfo.IsEmpty);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TransientRetry_PublishesSuccessfulAttemptValues(bool async)
        {
            TransientTdsErrorTdsServerArguments arguments = CreateArguments<TransientTdsErrorTdsServerArguments>(ServerEncryption.Off);
            arguments.IsEnabledTransientError = true;
            arguments.Number = 40613;
            using TransientTdsErrorTdsServer server = new(arguments);
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, connectRetryCount: 1));

            await OpenAsync(connection, async);

            Assert.Equal(2, server.PreLoginCount);
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.Off);
        }

        #endregion

        #region Helpers

        private T CreateArguments<T>(ServerEncryption serverEncryption)
            where T : TdsServerArguments, new()
        {
            T arguments = new();
            switch (serverEncryption)
            {
                case ServerEncryption.NotSupported:
                    arguments.Encryption = TDSPreLoginTokenEncryptionType.NotSupported;
                    break;
                case ServerEncryption.Off:
                    arguments.Encryption = TDSPreLoginTokenEncryptionType.Off;
                    arguments.EncryptionCertificate = _certificate;
                    break;
                case ServerEncryption.Required:
                    arguments.Encryption = TDSPreLoginTokenEncryptionType.Required;
                    arguments.EncryptionCertificate = _certificate;
                    break;
            }
            arguments.EncryptionProtocols = SslProtocols.Tls12;
            return arguments;
        }

        private static string BuildConnectionString(
            int port,
            SqlConnectionEncryptOption? encrypt = null,
            bool pooling = false,
            string? applicationName = null,
            bool mars = false,
            bool readOnlyIntent = false,
            int connectTimeout = 15,
            int connectRetryCount = 0,
            bool trustServerCertificate = true)
        {
            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + port,
                Encrypt = encrypt ?? SqlConnectionEncryptOption.Optional,
                TrustServerCertificate = trustServerCertificate,
                Pooling = pooling,
                MultipleActiveResultSets = mars,
                ApplicationIntent = readOnlyIntent ? ApplicationIntent.ReadOnly : ApplicationIntent.ReadWrite,
                ConnectTimeout = connectTimeout,
                ConnectRetryCount = connectRetryCount,
                ConnectRetryInterval = 1,
#if NETFRAMEWORK
                #pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
                TransparentNetworkIPResolution = false,
                #pragma warning restore 618
#endif
            };
            if (applicationName is not null)
            {
                builder.ApplicationName = applicationName;
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

        private static void AssertNegotiated(SqlConnectionEncryptionInfo info, ServerEncryption serverEncryption)
        {
            Assert.False(info.IsEmpty);
            switch (serverEncryption)
            {
                case ServerEncryption.NotSupported:
                    Assert.Equal(SqlConnectionEncryptionMode.None, info.NegotiatedEncryptionMode);
                    Assert.Null(info.NegotiatedEncryptionProtocol);
                    break;
                case ServerEncryption.Off:
                    Assert.Equal(SqlConnectionEncryptionMode.LoginOnly, info.NegotiatedEncryptionMode);
                    Assert.Equal(SslProtocols.Tls12, info.NegotiatedEncryptionProtocol);
                    break;
                case ServerEncryption.Required:
                    Assert.Equal(SqlConnectionEncryptionMode.Full, info.NegotiatedEncryptionMode);
                    Assert.Equal(SslProtocols.Tls12, info.NegotiatedEncryptionProtocol);
                    break;
            }
        }

        /// <summary>
        /// These tests use TrustServerCertificate=true, so SqlClient never evaluates the certificate:
        /// the verdict and chain status are always unavailable. Managed SNI still records the hash of
        /// the presented certificate whenever a TLS handshake took place; native SNI records it only
        /// when sni.dll provides the certificate capture exports.
        /// </summary>
        private void AssertBypassedCertificateFields(SqlConnectionEncryptionInfo info, ServerEncryption serverEncryption)
        {
            if (serverEncryption != ServerEncryption.NotSupported)
            {
                // Native SNI captures the hash only with an sni.dll that has the certificate capture
                // exports. Until the SNI package with those exports ships, accept either a null hash
                // (older sni.dll) or the correct hash. Managed SNI must always capture it.
                if (LocalAppContextSwitches.UseManagedNetworking)
                {
                    Assert.NotNull(info.PresentedServerCertificateHash256);
                }

                if (info.PresentedServerCertificateHash256 is ReadOnlyMemory<byte> hash)
                {
                    using System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create();
                    Assert.Equal(32, hash.Length);
                    Assert.Equal(sha256.ComputeHash(_certificate.RawData), hash.ToArray());
                }
            }
            else
            {
                Assert.Null(info.PresentedServerCertificateHash256);
            }
            Assert.Null(info.ValidationErrors);
            Assert.Null(info.ChainStatus);
        }

        private static int GetUnusedPort()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        #endregion
    }
}

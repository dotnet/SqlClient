// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;
using static Microsoft.Data.SqlClient.UnitTests.Utilities.EncryptionInfoTestData;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    /// <summary>
    /// Provides a self-signed server certificate that the simulated TDS server uses for TLS.
    /// </summary>
    public sealed class EncryptionInfoCertificateFixture : CertificateFixtureBase
    {
        /// <summary>
        /// Creates a self-signed certificate for <c>localhost</c> and <c>127.0.0.1</c>.
        /// </summary>
        public EncryptionInfoCertificateFixture()
        {
            Certificate = CreateCertificate("localhost", new[] { "localhost" }, new[] { "127.0.0.1" });
            CertificateFilePath = Path.Combine(Path.GetTempPath(), $"EncryptionInfo_{Guid.NewGuid():N}.cer");
            File.WriteAllBytes(CertificateFilePath, Certificate.Export(X509ContentType.Cert));
        }

        /// <summary>
        /// Gets the self-signed certificate. It is not installed in any trusted store.
        /// </summary>
        public X509Certificate2 Certificate { get; }

        /// <summary>
        /// Gets the path of a file containing the public certificate, for pinning it through the
        /// <c>ServerCertificate</c> connection string keyword.
        /// </summary>
        public string CertificateFilePath { get; }

        /// <summary>
        /// Deletes the exported certificate file.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            File.Delete(CertificateFilePath);
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Verifies that the negotiated encryption mode and TLS protocol are published to
    /// <see cref="SqlConnection.EncryptionInfo"/> and <see cref="SqlException.EncryptionInfo"/>.
    /// </summary>
    [Collection(SimulatedServerTestCollection.Name)]
    public class ConnectionEncryptionInfoTests : IClassFixture<EncryptionInfoCertificateFixture>
    {
        private readonly X509Certificate2 _certificate;
        private readonly string _certificateFilePath;

        /// <summary>
        /// Creates the test class with the shared server certificate.
        /// </summary>
        public ConnectionEncryptionInfoTests(EncryptionInfoCertificateFixture fixture)
        {
            _certificate = fixture.Certificate;
            _certificateFilePath = fixture.CertificateFilePath;
        }

        /// <summary>
        /// The encryption setting the simulated server advertises in its pre-login response.
        /// </summary>
        public enum ServerEncryption
        {
            /// <summary>The server does not support encryption: no TLS at all.</summary>
            NotSupported,

            /// <summary>The server supports encryption but does not require it: login-only TLS.</summary>
            Off,

            /// <summary>The server requires encryption: full TLS.</summary>
            Required,
        }

        #region Success

        /// <summary>
        /// A successful synchronous open publishes the mode and protocol that match the server's
        /// encryption setting.
        /// </summary>
        [Theory]
        [InlineData(ServerEncryption.NotSupported)]
        [InlineData(ServerEncryption.Off)]
        [InlineData(ServerEncryption.Required)]
        public void Open_Success_PublishesNegotiatedModeAndProtocol(ServerEncryption serverEncryption)
        {
            // Arrange
            using TdsServer server = StartServer(serverEncryption);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            AssertEmpty(connection.EncryptionInfo);

            // Act
            connection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            AssertNegotiated(connection.EncryptionInfo, serverEncryption);
            AssertBypassedCertificateFields(connection.EncryptionInfo, serverEncryption);
        }

        /// <summary>
        /// A successful asynchronous open publishes the mode and protocol that match the server's
        /// encryption setting.
        /// </summary>
        [Theory]
        [InlineData(ServerEncryption.NotSupported)]
        [InlineData(ServerEncryption.Off)]
        [InlineData(ServerEncryption.Required)]
        public async Task OpenAsync_Success_PublishesNegotiatedModeAndProtocol(ServerEncryption serverEncryption)
        {
            // Arrange
            using TdsServer server = StartServer(serverEncryption);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            AssertEmpty(connection.EncryptionInfo);

            // Act
            await connection.OpenAsync();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            AssertNegotiated(connection.EncryptionInfo, serverEncryption);
            AssertBypassedCertificateFields(connection.EncryptionInfo, serverEncryption);
        }

        /// <summary>
        /// With <c>Encrypt=Mandatory</c>, the client keeps TLS for the whole session even when the
        /// server only offers login encryption, so a synchronous open publishes <c>Full</c>.
        /// </summary>
        [Fact]
        public void Open_EncryptMandatoryServerOff_PublishesFull()
        {
            // Arrange
            using TdsServer server = StartServer(ServerEncryption.Off);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, SqlConnectionEncryptOption.Mandatory));

            // Act
            connection.Open();

            // Assert
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.Required);
            AssertBypassedCertificateFields(connection.EncryptionInfo, ServerEncryption.Required);
        }

        /// <summary>
        /// With <c>Encrypt=Mandatory</c>, the client keeps TLS for the whole session even when the
        /// server only offers login encryption, so an asynchronous open publishes <c>Full</c>.
        /// </summary>
        [Fact]
        public async Task OpenAsync_EncryptMandatoryServerOff_PublishesFull()
        {
            // Arrange
            using TdsServer server = StartServer(ServerEncryption.Off);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, SqlConnectionEncryptOption.Mandatory));

            // Act
            await connection.OpenAsync();

            // Assert
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.Required);
            AssertBypassedCertificateFields(connection.EncryptionInfo, ServerEncryption.Required);
        }

        /// <summary>
        /// Closing and disposing reset the connection to <see cref="SqlConnectionEncryptionInfo.Empty"/>
        /// without changing a snapshot the caller holds, and a synchronous reopen publishes values again.
        /// </summary>
        [Fact]
        public void Open_AfterClose_PublishesAgainAndHeldSnapshotUnchanged()
        {
            // Arrange
            using TdsServer server = StartServer(ServerEncryption.Off);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            connection.Open();
            SqlConnectionEncryptionInfo held = connection.EncryptionInfo;
            AssertNegotiated(held, ServerEncryption.Off);

            // Act
            connection.Close();
            SqlConnectionEncryptionInfo afterClose = connection.EncryptionInfo;
            connection.Open();
            SqlConnectionEncryptionInfo afterReopen = connection.EncryptionInfo;
            connection.Dispose();
            SqlConnectionEncryptionInfo afterDispose = connection.EncryptionInfo;

            // Assert
            Assert.Same(SqlConnectionEncryptionInfo.Empty, afterClose);
            AssertNegotiated(afterReopen, ServerEncryption.Off);
            AssertEmpty(afterDispose);
            // The snapshot the caller already holds is immutable.
            AssertNegotiated(held, ServerEncryption.Off);
        }

        /// <summary>
        /// Closing and disposing reset the connection to <see cref="SqlConnectionEncryptionInfo.Empty"/>
        /// without changing a snapshot the caller holds, and an asynchronous reopen publishes values again.
        /// </summary>
        [Fact]
        public async Task OpenAsync_AfterClose_PublishesAgainAndHeldSnapshotUnchanged()
        {
            // Arrange
            using TdsServer server = StartServer(ServerEncryption.Off);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            await connection.OpenAsync();
            SqlConnectionEncryptionInfo held = connection.EncryptionInfo;
            AssertNegotiated(held, ServerEncryption.Off);

            // Act
            connection.Close();
            SqlConnectionEncryptionInfo afterClose = connection.EncryptionInfo;
            await connection.OpenAsync();
            SqlConnectionEncryptionInfo afterReopen = connection.EncryptionInfo;
            connection.Dispose();
            SqlConnectionEncryptionInfo afterDispose = connection.EncryptionInfo;

            // Assert
            Assert.Same(SqlConnectionEncryptionInfo.Empty, afterClose);
            AssertNegotiated(afterReopen, ServerEncryption.Off);
            AssertEmpty(afterDispose);
            // The snapshot the caller already holds is immutable.
            AssertNegotiated(held, ServerEncryption.Off);
        }

        /// <summary>
        /// A synchronous open that reuses a pooled physical connection publishes that connection's
        /// saved snapshot without a new login.
        /// </summary>
        [Fact]
        public void Open_PooledReuse_PublishesPhysicalConnectionSnapshot()
        {
            // Arrange
            // Pool a physical connection under a unique pool key.
            using TdsServer server = StartServer(ServerEncryption.Off);
            string connectionString = BuildPooledConnectionString(server.EndPoint.Port);
            SqlConnectionEncryptionInfo first;
            using (SqlConnection connection = new(connectionString))
            {
                connection.Open();
                first = connection.EncryptionInfo;
            }
            int preLoginCount = server.PreLoginCount;
            using SqlConnection reused = new(connectionString);

            try
            {
                // Act
                reused.Open();
                SqlConnectionEncryptionInfo published = reused.EncryptionInfo;
                reused.Close();

                // Assert
                // No new login took place, and the saved snapshot was published unchanged.
                Assert.Equal(preLoginCount, server.PreLoginCount);
                AssertNegotiated(first, ServerEncryption.Off);
                Assert.Same(first, published);
                AssertEmpty(reused.EncryptionInfo);
            }
            finally
            {
                // Cleanup
                SqlConnection.ClearPool(reused);
            }
        }

        /// <summary>
        /// An asynchronous open that reuses a pooled physical connection publishes that connection's
        /// saved snapshot without a new login.
        /// </summary>
        [Fact]
        public async Task OpenAsync_PooledReuse_PublishesPhysicalConnectionSnapshot()
        {
            // Arrange
            // Pool a physical connection under a unique pool key.
            using TdsServer server = StartServer(ServerEncryption.Off);
            string connectionString = BuildPooledConnectionString(server.EndPoint.Port);
            SqlConnectionEncryptionInfo first;
            using (SqlConnection connection = new(connectionString))
            {
                await connection.OpenAsync();
                first = connection.EncryptionInfo;
            }
            int preLoginCount = server.PreLoginCount;
            using SqlConnection reused = new(connectionString);

            try
            {
                // Act
                await reused.OpenAsync();
                SqlConnectionEncryptionInfo published = reused.EncryptionInfo;
                reused.Close();

                // Assert
                // No new login took place, and the saved snapshot was published unchanged.
                Assert.Equal(preLoginCount, server.PreLoginCount);
                AssertNegotiated(first, ServerEncryption.Off);
                Assert.Same(first, published);
                AssertEmpty(reused.EncryptionInfo);
            }
            finally
            {
                // Cleanup
                SqlConnection.ClearPool(reused);
            }
        }

        /// <summary>
        /// A synchronous open that requests MARS publishes the physical connection's values. The
        /// simulated server does not enable MARS, so this covers the MARS request path only; the
        /// values belong to the physical connection, so a MARS session would surface the same ones.
        /// </summary>
        [Fact]
        public void Open_MarsRequestedServerWithoutMars_PublishesPhysicalConnectionValues()
        {
            // Arrange
            using TdsServer server = StartServer(ServerEncryption.Required);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, mars: true));

            // Act
            connection.Open();

            // Assert
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.Required);
        }

        /// <summary>
        /// An asynchronous open that requests MARS publishes the physical connection's values. The
        /// simulated server does not enable MARS, so this covers the MARS request path only; the
        /// values belong to the physical connection, so a MARS session would surface the same ones.
        /// </summary>
        [Fact]
        public async Task OpenAsync_MarsRequestedServerWithoutMars_PublishesPhysicalConnectionValues()
        {
            // Arrange
            using TdsServer server = StartServer(ServerEncryption.Required);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, mars: true));

            // Act
            await connection.OpenAsync();

            // Assert
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.Required);
        }

        #endregion

        #region Failure

        /// <summary>
        /// A login rejected after pre-login, during a synchronous open, attaches the negotiated mode
        /// and protocol to the exception without adding them to its message.
        /// </summary>
        [Theory]
        [InlineData(ServerEncryption.NotSupported)]
        [InlineData(ServerEncryption.Off)]
        [InlineData(ServerEncryption.Required)]
        public void Open_LoginFailureAfterPreLogin_AttachesNegotiatedValuesToException(ServerEncryption serverEncryption)
        {
            // Arrange
            using RoutingTdsServer server = StartReadOnlyRequiredServer(serverEncryption);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertLoginFailureCarriesNegotiatedValues(ex, connection, serverEncryption);
        }

        /// <summary>
        /// A login rejected after pre-login, during an asynchronous open, attaches the negotiated mode
        /// and protocol to the exception without adding them to its message.
        /// </summary>
        [Theory]
        [InlineData(ServerEncryption.NotSupported)]
        [InlineData(ServerEncryption.Off)]
        [InlineData(ServerEncryption.Required)]
        public async Task OpenAsync_LoginFailureAfterPreLogin_AttachesNegotiatedValuesToException(ServerEncryption serverEncryption)
        {
            // Arrange
            using RoutingTdsServer server = StartReadOnlyRequiredServer(serverEncryption);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertLoginFailureCarriesNegotiatedValues(ex, connection, serverEncryption);
        }

        /// <summary>
        /// A synchronous open that fails before pre-login leaves the exception's info empty.
        /// </summary>
        [Fact]
        public void Open_FailureBeforePreLogin_ExceptionEncryptionInfoIsEmpty()
        {
            // Arrange
            using SqlConnection connection = new(BuildConnectionString(GetUnusedPort(), connectTimeout: 2));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertEmpty(ex.EncryptionInfo);
            AssertEmpty(connection.EncryptionInfo);
        }

        /// <summary>
        /// An asynchronous open that fails before pre-login leaves the exception's info empty.
        /// </summary>
        [Fact]
        public async Task OpenAsync_FailureBeforePreLogin_ExceptionEncryptionInfoIsEmpty()
        {
            // Arrange
            using SqlConnection connection = new(BuildConnectionString(GetUnusedPort(), connectTimeout: 2));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertEmpty(ex.EncryptionInfo);
            AssertEmpty(connection.EncryptionInfo);
        }

        /// <summary>
        /// A synchronous open whose pre-login fails because the server offers no encryption while the
        /// client requires it leaves the exception's info empty.
        /// </summary>
        [Fact]
        public void Open_ServerRejectsEncryptionInPreLogin_ExceptionEncryptionInfoIsEmpty()
        {
            // Arrange
            using TdsServer server = StartServerWithoutEncryptionOption();
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, SqlConnectionEncryptOption.Mandatory));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertEmpty(ex.EncryptionInfo);
        }

        /// <summary>
        /// An asynchronous open whose pre-login fails because the server offers no encryption while the
        /// client requires it leaves the exception's info empty.
        /// </summary>
        [Fact]
        public async Task OpenAsync_ServerRejectsEncryptionInPreLogin_ExceptionEncryptionInfoIsEmpty()
        {
            // Arrange
            using TdsServer server = StartServerWithoutEncryptionOption();
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, SqlConnectionEncryptOption.Mandatory));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertEmpty(ex.EncryptionInfo);
        }

        /// <summary>
        /// A synchronous open that fails certificate validation inside the TLS handshake reports
        /// the mode selected by pre-login. Managed SNI also reports the protocol it observed.
        /// </summary>
        [Theory]
        [InlineData(ServerEncryption.Off)]
        [InlineData(ServerEncryption.Required)]
        public void Open_UntrustedServerCertificate_ExceptionReportsModeWithoutProtocol(ServerEncryption serverEncryption)
        {
            // Arrange
            using TdsServer server = StartUntrustedCertificateServer(serverEncryption);
            using SqlConnection connection = new(BuildConnectionString(
                server.EndPoint.Port,
                SqlConnectionEncryptOption.Mandatory,
                trustServerCertificate: false));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertUntrustedCertificateFailure(ex, connection, server);
        }

        /// <summary>
        /// An asynchronous open that fails certificate validation inside the TLS handshake reports
        /// the mode selected by pre-login. Managed SNI also reports the protocol it observed.
        /// </summary>
        [Theory]
        [InlineData(ServerEncryption.Off)]
        [InlineData(ServerEncryption.Required)]
        public async Task OpenAsync_UntrustedServerCertificate_ExceptionReportsModeWithoutProtocol(ServerEncryption serverEncryption)
        {
            // Arrange
            using TdsServer server = StartUntrustedCertificateServer(serverEncryption);
            using SqlConnection connection = new(BuildConnectionString(
                server.EndPoint.Port,
                SqlConnectionEncryptOption.Mandatory,
                trustServerCertificate: false));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertUntrustedCertificateFailure(ex, connection, server);
        }

        #endregion

        #region Strict (TDS 8)

        /// <summary>
        /// A synchronous open with <c>Encrypt=Strict</c> establishes TLS before pre-login and
        /// publishes <c>Strict</c> with the negotiated protocol.
        /// </summary>
        [Fact]
        public void Open_EncryptStrictPinnedCertificate_PublishesStrictAndProtocol()
        {
            // Arrange
            using TdsServer server = StartStrictServer(expectTlsHandshakeFailure: false);
            using SqlConnection connection = new(BuildStrictConnectionString(server.EndPoint.Port, _certificateFilePath));

            // Act
            connection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            AssertStrict(connection.EncryptionInfo);
        }

        /// <summary>
        /// An asynchronous open with <c>Encrypt=Strict</c> establishes TLS before pre-login and
        /// publishes <c>Strict</c> with the negotiated protocol.
        /// </summary>
        [Fact]
        public async Task OpenAsync_EncryptStrictPinnedCertificate_PublishesStrictAndProtocol()
        {
            // Arrange
            using TdsServer server = StartStrictServer(expectTlsHandshakeFailure: false);
            using SqlConnection connection = new(BuildStrictConnectionString(server.EndPoint.Port, _certificateFilePath));

            // Act
            await connection.OpenAsync();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            AssertStrict(connection.EncryptionInfo);
        }

        /// <summary>
        /// A synchronous open with <c>Encrypt=Strict</c> whose TLS handshake rejects the untrusted
        /// server certificate reports <c>Strict</c>, which is known before the handshake, but no
        /// protocol.
        /// </summary>
        [Fact]
        public void Open_EncryptStrictUntrustedCertificate_ExceptionReportsStrictWithoutProtocol()
        {
            // Arrange
            using TdsServer server = StartStrictServer(expectTlsHandshakeFailure: true);
            using SqlConnection connection = new(BuildStrictConnectionString(server.EndPoint.Port, serverCertificate: null));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertStrictHandshakeFailure(ex, connection, server);
        }

        /// <summary>
        /// An asynchronous open with <c>Encrypt=Strict</c> whose TLS handshake rejects the untrusted
        /// server certificate reports <c>Strict</c>, which is known before the handshake, but no
        /// protocol.
        /// </summary>
        [Fact]
        public async Task OpenAsync_EncryptStrictUntrustedCertificate_ExceptionReportsStrictWithoutProtocol()
        {
            // Arrange
            using TdsServer server = StartStrictServer(expectTlsHandshakeFailure: true);
            using SqlConnection connection = new(BuildStrictConnectionString(server.EndPoint.Port, serverCertificate: null));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertStrictHandshakeFailure(ex, connection, server);
        }

        #endregion

        #region Attempt isolation

        /// <summary>
        /// After routing, a synchronous open publishes only the routed (terminal) attempt's values:
        /// the gateway negotiates login-only TLS and the target negotiates none.
        /// </summary>
        [Fact]
        public void Open_Routed_PublishesTerminalAttemptValuesOnly()
        {
            // Arrange
            using TdsServer target = StartServer(ServerEncryption.NotSupported);
            using RoutingTdsServer router = StartRouter(target.EndPoint.Port);
            using SqlConnection connection = new(BuildConnectionString(router.EndPoint.Port, readOnlyIntent: true));

            // Act
            connection.Open();

            // Assert
            Assert.Equal(1, target.PreLoginCount);
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.NotSupported);
        }

        /// <summary>
        /// After routing, an asynchronous open publishes only the routed (terminal) attempt's values:
        /// the gateway negotiates login-only TLS and the target negotiates none.
        /// </summary>
        [Fact]
        public async Task OpenAsync_Routed_PublishesTerminalAttemptValuesOnly()
        {
            // Arrange
            using TdsServer target = StartServer(ServerEncryption.NotSupported);
            using RoutingTdsServer router = StartRouter(target.EndPoint.Port);
            using SqlConnection connection = new(BuildConnectionString(router.EndPoint.Port, readOnlyIntent: true));

            // Act
            await connection.OpenAsync();

            // Assert
            Assert.Equal(1, target.PreLoginCount);
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.NotSupported);
        }

        /// <summary>
        /// When the routed target is unreachable during a synchronous open, the exception does not
        /// carry the gateway attempt's values.
        /// </summary>
        [Fact]
        public void Open_RoutedTargetUnreachable_ExceptionDoesNotCarryGatewayValues()
        {
            // Arrange
            using RoutingTdsServer router = StartRouter(GetUnusedPort());
            using SqlConnection connection = new(BuildConnectionString(router.EndPoint.Port, readOnlyIntent: true, connectTimeout: 5));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            Assert.True(router.PreLoginCount >= 1);
            AssertEmpty(ex.EncryptionInfo);
        }

        /// <summary>
        /// When the routed target is unreachable during an asynchronous open, the exception does not
        /// carry the gateway attempt's values.
        /// </summary>
        [Fact]
        public async Task OpenAsync_RoutedTargetUnreachable_ExceptionDoesNotCarryGatewayValues()
        {
            // Arrange
            using RoutingTdsServer router = StartRouter(GetUnusedPort());
            using SqlConnection connection = new(BuildConnectionString(router.EndPoint.Port, readOnlyIntent: true, connectTimeout: 5));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            Assert.True(router.PreLoginCount >= 1);
            AssertEmpty(ex.EncryptionInfo);
        }

        /// <summary>
        /// After a transient login error, a synchronous open publishes the successful retry's values.
        /// </summary>
        [Fact]
        public void Open_TransientErrorRetried_PublishesSuccessfulAttemptValues()
        {
            // Arrange
            using TransientTdsErrorTdsServer server = StartTransientErrorServer();
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, connectRetryCount: 1));

            // Act
            connection.Open();

            // Assert
            Assert.Equal(2, server.PreLoginCount);
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.Off);
        }

        /// <summary>
        /// After a transient login error, an asynchronous open publishes the successful retry's values.
        /// </summary>
        [Fact]
        public async Task OpenAsync_TransientErrorRetried_PublishesSuccessfulAttemptValues()
        {
            // Arrange
            using TransientTdsErrorTdsServer server = StartTransientErrorServer();
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, connectRetryCount: 1));

            // Act
            await connection.OpenAsync();

            // Assert
            Assert.Equal(2, server.PreLoginCount);
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.Off);
        }

        #endregion

        #region Post-login paths

        /// <summary>
        /// When a synchronous command transparently reconnects after the server drops the
        /// connection, the reconnected login publishes a new snapshot, and the snapshot the caller
        /// held from the original login is unchanged. Recovery requires the reconnected login to
        /// preserve the original encryption, so both logins negotiate the same values.
        /// </summary>
        [Fact]
        public void ExecuteNonQuery_ReconnectAfterServerDrop_PublishesNewSnapshot()
        {
            // Arrange
            TdsServerArguments arguments = CreateArguments<TdsServerArguments>(ServerEncryption.Required);
            using DisconnectableTdsServer server = new(arguments);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, connectRetryCount: 2));
            connection.Open();
            SqlConnectionEncryptionInfo held = connection.EncryptionInfo;

            // Act
            DisconnectAndExecute(server, connection);

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.NotSame(held, connection.EncryptionInfo);
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.Required);
            AssertBypassedCertificateFields(connection.EncryptionInfo, ServerEncryption.Required);
            AssertNegotiated(held, ServerEncryption.Required);
        }

        /// <summary>
        /// When an asynchronous command transparently reconnects after the server drops the
        /// connection, the reconnected login publishes a new snapshot, and the snapshot the caller
        /// held from the original login is unchanged. Recovery requires the reconnected login to
        /// preserve the original encryption, so both logins negotiate the same values.
        /// </summary>
        [Fact]
        public async Task ExecuteNonQueryAsync_ReconnectAfterServerDrop_PublishesNewSnapshot()
        {
            // Arrange
            TdsServerArguments arguments = CreateArguments<TdsServerArguments>(ServerEncryption.Required);
            using DisconnectableTdsServer server = new(arguments);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port, connectRetryCount: 2));
            await connection.OpenAsync();
            SqlConnectionEncryptionInfo held = connection.EncryptionInfo;

            // Act
            await DisconnectAndExecuteAsync(server, connection);

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.NotSame(held, connection.EncryptionInfo);
            AssertNegotiated(connection.EncryptionInfo, ServerEncryption.Required);
            AssertBypassedCertificateFields(connection.EncryptionInfo, ServerEncryption.Required);
            AssertNegotiated(held, ServerEncryption.Required);
        }

        /// <summary>
        /// A synchronous non-pooled open that logs in successfully but then fails to enlist in the
        /// ambient transaction throws an exception that carries the negotiated encryption info,
        /// because enlistment is part of opening the connection.
        /// </summary>
        [Fact]
        public void Open_AmbientTransactionEnlistmentFails_ExceptionCarriesNegotiatedValues()
        {
            // Arrange
            // The simulated server cannot process transaction manager requests, so enlistment fails.
            using TdsServer server = StartServer(ServerEncryption.Required);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            using TransactionScope scope = new();

            // Act
            SqlException ex = Assert.Throws<SqlException>(connection.Open);

            // Assert
            AssertEnlistmentFailure(ex, connection, server, expectedLogins: 1);
        }

        /// <summary>
        /// An asynchronous non-pooled open that logs in successfully but then fails to enlist in the
        /// ambient transaction throws an exception that carries the negotiated encryption info,
        /// because enlistment is part of opening the connection.
        /// </summary>
        [Fact]
        public async Task OpenAsync_AmbientTransactionEnlistmentFails_ExceptionCarriesNegotiatedValues()
        {
            // Arrange
            // The simulated server cannot process transaction manager requests, so enlistment fails.
            using TdsServer server = StartServer(ServerEncryption.Required);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));
            using TransactionScope scope = new(TransactionScopeAsyncFlowOption.Enabled);

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(connection.OpenAsync);

            // Assert
            AssertEnlistmentFailure(ex, connection, server, expectedLogins: 1);
        }

        /// <summary>
        /// A synchronous pooled checkout that fails to enlist the idle physical connection in the
        /// ambient transaction throws an exception that carries the encryption info the physical
        /// connection negotiated when it logged in, and performs no login.
        /// </summary>
        [Fact]
        public void Open_PooledCheckoutEnlistmentFails_ExceptionCarriesNegotiatedValues()
        {
            // Arrange
            // Pool an idle physical connection, then check it out inside a transaction scope.
            using TdsServer server = StartServer(ServerEncryption.Required);
            string connectionString = BuildPooledConnectionString(server.EndPoint.Port);
            using (SqlConnection pooled = new(connectionString))
            {
                pooled.Open();
            }
            using SqlConnection connection = new(connectionString);

            try
            {
                using TransactionScope scope = new();

                // Act
                SqlException ex = Assert.Throws<SqlException>(connection.Open);

                // Assert
                AssertEnlistmentFailure(ex, connection, server, expectedLogins: 1);
            }
            finally
            {
                // Cleanup
                SqlConnection.ClearPool(connection);
            }
        }

        /// <summary>
        /// An asynchronous pooled checkout that fails to enlist the idle physical connection in the
        /// ambient transaction throws an exception that carries the encryption info the physical
        /// connection negotiated when it logged in, and performs no login.
        /// </summary>
        [Fact]
        public async Task OpenAsync_PooledCheckoutEnlistmentFails_ExceptionCarriesNegotiatedValues()
        {
            // Arrange
            // Pool an idle physical connection, then check it out inside a transaction scope.
            using TdsServer server = StartServer(ServerEncryption.Required);
            string connectionString = BuildPooledConnectionString(server.EndPoint.Port);
            using (SqlConnection pooled = new(connectionString))
            {
                await pooled.OpenAsync();
            }
            using SqlConnection connection = new(connectionString);

            try
            {
                using TransactionScope scope = new(TransactionScopeAsyncFlowOption.Enabled);

                // Act
                SqlException ex = await Assert.ThrowsAsync<SqlException>(connection.OpenAsync);

                // Assert
                AssertEnlistmentFailure(ex, connection, server, expectedLogins: 1);
            }
            finally
            {
                // Cleanup
                SqlConnection.ClearPool(connection);
            }
        }

        /// <summary>
        /// A synchronous pooled checkout whose session isolation-level reset is rejected by the
        /// server throws an exception that carries the encryption info the physical connection
        /// negotiated when it logged in, and performs no login.
        /// </summary>
        [Fact]
        public void Open_PooledIsolationResetFails_ExceptionCarriesNegotiatedValues()
        {
            // Arrange
            // Pool an idle physical connection whose isolation level must be reset on checkout,
            // and make the server reject the reset batch.
            using LocalAppContextSwitchesHelper switches = new();
            switches.EnableTransactionIsolationLevelReset = true;
            using TransientTdsErrorTdsServer server = StartIsolationLevelResetServer();
            string connectionString = BuildPooledConnectionString(server.EndPoint.Port);
            PoolDirtyIsolationLevelConnection(connectionString);
            server.SetErrorBehavior(true, IsolationLevelResetErrorNumber);
            using SqlConnection connection = new(connectionString);

            try
            {
                // Act
                SqlException ex = Assert.Throws<SqlException>(connection.Open);

                // Assert
                AssertIsolationLevelResetFailure(ex, connection, server);
            }
            finally
            {
                // Cleanup
                SqlConnection.ClearPool(connection);
            }
        }

        /// <summary>
        /// An asynchronous pooled checkout whose session isolation-level reset is rejected by the
        /// server throws an exception that carries the encryption info the physical connection
        /// negotiated when it logged in, and performs no login.
        /// </summary>
        [Fact]
        public async Task OpenAsync_PooledIsolationResetFails_ExceptionCarriesNegotiatedValues()
        {
            // Arrange
            // Pool an idle physical connection whose isolation level must be reset on checkout,
            // and make the server reject the reset batch.
            using LocalAppContextSwitchesHelper switches = new();
            switches.EnableTransactionIsolationLevelReset = true;
            using TransientTdsErrorTdsServer server = StartIsolationLevelResetServer();
            string connectionString = BuildPooledConnectionString(server.EndPoint.Port);
            PoolDirtyIsolationLevelConnection(connectionString);
            server.SetErrorBehavior(true, IsolationLevelResetErrorNumber);
            using SqlConnection connection = new(connectionString);

            try
            {
                // Act
                SqlException ex = await Assert.ThrowsAsync<SqlException>(connection.OpenAsync);

                // Assert
                AssertIsolationLevelResetFailure(ex, connection, server);
            }
            finally
            {
                // Cleanup
                SqlConnection.ClearPool(connection);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The error number the server returns for the rejected isolation-level reset batch.
        /// </summary>
        private const uint IsolationLevelResetErrorNumber = 50000;

        /// <summary>
        /// Starts a server, requiring TLS, whose transient errors are non-fatal so that a rejected
        /// batch surfaces as an ordinary <see cref="SqlException"/>. Errors are disabled until
        /// <see cref="TransientTdsErrorTdsServer.SetErrorBehavior"/> enables them.
        /// </summary>
        private TransientTdsErrorTdsServer StartIsolationLevelResetServer()
        {
            TransientTdsErrorTdsServerArguments arguments =
                CreateArguments<TransientTdsErrorTdsServerArguments>(ServerEncryption.Required);
            arguments.ErrorClass = 16;
            TransientTdsErrorTdsServer server = new(arguments);
            server.Start();
            return server;
        }

        /// <summary>
        /// Opens a pooled connection, marks its session isolation level as changed, and returns it
        /// to the pool, so the next checkout must reset the isolation level. The simulated server
        /// does not implement transactions, so the changed state is seeded directly.
        /// </summary>
        private static void PoolDirtyIsolationLevelConnection(string connectionString)
        {
            using SqlConnection pooled = new(connectionString);
            pooled.Open();
            ((Connection.SqlConnectionInternal)pooled.InnerConnection).IsolationLevelDirty = true;
        }

        /// <summary>
        /// Asserts that a rejected isolation-level reset on pooled checkout surfaced the server's
        /// error with the negotiated encryption info, left the connection without a snapshot, and
        /// did not cause an additional login.
        /// </summary>
        private static void AssertIsolationLevelResetFailure(
            SqlException ex,
            SqlConnection connection,
            TransientTdsErrorTdsServer server)
        {
            Assert.Equal((int)IsolationLevelResetErrorNumber, ex.Number);
            Assert.Equal(1, server.Login7Count);
            AssertNegotiated(ex.EncryptionInfo, ServerEncryption.Required);
            AssertCertificateFieldsNull(ex.EncryptionInfo);
            AssertEmpty(connection.EncryptionInfo);
            Assert.NotEqual(ConnectionState.Open, connection.State);
        }

        /// <summary>
        /// A simulated server that can drop every connected client while it keeps listening, so
        /// a connection with retries enabled reconnects transparently.
        /// </summary>
        private sealed class DisconnectableTdsServer : TdsServer
        {
            /// <summary>
            /// Starts the server with the given arguments. The server reads the arguments for each
            /// pre-login, so later changes apply to the next login.
            /// </summary>
            public DisconnectableTdsServer(TdsServerArguments arguments) : base(arguments)
            {
                Start();
            }

            /// <summary>
            /// Drops every connected client.
            /// </summary>
            public new void DisconnectAllClients() => base.DisconnectAllClients();
        }

        /// <summary>
        /// Drops all clients, then runs a synchronous command until the client detects the drop
        /// and a new login reaches the server. A command can succeed on the stale socket before
        /// the drop is observed, so the server is dropped again until a new login is seen.
        /// </summary>
        private static void DisconnectAndExecute(DisconnectableTdsServer server, SqlConnection connection)
        {
            int loginsBefore = server.Login7Count;
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (server.Login7Count == loginsBefore)
            {
                Assert.True(stopwatch.Elapsed < ReconnectTimeout, "The connection did not reconnect.");
                server.DisconnectAllClients();
                Thread.Sleep(50);
                using SqlCommand command = new("SELECT 1", connection);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Drops all clients, then runs an asynchronous command until the client detects the drop
        /// and a new login reaches the server. A command can succeed on the stale socket before
        /// the drop is observed, so the server is dropped again until a new login is seen.
        /// </summary>
        private static async Task DisconnectAndExecuteAsync(DisconnectableTdsServer server, SqlConnection connection)
        {
            int loginsBefore = server.Login7Count;
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (server.Login7Count == loginsBefore)
            {
                Assert.True(stopwatch.Elapsed < ReconnectTimeout, "The connection did not reconnect.");
                server.DisconnectAllClients();
                await Task.Delay(50);
                using SqlCommand command = new("SELECT 1", connection);
                await command.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// The maximum time allowed for a dropped connection to reconnect.
        /// </summary>
        private static readonly TimeSpan ReconnectTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Asserts that a failure to enlist after a completed login carries the negotiated
        /// encryption info, leaves the connection without a snapshot, and did not cause an additional login. Then
        /// stops the server and asserts that it failed only because it cannot process the
        /// client's transaction manager request, which is what made enlistment fail.
        /// </summary>
        private static void AssertEnlistmentFailure(
            SqlException ex,
            SqlConnection connection,
            TdsServer server,
            int expectedLogins)
        {
            Assert.Equal(expectedLogins, server.Login7Count);
            AssertNegotiated(ex.EncryptionInfo, ServerEncryption.Required);
            AssertCertificateFieldsNull(ex.EncryptionInfo);
            AssertEmpty(connection.EncryptionInfo);
            Assert.NotEqual(ConnectionState.Open, connection.State);

            // Depending on teardown ordering, the server may report the failure wrapped by the
            // connection's disposal, so assert on the base exception.
            AggregateException serverFailure = Assert.Throws<AggregateException>(server.Dispose);
            Assert.All(serverFailure.InnerExceptions, inner =>
            {
                NotImplementedException notImplemented = Assert.IsType<NotImplementedException>(inner.GetBaseException());
                Assert.Contains("TransactionManager", notImplemented.Message);
            });
        }

        /// <summary>
        /// Starts a simulated server with the given encryption setting.
        /// </summary>
        private TdsServer StartServer(ServerEncryption serverEncryption)
        {
            TdsServer server = new(CreateArguments<TdsServerArguments>(serverEncryption));
            server.Start();
            return server;
        }

        /// <summary>
        /// Starts a server that omits encryption support from its pre-login response. A server
        /// configured with <c>NotSupported</c> would itself fault on the client's encryption request.
        /// </summary>
        private static TdsServer StartServerWithoutEncryptionOption()
        {
            TdsServer server = new(new TdsServerArguments { Encryption = TDSPreLoginTokenEncryptionType.None });
            server.Start();
            return server;
        }

        /// <summary>
        /// Starts a server whose certificate the client cannot trust, and which expects the TLS
        /// handshake to fail.
        /// </summary>
        private TdsServer StartUntrustedCertificateServer(ServerEncryption serverEncryption)
        {
            TdsServerArguments arguments = CreateArguments<TdsServerArguments>(serverEncryption);
            arguments.ExpectTlsHandshakeFailure = true;
            TdsServer server = new(arguments);
            server.Start();
            return server;
        }

        /// <summary>
        /// Starts a server that establishes TLS before pre-login, as <c>Encrypt=Strict</c>
        /// (TDS 8) requires. When <paramref name="expectTlsHandshakeFailure"/> is set, the server
        /// expects the client to reject its certificate.
        /// </summary>
        private TdsServer StartStrictServer(bool expectTlsHandshakeFailure)
        {
            TdsServerArguments arguments = CreateArguments<TdsServerArguments>(ServerEncryption.Required);
            arguments.TlsFirst = true;
            arguments.ExpectTlsHandshakeFailure = expectTlsHandshakeFailure;
            TdsServer server = new(arguments);
            server.Start();
            return server;
        }

        /// <summary>
        /// Starts a server that requires read-only intent, so it rejects a read-write login with
        /// error 18456 after pre-login and any TLS handshake have completed.
        /// </summary>
        private RoutingTdsServer StartReadOnlyRequiredServer(ServerEncryption serverEncryption)
        {
            RoutingTdsServerArguments arguments = CreateArguments<RoutingTdsServerArguments>(serverEncryption);
            arguments.RequireReadOnly = true;
            arguments.RoutingTCPHost = "localhost";
            arguments.RoutingTCPPort = 1;
            RoutingTdsServer server = new(arguments);
            server.Start();
            return server;
        }

        /// <summary>
        /// Starts a gateway that negotiates login-only TLS and routes read-only logins to the given port.
        /// </summary>
        private RoutingTdsServer StartRouter(int targetPort)
        {
            RoutingTdsServerArguments arguments = CreateArguments<RoutingTdsServerArguments>(ServerEncryption.Off);
            arguments.RoutingTCPHost = "localhost";
            arguments.RoutingTCPPort = (ushort)targetPort;
            RoutingTdsServer router = new(arguments);
            router.Start();
            return router;
        }

        /// <summary>
        /// Starts a server, negotiating login-only TLS, that fails the first login with transient
        /// error 40613.
        /// </summary>
        private TransientTdsErrorTdsServer StartTransientErrorServer()
        {
            TransientTdsErrorTdsServerArguments arguments = CreateArguments<TransientTdsErrorTdsServerArguments>(ServerEncryption.Off);
            arguments.IsEnabledTransientError = true;
            arguments.Number = 40613;
            TransientTdsErrorTdsServer server = new(arguments);
            server.Start();
            return server;
        }

        /// <summary>
        /// Creates server arguments for the given encryption setting, limited to TLS 1.2.
        /// </summary>
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

        /// <summary>
        /// Builds a pooled connection string with a unique application name, so each test gets its
        /// own pool.
        /// </summary>
        private static string BuildPooledConnectionString(int port) =>
            BuildConnectionString(port, pooling: true, applicationName: $"EncryptionInfoPool_{Guid.NewGuid():N}");

        /// <summary>
        /// Builds a connection string for the simulated server.
        /// </summary>
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

        /// <summary>
        /// Asserts the mode and protocol expected for the given server encryption setting.
        /// </summary>
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
        /// Asserts that a login rejected after pre-login carries the negotiated values on the
        /// exception only, and not in its message.
        /// </summary>
        private void AssertLoginFailureCarriesNegotiatedValues(
            SqlException ex,
            SqlConnection connection,
            ServerEncryption serverEncryption)
        {
            Assert.Equal(18456, ex.Number);
            AssertNegotiated(ex.EncryptionInfo, serverEncryption);
            AssertBypassedCertificateFields(ex.EncryptionInfo, serverEncryption);
            AssertEmpty(connection.EncryptionInfo);
            Assert.DoesNotContain("Tls12", ex.Message);
            Assert.DoesNotContain("LoginOnly", ex.Message);
        }

        /// <summary>
        /// Asserts the values reported for a certificate-validation failure. The fixture certificate is
        /// self-signed and untrusted, and the server allows only TLS 1.2. The pre-login response
        /// selected full encryption, so the mode is reported even though the TLS handshake failed.
        /// Native SNI rejects the certificate inside the TLS handshake before reporting a protocol,
        /// so no protocol or cipher suite is reported. Managed SNI records the protocol and cipher
        /// suite SslStream agreed before raising the validation callback. Certificate fields are
        /// asserted by <c>ManagedSniCertificateCaptureTests</c>.
        /// </summary>
        private static void AssertUntrustedCertificateFailure(SqlException ex, SqlConnection connection, TdsServer server)
        {
            Assert.True(server.PreLoginCount >= 1);
            Assert.Equal(SqlConnectionEncryptionMode.Full, ex.EncryptionInfo.NegotiatedEncryptionMode);
            if (LocalAppContextSwitches.UseManagedNetworking)
            {
                Assert.Equal(SslProtocols.Tls12, ex.EncryptionInfo.NegotiatedEncryptionProtocol);
                Assert.InRange(Assert.NotNull(ex.EncryptionInfo.NegotiatedCipherSuiteId), 1, 0xFFFF);
            }
            else
            {
                Assert.Null(ex.EncryptionInfo.NegotiatedEncryptionProtocol);
                Assert.Null(ex.EncryptionInfo.NegotiatedCipherSuiteId);
            }

            AssertEmpty(connection.EncryptionInfo);
        }

        /// <summary>
        /// Builds a <c>Encrypt=Strict</c> connection string. Strict always validates the server
        /// certificate and ignores <c>TrustServerCertificate</c>, so the self-signed fixture
        /// certificate is trusted only when pinned through <paramref name="serverCertificate"/>.
        /// </summary>
        private static string BuildStrictConnectionString(int port, string? serverCertificate)
        {
            SqlConnectionStringBuilder builder = new(BuildConnectionString(
                port,
                SqlConnectionEncryptOption.Strict,
                trustServerCertificate: false));
            if (serverCertificate is not null)
            {
                builder.ServerCertificate = serverCertificate;
            }
            return builder.ConnectionString;
        }

        /// <summary>
        /// Asserts the values published by a successful <c>Encrypt=Strict</c> open.
        /// </summary>
        private static void AssertStrict(SqlConnectionEncryptionInfo info)
        {
            Assert.Equal(SqlConnectionEncryptionMode.Strict, info.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, info.NegotiatedEncryptionProtocol);
            AssertCertificateFieldsNull(info);
        }

        /// <summary>
        /// Asserts the values reported when the <c>Encrypt=Strict</c> TLS handshake fails. Strict is
        /// recorded before the handshake starts, so it is reported, but the handshake never
        /// reports a protocol or cipher suite. No pre-login message is exchanged.
        /// </summary>
        private static void AssertStrictHandshakeFailure(SqlException ex, SqlConnection connection, TdsServer server)
        {
            Assert.Equal(0, server.PreLoginCount);
            Assert.Equal(SqlConnectionEncryptionMode.Strict, ex.EncryptionInfo.NegotiatedEncryptionMode);
            Assert.Null(ex.EncryptionInfo.NegotiatedEncryptionProtocol);
            Assert.Null(ex.EncryptionInfo.NegotiatedCipherSuiteId);
            AssertEmpty(connection.EncryptionInfo);
        }

        /// <summary>
        /// Asserts the certificate fields for tests that use <c>TrustServerCertificate=true</c>.
        /// SqlClient never evaluates the certificate, so the verdict and chain status are always
        /// unavailable. Managed SNI still records the presented certificate and cipher suite whenever
        /// a TLS handshake took place; native SNI does not capture certificate details.
        /// </summary>
        private void AssertBypassedCertificateFields(SqlConnectionEncryptionInfo info, ServerEncryption serverEncryption)
        {
            if (serverEncryption != ServerEncryption.NotSupported && LocalAppContextSwitches.UseManagedNetworking)
            {
                Assert.NotNull(info.PresentedServerCertificateRawData);
                Assert.Equal(_certificate.RawData, info.PresentedServerCertificateRawData.Value.ToArray());
                using X509Certificate2? presented = info.GetServerCertificate();
                Assert.NotNull(presented);
                Assert.Equal(_certificate.RawData, presented.RawData);
            }
            else
            {
                Assert.Null(info.PresentedServerCertificateRawData);
                Assert.Null(info.GetServerCertificate());
            }

            Assert.Null(info.ValidationErrors);
            Assert.Null(info.ChainStatus);

            if (serverEncryption == ServerEncryption.NotSupported)
            {
                // No TLS handshake took place, so no cipher suite was negotiated.
                Assert.Null(info.NegotiatedCipherSuiteId);
            }
            else if (LocalAppContextSwitches.UseManagedNetworking)
            {
                Assert.InRange(Assert.NotNull(info.NegotiatedCipherSuiteId), 1, 0xFFFF);
            }
        }

        /// <summary>
        /// Returns a loopback port with no listener.
        /// </summary>
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

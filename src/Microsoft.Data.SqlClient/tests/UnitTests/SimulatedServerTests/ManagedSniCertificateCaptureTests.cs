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
        /// <summary>
        /// Creates the "localhost" certificate and a certificate for an unrelated host name.
        /// </summary>
        public ManagedSniCaptureCertificateFixture()
        {
            Localhost = CreateCertificate(
                "localhost", new[] { "localhost" }, new[] { "127.0.0.1" });
            OtherHost = CreateCertificate(
                "other.contoso.com", Array.Empty<string>(), Array.Empty<string>());
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

        /// <summary>Receives the shared server certificates.</summary>
        public ManagedSniCertificateCaptureTests(ManagedSniCaptureCertificateFixture fixture)
        {
            _fixture = fixture;
        }
        #region Successful opens

        /// <summary>
        /// With TrustServerCertificate, a synchronous open publishes the presented certificate,
        /// mode, protocol and the SslStream's cipher suite, but no verdict or chain status,
        /// because SqlClient did not evaluate the certificate.
        /// </summary>
        [Fact]
        public void Open_TrustServerCertificate_PublishesCertificateWithoutVerdict()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);
            using SqlConnection connection = new(
                BuildConnectionString(server.EndPoint.Port, trustServerCertificate: true));

            // Act
            connection.Open();

            // Assert
            AssertTrustedCertificateSnapshot(connection);
        }

        /// <summary>
        /// With TrustServerCertificate, an asynchronous open publishes the presented certificate,
        /// mode, protocol and the SslStream's cipher suite, but no verdict or chain status,
        /// because SqlClient did not evaluate the certificate.
        /// </summary>
        [Fact]
        public async Task OpenAsync_TrustServerCertificate_PublishesCertificateWithoutVerdict()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);
            using SqlConnection connection = new(
                BuildConnectionString(server.EndPoint.Port, trustServerCertificate: true));

            // Act
            await connection.OpenAsync();

            // Assert
            AssertTrustedCertificateSnapshot(connection);
        }

        /// <summary>
        /// With a matching ServerCertificate pin, a synchronous open publishes the certificate
        /// with no validation errors and no chain status, because the pin, not the chain,
        /// decides trust.
        /// </summary>
        [Fact]
        public void Open_MatchingServerCertificate_PublishesCertificateAndNoErrors()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);
            using TempCertFile pin = new(_fixture.Localhost);
            using SqlConnection connection = new(
                BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));

            // Act
            connection.Open();

            // Assert
            AssertMatchingPinSnapshot(connection.EncryptionInfo);
        }

        /// <summary>
        /// With a matching ServerCertificate pin, an asynchronous open publishes the certificate
        /// with no validation errors and no chain status, because the pin, not the chain,
        /// decides trust.
        /// </summary>
        [Fact]
        public async Task OpenAsync_MatchingServerCertificate_PublishesCertificateAndNoErrors()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);
            using TempCertFile pin = new(_fixture.Localhost);
            using SqlConnection connection = new(
                BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));

            // Act
            await connection.OpenAsync();

            // Assert
            AssertMatchingPinSnapshot(connection.EncryptionInfo);
        }

        #endregion

        #region Certificate validation failures

        /// <summary>
        /// An untrusted self-signed certificate fails a synchronous open; the exception carries
        /// the certificate, ChainError, a non-clean chain status and the observed negotiation,
        /// the connection itself stays empty, and nothing leaks into the message.
        /// </summary>
        [Fact]
        public void Open_UntrustedCertificate_ExceptionCarriesChainErrorAndCertificate()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost, expectTlsHandshakeFailure: true);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertUntrustedCertificateFailure(ex, connection);

            // The suite read inside the validation callback is the real one: a trusted handshake
            // with the same server and protocol negotiates the same suite.
            using SqlConnection trusted = new(
                BuildConnectionString(server.EndPoint.Port, trustServerCertificate: true));
            trusted.Open();
            Assert.Equal(
                GetClientSslStreamCipherSuite(trusted), ex.EncryptionInfo.NegotiatedCipherSuiteId);
        }

        /// <summary>
        /// An untrusted self-signed certificate fails an asynchronous open; the exception carries
        /// the certificate, ChainError, a non-clean chain status and the observed negotiation,
        /// the connection itself stays empty, and nothing leaks into the message.
        /// </summary>
        [Fact]
        public async Task OpenAsync_UntrustedCertificate_ExceptionCarriesChainErrorAndCertificate()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost, expectTlsHandshakeFailure: true);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertUntrustedCertificateFailure(ex, connection);

            // The suite read inside the validation callback is the real one: a trusted handshake
            // with the same server and protocol negotiates the same suite.
            using SqlConnection trusted = new(
                BuildConnectionString(server.EndPoint.Port, trustServerCertificate: true));
            await trusted.OpenAsync();
            Assert.Equal(
                GetClientSslStreamCipherSuite(trusted), ex.EncryptionInfo.NegotiatedCipherSuiteId);
        }

        /// <summary>
        /// A certificate whose names do not match the data source fails a synchronous open with
        /// both ChainError and HostNameMismatch in the exception's verdict.
        /// </summary>
        [Fact]
        public void Open_HostNameMismatch_ExceptionCarriesHostNameMismatch()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.OtherHost, expectTlsHandshakeFailure: true);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertHostNameMismatchFailure(ex);
        }

        /// <summary>
        /// A certificate whose names do not match the data source fails an asynchronous open with
        /// both ChainError and HostNameMismatch in the exception's verdict.
        /// </summary>
        [Fact]
        public async Task OpenAsync_HostNameMismatch_ExceptionCarriesHostNameMismatch()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.OtherHost, expectTlsHandshakeFailure: true);
            using SqlConnection connection = new(BuildConnectionString(server.EndPoint.Port));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertHostNameMismatchFailure(ex);
        }

        /// <summary>
        /// On a synchronous open, a HostNameInCertificate matching the certificate removes
        /// HostNameMismatch from the effective verdict, leaving only ChainError.
        /// </summary>
        [Fact]
        public void Open_HostNameInCertificateOverride_ReportsEffectiveVerdict()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.OtherHost, expectTlsHandshakeFailure: true);
            using SqlConnection connection = new(BuildConnectionString(
                server.EndPoint.Port, hostNameInCertificate: "other.contoso.com"));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertHostNameOverrideFailure(ex);
        }

        /// <summary>
        /// On an asynchronous open, a HostNameInCertificate matching the certificate removes
        /// HostNameMismatch from the effective verdict, leaving only ChainError.
        /// </summary>
        [Fact]
        public async Task OpenAsync_HostNameInCertificateOverride_ReportsEffectiveVerdict()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.OtherHost, expectTlsHandshakeFailure: true);
            using SqlConnection connection = new(BuildConnectionString(
                server.EndPoint.Port, hostNameInCertificate: "other.contoso.com"));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertHostNameOverrideFailure(ex);
        }

        /// <summary>
        /// A ServerCertificate pin that does not match the presented certificate fails a
        /// synchronous open with PinnedCertificateMismatch and no chain status.
        /// </summary>
        [Fact]
        public void Open_ServerCertificateMismatch_ExceptionCarriesPinnedMismatch()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost, expectTlsHandshakeFailure: true);
            using TempCertFile pin = new(_fixture.OtherHost);
            using SqlConnection connection = new(
                BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertPinnedMismatchFailure(ex, _fixture.Localhost);
            AssertNoLeak(ex, ex.EncryptionInfo);
        }

        /// <summary>
        /// A ServerCertificate pin that does not match the presented certificate fails an
        /// asynchronous open with PinnedCertificateMismatch and no chain status.
        /// </summary>
        [Fact]
        public async Task OpenAsync_ServerCertificateMismatch_ExceptionCarriesPinnedMismatch()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost, expectTlsHandshakeFailure: true);
            using TempCertFile pin = new(_fixture.OtherHost);
            using SqlConnection connection = new(
                BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertPinnedMismatchFailure(ex, _fixture.Localhost);
            AssertNoLeak(ex, ex.EncryptionInfo);
        }

        #endregion

        #region Per-attempt isolation

        /// <summary>
        /// Within one synchronous login, a routed connection is a new attempt: the routing
        /// server's certificate, presented first, is not published, and the terminal attempt's
        /// certificate is.
        /// </summary>
        [Fact]
        public void Open_Routed_PublishesOnlyTerminalAttemptCertificate()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);
            using RoutingTdsServer router = StartRouter(_fixture.OtherHost, server.EndPoint.Port);
            using SqlConnection connection = new(
                BuildConnectionString(router.EndPoint.Port, trustServerCertificate: true));

            // Act
            connection.Open();

            // Assert
            AssertRoutedTerminalAttemptSnapshot(connection.EncryptionInfo);
        }

        /// <summary>
        /// Within one asynchronous login, a routed connection is a new attempt: the routing
        /// server's certificate, presented first, is not published, and the terminal attempt's
        /// certificate is.
        /// </summary>
        [Fact]
        public async Task OpenAsync_Routed_PublishesOnlyTerminalAttemptCertificate()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost);
            using RoutingTdsServer router = StartRouter(_fixture.OtherHost, server.EndPoint.Port);
            using SqlConnection connection = new(
                BuildConnectionString(router.EndPoint.Port, trustServerCertificate: true));

            // Act
            await connection.OpenAsync();

            // Assert
            AssertRoutedTerminalAttemptSnapshot(connection.EncryptionInfo);
        }

        /// <summary>
        /// Within one synchronous login, when the routing server's certificate passes validation
        /// and the routed server's certificate fails it, the exception reports only the terminal
        /// attempt's certificate and verdict.
        /// </summary>
        [Fact]
        public void Open_RoutedTerminalAttemptFails_ExceptionCarriesOnlyTerminalAttempt()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost, expectTlsHandshakeFailure: true);
            using RoutingTdsServer router = StartRouter(_fixture.OtherHost, server.EndPoint.Port);
            // The pin matches the routing server's certificate only.
            using TempCertFile pin = new(_fixture.OtherHost);
            using SqlConnection connection = new(
                BuildConnectionString(router.EndPoint.Port, serverCertificate: pin.Path));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertPinnedMismatchFailure(ex, _fixture.Localhost);
        }

        /// <summary>
        /// Within one asynchronous login, when the routing server's certificate passes validation
        /// and the routed server's certificate fails it, the exception reports only the terminal
        /// attempt's certificate and verdict.
        /// </summary>
        [Fact]
        public async Task OpenAsync_RoutedTerminalAttemptFails_ExceptionCarriesOnlyTerminalAttempt()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartServer(_fixture.Localhost, expectTlsHandshakeFailure: true);
            using RoutingTdsServer router = StartRouter(_fixture.OtherHost, server.EndPoint.Port);
            // The pin matches the routing server's certificate only.
            using TempCertFile pin = new(_fixture.OtherHost);
            using SqlConnection connection = new(
                BuildConnectionString(router.EndPoint.Port, serverCertificate: pin.Path));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertPinnedMismatchFailure(ex, _fixture.Localhost);
        }

        /// <summary>
        /// Within one synchronous login, when the routed attempt fails in pre-login, before any
        /// certificate is presented, the exception carries nothing from the routing server's
        /// completed handshake: each attempt starts with a clean capture.
        /// </summary>
        [Fact]
        public void Open_RoutedTerminalAttemptFailsBeforeTls_ExceptionCarriesNoCertificate()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartUnencryptedServer();
            using RoutingTdsServer router = StartRouter(_fixture.OtherHost, server.EndPoint.Port);
            using SqlConnection connection = new(
                BuildConnectionString(router.EndPoint.Port, trustServerCertificate: true));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            AssertNoCertificateCaptured(ex.EncryptionInfo);
        }

        /// <summary>
        /// Within one asynchronous login, when the routed attempt fails in pre-login, before any
        /// certificate is presented, the exception carries nothing from the routing server's
        /// completed handshake: each attempt starts with a clean capture.
        /// </summary>
        [Fact]
        public async Task OpenAsync_RoutedTerminalAttemptFailsBeforeTls_ExceptionCarriesNoCertificate()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer server = StartUnencryptedServer();
            using RoutingTdsServer router = StartRouter(_fixture.OtherHost, server.EndPoint.Port);
            using SqlConnection connection = new(
                BuildConnectionString(router.EndPoint.Port, trustServerCertificate: true));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

            // Assert
            AssertNoCertificateCaptured(ex.EncryptionInfo);
        }

        /// <summary>
        /// A failed synchronous open on one connection does not leak its certificate into a later
        /// synchronous open on a different connection: each connection publishes only its own
        /// certificate.
        /// </summary>
        [Fact]
        public void Open_FailureThenSuccessOnNewConnection_DoesNotCarryPreviousCertificate()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer otherServer = StartServer(_fixture.OtherHost,
                expectTlsHandshakeFailure: true);
            using TdsServer server = StartServer(_fixture.Localhost);
            using TempCertFile pin = new(_fixture.Localhost);
            using SqlConnection failing = new(
                BuildConnectionString(otherServer.EndPoint.Port, serverCertificate: pin.Path));
            using SqlConnection connection = new(
                BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));

            // Act
            SqlException ex = Assert.Throws<SqlException>(() => failing.Open());
            connection.Open();

            // Assert
            AssertEachConnectionPublishesOwnCertificate(ex, connection);
        }

        /// <summary>
        /// A failed asynchronous open on one connection does not leak its certificate into a later
        /// asynchronous open on a different connection: each connection publishes only its own
        /// certificate.
        /// </summary>
        [Fact]
        public async Task OpenAsync_FailureThenSuccessOnNewConnection_DoesNotCarryPreviousCertificate()
        {
            // Arrange
            using LocalAppContextSwitchesHelper? switches = ForceManagedSni();
            using TdsServer otherServer = StartServer(_fixture.OtherHost,
                expectTlsHandshakeFailure: true);
            using TdsServer server = StartServer(_fixture.Localhost);
            using TempCertFile pin = new(_fixture.Localhost);
            using SqlConnection failing = new(
                BuildConnectionString(otherServer.EndPoint.Port, serverCertificate: pin.Path));
            using SqlConnection connection = new(
                BuildConnectionString(server.EndPoint.Port, serverCertificate: pin.Path));

            // Act
            SqlException ex = await Assert.ThrowsAsync<SqlException>(() => failing.OpenAsync());
            await connection.OpenAsync();

            // Assert
            AssertEachConnectionPublishesOwnCertificate(ex, connection);
        }

        #endregion

        #region Scenario assertions

        /// <summary>
        /// Asserts the TrustServerCertificate snapshot: certificate, mode, protocol and the client
        /// SslStream's cipher suite are published, with no verdict or chain status.
        /// </summary>
        private void AssertTrustedCertificateSnapshot(SqlConnection connection)
        {
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

        /// <summary>
        /// Asserts the matching-pin snapshot: the certificate with no validation errors and no
        /// chain status, because the chain is not consulted when a pin decides trust.
        /// </summary>
        private void AssertMatchingPinSnapshot(SqlConnectionEncryptionInfo info)
        {
            Assert.Equal(SqlConnectionEncryptionMode.Full, info.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, info.NegotiatedEncryptionProtocol);
            AssertCertificate(_fixture.Localhost, info);
            Assert.Equal(SqlServerCertificateValidationErrors.None, info.ValidationErrors);
            Assert.Null(info.ChainStatus);
        }

        /// <summary>
        /// Asserts the untrusted-certificate failure: the exception carries the certificate,
        /// ChainError, a non-clean chain status and the observed negotiation, the connection stays
        /// empty, and nothing leaks into the message.
        /// </summary>
        private void AssertUntrustedCertificateFailure(SqlException ex, SqlConnection connection)
        {
            SqlConnectionEncryptionInfo info = ex.EncryptionInfo;
            AssertCertificate(_fixture.Localhost, info);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, info.ValidationErrors);
            Assert.NotNull(info.ChainStatus);
            Assert.NotEqual(X509ChainStatusFlags.NoError, info.ChainStatus);
            // Certificate validation failed after the protocol was agreed; the observed values
            // are kept.
            AssertObservedNegotiation(info);
            Assert.True(connection.EncryptionInfo.IsEmpty);
            AssertNoLeak(ex, info);
        }

        /// <summary>
        /// Asserts the host-name-mismatch failure: ChainError and HostNameMismatch, a chain status,
        /// the observed negotiation, and no leak into the message.
        /// </summary>
        private void AssertHostNameMismatchFailure(SqlException ex)
        {
            SqlConnectionEncryptionInfo info = ex.EncryptionInfo;
            AssertCertificate(_fixture.OtherHost, info);
            Assert.Equal(
                SqlServerCertificateValidationErrors.ChainError
                    | SqlServerCertificateValidationErrors.HostNameMismatch,
                info.ValidationErrors);
            Assert.NotNull(info.ChainStatus);
            AssertObservedNegotiation(info);
            AssertNoLeak(ex, info);
        }

        /// <summary>
        /// Asserts the HostNameInCertificate-override failure: the platform reported a name
        /// mismatch, but the override resolves it, so only the untrusted chain remains in
        /// SqlClient's effective verdict.
        /// </summary>
        private void AssertHostNameOverrideFailure(SqlException ex)
        {
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError,
                ex.EncryptionInfo.ValidationErrors);
            AssertCertificate(_fixture.OtherHost, ex.EncryptionInfo);
            AssertObservedNegotiation(ex.EncryptionInfo);
        }

        /// <summary>
        /// Asserts a pin-mismatch failure: the presented certificate, PinnedCertificateMismatch, no
        /// chain status, and the observed negotiation.
        /// </summary>
        private static void AssertPinnedMismatchFailure(SqlException ex, X509Certificate2 presented)
        {
            SqlConnectionEncryptionInfo info = ex.EncryptionInfo;
            AssertCertificate(presented, info);
            Assert.Equal(SqlServerCertificateValidationErrors.PinnedCertificateMismatch,
                info.ValidationErrors);
            Assert.Null(info.ChainStatus);
            AssertObservedNegotiation(info);
        }

        /// <summary>
        /// Asserts that a routed open published only the terminal attempt's certificate, with no
        /// verdict or chain status because TrustServerCertificate bypassed evaluation.
        /// </summary>
        private void AssertRoutedTerminalAttemptSnapshot(SqlConnectionEncryptionInfo info)
        {
            AssertCertificate(_fixture.Localhost, info);
            Assert.Equal(SqlConnectionEncryptionMode.Full, info.NegotiatedEncryptionMode);
            Assert.Null(info.ValidationErrors);
            Assert.Null(info.ChainStatus);
        }

        /// <summary>
        /// Asserts that a snapshot carries no certificate and is otherwise empty.
        /// </summary>
        private static void AssertNoCertificateCaptured(SqlConnectionEncryptionInfo info)
        {
            Assert.Null(info.PresentedServerCertificateRawData);
            Assert.Null(info.GetServerCertificate());
            Assert.True(info.IsEmpty);
        }

        /// <summary>
        /// Asserts that the failed connection's exception carries its own certificate and the later
        /// successful connection publishes only its own certificate with no validation errors.
        /// </summary>
        private void AssertEachConnectionPublishesOwnCertificate(
            SqlException ex, SqlConnection connection)
        {
            AssertCertificate(_fixture.OtherHost, ex.EncryptionInfo);
            AssertCertificate(_fixture.Localhost, connection.EncryptionInfo);
            Assert.Equal(SqlServerCertificateValidationErrors.None,
                connection.EncryptionInfo.ValidationErrors);
        }

        #endregion

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
            global::Microsoft.Data.SqlClient.ManagedSni.SniHandle handle =
                connection.Parser._physicalStateObj.SessionHandle.ManagedHandle;
            Assert.NotNull(handle);
            FieldInfo? field = handle.GetType().GetField(
                "_sslStream", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            SslStream? sslStream = field.GetValue(handle) as SslStream;
            Assert.NotNull(sslStream);
            return (int)sslStream.NegotiatedCipherSuite;
        }

        /// <summary>
        /// Forces managed SNI on Windows for the lifetime of the returned helper; returns null
        /// elsewhere, where managed SNI is already the only implementation.
        /// </summary>
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

        /// <summary>
        /// Starts a simulated server that omits encryption support from its pre-login response,
        /// so a client demanding encryption fails before TLS starts.
        /// </summary>
        private static TdsServer StartUnencryptedServer()
        {
            TdsServer server = new(new TdsServerArguments
            {
                Encryption = TDSPreLoginTokenEncryptionType.None,
            });
            server.Start();
            return server;
        }

        /// <summary>
        /// Starts a TLS 1.2 simulated routing server that requires encryption and, after login,
        /// routes every connection to the given local port.
        /// </summary>
        /// <param name="certificate">The certificate the routing server presents.</param>
        /// <param name="routedPort">The local port of the server to route to.</param>
        private static RoutingTdsServer StartRouter(X509Certificate2 certificate, int routedPort)
        {
            RoutingTdsServer router = new(new RoutingTdsServerArguments
            {
                Encryption = TDSPreLoginTokenEncryptionType.Required,
                EncryptionCertificate = certificate,
                EncryptionProtocols = SslProtocols.Tls12,
                RoutingTCPHost = "localhost",
                RoutingTCPPort = (ushort)routedPort,
                RequireReadOnly = false,
            });
            router.Start();
            return router;
        }

        /// <summary>
        /// Builds a non-pooled, Encrypt=Mandatory connection string for a local simulated server,
        /// with connection retry disabled so each test observes a single login.
        /// </summary>
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

        /// <summary>
        /// Asserts that the published certificate is exactly the expected certificate's DER, both
        /// as raw data and as a reconstructed certificate without a private key.
        /// </summary>
        private static void AssertCertificate(X509Certificate2 expected, SqlConnectionEncryptionInfo info)
        {
            Assert.NotNull(info.PresentedServerCertificateRawData);
            Assert.Equal(expected.RawData, info.PresentedServerCertificateRawData.Value.ToArray());

            using X509Certificate2? certificate = info.GetServerCertificate();
            Assert.NotNull(certificate);
            Assert.Equal(expected.RawData, certificate.RawData);
            Assert.False(certificate.HasPrivateKey);
        }

        /// <summary>
        /// Asserts that neither the exception message nor its string form contains the captured
        /// certificate in hexadecimal, Base64, dashed hexadecimal, or SHA-256 thumbprint form.
        /// </summary>
        private static void AssertNoLeak(SqlException ex, SqlConnectionEncryptionInfo info)
        {
            byte[] der = info.PresentedServerCertificateRawData!.Value.ToArray();
            string[] encodings =
            {
                Convert.ToHexString(der),
                Convert.ToBase64String(der),
                BitConverter.ToString(der),
                Convert.ToHexString(SHA256.HashData(der)),
            };
            foreach (string encoding in encodings)
            {
                Assert.DoesNotContain(encoding, ex.Message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(encoding, ex.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// A temporary DER certificate file usable as a <c>ServerCertificate</c> pin; deleted on
        /// dispose.
        /// </summary>
        private sealed class TempCertFile : IDisposable
        {
            /// <summary>
            /// Writes the public part of <paramref name="certificate"/> to a new temporary file.
            /// </summary>
            public TempCertFile(X509Certificate2 certificate)
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "SqlClient_CapturePin_" + Guid.NewGuid().ToString("N") + ".cer");
                File.WriteAllBytes(Path, certificate.Export(X509ContentType.Cert));
            }

            /// <summary>The full path of the temporary certificate file.</summary>
            public string Path { get; }

            /// <summary>Deletes the file, ignoring failures.</summary>
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

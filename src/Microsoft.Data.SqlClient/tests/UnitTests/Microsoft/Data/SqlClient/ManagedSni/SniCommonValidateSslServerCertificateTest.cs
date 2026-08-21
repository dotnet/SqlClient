// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.ManagedSni;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Tests for <see cref="SniCommon.ValidateSslServerCertificate"/> to ensure
    /// that when a caller supplies a <c>ServerCertificate</c>, the driver always compares it
    /// against the certificate presented by the server — including when the platform reported
    /// no policy errors — and fails closed when the configured file cannot be loaded.
    ///
    /// The tests exercise the shared helper directly.
    /// </summary>
    public class SniCommonValidateSslServerCertificateTest
    {
        /// <summary>
        /// When a ServerCertificate pin is supplied and the presented server certificate
        /// does not match the pin, validation must fail — even if the platform reported
        /// <see cref="SslPolicyErrors.None"/>.  This is the security invariant the pin
        /// exists to enforce.
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_MismatchedPin_PolicyNone_Throws()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");
            using X509Certificate2 pinCert = CreateSelfSignedCertificate("other.contoso.com");
            using TempCertFile pinFile = new(pinCert);

            Assert.Throws<AuthenticationException>(() =>
                SniCommon.ValidateSslServerCertificate(
                    connectionId: Guid.NewGuid(),
                    targetServerName: "server.contoso.com",
                    hostNameInCertificate: null,
                    serverCert: serverCert,
                    validationCertFileName: pinFile.Path,
                    policyErrors: SslPolicyErrors.None));
        }

        /// <summary>
        /// When a ServerCertificate pin is supplied and the presented server certificate
        /// matches the pin exactly, validation succeeds (assuming no policy errors).
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_MatchingPin_PolicyNone_ReturnsTrue()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");
            using TempCertFile pinFile = new(serverCert);

            bool result = SniCommon.ValidateSslServerCertificate(
                connectionId: Guid.NewGuid(),
                targetServerName: "server.contoso.com",
                hostNameInCertificate: null,
                serverCert: serverCert,
                validationCertFileName: pinFile.Path,
                policyErrors: SslPolicyErrors.None);

            Assert.True(result);
        }

        /// <summary>
        /// An exact ServerCertificate match satisfies validation, which is the purpose of the
        /// option: the caller has told us precisely which certificate to accept.  This preserves
        /// the long-standing behavior for servers whose certificate would otherwise fail chain
        /// validation (for example a self-signed or private-CA certificate).
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_MatchingPin_ChainErrors_ReturnsTrue()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");
            using TempCertFile pinFile = new(serverCert);

            bool result = SniCommon.ValidateSslServerCertificate(
                connectionId: Guid.NewGuid(),
                targetServerName: "server.contoso.com",
                hostNameInCertificate: null,
                serverCert: serverCert,
                validationCertFileName: pinFile.Path,
                policyErrors: SslPolicyErrors.RemoteCertificateChainErrors);

            Assert.True(result);
        }

        /// <summary>
        /// A ServerCertificate mismatch fails even when the platform reported a policy error that
        /// would otherwise be evaluated, because the caller's explicit choice of certificate takes
        /// precedence.
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_MismatchedPin_ChainErrors_Throws()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");
            using X509Certificate2 pinCert = CreateSelfSignedCertificate("other.contoso.com");
            using TempCertFile pinFile = new(pinCert);

            Assert.Throws<AuthenticationException>(() =>
                SniCommon.ValidateSslServerCertificate(
                    connectionId: Guid.NewGuid(),
                    targetServerName: "server.contoso.com",
                    hostNameInCertificate: null,
                    serverCert: serverCert,
                    validationCertFileName: pinFile.Path,
                    policyErrors: SslPolicyErrors.RemoteCertificateChainErrors));
        }

        /// <summary>
        /// When a ServerCertificate pin is supplied but the server did not present a
        /// certificate at all (<see cref="SslPolicyErrors.RemoteCertificateNotAvailable"/>,
        /// <c>serverCert == null</c>), the helper must fail with an
        /// <see cref="AuthenticationException"/> — not a <see cref="NullReferenceException"/>
        /// from attempting to dereference the missing server certificate during comparison.
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_Pin_NullServerCert_NotAvailable_Throws()
        {
            using X509Certificate2 pinCert = CreateSelfSignedCertificate("server.contoso.com");
            using TempCertFile pinFile = new(pinCert);

            Assert.Throws<AuthenticationException>(() =>
                SniCommon.ValidateSslServerCertificate(
                    connectionId: Guid.NewGuid(),
                    targetServerName: "server.contoso.com",
                    hostNameInCertificate: null,
                    serverCert: null,
                    validationCertFileName: pinFile.Path,
                    policyErrors: SslPolicyErrors.RemoteCertificateNotAvailable));
        }

        /// <summary>
        /// A matching ServerCertificate must not satisfy validation when the platform reported
        /// <see cref="SslPolicyErrors.RemoteCertificateNotAvailable"/>, even if a non-null
        /// certificate was somehow also handed to the callback.  The two are expected to agree,
        /// but the contradictory combination must fail closed rather than let a comparison stand
        /// in for a certificate the platform said was unavailable.
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_MatchingPin_NotAvailable_Throws()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");
            using TempCertFile pinFile = new(serverCert);

            Assert.Throws<AuthenticationException>(() =>
                SniCommon.ValidateSslServerCertificate(
                    connectionId: Guid.NewGuid(),
                    targetServerName: "server.contoso.com",
                    hostNameInCertificate: null,
                    serverCert: serverCert,
                    validationCertFileName: pinFile.Path,
                    policyErrors: SslPolicyErrors.RemoteCertificateNotAvailable));
        }

        /// <summary>
        /// Defense in depth: a null server certificate is normally reported through
        /// <see cref="SslPolicyErrors.RemoteCertificateNotAvailable"/>, but should the two ever
        /// disagree, the comparison must still fail with an <see cref="AuthenticationException"/>
        /// rather than a <see cref="NullReferenceException"/>.
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_Pin_NullServerCert_PolicyNone_Throws()
        {
            using X509Certificate2 pinCert = CreateSelfSignedCertificate("server.contoso.com");
            using TempCertFile pinFile = new(pinCert);

            Assert.Throws<AuthenticationException>(() =>
                SniCommon.ValidateSslServerCertificate(
                    connectionId: Guid.NewGuid(),
                    targetServerName: "server.contoso.com",
                    hostNameInCertificate: null,
                    serverCert: null,
                    validationCertFileName: pinFile.Path,
                    policyErrors: SslPolicyErrors.None));
        }

        /// <summary>
        /// When no ServerCertificate pin is supplied, the historical short-circuit is
        /// preserved: <see cref="SslPolicyErrors.None"/> means the platform already
        /// validated the certificate, so the helper returns true.  This guards against
        /// unintended breakage of the pin-less path.
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_NoPin_PolicyNone_ReturnsTrue()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");

            bool result = SniCommon.ValidateSslServerCertificate(
                connectionId: Guid.NewGuid(),
                targetServerName: "server.contoso.com",
                hostNameInCertificate: null,
                serverCert: serverCert,
                validationCertFileName: null,
                policyErrors: SslPolicyErrors.None);

            Assert.True(result);
        }

        /// <summary>
        /// When a ServerCertificate pin is supplied but the file cannot be loaded
        /// (missing file, corrupt bytes, wrong format, etc.), validation must fail
        /// closed rather than silently ignoring the option and falling back to host name
        /// validation.  The caller explicitly asked us to compare against a specific
        /// certificate, so we cannot accept the connection on any weaker basis.
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_UnreadablePinFile_PolicyNone_Throws()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");

            string missingPath = Path.Combine(
                Path.GetTempPath(),
                "SqlClient_MissingPin_" + Guid.NewGuid().ToString("N") + ".cer");

            Assert.False(File.Exists(missingPath));

            Assert.Throws<AuthenticationException>(() =>
                SniCommon.ValidateSslServerCertificate(
                    connectionId: Guid.NewGuid(),
                    targetServerName: "server.contoso.com",
                    hostNameInCertificate: null,
                    serverCert: serverCert,
                    validationCertFileName: missingPath,
                    policyErrors: SslPolicyErrors.None));
        }

        private static X509Certificate2 CreateSelfSignedCertificate(string subjectCommonName)
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new(
                $"CN={subjectCommonName}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            DateTimeOffset notAfter = notBefore.AddHours(1);
            return request.CreateSelfSigned(notBefore, notAfter);
        }

        /// <summary>
        /// Writes a certificate to a temporary file for use as a <c>ServerCertificate</c> pin and
        /// deletes it on disposal.
        /// </summary>
        private sealed class TempCertFile : IDisposable
        {
            public string Path { get; }

            public TempCertFile(X509Certificate2 cert)
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "SqlClient_Pin_" + Guid.NewGuid().ToString("N") + ".cer");
                File.WriteAllBytes(Path, cert.Export(X509ContentType.Cert));
            }

            public void Dispose()
            {
                try
                {
                    File.Delete(Path);
                }
                catch (IOException)
                {
                    // Best effort: a cleanup failure must not mask the assertion failure that
                    // caused the test to unwind through this Dispose.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}

#endif

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
    /// that when a caller supplies a <c>ServerCertificate</c> pin the driver enforces the
    /// pin as an *additive* check on top of normal TLS validation, and never accepts a
    /// server certificate on the basis of platform trust alone.
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

            string pinPath = WriteCertToTempFile(pinCert);
            try
            {
                Assert.Throws<AuthenticationException>(() =>
                    SniCommon.ValidateSslServerCertificate(
                        connectionId: Guid.NewGuid(),
                        targetServerName: "server.contoso.com",
                        hostNameInCertificate: null,
                        serverCert: serverCert,
                        validationCertFileName: pinPath,
                        policyErrors: SslPolicyErrors.None));
            }
            finally
            {
                File.Delete(pinPath);
            }
        }

        /// <summary>
        /// When a ServerCertificate pin is supplied and the presented server certificate
        /// matches the pin exactly, validation succeeds (assuming no policy errors).
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_MatchingPin_PolicyNone_ReturnsTrue()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");

            string pinPath = WriteCertToTempFile(serverCert);
            try
            {
                bool result = SniCommon.ValidateSslServerCertificate(
                    connectionId: Guid.NewGuid(),
                    targetServerName: "server.contoso.com",
                    hostNameInCertificate: null,
                    serverCert: serverCert,
                    validationCertFileName: pinPath,
                    policyErrors: SslPolicyErrors.None);

                Assert.True(result);
            }
            finally
            {
                File.Delete(pinPath);
            }
        }

        /// <summary>
        /// A matching ServerCertificate pin must not mask a platform policy error.
        /// This regression test guards the "additive" semantics: even when the pin bytes
        /// match exactly, if the platform reported <see cref="SslPolicyErrors.RemoteCertificateChainErrors"/>
        /// (or any other non-<c>None</c> flag), validation must still fail rather than
        /// short-circuit on the pin.
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_MatchingPin_ChainErrors_Throws()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");

            string pinPath = WriteCertToTempFile(serverCert);
            try
            {
                Assert.Throws<AuthenticationException>(() =>
                    SniCommon.ValidateSslServerCertificate(
                        connectionId: Guid.NewGuid(),
                        targetServerName: "server.contoso.com",
                        hostNameInCertificate: null,
                        serverCert: serverCert,
                        validationCertFileName: pinPath,
                        policyErrors: SslPolicyErrors.RemoteCertificateChainErrors));
            }
            finally
            {
                File.Delete(pinPath);
            }
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
        /// closed rather than silently falling back to platform trust.  The caller
        /// explicitly asked us to pin against a specific certificate, so we cannot
        /// accept the connection on any weaker basis.
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

        private static string WriteCertToTempFile(X509Certificate2 cert)
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "SqlClient_Pin_" + Guid.NewGuid().ToString("N") + ".cer");
            File.WriteAllBytes(path, cert.Export(X509ContentType.Cert));
            return path;
        }
    }
}

#endif

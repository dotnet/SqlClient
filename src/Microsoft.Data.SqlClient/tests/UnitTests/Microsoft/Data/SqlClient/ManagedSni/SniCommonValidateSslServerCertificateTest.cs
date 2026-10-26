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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManagedSni;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Verifies configured certificate names and pins through the shared validation helper,
    /// including certificate-name validation during a loopback TLS handshake.
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
        /// Without an explicit certificate name or pin, a successful platform validation
        /// continues to satisfy certificate validation.
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

        /// <summary>
        /// An explicit certificate name is checked regardless of the platform's name result.
        /// The supplied policy flags isolate the helper's hostname-selection behavior.
        /// </summary>
        [Theory]
        [InlineData("server.contoso.com", SslPolicyErrors.None, true)]
        [InlineData("SERVER.CONTOSO.COM", SslPolicyErrors.None, true)]
        [InlineData("other.contoso.com", SslPolicyErrors.None, false)]
        [InlineData("server.contoso.com", SslPolicyErrors.RemoteCertificateNameMismatch, true)]
        [InlineData("other.contoso.com", SslPolicyErrors.RemoteCertificateNameMismatch, false)]
        public void ValidateSslServerCertificate_ExplicitName_UsesConfiguredName(
            string hostNameInCertificate, SslPolicyErrors policyErrors, bool expectedSuccess)
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");

            bool Validate() => SniCommon.ValidateSslServerCertificate(
                Guid.NewGuid(), "server.contoso.com", hostNameInCertificate, serverCert,
                validationCertFileName: null, policyErrors: policyErrors);

            if (expectedSuccess)
            {
                Assert.True(Validate());
            }
            else
            {
                Assert.Throws<AuthenticationException>(() => Validate());
            }
        }

        /// <summary>
        /// Configured certificate names use the existing subject alternative name matching.
        /// </summary>
        [Theory]
        [InlineData("database.contoso.com", true)]
        [InlineData("other.contoso.com", false)]
        public void ValidateSslServerCertificate_ExplicitName_UsesSubjectAlternativeName(
            string hostNameInCertificate, bool expectedSuccess)
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(
                "server.contoso.com", "server.contoso.com", "database.contoso.com");

            bool Validate() => SniCommon.ValidateSslServerCertificate(
                Guid.NewGuid(), "server.contoso.com", hostNameInCertificate, serverCert,
                validationCertFileName: null, policyErrors: SslPolicyErrors.None);

            if (expectedSuccess)
            {
                Assert.True(Validate());
            }
            else
            {
                Assert.Throws<AuthenticationException>(() => Validate());
            }
        }

        /// <summary>
        /// A matching explicit name does not replace certificate-chain validation.
        /// </summary>
        [Theory]
        [InlineData(SslPolicyErrors.RemoteCertificateChainErrors)]
        [InlineData(SslPolicyErrors.RemoteCertificateChainErrors |
            SslPolicyErrors.RemoteCertificateNameMismatch)]
        public void ValidateSslServerCertificate_ExplicitName_ChainErrors_Throws(
            SslPolicyErrors policyErrors)
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");

            Assert.Throws<AuthenticationException>(() =>
                SniCommon.ValidateSslServerCertificate(
                    Guid.NewGuid(), "other.contoso.com", "server.contoso.com", serverCert,
                    validationCertFileName: null, policyErrors: policyErrors));
        }

        /// <summary>
        /// A missing server certificate fails normally when an explicit name is configured.
        /// </summary>
        [Theory]
        [InlineData(SslPolicyErrors.None)]
        [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable)]
        [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable |
            SslPolicyErrors.RemoteCertificateNameMismatch |
            SslPolicyErrors.RemoteCertificateChainErrors)]
        public void ValidateSslServerCertificate_ExplicitName_CertificateNotAvailable_Throws(
            SslPolicyErrors policyErrors)
        {
            Assert.Throws<AuthenticationException>(() =>
                SniCommon.ValidateSslServerCertificate(
                    Guid.NewGuid(), "server.contoso.com", "database.contoso.com",
                    serverCert: null, validationCertFileName: null,
                    policyErrors: policyErrors));
        }

        /// <summary>
        /// An exact certificate pin retains its existing precedence over hostname validation.
        /// </summary>
        [Theory]
        [InlineData(SslPolicyErrors.None)]
        [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch)]
        [InlineData(SslPolicyErrors.RemoteCertificateChainErrors |
            SslPolicyErrors.RemoteCertificateNameMismatch)]
        public void ValidateSslServerCertificate_ExplicitName_MatchingPin_ReturnsTrue(
            SslPolicyErrors policyErrors)
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");
            using TempCertFile pinFile = new(serverCert);

            Assert.True(SniCommon.ValidateSslServerCertificate(
                Guid.NewGuid(), "server.contoso.com", "database.contoso.com", serverCert,
                pinFile.Path, policyErrors));
        }

        /// <summary>
        /// Without an explicit name, platform name mismatches continue to use the target name.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ValidateSslServerCertificate_NoExplicitName_NameMismatch_Throws(
            string hostNameInCertificate)
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate("server.contoso.com");

            Assert.Throws<AuthenticationException>(() =>
                SniCommon.ValidateSslServerCertificate(
                    Guid.NewGuid(), "other.contoso.com", hostNameInCertificate, serverCert,
                    validationCertFileName: null,
                    policyErrors: SslPolicyErrors.RemoteCertificateNameMismatch));
        }

        /// <summary>
        /// Real TLS policy results and configured certificate names are evaluated together.
        /// Trust is scoped to this connection; no machine certificate stores are changed.
        /// </summary>
        [Theory]
        [InlineData("server.contoso.com", null, true, SslPolicyErrors.None)]
        [InlineData("server.contoso.com", "", true, SslPolicyErrors.None)]
        [InlineData("server.contoso.com", "server.contoso.com", true, SslPolicyErrors.None)]
        [InlineData("server.contoso.com", "other.contoso.com", false, SslPolicyErrors.None)]
        [InlineData("other.contoso.com", "other.contoso.com", true,
            SslPolicyErrors.RemoteCertificateNameMismatch)]
        [InlineData("other.contoso.com", "database.contoso.com", false,
            SslPolicyErrors.RemoteCertificateNameMismatch)]
        [InlineData("other.contoso.com", null, false,
            SslPolicyErrors.RemoteCertificateNameMismatch)]
        public async Task ValidateSslServerCertificate_TlsHandshake_UsesConfiguredName(
            string certificateName, string hostNameInCertificate, bool expectedSuccess,
            SslPolicyErrors expectedPolicyErrors)
        {
            using X509Certificate2 generatedCert = CreateSelfSignedCertificate(
                certificateName, certificateName);
            // Schannel needs an imported private key rather than the generated ephemeral key.
#if NET9_0_OR_GREATER
            using X509Certificate2 serverCert = X509CertificateLoader.LoadPkcs12(
                generatedCert.Export(X509ContentType.Pfx), password: null);
#else
            using X509Certificate2 serverCert = new(
                generatedCert.Export(X509ContentType.Pfx), (string)null);
#endif
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            using TcpClient client = new();
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, timeout.Token);
            using TcpClient server = await listener.AcceptTcpClientAsync(timeout.Token);
            using SslStream serverStream = new(server.GetStream());

            SslPolicyErrors? observedPolicyErrors = null;
            using SslStream clientStream = new(client.GetStream(), false,
                (_, certificate, _, policyErrors) =>
                {
                    observedPolicyErrors = policyErrors;
                    return SniCommon.ValidateSslServerCertificate(
                        Guid.NewGuid(), "server.contoso.com", hostNameInCertificate, certificate,
                        validationCertFileName: null, policyErrors: policyErrors);
                });
            X509ChainPolicy chainPolicy = new()
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                RevocationMode = X509RevocationMode.NoCheck
            };
            chainPolicy.CustomTrustStore.Add(serverCert);

            Task<Exception> serverResult = Record.ExceptionAsync(async () =>
            {
                try
                {
                    await serverStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = serverCert,
                        EnabledSslProtocols = SslProtocols.Tls12
                    }, timeout.Token);
                }
                catch
                {
                    server.Dispose();
                    throw;
                }
            });
            Exception clientException = await Record.ExceptionAsync(() =>
                clientStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = "server.contoso.com",
                    CertificateChainPolicy = chainPolicy,
                    EnabledSslProtocols = SslProtocols.Tls12
                }, timeout.Token));
            Exception serverException = await serverResult;

            Assert.True(observedPolicyErrors.HasValue,
                $"TLS validation was not reached. Client: {clientException}; " +
                $"server: {serverException}");
            Assert.Equal(expectedPolicyErrors, observedPolicyErrors);
            if (expectedSuccess)
            {
                Assert.Null(clientException);
                Assert.Null(serverException);
                Assert.True(clientStream.IsAuthenticated);
            }
            else
            {
                Assert.IsType<AuthenticationException>(clientException);
                Assert.True(serverException is null or AuthenticationException or IOException,
                    $"Unexpected server handshake result: {serverException}");
            }
        }

        /// <summary>
        /// Creates an in-memory certificate for name matching and connection-scoped TLS trust.
        /// </summary>
        /// <param name="subjectCommonName">The certificate subject's common name.</param>
        /// <param name="dnsNames">Optional DNS subject alternative names.</param>
        /// <returns>A disposable certificate with its private key.</returns>
        private static X509Certificate2 CreateSelfSignedCertificate(
            string subjectCommonName, params string[] dnsNames)
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new(
                $"CN={subjectCommonName}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Explicit server authentication usage is required by macOS TLS certificate policy.
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

            if (dnsNames.Length > 0)
            {
                SubjectAlternativeNameBuilder names = new();
                foreach (string name in dnsNames)
                {
                    names.AddDnsName(name);
                }
                request.CertificateExtensions.Add(names.Build());
            }

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

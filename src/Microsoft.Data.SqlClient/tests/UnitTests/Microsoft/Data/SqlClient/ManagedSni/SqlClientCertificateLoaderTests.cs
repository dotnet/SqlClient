// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.ManagedSni;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Tests loading client certificates and their private keys without persisting key material.
    /// </summary>
    public sealed class SqlClientCertificateLoaderTests : IDisposable
    {
        private const string Password = "<pwd>";
        private readonly string _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SqlClientCertificateLoaderTests-{Guid.NewGuid():N}");

        /// <summary>
        /// Removes certificate files created by each test.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
        }

        /// <summary>
        /// Verifies that an encrypted PFX is loaded with its private key.
        /// </summary>
        [Fact]
        public void Load_EncryptedPfx_ReturnsCertificateWithPrivateKey()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client.pfx");

            using X509Certificate2 source = CreateCertificate(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
            File.WriteAllBytes(certificatePath, source.Export(X509ContentType.Pkcs12, Password));

            using SqlClientCertificateContext loaded = SqlClientCertificateLoader.Load(certificatePath, null, Password);

            Assert.True(loaded.Certificate.HasPrivateKey);
            Assert.Equal(source.Thumbprint, loaded.Certificate.Thumbprint);
        }

        /// <summary>
        /// Verifies that a PEM certificate and encrypted PKCS#8 key are combined successfully.
        /// </summary>
        [Fact]
        public void Load_PemCertificateAndEncryptedKey_ReturnsCertificateWithPrivateKey()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client.pem");
            string keyPath = Path.Combine(_temporaryDirectory, "client.key");

            using RSA key = RSA.Create(2048);
            CertificateRequest request = new("CN=SqlClient Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 source = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddMinutes(5));
            File.WriteAllText(certificatePath, source.ExportCertificatePem());
            File.WriteAllText(
                keyPath,
                key.ExportEncryptedPkcs8PrivateKeyPem(
                    Password,
                    new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10_000)));

            using SqlClientCertificateContext loaded = SqlClientCertificateLoader.Load(certificatePath, keyPath, Password);

            Assert.True(loaded.Certificate.HasPrivateKey);
            Assert.Equal(source.Thumbprint, loaded.Certificate.Thumbprint);
        }

        /// <summary>
        /// Verifies that intermediate certificates in a PEM bundle are retained for the TLS client chain.
        /// </summary>
        [Fact]
        public void Load_PemCertificateChain_RetainsIntermediateCertificate()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client-chain.pem");
            string keyPath = Path.Combine(_temporaryDirectory, "client.key");

            CreateCertificateChain(
                out X509Certificate2 leafCertificate,
                out X509Certificate2 intermediateCertificate);
            using (leafCertificate)
            using (intermediateCertificate)
            using (RSA leafKey = leafCertificate.GetRSAPrivateKey()
                ?? throw new InvalidOperationException("The test leaf certificate must have a private key."))
            {
                File.WriteAllText(
                    certificatePath,
                    leafCertificate.ExportCertificatePem() +
                    Environment.NewLine +
                    intermediateCertificate.ExportCertificatePem());
                File.WriteAllText(keyPath, leafKey.ExportPkcs8PrivateKeyPem());

                using SqlClientCertificateContext loaded =
                    SqlClientCertificateLoader.Load(certificatePath, keyPath, keyPassword: null);

                Assert.Equal(leafCertificate.Thumbprint, loaded.Certificate.Thumbprint);
                Assert.Equal(1, loaded.AdditionalCertificateCount);
            }
        }

        /// <summary>
        /// Verifies that intermediate certificates in a PFX bundle are retained for the TLS client chain.
        /// </summary>
        [Fact]
        public void Load_PfxCertificateChain_RetainsIntermediateCertificate()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client-chain.pfx");

            CreateCertificateChain(
                out X509Certificate2 leafCertificate,
                out X509Certificate2 intermediateCertificate);
            using (leafCertificate)
            using (intermediateCertificate)
            {
                X509Certificate2Collection bundle = new();
                bundle.Add(leafCertificate);
                bundle.Add(intermediateCertificate);
                byte[] pkcs12 = bundle.Export(X509ContentType.Pkcs12, Password)
                    ?? throw new InvalidOperationException("The test PKCS#12 bundle could not be exported.");
                try
                {
                    File.WriteAllBytes(certificatePath, pkcs12);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(pkcs12);
                }

                using SqlClientCertificateContext loaded =
                    SqlClientCertificateLoader.Load(certificatePath, keyPath: null, keyPassword: Password);

                Assert.Equal(leafCertificate.Thumbprint, loaded.Certificate.Thumbprint);
                Assert.Equal(1, loaded.AdditionalCertificateCount);
            }
        }

        /// <summary>
        /// Verifies that a PFX certificate can use an ECDSA private key.
        /// </summary>
        [Fact]
        public void Load_EcdsaPfx_ReturnsCertificateWithPrivateKey()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client-ecdsa.pfx");

            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest request = new("CN=SqlClient ECDSA", key, HashAlgorithmName.SHA256);
            using X509Certificate2 source = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddMinutes(5));
            File.WriteAllBytes(certificatePath, source.Export(X509ContentType.Pkcs12, Password));

            using SqlClientCertificateContext loaded =
                SqlClientCertificateLoader.Load(certificatePath, keyPath: null, keyPassword: Password);
            using ECDsa loadedKey = loaded.Certificate.GetECDsaPrivateKey()
                ?? throw new InvalidOperationException("The loaded ECDSA certificate must have a private key.");

            Assert.NotNull(loadedKey);
            Assert.Equal(source.Thumbprint, loaded.Certificate.Thumbprint);
        }

        /// <summary>
        /// Verifies that detached ECDSA keys fail with the documented RSA-only error.
        /// </summary>
        /// <param name="useSec1">Whether to encode the key in SEC1 rather than PKCS#8 format.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Load_EcdsaPemAndKey_ThrowsUnsupportedAlgorithm(bool useSec1)
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client-ecdsa.pem");
            string keyPath = Path.Combine(_temporaryDirectory, "client-ecdsa.key");

            CreateCertificateChain(
                out X509Certificate2 unusedRsaLeafCertificate,
                out X509Certificate2 intermediateCertificate);
            using (unusedRsaLeafCertificate)
            using (intermediateCertificate)
            using (ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256))
            {
                CertificateRequest request = new("CN=SqlClient ECDSA", key, HashAlgorithmName.SHA256);
                using RSA issuerKey = intermediateCertificate.GetRSAPrivateKey()
                    ?? throw new InvalidOperationException("The intermediate certificate must have an RSA private key.");
                using X509Certificate2 publicCertificate = request.Create(
                    intermediateCertificate.SubjectName,
                    X509SignatureGenerator.CreateForRSA(issuerKey, RSASignaturePadding.Pkcs1),
                    DateTimeOffset.UtcNow.AddMinutes(-5),
                    DateTimeOffset.UtcNow.AddMinutes(5),
                    CreateSerialNumber());
                using X509Certificate2 source = publicCertificate.CopyWithPrivateKey(key);
                File.WriteAllText(
                    certificatePath,
                    source.ExportCertificatePem() +
                    Environment.NewLine +
                    intermediateCertificate.ExportCertificatePem());
                File.WriteAllText(
                    keyPath,
                    useSec1 ? key.ExportECPrivateKeyPem() : key.ExportPkcs8PrivateKeyPem());

                AuthenticationException exception = Assert.Throws<AuthenticationException>(
                    () => SqlClientCertificateLoader.Load(certificatePath, keyPath, keyPassword: null));
                Assert.Contains("RSA", exception.Message, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Verifies that an incorrect private-key password is reported as an authentication failure.
        /// </summary>
        [Fact]
        public void Load_IncorrectPassword_ThrowsAuthenticationException()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client.pfx");

            using X509Certificate2 source = CreateCertificate(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
            File.WriteAllBytes(certificatePath, source.Export(X509ContentType.Pkcs12, Password));

            Assert.Throws<AuthenticationException>(
                () => SqlClientCertificateLoader.Load(certificatePath, null, "incorrect"));
        }

        /// <summary>
        /// Verifies that an unreadable private-key path is reported as an authentication failure.
        /// </summary>
        [Fact]
        public void Load_MissingPrivateKey_ThrowsAuthenticationException()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client.pem");
            string missingKeyPath = Path.Combine(_temporaryDirectory, "missing.key");

            using X509Certificate2 source = CreateCertificate(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddMinutes(5));
            File.WriteAllText(certificatePath, source.ExportCertificatePem());

            Assert.Throws<AuthenticationException>(
                () => SqlClientCertificateLoader.Load(certificatePath, missingKeyPath, keyPassword: null));
        }

        /// <summary>
        /// Verifies that an unsupported PEM key label is normalized to a certificate-load failure.
        /// </summary>
        [Fact]
        public void Load_UnsupportedPemKey_ThrowsAuthenticationException()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client.pem");
            string keyPath = Path.Combine(_temporaryDirectory, "client.key");

            using X509Certificate2 source = CreateCertificate(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddMinutes(5));
            File.WriteAllText(certificatePath, source.ExportCertificatePem());
            File.WriteAllText(
                keyPath,
                "-----BEGIN DSA PRIVATE KEY-----\nAA==\n-----END DSA PRIVATE KEY-----");

            AuthenticationException exception = Assert.Throws<AuthenticationException>(
                () => SqlClientCertificateLoader.Load(certificatePath, keyPath, keyPassword: null));
            Assert.Contains(
                "could not be loaded",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that an expired certificate is rejected before the TLS handshake.
        /// </summary>
        [Fact]
        public void Load_ExpiredCertificate_ThrowsAuthenticationException()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client.pfx");

            using X509Certificate2 source = CreateCertificate(
                DateTimeOffset.UtcNow.AddMinutes(-10),
                DateTimeOffset.UtcNow.AddMinutes(-5));
            File.WriteAllBytes(certificatePath, source.Export(X509ContentType.Pkcs12, Password));

            Assert.Throws<AuthenticationException>(
                () => SqlClientCertificateLoader.Load(certificatePath, null, Password));
        }

        /// <summary>
        /// Verifies that the container format is detected from the file contents rather than the
        /// file extension, so a PKCS#12 bundle loads regardless of how it is named.
        /// </summary>
        /// <param name="fileName">The certificate file name under test.</param>
        [Theory]
        [InlineData("client.pem")]
        [InlineData("client.cer")]
        [InlineData("client")]
        public void Load_Pkcs12WithNonPkcs12Extension_ReturnsCertificateWithPrivateKey(string fileName)
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, fileName);

            using X509Certificate2 source = CreateCertificate(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddMinutes(5));
            File.WriteAllBytes(certificatePath, source.Export(X509ContentType.Pkcs12, Password));

            using SqlClientCertificateContext loaded =
                SqlClientCertificateLoader.Load(certificatePath, null, Password);

            Assert.True(loaded.Certificate.HasPrivateKey);
            Assert.Equal(source.Thumbprint, loaded.Certificate.Thumbprint);
        }

        /// <summary>
        /// Verifies that a PEM certificate named with a PKCS#12 extension is still combined with a
        /// detached private key rather than being rejected on the basis of its name.
        /// </summary>
        [Fact]
        public void Load_PemWithPkcs12Extension_ReturnsCertificateWithPrivateKey()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client.pfx");
            string keyPath = Path.Combine(_temporaryDirectory, "client.key");

            using RSA key = RSA.Create(2048);
            CertificateRequest request = new(
                "CN=SqlClient Test",
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            using X509Certificate2 source = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddMinutes(5));
            File.WriteAllText(certificatePath, source.ExportCertificatePem());
            File.WriteAllText(keyPath, key.ExportPkcs8PrivateKeyPem());

            using SqlClientCertificateContext loaded =
                SqlClientCertificateLoader.Load(certificatePath, keyPath, keyPassword: null);

            Assert.True(loaded.Certificate.HasPrivateKey);
            Assert.Equal(source.Thumbprint, loaded.Certificate.Thumbprint);
        }

        /// <summary>
        /// Verifies that the ODBC driver's <c>file:</c> path syntax is rejected with a specific error.
        /// </summary>
        [Fact]
        public void Load_OdbcStylePath_ThrowsAuthenticationException()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client.pfx");

            using X509Certificate2 source = CreateCertificate(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddMinutes(5));
            File.WriteAllBytes(certificatePath, source.Export(X509ContentType.Pkcs12, Password));

            AuthenticationException exception = Assert.Throws<AuthenticationException>(
                () => SqlClientCertificateLoader.Load($"file:{certificatePath}", null, Password));
            Assert.Contains("file:", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that traditional (PKCS#1) encrypted private keys report an actionable error
        /// rather than a generic load failure.
        /// </summary>
        [Fact]
        public void Load_EncryptedPkcs1Key_ThrowsAuthenticationException()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "client.pem");
            string keyPath = Path.Combine(_temporaryDirectory, "client.key");

            using X509Certificate2 source = CreateCertificate(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddMinutes(5));
            File.WriteAllText(certificatePath, source.ExportCertificatePem());
            File.WriteAllText(
                keyPath,
                "-----BEGIN RSA PRIVATE KEY-----\n" +
                "Proc-Type: 4,ENCRYPTED\n" +
                "DEK-Info: AES-256-CBC,0123456789ABCDEF0123456789ABCDEF\n" +
                "\n" +
                "AA==\n" +
                "-----END RSA PRIVATE KEY-----");

            AuthenticationException exception = Assert.Throws<AuthenticationException>(
                () => SqlClientCertificateLoader.Load(certificatePath, keyPath, Password));
            Assert.Contains("PKCS#8", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that the end-entity certificate is selected from a PKCS#12 bundle even when an
        /// issuer certificate in the same bundle also carries a private key.
        /// </summary>
        [Fact]
        public void Load_Pkcs12WithMultiplePrivateKeys_SelectsLeafCertificate()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            string certificatePath = Path.Combine(_temporaryDirectory, "chain.pfx");

            CreateCertificateChain(
                out X509Certificate2 leafCertificate,
                out X509Certificate2 intermediateCertificate);
            using (leafCertificate)
            using (intermediateCertificate)
            {
                // Order the intermediate first so that a positional heuristic would select the wrong
                // certificate. Both entries carry private keys.
                X509Certificate2Collection bundle = new()
                {
                    intermediateCertificate,
                    leafCertificate
                };
                byte[] bundleBytes = bundle.Export(X509ContentType.Pkcs12, Password)
                    ?? throw new InvalidOperationException("The PKCS#12 bundle could not be exported.");
                File.WriteAllBytes(certificatePath, bundleBytes);

                using SqlClientCertificateContext loaded =
                    SqlClientCertificateLoader.Load(certificatePath, null, Password);

                Assert.Equal(leafCertificate.Thumbprint, loaded.Certificate.Thumbprint);
                Assert.True(loaded.Certificate.HasPrivateKey);
            }
        }

        /// <summary>
        /// Creates a self-signed certificate with the requested validity period.
        /// </summary>
        /// <param name="notBefore">The beginning of the validity period.</param>
        /// <param name="notAfter">The end of the validity period.</param>
        /// <returns>A certificate containing its private key.</returns>
        private static X509Certificate2 CreateCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
        {
            using RSA key = RSA.Create(2048);
            CertificateRequest request = new("CN=SqlClient Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return request.CreateSelfSigned(notBefore, notAfter);
        }

        /// <summary>
        /// Creates a certificate request for a certificate authority or TLS client leaf.
        /// </summary>
        /// <param name="subjectName">The request subject.</param>
        /// <param name="key">The request's private key.</param>
        /// <param name="isCertificateAuthority">Whether the request represents a certificate authority.</param>
        /// <returns>The configured certificate request.</returns>
        private static CertificateRequest CreateCertificateRequest(
            string subjectName,
            RSA key,
            bool isCertificateAuthority)
        {
            CertificateRequest request = new(
                subjectName,
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    certificateAuthority: isCertificateAuthority,
                    hasPathLengthConstraint: false,
                    pathLengthConstraint: 0,
                    critical: true));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    isCertificateAuthority
                        ? X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign
                        : X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));
            if (!isCertificateAuthority)
            {
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection { new("1.3.6.1.5.5.7.3.2") },
                        critical: true));
            }

            return request;
        }

        /// <summary>
        /// Creates a client leaf certificate and its issuing intermediate certificate.
        /// </summary>
        /// <param name="leafCertificate">Receives the client certificate with its private key.</param>
        /// <param name="intermediateCertificate">Receives the issuing intermediate with its private key.</param>
        private static void CreateCertificateChain(
            out X509Certificate2 leafCertificate,
            out X509Certificate2 intermediateCertificate)
        {
            DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            DateTimeOffset notAfter = DateTimeOffset.UtcNow.AddMinutes(5);
            using RSA rootKey = RSA.Create(2048);
            CertificateRequest rootRequest = CreateCertificateRequest(
                "CN=SqlClient Root",
                rootKey,
                isCertificateAuthority: true);
            using X509Certificate2 rootCertificate =
                rootRequest.CreateSelfSigned(notBefore, notAfter);

            using RSA intermediateKey = RSA.Create(2048);
            CertificateRequest intermediateRequest = CreateCertificateRequest(
                "CN=SqlClient Intermediate",
                intermediateKey,
                isCertificateAuthority: true);
            using X509Certificate2 intermediatePublicCertificate = intermediateRequest.Create(
                rootCertificate,
                notBefore,
                notAfter,
                CreateSerialNumber());
            intermediateCertificate =
                intermediatePublicCertificate.CopyWithPrivateKey(intermediateKey);

            using RSA leafKey = RSA.Create(2048);
            CertificateRequest leafRequest = CreateCertificateRequest(
                "CN=SqlClient Leaf",
                leafKey,
                isCertificateAuthority: false);
            using X509Certificate2 leafPublicCertificate = leafRequest.Create(
                intermediateCertificate,
                notBefore,
                notAfter,
                CreateSerialNumber());
            leafCertificate = leafPublicCertificate.CopyWithPrivateKey(leafKey);
        }

        /// <summary>
        /// Creates a positive random serial number for a test certificate.
        /// </summary>
        /// <returns>A random sixteen-byte serial number.</returns>
        private static byte[] CreateSerialNumber()
        {
            byte[] serialNumber = RandomNumberGenerator.GetBytes(16);
            serialNumber[0] &= 0x7F;
            return serialNumber;
        }
    }
}

#endif

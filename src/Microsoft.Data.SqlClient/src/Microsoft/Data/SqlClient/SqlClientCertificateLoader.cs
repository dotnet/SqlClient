// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.Common;
#if NET
using System.Net.Security;
using System.Text;
#endif

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Owns a client certificate and its issuer chain.
    /// </summary>
    /// <remarks>
    /// On .NET this also builds the <c>SslStreamCertificateContext</c> that managed SNI hands to
    /// <c>SslStream</c>, which presents the issuer chain during the handshake. Native SNI instead
    /// consumes the <c>PCCERT_CONTEXT</c> exposed by <see cref="Certificate" />, and
    /// <c>SNIAuthProviderInfo</c> has no field for the chain, so only the end-entity certificate is
    /// presented on that path.
    /// </remarks>
    internal sealed class SqlClientCertificateContext : IDisposable
    {
        private readonly X509Certificate2Collection _additionalCertificates;
        private bool _disposed;

        internal SqlClientCertificateContext(
            X509Certificate2 certificate,
            X509Certificate2Collection additionalCertificates)
        {
            Certificate = certificate;
            _additionalCertificates = additionalCertificates;
#if NET
            SslContext = SslStreamCertificateContext.Create(
                certificate,
                additionalCertificates,
                offline: true);
#endif
        }

        internal X509Certificate2 Certificate { get; }

#if NET
        internal SslStreamCertificateContext SslContext { get; }
#endif

        internal int AdditionalCertificateCount => _additionalCertificates?.Count ?? 0;

        /// <summary>
        /// Disposes the certificate and its issuer chain.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Certificate.Dispose();
            DisposeCertificates(_additionalCertificates);
        }

        /// <summary>
        /// Disposes every certificate in the collection.
        /// </summary>
        /// <param name="certificates">The certificates to dispose, or <see langword="null" />.</param>
        private static void DisposeCertificates(X509Certificate2Collection certificates)
        {
            if (certificates == null)
            {
                return;
            }

            foreach (X509Certificate2 certificate in certificates)
            {
                certificate.Dispose();
            }
        }
    }

    /// <summary>
    /// Loads the client certificate and private key configured for certificate authentication.
    /// </summary>
    internal static class SqlClientCertificateLoader
    {
        private static X509KeyStorageFlags PrivateKeyStorageFlags =>
#if NET
            OsConstants.IsWindows
                ? X509KeyStorageFlags.UserKeySet
                : X509KeyStorageFlags.EphemeralKeySet;
#else
            // .NET Framework runs on Windows only, and Schannel cannot use ephemeral keys.
            X509KeyStorageFlags.UserKeySet;
#endif

        /// <summary>
        /// Loads the configured client certificate and private key.
        /// </summary>
        /// <param name="certificatePath">The certificate file path.</param>
        /// <param name="keyPath">The detached private key file path, or <see langword="null" /> when the certificate carries its own key.</param>
        /// <param name="keyPassword">The certificate or private key password, or <see langword="null" /> when none is configured.</param>
        /// <returns>The loaded certificate context, or <see langword="null" /> when no certificate is configured.</returns>
        /// <exception cref="AuthenticationException">The certificate or private key could not be loaded, or is not currently valid.</exception>
        internal static SqlClientCertificateContext Load(string certificatePath, string keyPath, string keyPassword)
        {
            if (string.IsNullOrEmpty(certificatePath))
            {
                return null;
            }

            ThrowIfOdbcStylePath(certificatePath);
            ThrowIfOdbcStylePath(keyPath);

            X509Certificate2 certificate = null;
            X509Certificate2Collection additionalCertificates = null;
            try
            {
                certificate = string.IsNullOrEmpty(keyPath)
                    ? LoadPkcs12(
                        certificatePath,
                        keyPassword,
                        out additionalCertificates)
                    : LoadWithPrivateKey(
                        certificatePath,
                        keyPath,
                        keyPassword,
                        out additionalCertificates);
                if (!certificate.HasPrivateKey)
                {
                    throw ADP.SSLCertificateAuthenticationException(StringsHelper.GetString(Strings.SQL_ClientCertificateMissingPrivateKey));
                }

                DateTime now = DateTime.Now;
                if (now < certificate.NotBefore || now > certificate.NotAfter)
                {
                    throw ADP.SSLCertificateAuthenticationException(StringsHelper.GetString(Strings.SQL_ClientCertificateNotValid));
                }

                SqlClientCertificateContext context = new(certificate, additionalCertificates);
                certificate = null;
                additionalCertificates = null;
                return context;
            }
            catch (Exception e) when (
                e is CryptographicException or
                IOException or
                UnauthorizedAccessException or
                ArgumentException)
            {
                throw ADP.SSLCertificateAuthenticationException(StringsHelper.GetString(Strings.SQL_ClientCertificateLoadFailed), e);
            }
            finally
            {
                certificate?.Dispose();
                DisposeCertificates(additionalCertificates);
            }
        }

        /// <summary>
        /// Loads a PKCS#12 bundle that carries its own private key.
        /// </summary>
        /// <param name="certificatePath">The bundle file path.</param>
        /// <param name="password">The bundle password, or <see langword="null" /> when none is configured.</param>
        /// <param name="additionalCertificates">Receives the issuer certificates found in the bundle.</param>
        /// <returns>The end-entity certificate with its private key.</returns>
        private static X509Certificate2 LoadPkcs12(
            string certificatePath,
            string password,
            out X509Certificate2Collection additionalCertificates)
        {
            string effectivePassword = string.IsNullOrEmpty(password) ? null : password;
            X509KeyStorageFlags collectionStorageFlags = PrivateKeyStorageFlags;
            if (OsConstants.IsWindows)
            {
                collectionStorageFlags |= X509KeyStorageFlags.Exportable;
            }

            X509Certificate2Collection certificates;
#if NET9_0_OR_GREATER
            certificates = X509CertificateLoader.LoadPkcs12CollectionFromFile(
                certificatePath,
                effectivePassword,
                collectionStorageFlags);
#else
            certificates = new X509Certificate2Collection();
#pragma warning disable SYSLIB0057
            certificates.Import(
                certificatePath,
                effectivePassword,
                collectionStorageFlags);
#pragma warning restore SYSLIB0057
#endif

            additionalCertificates = null;
            try
            {
                int leafCertificateIndex = FindLeafCertificateIndex(certificates);
                if (leafCertificateIndex < 0)
                {
                    throw new CryptographicException(StringsHelper.GetString(Strings.SQL_ClientCertificateMissingPrivateKey));
                }

                additionalCertificates = new X509Certificate2Collection();
                for (int index = 0; index < certificates.Count; index++)
                {
                    if (index != leafCertificateIndex)
                    {
                        additionalCertificates.Add(certificates[index]);
                    }
                }

                return MaterializeForTls(certificates[leafCertificateIndex]);
            }
            catch
            {
                DisposeCertificates(certificates);
                additionalCertificates = null;
                throw;
            }
        }

#if NET
        /// <summary>
        /// Combines a certificate with a detached private key file.
        /// </summary>
        /// <param name="certificatePath">The certificate file path.</param>
        /// <param name="keyPath">The private key file path.</param>
        /// <param name="password">The private key password, or <see langword="null" /> when the key is not encrypted.</param>
        /// <param name="additionalCertificates">Receives the issuer certificates supplied alongside the leaf.</param>
        /// <returns>The end-entity certificate with its private key.</returns>
        private static X509Certificate2 LoadWithPrivateKey(
            string certificatePath,
            string keyPath,
            string password,
            out X509Certificate2Collection additionalCertificates)
        {
            X509Certificate2Collection publicCertificates = LoadPublicCertificates(certificatePath);
            additionalCertificates = null;
            using RSA privateKey = RSA.Create();
            byte[] keyBytes = null;

            try
            {
                keyBytes = File.ReadAllBytes(keyPath);
                try
                {
                    ImportRsaPrivateKey(privateKey, keyBytes, password);
                }
                catch (Exception e) when (
                    (e is CryptographicException || e is ArgumentException) &&
                    CanImportEcdsaPrivateKey(keyBytes, password))
                {
                    throw ADP.SSLCertificateAuthenticationException(
                        StringsHelper.GetString(Strings.SQL_ClientCertificateUnsupportedKeyAlgorithm));
                }

                X509Certificate2 combinedCertificate = null;
                int matchingCertificateIndex = -1;
                for (int index = 0; index < publicCertificates.Count; index++)
                {
                    try
                    {
                        combinedCertificate = publicCertificates[index].CopyWithPrivateKey(privateKey);
                        matchingCertificateIndex = index;
                        break;
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (CryptographicException)
                    {
                    }
                }

                if (combinedCertificate == null)
                {
                    throw new CryptographicException(StringsHelper.GetString(Strings.SQL_ClientCertificateLoadFailed));
                }

                combinedCertificate = MaterializeForTls(combinedCertificate);
                additionalCertificates = new X509Certificate2Collection();
                for (int index = 0; index < publicCertificates.Count; index++)
                {
                    if (index != matchingCertificateIndex)
                    {
                        additionalCertificates.Add(publicCertificates[index]);
                    }
                }

                publicCertificates[matchingCertificateIndex].Dispose();
                return combinedCertificate;
            }
            catch
            {
                DisposeCertificates(publicCertificates);
                throw;
            }
            finally
            {
                if (keyBytes != null)
                {
                    CryptographicOperations.ZeroMemory(keyBytes);
                }
            }
        }

#else
        /// <summary>
        /// Rejects a detached private key on .NET Framework.
        /// </summary>
        /// <remarks>
        /// Importing a PEM or DER private key needs <c>RSA.ImportFromPem</c> and
        /// <c>RSA.ImportPkcs8PrivateKey</c>, which .NET Framework does not provide. A PKCS#12
        /// certificate, which carries its own key, is supported on every target.
        /// </remarks>
        /// <param name="certificatePath">The certificate file path.</param>
        /// <param name="keyPath">The private key file path.</param>
        /// <param name="password">The private key password.</param>
        /// <param name="additionalCertificates">Always <see langword="null" />.</param>
        /// <returns>This method always throws.</returns>
        private static X509Certificate2 LoadWithPrivateKey(
            string certificatePath,
            string keyPath,
            string password,
            out X509Certificate2Collection additionalCertificates)
        {
            additionalCertificates = null;
            throw ADP.SSLCertificateAuthenticationException(
                StringsHelper.GetString(Strings.SQL_ClientKeyRequiresNetCore));
        }
#endif

        /// <summary>
        /// Returns a certificate whose private key Schannel can use for a TLS handshake.
        /// </summary>
        /// <param name="certificate">The loaded certificate. It is disposed and replaced on Windows.</param>
        /// <returns>The certificate to present during the handshake.</returns>
        private static X509Certificate2 MaterializeForTls(X509Certificate2 certificate)
        {
            if (!OsConstants.IsWindows)
            {
                return certificate;
            }

            // Schannel cannot acquire credentials from an ephemeral private key. Importing
            // without PersistKeySet creates a temporary user key container that is deleted
            // when the certificate is disposed.
            using (certificate)
            {
                byte[] pkcs12 = certificate.Export(X509ContentType.Pkcs12);
                try
                {
#if NET9_0_OR_GREATER
                    return X509CertificateLoader.LoadPkcs12(
                        pkcs12,
                        password: null,
                        X509KeyStorageFlags.UserKeySet);
#else
#pragma warning disable SYSLIB0057
                    return new X509Certificate2(
                        pkcs12,
                        (string)null,
                        X509KeyStorageFlags.UserKeySet);
#pragma warning restore SYSLIB0057
#endif
                }
                finally
                {
#if NET
                    CryptographicOperations.ZeroMemory(pkcs12);
#else
                    // CryptographicOperations is unavailable on .NET Framework.
                    Array.Clear(pkcs12, 0, pkcs12.Length);
#endif
                }
            }
        }

#if NET
        /// <summary>
        /// Reads the public certificates from a PEM or DER certificate file.
        /// </summary>
        /// <param name="certificatePath">The certificate file path.</param>
        /// <returns>The certificates found in the file, leaf first for PEM bundles.</returns>
        private static X509Certificate2Collection LoadPublicCertificates(string certificatePath)
        {
            byte[] certificateBytes = File.ReadAllBytes(certificatePath);
            try
            {
                if (IsPem(certificateBytes))
                {
                    char[] certificateCharacters = Encoding.ASCII.GetChars(certificateBytes);
                    try
                    {
                        X509Certificate2Collection pemCertificates = new();
                        pemCertificates.ImportFromPem(certificateCharacters);
                        if (pemCertificates.Count == 0)
                        {
                            throw new CryptographicException(StringsHelper.GetString(Strings.SQL_ClientCertificateLoadFailed));
                        }

                        return pemCertificates;
                    }
                    finally
                    {
                        Array.Clear(certificateCharacters, 0, certificateCharacters.Length);
                    }
                }

                // A PKCS#12 bundle already carries its private key. Combining it with a detached key
                // is a misconfiguration, and the certificate loader below would otherwise report it
                // as an unspecified certificate-load failure.
                if (X509Certificate2.GetCertContentType(certificateBytes) == X509ContentType.Pkcs12)
                {
                    throw ADP.SSLCertificateAuthenticationException(
                        StringsHelper.GetString(Strings.SQL_ClientKeyWithPkcs12Certificate));
                }

#if NET9_0_OR_GREATER
                X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(certificateBytes);
#else
#pragma warning disable SYSLIB0057
                X509Certificate2 certificate = new(certificateBytes);
#pragma warning restore SYSLIB0057
#endif
                return new X509Certificate2Collection(certificate);
            }
            finally
            {
                Array.Clear(certificateBytes, 0, certificateBytes.Length);
            }
        }

        /// <summary>
        /// Imports a DER-encoded RSA private key.
        /// </summary>
        /// <param name="privateKey">The key to populate.</param>
        /// <param name="keyBytes">The DER-encoded key material.</param>
        /// <param name="password">The key password, or <see langword="null" /> when the key is not encrypted.</param>
        private static void ImportDerPrivateKey(RSA privateKey, byte[] keyBytes, string password)
        {
            int bytesRead;
            if (!string.IsNullOrEmpty(password))
            {
                privateKey.ImportEncryptedPkcs8PrivateKey(password, keyBytes, out bytesRead);
            }
            else
            {
                try
                {
                    privateKey.ImportPkcs8PrivateKey(keyBytes, out bytesRead);
                }
                catch (CryptographicException)
                {
                    privateKey.ImportRSAPrivateKey(keyBytes, out bytesRead);
                }
            }

            if (bytesRead != keyBytes.Length)
            {
                throw new CryptographicException(StringsHelper.GetString(Strings.SQL_ClientCertificateLoadFailed));
            }
        }

        /// <summary>
        /// Imports an RSA private key in PEM or DER form.
        /// </summary>
        /// <param name="privateKey">The key to populate.</param>
        /// <param name="keyBytes">The encoded key material.</param>
        /// <param name="password">The key password, or <see langword="null" /> when the key is not encrypted.</param>
        private static void ImportRsaPrivateKey(RSA privateKey, byte[] keyBytes, string password)
        {
            if (!IsPem(keyBytes))
            {
                ImportDerPrivateKey(privateKey, keyBytes, password);
                return;
            }

            // Traditional (PKCS#1) PEM encryption is identified by Proc-Type/DEK-Info headers and is
            // not supported by RSA.ImportFromEncryptedPem, which reads PKCS#8 EncryptedPrivateKeyInfo.
            if (keyBytes.AsSpan().IndexOf("Proc-Type:"u8) >= 0)
            {
                throw ADP.SSLCertificateAuthenticationException(
                    StringsHelper.GetString(Strings.SQL_ClientCertificateEncryptedPkcs1NotSupported));
            }

            char[] keyCharacters = Encoding.ASCII.GetChars(keyBytes);
            try
            {
                if (string.IsNullOrEmpty(password))
                {
                    privateKey.ImportFromPem(keyCharacters);
                }
                else
                {
                    privateKey.ImportFromEncryptedPem(keyCharacters, password);
                }
            }
            finally
            {
                Array.Clear(keyCharacters, 0, keyCharacters.Length);
            }
        }

        /// <summary>
        /// Reports whether the key material is an ECDSA private key, so an RSA import failure can be
        /// reported as an unsupported key algorithm rather than a malformed key.
        /// </summary>
        /// <param name="keyBytes">The encoded key material.</param>
        /// <param name="password">The key password, or <see langword="null" /> when the key is not encrypted.</param>
        /// <returns><see langword="true" /> when the material parses as an ECDSA private key.</returns>
        private static bool CanImportEcdsaPrivateKey(byte[] keyBytes, string password)
        {
            if (keyBytes.AsSpan().IndexOf("-----BEGIN EC PRIVATE KEY-----"u8) >= 0)
            {
                return true;
            }

            try
            {
                using ECDsa privateKey = ECDsa.Create();
                if (IsPem(keyBytes))
                {
                    char[] keyCharacters = Encoding.ASCII.GetChars(keyBytes);
                    try
                    {
                        if (string.IsNullOrEmpty(password))
                        {
                            privateKey.ImportFromPem(keyCharacters);
                        }
                        else
                        {
                            privateKey.ImportFromEncryptedPem(keyCharacters, password);
                        }
                    }
                    finally
                    {
                        Array.Clear(keyCharacters, 0, keyCharacters.Length);
                    }

                    return true;
                }

                int bytesRead;
                if (!string.IsNullOrEmpty(password))
                {
                    privateKey.ImportEncryptedPkcs8PrivateKey(password, keyBytes, out bytesRead);
                }
                else
                {
                    try
                    {
                        privateKey.ImportPkcs8PrivateKey(keyBytes, out bytesRead);
                    }
                    catch (CryptographicException)
                    {
                        privateKey.ImportECPrivateKey(keyBytes, out bytesRead);
                    }
                }

                return bytesRead == keyBytes.Length;
            }
            catch (Exception e) when (e is CryptographicException or ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Reports whether the bytes contain a PEM armour header.
        /// </summary>
        /// <param name="bytes">The file contents to inspect.</param>
        /// <returns><see langword="true" /> when the contents are PEM encoded.</returns>
        private static bool IsPem(byte[] bytes)
        {
            ReadOnlySpan<byte> marker = "-----BEGIN"u8;
            return bytes.AsSpan().IndexOf(marker) >= 0;
        }
#endif

        /// <summary>
        /// Rejects the ODBC driver's <c>file:</c> path syntax, which this driver does not accept.
        /// </summary>
        /// <param name="path">The configured certificate or key path.</param>
        private static void ThrowIfOdbcStylePath(string path)
        {
            if (!string.IsNullOrEmpty(path) &&
                path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                throw ADP.SSLCertificateAuthenticationException(
                    StringsHelper.GetString(Strings.SQL_ClientCertificateOdbcPathSyntax));
            }
        }

        /// <summary>
        /// Selects the end-entity certificate from a PKCS#12 bundle.
        /// </summary>
        /// <remarks>
        /// A bundle can contain issuer certificates that also carry private keys, and collection
        /// ordering is provider-specific. The end-entity certificate is therefore identified by
        /// content rather than by position.
        /// </remarks>
        /// <param name="certificates">The certificates loaded from the bundle.</param>
        /// <returns>The index of the end-entity certificate, or -1 when no private key is present.</returns>
        private static int FindLeafCertificateIndex(X509Certificate2Collection certificates)
        {
            int fallbackIndex = -1;
            for (int index = 0; index < certificates.Count; index++)
            {
                X509Certificate2 candidate = certificates[index];
                if (!candidate.HasPrivateKey)
                {
                    continue;
                }

                if (fallbackIndex < 0)
                {
                    fallbackIndex = index;
                }

                if (IsCertificateAuthority(candidate) || IssuesAnotherCertificate(certificates, index))
                {
                    continue;
                }

                return index;
            }

            return fallbackIndex;
        }

        /// <summary>
        /// Reports whether the certificate is marked as a certificate authority.
        /// </summary>
        /// <param name="certificate">The certificate to inspect.</param>
        /// <returns><see langword="true" /> when basic constraints mark the certificate as a CA.</returns>
        private static bool IsCertificateAuthority(X509Certificate2 certificate)
        {
            foreach (X509Extension extension in certificate.Extensions)
            {
                if (extension is X509BasicConstraintsExtension basicConstraints)
                {
                    return basicConstraints.CertificateAuthority;
                }
            }

            return false;
        }

        /// <summary>
        /// Reports whether the certificate issued another certificate in the same bundle.
        /// </summary>
        /// <param name="certificates">The certificates loaded from the bundle.</param>
        /// <param name="candidateIndex">The index of the certificate to test.</param>
        /// <returns><see langword="true" /> when the certificate is an issuer within the bundle.</returns>
        private static bool IssuesAnotherCertificate(X509Certificate2Collection certificates, int candidateIndex)
        {
            byte[] subject = certificates[candidateIndex].SubjectName.RawData;
            for (int index = 0; index < certificates.Count; index++)
            {
                if (index == candidateIndex)
                {
                    continue;
                }

                if (subject.AsSpan().SequenceEqual(certificates[index].IssuerName.RawData))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Disposes every certificate in the collection.
        /// </summary>
        /// <param name="certificates">The certificates to dispose, or <see langword="null" />.</param>
        private static void DisposeCertificates(X509Certificate2Collection certificates)
        {
            if (certificates == null)
            {
                return;
            }

            foreach (X509Certificate2 certificate in certificates)
            {
                certificate.Dispose();
            }
        }
    }
}

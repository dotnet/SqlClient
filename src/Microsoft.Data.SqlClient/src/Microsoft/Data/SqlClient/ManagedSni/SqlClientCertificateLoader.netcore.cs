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
using System.Text;

namespace Microsoft.Data.SqlClient.ManagedSni
{
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
            SslContext = SslStreamCertificateContext.Create(
                certificate,
                additionalCertificates,
                offline: true);
        }

        internal X509Certificate2 Certificate { get; }

        internal SslStreamCertificateContext SslContext { get; }

        internal int AdditionalCertificateCount => _additionalCertificates?.Count ?? 0;

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

    internal static class SqlClientCertificateLoader
    {
        private static X509KeyStorageFlags PrivateKeyStorageFlags =>
            OsConstants.IsWindows
                ? X509KeyStorageFlags.UserKeySet
                : X509KeyStorageFlags.EphemeralKeySet;

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
                    throw new AuthenticationException(StringsHelper.GetString(Strings.SQL_ClientCertificateMissingPrivateKey));
                }

                DateTime now = DateTime.Now;
                if (now < certificate.NotBefore || now > certificate.NotAfter)
                {
                    throw new AuthenticationException(StringsHelper.GetString(Strings.SQL_ClientCertificateNotValid));
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
                throw new AuthenticationException(StringsHelper.GetString(Strings.SQL_ClientCertificateLoadFailed), e);
            }
            finally
            {
                certificate?.Dispose();
                DisposeCertificates(additionalCertificates);
            }
        }

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
                    throw new AuthenticationException(
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
                    CryptographicOperations.ZeroMemory(pkcs12);
                }
            }
        }

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
                    throw new AuthenticationException(
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
                throw new AuthenticationException(
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

        private static bool IsPem(byte[] bytes)
        {
            ReadOnlySpan<byte> marker = "-----BEGIN"u8;
            return bytes.AsSpan().IndexOf(marker) >= 0;
        }

        /// <summary>
        /// Rejects the ODBC driver's <c>file:</c> path syntax, which this driver does not accept.
        /// </summary>
        /// <param name="path">The configured certificate or key path.</param>
        private static void ThrowIfOdbcStylePath(string path)
        {
            if (!string.IsNullOrEmpty(path) &&
                path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                throw new AuthenticationException(
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

#endif

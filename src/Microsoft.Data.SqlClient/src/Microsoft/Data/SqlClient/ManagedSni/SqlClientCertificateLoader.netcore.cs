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
                int leafCertificateIndex = -1;
                for (int index = 0; index < certificates.Count; index++)
                {
                    if (certificates[index].HasPrivateKey)
                    {
                        leafCertificateIndex = index;
                        break;
                    }
                }

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

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.AlwaysEncrypted;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/SqlColumnEncryptionCertificateStoreProvider/*' />
    public class SqlColumnEncryptionCertificateStoreProvider : SqlColumnEncryptionKeyStoreProvider
    {
        // Constants
        //
        // Assumption: Certificate Locations (LocalMachine & CurrentUser), Certificate Store name "My"
        // Certificate provider name (CertificateStore) don't need to be localized.

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/ProviderName/*' />
        public const string ProviderName = @"MSSQL_CERTIFICATE_STORE";

        /// <summary>
        /// This encryption keystore uses a certificate as the column master key.
        /// </summary>
        internal const string MasterKeyType = @"certificate";

        /// <summary>
        /// This encryption keystore uses the master key path to reference a specific certificate.
        /// </summary>
        internal const string KeyPathReference = @"certificate";

        /// <summary>
        /// RSA_OAEP is the only algorithm supported for encrypting/decrypting column encryption keys.
        /// </summary>
        internal const string RSAEncryptionAlgorithmWithOAEP = @"RSA_OAEP";

        /// <summary>
        /// LocalMachine certificate store location. Valid certificate locations are LocalMachine (on Windows) and CurrentUser.
        /// </summary>
        private const string CertLocationLocalMachine = @"LocalMachine";

        /// <summary>
        /// CurrentUser certificate store location. Valid certificate locations are LocalMachine (on Windows) and CurrentUser.
        /// </summary>
        private const string CertLocationCurrentUser = @"CurrentUser";

        /// <summary>
        /// Valid certificate store
        /// </summary>
        private const string MyCertificateStore = @"My";

        /// <summary>
        /// Gets a string array containing valid certificate locations.
        /// </summary>
        private static string[] ValidCertificateLocations =>
            Environment.OSVersion.Platform == PlatformID.Win32NT
                ? [CertLocationLocalMachine, CertLocationCurrentUser]
                : [CertLocationCurrentUser];

        /// <summary>
        /// This function validates that the encryption algorithm is RSA_OAEP and if it is not,
        /// then throws an exception
        /// </summary>
        /// <param name="encryptionAlgorithm">Asymmetric key encryption algorithm</param>
        /// <param name="isSystemOp"></param>
        private static void ValidateEncryptionAlgorithm([NotNull] string? encryptionAlgorithm, bool isSystemOp)
        {
            // This validates that the encryption algorithm is RSA_OAEP
            if (encryptionAlgorithm is null)
            {
                throw SQL.NullKeyEncryptionAlgorithm(isSystemOp);
            }

            if (!string.Equals(encryptionAlgorithm, RSAEncryptionAlgorithmWithOAEP, StringComparison.OrdinalIgnoreCase))
            {
                throw SQL.InvalidKeyEncryptionAlgorithm(encryptionAlgorithm, RSAEncryptionAlgorithmWithOAEP, isSystemOp);
            }
        }

        /// <summary>
        /// Checks if the certificate path is Empty, Null or larger than short.MaxValue characters (and raises exception if they are).
        /// </summary>
        private static void ValidateCertificatePathLength([NotNull] string? masterKeyPath, bool isSystemOp)
        {
            if (masterKeyPath is null)
            {
                throw SQL.NullCertificatePath(ValidCertificateLocations, isSystemOp);
            }

            if (string.IsNullOrWhiteSpace(masterKeyPath))
            {
                throw SQL.InvalidCertificatePath(masterKeyPath, ValidCertificateLocations, isSystemOp);
            }

            if (masterKeyPath.Length >= short.MaxValue)
            {
                throw SQL.LargeCertificatePathLength(masterKeyPath.Length, short.MaxValue, isSystemOp);
            }
        }

        /// <summary>
        /// Parses the given certificate path, searches in certificate store and returns a matching certificate's private key
        /// </summary>
        /// <param name="keyPath">
        /// Certificate key path. Format of the path is [LocalMachine|CurrentUser]/[StoreName]/Thumbprint
        /// </param>
        /// <param name="isSystemOp"></param>
        /// <returns>Returns the private key of the certificate identified by the certificate path</returns>
        private static RSA GetCertificatePrivateKeyByPath(string keyPath, bool isSystemOp)
        {
            StoreLocation storeLocation;
            StoreName storeName;
            // Convert keyPath to a span and slice based on the existence/position of separators. While string.Split('/') would also
            // suffice, using Span.Slice avoids allocating a string[] and its contents - the first two components will be mapped to
            // an enum value anyway.
            ReadOnlySpan<char> keyPathSpan = keyPath.AsSpan();
            int firstSeparator = keyPathSpan.IndexOf('/');
            ReadOnlySpan<char> trailingFirstSeparator = firstSeparator == -1 ? default : keyPathSpan.Slice(firstSeparator + 1);
            int secondSeparator = firstSeparator == -1 ? -1 : trailingFirstSeparator.IndexOf('/');
            ReadOnlySpan<char> trailingSecondSeparator = secondSeparator == -1 ? default : trailingFirstSeparator.Slice(secondSeparator + 1);
            int subsequentSeparators = secondSeparator == -1 ? -1 : trailingSecondSeparator.IndexOf('/');

            // Validate certificate path
            // Certificate path should only contain 3 parts (Certificate Location, Certificate Store Name and Thumbprint)
            // We know there are more than three parts if there are three or more separators
            if (subsequentSeparators != -1)
            {
                throw SQL.InvalidCertificatePath(keyPath, ValidCertificateLocations, isSystemOp);
            }

            ReadOnlySpan<char> storeLocationSpan = default;
            ReadOnlySpan<char> storeNameSpan = default;
            ReadOnlySpan<char> thumbprintSpan;

            // Extract the store location where the cert is stored. This can be in one of the following formats:
            // * [Location]/[StoreName]/[Thumbprint] (firstSeparator and secondSeparator are not -1)
            // * [StoreName]/[Thumbprint] (firstSeparator is not -1, secondSeparator is -1)
            // * [Thumbprint] (general case)
            if (secondSeparator != -1)
            {
                // There are two separators (and thus, three parts)
                storeLocationSpan = keyPathSpan.Slice(0, firstSeparator);
                storeNameSpan = trailingFirstSeparator.Slice(0, secondSeparator);
                thumbprintSpan = trailingSecondSeparator;
            }
            else if (firstSeparator != -1)
            {
                storeNameSpan = keyPathSpan.Slice(0, firstSeparator);
                thumbprintSpan = trailingFirstSeparator;
            }
            else
            {
                thumbprintSpan = keyPathSpan;
            }

            // Extract the store location where the cert is stored
            if (storeLocationSpan.IsEmpty
                && Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Default to Local Machine on Windows. Non-Windows platforms only support CurrentUser
                storeLocation = StoreLocation.LocalMachine;
            }
            else if (storeLocationSpan.Equals(CertLocationLocalMachine.AsSpan(), StringComparison.OrdinalIgnoreCase)
                && Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                storeLocation = StoreLocation.LocalMachine;
            }
            else if (storeLocationSpan.Equals(CertLocationCurrentUser.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                storeLocation = StoreLocation.CurrentUser;
            }
            else
            {
                // Throw an invalid certificate location exception
                throw SQL.InvalidCertificateLocation(storeLocationSpan.ToString(), keyPath, ValidCertificateLocations, isSystemOp);
            }

            // Parse the certificate store name
            if (storeNameSpan.IsEmpty)
            {
                // Default to My certificate store
                storeName = StoreName.My;
            }
            else if (storeNameSpan.Equals(MyCertificateStore.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                storeName = StoreName.My;
            }
            else
            {
                // We only support storing them in My certificate store
                throw SQL.InvalidCertificateStore(storeNameSpan.ToString(), keyPath, MyCertificateStore, isSystemOp);
            }

            // Get thumbprint
            if (thumbprintSpan.IsEmpty)
            {
                // An empty thumbprint specified
                throw SQL.EmptyCertificateThumbprint(keyPath, isSystemOp);
            }

            // Find the certificate and return
            return GetCertificatePrivateKey(storeLocation, storeName, keyPath, thumbprintSpan, isSystemOp);
        }

        /// <summary>
        /// Searches for a certificate in certificate store and returns the matching certificate's private key
        /// </summary>
        /// <param name="storeLocation">Store Location: This can be one of LocalMachine or UserName</param>
        /// <param name="storeName">Store Location: Currently this can only be My store.</param>
        /// <param name="masterKeyPath"></param>
        /// <param name="thumbprint">Certificate thumbprint</param>
        /// <param name="isSystemOp"></param>
        /// <returns>Matching certificate's private key</returns>
        private static RSA GetCertificatePrivateKey(StoreLocation storeLocation, StoreName storeName, string masterKeyPath, ReadOnlySpan<char> thumbprint, bool isSystemOp)
        {
            // Open specified certificate store
            using X509Store certificateStore = new(storeName, storeLocation);
            certificateStore.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            // Search for the specified certificate
            X509Certificate2Collection matchingCertificates =
                        certificateStore.Certificates.Find(X509FindType.FindByThumbprint,
                        thumbprint.ToString(),
                        false);

            // Throw an exception if a cert with the specified thumbprint is not found
            if (matchingCertificates == null || matchingCertificates.Count == 0)
            {
                throw SQL.CertificateNotFound(thumbprint.ToString(), storeName.ToString(), storeLocation.ToString(), isSystemOp);
            }

            using X509Certificate2 certificate = matchingCertificates[0];
            if (!certificate.HasPrivateKey)
            {
                // Ensure the certificate has private key
                throw SQL.CertificateWithNoPrivateKey(masterKeyPath, isSystemOp);
            }

            // Return the matching certificate's private key. Throw an exception if the private key is not RSA
            return certificate.GetRSAPrivateKey()
                ?? throw SQL.CertificateWithNoPrivateKey(masterKeyPath, isSystemOp);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/DecryptColumnEncryptionKey/*' />
        public override byte[] DecryptColumnEncryptionKey(string? masterKeyPath, string? encryptionAlgorithm, byte[]? encryptedColumnEncryptionKey)
        {
            // Validate the input parameters
            ValidateCertificatePathLength(masterKeyPath, isSystemOp: true);

            if (encryptedColumnEncryptionKey is null)
            {
                throw SQL.NullEncryptedColumnEncryptionKey();
            }
            else if (encryptedColumnEncryptionKey.Length == 0)
            {
                throw SQL.EmptyEncryptedColumnEncryptionKey();
            }

            // Validate encryptionAlgorithm
            ValidateEncryptionAlgorithm(encryptionAlgorithm, isSystemOp: true);

            // Parse the path and get the X509 cert
            RSA rsaPrivateKey = GetCertificatePrivateKeyByPath(masterKeyPath, isSystemOp: true);
            using EncryptedColumnEncryptionKeyParameters cekHandler = new(rsaPrivateKey, masterKeyPath, MasterKeyType, KeyPathReference);

            return cekHandler.Decrypt(encryptedColumnEncryptionKey);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/EncryptColumnEncryptionKey/*' />
        public override byte[] EncryptColumnEncryptionKey(string? masterKeyPath, string? encryptionAlgorithm, byte[]? columnEncryptionKey)
        {
            // Validate the input parameters
            ValidateCertificatePathLength(masterKeyPath, isSystemOp: false);
            if (columnEncryptionKey is null)
            {
                throw SQL.NullColumnEncryptionKey();
            }
            else if (columnEncryptionKey.Length == 0)
            {
                throw SQL.EmptyColumnEncryptionKey();
            }

            // Validate encryptionAlgorithm
            ValidateEncryptionAlgorithm(encryptionAlgorithm, isSystemOp: false);

            // Parse the certificate path and get the X509 cert
            RSA rsaPrivateKey = GetCertificatePrivateKeyByPath(masterKeyPath, isSystemOp: false);

            using EncryptedColumnEncryptionKeyParameters cekBuilder = new(rsaPrivateKey, masterKeyPath, MasterKeyType, KeyPathReference);

            return cekBuilder.Encrypt(columnEncryptionKey);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/SignColumnMasterKeyMetadata/*' />
        public override byte[] SignColumnMasterKeyMetadata(string? masterKeyPath, bool allowEnclaveComputations)
        {
            // Validate the input parameters
            ValidateCertificatePathLength(masterKeyPath, isSystemOp: false);

            // Parse the certificate path and get the X509 cert
            RSA rsaPrivateKey = GetCertificatePrivateKeyByPath(masterKeyPath, isSystemOp: false);

            using ColumnMasterKeyMetadata cmkSigner = new(rsaPrivateKey, masterKeyPath, ProviderName, allowEnclaveComputations);

            return cmkSigner.Sign();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCertificateStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionCertificateStoreProvider"]/VerifyColumnMasterKeyMetadata/*' />
        public override bool VerifyColumnMasterKeyMetadata(string? masterKeyPath, bool allowEnclaveComputations, byte[] signature)
        {
            // Validate the input parameters
            ValidateCertificatePathLength(masterKeyPath, isSystemOp: false);

            // Parse the certificate path and get the X509 cert
            RSA rsaPrivateKey = GetCertificatePrivateKeyByPath(masterKeyPath, isSystemOp: true);

            using ColumnMasterKeyMetadata cmkVerifier = new(rsaPrivateKey, masterKeyPath, ProviderName, allowEnclaveComputations);

            return cmkVerifier.Verify(signature);
        }
    }
}

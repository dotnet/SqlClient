// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.AlwaysEncrypted;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/SqlColumnEncryptionCngProvider/*' />
    public class SqlColumnEncryptionCngProvider : SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/ProviderName/*' />
        public const string ProviderName = @"MSSQL_CNG_STORE";

        /// <summary>
        /// This encryption keystore uses an asymmetric key as the column master key.
        /// </summary>
        internal const string MasterKeyType = @"asymmetric key";

        /// <summary>
        /// This encryption keystore uses the master key path to reference a CNG provider.
        /// </summary>
        internal const string KeyPathReference = @"Microsoft Cryptography API: Next Generation (CNG) provider";

        /// <summary>
        /// RSA_OAEP is the only algorithm supported for encrypting/decrypting column encryption keys using this provider.
        /// For now, we are keeping all the providers in sync.
        /// </summary>
        private const string RSAEncryptionAlgorithmWithOAEP = @"RSA_OAEP";

        /// <summary>
        /// This function validates that the encryption algorithm is RSA_OAEP and if it is not,
        /// then throws an exception.
        /// </summary>
        /// <param name="encryptionAlgorithm">Asymmetric key encryption algorithm.</param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API.</param>
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
        /// Checks if the CNG key path is Empty or Null (and raises exception if they are).
        /// </summary>
        /// <param name="masterKeyPath">Key path containing the CNG provider name and key name.</param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API.</param>
        private static void ValidateNonEmptyKeyPath([NotNull] string? masterKeyPath, bool isSystemOp)
        {
            if (masterKeyPath is null)
            {
                throw SQL.NullCngKeyPath(isSystemOp);
            }

            if (string.IsNullOrWhiteSpace(masterKeyPath))
            {
                throw SQL.InvalidCngPath(masterKeyPath, isSystemOp);
            }
        }

        /// <summary>
        /// Creates a RSACng from the given key path.
        /// </summary>
        /// <param name="keyPath">Key path in the format of [CNG provider name]/[key name].</param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API.</param>
        /// <returns></returns>
        private static RSACng CreateRSACngProvider(string keyPath, bool isSystemOp)
        {
            // Get CNGProvider and the KeyID
            GetCngProviderAndKeyId(keyPath, isSystemOp, out CngProvider cngProvider, out string keyIdentifier);

            try
            {
                using CngKey cngKey = CngKey.Open(keyIdentifier, cngProvider);

                // The RSACng constructor copies the input CngKey, so it is safe to dispose the original.
                return new RSACng(cngKey);
            }
            catch (CryptographicException)
            {
                throw SQL.InvalidCngKey(keyPath, cngProvider.Provider, keyIdentifier, isSystemOp);
            }
        }

        /// <summary>
        /// Extracts the CNG provider and key name from the given key path.
        /// </summary>
        /// <param name="keyPath">Key path in the format [CNG provider name]/[key name].</param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API.</param>
        /// <param name="cngProvider">CNG provider.</param>
        /// <param name="keyIdentifier">Key name inside the CNG provider.</param>
        private static void GetCngProviderAndKeyId(string keyPath, bool isSystemOp, out CngProvider cngProvider, out string keyIdentifier)
        {
            ReadOnlySpan<char> keyPathSpan = keyPath.AsSpan();
            int indexOfSlash = keyPathSpan.IndexOf('/');

            if (indexOfSlash == -1)
            {
                throw SQL.InvalidCngPath(keyPath, isSystemOp);
            }
            else if (indexOfSlash == 0)
            {
                throw SQL.EmptyCngName(keyPath, isSystemOp);
            }
            else if (indexOfSlash == keyPath.Length - 1)
            {
                throw SQL.EmptyCngKeyId(keyPath, isSystemOp);
            }

            ReadOnlySpan<char> cngProviderName = keyPathSpan.Slice(0, indexOfSlash);

            // If the provider is one of the well-known providers, use the static instance instead of allocating a new string and CngProvider.
            if (cngProviderName.Equals(CngProvider.MicrosoftSoftwareKeyStorageProvider.Provider.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                cngProvider = CngProvider.MicrosoftSoftwareKeyStorageProvider;
            }
            else if (cngProviderName.Equals(CngProvider.MicrosoftSmartCardKeyStorageProvider.Provider.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                cngProvider = CngProvider.MicrosoftSmartCardKeyStorageProvider;
            }
#if NET
            else if (cngProviderName.Equals(CngProvider.MicrosoftPlatformCryptoProvider.Provider.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                cngProvider = CngProvider.MicrosoftPlatformCryptoProvider;
            }
#endif
            else
            {
                cngProvider = new(cngProviderName.ToString());
            }

            keyIdentifier = keyPath.Substring(indexOfSlash + 1);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/DecryptColumnEncryptionKey/*' />
        public override byte[] DecryptColumnEncryptionKey(string? masterKeyPath, string? encryptionAlgorithm, byte[]? encryptedColumnEncryptionKey)
        {
            if (!ADP.IsWindows)
            {
                throw new PlatformNotSupportedException();
            }

            // Validate the input parameters
            ValidateNonEmptyKeyPath(masterKeyPath, isSystemOp: true);

            if (encryptedColumnEncryptionKey is null)
            {
                throw SQL.NullEncryptedColumnEncryptionKey();
            }

            if (encryptedColumnEncryptionKey.Length == 0)
            {
                throw SQL.EmptyEncryptedColumnEncryptionKey();
            }

            // Validate encryptionAlgorithm
            ValidateEncryptionAlgorithm(encryptionAlgorithm, isSystemOp: true);

            // Create RSA Provider with the given CNG provider name and key name
            RSA rsaProvider = CreateRSACngProvider(masterKeyPath, isSystemOp: true);
            using EncryptedColumnEncryptionKeyParameters cekDecryptionParameters = new(rsaProvider, masterKeyPath, MasterKeyType, KeyPathReference);

            return cekDecryptionParameters.Decrypt(encryptedColumnEncryptionKey);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/EncryptColumnEncryptionKey/*' />
        public override byte[] EncryptColumnEncryptionKey(string? masterKeyPath, string? encryptionAlgorithm, byte[]? columnEncryptionKey)
        {
            if (!ADP.IsWindows)
            {
                throw new PlatformNotSupportedException();
            }

            // Validate the input parameters
            ValidateNonEmptyKeyPath(masterKeyPath, isSystemOp: false);

            if (columnEncryptionKey is null)
            {
                throw SQL.NullColumnEncryptionKey();
            }

            if (columnEncryptionKey.Length == 0)
            {
                throw SQL.EmptyColumnEncryptionKey();
            }

            // Validate encryptionAlgorithm
            ValidateEncryptionAlgorithm(encryptionAlgorithm, isSystemOp: false);

            // Create RSA Provider with the given CNG provider name and key name
            RSA rsaProvider = CreateRSACngProvider(masterKeyPath, isSystemOp: false);
            using EncryptedColumnEncryptionKeyParameters cekEncryptionParameters = new(rsaProvider, masterKeyPath, MasterKeyType, KeyPathReference);

            return cekEncryptionParameters.Encrypt(columnEncryptionKey);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/SignColumnMasterKeyMetadata/*' />
        public override byte[] SignColumnMasterKeyMetadata(string? masterKeyPath, bool allowEnclaveComputations)
        {
            throw ADP.IsWindows
                ? new NotSupportedException()
                : new PlatformNotSupportedException();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/VerifyColumnMasterKeyMetadata/*' />
        public override bool VerifyColumnMasterKeyMetadata(string? masterKeyPath, bool allowEnclaveComputations, byte[]? signature)
        {
            throw ADP.IsWindows
                ? new NotSupportedException()
                : new PlatformNotSupportedException();
        }
    }
}

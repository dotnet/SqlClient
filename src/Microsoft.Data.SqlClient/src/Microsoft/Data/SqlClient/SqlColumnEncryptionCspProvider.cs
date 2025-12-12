// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.AlwaysEncrypted;
using Microsoft.Win32;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

#nullable enable

namespace Microsoft.Data.SqlClient
{

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/SqlColumnEncryptionCspProvider/*' />
    public class SqlColumnEncryptionCspProvider : SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/ProviderName/*' />
        public const string ProviderName = @"MSSQL_CSP_PROVIDER";

        /// <summary>
        /// This encryption keystore uses an asymmetric key as the column master key.
        /// </summary>
        internal const string MasterKeyType = @"asymmetric key";

        /// <summary>
        /// This encryption keystore uses the master key path to reference a CSP provider.
        /// </summary>
        internal const string KeyPathReference = @"Microsoft Cryptographic Service Provider (CSP)";

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
        /// Checks if the CSP key path is Empty or Null (and raises exception if they are).
        /// </summary>
        /// <param name="masterKeyPath">Key path containing the CSP provider name and key name.</param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API.</param>
        private static void ValidateNonEmptyCSPKeyPath([NotNull] string? masterKeyPath, bool isSystemOp)
        {
            if (masterKeyPath is null)
            {
                throw SQL.NullCspKeyPath(isSystemOp);
            }

            if (string.IsNullOrWhiteSpace(masterKeyPath))
            {
                throw SQL.InvalidCspPath(masterKeyPath, isSystemOp);
            }
        }

        /// <summary>
        /// Creates a RSACryptoServiceProvider from the given key path.
        /// </summary>
        /// <param name="keyPath">Key path in the format of [CSP provider name]/[key name].</param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API.</param>
        /// <returns></returns>
        private static RSACryptoServiceProvider CreateRSACryptoProvider(string keyPath, bool isSystemOp)
        {
            // Get CNGProvider and the KeyID
            GetCspProviderAndKeyName(keyPath, isSystemOp, out string cspProviderName, out string keyName);

            // Verify the existence of CSP and then get the provider type
            int providerType = GetProviderType(cspProviderName, keyPath, isSystemOp);

            // Create a new instance of CspParameters for an RSA container.
            CspParameters cspParams = new(providerType, cspProviderName, keyName) { Flags = CspProviderFlags.UseExistingKey };
            const int KEYSETDOESNOTEXIST = -2146893802;

            try
            {
                // Create a new instance of RSACryptoServiceProvider
                return new RSACryptoServiceProvider(cspParams);
            }
            catch (CryptographicException e) when (e.HResult == KEYSETDOESNOTEXIST)
            {
                // Key does not exist
                throw SQL.InvalidCspKeyIdentifier(keyName, keyPath, isSystemOp);
            }
        }

        /// <summary>
        /// Extracts the CSP provider name and key name from the given key path.
        /// </summary>
        /// <param name="keyPath">Key path in the format [CSP provider name]/[key name].</param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API.</param>
        /// <param name="cspProviderName">CSP provider name.</param>
        /// <param name="keyIdentifier">Key name inside the CSP provider.</param>
        private static void GetCspProviderAndKeyName(string keyPath, bool isSystemOp, out string cspProviderName, out string keyIdentifier)
        {
            int indexOfSlash = keyPath.IndexOf('/');

            if (indexOfSlash == -1)
            {
                throw SQL.InvalidCspPath(keyPath, isSystemOp);
            }
            else if (indexOfSlash == 0)
            {
                throw SQL.EmptyCspName(keyPath, isSystemOp);
            }
            else if (indexOfSlash == keyPath.Length - 1)
            {
                throw SQL.EmptyCspKeyId(keyPath, isSystemOp);
            }

            cspProviderName = keyPath.Substring(0, indexOfSlash);
            keyIdentifier = keyPath.Substring(indexOfSlash + 1);
        }

        /// <summary>
        /// Gets the type from a given CSP provider name.
        /// </summary>
        /// <param name="providerName">CSP provider name.</param>
        /// <param name="keyPath">Key path in the format of [CSP provider name]/[key name].</param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API.</param>
        /// <returns></returns>
        private static int GetProviderType(string providerName, string keyPath, bool isSystemOp)
        {
            using RegistryKey key = Registry.LocalMachine.OpenSubKey(@$"SOFTWARE\Microsoft\Cryptography\Defaults\Provider\{providerName}")
                ?? throw SQL.InvalidCspName(providerName, keyPath, isSystemOp);

            return (int)(key.GetValue(@"Type")
                ?? throw SQL.InvalidCspName(providerName, keyPath, isSystemOp));
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/DecryptColumnEncryptionKey/*' />
        public override byte[] DecryptColumnEncryptionKey(string? masterKeyPath, string? encryptionAlgorithm, byte[]? encryptedColumnEncryptionKey)
        {
            ADP.ThrowOnNonWindowsPlatform();

            // Validate the input parameters
            ValidateNonEmptyCSPKeyPath(masterKeyPath, isSystemOp: true);

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

            // Create RSA Provider with the given CSP name and key name
            RSA rsaProvider = CreateRSACryptoProvider(masterKeyPath, isSystemOp: true);
            using EncryptedColumnEncryptionKeyParameters cekDecryptionParameters = new(rsaProvider, masterKeyPath, MasterKeyType, KeyPathReference);

            return cekDecryptionParameters.Decrypt(encryptedColumnEncryptionKey);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/EncryptColumnEncryptionKey/*' />
        public override byte[] EncryptColumnEncryptionKey(string? masterKeyPath, string? encryptionAlgorithm, byte[]? columnEncryptionKey)
        {
            ADP.ThrowOnNonWindowsPlatform();

            // Validate the input parameters
            ValidateNonEmptyCSPKeyPath(masterKeyPath, isSystemOp: false);

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

            // Create RSA Provider with the given CSP name and key name
            RSA rsaProvider = CreateRSACryptoProvider(masterKeyPath, isSystemOp: false);
            using EncryptedColumnEncryptionKeyParameters cekEncryptionParameters = new(rsaProvider, masterKeyPath, MasterKeyType, KeyPathReference);

            return cekEncryptionParameters.Encrypt(columnEncryptionKey);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/SignColumnMasterKeyMetadata/*' />
        public override byte[] SignColumnMasterKeyMetadata(string? masterKeyPath, bool allowEnclaveComputations)
        {
            ADP.ThrowOnNonWindowsPlatform();
            throw new NotSupportedException();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCspProvider.xml' path='docs/members[@name="SqlColumnEncryptionCspProvider"]/VerifyColumnMasterKeyMetadata/*' />
        public override bool VerifyColumnMasterKeyMetadata(string? masterKeyPath, bool allowEnclaveComputations, byte[]? signature)
        {
            ADP.ThrowOnNonWindowsPlatform();
            throw new NotSupportedException();
        }
    }
}

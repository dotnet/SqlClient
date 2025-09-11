// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/DecryptColumnEncryptionKey/*' />
        public override byte[] DecryptColumnEncryptionKey(string? masterKeyPath, string? encryptionAlgorithm, byte[]? encryptedColumnEncryptionKey)
        {
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

            // Create RSA Provider with the given CNG name and key name
            RSA rsaProvider = CreateRSACngProvider(masterKeyPath, isSystemOp: true);
            using EncryptedColumnEncryptionKeyParameters cekDecryptionParameters = new(rsaProvider, masterKeyPath, MasterKeyType, KeyPathReference);

            return cekDecryptionParameters.Decrypt(encryptedColumnEncryptionKey);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/EncryptColumnEncryptionKey/*' />
        public override byte[] EncryptColumnEncryptionKey(string? masterKeyPath, string? encryptionAlgorithm, byte[]? columnEncryptionKey)
        {
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

            // Create RSACNGProviderWithKey
            RSA rsaProvider = CreateRSACngProvider(masterKeyPath, isSystemOp: false);
            using EncryptedColumnEncryptionKeyParameters cekEncryptionParameters = new(rsaProvider, masterKeyPath, MasterKeyType, KeyPathReference);

            return cekEncryptionParameters.Encrypt(columnEncryptionKey);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/SignColumnMasterKeyMetadata/*' />
        public override byte[] SignColumnMasterKeyMetadata(string? masterKeyPath, bool allowEnclaveComputations)
        {
            throw new NotSupportedException();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/VerifyColumnMasterKeyMetadata/*' />
        public override bool VerifyColumnMasterKeyMetadata(string? masterKeyPath, bool allowEnclaveComputations, byte[]? signature)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// This function validates that the encryption algorithm is RSA_OAEP and if it is not,
        /// then throws an exception
        /// </summary>
        /// <param name="encryptionAlgorithm">Asymmetric key encryption algorithm</param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API</param>
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
        /// <param name="masterKeyPath">keypath containing the CNG provider name and key name</param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API</param>
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
        /// Creates a RSACng object from the given keyPath.
        /// </summary>
        /// <param name="keyPath"></param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API</param>
        /// <returns></returns>
        private static RSACng CreateRSACngProvider(string keyPath, bool isSystemOp)
        {
            // Get CNGProvider and the KeyID
            GetCngProviderAndKeyId(keyPath, isSystemOp, out string cngProviderName, out string keyIdentifier);

            CngProvider cngProvider = new(cngProviderName);
            CngKey cngKey;

            try
            {
                cngKey = CngKey.Open(keyIdentifier, cngProvider);
            }
            catch (CryptographicException)
            {
                throw SQL.InvalidCngKey(keyPath, cngProviderName, keyIdentifier, isSystemOp);
            }

            using (cngKey)
            {
                return new RSACng(cngKey);
            }
        }

        /// <summary>
        /// Extracts the CNG provider and key name from the key path
        /// </summary>
        /// <param name="keyPath">keypath in the format [CNG Provider]/[KeyName]</param>
        /// <param name="isSystemOp">Indicates if ADO.NET calls or the customer calls the API</param>
        /// <param name="cngProvider">CNG Provider</param>
        /// <param name="keyIdentifier">Key identifier inside the CNG provider</param>
        private static void GetCngProviderAndKeyId(string keyPath, bool isSystemOp, out string cngProvider, out string keyIdentifier)
        {
            int indexOfSlash = keyPath.IndexOf('/');

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

            cngProvider = keyPath.Substring(0, indexOfSlash);
            keyIdentifier = keyPath.Substring(indexOfSlash + 1);
        }
    }
}

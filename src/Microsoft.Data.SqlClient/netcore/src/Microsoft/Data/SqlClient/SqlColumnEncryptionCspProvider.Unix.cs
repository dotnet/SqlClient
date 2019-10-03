// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Provides implementation similar to certificate store provider.
    /// A CEK encrypted with certificate store provider should be decryptable by this provider and vice versa.
    /// 
    /// Envolope Format for the encrypted column encryption key  
    ///           version + keyPathLength + ciphertextLength + keyPath + ciphertext +  signature
    /// version: A single byte indicating the format version.
    /// keyPathLength: Length of the keyPath.
    /// ciphertextLength: ciphertext length
    /// keyPath: keyPath used to encrypt the column encryption key. This is only used for troubleshooting purposes and is not verified during decryption.
    /// ciphertext: Encrypted column encryption key
    /// signature: Signature of the entire byte array. Signature is validated before decrypting the column encryption key.
    /// </summary>
    public class SqlColumnEncryptionCspProvider : SqlColumnEncryptionKeyStoreProvider
    {
        /// <summary>
        /// Name for the CSP key store provider.
        /// </summary>
        public const string ProviderName = @"MSSQL_CSP_PROVIDER";

        /// <summary>
        /// This function uses the asymmetric key specified by the key path
        /// and decrypts an encrypted CEK with RSA encryption algorithm.
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key in CSP</param>
        /// <param name="encryptionAlgorithm">Asymmetric Key Encryption Algorithm</param>
        /// <param name="encryptedColumnEncryptionKey">Encrypted Column Encryption Key</param>
        /// <returns>Plain text column encryption key</returns>
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm,
            byte[] encryptedColumnEncryptionKey)
        {
            throw new PlatformNotSupportedException();
        }

        /// <summary>
        /// This function uses the asymmetric key specified by the key path
        /// and encrypts CEK with RSA encryption algorithm.
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key in AKV</param>
        /// <param name="encryptionAlgorithm">Asymmetric Key Encryption Algorithm</param>
        /// <param name="columnEncryptionKey">Plain text column encryption key</param>
        /// <returns>Encrypted column encryption key</returns>
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm,
            byte[] columnEncryptionKey)
        {
            throw new PlatformNotSupportedException();
        }

        /// <summary>
        /// Throws NotSupportedException. In this version of .NET Framework this provider does not support signing column master key metadata.
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key. Path format is specific to a key store provider.</param>
        /// <param name="allowEnclaveComputations">Boolean indicating whether this key can be sent to trusted enclave</param>
        /// <returns>Encrypted column encryption key</returns>
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations)
        {
            throw new PlatformNotSupportedException();
        }

        /// <summary>
        /// Throws NotSupportedException. In this version of .NET Framework this provider does not support verifying signatures of column master key metadata.
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key. Path format is specific to a key store provider.</param>
        /// <param name="allowEnclaveComputations">Boolean indicating whether this key can be sent to trusted enclave</param>
        /// <param name="signature">Signature for the master key metadata</param>
        /// <returns>Boolean indicating whether the master key metadata can be verified based on the provided signature</returns>
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
        {
            throw new PlatformNotSupportedException();
        }
    }
}

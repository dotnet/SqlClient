// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

#nullable enable

namespace Microsoft.Data.SqlClient.AlwaysEncrypted
{
    /// <summary>
    /// Encryption key class containing 4 keys. This class is used by SqlAeadAes256CbcHmac256Algorithm
    /// 1) root key - Main key that is used to derive the keys used in the encryption algorithm
    /// 2) encryption key - A derived key that is used to encrypt the plain text and generate cipher text
    /// 3) mac_key - A derived key that is used to compute HMAC of the cipher text
    /// 4) iv_key - A derived key that is used to generate a synthetic IV from plain text data.
    /// </summary>
    internal sealed class AeadAes256CbcHmac256EncryptionKey : SymmetricKey
    {
        /// <summary>
        /// Key size in bits.
        /// </summary>
        public const int KeySizeInBits = 256;

        /// <summary>
        /// Key size in bytes.
        /// </summary>
        public const int KeySizeInBytes = KeySizeInBits / 8;

        /// <summary>
        /// <see cref="KeySizeInBits"/> as a string, for use in the salt formats below.
        /// </summary>
        private const string KeySizeInBitsString = "256";

        /// <summary>
        /// Encryption Key Salt. This is used to derive the encryption key from the root key.
        /// </summary>
        /// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/always-encrypted-cryptography?view=sql-server-ver17#step-2-computing-aes_256_cbc-ciphertext"/>
        private const string EncryptionKeySaltString = $"Microsoft SQL Server cell encryption key with encryption algorithm:{SqlAeadAes256CbcHmac256Algorithm.AlgorithmName} and key length:{KeySizeInBitsString}";

        /// <summary>
        /// MAC Key Salt. This is used to derive the MAC key from the root key.
        /// </summary>
        /// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/always-encrypted-cryptography?view=sql-server-ver17#step-3-computing-mac"/>
        private const string MacKeySaltString = $"Microsoft SQL Server cell MAC key with encryption algorithm:{SqlAeadAes256CbcHmac256Algorithm.AlgorithmName} and key length:{KeySizeInBitsString}";

        /// <summary>
        /// IV Key Salt. This is used to derive the IV key from the root key. This is only used for Deterministic encryption.
        /// </summary>
        /// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/always-encrypted-cryptography?view=sql-server-ver17#step-1-generating-the-initialization-vector-iv"/>
        private const string IvKeySaltString = $"Microsoft SQL Server cell IV key with encryption algorithm:{SqlAeadAes256CbcHmac256Algorithm.AlgorithmName} and key length:{KeySizeInBitsString}";

        private static byte[] EncryptionKeySalt =>
            field ??= Encoding.Unicode.GetBytes(EncryptionKeySaltString);
        private static byte[] MacKeySalt =>
            field ??= Encoding.Unicode.GetBytes(MacKeySaltString);
        private static byte[] IvKeySalt =>
            field ??= Encoding.Unicode.GetBytes(IvKeySaltString);

        /// <summary>
        /// Derives all the required keys from the given root key
        /// </summary>
        /// <param name="rootKey">Root key used to derive all the required derived keys</param>
        public AeadAes256CbcHmac256EncryptionKey(byte[] rootKey) : base(rootKey)
        {
            // Key validation
            if (rootKey.Length != KeySizeInBytes)
            {
                throw SQL.InvalidKeySize(SqlAeadAes256CbcHmac256Algorithm.AlgorithmName,
                                         rootKey.Length,
                                         KeySizeInBytes);
            }

            // Derive keys from the root key
            //
            // Derive encryption key
            byte[] buff1 = new byte[KeySizeInBytes];
            SqlSecurityUtility.GetHMACWithSHA256(EncryptionKeySalt, RootKey, buff1);
            EncryptionKey = buff1;

            // Derive MAC key
            byte[] buff2 = new byte[KeySizeInBytes];
            SqlSecurityUtility.GetHMACWithSHA256(MacKeySalt, RootKey, buff2);
            MacKey = buff2;

            // Derive IV key
            byte[] buff3 = new byte[KeySizeInBytes];
            SqlSecurityUtility.GetHMACWithSHA256(IvKeySalt, RootKey, buff3);
            IvKey = buff3;
        }

        /// <summary>
        /// Encryption key should be used for encryption and decryption
        /// </summary>
        public byte[] EncryptionKey { get; }

        /// <summary>
        /// MAC key should be used to compute and validate HMAC
        /// </summary>
        public byte[] MacKey { get; }

        /// <summary>
        /// IV key should be used to compute synthetic IV from a given plain text
        /// </summary>
        public byte[] IvKey { get; }
    }
}

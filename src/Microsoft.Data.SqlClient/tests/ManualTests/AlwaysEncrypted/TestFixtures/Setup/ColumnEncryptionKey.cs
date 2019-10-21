// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup
{
    public class ColumnEncryptionKey : DbObject
    {
        public string Algorithm { get; set; } = "RSA_OAEP";
        public string EncryptedValue { get; }
        public ColumnMasterKey ColumnMasterKey { get; }
        public static int KeySizeInBytes { get; } = 32;

        public ColumnEncryptionKey(string name, ColumnMasterKey columnMasterKey, SqlColumnEncryptionKeyStoreProvider columnEncryptionProvider) : base(name)
        {
            ColumnMasterKey = columnMasterKey;
            byte[] plainTextColumnEncryptionKey = GenerateRandomBytes(KeySizeInBytes);
            byte[] encryptedColumnEncryptionKey = CreateEncryptedCek(columnMasterKey.KeyPath, Algorithm, plainTextColumnEncryptionKey, columnEncryptionProvider);
            EncryptedValue = string.Concat("0x", BitConverter.ToString(encryptedColumnEncryptionKey).Replace("-", string.Empty));
        }

        public override void Create(SqlConnection sqlConnection)
        {
            string sql =
                $@"CREATE COLUMN ENCRYPTION KEY [{Name}] 
                    WITH VALUES (
                        COLUMN_MASTER_KEY = [{ColumnMasterKey.Name}],
                        ALGORITHM = '{Algorithm}', 
                        ENCRYPTED_VALUE = {EncryptedValue}
                    )";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        public override void Drop(SqlConnection sqlConnection)
        {
            string sql = $"DROP COLUMN ENCRYPTION KEY [{Name}]";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Generates cryptographically random bytes
        /// </summary>
        /// <param name="length">No of cryptographically random bytes to be generated</param>
        /// <returns>A byte array containing cryptographically generated random bytes</returns>
        public static byte[] GenerateRandomBytes(int length)
        {
            // Generate random bytes cryptographically.
            byte[] randomBytes = new byte[length];
            RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
            rngCsp.GetBytes(randomBytes);

            return randomBytes;
        }

        /// <summary>
        /// Create Encrypted CEK
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static byte[] CreateEncryptedCek(string masterKeyPath, string encryptionKeyAlgorithm, byte[] key, SqlColumnEncryptionKeyStoreProvider columnEncryptionProvider)
        {
            byte[] encryptedCek = null;
            encryptedCek = columnEncryptionProvider.EncryptColumnEncryptionKey(masterKeyPath, encryptionKeyAlgorithm, key);

            return encryptedCek;
        }

        /// <summary>
        /// ECEK Corruption types (useful for testing)
        /// </summary>
        internal enum ECEKCorruption
        {
            ALGORITHM_VERSION,
            CEK_LENGTH,
            SIGNATURE,
            SIGNATURE_LENGTH
        }

        /// <summary>
        /// Takes a well formed encrypted CEK and corrupts it based on ECEKCorruption flags
        /// </summary>
        /// <param name="encryptedCek">An encrypted cek that is wellformed (can be successfully decrypted)</param>
        /// <param name="type">Type of corrupted desired</param>
        /// <returns>A byte array containing corrupted CEK (decryption will throw an exception)</returns>
        internal static byte[] GenerateInvalidEncryptedCek(byte[] encryptedCek, ECEKCorruption type)
        {
            byte[] cipherText = null;
            switch (type)
            {
                case ECEKCorruption.ALGORITHM_VERSION:
                    cipherText = new byte[encryptedCek.Length];
                    cipherText[0] = 0x10;
                    break;

                case ECEKCorruption.CEK_LENGTH:
                    int sourceIndex = 0;
                    int targetIndex = 0;
                    cipherText = new byte[encryptedCek.Length - 10];

                    // Remove 10 bytes from the encrypted CEK, copy the signatures as is (signature validation comes later)
                    cipherText[sourceIndex] = encryptedCek[targetIndex];
                    sourceIndex++;
                    targetIndex++;

                    short keyPathLen = BitConverter.ToInt16(encryptedCek, sourceIndex);
                    sourceIndex += 2;
                    // Copy it over as is
                    Buffer.BlockCopy(encryptedCek, sourceIndex, cipherText, targetIndex, 2);
                    targetIndex += 2;

                    // Read ciphertext length
                    short cipherTextLen = BitConverter.ToInt16(encryptedCek, sourceIndex);
                    sourceIndex += 2;
                    // Reduce this by 5 and copy to target
                    Buffer.BlockCopy(BitConverter.GetBytes(cipherTextLen - 5), 0, cipherText, targetIndex, 2);
                    targetIndex += 2;

                    // Copy the cipherText
                    Buffer.BlockCopy(encryptedCek, sourceIndex, cipherText, targetIndex, cipherTextLen - 5);
                    sourceIndex += cipherTextLen;
                    targetIndex += cipherTextLen - 5;

                    // Copy the key path
                    Buffer.BlockCopy(encryptedCek, sourceIndex, cipherText, targetIndex, keyPathLen);
                    sourceIndex += keyPathLen;
                    targetIndex += keyPathLen;

                    // Copy the signature
                    Buffer.BlockCopy(encryptedCek, sourceIndex, cipherText, targetIndex, encryptedCek.Length - sourceIndex - 6);
                    break;

                case ECEKCorruption.SIGNATURE:
                    cipherText = new byte[encryptedCek.Length];
                    Buffer.BlockCopy(encryptedCek, 0, cipherText, 0, cipherText.Length);
                    // Wipe out the signature (signature is 32 bytes long)
                    for (int i = 0; i < 32; i++)
                    {
                        cipherText[cipherText.Length - i - 1] = 0x00;
                    }

                    break;

                case ECEKCorruption.SIGNATURE_LENGTH:
                    // Make the signature shorter by 7 bytes, its length is 32 bytes 
                    cipherText = new byte[encryptedCek.Length - 7];
                    Buffer.BlockCopy(encryptedCek, 0, cipherText, 0, cipherText.Length);
                    break;
            }

            return cipherText;
        }
    }
}

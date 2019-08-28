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
        public int KeySizeInBytes { get; } = 32;

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
        private static byte[] GenerateRandomBytes(int length)
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
    }
}

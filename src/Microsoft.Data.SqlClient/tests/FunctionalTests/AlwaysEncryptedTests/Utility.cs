// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    internal static class Utility
    {
        internal const string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA256";

        // reflections
        public static Assembly systemData = Assembly.GetAssembly(typeof(SqlConnection));
        public static Type sqlClientSymmetricKey = systemData.GetType("Microsoft.Data.SqlClient.SqlClientSymmetricKey");
        public static Type sqlAeadAes256CbcHmac256Factory = systemData.GetType("Microsoft.Data.SqlClient.SqlAeadAes256CbcHmac256Factory");
        public static ConstructorInfo sqlColumnEncryptionKeyConstructor = sqlClientSymmetricKey.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(byte[]) }, null);
        public static MethodInfo sqlAeadAes256CbcHmac256FactoryCreate = sqlAeadAes256CbcHmac256Factory.GetMethod("Create", BindingFlags.Instance | BindingFlags.NonPublic);
        public static Type sqlClientEncryptionAlgorithm = systemData.GetType("Microsoft.Data.SqlClient.SqlClientEncryptionAlgorithm");
        public static MethodInfo sqlClientEncryptionAlgorithmEncryptData = sqlClientEncryptionAlgorithm.GetMethod("EncryptData", BindingFlags.Instance | BindingFlags.NonPublic);
        public static Type SqlCipherMetadata = systemData.GetType("Microsoft.Data.SqlClient.SqlCipherMetadata");
        public static FieldInfo sqlTceCipherInfoEntryField = SqlCipherMetadata.GetField("_sqlTceCipherInfoEntry", BindingFlags.Instance | BindingFlags.NonPublic);
        public static Type SqlTceCipherInfoEntry = systemData.GetType("Microsoft.Data.SqlClient.SqlTceCipherInfoEntry");
        public static MethodInfo SqlTceCipherInfoEntryAdd = SqlTceCipherInfoEntry.GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic);
        public static ConstructorInfo SqlCipherMetadataConstructor = SqlCipherMetadata.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { SqlTceCipherInfoEntry, typeof(ushort), typeof(byte), typeof(string), typeof(byte), typeof(byte) }, null);
        public static ConstructorInfo SqlTceCipherInfoEntryConstructor = SqlTceCipherInfoEntry.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(int) }, null);
        public static Type SqlSecurityUtil = systemData.GetType("Microsoft.Data.SqlClient.SqlSecurityUtility", throwOnError: true);
        public static MethodInfo SqlSecurityUtilEncryptWithKey = SqlSecurityUtil.GetMethod("EncryptWithKey", BindingFlags.Static | BindingFlags.NonPublic);
        public static MethodInfo SqlSecurityUtilDecryptWithKey = SqlSecurityUtil.GetMethod("DecryptWithKey", BindingFlags.Static | BindingFlags.NonPublic);
        public static MethodInfo sqlClientEncryptionAlgorithmDecryptData = sqlClientEncryptionAlgorithm.GetMethod("DecryptData", BindingFlags.Instance | BindingFlags.NonPublic);

        #region Fields

        /// <summary>
        /// SQL types unsupported by TCE
        /// </summary>
        private static SqlDbType[] unsupportedTypes = new SqlDbType[] { SqlDbType.Image,
                                                                 SqlDbType.NText,
                                                                 SqlDbType.Text,
                                                                 SqlDbType.Timestamp,
                                                                 SqlDbType.Variant,
                                                                 SqlDbType.Xml,
                                                                 SqlDbType.Udt,
                                                                 SqlDbType.Structured };

        private static Random random = new Random();

        private static int[] charLengths = new int[] { 1, 10, 100, 1000, 4000 }; // TODO: large values needs to be moved to their own tests due to row length restriction (8060 bytes)
        private static int[] ncharLengths = new int[] { 1, 10, 100, 1000, 2000 };
        
        #endregion

        #region Methods

        #region Encryption Management

        /// <summary>
        /// ECEK Corruption types (useful for testing)
        /// </summary>
        internal enum ECEKCorruption {
            ALGORITHM_VERSION,
            CEK_LENGTH,
            SIGNATURE,
            SIGNATURE_LENGTH
        }

        /// <summary>
        /// Encryption Type as per the test code. Different than product code's enumeration.
        /// </summary>
        internal enum CColumnEncryptionType
        {
            PlainText = 0,
            Deterministic,
            Randomized
        }
        #endregion

        #region Key Generation

        /// <summary>
        /// Generates cryptographically random bytes
        /// </summary>
        /// <param name="length">No of cryptographically random bytes to be generated</param>
        /// <returns>A byte array containing cryptographically generated random bytes</returns>
        internal static byte[] GenerateRandomBytes(int length)
        {
            // Generate random bytes cryptographically.
            byte[] randomBytes = new byte[length];
            RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
            rngCsp.GetBytes(randomBytes);

            return randomBytes;
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
        
        internal static X509Certificate2 CreateCertificate()
        {
            byte[] certificateRawBytes = new byte[] { 48, 130, 3, 52, 48, 130, 2, 28, 160, 3, 2, 1, 2, 2, 16, 55, 130, 235, 197, 31, 26, 120, 172, 77, 241, 92, 126, 16, 70, 52, 187, 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 11, 5, 0, 48, 28, 49, 26, 48, 24, 6, 3, 85, 4, 3, 19, 17, 77, 121, 67, 101, 114, 116, 105, 102, 105, 99, 97, 116, 101, 78, 97, 109, 101, 48, 30, 23, 13, 49, 57, 48, 56, 50, 54, 50, 50, 52, 51, 48, 51, 90, 23, 13, 51, 57, 49, 50, 51, 49, 50, 51, 53, 57, 53, 57, 90, 48, 28, 49, 26, 48, 24, 6, 3, 85, 4, 3, 19, 17, 77, 121, 67, 101, 114, 116, 105, 102, 105, 99, 97, 116, 101, 78, 97, 109, 101, 48, 130, 1, 34, 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 1, 5, 0, 3, 130, 1, 15, 0, 48, 130, 1, 10, 2, 130, 1, 1, 0, 155, 128, 78, 186, 240, 28, 129, 84, 13, 87, 243, 127, 162, 194, 20, 206, 218, 60, 189, 144, 81, 143, 208, 102, 174, 105, 218, 234, 76, 251, 160, 109, 113, 213, 219, 193, 7, 241, 63, 225, 10, 187, 252, 103, 51, 121, 1, 32, 235, 13, 54, 204, 252, 112, 142, 20, 146, 68, 176, 50, 242, 96, 22, 67, 85, 82, 247, 122, 184, 53, 233, 49, 223, 208, 239, 61, 95, 51, 241, 132, 197, 212, 130, 123, 117, 58, 97, 53, 141, 65, 32, 143, 148, 90, 116, 63, 236, 183, 99, 150, 48, 149, 62, 175, 253, 74, 116, 212, 27, 153, 92, 209, 64, 0, 82, 189, 223, 49, 216, 139, 7, 79, 86, 123, 147, 117, 131, 204, 195, 16, 221, 218, 30, 75, 17, 170, 209, 10, 193, 198, 146, 199, 196, 79, 2, 27, 63, 117, 218, 129, 45, 5, 35, 188, 36, 152, 248, 72, 250, 209, 255, 71, 43, 96, 253, 190, 90, 176, 236, 107, 248, 170, 120, 185, 83, 249, 241, 23, 205, 251, 101, 107, 140, 168, 23, 248, 238, 80, 23, 221, 224, 25, 71, 241, 16, 26, 189, 166, 49, 175, 127, 158, 123, 109, 131, 125, 241, 51, 55, 181, 183, 211, 88, 13, 211, 219, 68, 72, 237, 19, 170, 219, 248, 147, 168, 237, 73, 149, 62, 204, 152, 217, 48, 237, 15, 195, 7, 175, 34, 241, 175, 179, 19, 172, 147, 201, 105, 51, 218, 138, 235, 24, 41, 213, 84, 185, 2, 3, 1, 0, 1, 163, 114, 48, 112, 48, 31, 6, 3, 85, 29, 37, 4, 24, 48, 22, 6, 8, 43, 6, 1, 5, 5, 8, 2, 2, 6, 10, 43, 6, 1, 4, 1, 130, 55, 10, 3, 11, 48, 77, 6, 3, 85, 29, 1, 4, 70, 48, 68, 128, 16, 216, 253, 207, 70, 139, 39, 93, 135, 25, 98, 110, 197, 6, 196, 131, 50, 161, 30, 48, 28, 49, 26, 48, 24, 6, 3, 85, 4, 3, 19, 17, 77, 121, 67, 101, 114, 116, 105, 102, 105, 99, 97, 116, 101, 78, 97, 109, 101, 130, 16, 55, 130, 235, 197, 31, 26, 120, 172, 77, 241, 92, 126, 16, 70, 52, 187, 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 11, 5, 0, 3, 130, 1, 1, 0, 150, 191, 139, 9, 124, 192, 185, 119, 117, 41, 35, 37, 117, 23, 143, 120, 206, 10, 116, 236, 243, 171, 49, 216, 42, 10, 172, 230, 187, 118, 201, 238, 9, 77, 49, 234, 169, 193, 163, 177, 120, 24, 148, 56, 47, 82, 197, 107, 72, 167, 220, 161, 84, 70, 242, 17, 54, 38, 235, 65, 217, 239, 22, 164, 82, 103, 167, 120, 252, 126, 158, 224, 68, 205, 210, 105, 239, 25, 170, 197, 113, 167, 152, 54, 8, 117, 224, 150, 205, 218, 42, 177, 191, 57, 52, 2, 25, 251, 246, 7, 202, 12, 112, 202, 161, 195, 218, 102, 54, 249, 156, 71, 11, 175, 132, 189, 54, 111, 226, 114, 15, 81, 205, 22, 71, 172, 44, 35, 48, 161, 54, 139, 155, 31, 205, 156, 134, 178, 61, 227, 205, 250, 134, 255, 113, 35, 96, 137, 200, 218, 50, 164, 148, 12, 68, 176, 227, 46, 219, 215, 108, 194, 62, 191, 191, 206, 181, 171, 224, 14, 11, 194, 122, 102, 183, 10, 79, 238, 48, 66, 134, 98, 21, 208, 30, 154, 187, 35, 116, 251, 20, 251, 175, 117, 93, 33, 202, 207, 104, 225, 41, 158, 255, 206, 45, 205, 66, 166, 5, 201, 225, 191, 19, 165, 131, 224, 7, 57, 113, 156, 37, 72, 149, 166, 180, 114, 250, 132, 218, 194, 53, 37, 21, 164, 163, 4, 74, 40, 165, 245, 211, 77, 185, 115, 203, 25, 136, 17, 110, 168, 65, 69, 193, 166, 252, 150 };
            X509Certificate2 certificate = new X509Certificate2(certificateRawBytes, "", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
            X509Store certStore = null;
            try
            {
                certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                certStore.Open(OpenFlags.ReadWrite);
                if (!certStore.Certificates.Contains(certificate))
                {
                    certStore.Add(certificate);
                }

            }
            finally
            {
                if (certStore != null)
                {
                    certStore.Close();
                }
            }

            return certificate;
        }

        /// <summary>
        /// Removes a certificate from the local certificate store (useful for test cleanup).
        /// </summary>
        internal static void RemoveCertificate(X509Certificate2 certificate)
        {
            X509Store certStore = null;
            try
            {
                certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                certStore.Open(OpenFlags.ReadWrite);
                certStore.Remove(certificate);
            }
            finally
            {
                if (certStore != null)
                {
                    certStore.Close();
                }
            }
        }

        /// <summary>
        /// Encrypt Data using AED
        /// </summary>
        /// <param name="plainTextData"></param>
        /// <returns></returns>
        internal static byte[] EncryptDataUsingAED(byte[] plainTextData, byte[] key, CColumnEncryptionType encryptionType)
        {
            Assert.True(plainTextData != null);
            Assert.True(key != null && key.Length > 0);
            byte[] encryptedData = null;

            Object columnEncryptionKey = sqlColumnEncryptionKeyConstructor.Invoke(new object[] { key });
            Assert.True(columnEncryptionKey != null);

            Object aesFactory = Activator.CreateInstance(sqlAeadAes256CbcHmac256Factory);
            Assert.True(aesFactory != null);

            object[] parameters = new object[] { columnEncryptionKey, encryptionType, ColumnEncryptionAlgorithmName };
            Object authenticatedAES = sqlAeadAes256CbcHmac256FactoryCreate.Invoke(aesFactory, parameters);
            Assert.True(authenticatedAES != null);

            parameters = new object[] { plainTextData };
            Object finalCellBlob = sqlClientEncryptionAlgorithmEncryptData.Invoke(authenticatedAES, parameters);
            Assert.True(finalCellBlob != null);

            encryptedData = (byte[])finalCellBlob;

            return encryptedData;
        }

        /// <summary>
        /// Adds an entry in SqlCipherMetadat (using reflections).
        /// </summary>
        internal static void AddEncryptionKeyToCipherMD(
                Object entry,
                byte[] encryptedKey,
                int databaseId,
                int cekId,
                int cekVersion,
                byte[] cekMdVersion,
                string keyPath,
                string keyStoreName,
                string algorithmName)
        {
            // Get the SqlTceCipherInfo contained in the object "entry" and add a record to it
            Object sqlTceCipherInfoEntry = sqlTceCipherInfoEntryField.GetValue(entry);
            SqlTceCipherInfoEntryAdd.Invoke(sqlTceCipherInfoEntry,
                new Object[] { encryptedKey, databaseId, cekId, cekVersion, cekMdVersion, keyPath, keyStoreName, algorithmName });
        }

        internal static Object GetSqlCipherMetadata(ushort ordinal, byte cipherAlgorithmId, string cipherAlgorithmName, byte encryptionType, byte normalizationRuleVersion)
        {
            Assert.True(null != SqlCipherMetadataConstructor);
            Assert.True(null != SqlTceCipherInfoEntryConstructor);
            Object entry = SqlTceCipherInfoEntryConstructor.Invoke(new object[] { 1 });// this param is "ordinal"
            Object[] parameters = new Object[] { entry, ordinal, cipherAlgorithmId, cipherAlgorithmName, encryptionType, normalizationRuleVersion };
            return SqlCipherMetadataConstructor.Invoke(parameters);
        }

        internal static byte[] DecryptWithKey(byte[] cipherText, Object cipherMd, string serverName)
        {
            return (byte[])SqlSecurityUtilDecryptWithKey.Invoke(null, new Object[] { cipherText, cipherMd, serverName });
        }

        internal static byte[] EncryptWithKey(byte[] plainText, Object cipherMd, string serverName)
        {
            return (byte[])SqlSecurityUtilEncryptWithKey.Invoke(null, new Object[] { plainText, cipherMd, serverName });
        }

        /// <summary>
        /// Decrypt Data using AEAD
        /// </summary>
        internal static byte[] DecryptDataUsingAED(byte[] encryptedCellBlob, byte[] key, CColumnEncryptionType encryptionType)
        {
            Assert.True(encryptedCellBlob != null && encryptedCellBlob.Length > 0);
            Assert.True(key != null && key.Length > 0);

            byte[] decryptedData = null;

            Object columnEncryptionKey = sqlColumnEncryptionKeyConstructor.Invoke(new object[] { key });
            Assert.True(columnEncryptionKey != null);

            Object aesFactory = Activator.CreateInstance(sqlAeadAes256CbcHmac256Factory);
            Assert.True(aesFactory != null);

            object[] parameters = new object[] { columnEncryptionKey, encryptionType, ColumnEncryptionAlgorithmName };
            Object authenticatedAES = sqlAeadAes256CbcHmac256FactoryCreate.Invoke(aesFactory, parameters);
            Assert.True(authenticatedAES != null);

            parameters = new object[] { encryptedCellBlob };
            Object decryptedValue = sqlClientEncryptionAlgorithmDecryptData.Invoke(authenticatedAES, parameters);
            Assert.True(decryptedValue != null);

            decryptedData = (byte[])decryptedValue;

            return decryptedData;
        }
        
        /// <summary>
        /// Create a self-signed certificate without private key. NET46 only.
        /// </summary>
        internal static X509Certificate2 CreateCertificateWithNoPrivateKey()
        {
            byte[] certificateRawBytes = new byte[] { 48, 130, 3, 47, 48, 130, 2, 27, 160, 3, 2, 1, 2, 2, 16, 137, 236, 21, 9, 59, 63, 234, 150, 65, 44, 54, 227, 174, 128, 125, 73, 48, 9, 6, 5, 43, 14, 3, 2, 29, 5, 0, 48, 29, 49, 27, 48, 25, 6, 3, 85, 4, 3, 19, 18, 77, 121, 67, 101, 114, 116, 105, 102, 105, 99, 97, 116, 101, 78, 97, 109, 101, 50, 48, 30, 23, 13, 49, 57, 48, 56, 50, 54, 50, 50, 52, 51, 48, 51, 90, 23, 13, 51, 57, 49, 50, 51, 49, 50, 51, 53, 57, 53, 57, 90, 48, 29, 49, 27, 48, 25, 6, 3, 85, 4, 3, 19, 18, 77, 121, 67, 101, 114, 116, 105, 102, 105, 99, 97, 116, 101, 78, 97, 109, 101, 50, 48, 130, 1, 34, 48, 13, 6, 9, 42, 134, 72, 134, 247, 13, 1, 1, 1, 5, 0, 3, 130, 1, 15, 0, 48, 130, 1, 10, 2, 130, 1, 1, 0, 168, 126, 35, 3, 254, 214, 118, 112, 89, 108, 47, 161, 99, 16, 109, 82, 58, 110, 202, 17, 23, 47, 225, 187, 54, 200, 104, 55, 135, 227, 105, 166, 93, 42, 224, 15, 117, 194, 113, 88, 150, 67, 136, 101, 135, 206, 238, 68, 202, 180, 116, 86, 46, 127, 204, 100, 60, 194, 23, 41, 115, 80, 218, 245, 129, 207, 211, 211, 226, 246, 231, 131, 112, 174, 22, 66, 26, 154, 233, 175, 238, 21, 206, 77, 225, 234, 250, 223, 183, 131, 139, 109, 177, 113, 153, 99, 89, 136, 105, 124, 185, 106, 37, 189, 175, 1, 95, 51, 151, 227, 49, 11, 42, 111, 7, 53, 95, 35, 17, 17, 89, 166, 98, 158, 235, 31, 207, 61, 79, 191, 249, 35, 27, 95, 148, 241, 104, 229, 254, 230, 6, 65, 151, 78, 0, 90, 196, 208, 123, 125, 161, 87, 39, 44, 96, 125, 146, 198, 248, 230, 233, 105, 212, 164, 7, 117, 118, 90, 80, 191, 251, 204, 20, 204, 137, 247, 45, 148, 235, 227, 205, 33, 157, 59, 75, 147, 84, 211, 97, 192, 174, 156, 85, 215, 101, 66, 212, 239, 210, 63, 249, 218, 225, 5, 15, 253, 202, 34, 152, 112, 36, 7, 8, 19, 189, 94, 230, 229, 68, 29, 92, 249, 116, 109, 115, 84, 56, 202, 113, 60, 101, 194, 163, 57, 126, 69, 56, 25, 95, 255, 115, 68, 247, 212, 140, 240, 39, 7, 110, 31, 12, 232, 42, 195, 202, 229, 2, 3, 1, 0, 1, 163, 115, 48, 113, 48, 31, 6, 3, 85, 29, 37, 4, 24, 48, 22, 6, 8, 43, 6, 1, 5, 5, 8, 2, 2, 6, 10, 43, 6, 1, 4, 1, 130, 55, 10, 3, 11, 48, 78, 6, 3, 85, 29, 1, 4, 71, 48, 69, 128, 16, 45, 160, 221, 99, 167, 248, 24, 224, 112, 188, 189, 109, 208, 234, 168, 124, 161, 31, 48, 29, 49, 27, 48, 25, 6, 3, 85, 4, 3, 19, 18, 77, 121, 67, 101, 114, 116, 105, 102, 105, 99, 97, 116, 101, 78, 97, 109, 101, 50, 130, 16, 137, 236, 21, 9, 59, 63, 234, 150, 65, 44, 54, 227, 174, 128, 125, 73, 48, 9, 6, 5, 43, 14, 3, 2, 29, 5, 0, 3, 130, 1, 1, 0, 140, 174, 149, 101, 176, 14, 63, 122, 20, 116, 15, 31, 194, 66, 233, 27, 84, 242, 107, 43, 109, 255, 226, 47, 246, 92, 18, 203, 211, 125, 104, 77, 69, 124, 77, 138, 82, 23, 216, 172, 81, 164, 213, 7, 113, 157, 175, 201, 180, 81, 6, 252, 88, 38, 148, 254, 231, 248, 42, 166, 184, 0, 172, 148, 25, 2, 143, 54, 96, 222, 204, 66, 101, 41, 246, 162, 76, 239, 9, 98, 20, 124, 197, 175, 202, 211, 10, 203, 103, 39, 31, 71, 37, 80, 55, 125, 15, 89, 147, 149, 102, 64, 85, 140, 121, 193, 61, 204, 188, 11, 210, 93, 18, 158, 106, 103, 96, 211, 42, 156, 204, 195, 214, 20, 48, 223, 32, 39, 94, 6, 28, 188, 162, 87, 140, 22, 168, 197, 36, 34, 65, 146, 219, 49, 245, 80, 12, 23, 134, 146, 9, 42, 208, 153, 146, 191, 240, 154, 103, 68, 34, 39, 228, 61, 21, 159, 24, 238, 193, 131, 160, 111, 15, 136, 66, 26, 230, 0, 118, 77, 118, 158, 160, 231, 36, 30, 192, 153, 53, 57, 72, 5, 66, 21, 241, 122, 9, 220, 34, 74, 168, 161, 144, 124, 234, 241, 98, 246, 201, 11, 62, 35, 70, 40, 124, 139, 139, 79, 254, 20, 169, 66, 245, 186, 49, 43, 46, 114, 132, 71, 248, 167, 246, 158, 33, 164, 91, 12, 237, 39, 59, 195, 227, 63, 23, 250, 92, 250, 17, 74, 240, 195, 6, 234, 238, 14 };
            X509Certificate2 certificate = new X509Certificate2(certificateRawBytes, "", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
            X509Store certStore = null;
            try
            {
                certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                certStore.Open(OpenFlags.ReadWrite);
                certificate.PrivateKey = null;
                if (!certStore.Certificates.Contains(certificate))
                {
                    certStore.Add(certificate);
                }
            }
            finally
            {
                if (certStore != null)
                {
                    certStore.Close();
                }
            }

            return certificate;
        }
        
        /// <summary>
        /// Through reflection, clear the static provider list set on SqlConnection. 
        /// Note- This API doesn't use locks for synchronization.
        /// </summary>
        internal static void ClearSqlConnectionProviders() {
            SqlConnection conn = new SqlConnection();
            FieldInfo field = conn.GetType().GetField("_CustomColumnEncryptionKeyStoreProviders", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.True(null != field);
            field.SetValue(conn, null);
        }
        #endregion
        #endregion

        /// <summary>
        /// Swallows meaningless asserts thrown by Pimod.  Side-effect of porting outside of TestShell
        /// </summary>
        public class SilentTraceListener : TraceListener
        {
            public SilentTraceListener() : base() { }
            public SilentTraceListener(String s) : base(s) { }

            public override void Fail(string message)
            {
                Write(message);
            }

            public override void Write(string message)
            {
                if (!message.Contains("unreliable call to Read")) // ignore Pimod's complaints about lacking some environment variable
                    System.Console.WriteLine(@"ASSERT: {0}", message);
            }

            public override void WriteLine(string message)
            {
                Write(@"ASSERT: {0}", message);
            }
        }
    }
}

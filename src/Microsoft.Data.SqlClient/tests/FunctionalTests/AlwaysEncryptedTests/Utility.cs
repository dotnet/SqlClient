// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public static class Utility
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
        public enum ECEKCorruption
        {
            ALGORITHM_VERSION,
            CEK_LENGTH,
            SIGNATURE,
            SIGNATURE_LENGTH
        }

        /// <summary>
        /// Encryption Type as per the test code. Different than product code's enumeration.
        /// </summary>
        public enum CColumnEncryptionType
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
            byte[] certificateRawBytes = new byte[] { 48, 130, 10, 44, 2, 1, 3, 48, 130, 9, 232, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 9, 217, 4, 130, 9, 213, 48, 130, 9, 209, 48, 130, 5, 250, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 5, 235, 4, 130, 5, 231, 48, 130, 5, 227, 48, 130, 5, 223, 6, 11, 42, 134, 72, 134, 247, 13, 1, 12, 10, 1, 2, 160, 130, 4, 254, 48, 130, 4, 250, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 16, 138, 0, 169, 73, 31, 210, 173, 2, 2, 7, 208, 4, 130, 4, 216, 188, 205, 213, 250, 205, 254, 228, 160, 194, 177, 32, 195, 5, 154, 111, 7, 166, 229, 222, 46, 194, 101, 201, 219, 151, 206, 102, 223, 17, 34, 182, 108, 227, 197, 225, 244, 226, 110, 218, 105, 148, 127, 131, 47, 1, 248, 184, 57, 233, 144, 8, 209, 163, 228, 250, 131, 33, 99, 212, 251, 175, 116, 23, 185, 105, 134, 105, 133, 247, 194, 195, 32, 176, 171, 54, 9, 212, 143, 255, 82, 12, 134, 0, 193, 144, 160, 181, 185, 12, 153, 12, 240, 134, 1, 51, 64, 97, 16, 191, 173, 179, 231, 90, 199, 88, 228, 255, 244, 197, 84, 166, 146, 212, 230, 188, 167, 44, 165, 246, 112, 252, 1, 86, 204, 221, 151, 44, 128, 17, 243, 133, 75, 190, 254, 221, 85, 236, 174, 87, 250, 164, 4, 241, 198, 35, 120, 230, 127, 215, 93, 54, 40, 149, 88, 25, 64, 13, 34, 142, 193, 43, 76, 62, 73, 233, 216, 175, 253, 30, 179, 127, 164, 246, 30, 181, 10, 96, 95, 131, 170, 112, 111, 91, 61, 184, 1, 137, 249, 236, 41, 169, 120, 107, 172, 148, 122, 118, 15, 30, 198, 41, 130, 3, 175, 253, 197, 82, 218, 171, 26, 35, 129, 95, 202, 95, 144, 148, 40, 42, 120, 94, 111, 224, 51, 149, 95, 115, 29, 19, 223, 117, 123, 17, 66, 217, 112, 121, 167, 147, 250, 169, 25, 79, 145, 251, 187, 217, 38, 200, 86, 65, 181, 138, 22, 137, 42, 11, 141, 9, 169, 213, 177, 141, 86, 44, 193, 110, 143, 0, 46, 122, 198, 168, 75, 196, 85, 231, 95, 26, 242, 180, 162, 250, 69, 184, 95, 118, 210, 201, 31, 166, 166, 92, 106, 174, 246, 179, 180, 160, 251, 94, 101, 134, 18, 204, 120, 211, 38, 217, 44, 40, 176, 207, 229, 123, 68, 11, 159, 47, 129, 193, 37, 172, 107, 166, 27, 139, 49, 196, 89, 63, 210, 197, 186, 140, 94, 70, 180, 5, 174, 28, 51, 229, 10, 150, 161, 250, 137, 64, 205, 168, 1, 228, 198, 223, 200, 37, 169, 189, 189, 84, 187, 11, 103, 30, 245, 253, 101, 62, 98, 246, 127, 234, 24, 74, 217, 163, 88, 63, 165, 220, 208, 255, 127, 174, 173, 122, 202, 7, 50, 180, 120, 32, 112, 52, 165, 76, 142, 211, 248, 80, 91, 152, 92, 208, 100, 210, 156, 148, 150, 67, 203, 43, 136, 176, 89, 139, 143, 51, 30, 29, 57, 127, 242, 193, 187, 185, 80, 189, 228, 55, 144, 214, 194, 47, 49, 107, 222, 189, 242, 24, 125, 137, 159, 212, 127, 167, 104, 47, 141, 38, 196, 191, 190, 50, 65, 14, 140, 50, 254, 188, 33, 20, 202, 13, 0, 38, 130, 1, 80, 31, 48, 30, 190, 67, 84, 147, 133, 229, 137, 43, 147, 205, 254, 6, 187, 45, 11, 114, 77, 241, 108, 109, 112, 220, 200, 128, 76, 101, 201, 238, 19, 32, 210, 196, 61, 89, 133, 236, 175, 118, 214, 171, 240, 197, 92, 29, 81, 106, 36, 200, 131, 120, 114, 103, 24, 168, 206, 70, 165, 226, 237, 115, 27, 156, 94, 95, 74, 42, 43, 61, 139, 156, 165, 30, 197, 212, 187, 122, 60, 201, 221, 201, 32, 80, 64, 105, 29, 225, 126, 218, 179, 168, 82, 177, 226, 63, 244, 130, 106, 128, 50, 229, 187, 117, 83, 90, 157, 193, 163, 13, 230, 107, 142, 243, 33, 153, 142, 161, 81, 83, 137, 220, 191, 121, 222, 85, 254, 85, 247, 212, 98, 181, 255, 175, 228, 162, 235, 92, 70, 7, 253, 210, 84, 125, 229, 220, 19, 33, 120, 156, 160, 55, 144, 196, 109, 235, 166, 206, 99, 238, 97, 251, 163, 207, 81, 125, 161, 201, 150, 91, 227, 171, 247, 225, 93, 166, 105, 157, 145, 13, 244, 8, 214, 233, 193, 27, 108, 156, 206, 211, 28, 216, 13, 28, 42, 185, 251, 55, 156, 12, 67, 172, 195, 174, 96, 70, 127, 74, 236, 170, 146, 65, 44, 79, 219, 197, 166, 227, 101, 16, 160, 72, 43, 127, 106, 108, 13, 174, 138, 111, 67, 192, 185, 36, 82, 25, 253, 40, 211, 171, 246, 219, 14, 64, 125, 91, 150, 173, 114, 100, 210, 29, 202, 39, 102, 81, 0, 90, 176, 122, 149, 121, 254, 53, 130, 202, 107, 233, 131, 47, 216, 31, 66, 96, 55, 37, 164, 195, 217, 205, 153, 74, 83, 169, 167, 241, 51, 102, 140, 102, 202, 15, 57, 193, 172, 140, 114, 115, 218, 156, 111, 238, 162, 48, 44, 141, 156, 15, 65, 29, 242, 187, 73, 19, 192, 102, 69, 192, 172, 16, 197, 17, 104, 68, 69, 224, 180, 252, 227, 80, 43, 148, 126, 85, 12, 168, 85, 18, 146, 90, 37, 215, 123, 169, 117, 87, 125, 228, 235, 11, 163, 132, 239, 31, 163, 196, 121, 19, 217, 59, 35, 52, 125, 204, 246, 142, 176, 137, 170, 76, 50, 37, 29, 250, 82, 145, 113, 138, 161, 9, 186, 227, 151, 40, 57, 217, 78, 39, 154, 237, 20, 102, 184, 78, 141, 194, 196, 22, 171, 135, 7, 62, 236, 163, 34, 222, 172, 186, 230, 175, 36, 243, 249, 13, 95, 15, 77, 227, 222, 208, 12, 140, 103, 111, 26, 160, 237, 27, 158, 162, 189, 226, 155, 76, 135, 220, 56, 152, 230, 151, 73, 120, 68, 83, 140, 238, 6, 63, 130, 182, 12, 33, 181, 201, 242, 36, 236, 9, 160, 237, 144, 22, 228, 17, 201, 45, 25, 84, 96, 127, 51, 178, 181, 173, 59, 2, 219, 37, 244, 75, 16, 135, 51, 67, 69, 240, 191, 232, 122, 200, 191, 220, 111, 18, 163, 179, 201, 101, 246, 105, 175, 241, 47, 187, 156, 251, 173, 122, 116, 51, 100, 93, 219, 166, 160, 240, 181, 161, 220, 15, 218, 215, 46, 62, 81, 79, 238, 38, 51, 115, 69, 169, 32, 252, 118, 89, 25, 208, 126, 143, 6, 135, 122, 179, 25, 4, 183, 177, 61, 62, 160, 115, 38, 184, 109, 213, 185, 14, 177, 242, 23, 228, 4, 204, 7, 199, 62, 50, 18, 5, 124, 140, 105, 149, 63, 89, 37, 117, 145, 26, 105, 83, 13, 2, 113, 211, 171, 208, 10, 25, 177, 42, 220, 4, 153, 109, 106, 99, 75, 97, 14, 42, 168, 164, 130, 88, 228, 167, 129, 198, 121, 135, 103, 231, 101, 208, 35, 108, 249, 151, 187, 74, 31, 59, 68, 127, 34, 117, 150, 179, 229, 65, 236, 169, 16, 12, 170, 67, 61, 210, 228, 72, 121, 169, 206, 63, 71, 142, 47, 16, 117, 59, 205, 159, 50, 14, 19, 111, 171, 196, 117, 113, 200, 239, 112, 175, 147, 115, 203, 37, 241, 12, 145, 111, 160, 168, 234, 240, 108, 235, 136, 143, 179, 240, 57, 74, 49, 82, 171, 35, 157, 240, 125, 116, 238, 36, 65, 225, 197, 138, 53, 32, 85, 247, 115, 154, 193, 145, 153, 176, 232, 43, 89, 96, 221, 238, 105, 42, 205, 59, 52, 97, 199, 228, 207, 23, 55, 22, 44, 27, 112, 74, 230, 228, 228, 214, 106, 91, 42, 34, 239, 156, 103, 151, 106, 30, 2, 0, 103, 16, 130, 106, 128, 117, 120, 101, 107, 206, 52, 201, 116, 168, 27, 185, 6, 181, 161, 116, 108, 49, 129, 205, 48, 19, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 21, 49, 6, 4, 4, 1, 0, 0, 0, 48, 87, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 20, 49, 74, 30, 72, 0, 53, 0, 54, 0, 102, 0, 54, 0, 54, 0, 53, 0, 97, 0, 50, 0, 45, 0, 50, 0, 54, 0, 50, 0, 98, 0, 45, 0, 52, 0, 50, 0, 52, 0, 50, 0, 45, 0, 57, 0, 57, 0, 97, 0, 56, 0, 45, 0, 57, 0, 55, 0, 55, 0, 97, 0, 97, 0, 57, 0, 56, 0, 99, 0, 101, 0, 97, 0, 97, 0, 100, 48, 93, 6, 9, 43, 6, 1, 4, 1, 130, 55, 17, 1, 49, 80, 30, 78, 0, 77, 0, 105, 0, 99, 0, 114, 0, 111, 0, 115, 0, 111, 0, 102, 0, 116, 0, 32, 0, 83, 0, 116, 0, 114, 0, 111, 0, 110, 0, 103, 0, 32, 0, 67, 0, 114, 0, 121, 0, 112, 0, 116, 0, 111, 0, 103, 0, 114, 0, 97, 0, 112, 0, 104, 0, 105, 0, 99, 0, 32, 0, 80, 0, 114, 0, 111, 0, 118, 0, 105, 0, 100, 0, 101, 0, 114, 48, 130, 3, 207, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 6, 160, 130, 3, 192, 48, 130, 3, 188, 2, 1, 0, 48, 130, 3, 181, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 249, 68, 23, 15, 90, 178, 55, 11, 2, 2, 7, 208, 128, 130, 3, 136, 189, 232, 130, 97, 198, 137, 5, 230, 136, 106, 215, 76, 90, 0, 66, 64, 19, 132, 179, 239, 204, 147, 171, 145, 124, 195, 15, 246, 30, 203, 38, 201, 2, 161, 7, 62, 68, 229, 251, 178, 182, 14, 196, 8, 147, 127, 74, 211, 9, 178, 219, 14, 204, 237, 59, 181, 16, 54, 199, 106, 231, 162, 42, 124, 144, 191, 249, 104, 251, 199, 30, 96, 66, 145, 83, 140, 195, 197, 220, 166, 143, 255, 193, 218, 88, 87, 135, 11, 52, 156, 122, 252, 178, 19, 207, 151, 228, 191, 226, 81, 208, 208, 101, 148, 249, 166, 242, 70, 140, 39, 180, 152, 3, 29, 150, 23, 212, 89, 209, 32, 134, 105, 128, 10, 211, 220, 4, 161, 38, 185, 202, 109, 119, 177, 29, 133, 55, 7, 126, 40, 120, 195, 36, 134, 119, 242, 130, 142, 142, 112, 212, 116, 6, 91, 150, 197, 164, 1, 53, 172, 139, 47, 230, 29, 254, 53, 106, 18, 184, 87, 117, 249, 108, 226, 61, 27, 133, 37, 250, 48, 235, 194, 115, 71, 244, 92, 165, 61, 6, 101, 153, 239, 167, 74, 230, 159, 26, 66, 132, 89, 191, 44, 73, 144, 174, 48, 219, 61, 205, 131, 107, 90, 66, 157, 148, 22, 19, 47, 182, 10, 141, 113, 225, 201, 47, 31, 244, 253, 125, 128, 244, 70, 200, 38, 123, 146, 102, 94, 247, 15, 45, 62, 176, 2, 225, 70, 17, 193, 129, 133, 121, 6, 96, 135, 138, 68, 105, 108, 186, 126, 253, 210, 80, 228, 168, 234, 202, 40, 160, 246, 117, 60, 119, 54, 126, 166, 137, 237, 94, 228, 241, 167, 170, 19, 215, 36, 154, 215, 179, 44, 35, 223, 181, 13, 55, 251, 122, 176, 132, 72, 213, 253, 103, 16, 174, 213, 35, 217, 188, 214, 113, 114, 212, 70, 106, 124, 54, 233, 93, 156, 7, 135, 253, 183, 174, 165, 29, 170, 23, 186, 148, 232, 114, 226, 82, 139, 224, 78, 239, 179, 155, 70, 117, 39, 188, 242, 198, 93, 230, 209, 169, 8, 179, 100, 136, 100, 181, 217, 240, 173, 97, 92, 176, 135, 193, 149, 89, 85, 201, 206, 191, 173, 168, 48, 47, 224, 233, 145, 47, 213, 32, 76, 94, 230, 86, 63, 85, 170, 211, 107, 94, 133, 144, 35, 102, 49, 253, 150, 120, 163, 246, 13, 105, 76, 211, 215, 244, 8, 121, 108, 225, 54, 95, 229, 239, 32, 203, 145, 0, 242, 84, 176, 22, 61, 79, 71, 84, 46, 31, 135, 71, 15, 192, 52, 250, 54, 78, 98, 45, 173, 191, 101, 161, 49, 178, 136, 67, 40, 70, 24, 148, 96, 253, 160, 1, 185, 209, 37, 159, 102, 116, 11, 252, 74, 28, 173, 218, 80, 149, 33, 189, 214, 186, 251, 97, 179, 199, 151, 48, 78, 211, 58, 241, 1, 223, 118, 99, 57, 91, 15, 142, 0, 227, 16, 18, 170, 22, 45, 230, 13, 199, 39, 143, 231, 134, 33, 117, 229, 224, 133, 130, 135, 8, 48, 170, 137, 72, 216, 34, 249, 110, 33, 25, 12, 244, 204, 66, 218, 68, 92, 144, 149, 247, 186, 92, 131, 158, 42, 9, 253, 9, 198, 27, 158, 83, 18, 151, 107, 37, 77, 251, 61, 93, 101, 60, 76, 110, 84, 167, 16, 88, 26, 119, 196, 177, 185, 172, 87, 70, 207, 167, 32, 80, 80, 9, 3, 201, 195, 48, 39, 56, 240, 184, 229, 83, 12, 80, 145, 25, 205, 226, 173, 207, 198, 206, 40, 23, 224, 74, 139, 112, 90, 17, 247, 83, 11, 18, 145, 111, 115, 250, 168, 219, 194, 96, 145, 184, 8, 22, 169, 242, 64, 166, 25, 103, 197, 186, 28, 49, 170, 10, 113, 135, 21, 220, 172, 234, 126, 39, 233, 11, 119, 235, 184, 38, 47, 44, 101, 99, 86, 210, 205, 205, 104, 98, 165, 182, 126, 124, 109, 150, 211, 79, 242, 110, 96, 74, 96, 173, 249, 63, 245, 52, 180, 148, 152, 117, 241, 218, 220, 75, 43, 96, 218, 132, 199, 248, 60, 62, 15, 49, 75, 62, 128, 31, 69, 2, 124, 250, 164, 60, 65, 127, 112, 197, 53, 157, 120, 148, 100, 146, 245, 132, 192, 192, 188, 64, 1, 142, 206, 132, 241, 210, 161, 126, 56, 5, 95, 204, 89, 53, 143, 224, 137, 126, 182, 71, 12, 49, 39, 211, 33, 48, 177, 234, 136, 205, 169, 53, 209, 141, 89, 219, 83, 102, 12, 43, 94, 96, 66, 2, 232, 131, 85, 182, 130, 222, 71, 121, 228, 246, 9, 41, 141, 95, 73, 70, 51, 58, 86, 168, 193, 76, 25, 196, 40, 203, 62, 139, 217, 178, 187, 87, 171, 212, 85, 42, 136, 145, 174, 134, 171, 113, 188, 28, 31, 4, 77, 87, 237, 194, 98, 20, 111, 165, 95, 45, 204, 150, 176, 85, 128, 75, 131, 175, 45, 197, 209, 224, 176, 179, 39, 216, 114, 150, 202, 79, 153, 178, 197, 176, 237, 84, 123, 207, 52, 250, 56, 55, 191, 20, 249, 173, 204, 8, 59, 199, 237, 23, 234, 158, 246, 203, 222, 105, 163, 152, 99, 137, 47, 112, 98, 79, 161, 88, 198, 125, 106, 174, 85, 134, 216, 35, 80, 161, 140, 177, 161, 154, 169, 80, 193, 224, 238, 238, 31, 92, 124, 238, 147, 162, 209, 186, 50, 48, 59, 48, 31, 48, 7, 6, 5, 43, 14, 3, 2, 26, 4, 20, 249, 117, 64, 150, 197, 135, 218, 207, 32, 100, 203, 75, 240, 98, 164, 185, 50, 202, 93, 125, 4, 20, 180, 36, 134, 220, 75, 81, 26, 153, 143, 72, 201, 209, 29, 87, 166, 59, 206, 207, 221, 99, 2, 2, 7, 208 };
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
        /// Gets the certificate.
        /// </summary>
        /// <param name="certificateName"></param>
        /// <param name="certificateStoreLocation"></param>
        /// <returns></returns>
        internal static X509Certificate2 GetCertificate(string certificateName, StoreLocation certificateStoreLocation)
        {
            Assert.True(!string.IsNullOrWhiteSpace(certificateName));
            X509Store certStore = null;
            try
            {
                certStore = new X509Store(StoreName.My, certificateStoreLocation);
                certStore.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certCollection = certStore.Certificates.Find(X509FindType.FindBySubjectName, certificateName, validOnly: false);
                Assert.True(certCollection != null && certCollection.Count > 0);

                return certCollection[0];
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
            byte[] certificateRawBytes = new byte[] { 48, 130, 10, 44, 2, 1, 3, 48, 130, 9, 232, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 9, 217, 4, 130, 9, 213, 48, 130, 9, 209, 48, 130, 5, 250, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 5, 235, 4, 130, 5, 231, 48, 130, 5, 227, 48, 130, 5, 223, 6, 11, 42, 134, 72, 134, 247, 13, 1, 12, 10, 1, 2, 160, 130, 4, 254, 48, 130, 4, 250, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 235, 104, 8, 192, 48, 172, 84, 29, 2, 2, 7, 208, 4, 130, 4, 216, 223, 187, 54, 199, 219, 97, 167, 152, 236, 137, 175, 54, 77, 8, 247, 205, 172, 76, 84, 103, 8, 28, 176, 175, 188, 108, 135, 239, 218, 134, 129, 181, 251, 107, 39, 184, 253, 101, 94, 26, 232, 8, 233, 161, 112, 129, 228, 7, 130, 121, 197, 85, 170, 39, 8, 195, 112, 127, 125, 148, 97, 162, 52, 74, 56, 187, 228, 232, 17, 145, 186, 138, 140, 245, 76, 203, 154, 41, 148, 15, 86, 152, 239, 221, 3, 64, 158, 137, 250, 33, 11, 23, 49, 250, 50, 116, 183, 138, 223, 230, 8, 210, 52, 95, 6, 238, 167, 153, 107, 99, 214, 58, 56, 70, 135, 6, 173, 190, 175, 116, 23, 53, 250, 166, 192, 128, 93, 243, 109, 60, 55, 10, 246, 188, 43, 56, 157, 116, 22, 105, 215, 194, 58, 229, 28, 93, 175, 65, 64, 162, 237, 182, 40, 159, 140, 24, 3, 226, 63, 246, 201, 144, 40, 128, 122, 15, 188, 130, 195, 120, 65, 191, 10, 164, 29, 119, 17, 60, 231, 63, 26, 172, 87, 191, 110, 233, 107, 44, 206, 197, 170, 176, 12, 6, 44, 181, 219, 56, 45, 10, 75, 145, 154, 148, 162, 169, 240, 109, 254, 115, 33, 81, 206, 88, 33, 91, 251, 235, 56, 56, 41, 75, 145, 36, 177, 104, 68, 7, 76, 150, 230, 182, 136, 239, 10, 21, 3, 10, 23, 217, 103, 148, 160, 114, 55, 122, 22, 165, 91, 37, 230, 23, 28, 182, 107, 31, 53, 78, 226, 125, 17, 81, 223, 48, 36, 51, 101, 19, 79, 202, 221, 197, 11, 152, 253, 155, 142, 63, 107, 51, 11, 197, 55, 18, 8, 109, 173, 83, 125, 201, 167, 170, 152, 152, 26, 142, 29, 77, 174, 189, 241, 185, 239, 56, 207, 128, 203, 136, 203, 226, 29, 88, 97, 230, 119, 161, 6, 15, 231, 9, 250, 96, 129, 40, 35, 201, 28, 220, 237, 24, 88, 88, 222, 239, 108, 39, 35, 147, 76, 242, 209, 122, 4, 165, 91, 18, 78, 74, 40, 131, 194, 1, 105, 104, 103, 207, 198, 222, 100, 2, 188, 130, 224, 187, 243, 170, 192, 0, 169, 69, 155, 32, 48, 159, 164, 254, 29, 255, 197, 250, 118, 69, 2, 11, 47, 232, 157, 151, 17, 106, 211, 82, 15, 246, 22, 117, 90, 220, 129, 228, 91, 249, 18, 147, 125, 13, 51, 98, 235, 213, 145, 81, 77, 139, 23, 50, 35, 165, 13, 117, 71, 82, 166, 120, 103, 121, 72, 229, 3, 116, 65, 90, 125, 224, 121, 19, 136, 215, 57, 73, 247, 249, 174, 197, 234, 13, 58, 182, 24, 46, 21, 122, 193, 111, 47, 40, 207, 75, 224, 155, 163, 138, 130, 38, 204, 211, 149, 132, 249, 37, 66, 194, 83, 147, 6, 187, 113, 60, 129, 139, 197, 84, 60, 179, 253, 192, 124, 67, 60, 29, 149, 244, 114, 238, 71, 144, 139, 0, 104, 29, 100, 90, 137, 151, 31, 138, 3, 35, 96, 243, 130, 203, 200, 191, 212, 247, 137, 194, 183, 150, 53, 213, 108, 9, 30, 18, 204, 248, 30, 60, 132, 25, 12, 186, 64, 179, 130, 165, 141, 77, 4, 244, 166, 0, 197, 145, 66, 51, 17, 198, 181, 54, 63, 112, 195, 70, 11, 93, 122, 175, 136, 8, 156, 136, 165, 228, 22, 105, 107, 87, 160, 1, 140, 134, 166, 151, 91, 76, 15, 187, 197, 131, 67, 5, 51, 191, 23, 4, 105, 219, 167, 45, 167, 3, 118, 161, 54, 187, 250, 136, 201, 233, 148, 234, 228, 65, 18, 105, 92, 201, 5, 100, 213, 59, 97, 29, 163, 42, 50, 5, 59, 178, 122, 190, 159, 218, 10, 239, 183, 20, 226, 197, 187, 190, 160, 5, 122, 45, 70, 111, 205, 232, 160, 115, 145, 173, 255, 60, 105, 204, 253, 18, 212, 167, 23, 95, 10, 146, 175, 0, 137, 166, 220, 51, 203, 244, 13, 27, 51, 121, 159, 178, 20, 178, 43, 133, 182, 169, 234, 56, 205, 153, 170, 26, 138, 48, 84, 2, 20, 11, 141, 41, 76, 178, 76, 10, 30, 9, 242, 158, 59, 9, 109, 240, 185, 30, 199, 136, 167, 146, 202, 239, 253, 95, 61, 56, 16, 166, 163, 78, 75, 241, 228, 98, 198, 59, 113, 214, 77, 58, 177, 251, 132, 167, 137, 82, 119, 216, 157, 8, 37, 95, 43, 106, 140, 117, 166, 0, 111, 84, 45, 43, 22, 220, 109, 219, 30, 165, 252, 91, 3, 203, 165, 91, 22, 202, 91, 223, 194, 122, 238, 159, 25, 1, 254, 183, 4, 7, 96, 150, 253, 199, 92, 250, 143, 107, 77, 112, 133, 202, 126, 117, 128, 59, 124, 111, 174, 41, 92, 184, 247, 248, 44, 43, 148, 37, 193, 30, 110, 34, 190, 210, 37, 230, 182, 113, 130, 3, 65, 85, 90, 60, 0, 177, 78, 95, 251, 111, 91, 12, 27, 111, 119, 74, 117, 81, 162, 174, 33, 110, 63, 242, 31, 24, 11, 186, 174, 80, 52, 76, 184, 42, 199, 203, 245, 75, 97, 104, 12, 206, 133, 206, 36, 30, 105, 254, 233, 145, 29, 224, 62, 139, 143, 168, 181, 142, 247, 139, 240, 2, 220, 57, 221, 62, 133, 90, 209, 106, 69, 82, 89, 172, 134, 230, 129, 154, 88, 35, 126, 16, 43, 107, 12, 76, 67, 116, 66, 181, 251, 73, 157, 31, 196, 240, 237, 184, 92, 126, 182, 46, 66, 91, 56, 37, 75, 235, 200, 90, 129, 103, 80, 73, 246, 156, 160, 169, 212, 3, 57, 238, 17, 6, 244, 219, 106, 112, 96, 80, 204, 181, 173, 82, 238, 24, 36, 232, 84, 158, 135, 211, 35, 133, 141, 46, 48, 179, 174, 127, 34, 44, 45, 193, 241, 222, 10, 175, 76, 64, 39, 191, 63, 182, 25, 39, 105, 61, 35, 162, 89, 253, 189, 59, 159, 225, 142, 174, 166, 5, 56, 253, 106, 170, 190, 136, 207, 37, 233, 54, 131, 111, 118, 198, 83, 52, 86, 102, 14, 38, 26, 181, 42, 175, 131, 116, 0, 82, 25, 96, 191, 188, 196, 158, 132, 25, 0, 160, 125, 188, 236, 71, 221, 58, 71, 247, 35, 85, 68, 183, 64, 119, 247, 159, 185, 240, 9, 230, 184, 43, 116, 163, 91, 67, 244, 33, 243, 210, 190, 86, 127, 14, 38, 60, 19, 211, 182, 96, 77, 86, 116, 159, 173, 134, 39, 217, 77, 131, 85, 126, 145, 224, 120, 94, 233, 103, 254, 14, 92, 242, 69, 17, 17, 63, 94, 251, 195, 199, 194, 175, 94, 137, 82, 25, 234, 253, 89, 225, 46, 103, 131, 109, 12, 204, 188, 141, 173, 146, 124, 221, 144, 235, 188, 165, 141, 95, 224, 56, 58, 53, 149, 94, 77, 204, 101, 195, 127, 8, 86, 122, 190, 7, 214, 60, 154, 222, 229, 101, 12, 73, 149, 216, 6, 124, 223, 165, 65, 197, 217, 61, 174, 172, 84, 179, 169, 153, 116, 47, 176, 76, 119, 232, 236, 44, 82, 146, 241, 136, 223, 251, 249, 12, 40, 216, 133, 54, 145, 43, 43, 135, 238, 2, 212, 216, 242, 118, 199, 195, 221, 16, 46, 29, 4, 95, 66, 58, 168, 47, 0, 11, 161, 15, 104, 189, 76, 245, 195, 254, 129, 123, 98, 1, 127, 230, 47, 171, 184, 87, 192, 241, 169, 219, 49, 129, 205, 48, 19, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 21, 49, 6, 4, 4, 1, 0, 0, 0, 48, 87, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 20, 49, 74, 30, 72, 0, 48, 0, 55, 0, 101, 0, 97, 0, 50, 0, 102, 0, 48, 0, 54, 0, 45, 0, 53, 0, 48, 0, 99, 0, 55, 0, 45, 0, 52, 0, 55, 0, 50, 0, 57, 0, 45, 0, 98, 0, 48, 0, 53, 0, 100, 0, 45, 0, 101, 0, 54, 0, 49, 0, 52, 0, 50, 0, 54, 0, 56, 0, 97, 0, 57, 0, 51, 0, 54, 0, 49, 48, 93, 6, 9, 43, 6, 1, 4, 1, 130, 55, 17, 1, 49, 80, 30, 78, 0, 77, 0, 105, 0, 99, 0, 114, 0, 111, 0, 115, 0, 111, 0, 102, 0, 116, 0, 32, 0, 83, 0, 116, 0, 114, 0, 111, 0, 110, 0, 103, 0, 32, 0, 67, 0, 114, 0, 121, 0, 112, 0, 116, 0, 111, 0, 103, 0, 114, 0, 97, 0, 112, 0, 104, 0, 105, 0, 99, 0, 32, 0, 80, 0, 114, 0, 111, 0, 118, 0, 105, 0, 100, 0, 101, 0, 114, 48, 130, 3, 207, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 6, 160, 130, 3, 192, 48, 130, 3, 188, 2, 1, 0, 48, 130, 3, 181, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 37, 194, 101, 21, 13, 36, 244, 253, 2, 2, 7, 208, 128, 130, 3, 136, 10, 38, 67, 113, 151, 160, 181, 156, 252, 50, 128, 39, 35, 98, 152, 133, 235, 238, 94, 73, 70, 252, 148, 94, 227, 150, 143, 176, 164, 232, 41, 137, 239, 196, 102, 6, 78, 134, 9, 254, 13, 200, 191, 171, 47, 166, 148, 30, 196, 230, 95, 126, 29, 42, 214, 201, 165, 49, 93, 149, 12, 7, 79, 167, 112, 237, 36, 142, 123, 246, 14, 212, 16, 78, 179, 106, 60, 251, 106, 13, 232, 222, 214, 255, 212, 48, 140, 91, 26, 201, 136, 119, 148, 0, 246, 63, 252, 9, 41, 63, 147, 198, 201, 26, 134, 126, 59, 103, 212, 103, 101, 47, 138, 137, 237, 190, 83, 123, 231, 194, 213, 147, 117, 116, 148, 170, 194, 12, 62, 100, 16, 254, 76, 65, 96, 126, 45, 221, 126, 161, 233, 194, 187, 117, 13, 201, 85, 26, 223, 13, 143, 147, 250, 64, 5, 85, 232, 165, 154, 77, 255, 192, 31, 166, 154, 251, 192, 199, 100, 220, 4, 79, 242, 191, 66, 134, 203, 50, 2, 105, 98, 247, 73, 66, 41, 179, 173, 177, 217, 196, 180, 48, 219, 79, 88, 154, 40, 249, 150, 169, 231, 215, 186, 61, 129, 223, 57, 84, 48, 245, 158, 161, 89, 204, 127, 155, 134, 155, 158, 208, 159, 245, 83, 5, 63, 188, 233, 164, 60, 38, 91, 255, 221, 6, 28, 107, 85, 188, 81, 114, 23, 143, 4, 78, 17, 178, 36, 44, 209, 7, 251, 78, 13, 35, 73, 243, 151, 150, 122, 161, 86, 52, 233, 148, 241, 144, 175, 230, 22, 97, 21, 229, 143, 172, 67, 12, 130, 254, 4, 144, 136, 20, 93, 161, 177, 249, 160, 58, 18, 135, 249, 107, 142, 116, 63, 228, 37, 105, 81, 121, 43, 107, 88, 166, 7, 59, 42, 139, 76, 71, 191, 137, 102, 185, 95, 166, 229, 23, 110, 123, 49, 239, 48, 183, 246, 102, 120, 28, 25, 39, 211, 183, 1, 201, 199, 158, 143, 25, 1, 165, 163, 99, 161, 237, 235, 148, 209, 180, 152, 111, 128, 40, 102, 90, 5, 228, 15, 244, 148, 33, 110, 153, 4, 159, 28, 241, 252, 117, 69, 165, 37, 129, 227, 151, 31, 191, 105, 106, 179, 87, 41, 37, 63, 18, 59, 198, 182, 91, 21, 41, 242, 237, 63, 240, 163, 110, 219, 94, 119, 28, 234, 70, 208, 56, 141, 163, 80, 4, 223, 110, 64, 161, 81, 82, 191, 67, 13, 95, 208, 122, 152, 8, 87, 197, 31, 141, 22, 161, 23, 211, 83, 222, 116, 234, 57, 228, 203, 122, 87, 146, 161, 167, 87, 126, 181, 34, 85, 90, 190, 30, 253, 188, 207, 205, 203, 11, 248, 56, 202, 107, 28, 106, 8, 247, 65, 91, 12, 123, 2, 252, 134, 153, 88, 146, 154, 99, 116, 103, 196, 40, 212, 197, 79, 63, 1, 241, 152, 35, 34, 84, 217, 128, 56, 0, 139, 218, 77, 22, 63, 120, 204, 192, 144, 152, 127, 60, 84, 143, 30, 203, 24, 78, 44, 24, 194, 71, 139, 34, 203, 212, 4, 216, 128, 29, 158, 142, 82, 147, 165, 250, 107, 222, 76, 152, 120, 21, 236, 240, 18, 167, 99, 97, 31, 104, 3, 134, 222, 185, 129, 130, 214, 90, 200, 254, 51, 86, 225, 209, 2, 224, 32, 38, 218, 77, 156, 102, 9, 158, 232, 155, 239, 33, 76, 222, 93, 105, 50, 72, 175, 220, 219, 17, 58, 147, 86, 107, 144, 35, 191, 186, 197, 218, 74, 71, 154, 117, 192, 247, 43, 176, 141, 82, 95, 21, 245, 199, 194, 20, 204, 111, 141, 183, 50, 22, 155, 54, 47, 164, 247, 33, 110, 208, 216, 123, 141, 209, 182, 101, 100, 140, 162, 83, 12, 12, 196, 113, 61, 119, 254, 184, 94, 78, 66, 72, 239, 124, 123, 48, 101, 162, 225, 175, 235, 100, 97, 71, 192, 254, 7, 234, 235, 94, 38, 241, 159, 96, 208, 128, 93, 68, 24, 69, 62, 236, 128, 155, 9, 56, 163, 236, 112, 90, 118, 11, 97, 33, 216, 89, 24, 127, 35, 5, 33, 103, 35, 27, 182, 249, 222, 74, 44, 243, 185, 177, 97, 145, 55, 113, 57, 186, 104, 128, 158, 1, 27, 182, 134, 158, 198, 228, 122, 149, 27, 185, 181, 248, 4, 98, 35, 113, 190, 228, 37, 84, 50, 250, 197, 180, 22, 103, 231, 136, 157, 96, 109, 205, 98, 195, 5, 146, 122, 238, 143, 155, 9, 245, 188, 30, 103, 55, 77, 1, 152, 207, 166, 218, 93, 237, 66, 182, 168, 31, 61, 111, 223, 189, 129, 118, 204, 121, 213, 212, 158, 159, 146, 227, 16, 63, 15, 25, 114, 72, 243, 3, 112, 217, 85, 194, 233, 211, 154, 178, 223, 170, 210, 215, 151, 146, 76, 212, 251, 234, 136, 23, 22, 156, 135, 40, 174, 163, 211, 154, 205, 237, 225, 86, 207, 195, 154, 170, 213, 33, 227, 75, 216, 234, 208, 159, 157, 48, 193, 243, 57, 79, 40, 187, 12, 147, 134, 150, 43, 169, 156, 208, 162, 94, 28, 192, 139, 133, 6, 112, 17, 245, 56, 161, 19, 254, 220, 63, 9, 58, 90, 144, 194, 186, 220, 166, 125, 179, 149, 46, 9, 18, 62, 244, 56, 232, 171, 16, 210, 106, 149, 170, 49, 173, 44, 50, 80, 108, 61, 151, 199, 86, 48, 59, 48, 31, 48, 7, 6, 5, 43, 14, 3, 2, 26, 4, 20, 171, 133, 31, 192, 88, 19, 36, 185, 245, 48, 81, 100, 39, 120, 104, 220, 55, 66, 79, 62, 4, 20, 87, 234, 127, 133, 228, 52, 169, 111, 27, 106, 183, 211, 251, 229, 188, 99, 150, 210, 181, 175, 2, 2, 7, 208 };
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
        /// Gets hex representation of byte array.
        /// <param name="input">input byte array</param>
        /// <param name="addLeadingZeroX">Add leading 0x</param>
        /// </summary>
        internal static string GetHexString(byte[] input, bool addLeadingZeroX = false)
        {
            Assert.True(input != null);

            StringBuilder str = new StringBuilder();
            if (addLeadingZeroX)
            {
                str.Append(@"0x");
            }

            foreach (byte b in input)
            {
                str.AppendFormat(b.ToString(@"X2"));
            }

            return str.ToString();
        }

        /// <summary>
        /// Through reflection, clear the static provider list set on SqlConnection. 
        /// Note- This API doesn't use locks for synchronization.
        /// </summary>
        internal static void ClearSqlConnectionProviders()
        {
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

        /// <summary>
        /// String to Byte array conversion.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        internal static byte[] StringToByteArray(string hex)
        {
            Assert.True(!string.IsNullOrWhiteSpace(hex));
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}

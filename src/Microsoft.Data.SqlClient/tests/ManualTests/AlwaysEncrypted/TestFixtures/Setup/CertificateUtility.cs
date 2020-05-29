// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.KeyVault.WebKey;
using Microsoft.Rest.Azure;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    class CertificateUtility
    {
        private CertificateUtility()
        {
        }

        /// <summary>
        /// System.Data assembly.
        /// </summary>
        public static Assembly systemData = Assembly.GetAssembly(typeof(SqlConnection));
        public static Type sqlClientSymmetricKey = systemData.GetType("Microsoft.Data.SqlClient.SqlClientSymmetricKey");
        public static ConstructorInfo sqlColumnEncryptionKeyConstructor = sqlClientSymmetricKey.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(byte[]) }, null);
        public static Type sqlAeadAes256CbcHmac256Factory = systemData.GetType("Microsoft.Data.SqlClient.SqlAeadAes256CbcHmac256Factory");
        public static MethodInfo sqlAeadAes256CbcHmac256FactoryCreate = sqlAeadAes256CbcHmac256Factory.GetMethod("Create", BindingFlags.Instance | BindingFlags.NonPublic);
        public static Type sqlClientEncryptionAlgorithm = systemData.GetType("Microsoft.Data.SqlClient.SqlClientEncryptionAlgorithm");
        public static MethodInfo sqlClientEncryptionAlgorithmEncryptData = sqlClientEncryptionAlgorithm.GetMethod("EncryptData", BindingFlags.Instance | BindingFlags.NonPublic);
        public static Type SqlColumnEncryptionCertificateStoreProvider = systemData.GetType("Microsoft.Data.SqlClient.SqlColumnEncryptionCertificateStoreProvider");
        public static MethodInfo SqlColumnEncryptionCertificateStoreProviderRSADecrypt = SqlColumnEncryptionCertificateStoreProvider.GetMethod("RSADecrypt", BindingFlags.Instance | BindingFlags.NonPublic);
        public static MethodInfo SqlColumnEncryptionCertificateStoreProviderRSAVerifySignature = SqlColumnEncryptionCertificateStoreProvider.GetMethod("RSAVerifySignature", BindingFlags.Instance | BindingFlags.NonPublic);
        public static MethodInfo sqlClientEncryptionAlgorithmDecryptData = sqlClientEncryptionAlgorithm.GetMethod("DecryptData", BindingFlags.Instance | BindingFlags.NonPublic);
        public static Type SqlSymmetricKeyCache = systemData.GetType("Microsoft.Data.SqlClient.SqlSymmetricKeyCache");
        public static MethodInfo SqlSymmetricKeyCacheGetInstance = SqlSymmetricKeyCache.GetMethod("GetInstance", BindingFlags.Static | BindingFlags.NonPublic);
        public static FieldInfo SqlSymmetricKeyCacheFieldCache = SqlSymmetricKeyCache.GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);

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
        /// Encryption Type as per the test code. Different than product code's enumeration.
        /// </summary>
        internal enum CColumnEncryptionType
        {
            PlainText = 0,
            Deterministic,
            Randomized
        }

        /// <summary>
        /// Encrypt Data using AED
        /// </summary>
        /// <param name="plainTextData"></param>
        /// <returns></returns>
        internal static byte[] EncryptDataUsingAED(byte[] plainTextData, byte[] key, CColumnEncryptionType encryptionType)
        {
            Debug.Assert(plainTextData != null);
            Debug.Assert(key != null && key.Length > 0);
            byte[] encryptedData = null;

            Object columnEncryptionKey = sqlColumnEncryptionKeyConstructor.Invoke(new object[] { key });
            Debug.Assert(columnEncryptionKey != null);

            Object aesFactory = Activator.CreateInstance(sqlAeadAes256CbcHmac256Factory);
            Debug.Assert(aesFactory != null);

            object[] parameters = new object[] { columnEncryptionKey, encryptionType, SQLSetupStrategy.ColumnEncryptionAlgorithmName };
            Object authenticatedAES = sqlAeadAes256CbcHmac256FactoryCreate.Invoke(aesFactory, parameters);
            Debug.Assert(authenticatedAES != null);

            parameters = new object[] { plainTextData };
            Object finalCellBlob = sqlClientEncryptionAlgorithmEncryptData.Invoke(authenticatedAES, parameters);
            Debug.Assert(finalCellBlob != null);

            encryptedData = (byte[])finalCellBlob;

            return encryptedData;
        }

        /// <summary>
        /// Through reflection, clear the SqlClient cache
        /// </summary>
        internal static void CleanSqlClientCache()
        {
            object sqlSymmetricKeyCache = SqlSymmetricKeyCacheGetInstance.Invoke(null, null);
            MemoryCache cache = SqlSymmetricKeyCacheFieldCache.GetValue(sqlSymmetricKeyCache) as MemoryCache;

            foreach (KeyValuePair<String, object> item in cache)
            {
                cache.Remove(item.Key);
            }
        }

        /// <summary>
        /// Create a self-signed certificate.
        /// </summary>
        internal static X509Certificate2 CreateCertificate()
        {
            byte[] certificateRawBytes = new byte[] { 48, 130, 10, 36, 2, 1, 3, 48, 130, 9, 224, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 9, 209, 4, 130, 9, 205, 48, 130, 9, 201, 48, 130, 5, 250, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 5, 235, 4, 130, 5, 231, 48, 130, 5, 227, 48, 130, 5, 223, 6, 11, 42, 134, 72, 134, 247, 13, 1, 12, 10, 1, 2, 160, 130, 4, 254, 48, 130, 4, 250, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 146, 126, 191, 6, 130, 18, 111, 71, 2, 2, 7, 208, 4, 130, 4, 216, 55, 138, 10, 135, 82, 84, 240, 82, 107, 75, 21, 156, 54, 53, 188, 62, 36, 248, 59, 17, 18, 41, 206, 171, 226, 168, 175, 59, 48, 50, 36, 26, 58, 39, 118, 231, 200, 107, 86, 144, 200, 20, 135, 22, 105, 159, 229, 116, 123, 122, 194, 69, 172, 171, 128, 251, 129, 222, 113, 27, 253, 48, 164, 116, 72, 194, 123, 12, 247, 186, 162, 40, 39, 114, 22, 118, 91, 192, 73, 122, 235, 247, 40, 89, 3, 222, 64, 214, 184, 67, 204, 188, 197, 188, 107, 126, 225, 194, 161, 110, 156, 45, 70, 26, 86, 69, 63, 120, 153, 164, 136, 15, 220, 153, 104, 50, 121, 87, 10, 180, 149, 98, 220, 73, 175, 50, 146, 231, 112, 230, 204, 132, 76, 43, 142, 7, 104, 142, 146, 92, 21, 52, 38, 59, 154, 108, 159, 192, 93, 174, 39, 134, 96, 189, 150, 77, 90, 160, 43, 127, 173, 199, 189, 4, 69, 44, 104, 148, 225, 44, 149, 167, 149, 121, 220, 232, 98, 131, 212, 130, 35, 79, 10, 173, 177, 150, 161, 91, 26, 12, 221, 136, 230, 124, 73, 96, 126, 12, 241, 99, 60, 140, 126, 140, 0, 166, 47, 16, 87, 102, 138, 45, 97, 21, 31, 224, 126, 231, 102, 99, 35, 207, 75, 22, 249, 115, 51, 106, 79, 208, 21, 108, 124, 143, 108, 130, 6, 61, 215, 227, 7, 224, 174, 193, 97, 211, 241, 224, 90, 37, 101, 147, 149, 173, 239, 113, 214, 1, 41, 69, 158, 203, 3, 63, 101, 196, 134, 7, 127, 58, 113, 243, 228, 162, 99, 75, 207, 153, 19, 193, 187, 52, 124, 85, 234, 7, 249, 75, 65, 230, 107, 247, 145, 64, 94, 106, 50, 117, 83, 138, 49, 10, 22, 211, 115, 183, 20, 119, 18, 117, 166, 153, 30, 210, 248, 118, 200, 21, 180, 118, 208, 53, 90, 243, 74, 76, 109, 106, 46, 103, 112, 197, 89, 92, 178, 83, 48, 97, 162, 73, 78, 105, 145, 213, 230, 17, 211, 121, 200, 101, 179, 158, 85, 99, 211, 68, 122, 234, 176, 4, 33, 225, 120, 139, 163, 110, 35, 199, 23, 45, 126, 199, 80, 145, 14, 74, 217, 200, 172, 216, 159, 237, 241, 157, 85, 210, 141, 180, 150, 187, 82, 48, 245, 154, 125, 60, 223, 244, 21, 20, 39, 88, 8, 153, 185, 227, 76, 78, 137, 99, 98, 81, 141, 27, 197, 41, 39, 251, 80, 27, 85, 78, 65, 15, 216, 106, 106, 113, 33, 253, 210, 46, 214, 47, 49, 89, 170, 215, 207, 62, 182, 88, 25, 186, 166, 214, 172, 63, 94, 17, 123, 235, 226, 72, 73, 204, 18, 173, 134, 92, 66, 2, 213, 151, 251, 95, 175, 38, 56, 156, 138, 96, 123, 190, 107, 59, 230, 24, 210, 224, 206, 169, 159, 95, 180, 237, 34, 194, 62, 4, 213, 228, 85, 216, 138, 157, 50, 20, 101, 160, 195, 138, 207, 18, 17, 232, 6, 73, 82, 247, 173, 50, 180, 53, 58, 156, 97, 230, 112, 211, 251, 204, 120, 188, 34, 41, 67, 83, 197, 131, 251, 176, 20, 70, 169, 116, 237, 43, 117, 45, 31, 66, 74, 152, 216, 3, 108, 102, 99, 5, 127, 76, 129, 57, 180, 90, 218, 157, 108, 85, 4, 240, 101, 149, 154, 221, 208, 70, 152, 34, 128, 57, 135, 38, 17, 139, 142, 167, 109, 73, 129, 181, 105, 45, 151, 106, 171, 166, 0, 113, 147, 141, 19, 228, 196, 88, 175, 219, 18, 213, 54, 105, 179, 8, 249, 250, 164, 86, 28, 185, 19, 60, 50, 140, 73, 237, 148, 201, 33, 204, 189, 43, 83, 163, 138, 1, 10, 13, 240, 196, 211, 221, 169, 207, 100, 167, 203, 146, 115, 70, 118, 230, 4, 224, 192, 209, 242, 144, 150, 72, 170, 149, 255, 196, 7, 91, 55, 251, 57, 127, 103, 98, 113, 83, 224, 97, 118, 132, 81, 119, 8, 105, 250, 155, 107, 149, 28, 127, 66, 127, 224, 79, 96, 9, 168, 73, 84, 228, 123, 161, 222, 179, 115, 73, 184, 62, 24, 228, 44, 156, 42, 124, 209, 29, 81, 19, 169, 24, 212, 6, 238, 239, 221, 68, 220, 106, 0, 45, 201, 129, 3, 50, 150, 244, 32, 220, 237, 20, 39, 175, 249, 80, 189, 166, 68, 251, 102, 60, 137, 93, 209, 86, 194, 55, 164, 100, 76, 220, 249, 30, 233, 101, 177, 150, 71, 28, 227, 180, 44, 115, 83, 201, 129, 44, 128, 247, 68, 175, 97, 36, 170, 76, 236, 57, 119, 240, 0, 129, 185, 35, 160, 231, 183, 56, 162, 197, 237, 186, 109, 118, 232, 84, 108, 125, 93, 92, 101, 193, 180, 210, 192, 244, 47, 55, 56, 217, 178, 200, 168, 232, 80, 223, 209, 255, 234, 146, 46, 215, 170, 197, 94, 84, 213, 233, 140, 247, 69, 185, 103, 183, 91, 23, 232, 32, 246, 244, 30, 41, 156, 28, 72, 109, 90, 127, 135, 132, 19, 136, 233, 168, 29, 98, 17, 111, 5, 185, 234, 86, 234, 114, 47, 227, 81, 77, 108, 179, 184, 91, 31, 74, 23, 29, 248, 41, 207, 8, 23, 181, 33, 99, 217, 48, 145, 97, 126, 139, 133, 11, 100, 69, 151, 146, 38, 79, 231, 155, 92, 134, 139, 189, 237, 132, 196, 95, 45, 141, 15, 26, 37, 58, 219, 10, 0, 36, 221, 240, 82, 117, 163, 121, 141, 206, 21, 180, 195, 58, 109, 56, 123, 152, 206, 116, 161, 221, 125, 248, 23, 31, 240, 227, 186, 52, 171, 147, 51, 39, 203, 92, 205, 182, 146, 149, 111, 27, 59, 219, 234, 216, 52, 89, 22, 224, 76, 62, 94, 76, 131, 48, 162, 134, 161, 177, 44, 205, 101, 253, 13, 237, 40, 29, 72, 224, 121, 74, 189, 57, 81, 58, 169, 178, 173, 157, 182, 143, 205, 64, 225, 137, 188, 235, 43, 195, 3, 187, 105, 113, 72, 82, 153, 58, 97, 38, 251, 212, 149, 191, 11, 153, 157, 106, 16, 236, 237, 209, 210, 208, 19, 68, 92, 176, 65, 24, 115, 181, 94, 24, 126, 2, 216, 63, 200, 136, 178, 92, 248, 11, 128, 68, 122, 14, 46, 234, 48, 142, 219, 92, 29, 136, 70, 200, 52, 78, 70, 160, 215, 113, 102, 190, 66, 16, 69, 120, 25, 201, 23, 209, 41, 79, 25, 151, 38, 38, 82, 244, 143, 121, 216, 111, 91, 167, 232, 32, 234, 243, 195, 168, 240, 135, 188, 1, 92, 145, 77, 240, 107, 20, 82, 147, 168, 132, 78, 115, 206, 95, 47, 8, 80, 91, 255, 28, 38, 161, 52, 168, 211, 236, 143, 238, 146, 172, 104, 2, 254, 240, 229, 210, 225, 47, 41, 76, 134, 5, 20, 203, 188, 48, 195, 120, 103, 234, 94, 217, 142, 238, 254, 131, 146, 214, 106, 212, 229, 201, 79, 151, 198, 100, 132, 99, 228, 82, 182, 94, 216, 226, 163, 42, 113, 110, 201, 70, 221, 127, 242, 7, 176, 60, 121, 158, 37, 56, 6, 156, 191, 75, 94, 222, 10, 155, 39, 64, 172, 216, 106, 210, 202, 246, 66, 83, 107, 250, 17, 134, 222, 212, 71, 200, 215, 103, 35, 82, 225, 106, 17, 106, 74, 18, 130, 236, 175, 45, 145, 155, 169, 88, 72, 244, 3, 38, 245, 208, 49, 129, 205, 48, 19, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 21, 49, 6, 4, 4, 1, 0, 0, 0, 48, 87, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 20, 49, 74, 30, 72, 0, 100, 0, 99, 0, 99, 0, 52, 0, 51, 0, 48, 0, 56, 0, 56, 0, 45, 0, 50, 0, 57, 0, 54, 0, 53, 0, 45, 0, 52, 0, 57, 0, 97, 0, 48, 0, 45, 0, 56, 0, 51, 0, 54, 0, 53, 0, 45, 0, 50, 0, 52, 0, 101, 0, 52, 0, 97, 0, 52, 0, 49, 0, 100, 0, 55, 0, 50, 0, 52, 0, 48, 48, 93, 6, 9, 43, 6, 1, 4, 1, 130, 55, 17, 1, 49, 80, 30, 78, 0, 77, 0, 105, 0, 99, 0, 114, 0, 111, 0, 115, 0, 111, 0, 102, 0, 116, 0, 32, 0, 83, 0, 116, 0, 114, 0, 111, 0, 110, 0, 103, 0, 32, 0, 67, 0, 114, 0, 121, 0, 112, 0, 116, 0, 111, 0, 103, 0, 114, 0, 97, 0, 112, 0, 104, 0, 105, 0, 99, 0, 32, 0, 80, 0, 114, 0, 111, 0, 118, 0, 105, 0, 100, 0, 101, 0, 114, 48, 130, 3, 199, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 6, 160, 130, 3, 184, 48, 130, 3, 180, 2, 1, 0, 48, 130, 3, 173, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 206, 244, 28, 93, 203, 68, 165, 233, 2, 2, 7, 208, 128, 130, 3, 128, 74, 136, 80, 43, 195, 182, 181, 122, 132, 229, 10, 181, 229, 1, 78, 122, 145, 95, 16, 236, 242, 107, 9, 141, 186, 205, 32, 139, 154, 132, 184, 180, 80, 26, 3, 85, 196, 10, 33, 216, 101, 105, 172, 196, 77, 222, 232, 229, 37, 199, 6, 189, 152, 8, 203, 15, 231, 164, 140, 163, 120, 23, 137, 34, 16, 241, 186, 64, 11, 241, 210, 160, 186, 90, 55, 39, 21, 210, 145, 74, 151, 40, 122, 221, 240, 191, 185, 115, 85, 208, 125, 136, 51, 210, 137, 124, 155, 65, 135, 50, 35, 233, 223, 157, 131, 108, 11, 142, 152, 217, 162, 163, 218, 47, 89, 255, 229, 21, 224, 139, 187, 4, 175, 251, 248, 8, 18, 16, 112, 134, 75, 17, 90, 246, 62, 150, 31, 207, 95, 172, 5, 220, 135, 201, 179, 247, 193, 177, 23, 5, 170, 207, 66, 219, 145, 117, 99, 167, 238, 100, 158, 169, 44, 22, 199, 132, 38, 67, 203, 66, 187, 53, 216, 98, 113, 76, 142, 153, 36, 238, 110, 152, 251, 68, 6, 154, 255, 51, 65, 75, 91, 9, 121, 86, 116, 35, 224, 47, 220, 194, 17, 136, 175, 76, 165, 210, 153, 89, 104, 197, 133, 200, 49, 173, 1, 167, 5, 88, 183, 58, 193, 146, 30, 60, 129, 195, 3, 16, 78, 87, 167, 135, 182, 182, 150, 68, 116, 161, 116, 125, 180, 155, 103, 63, 0, 98, 27, 179, 142, 64, 73, 31, 35, 63, 138, 137, 30, 169, 149, 221, 104, 21, 182, 23, 67, 246, 2, 162, 217, 165, 238, 124, 229, 149, 84, 5, 203, 174, 149, 79, 153, 25, 153, 233, 213, 86, 250, 10, 42, 6, 226, 113, 123, 90, 76, 153, 39, 203, 237, 124, 36, 191, 232, 132, 127, 82, 163, 109, 100, 121, 54, 254, 116, 155, 26, 255, 50, 150, 140, 172, 240, 208, 245, 65, 72, 49, 183, 149, 220, 244, 120, 193, 37, 222, 144, 137, 82, 168, 233, 13, 179, 2, 217, 29, 177, 4, 136, 69, 192, 133, 249, 180, 9, 62, 162, 216, 251, 164, 188, 173, 143, 149, 32, 204, 255, 246, 249, 33, 216, 75, 23, 127, 215, 134, 69, 79, 112, 213, 198, 89, 44, 51, 19, 226, 16, 210, 125, 212, 232, 18, 252, 178, 93, 245, 33, 62, 81, 207, 78, 167, 144, 238, 251, 27, 194, 21, 53, 44, 63, 58, 26, 176, 75, 79, 164, 67, 59, 80, 17, 54, 209, 58, 184, 2, 36, 202, 135, 91, 35, 78, 55, 203, 134, 238, 79, 178, 84, 242, 46, 223, 131, 227, 87, 255, 182, 244, 117, 162, 60, 134, 161, 49, 59, 95, 64, 190, 30, 195, 100, 106, 7, 120, 181, 202, 122, 174, 234, 30, 11, 88, 65, 238, 53, 64, 243, 233, 185, 168, 34, 8, 58, 233, 171, 210, 104, 105, 93, 49, 206, 11, 40, 172, 248, 204, 80, 128, 53, 143, 54, 95, 92, 70, 152, 209, 193, 116, 252, 138, 19, 50, 249, 43, 14, 225, 167, 8, 205, 112, 103, 79, 223, 14, 141, 147, 70, 197, 91, 11, 117, 202, 19, 180, 240, 21, 118, 108, 25, 63, 54, 94, 156, 112, 109, 16, 216, 113, 192, 246, 207, 156, 203, 65, 75, 143, 157, 125, 158, 151, 167, 207, 96, 6, 162, 97, 66, 114, 95, 227, 52, 44, 98, 121, 139, 181, 240, 89, 27, 59, 156, 189, 93, 28, 48, 165, 11, 245, 102, 198, 29, 5, 6, 180, 147, 58, 130, 65, 201, 10, 164, 193, 93, 168, 96, 156, 89, 225, 139, 70, 245, 74, 128, 3, 141, 133, 137, 21, 163, 77, 3, 19, 226, 35, 248, 156, 56, 56, 37, 221, 69, 67, 214, 3, 152, 149, 224, 92, 72, 173, 39, 196, 229, 153, 67, 151, 190, 115, 20, 70, 126, 210, 140, 109, 186, 46, 82, 88, 185, 96, 1, 254, 161, 217, 130, 226, 133, 18, 103, 175, 132, 249, 102, 51, 229, 192, 94, 44, 10, 25, 197, 237, 77, 196, 1, 253, 153, 78, 237, 151, 136, 89, 203, 113, 244, 217, 235, 252, 31, 116, 139, 233, 40, 197, 22, 176, 157, 130, 109, 149, 215, 11, 20, 3, 156, 239, 29, 250, 95, 188, 241, 184, 117, 108, 216, 74, 91, 169, 186, 122, 175, 214, 36, 62, 240, 142, 107, 172, 7, 250, 31, 101, 75, 83, 255, 56, 8, 231, 200, 194, 154, 105, 202, 170, 207, 252, 128, 10, 249, 53, 41, 168, 94, 225, 163, 10, 251, 149, 64, 10, 144, 252, 44, 136, 149, 119, 183, 7, 230, 87, 160, 46, 62, 185, 82, 218, 213, 125, 62, 70, 43, 27, 5, 181, 50, 193, 11, 30, 0, 8, 81, 94, 169, 171, 143, 113, 235, 171, 38, 129, 116, 11, 191, 75, 235, 185, 184, 178, 36, 193, 174, 177, 51, 87, 163, 142, 52, 62, 161, 237, 139, 50, 51, 227, 188, 164, 106, 233, 209, 8, 237, 241, 92, 145, 51, 6, 36, 197, 24, 255, 143, 5, 144, 43, 87, 242, 208, 251, 79, 171, 90, 103, 219, 73, 242, 95, 36, 48, 95, 127, 40, 128, 201, 80, 79, 74, 226, 25, 43, 50, 56, 180, 59, 84, 148, 110, 151, 9, 45, 4, 212, 172, 31, 189, 44, 115, 59, 169, 48, 59, 48, 31, 48, 7, 6, 5, 43, 14, 3, 2, 26, 4, 20, 238, 91, 24, 104, 64, 45, 237, 63, 114, 36, 111, 106, 82, 43, 251, 110, 60, 159, 42, 178, 4, 20, 20, 49, 70, 55, 115, 247, 221, 156, 47, 189, 197, 19, 116, 77, 161, 163, 216, 77, 166, 144, 2, 2, 7, 208 };
            X509Certificate2 certificate = new X509Certificate2(certificateRawBytes, "", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
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

            if (DataTestUtility.IsAKVSetupAvailable())
            {
                KeyVaultClient keyVaultClient = keyVaultClient = new KeyVaultClient(AADUtility.AzureActiveDirectoryAuthenticationCallback);
                IPage<KeyItem> keys = keyVaultClient.GetKeysAsync(DataTestUtility.AKVBaseUrl).Result;
                bool testAKVKeyExists = false;
                while (true)
                {
                    foreach (KeyItem ki in keys)
                    {
                        if (ki.Identifier.Name.Equals(DataTestUtility.AKVKeyName))
                        {
                            testAKVKeyExists = true;
                        }
                    }

                    if (!string.IsNullOrEmpty(keys.NextPageLink))
                    {
                        keys = keyVaultClient.GetKeysNextAsync(keys.NextPageLink).Result;
                    }
                    else
                    {
                        break;
                    }
                }

                if (!testAKVKeyExists)
                {
                    RSAParameters p = certificate.GetRSAPrivateKey().ExportParameters(true);
                    KeyBundle kb = new KeyBundle()
                    {
                        Key = new Azure.KeyVault.WebKey.JsonWebKey
                        {
                            Kty = JsonWebKeyType.Rsa,
                            D = p.D,
                            DP = p.DP,
                            DQ = p.DQ,
                            P = p.P,
                            Q = p.Q,
                            QI = p.InverseQ,
                            N = p.Modulus,
                            E = p.Exponent,
                        },
                    };
                    keyVaultClient.ImportKeyAsync(DataTestUtility.AKVBaseUrl, DataTestUtility.AKVKeyName, kb);
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

        internal static byte[] DecryptRsaDirectly(byte[] rsaPfx, byte[] ciphertextCek, string masterKeyPath)
        {
            Debug.Assert(rsaPfx != null && rsaPfx.Length > 0);
            // The rest of the parameters may be invalid for exception handling test cases

            X509Certificate2 x509 = new X509Certificate2(rsaPfx, @"P@zzw0rD!SqlvN3x+");

            Debug.Assert(x509.HasPrivateKey);

            SqlColumnEncryptionCertificateStoreProvider rsaProvider = new SqlColumnEncryptionCertificateStoreProvider();
            Object RsaDecryptionResult = SqlColumnEncryptionCertificateStoreProviderRSADecrypt.Invoke(rsaProvider, new object[] { ciphertextCek, x509 });

            return (byte[])RsaDecryptionResult;
        }

        internal static bool VerifyRsaSignatureDirectly(byte[] hashedCek, byte[] signedCek, byte[] rsaPfx)
        {
            Debug.Assert(rsaPfx != null && rsaPfx.Length > 0);

            X509Certificate2 x509 = new X509Certificate2(rsaPfx, @"P@zzw0rD!SqlvN3x+");
            Debug.Assert(x509.HasPrivateKey);

            SqlColumnEncryptionCertificateStoreProvider rsaProvider = new SqlColumnEncryptionCertificateStoreProvider();
            Object RsaVerifySignatureResult = SqlColumnEncryptionCertificateStoreProviderRSAVerifySignature.Invoke(rsaProvider, new object[] { hashedCek, signedCek, x509 });

            return (bool)RsaVerifySignatureResult;
        }

        /// <summary>
        /// Decrypt Data using AEAD
        /// </summary>
        internal static byte[] DecryptDataUsingAED(byte[] encryptedCellBlob, byte[] key, CColumnEncryptionType encryptionType)
        {
            Debug.Assert(encryptedCellBlob != null && encryptedCellBlob.Length > 0);
            Debug.Assert(key != null && key.Length > 0);

            byte[] decryptedData = null;

            Object columnEncryptionKey = sqlColumnEncryptionKeyConstructor.Invoke(new object[] { key });
            Debug.Assert(columnEncryptionKey != null);

            Object aesFactory = Activator.CreateInstance(sqlAeadAes256CbcHmac256Factory);
            Debug.Assert(aesFactory != null);

            object[] parameters = new object[] { columnEncryptionKey, encryptionType, SQLSetupStrategy.ColumnEncryptionAlgorithmName };
            Object authenticatedAES = sqlAeadAes256CbcHmac256FactoryCreate.Invoke(aesFactory, parameters);
            Debug.Assert(authenticatedAES != null);

            parameters = new object[] { encryptedCellBlob };
            Object decryptedValue = sqlClientEncryptionAlgorithmDecryptData.Invoke(authenticatedAES, parameters);
            Debug.Assert(decryptedValue != null);

            decryptedData = (byte[])decryptedValue;

            return decryptedData;
        }

        internal static SqlConnection GetOpenConnection(bool fTceEnabled, SqlConnectionStringBuilder sb, bool fSuppressAttestation = false)
        {
            SqlConnection conn = new SqlConnection(GetConnectionString(fTceEnabled, sb, fSuppressAttestation));
            try
            {
                conn.Open();
            }
            catch (Exception)
            {
                conn.Dispose();
                throw;
            }

            SqlConnection.ClearPool(conn);
            return conn;
        }

        /// <summary>
        /// Fetches a connection string that can be used to connect to SQL Server
        /// </summary>
        public static string GetConnectionString(bool fTceEnabled, SqlConnectionStringBuilder sb, bool fSuppressAttestation = false)
        {
            SqlConnectionStringBuilder builder = sb;
            if (fTceEnabled)
            {
                builder.ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled;
            }
            if (!fSuppressAttestation && DataTestUtility.EnclaveEnabled)
            {
                builder.EnclaveAttestationUrl = sb.EnclaveAttestationUrl;
                builder.AttestationProtocol = sb.AttestationProtocol;
            }
            builder.ConnectTimeout = 10000;
            return builder.ToString();
        }

        /// <summary>
        /// Turns on/off the TCE feature on server via traceflag
        /// </summary>
        public static void ChangeServerTceSetting(bool fEnable, SqlConnectionStringBuilder sb)
        {
            using (SqlConnection conn = GetOpenConnection(false, sb, fSuppressAttestation: true))
            {
                using (SqlCommand cmd = new SqlCommand("", conn))
                {
                    if (fEnable)
                    {
                        cmd.CommandText = "dbcc traceoff(4053, -1)";
                    }
                    else
                    {
                        cmd.CommandText = "dbcc traceon(4053, -1)"; // traceon disables feature
                    }
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}

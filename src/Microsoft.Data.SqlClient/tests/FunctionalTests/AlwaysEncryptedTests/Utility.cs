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
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

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
            Assert.True(SqlCipherMetadataConstructor != null);
            Assert.True(SqlTceCipherInfoEntryConstructor != null);
            Object entry = SqlTceCipherInfoEntryConstructor.Invoke(new object[] { 1 });// this param is "ordinal"
            Object[] parameters = new Object[] { entry, ordinal, cipherAlgorithmId, cipherAlgorithmName, encryptionType, normalizationRuleVersion };
            return SqlCipherMetadataConstructor.Invoke(parameters);
        }

        internal static byte[] DecryptWithKey(byte[] cipherText, Object cipherMd)
        {
            return (byte[])SqlSecurityUtilDecryptWithKey.Invoke(null, new Object[] { cipherText, cipherMd, new SqlConnection(), new SqlCommand() });
        }

        internal static byte[] EncryptWithKey(byte[] plainText, Object cipherMd)
        {
            return (byte[])SqlSecurityUtilEncryptWithKey.Invoke(null, new Object[] { plainText, cipherMd, new SqlConnection(), new SqlCommand() });
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

        internal static object ClearSqlConnectionGlobalProvidersLock = new();

        /// <summary>
        /// Through reflection, clear the static provider list set on SqlConnection. 
        /// Note- Any test using this method should be wrapped in a lock statement using ClearSqlConnectionGlobalProvidersLock
        /// </summary>
        internal static void ClearSqlConnectionGlobalProviders()
        {
            SqlConnection conn = new SqlConnection();
            FieldInfo field = conn.GetType().GetField("s_globalCustomColumnEncryptionKeyStoreProviders", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.True(field != null);
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
                {
                    System.Console.WriteLine(@"ASSERT: {0}", message);
                }
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

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    class CertificateUtility
    {
        public const string EmbeddedCertificatePassword = @"P@zzw0rD!SqlvN3x+";

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
            ClearCache(cache);
        }

        /// <summary>
        /// Create a self-signed certificate.
        /// </summary>
        internal static X509Certificate2 CreateCertificate()
        {
            X509Certificate2 certificate = new X509Certificate2(Resources.Resources.Certificate1, EmbeddedCertificatePassword, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
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
                SetupAKVKeysAsync().Wait();
            }

            return certificate;
        }

        private static async Task SetupAKVKeysAsync()
        {
            KeyClient keyClient = new KeyClient(DataTestUtility.AKVBaseUri, DataTestUtility.GetTokenCredential());
            AsyncPageable<KeyProperties> keys = keyClient.GetPropertiesOfKeysAsync();
            IAsyncEnumerator<KeyProperties> enumerator = keys.GetAsyncEnumerator();

            bool testAKVKeyExists = false;
            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    KeyProperties keyProperties = enumerator.Current;
                    if (keyProperties.Name.Equals(DataTestUtility.AKVKeyName))
                    {
                        testAKVKeyExists = true;
                    }
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            if (!testAKVKeyExists)
            {
                var rsaKeyOptions = new CreateRsaKeyOptions(DataTestUtility.AKVKeyName, hardwareProtected: false)
                {
                    KeySize = 2048,
                    ExpiresOn = DateTimeOffset.Now.AddYears(1)
                };
                keyClient.CreateRsaKey(rsaKeyOptions);
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

        internal static byte[] DecryptRsaDirectly(byte[] rsaPfx, byte[] ciphertextCek, string masterKeyPath)
        {
            Debug.Assert(rsaPfx != null && rsaPfx.Length > 0);
            // The rest of the parameters may be invalid for exception handling test cases

            X509Certificate2 x509 = new X509Certificate2(rsaPfx, EmbeddedCertificatePassword);

            Debug.Assert(x509.HasPrivateKey);

            SqlColumnEncryptionCertificateStoreProvider rsaProvider = new SqlColumnEncryptionCertificateStoreProvider();
            Object RsaDecryptionResult = SqlColumnEncryptionCertificateStoreProviderRSADecrypt.Invoke(rsaProvider, new object[] { ciphertextCek, x509 });

            return (byte[])RsaDecryptionResult;
        }

        internal static bool VerifyRsaSignatureDirectly(byte[] hashedCek, byte[] signedCek, byte[] rsaPfx)
        {
            Debug.Assert(rsaPfx != null && rsaPfx.Length > 0);

            X509Certificate2 x509 = new X509Certificate2(rsaPfx, EmbeddedCertificatePassword);
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

        private static void ClearCache(MemoryCache cache)
        {
#if NET
            cache.Clear();
#else
            // Compact with a target of 100% of objects is equivalent to clearing the cache
            cache.Compact(1);
#endif
        }
    }
}

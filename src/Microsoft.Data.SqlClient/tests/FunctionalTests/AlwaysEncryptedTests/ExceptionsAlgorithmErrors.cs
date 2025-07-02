// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures;
using Xunit;
using static Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests.Utility;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class ExceptionsAlgorithmErrors : IClassFixture<ColumnEncryptionCertificateFixture>
    {
        // Reflection
        public static Assembly systemData = Assembly.GetAssembly(typeof(SqlConnection));
        public static Type sqlClientSymmetricKey = systemData.GetType("Microsoft.Data.SqlClient.SqlClientSymmetricKey");
        public static ConstructorInfo sqlColumnEncryptionKeyConstructor = sqlClientSymmetricKey.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(byte[]) }, null);

        private readonly ColumnEncryptionCertificateFixture _fixture;
        private readonly byte[] _cek;
        private readonly byte[] _encryptedCek;
        private readonly string _certificatePath;

        public ExceptionsAlgorithmErrors(ColumnEncryptionCertificateFixture fixture)
        {
            // Disable the cache to avoid false failures.
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;

            SqlColumnEncryptionCertificateStoreProvider provider = new SqlColumnEncryptionCertificateStoreProvider();
            X509Certificate2 currUserCertificate = fixture.GetCertificate(StoreLocation.CurrentUser);

            _cek = GenerateRandomBytes(32);
            _fixture = fixture;
            _certificatePath = string.Format("CurrentUser/My/{0}", currUserCertificate.Thumbprint);
            _encryptedCek = provider.EncryptColumnEncryptionKey(_certificatePath, "RSA_OAEP", _cek);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestNullCEK()
        {
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => sqlColumnEncryptionKeyConstructor.Invoke(new object[] { new byte[] { } }));
            string expectedMessage = SystemDataResourceManager.Instance.TCE_NullColumnEncryptionKeySysErr;
            Assert.Matches(expectedMessage, e.InnerException.Message);
            e = Assert.Throws<TargetInvocationException>(() => sqlColumnEncryptionKeyConstructor.Invoke(new object[] { null }));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidKeySize()
        {
            const int keySize = 48;
            byte[] key = GenerateRandomBytes(keySize);
            for (int i = 0; i < key.Length; i++)
            {
                key[i] = 0x00;
            }
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() =>
                EncryptDataUsingAED(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, key, CColumnEncryptionType.Deterministic));
            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_InvalidKeySize, "AEAD_AES_256_CBC_HMAC_SHA256", keySize, 32);
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidEncryptionType()
        {
            const byte invalidEncryptionType = 3;
            Object cipherMD = GetSqlCipherMetadata(0, 2, null, invalidEncryptionType, 0x01);
            AddEncryptionKeyToCipherMD(cipherMD, _encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, _certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = EncryptDataUsingAED(plainText, _cek, CColumnEncryptionType.Deterministic);

            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_InvalidEncryptionType,
                "AEAD_AES_256_CBC_HMAC_SHA256", invalidEncryptionType, "'Deterministic', 'Randomized'");
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => DecryptWithKey(cipherText, cipherMD));
            Assert.Contains(expectedMessage, e.InnerException.Message);

            e = Assert.Throws<TargetInvocationException>(() => EncryptWithKey(plainText, cipherMD));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidCipherText()
        {
            const int invalidCiphertextLength = 53;
            // Attempt to decrypt 53 random bytes
            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_InvalidCipherTextSize,
                invalidCiphertextLength, 65);
            byte[] cipherText = GenerateRandomBytes(invalidCiphertextLength); // minimum length is 65
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => DecryptDataUsingAED(cipherText, _cek, CColumnEncryptionType.Deterministic));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidAlgorithmVersion()
        {
            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_InvalidAlgorithmVersion,
                40, "01");
            byte[] plainText = Encoding.Unicode.GetBytes("Hello World");
            byte[] cipherText = EncryptDataUsingAED(plainText, _cek, CColumnEncryptionType.Deterministic);
            // Put a version number of 0x10
            cipherText[0] = 0x40;
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => DecryptDataUsingAED(cipherText, _cek, CColumnEncryptionType.Deterministic));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidAuthenticationTag()
        {
            string expectedMessage = SystemDataResourceManager.Instance.TCE_InvalidAuthenticationTag;
            byte[] plainText = Encoding.Unicode.GetBytes("Hello World");
            byte[] cipherText = EncryptDataUsingAED(plainText, _cek, CColumnEncryptionType.Deterministic);
            // Zero out 4 bytes of authentication tag
            for (int i = 0; i < 4; i++)
            {
                cipherText[i + 1] = 0x00;
            }
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => DecryptDataUsingAED(cipherText, _cek, CColumnEncryptionType.Deterministic));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestNullColumnEncryptionAlgorithm()
        {
            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_NullColumnEncryptionAlgorithm,
                "'AEAD_AES_256_CBC_HMAC_SHA256'");
            Object cipherMD = GetSqlCipherMetadata(0, 0, null, 1, 0x01);
            AddEncryptionKeyToCipherMD(cipherMD, _encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, _certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = EncryptDataUsingAED(plainText, _cek, CColumnEncryptionType.Deterministic);

            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => DecryptWithKey(cipherText, cipherMD));
            Assert.Contains(expectedMessage, e.InnerException.Message);
            e = Assert.Throws<TargetInvocationException>(() => EncryptWithKey(plainText, cipherMD));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestUnknownEncryptionAlgorithmId()
        {
            const byte unknownEncryptionAlgoId = 3;
            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_UnknownColumnEncryptionAlgorithmId,
                unknownEncryptionAlgoId, "'1', '2'");
            Object cipherMD = GetSqlCipherMetadata(0, unknownEncryptionAlgoId, null, 1, 0x01);
            AddEncryptionKeyToCipherMD(cipherMD, _encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, _certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = EncryptDataUsingAED(plainText, _cek, CColumnEncryptionType.Deterministic);

            Exception decryptEx = Assert.Throws<TargetInvocationException>(() => DecryptWithKey(plainText, cipherMD));
            Assert.Matches(expectedMessage, decryptEx.InnerException.Message);

            Exception encryptEx = Assert.Throws<TargetInvocationException>(() => EncryptWithKey(plainText, cipherMD));
            Assert.Matches(expectedMessage, encryptEx.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestUnknownCustomKeyStoreProvider()
        {
            lock (Utility.ClearSqlConnectionGlobalProvidersLock)
            {
                // Clear out the existing providers (to ensure test reliability)
                ClearSqlConnectionGlobalProviders();

                const string invalidProviderName = "Dummy_Provider";
                string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_UnrecognizedKeyStoreProviderName,
                    invalidProviderName, "'MSSQL_CERTIFICATE_STORE', 'MSSQL_CNG_STORE', 'MSSQL_CSP_PROVIDER'", "");
                Object cipherMD = GetSqlCipherMetadata(0, 1, null, 1, 0x03);
                AddEncryptionKeyToCipherMD(cipherMD, _encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, _certificatePath, invalidProviderName, "RSA_OAEP");
                byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
                byte[] cipherText = EncryptDataUsingAED(plainText, _cek, CColumnEncryptionType.Deterministic);

                Exception decryptEx = Assert.Throws<TargetInvocationException>(() => DecryptWithKey(plainText, cipherMD));
                Assert.Contains(expectedMessage, decryptEx.InnerException.Message);

                Exception encryptEx = Assert.Throws<TargetInvocationException>(() => EncryptWithKey(plainText, cipherMD));
                Assert.Contains(expectedMessage, encryptEx.InnerException.Message);

                ClearSqlConnectionGlobalProviders();
            }
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestTceUnknownEncryptionAlgorithm()
        {
            const string unknownEncryptionAlgorithm = "Dummy";
            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_UnknownColumnEncryptionAlgorithm,
                unknownEncryptionAlgorithm, "'AEAD_AES_256_CBC_HMAC_SHA256'");
            Object cipherMD = GetSqlCipherMetadata(0, 0, "Dummy", 1, 0x01);
            AddEncryptionKeyToCipherMD(cipherMD, _encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, _certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = EncryptDataUsingAED(plainText, _cek, CColumnEncryptionType.Deterministic);

            Exception decryptEx = Assert.Throws<TargetInvocationException>(() => DecryptWithKey(cipherText, cipherMD));
            Assert.Contains(expectedMessage, decryptEx.InnerException.Message);

            Exception encryptEx = Assert.Throws<TargetInvocationException>(() => EncryptWithKey(plainText, cipherMD));
            Assert.Contains(expectedMessage, encryptEx.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestExceptionsFromCertStore()
        {
            byte[] corruptedCek = GenerateInvalidEncryptedCek(_cek, ECEKCorruption.SIGNATURE);

            string expectedMessage = string.Format(SystemDataResourceManager.Instance.TCE_KeyDecryptionFailedCertStore,
                "MSSQL_CERTIFICATE_STORE", BitConverter.ToString(corruptedCek, corruptedCek.Length - 10, 10));

            Object cipherMD = GetSqlCipherMetadata(0, 1, null, 1, 0x01);
            AddEncryptionKeyToCipherMD(cipherMD, corruptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, _certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = EncryptDataUsingAED(plainText, _cek, CColumnEncryptionType.Deterministic);

            Exception decryptEx = Assert.Throws<TargetInvocationException>(() => DecryptWithKey(cipherText, cipherMD));
            Assert.Matches(expectedMessage, decryptEx.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestExceptionsFromCustomKeyStore()
        {
            lock (Utility.ClearSqlConnectionGlobalProvidersLock)
            {
                string expectedMessage = "Failed to decrypt a column encryption key";

                // Clear out the existing providers (to ensure test reliability)
                ClearSqlConnectionGlobalProviders();

                IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
                customProviders.Add(DummyKeyStoreProvider.Name, new DummyKeyStoreProvider());
                SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders);

                object cipherMD = GetSqlCipherMetadata(0, 1, null, 1, 0x01);
                AddEncryptionKeyToCipherMD(cipherMD, _encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, _certificatePath, "DummyProvider", "DummyAlgo");
                byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
                byte[] cipherText = EncryptDataUsingAED(plainText, _cek, CColumnEncryptionType.Deterministic);

                Exception decryptEx = Assert.Throws<TargetInvocationException>(() => DecryptWithKey(cipherText, cipherMD));
                Assert.Contains(expectedMessage, decryptEx.InnerException.Message);

                Exception encryptEx = Assert.Throws<TargetInvocationException>(() => EncryptWithKey(cipherText, cipherMD));
                Assert.Contains(expectedMessage, encryptEx.InnerException.Message);

                ClearSqlConnectionGlobalProviders();
            }
        }
    }
}

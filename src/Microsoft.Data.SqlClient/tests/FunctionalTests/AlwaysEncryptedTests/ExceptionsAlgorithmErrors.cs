// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Xunit;
using static Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests.Utility;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class ExceptionsAlgorithmErrors : IClassFixture<CertFixture>
    {
        // Reflection
        public static Assembly systemData = Assembly.GetAssembly(typeof(SqlConnection));
        public static Type sqlClientSymmetricKey = systemData.GetType("Microsoft.Data.SqlClient.SqlClientSymmetricKey");
        public static ConstructorInfo sqlColumnEncryptionKeyConstructor = sqlClientSymmetricKey.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(byte[]) }, null);

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestNullCEK()
        {
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => sqlColumnEncryptionKeyConstructor.Invoke(new object[] { new byte[] { } }));
            string expectedMessage = @"Internal error. Column encryption key cannot be null.\s+\(?Parameter (name: )?'?encryptionKey('\))?";
            Assert.Matches(expectedMessage, e.InnerException.Message);
            e = Assert.Throws<TargetInvocationException>(() => sqlColumnEncryptionKeyConstructor.Invoke(new object[] { null }));
            Assert.Matches(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidKeySize()
        {
            byte[] key = Utility.GenerateRandomBytes(48);
            for (int i = 0; i < key.Length; i++)
            {
                key[i] = 0x00;
            }
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() =>
                Utility.EncryptDataUsingAED(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, key, Utility.CColumnEncryptionType.Deterministic));
            string expectedMessage = @"The column encryption key has been successfully decrypted but it's length: 48 does not match the length: 32 for algorithm 'AEAD_AES_256_CBC_HMAC_SHA256'. Verify the encrypted value of the column encryption key in the database.\s+\(?Parameter (name: )?'?encryptionKey('\))?";
            Assert.Matches(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidEncryptionType()
        {
            Object cipherMD = Utility.GetSqlCipherMetadata(0, 2, null, 3, 0x01);
            Utility.AddEncryptionKeyToCipherMD(cipherMD, CertFixture.encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, CertFixture.certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = Utility.EncryptDataUsingAED(plainText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic);

            string expectedMessage = @"Encryption type '3' specified for the column in the database is either invalid or corrupted. Valid encryption types for algorithm 'AEAD_AES_256_CBC_HMAC_SHA256' are: 'Deterministic', 'Randomized'.\s+\(?Parameter (name: )?'?encryptionType('\))?";
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => Utility.DecryptWithKey(cipherText, cipherMD, "testsrv"));
            Assert.Matches(expectedMessage, e.InnerException.Message);

            e = Assert.Throws<TargetInvocationException>(() => Utility.EncryptWithKey(plainText, cipherMD, "testsrv"));
            Assert.Matches(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidCipherText()
        {
            // Attempt to decrypt 53 random bytes
            string expectedMessage = @"Specified ciphertext has an invalid size of 53 bytes, which is below the minimum 65 bytes required for decryption.\s+\(?Parameter (name: )?'?cipherText('\))?";
            byte[] cipherText = Utility.GenerateRandomBytes(53); // minimum length is 65
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => Utility.DecryptDataUsingAED(cipherText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic));
            Assert.Matches(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidAlgorithmVersion()
        {
            string expectedMessage = @"The specified ciphertext's encryption algorithm version '40' does not match the expected encryption algorithm version '01'.\s+\(?Parameter (name: )?'?cipherText('\))?";
            byte[] plainText = Encoding.Unicode.GetBytes("Hello World");
            byte[] cipherText = Utility.EncryptDataUsingAED(plainText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic);
            // Put a version number of 0x10
            cipherText[0] = 0x40;
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => Utility.DecryptDataUsingAED(cipherText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic));
            Assert.Matches(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidAuthenticationTag()
        {
            string expectedMessage = @"Specified ciphertext has an invalid authentication tag.\s+\(?Parameter (name: )?'?cipherText('\))?";
            byte[] plainText = Encoding.Unicode.GetBytes("Hello World");
            byte[] cipherText = Utility.EncryptDataUsingAED(plainText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic);
            // Zero out 4 bytes of authentication tag
            for (int i = 0; i < 4; i++)
            {
                cipherText[i + 1] = 0x00;
            }
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => Utility.DecryptDataUsingAED(cipherText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic));
            Assert.Matches(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestNullColumnEncryptionAlgorithm()
        {
            string expectedMessage = "Internal error. Encryption algorithm cannot be null.";
            Object cipherMD = Utility.GetSqlCipherMetadata(0, 0, null, 1, 0x01);
            Utility.AddEncryptionKeyToCipherMD(cipherMD, CertFixture.encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, CertFixture.certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = Utility.EncryptDataUsingAED(plainText, CertFixture.cek, CColumnEncryptionType.Deterministic);

            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => Utility.DecryptWithKey(cipherText, cipherMD, "testsrv"));
            Assert.Contains(expectedMessage, e.InnerException.Message);
            e = Assert.Throws<TargetInvocationException>(() => Utility.EncryptWithKey(plainText, cipherMD, "testsrv"));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestUnknownEncryptionAlgorithmId()
        {
            string errorMessage = @"Encryption algorithm id '3' for the column in the database is either invalid or corrupted. Valid encryption algorithm ids are: '1', '2'.\s+\(?Parameter (name: )?'?cipherAlgorithmId('\))?";
            Object cipherMD = Utility.GetSqlCipherMetadata(0, 3, null, 1, 0x01);
            Utility.AddEncryptionKeyToCipherMD(cipherMD, CertFixture.encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, CertFixture.certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = Utility.EncryptDataUsingAED(plainText, CertFixture.cek, CColumnEncryptionType.Deterministic);

            Exception decryptEx = Assert.Throws<TargetInvocationException>(() => Utility.DecryptWithKey(plainText, cipherMD, "localhost"));
            Assert.Matches(errorMessage, decryptEx.InnerException.Message);

            Exception encryptEx = Assert.Throws<TargetInvocationException>(() => Utility.EncryptWithKey(plainText, cipherMD, "localhost"));
            Assert.Matches(errorMessage, encryptEx.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestUnknownCustomKeyStoreProvider()
        {
            string errorMessage = "Failed to decrypt a column encryption key. Invalid key store provider name: 'Dummy_Provider'. A key store provider name must denote either a system key store provider or a registered custom key store provider.";
            Object cipherMD = Utility.GetSqlCipherMetadata(0, 1, null, 1, 0x03);
            Utility.AddEncryptionKeyToCipherMD(cipherMD, CertFixture.encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, CertFixture.certificatePath, "Dummy_Provider", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = Utility.EncryptDataUsingAED(plainText, CertFixture.cek, CColumnEncryptionType.Deterministic);

            Exception decryptEx = Assert.Throws<TargetInvocationException>(() => Utility.DecryptWithKey(plainText, cipherMD, "localhost"));
            Assert.Contains(errorMessage, decryptEx.InnerException.Message);

            Exception encryptEx = Assert.Throws<TargetInvocationException>(() => Utility.EncryptWithKey(plainText, cipherMD, "localhost"));
            Assert.Contains(errorMessage, encryptEx.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestTceUnknownEncryptionAlgorithm()
        {
            string errorMessage = "Encryption algorithm 'Dummy' for the column in the database is either invalid or corrupted.";
            Object cipherMD = Utility.GetSqlCipherMetadata(0, 0, "Dummy", 1, 0x01);
            Utility.AddEncryptionKeyToCipherMD(cipherMD, CertFixture.encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, CertFixture.certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = Utility.EncryptDataUsingAED(plainText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic);

            Exception decryptEx = Assert.Throws<TargetInvocationException>(() => Utility.DecryptWithKey(cipherText, cipherMD, "localhost"));
            Assert.Contains(errorMessage, decryptEx.InnerException.Message);

            Exception encryptEx = Assert.Throws<TargetInvocationException>(() => Utility.EncryptWithKey(plainText, cipherMD, "localhost"));
            Assert.Contains(errorMessage, encryptEx.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestExceptionsFromCertStore()
        {
            byte[] corruptedCek = Utility.GenerateInvalidEncryptedCek(CertFixture.cek, Utility.ECEKCorruption.SIGNATURE);

            // Pass a garbled encrypted CEK
            string[] errorMessages = {
                string.Format(@"Failed to decrypt a column encryption key using key store provider: 'MSSQL_CERTIFICATE_STORE'. The last 10 bytes of the encrypted column encryption key are: '{0}'.\r\nSpecified encrypted column encryption key contains an invalid encryption algorithm version '00'. Expected version is '01'.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?", BitConverter.ToString(corruptedCek,corruptedCek.Length-10,10)),
                string.Format(@"Specified encrypted column encryption key signature does not match the signature computed with the column master key (certificate) in 'CurrentUser/My/{0}'. The encrypted column encryption key may be corrupt, or the specified path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?", CertFixture.thumbprint)
            };

            Object cipherMD = Utility.GetSqlCipherMetadata(0, 1, null, 1, 0x01);
            Utility.AddEncryptionKeyToCipherMD(cipherMD, corruptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, CertFixture.certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = Utility.EncryptDataUsingAED(plainText, CertFixture.cek, CColumnEncryptionType.Deterministic);

            Exception decryptEx = Assert.Throws<TargetInvocationException>(() => Utility.DecryptWithKey(cipherText, cipherMD, "localhost"));
            Assert.Matches(errorMessages[0], decryptEx.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestExceptionsFromCustomKeyStore()
        {
            string[] errorMessages = {
                string.Format("Failed to decrypt a column encryption key using key store provider: 'DummyProvider'. Verify the properties of the column encryption key and its column master key in your database. The last 10 bytes of the encrypted column encryption key are: '{0}'.\r\nThe method or operation is not implemented.", BitConverter.ToString(CertFixture.encryptedCek, CertFixture.encryptedCek.Length-10, 10)),
                string.Format("The method or operation is not implemented.")
                };

            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add("DummyProvider", new DummyKeyStoreProvider());
            SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders);

            Object cipherMD = Utility.GetSqlCipherMetadata(0, 1, null, 1, 0x01);
            Utility.AddEncryptionKeyToCipherMD(cipherMD, CertFixture.encryptedCek, 0, 0, 0, new byte[] { 0x01, 0x02, 0x03 }, CertFixture.certificatePath, "DummyProvider", "DummyAlgo");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = Utility.EncryptDataUsingAED(plainText, CertFixture.cek, CColumnEncryptionType.Deterministic);

            Exception decryptEx = Assert.Throws<TargetInvocationException>(() => Utility.DecryptWithKey(cipherText, cipherMD, "localhost"));
            Assert.Equal(errorMessages[0], decryptEx.InnerException.Message);

            Exception encryptEx = Assert.Throws<TargetInvocationException>(() => Utility.EncryptWithKey(plainText, cipherMD, "localhost"));
            Assert.Equal(errorMessages[0], encryptEx.InnerException.Message);

        }
    }

    public class CertFixture : IDisposable
    {
        private readonly SqlColumnEncryptionCertificateStoreProvider provider = new SqlColumnEncryptionCertificateStoreProvider();

        public static X509Certificate2 certificate;
        public static string thumbprint;
        public static string certificatePath;
        public static byte[] cek;
        public static byte[] encryptedCek;

        public CertFixture()
        {
            certificate = Utility.CreateCertificate();
            thumbprint = certificate.Thumbprint;
            certificatePath = string.Format("CurrentUser/My/{0}", thumbprint);
            cek = Utility.GenerateRandomBytes(32);
            encryptedCek = provider.EncryptColumnEncryptionKey(certificatePath, "RSA_OAEP", cek);

            // Disable the cache to avoid false failures.
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;
        }

        public void Dispose()
        {
            // Do NOT remove certificate for concurrent consistency. Certificates are used for other test cases as well.
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using System.Reflection;

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
        public void TestNullCEK() {
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => sqlColumnEncryptionKeyConstructor.Invoke(new object[] {new byte[] {}}));
            string expectedMessage = "Internal error. Column encryption key cannot be null.\r\nParameter name: encryptionKey";
            Assert.Contains(expectedMessage, e.InnerException.Message);
            e = Assert.Throws<TargetInvocationException>(() => sqlColumnEncryptionKeyConstructor.Invoke(new object[] { null }));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidKeySize () {
            byte[] key = Utility.GenerateRandomBytes(48);            
            for (int i =0; i < key.Length; i++) {
                key[i] = 0x00;
            }
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() =>
                Utility.EncryptDataUsingAED(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, key, Utility.CColumnEncryptionType.Deterministic));
            string expectedMessage = "The column encryption key has been successfully decrypted but it's length: 48 does not match the length: 32 for algorithm 'AEAD_AES_256_CBC_HMAC_SHA256'. Verify the encrypted value of the column encryption key in the database.\r\nParameter name: encryptionKey";
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidEncryptionType() {
            Object cipherMD = Utility.GetSqlCipherMetadata (0, 2, null, 3, 0x01);
            Utility.AddEncryptionKeyToCipherMD (cipherMD, CertFixture.encryptedCek, 0, 0, 0, new byte[]{0x01, 0x02, 0x03}, CertFixture.certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = Utility.EncryptDataUsingAED (plainText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic);

            string expectedMessage = "Encryption type '3' specified for the column in the database is either invalid or corrupted. Valid encryption types for algorithm 'AEAD_AES_256_CBC_HMAC_SHA256' are: 'Deterministic', 'Randomized'.\r\nParameter name: encryptionType";
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => Utility.DecryptWithKey(cipherText, cipherMD, "testsrv"));
            Assert.Contains(expectedMessage, e.InnerException.Message);

            e = Assert.Throws<TargetInvocationException>(() => Utility.EncryptWithKey(plainText, cipherMD, "testsrv"));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidCipherText() {
            // Attempt to decrypt 53 random bytes
            string expectedMessage = "Specified ciphertext has an invalid size of 53 bytes, which is below the minimum 65 bytes required for decryption.\r\nParameter name: cipherText";
            byte[] cipherText = Utility.GenerateRandomBytes(53); // minimum length is 65
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => Utility.DecryptDataUsingAED(cipherText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidAlgorithmVersion() {
            string expectedMessage = "The specified ciphertext's encryption algorithm version '40' does not match the expected encryption algorithm version '01'.\r\nParameter name: cipherText";
            byte[] plainText = Encoding.Unicode.GetBytes("Hello World");
            byte[] cipherText = Utility.EncryptDataUsingAED(plainText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic);
            // Put a version number of 0x10
            cipherText[0] = 0x40;
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => Utility.DecryptDataUsingAED(cipherText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestInvalidAuthenticationTag() {
            string expectedMessage = "Specified ciphertext has an invalid authentication tag.\r\nParameter name: cipherText";
            byte[] plainText = Encoding.Unicode.GetBytes("Hello World");
            byte[] cipherText = Utility.EncryptDataUsingAED(plainText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic);
            // Zero out 4 bytes of authentication tag
            for (int i =0; i < 4; i++) {
                cipherText[1] = 0x00;
            }
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => Utility.DecryptDataUsingAED(cipherText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic));
            Assert.Contains(expectedMessage, e.InnerException.Message);
        }

        [Fact]
        [ActiveIssue(9658)]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestNullColumnEncryptionAlgorithm () {
            string expectedMessage = "Internal error. Encryption algorithm cannot be null. Valid algorithms are: 'AES_256_CBC', 'AEAD_AES_256_CBC_HMAC_SHA256'.\r\nParameter name: encryptionAlgorithm";
            Object cipherMD = Utility.GetSqlCipherMetadata (0, 0, null, 1, 0x01);
            Utility.AddEncryptionKeyToCipherMD (cipherMD, CertFixture.encryptedCek, 0, 0, 0, new byte[]{0x01, 0x02, 0x03}, CertFixture.certificatePath, "MSSQL_CERTIFICATE_STORE", "RSA_OAEP");
            byte[] plainText = Encoding.Unicode.GetBytes("HelloWorld");
            byte[] cipherText = Utility.EncryptDataUsingAED (plainText, CertFixture.cek, Utility.CColumnEncryptionType.Deterministic);

            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => Utility.DecryptWithKey(cipherText, cipherMD, "testsrv"));
            Assert.Contains(expectedMessage, e.InnerException.Message);
            e = Assert.Throws<TargetInvocationException>(() => Utility.EncryptWithKey(plainText, cipherMD, "testsrv"));
            Assert.Contains(expectedMessage, e.InnerException.Message);
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




// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class ExceptionsCertStore : IClassFixture<ExceptionCertFixture>
    {
        private readonly string masterKeyEncAlgo = "RSA_OAEP";

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void EmptyCertificateThumbprint()
        {
            string dummyPath = string.Format("CurrentUser/My/");
            string expectedMessage = string.Format(@"Empty certificate thumbprint specified in certificate path '{0}'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?", dummyPath);

            ArgumentException e = Assert.Throws<ArgumentException>(() => ExceptionCertFixture.certStoreProvider.EncryptColumnEncryptionKey(dummyPath, masterKeyEncAlgo, ExceptionCertFixture.encryptedCek));
            Assert.Matches(expectedMessage, e.Message);

            expectedMessage = string.Format(@"Internal error. Empty certificate thumbprint specified in certificate path '{0}'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?", dummyPath);
            e = Assert.Throws<ArgumentException>(() => ExceptionCertFixture.certStoreProvider.DecryptColumnEncryptionKey(dummyPath, masterKeyEncAlgo, ExceptionCertFixture.encryptedCek));
            Assert.Matches(expectedMessage, e.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void CertificateNotFound()
        {
            string dummyPath = string.Format("CurrentUser/My/JunkThumbprint");
            string expectedMessage = string.Format(@"Certificate with thumbprint 'JunkThumbprint' not found in certificate store 'My' in certificate location 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?");
            ArgumentException e = Assert.Throws<ArgumentException>(() => ExceptionCertFixture.certStoreProvider.EncryptColumnEncryptionKey(dummyPath, masterKeyEncAlgo, ExceptionCertFixture.encryptedCek));
            Assert.Matches(expectedMessage, e.Message);

            expectedMessage = string.Format(@"Certificate with thumbprint 'JunkThumbprint' not found in certificate store 'My' in certificate location 'CurrentUser'. Verify the certificate path in the column master key definition in the database is correct, and the certificate has been imported correctly into the certificate location/store.\s+\(?Parameter (name: )?'?masterKeyPath('\))?");
            e = Assert.Throws<ArgumentException>(() => ExceptionCertFixture.certStoreProvider.DecryptColumnEncryptionKey(dummyPath, masterKeyEncAlgo, ExceptionCertFixture.encryptedCek));
            Assert.Matches(expectedMessage, e.Message);
        }

#if NET46
        [Fact]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp)]
        public void CertificateWithNoPrivateKey()
        {
            string expectedMessage = string.Format("Certificate specified in key path '{0}' does not have a private key to encrypt a column encryption key. Verify the certificate is imported correctly.\r\nParameter name: masterKeyPath", ExceptionCertFixture.masterKeyPathNPK);
            ArgumentException e = Assert.Throws<ArgumentException>(() => 
                ExceptionCertFixture.certStoreProvider.EncryptColumnEncryptionKey(
                ExceptionCertFixture.masterKeyPathNPK, masterKeyEncAlgo, ExceptionCertFixture.encryptedCek));
            Assert.Contains(expectedMessage, e.Message);

            expectedMessage = string.Format("Certificate specified in key path '{0}' does not have a private key to decrypt a column encryption key. Verify the certificate is imported correctly.\r\nParameter name: masterKeyPath", ExceptionCertFixture.masterKeyPathNPK);
            e = Assert.Throws<ArgumentException>(() => 
                ExceptionCertFixture.certStoreProvider.DecryptColumnEncryptionKey(
                ExceptionCertFixture.masterKeyPathNPK, masterKeyEncAlgo, ExceptionCertFixture.encryptedCek));
            Assert.Contains(expectedMessage, e.Message);
        }
#endif
    }
    public class ExceptionCertFixture : IDisposable
    {
        public static readonly SqlColumnEncryptionCertificateStoreProvider certStoreProvider = new SqlColumnEncryptionCertificateStoreProvider();
        public static X509Certificate2 certificate;
        public static string certificatePath;
        public static string thumbprint;
        public static byte[] cek;
        public static byte[] encryptedCek;
#if NET46
        public static X509Certificate2 masterKeyCertificateNPK; // no private key
        public static string thumbprintNPK; // No private key
        public static string masterKeyPathNPK;
#endif

        public ExceptionCertFixture()
        {
            certificate = Utility.CreateCertificate();
            thumbprint = certificate.Thumbprint;
            certificatePath = string.Format("CurrentUser/My/{0}", thumbprint);
            cek = Utility.GenerateRandomBytes(32);
            encryptedCek = certStoreProvider.EncryptColumnEncryptionKey(certificatePath, "RSA_OAEP", cek);
#if NET46
            masterKeyCertificateNPK = Utility.CreateCertificateWithNoPrivateKey();
            thumbprintNPK = masterKeyCertificateNPK.Thumbprint;
            masterKeyPathNPK = "CurrentUser/My/" + thumbprintNPK;
#endif
            // Disable the cache to avoid false failures.
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;
        }

        public void Dispose()
        {
            // Do NOT remove certificate for concurrent consistency. Certificates are used for other test cases as well.
        }
    }
}

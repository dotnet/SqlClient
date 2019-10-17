using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using System.Text;
using Xunit;
using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    [PlatformSpecific(TestPlatforms.Windows)]
    public class ExceptionTestAKVStore : IClassFixture<SQLSetupStrategy>
    {
        private const string MasterKeyEncAlgo = "RSA_OAEP";
        private const string BadMasterKeyEncAlgo = "BadMasterKeyAlgorithm";

        private SQLSetupStrategy fixture;

        private byte[] cek;
        private byte[] encryptedCek;

        public ExceptionTestAKVStore(SQLSetupStrategy fixture)
        {
            this.fixture = fixture;
            cek = ColumnEncryptionKey.GenerateRandomBytes(ColumnEncryptionKey.KeySizeInBytes);
            encryptedCek = fixture.akvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, cek);

            // Disable the cache to avoid false failures.
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidKeyEncryptionAlgorithm()
        {
            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.akvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, BadMasterKeyEncAlgo, cek));
            Assert.Contains("Invalid key encryption algorithm specified: 'BadMasterKeyAlgorithm'. Expected value: 'RSA_OAEP' or 'RSA-OAEP'.\r\nParameter name: encryptionAlgorithm", ex1.Message);

            Exception ex2 = Assert.Throws<ArgumentException>(() => fixture.akvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, BadMasterKeyEncAlgo, cek));
            Assert.Contains("Invalid key encryption algorithm specified: 'BadMasterKeyAlgorithm'. Expected value: 'RSA_OAEP' or 'RSA-OAEP'.\r\nParameter name: encryptionAlgorithm", ex2.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void NullEncryptionAlgorithm()
        {
            Exception ex1 = Assert.Throws<ArgumentNullException>(() => fixture.akvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, null, cek));
            Assert.Contains("Internal error. Key encryption algorithm cannot be null.\r\nParameter name: encryptionAlgorithm", ex1.Message);
            Exception ex2 = Assert.Throws<ArgumentNullException>(() => fixture.akvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, null, cek));
            Assert.Contains("Key encryption algorithm cannot be null.\r\nParameter name: encryptionAlgorithm", ex2.Message);
        }


        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void EmptyColumnEncryptionKey()
        {
            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.akvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, new byte[] { }));
            Assert.Contains("Empty column encryption key specified.\r\nParameter name: columnEncryptionKey", ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void NullColumnEncryptionKey()
        {
            Exception ex1 = Assert.Throws<ArgumentNullException>(() => fixture.akvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, null));
            Assert.Contains("Column encryption key cannot be null.\r\nParameter name: columnEncryptionKey", ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void EmptyEncryptedColumnEncryptionKey()
        {
            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.akvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, new byte[] { }));
            Assert.Contains("Internal error. Empty encrypted column encryption key specified.\r\nParameter name: encryptedColumnEncryptionKey", ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void NullEncryptedColumnEncryptionKey()
        {
            Exception ex1 = Assert.Throws<ArgumentNullException>(() => fixture.akvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, null));
            Assert.Contains("Internal error. Encrypted column encryption key cannot be null.\r\nParameter name: encryptedColumnEncryptionKey", ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidAlgorithmVersion()
        {
            byte[] encrypteCekLocal = ColumnEncryptionKey.GenerateInvalidEncryptedCek(encryptedCek, ColumnEncryptionKey.ECEKCorruption.ALGORITHM_VERSION);
            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.akvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, encrypteCekLocal));
            Assert.Contains("Specified encrypted column encryption key contains an invalid encryption algorithm version '10'. Expected version is '01'.\r\nParameter name: encryptedColumnEncryptionKey", ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidCertificateSignature()
        {
            // Put an invalid signature
            byte[] encrypteCekLocal = ColumnEncryptionKey.GenerateInvalidEncryptedCek(encryptedCek, ColumnEncryptionKey.ECEKCorruption.SIGNATURE);
            string errorMessage = String.Format(
                "The specified encrypted column encryption key signature does not match the signature computed with the column master key (Asymmetric key in Azure Key Vault) in '{0}'. The encrypted column encryption key may be corrupt, or the specified path may be incorrect.\r\nParameter name: encryptedColumnEncryptionKey", DataTestUtility.AKVUrl);

            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.akvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, encrypteCekLocal));
            Assert.Contains(errorMessage, ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidCipherTextLength()
        {
            // Put an invalid signature
            byte[] encrypteCekLocal = ColumnEncryptionKey.GenerateInvalidEncryptedCek(encryptedCek, ColumnEncryptionKey.ECEKCorruption.CEK_LENGTH);
            string errorMessage = String.Format("The specified encrypted column encryption key's ciphertext length: 251 does not match the ciphertext length: 256 when using column master key (Azure Key Vault key) in '{0}'. The encrypted column encryption key may be corrupt, or the specified Azure Key Vault key path may be incorrect.\r\nParameter name: encryptedColumnEncryptionKey", DataTestUtility.AKVUrl);

            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.akvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, encrypteCekLocal));
            Assert.Contains(errorMessage, ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidSignatureInEncryptedCek()
        {
            byte[] encryptedCekLocal = ColumnEncryptionKey.GenerateInvalidEncryptedCek(encryptedCek, ColumnEncryptionKey.ECEKCorruption.SIGNATURE_LENGTH);
            string errorMessage = String.Format("The specified encrypted column encryption key's signature length: 249 does not match the signature length: 256 when using column master key (Azure Key Vault key) in '{0}'. The encrypted column encryption key may be corrupt, or the specified Azure Key Vault key path may be incorrect.\r\nParameter name: encryptedColumnEncryptionKey", DataTestUtility.AKVUrl);

            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.akvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, encryptedCekLocal));
            Assert.Contains(errorMessage, ex1.Message);
        }

        [Fact]
        public void InvalidURL()
        {
            char[] barePath = new char[32780];
            for (int i = 0; i < barePath.Length; i++)
            {
                barePath[i] = 'a';
            }

            string fakePath = new string(barePath);
            string errorMessage = String.Format("Invalid url specified: '{0}'.\r\nParameter name: masterKeyPath", fakePath);

            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.akvStoreProvider.EncryptColumnEncryptionKey(fakePath, MasterKeyEncAlgo, cek));
            Assert.Contains(errorMessage, ex1.Message);

            Exception ex2 = Assert.Throws<ArgumentException>(() => fixture.akvStoreProvider.DecryptColumnEncryptionKey(fakePath, MasterKeyEncAlgo, encryptedCek));
            Assert.Contains(errorMessage, ex2.Message);
        }

        [Fact]
        public void NullAKVKeyPath()
        {
            Exception ex1 = Assert.Throws<ArgumentNullException>(
                () => fixture.akvStoreProvider.EncryptColumnEncryptionKey(null, MasterKeyEncAlgo, cek));
            Assert.Contains("Azure Key Vault key path cannot be null.\r\nParameter name: masterKeyPath", ex1.Message);

            Exception ex2 = Assert.Throws<ArgumentNullException>(
                () => fixture.akvStoreProvider.DecryptColumnEncryptionKey(null, MasterKeyEncAlgo, encryptedCek));
            Assert.Contains("Internal error. Azure Key Vault key path cannot be null.\r\nParameter name: masterKeyPath", ex2.Message);
        }

        [Fact]
        public void InvalidCertificatePath()
        {
            string dummyPath = @"https://www.microsoft.com";
            string errorMessage = String.Format("Invalid Azure Key Vault key path specified: '{0}'. Valid trusted endpoints: vault.azure.net, vault.azure.cn, vault.usgovcloudapi.net, vault.microsoftazure.de.\r\nParameter name: masterKeyPath", dummyPath);
            
            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.akvStoreProvider.EncryptColumnEncryptionKey(dummyPath, MasterKeyEncAlgo, cek));
            Assert.Contains(errorMessage, ex1.Message);

            Exception ex2 = Assert.Throws<ArgumentException>(
                () => fixture.akvStoreProvider.DecryptColumnEncryptionKey(dummyPath, MasterKeyEncAlgo, encryptedCek));
            Assert.Contains(errorMessage, ex2.Message);
        }

        // [InlineData(true)] -> Enable with AE v2
        [InlineData(false)]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void AkvStoreProviderVerifyFunctionWithInvalidSignature(bool fEnclaveEnabled)
        {
            //sign the cmk
            byte[] cmkSignature = fixture.akvStoreProvider.SignColumnMasterKeyMetadata(DataTestUtility.AKVUrl, allowEnclaveComputations: fEnclaveEnabled);
            Assert.True(cmkSignature != null);

            // Expect failed verification for a toggle of enclaveEnabled bit
            if (fixture.akvStoreProvider.VerifyColumnMasterKeyMetadata(DataTestUtility.AKVUrl, allowEnclaveComputations: !fEnclaveEnabled, signature: cmkSignature))
            {
                Exception ex1 = Assert.Throws<ArgumentException>(() => { });
                Assert.Contains(@"Unable to verify Column Master Key signature using key store provider", ex1.Message);
            }

            // Prepare another cipherText buffer
            byte[] tamperedCmkSignature = new byte[cmkSignature.Length];
            Buffer.BlockCopy(cmkSignature, 0, tamperedCmkSignature, 0, tamperedCmkSignature.Length);

            // Corrupt one byte at a time 10 times
            RandomNumberGenerator rng = new RNGCryptoServiceProvider();
            byte[] randomIndexInCipherText = new byte[1];
            for (int i = 0; i < 10; i++)
            {
                Assert.True(fixture.akvStoreProvider.VerifyColumnMasterKeyMetadata(DataTestUtility.AKVUrl, allowEnclaveComputations: fEnclaveEnabled, signature: tamperedCmkSignature), @"tamperedCmkSignature before tampering should be verified without any problems.");

                int startingByteIndex = 0;
                rng.GetBytes(randomIndexInCipherText);

                tamperedCmkSignature[startingByteIndex + randomIndexInCipherText[0]] = (byte)(cmkSignature[startingByteIndex + randomIndexInCipherText[0]] + 1);

                // Expect failed verification for invalid signature bytes
                if (fixture.akvStoreProvider.VerifyColumnMasterKeyMetadata(DataTestUtility.AKVUrl, allowEnclaveComputations: fEnclaveEnabled, signature: tamperedCmkSignature))
                {
                    Exception ex1 = Assert.Throws<ArgumentException>(() => { });
                    Assert.Contains(@"Unable to verify Column Master Key signature using key store provider", ex1.Message);
                }

                // Fix up the corrupted byte
                tamperedCmkSignature[startingByteIndex + randomIndexInCipherText[0]] = cmkSignature[startingByteIndex + randomIndexInCipherText[0]];
            }
        }
    }
}

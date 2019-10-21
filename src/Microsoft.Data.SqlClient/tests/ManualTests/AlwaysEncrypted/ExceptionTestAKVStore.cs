// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class ExceptionTestAKVStore : IClassFixture<SQLSetupStrategyAzureKeyVault>
    {
        private const string MasterKeyEncAlgo = "RSA_OAEP";
        private const string BadMasterKeyEncAlgo = "BadMasterKeyAlgorithm";

        private SQLSetupStrategyAzureKeyVault fixture;

        private byte[] cek;
        private byte[] encryptedCek;

        public ExceptionTestAKVStore(SQLSetupStrategyAzureKeyVault fixture)
        {
            this.fixture = fixture;
            cek = ColumnEncryptionKey.GenerateRandomBytes(ColumnEncryptionKey.KeySizeInBytes);
            encryptedCek = fixture.AkvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, cek);

            // Disable the cache to avoid false failures.
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidKeyEncryptionAlgorithm()
        {
            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.AkvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, BadMasterKeyEncAlgo, cek));
            Assert.Contains($"Invalid key encryption algorithm specified: 'BadMasterKeyAlgorithm'. Expected value: 'RSA_OAEP' or 'RSA-OAEP'.{Environment.NewLine}Parameter name: encryptionAlgorithm", ex1.Message);

            Exception ex2 = Assert.Throws<ArgumentException>(() => fixture.AkvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, BadMasterKeyEncAlgo, cek));
            Assert.Contains($"Invalid key encryption algorithm specified: 'BadMasterKeyAlgorithm'. Expected value: 'RSA_OAEP' or 'RSA-OAEP'.{Environment.NewLine}Parameter name: encryptionAlgorithm", ex2.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void NullEncryptionAlgorithm()
        {
            Exception ex1 = Assert.Throws<ArgumentNullException>(() => fixture.AkvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, null, cek));
            Assert.Contains($"Internal error. Key encryption algorithm cannot be null.{Environment.NewLine}Parameter name: encryptionAlgorithm", ex1.Message);
            Exception ex2 = Assert.Throws<ArgumentNullException>(() => fixture.AkvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, null, cek));
            Assert.Contains($"Key encryption algorithm cannot be null.{Environment.NewLine}Parameter name: encryptionAlgorithm", ex2.Message);
        }


        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void EmptyColumnEncryptionKey()
        {
            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.AkvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, new byte[] { }));
            Assert.Contains($"Empty column encryption key specified.{Environment.NewLine}Parameter name: columnEncryptionKey", ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void NullColumnEncryptionKey()
        {
            Exception ex1 = Assert.Throws<ArgumentNullException>(() => fixture.AkvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, null));
            Assert.Contains($"Column encryption key cannot be null.{Environment.NewLine}Parameter name: columnEncryptionKey", ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void EmptyEncryptedColumnEncryptionKey()
        {
            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.AkvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, new byte[] { }));
            Assert.Contains($"Internal error. Empty encrypted column encryption key specified.{Environment.NewLine}Parameter name: encryptedColumnEncryptionKey", ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void NullEncryptedColumnEncryptionKey()
        {
            Exception ex1 = Assert.Throws<ArgumentNullException>(() => fixture.AkvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, null));
            Assert.Contains($"Internal error. Encrypted column encryption key cannot be null.{Environment.NewLine}Parameter name: encryptedColumnEncryptionKey", ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidAlgorithmVersion()
        {
            byte[] encrypteCekLocal = ColumnEncryptionKey.GenerateInvalidEncryptedCek(encryptedCek, ColumnEncryptionKey.ECEKCorruption.ALGORITHM_VERSION);
            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.AkvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, encrypteCekLocal));
            Assert.Contains($"Specified encrypted column encryption key contains an invalid encryption algorithm version '10'. Expected version is '01'.{Environment.NewLine}Parameter name: encryptedColumnEncryptionKey", ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidCertificateSignature()
        {
            // Put an invalid signature
            byte[] encrypteCekLocal = ColumnEncryptionKey.GenerateInvalidEncryptedCek(encryptedCek, ColumnEncryptionKey.ECEKCorruption.SIGNATURE);
            string errorMessage =
                $"The specified encrypted column encryption key signature does not match the signature computed with the column master key (Asymmetric key in Azure Key Vault) in '{DataTestUtility.AKVUrl}'. The encrypted column encryption key may be corrupt, or the specified path may be incorrect.{Environment.NewLine}Parameter name: encryptedColumnEncryptionKey";

            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.AkvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, encrypteCekLocal));
            Assert.Contains(errorMessage, ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidCipherTextLength()
        {
            // Put an invalid signature
            byte[] encrypteCekLocal = ColumnEncryptionKey.GenerateInvalidEncryptedCek(encryptedCek, ColumnEncryptionKey.ECEKCorruption.CEK_LENGTH);
            string errorMessage = $"The specified encrypted column encryption key's ciphertext length: 251 does not match the ciphertext length: 256 when using column master key (Azure Key Vault key) in '{DataTestUtility.AKVUrl}'. The encrypted column encryption key may be corrupt, or the specified Azure Key Vault key path may be incorrect.{Environment.NewLine}Parameter name: encryptedColumnEncryptionKey";

            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.AkvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, encrypteCekLocal));
            Assert.Contains(errorMessage, ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidSignatureInEncryptedCek()
        {
            byte[] encryptedCekLocal = ColumnEncryptionKey.GenerateInvalidEncryptedCek(encryptedCek, ColumnEncryptionKey.ECEKCorruption.SIGNATURE_LENGTH);
            string errorMessage = $"The specified encrypted column encryption key's signature length: 249 does not match the signature length: 256 when using column master key (Azure Key Vault key) in '{DataTestUtility.AKVUrl}'. The encrypted column encryption key may be corrupt, or the specified Azure Key Vault key path may be incorrect.{Environment.NewLine}Parameter name: encryptedColumnEncryptionKey";

            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.AkvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, MasterKeyEncAlgo, encryptedCekLocal));
            Assert.Contains(errorMessage, ex1.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidURL()
        {
            char[] barePath = new char[32780];
            for (int i = 0; i < barePath.Length; i++)
            {
                barePath[i] = 'a';
            }

            string fakePath = new string(barePath);
            string errorMessage = $"Invalid url specified: '{fakePath}'.{Environment.NewLine}Parameter name: masterKeyPath";

            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.AkvStoreProvider.EncryptColumnEncryptionKey(fakePath, MasterKeyEncAlgo, cek));
            Assert.Contains(errorMessage, ex1.Message);

            Exception ex2 = Assert.Throws<ArgumentException>(() => fixture.AkvStoreProvider.DecryptColumnEncryptionKey(fakePath, MasterKeyEncAlgo, encryptedCek));
            Assert.Contains(errorMessage, ex2.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void NullAKVKeyPath()
        {
            Exception ex1 = Assert.Throws<ArgumentNullException>(
                () => fixture.AkvStoreProvider.EncryptColumnEncryptionKey(null, MasterKeyEncAlgo, cek));
            Assert.Contains($"Azure Key Vault key path cannot be null.{Environment.NewLine}Parameter name: masterKeyPath", ex1.Message);

            Exception ex2 = Assert.Throws<ArgumentNullException>(
                () => fixture.AkvStoreProvider.DecryptColumnEncryptionKey(null, MasterKeyEncAlgo, encryptedCek));
            Assert.Contains($"Internal error. Azure Key Vault key path cannot be null.{Environment.NewLine}Parameter name: masterKeyPath", ex2.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void InvalidCertificatePath()
        {
            string dummyPath = @"https://www.microsoft.com";
            string errorMessage = $"Invalid Azure Key Vault key path specified: '{dummyPath}'. Valid trusted endpoints: vault.azure.net, vault.azure.cn, vault.usgovcloudapi.net, vault.microsoftazure.de.{Environment.NewLine}Parameter name: masterKeyPath";
            
            Exception ex1 = Assert.Throws<ArgumentException>(() => fixture.AkvStoreProvider.EncryptColumnEncryptionKey(dummyPath, MasterKeyEncAlgo, cek));
            Assert.Contains(errorMessage, ex1.Message);

            Exception ex2 = Assert.Throws<ArgumentException>(
                () => fixture.AkvStoreProvider.DecryptColumnEncryptionKey(dummyPath, MasterKeyEncAlgo, encryptedCek));
            Assert.Contains(errorMessage, ex2.Message);
        }

        // [InlineData(true)] -> Enable with AE v2
        [InlineData(false)]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void AkvStoreProviderVerifyFunctionWithInvalidSignature(bool fEnclaveEnabled)
        {
            //sign the cmk
            byte[] cmkSignature = fixture.AkvStoreProvider.SignColumnMasterKeyMetadata(DataTestUtility.AKVUrl, allowEnclaveComputations: fEnclaveEnabled);
            Assert.True(cmkSignature != null);

            // Expect failed verification for a toggle of enclaveEnabled bit
            if (fixture.AkvStoreProvider.VerifyColumnMasterKeyMetadata(DataTestUtility.AKVUrl, allowEnclaveComputations: !fEnclaveEnabled, signature: cmkSignature))
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
                Assert.True(fixture.AkvStoreProvider.VerifyColumnMasterKeyMetadata(DataTestUtility.AKVUrl, allowEnclaveComputations: fEnclaveEnabled, signature: tamperedCmkSignature), @"tamperedCmkSignature before tampering should be verified without any problems.");

                int startingByteIndex = 0;
                rng.GetBytes(randomIndexInCipherText);

                tamperedCmkSignature[startingByteIndex + randomIndexInCipherText[0]] = (byte)(cmkSignature[startingByteIndex + randomIndexInCipherText[0]] + 1);

                // Expect failed verification for invalid signature bytes
                if (fixture.AkvStoreProvider.VerifyColumnMasterKeyMetadata(DataTestUtility.AKVUrl, allowEnclaveComputations: fEnclaveEnabled, signature: tamperedCmkSignature))
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

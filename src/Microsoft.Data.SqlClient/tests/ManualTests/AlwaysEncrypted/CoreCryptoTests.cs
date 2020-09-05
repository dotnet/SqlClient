using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class CoreCryptoTests : IClassFixture<SQLSetupStrategyCertStoreProvider>
    {
        // Synapse: Always Encrypted not supported in Azure Synapse.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestAeadCryptoWithNativeBaseline()
        {
            // Initialize the reader for resource text file which has the native code generated baseline.
            CryptoNativeBaselineReader cryptoNativeBaselineReader = new CryptoNativeBaselineReader();

            // Read and initialized the crypto vectors from the resource text file.
            cryptoNativeBaselineReader.InitializeCryptoVectors();

            IList<CryptoVector> cryptoParametersListForTest = cryptoNativeBaselineReader.CryptoVectors;

            Assert.True(cryptoParametersListForTest.Count >= 1, @"Invalid number of AEAD test vectors. Expected at least 1.");

            // For each crypto vector, run the test to compare the output generated through sqlclient's code and the native code.
            foreach (CryptoVector cryptoParameter in cryptoParametersListForTest)
            {
                // For Deterministic encryption, compare the result of encrypting the cell data (or plain text).
                if (cryptoParameter.CryptoVectorEncryptionTypeVal == CryptoVectorEncryptionType.Deterministic)
                {
                    TestEncryptionResultUsingAead(cryptoParameter.PlainText,
                                                  cryptoParameter.RootKey,
                                                  cryptoParameter.CryptoVectorEncryptionTypeVal == CryptoVectorEncryptionType.Deterministic ? CertificateUtility.CColumnEncryptionType.Deterministic : CertificateUtility.CColumnEncryptionType.Randomized,
                                                  cryptoParameter.FinalCell);
                }

                // For Randomized and Deterministic encryption, try the decryption of final cell value and compare against the native code baseline's plain text.
                TestDecryptionResultUsingAead(cryptoParameter.FinalCell,
                                              cryptoParameter.RootKey,
                                              cryptoParameter.CryptoVectorEncryptionTypeVal == CryptoVectorEncryptionType.Deterministic ? CertificateUtility.CColumnEncryptionType.Deterministic : CertificateUtility.CColumnEncryptionType.Randomized,
                                              cryptoParameter.PlainText);
            }
        }

        // Synapse: Always Encrypted not supported in Azure Synapse.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestRsaCryptoWithNativeBaseline()
        {
            // Initialize the reader for resource text file which has the native code generated baseline.
            CryptoNativeBaselineReader cryptoNativeBaselineReader = new CryptoNativeBaselineReader();

            // Read and initialized the crypto vectors from the resource text file.
            cryptoNativeBaselineReader.InitializeCryptoVectors(CryptNativeTestVectorType.Rsa);

            IList<CryptoVector> cryptoParametersListForTest = cryptoNativeBaselineReader.CryptoVectors;

            Assert.True(cryptoParametersListForTest.Count >= 3, @"Invalid number of RSA test vectors. Expected at least 3 (RSA Keypair + PFX + test vectors).");
            Assert.True(cryptoParametersListForTest[0].CryptNativeTestVectorTypeVal == CryptNativeTestVectorType.RsaKeyPair, @"First entry must be an RSA key pair.");
            Assert.True(cryptoParametersListForTest[1].CryptNativeTestVectorTypeVal == CryptNativeTestVectorType.RsaPfx, @"2nd entry must be a PFX.");

            byte[] rsaKeyPair = cryptoParametersListForTest[0].RsaKeyPair;
            byte[] rsaPfx = cryptoParametersListForTest[1].RsaKeyPair;

            // For each crypto vector, run the test to compare the output generated through sqlclient's code and the native code.
            foreach (CryptoVector cryptoParameter in cryptoParametersListForTest)
            {
                if (cryptoParameter.CryptNativeTestVectorTypeVal == CryptNativeTestVectorType.Rsa)
                {
                    // Verify that we are using the right padding scheme for RSA encryption
                    byte[] plaintext = CertificateUtility.DecryptRsaDirectly(rsaPfx, cryptoParameter.CiphertextCek, @"Test");
                    Assert.True(cryptoParameter.PlaintextCek.SequenceEqual(plaintext), "Plaintext CEK Value does not match with the native code baseline.");

                    // Verify that the signed blob is conforming to our envelope (SHA-256, PKCS 1 padding)
                    bool signatureVerified = CertificateUtility.VerifyRsaSignatureDirectly(cryptoParameter.HashedCek, cryptoParameter.SignedCek, rsaPfx);
                    Assert.True(signatureVerified, "Plaintext CEK signature scheme does not match with the native code baseline.");

                    //// TODO:  Programmatically install the in-memory PFX into the right store (based on path) & use the public API
                    //plaintext = Utility.VerifyRsaSignature(cryptoParameter.PathCek, cryptoParameter.FinalcellCek, rsaPfx);
                    //CError.Compare(cryptoParameter.PlaintextCek.SequenceEqual(plaintext), "Plaintext CEK Value does not match with the native code baseline (end to end).");
                }
            }
        }


        /// <summary>
        /// Helper function to test the result of encryption using Aead.
        /// </summary>
        /// <param name="plainText"></param>
        /// <param name="rootKey"></param>
        /// <param name="encryptionType"></param>
        /// <param name="expectedFinalCellValue"></param>
        private void TestEncryptionResultUsingAead(byte[] plainText, byte[] rootKey, CertificateUtility.CColumnEncryptionType encryptionType, byte[] expectedFinalCellValue)
        {
            // Encrypt.
            byte[] encryptedCellData = CertificateUtility.EncryptDataUsingAED(plainText, rootKey, encryptionType);
            Debug.Assert(encryptedCellData != null && encryptedCellData.Length > 0);

            Assert.True(encryptedCellData.SequenceEqual(expectedFinalCellValue), "Final Cell Value does not match with the native code baseline.");
        }

        /// <summary>
        /// Helper function to test the result of decryption using Aead.
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="rootKey"></param>
        /// <param name="encryptionType"></param>
        /// <param name="expectedPlainText"></param>
        private void TestDecryptionResultUsingAead(byte[] cipherText, byte[] rootKey, CertificateUtility.CColumnEncryptionType encryptionType, byte[] expectedPlainText)
        {
            // Decrypt.
            byte[] decryptedCellData = CertificateUtility.DecryptDataUsingAED(cipherText, rootKey, encryptionType);
            Debug.Assert(decryptedCellData != null);

            Assert.True(decryptedCellData.SequenceEqual(expectedPlainText), "Decrypted cell data does not match with the native code baseline.");
        }
    }
}

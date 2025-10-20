using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class CoreCryptoTests
    {
        [Fact]
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

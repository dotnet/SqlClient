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

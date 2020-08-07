using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Azure.Identity;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public static class AKVUnitTests
    {
        const string EncryptionAlgorithm = "RSA_OAEP";
        public static readonly byte[] s_columnEncryptionKey = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public static void LegacyAuthenticationCallbackTest()
        {
            // SqlClientCustomTokenCredential implements legacy authentication callback to request access token at client-side.
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new SqlClientCustomTokenCredential());
            byte[] encryptedCek = akvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCek = akvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(s_columnEncryptionKey, decryptedCek);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public static void TokenCredentialTest()
        {
            ClientSecretCredential clientSecretCredential = new ClientSecretCredential(DataTestUtility.AKVTenantId, DataTestUtility.AKVClientId, DataTestUtility.AKVClientSecret);
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(clientSecretCredential);
            byte[] encryptedCek = akvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCek = akvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(s_columnEncryptionKey, decryptedCek);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public static void TokenCredentialRotationTest()
        {
            // SqlClientCustomTokenCredential implements legacy authentication callback to request access token at client-side.
            SqlColumnEncryptionAzureKeyVaultProvider oldAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new SqlClientCustomTokenCredential());

            ClientSecretCredential clientSecretCredential = new ClientSecretCredential(DataTestUtility.AKVTenantId, DataTestUtility.AKVClientId, DataTestUtility.AKVClientSecret);
            SqlColumnEncryptionAzureKeyVaultProvider newAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(clientSecretCredential);

            byte[] encryptedCekWithNewProvider = newAkvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCekWithOldProvider = oldAkvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCekWithNewProvider);
            Assert.Equal(s_columnEncryptionKey, decryptedCekWithOldProvider);

            byte[] encryptedCekWithOldProvider = oldAkvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCekWithNewProvider = newAkvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCekWithOldProvider);
            Assert.Equal(s_columnEncryptionKey, decryptedCekWithNewProvider);
        }
    }
}

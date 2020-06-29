using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Azure.Identity;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class AKVUnitTests
    {
        const string EncryptionAlgorithm = "RSA_OAEP";
        public static readonly byte[] ColumnEncryptionKey = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void BackwardCompatibilityWithAuthenticationCallbackWorks()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(AzureActiveDirectoryAuthenticationCallback);
            byte[] encryptedCek = akvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, ColumnEncryptionKey);
            byte[] decryptedCek = akvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(ColumnEncryptionKey, decryptedCek);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void TokenCredentialWorks()
        {
            ClientSecretCredential clientSecretCredential = new ClientSecretCredential(DataTestUtility.AKVTenantId, DataTestUtility.AKVClientId, DataTestUtility.AKVClientSecret);
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(clientSecretCredential);
            byte[] encryptedCek = akvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, ColumnEncryptionKey);
            byte[] decryptedCek = akvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(ColumnEncryptionKey, decryptedCek);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void IsCompatibleWithProviderUsingLegacyClient()
        {
            ClientSecretCredential clientSecretCredential = new ClientSecretCredential(DataTestUtility.AKVTenantId, DataTestUtility.AKVClientId, DataTestUtility.AKVClientSecret);
            SqlColumnEncryptionAzureKeyVaultProvider newAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(clientSecretCredential);
            SqlColumnEncryptionAzureKeyVaultProvider oldAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(AzureActiveDirectoryAuthenticationCallback);

            byte[] encryptedCekWithNewProvider = newAkvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, ColumnEncryptionKey);
            byte[] decryptedCekWithOldProvider = oldAkvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCekWithNewProvider);
            Assert.Equal(ColumnEncryptionKey, decryptedCekWithOldProvider);

            byte[] encryptedCekWithOldProvider = oldAkvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, ColumnEncryptionKey);
            byte[] decryptedCekWithNewProvider = newAkvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCekWithOldProvider);
            Assert.Equal(ColumnEncryptionKey, decryptedCekWithNewProvider);
        }

        public static async Task<string> AzureActiveDirectoryAuthenticationCallback(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            ClientCredential clientCred = new ClientCredential(DataTestUtility.AKVClientId, DataTestUtility.AKVClientSecret);
            AuthenticationResult result = await authContext.AcquireTokenAsync(resource, clientCred);
            if (result == null)
            {
                throw new InvalidOperationException($"Failed to retrieve an access token for {resource}");
            }

            return result.AccessToken;
        }
    }
}

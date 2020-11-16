using Azure.Identity;
using Microsoft.Data.Encryption.AzureKeyVaultProvider;
using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Threading.Tasks;
using Xunit;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.AzureKeyVaultProviderTests
{
    public class AzureKeyVaultProviderShould
    {
        const KeyEncryptionKeyAlgorithm EncryptionAlgorithm = KeyEncryptionKeyAlgorithm.RSA_OAEP;
        public static readonly byte[] ColumnEncryptionKey = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };


        [Fact]
        public void BackwardCompatibilityWithAuthenticationCallbackWorks()
        {
            AzureKeyVaultKeyStoreProvider akvProvider = new AzureKeyVaultKeyStoreProvider(AzureActiveDirectoryAuthenticationCallback);
            byte[] encryptedCek = akvProvider.WrapKey(keyEncryptionKeyPath, EncryptionAlgorithm, ColumnEncryptionKey);
            byte[] decryptedCek = akvProvider.UnwrapKey(keyEncryptionKeyPath, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(ColumnEncryptionKey, decryptedCek);
        }

        [Fact]
        public void TokenCredentialWorks()
        {
            AzureKeyVaultKeyStoreProvider akvProvider = new AzureKeyVaultKeyStoreProvider(new ClientSecretCredential(tenantId, clientId, clientSecret));
            byte[] encryptedCek = akvProvider.WrapKey(keyEncryptionKeyPath, EncryptionAlgorithm, ColumnEncryptionKey);
            byte[] decryptedCek = akvProvider.UnwrapKey(keyEncryptionKeyPath, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(ColumnEncryptionKey, decryptedCek);
        }

        [Fact]
        public void IsCompatibleWithProviderUsingLegacyClient()
        {
            AzureKeyVaultKeyStoreProvider newAkvProvider = new AzureKeyVaultKeyStoreProvider(new ClientSecretCredential(tenantId, clientId, clientSecret));
            SqlColumnEncryptionAzureKeyVaultProvider oldAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(AzureActiveDirectoryAuthenticationCallback);

            byte[] encryptedCekWithNewProvider = newAkvProvider.WrapKey(keyEncryptionKeyPath, EncryptionAlgorithm, ColumnEncryptionKey);
            byte[] decryptedCekWithOldProvider = oldAkvProvider.DecryptColumnEncryptionKey(keyEncryptionKeyPath, EncryptionAlgorithm.ToString(), encryptedCekWithNewProvider);
            Assert.Equal(ColumnEncryptionKey, decryptedCekWithOldProvider);

            byte[] encryptedCekWithOldProvider = oldAkvProvider.EncryptColumnEncryptionKey(keyEncryptionKeyPath, EncryptionAlgorithm.ToString(), ColumnEncryptionKey);
            byte[] decryptedCekWithNewProvider = newAkvProvider.UnwrapKey(keyEncryptionKeyPath, EncryptionAlgorithm, encryptedCekWithOldProvider);
            Assert.Equal(ColumnEncryptionKey, decryptedCekWithNewProvider);
        }

        public static async Task<string> AzureActiveDirectoryAuthenticationCallback(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            ClientCredential clientCred = new ClientCredential(clientId, clientSecret);
            AuthenticationResult result = await authContext.AcquireTokenAsync(resource, clientCred);
            if (result == null)
            {
                throw new InvalidOperationException($"Failed to retrieve an access token for {resource}");
            }

            return result.AccessToken;
        }
    }
}
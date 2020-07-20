using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Azure.Identity;
using Xunit;
using Azure.Core;
using System.Threading;
using System.Net.Http;
using System.Linq;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class AKVUnitTests
    {
        const string EncryptionAlgorithm = "RSA_OAEP";
        public static readonly byte[] ColumnEncryptionKey = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void BackwardCompatibilityWithAuthenticationCallbackWorks()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new ChallengeBasedTokenCredential());
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
            SqlColumnEncryptionAzureKeyVaultProvider oldAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new ChallengeBasedTokenCredential());

            byte[] encryptedCekWithNewProvider = newAkvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, ColumnEncryptionKey);
            byte[] decryptedCekWithOldProvider = oldAkvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCekWithNewProvider);
            Assert.Equal(ColumnEncryptionKey, decryptedCekWithOldProvider);

            byte[] encryptedCekWithOldProvider = oldAkvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, ColumnEncryptionKey);
            byte[] decryptedCekWithNewProvider = newAkvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCekWithOldProvider);
            Assert.Equal(ColumnEncryptionKey, decryptedCekWithNewProvider);
        }

        internal class ChallengeBasedTokenCredential : TokenCredential
        {
            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            private static string ValidateChallenge(string challenge)
            {
                string Bearer = "Bearer ";
                if (string.IsNullOrEmpty(challenge))
                    throw new ArgumentNullException(nameof(challenge));

                string trimmedChallenge = challenge.Trim();

                if (!trimmedChallenge.StartsWith(Bearer))
                    throw new ArgumentException("Challenge is not Bearer", nameof(challenge));

                return trimmedChallenge.Substring(Bearer.Length);
            }
        }
    }
}

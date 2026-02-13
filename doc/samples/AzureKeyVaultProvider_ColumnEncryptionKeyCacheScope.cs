using System.Collections.Generic;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;

namespace AzureKeyVaultProvider_ColumnEncryptionKeyCacheScope
{
    // <Snippet1>
    class Program
    {
        private static string connectionString;

        static void Main()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customKeyStoreProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
                    SqlColumnEncryptionAzureKeyVaultProvider azureKeyVaultProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new DefaultAzureCredential());
                    customKeyStoreProviders.Add(SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, azureKeyVaultProvider);
                    command.RegisterColumnEncryptionKeyStoreProvidersOnCommand(customKeyStoreProviders);
                    // Perform database operation using Azure Key Vault Provider
                    // Any decrypted column encryption keys will be cached
                } // Column encryption key cache of "azureKeyVaultProvider" is cleared when "azureKeyVaultProvider" goes out of scope
            }
        }
    }
    // </Snippet1>
}

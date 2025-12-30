namespace RegisterCustomKeyStoreProvider_Example;

// <Snippet1>
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using System.Collections.Generic;

class Program
{
    // Maps a SqlColumnEncryptionAzureKeyVaultProvider to some object that represents a user
    static Dictionary<object, SqlColumnEncryptionAzureKeyVaultProvider> providerByUser = new();

    void ExecuteSelectQuery(object user, SqlConnection connection)
    {
        // Check if the user already has a SqlColumnEncryptionAzureKeyVaultProvider
        SqlColumnEncryptionAzureKeyVaultProvider azureKeyVaultProvider = providerByUser[user];
        if (azureKeyVaultProvider is null)
        {
            // Create a new SqlColumnEncryptionAzureKeyVaultProvider with the user's credentials and save it for future use
            azureKeyVaultProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new DefaultAzureCredential());
            providerByUser[user] = azureKeyVaultProvider;
        }

        Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new();
        customProviders.Add(SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, azureKeyVaultProvider);

        using SqlCommand command = new("SELECT * FROM Customers", connection);
        command.RegisterColumnEncryptionKeyStoreProvidersOnCommand(customProviders);
        // Perform database operations
        // Any decrypted column encryption keys will be cached by azureKeyVaultProvider
    }

    void ExecuteUpdateQuery(object user, SqlConnection connection)
    {
        // Check if the user already has a SqlColumnEncryptionAzureKeyVaultProvider
        SqlColumnEncryptionAzureKeyVaultProvider azureKeyVaultProvider = providerByUser[user];
        if (azureKeyVaultProvider is null)
        {
            // Create a new SqlColumnEncryptionAzureKeyVaultProvider with the user's credentials and save it for future use
            azureKeyVaultProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new DefaultAzureCredential());
            providerByUser[user] = azureKeyVaultProvider;
        }

        Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new();
        customProviders.Add(SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, azureKeyVaultProvider);

        using SqlCommand command = new("UPDATE Customers SET Name = 'NewName' WHERE CustomerId = 1", connection);
        command.RegisterColumnEncryptionKeyStoreProvidersOnCommand(customProviders);
        // Perform database operations
        // Any decrypted column encryption keys will be cached by azureKeyVaultProvider
    }
}
// </Snippet1>

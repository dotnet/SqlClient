namespace RegisterCustomKeyStoreProvider_CommandPrecedence;

using System.Collections.Generic;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;

// <Snippet1>
class Program
{
    static void Main(string connectionString)
    {
        Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customKeyStoreProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
        MyCustomKeyStoreProvider firstProvider = new MyCustomKeyStoreProvider();
        customKeyStoreProviders.Add("FIRST_CUSTOM_STORE", firstProvider);
        // Registers the provider globally
        SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customKeyStoreProviders);

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            customKeyStoreProviders.Clear();
            MyCustomKeyStoreProvider secondProvider = new MyCustomKeyStoreProvider();
            customKeyStoreProviders.Add("SECOND_CUSTOM_STORE", secondProvider);
            // Registers the provider on the connection
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(customKeyStoreProviders);

            using (SqlCommand command = connection.CreateCommand())
            {
                customKeyStoreProviders.Clear();
                SqlColumnEncryptionAzureKeyVaultProvider azureKeyVaultProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new DefaultAzureCredential());
                customKeyStoreProviders.Add(SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, azureKeyVaultProvider);
                // Registers the provider on the command
                // These providers will take precedence over connection-level providers and globally registered providers
                command.RegisterColumnEncryptionKeyStoreProvidersOnCommand(customKeyStoreProviders);
            }
        }
    }
}
// </Snippet1>

class MyCustomKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
{
    public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
    {
        // Custom decryption logic here
        return new byte[0];
    }
    public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
    {
        // Custom encryption logic here
        return new byte[0];
    }
}

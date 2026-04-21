namespace RegisterCustomKeyStoreProvider_ConnectionPrecedence;

using System.Collections.Generic;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;

// <Snippet1>
class Program
{
    static void Main(string connectionString)
    {
        Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customKeyStoreProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
        MyCustomKeyStoreProvider myProvider = new MyCustomKeyStoreProvider();
        customKeyStoreProviders.Add("MY_CUSTOM_STORE", myProvider);
        // Registers the provider globally
        SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customKeyStoreProviders);

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            customKeyStoreProviders.Clear();
            SqlColumnEncryptionAzureKeyVaultProvider azureKeyVaultProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new DefaultAzureCredential());
            customKeyStoreProviders.Add(SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, azureKeyVaultProvider);
            // Registers the provider on the connection
            // These providers will take precedence over globally registered providers
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(customKeyStoreProviders);
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

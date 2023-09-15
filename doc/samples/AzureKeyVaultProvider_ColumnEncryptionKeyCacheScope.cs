// <Snippet1>
class Program
{
    static void Main()
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customKeyStoreProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
                SqlColumnEncryptionAzureKeyVaultProvider azureKeyVaultProvider = new SqlColumnEncryptionAzureKeyVaultProvider();
                customKeyStoreProviders.Add(SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, azureKeyVaultProvider);
                command.RegisterColumnEncryptionKeyStoreProvidersOnCommand(customKeyStoreProviders);
                // Perform database operation using Azure Key Vault Provider
                // Any decrypted column encryption keys will be cached
            } // Column encryption key cache of "azureKeyVaultProvider" is cleared when "azureKeyVaultProvider" goes out of scope
        }
    }
}
// </Snippet1>

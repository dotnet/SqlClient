// <Snippet1>
class Program
{
    static void Main()
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
                SqlColumnEncryptionAzureKeyVaultProvider azureKeyVaultProvider = new SqlColumnEncryptionAzureKeyVaultProvider();
                customKeyStoreProviders.Add(SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, azureKeyVaultProvider);
                // Registers the provider on the command
                // These providers will take precedence over connection-level providers and globally registered providers
                command.RegisterColumnEncryptionKeyStoreProvidersOnCommand(customKeyStoreProviders);
            }
        }
    }
}
// </Snippet1>

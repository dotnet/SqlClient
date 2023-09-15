// <Snippet1>
class Program
{
    static void Main()
    {
        Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customKeyStoreProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
        MyCustomKeyStoreProvider myProvider = new MyCustomKeyStoreProvider();
        customKeyStoreProviders.Add("MY_CUSTOM_STORE", myProvider);
        // Registers the provider globally
        SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customKeyStoreProviders);

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            customKeyStoreProviders.Clear();
            SqlColumnEncryptionAzureKeyVaultProvider azureKeyVaultProvider = new SqlColumnEncryptionAzureKeyVaultProvider();
            customKeyStoreProviders.Add(SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, azureKeyVaultProvider);
            // Registers the provider on the connection
            // These providers will take precedence over globally registered providers
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(customKeyStoreProviders);
        }
    }
}
// </Snippet1>

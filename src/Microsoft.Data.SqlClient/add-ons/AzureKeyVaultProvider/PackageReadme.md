# Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.svg?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider)

## Description

This library provides an **Always Encrypted Azure Key Vault Provider** for [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient). It enables .NET applications to use [Microsoft Azure Key Vault](https://azure.microsoft.com/services/key-vault/) with [Always Encrypted](https://aka.ms/AlwaysEncrypted) in Microsoft Azure SQL Database and Microsoft SQL Server.

Always Encrypted allows clients to encrypt sensitive data inside client applications and never reveal the encryption keys to SQL Server. This provider enables storing column master keys (CMKs) in Azure Key Vault, providing centralized key management, secure key storage, and integration with Azure AD authentication.

## Supportability

This package supports:

- .NET Framework 4.6.2+
- .NET 8.0+

## Installation

Install the package via NuGet:

```bash
dotnet add package Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
```

Or via the Package Manager Console:

```powershell
Install-Package Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
```

## Getting Started

### Register the Provider

Before you can use Azure Key Vault with Always Encrypted, you must register the provider globally or per-connection:

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Azure.Identity;

// Create the AKV provider using Azure.Identity (recommended)
var azureCredential = new DefaultAzureCredential();
var akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(azureCredential);

// Register globally (once per application)
SqlConnection.RegisterColumnEncryptionKeyStoreProviders(
    new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>
    {
        { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, akvProvider }
    });
```

### Use with a Connection

Enable Always Encrypted in your connection string:

```csharp
var connectionString = "Server=myserver;Database=mydb;Column Encryption Setting=Enabled;...";

using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

// Execute queries against encrypted columns - encryption/decryption is automatic
using var command = new SqlCommand("SELECT SSN FROM Customers WHERE Id = @id", connection);
command.Parameters.AddWithValue("@id", customerId);
var ssn = await command.ExecuteScalarAsync();
```

## Key Features

- **Azure Key Vault Integration**: Store and manage column master keys (CMKs) in Azure Key Vault
- **Azure AD Authentication**: Supports Azure.Identity credentials for seamless Azure AD authentication
- **Key Caching**: Built-in caching of column encryption keys (CEKs) for improved performance
- **Multiple Authentication Methods**: Supports DefaultAzureCredential, ClientSecretCredential, ManagedIdentityCredential, and more

## Documentation

- [Always Encrypted Overview](https://learn.microsoft.com/sql/relational-databases/security/encryption/always-encrypted-database-engine)
- [Configure Always Encrypted with Azure Key Vault](https://learn.microsoft.com/sql/relational-databases/security/encryption/configure-always-encrypted-keys-using-powershell)
- [Microsoft.Data.SqlClient Documentation](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)
- [Azure Key Vault Documentation](https://learn.microsoft.com/azure/key-vault/)

## Release Notes

Release notes are available at: https://go.microsoft.com/fwlink/?linkid=2090501

## License

This package is licensed under the [MIT License](https://licenses.nuget.org/MIT).

## Related Packages

- [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient) - The main SqlClient driver
- [Azure.Identity](https://www.nuget.org/packages/Azure.Identity) - Azure AD authentication library
- [Azure.Security.KeyVault.Keys](https://www.nuget.org/packages/Azure.Security.KeyVault.Keys) - Azure Key Vault Keys client library

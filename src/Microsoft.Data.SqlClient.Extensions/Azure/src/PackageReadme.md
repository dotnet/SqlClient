# Microsoft.Data.SqlClient.Extensions.Azure

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Data.SqlClient.Extensions.Azure.svg?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Azure)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Microsoft.Data.SqlClient.Extensions.Azure?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Azure)

## Description

This package provides **Azure integration extensions** for [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient). It enables seamless authentication and connectivity with Azure SQL Database using Azure Identity and other Azure services.

## Key Features

- **Entra ID Authentication**: Simplified Entra ID token-based authentication
- **Managed Identity Support**: Connect to Azure SQL using Azure Managed Identities
- **Token Caching**: Automatic caching of authentication tokens for improved performance
- **Azure.Identity Integration**: Leverage the full power of Azure.Identity credential providers

## Supportability

This package supports:

- .NET Standard 2.0 (compatible with .NET Framework 4.6.1+, .NET Core 2.0+, and .NET 5+)

## Installation

Install the package via NuGet:

```bash
dotnet add package Microsoft.Data.SqlClient.Extensions.Azure
```

Or via the Package Manager Console:

```powershell
Install-Package Microsoft.Data.SqlClient.Extensions.Azure
```

## Getting Started

### Connect Using Default Azure Credential

```csharp
using Microsoft.Data.SqlClient;
using Azure.Identity;

// Use DefaultAzureCredential for automatic authentication
// Works with Managed Identity, Azure CLI, Visual Studio, and more
var credential = new DefaultAzureCredential();

var connectionString = "Server=myserver.database.windows.net;Database=mydb;";

using var connection = new SqlConnection(connectionString);
connection.AccessTokenCallback = async (ctx, cancellationToken) =>
{
    var token = await credential.GetTokenAsync(
        new Azure.Core.TokenRequestContext(new[] { "https://database.windows.net/.default" }),
        cancellationToken);
    return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
};

await connection.OpenAsync();
```

### Connect Using Managed Identity

For applications running in Azure (App Service, Functions, VMs, AKS, etc.):

```csharp
using Microsoft.Data.SqlClient;
using Azure.Identity;

// Use system-assigned managed identity
var credential = new ManagedIdentityCredential();

// Or use user-assigned managed identity
// var credential = new ManagedIdentityCredential("client-id-of-user-assigned-identity");

var connectionString = "Server=myserver.database.windows.net;Database=mydb;";

using var connection = new SqlConnection(connectionString);
connection.AccessTokenCallback = async (ctx, cancellationToken) =>
{
    var token = await credential.GetTokenAsync(
        new Azure.Core.TokenRequestContext(new[] { "https://database.windows.net/.default" }),
        cancellationToken);
    return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
};

await connection.OpenAsync();
```

### Connect Using Service Principal

For service-to-service authentication:

```csharp
using Microsoft.Data.SqlClient;
using Azure.Identity;

var credential = new ClientSecretCredential(
    tenantId: "your-tenant-id",
    clientId: "your-client-id",
    clientSecret: "your-client-secret");

var connectionString = "Server=myserver.database.windows.net;Database=mydb;";

using var connection = new SqlConnection(connectionString);
connection.AccessTokenCallback = async (ctx, cancellationToken) =>
{
    var token = await credential.GetTokenAsync(
        new Azure.Core.TokenRequestContext(new[] { "https://database.windows.net/.default" }),
        cancellationToken);
    return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
};

await connection.OpenAsync();
```

## Authentication Methods

| Authentication Type | Credential Class | Use Case |
|---------------------|------------------|----------|
| Default | `DefaultAzureCredential` | Development and production with automatic credential chain |
| Managed Identity | `ManagedIdentityCredential` | Azure-hosted applications |
| Service Principal | `ClientSecretCredential` | Service-to-service authentication |
| Certificate | `ClientCertificateCredential` | Certificate-based service auth |
| Interactive | `InteractiveBrowserCredential` | Interactive user authentication |
| Azure CLI | `AzureCliCredential` | Local development |
| Visual Studio | `VisualStudioCredential` | Development in Visual Studio |

### Interactive authentication in windowless Windows hosts

On Windows, `Authentication=Active Directory Interactive` uses Web Account Manager
(WAM) with SqlClient's built-in Microsoft application ID. WAM requires a valid
parent window handle when interactive sign-in is needed. A local Azure Functions
worker can lack a console window even when its developer is using an interactive
desktop.

For windowless local development, use `Authentication=Active Directory Default`
with a configured developer credential, such as an Azure CLI or Visual Studio
sign-in. For Azure-hosted Functions, prefer managed identity.

If the application owns a suitable window, supply its handle through
`ActiveDirectoryAuthenticationProvider.SetParentActivityOrWindowFunc` before
opening connections. Do not use an unrelated application's window.

If interactive sign-in without WAM is required, register your own Entra application
and supply its client ID with `UseWamBroker = false`. Setting `UseWamBroker = false`
alone does **not** disable WAM for SqlClient's first-party application ID.
On modern .NET, configure a public client/native redirect URI of `http://localhost`
for system-browser authentication and the Azure SQL Database delegated
`user_impersonation` permission, with consent as required by your tenant. The signed-in
user must also have database access.

See the [custom application ID sample](https://github.com/dotnet/SqlClient/blob/main/doc/samples/ApplicationClientIdAzureAuthenticationProvider.cs)
and the [MSAL parent window requirements](https://learn.microsoft.com/entra/msal/dotnet/acquiring-tokens/desktop-mobile/wam#parent-window-handles).

## Documentation

- [Entra ID Authentication](https://learn.microsoft.com/azure/azure-sql/database/authentication-aad-overview)
- [Azure.Identity Documentation](https://learn.microsoft.com/dotnet/api/overview/azure/identity-readme)
- [Microsoft.Data.SqlClient Documentation](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)
- [Managed Identities Overview](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/overview)

## License

This package is licensed under the [MIT License](https://licenses.nuget.org/MIT).

## Related Packages

- [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient) - The main SqlClient driver
- [Microsoft.Data.SqlClient.Extensions.Abstractions](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Abstractions) - Core abstractions
- [Microsoft.Data.SqlClient.Internal.Logging](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Internal.Logging) - Logging internals
- [Azure.Identity](https://www.nuget.org/packages/Azure.Identity) - Entra ID authentication library
- 
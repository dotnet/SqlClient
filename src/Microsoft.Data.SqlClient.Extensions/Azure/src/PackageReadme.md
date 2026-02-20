# Microsoft.Data.SqlClient.Extensions.Azure

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Data.SqlClient.Extensions.Azure.svg?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Azure)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Microsoft.Data.SqlClient.Extensions.Azure?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Azure)

## Description

This package provides **Azure integration extensions** for [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient). It enables seamless authentication and connectivity with Azure SQL Database using Azure Identity and other Azure services.

## Key Features

- **Azure AD Authentication**: Simplified Azure Active Directory token-based authentication
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

## Documentation

- [Azure SQL Database Azure AD Authentication](https://learn.microsoft.com/azure/azure-sql/database/authentication-aad-overview)
- [Azure.Identity Documentation](https://learn.microsoft.com/dotnet/api/overview/azure/identity-readme)
- [Microsoft.Data.SqlClient Documentation](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)
- [Managed Identities Overview](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/overview)

## License

This package is licensed under the [MIT License](https://licenses.nuget.org/MIT).

## Related Packages

- [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient) - The main SqlClient driver
- [Microsoft.Data.SqlClient.Extensions.Abstractions](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Abstractions) - Core abstractions
- [Microsoft.Data.SqlClient.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Logging) - Logging extensions
- [Azure.Identity](https://www.nuget.org/packages/Azure.Identity) - Azure AD authentication library

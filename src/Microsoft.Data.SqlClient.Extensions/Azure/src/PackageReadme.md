# Microsoft.Data.SqlClient.Extensions.Azure

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Data.SqlClient.Extensions.Azure.svg?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Azure)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Microsoft.Data.SqlClient.Extensions.Azure?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Azure)

## Description

This package provides **Azure integration extensions** for [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient). It enables seamless authentication and connectivity with Azure SQL Database using Azure Identity and other Azure services.

## Key Features

- **Entra ID Authentication**: Simplified Entra ID token-based authentication
- **Managed Identity Support**: Connect to Azure SQL using Azure Managed Identities
- **Token Caching**: Automatic caching of authentication tokens for improved performance
- **Azure Identity Integration**: Use Azure.Identity credential types supplied by Azure.Core

## Supportability

This package targets:

- .NET Framework 4.6.2 (`net462`)
- .NET Standard 2.0 (`netstandard2.0`)

When used with the current Microsoft.Data.SqlClient driver, supported runtimes are
.NET Framework 4.6.2+ and .NET 10.0+. The .NET Standard target does not extend driver runtime support.

## Installation

Install the package via NuGet:

```bash
dotnet add package Microsoft.Data.SqlClient.Extensions.Azure
```

Or via the Package Manager Console:

```powershell
Install-Package Microsoft.Data.SqlClient.Extensions.Azure
```

### Azure.Core migration

This package uses Azure.Core 1.62.0 or later instead of depending on Azure.Identity.
Credential types remain in the `Azure.Identity` namespace, so existing `using` directives
do not need to change.

If your application references Azure.Identity older than 1.21.0, remove that reference
or upgrade it to 1.21.0 or later to avoid duplicate-type errors (CS0433). If another
dependency brings in an older Azure.Identity transitively, add a direct reference to
Azure.Identity 1.21.0 or later, which forwards its types to Azure.Core. Applications
using Azure.Identity.Broker must also upgrade it to 1.6.0 or later.

See the [Azure SDK migration guide](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/MigrationGuide.md)
for details.

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

## Authentication Logging

The built-in Entra ID provider writes non-PII MSAL, Azure.Identity, and SqlClient token acquisition
diagnostics to `Microsoft.Data.SqlClient.EventSource` under the **AuthenticationTrace** keyword
(`0x2000`). No code change or logging callback is needed. Collectors choose the level and can attach
to a running application.

Collect authentication events only, at Verbose level:

```bash
dotnet-trace collect --process-id <pid> \
  --providers Microsoft.Data.SqlClient.EventSource:0x2000:5 \
  --rundown false
```

| Keyword mask | Collects |
|---|---|
| `0x2000` | Authentication events only |
| `0x2` | Ordinary SqlClient Trace events only |
| `0x2002` | Both |

Use level `4` for Informational and above, or `5` for Verbose. Each event has `message`,
`source` (`MSAL`, `Azure-Identity`, or `SqlClient`), and `connectionId`.
`dotnet-trace` works on modern .NET. On .NET Framework, use an ETW collector or an in-process
`EventListener` with the same keyword and level.

Privacy:

- PII logging stays disabled for MSAL. There is no PII opt-in.
- For Default, Managed Identity, Service Principal, and Workload Identity, Azure.Identity unsafe
  support logging, account-identifier logging, and HTTP content logging stay disabled.
- Only known Azure.Identity events are forwarded. Raw SDK exception payloads are not forwarded.
- Treat trace files as sensitive operational data.

Known limitations:

- `DefaultAzureCredential` logs one `Azure credential token request failed.` error for each
  credential it tries and skips, even when a later credential succeeds. Use the
  `DefaultAzureCredential selected ...` event and the final SqlClient result to read the outcome.
- MSAL error messages can include exception text and stack details produced by MSAL.
- Azure.Identity token failures are matched to SqlClient's request by its request ID. MSAL log text
  has no such ID, so it is matched by async context only. If an app `EventListener` for
  `Azure-Identity` runs its own credential while handling an event, that credential's MSAL text can
  appear under the SQL connection.
- Credentials created by the application for `AccessTokenCallback` are not covered.
  Use Azure.Identity logging for those credentials.
- Cached tokens and pooled connections can skip authentication, so an open does not always
  produce events.

Samples:
[dotnet-trace](https://github.com/dotnet/SqlClient/blob/main/doc/samples/SqlAuthenticationLogging_Example.cs) and
[EventListener](https://github.com/dotnet/SqlClient/blob/main/doc/samples/SqlAuthenticationEventListener_Example.cs).
See [issue #1846](https://github.com/dotnet/SqlClient/issues/1846).

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
- [Azure.Core](https://www.nuget.org/packages/Azure.Core) - Azure SDK primitives and Entra ID credentials
- 
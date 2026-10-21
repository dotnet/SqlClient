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

The built-in Entra ID provider automatically forwards non-PII authentication diagnostics to
`Microsoft.Data.SqlClient.EventSource` when its **Trace keyword (2)** is enabled. No custom provider
registration is needed for this collection, including Verbose diagnostics when requested by the collector.

To receive callbacks, configure the provider before
opening connections:

```csharp
using System.Diagnostics.Tracing;
using Microsoft.Data.SqlClient;

var provider = new ActiveDirectoryAuthenticationProvider(
    new ActiveDirectoryAuthenticationProviderOptions
    {
        EnableMsalPiiLogging = false,
        AuthenticationLoggingCallback = entry =>
        {
            if (!entry.ContainsPii && entry.Level <= EventLevel.Informational)
            {
                Console.WriteLine(
                    $"[{entry.Level}] [{entry.Source}] [{entry.ConnectionId}] {entry.Message}");
            }
        }
    });

SqlAuthenticationProvider.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryDefault, provider);
```

`AuthenticationLoggingCallback` receives direct MSAL messages, a reviewed non-PII subset of
Azure Identity diagnostics, and SqlClient authentication lifecycle diagnostics. It does not receive
every existing provider trace or diagnostics from application-created credentials.

Register the provider for each authentication method that needs these settings. Callbacks and
EventSource are independent: either or both can be used. A configured callback receives every eligible
severity, including Verbose. Consumers control filtering using `entry.Level`, as the example does.
Consumer filtering does not avoid upstream SDK message formatting or callback entry allocation.
EventSource collectors independently control tracing through their keyword and level settings.
`SqlAuthenticationLogEntry` is owned by SqlClient and its `Level` uses the framework
`System.Diagnostics.Tracing.EventLevel` enum. Consuming the logging API does not require MSAL types.

Leave `AuthenticationLoggingCallback` null to disable callback delivery.
Authentication EventSource collection remains controlled by the collector.

Callbacks must execute synchronously, be thread-safe, return promptly, and not throw exceptions.
Do not use async lambdas (`async void`):
exceptions after an await cannot be isolated. For asynchronous sinks, synchronously enqueue into an
application-owned bounded buffer and observe processing failures in its consumer. Do not start
authentication synchronously from a logging callback.

As a defensive safeguard, the provider catches nonfatal synchronous callback exceptions, reports a
sanitized EventSource diagnostic, and allows authentication to continue.
Callback-induced logging is not forwarded recursively. Settings are copied when the
provider is constructed. To change the callback or PII option, construct a new provider and register it
using `SqlAuthenticationProvider.SetProvider` for each affected authentication method. Replacing the provider
preserves the same identity's signed-in token state. PII-capable and non-PII clients coordinate their caches without broadening
sharing across identities. Cached clients route logs to the current
acquisition and do not retain application logging callbacks.

PII logging is disabled by default. `EnableMsalPiiLogging = true` permits PII-bearing messages **only
in the callback for direct MSAL flows** (Integrated, Password, Interactive, and Device Code Flow).
The authentication bridge never forwards PII-marked messages to SqlClient EventSource. Obtain
appropriate consent and protect sensitive callback output when opting in.

For Default, Managed Identity/MSI, Service Principal, and Workload Identity, the provider forwards
only a supported, scoped subset of `Azure-Identity` diagnostics, including MSAL messages where
the credential uses MSAL, credential selection, and sanitized failures. Unsafe support logging,
account-identifier logging, and HTTP content logging remain disabled, even with PII opt-in.
Unknown SDK events and raw SDK exception payloads are not forwarded. Application-created
credentials used in `AccessTokenCallback` are outside this bridge and require their own logging.

See the [logging sample](https://github.com/dotnet/SqlClient/blob/main/doc/samples/SqlAuthenticationLogging_Example.cs)
for automatic EventSource collection and optional callback registration. This addresses
[issue #1846](https://github.com/dotnet/SqlClient/issues/1846).

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
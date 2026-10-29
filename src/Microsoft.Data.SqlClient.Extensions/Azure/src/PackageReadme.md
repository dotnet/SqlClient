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
- **Azure Attestation**: Validates Microsoft Azure Attestation tokens for Always Encrypted with secure enclaves. Applications that connect with `Attestation Protocol=AAS` must reference this package.

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

## Breaking change: exact versions for automatic authentication discovery

Untrimmed authentication bootstrap requires the loaded SqlClient driver and automatically
discovered Azure extension to match Abstractions' exact informational version, including
prerelease labels and excluding `+` build metadata. A newer Azure patch or minor version is
not automatically accepted merely because its major version matches.

This changes behavior for mixed-version deployments that previously relied on successful assembly
binding and automatic provider discovery. A driver or discovered Azure version-policy failure is
traced once and disables further automatic Azure discovery and registration for that registry.
Existing providers are preserved, and explicit `SetProvider` and `GetProvider` remain usable with
their normal validation and configuration precedence. Without a registered provider or an
access-token callback, Entra ID connections can fail because no authentication provider is available.

The recommended fix is to upgrade SqlClient, Abstractions, Azure, Logging and AKV together to the
same exact package version, then restart to retry automatic discovery. SqlServer.Server and
native SNI are independently versioned.

### Using a newer Azure package explicitly

If you deliberately deploy a newer Azure package, opt out of automatic authentication discovery
before the first registry access and construct and register the provider yourself:

```csharp
using System;
using Microsoft.Data.SqlClient;

AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery", false);

var provider = new ActiveDirectoryAuthenticationProvider(
    new ActiveDirectoryAuthenticationProviderOptions());
if (!SqlAuthenticationProvider.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryDefault, provider))
{
    throw new InvalidOperationException(
        "Provider registration failed. Check configuration precedence and SqlClient traces.");
}
```

Register a provider for every authentication method your application uses, before opening
connections. Configuration-owned registrations retain precedence; change the configuration or
initializer if it prevents your explicit registration.

Explicit registration bypasses automatic discovery's version gate, not binary compatibility
requirements. A newer Azure package can require newer Abstractions or Logging APIs; you must
validate the selected package combination and upgrade those dependencies when required.
It does not suppress the separate `SQLCLIENT001` build warning or alter Azure Attestation loading.
An application-supplied `AccessTokenCallback`, as shown below, is another authentication option.

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
- [Azure.Core](https://www.nuget.org/packages/Azure.Core) - Azure SDK primitives and Entra ID credentials
- 
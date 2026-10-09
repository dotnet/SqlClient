# Microsoft.Data.SqlClient.Extensions.Abstractions

[![NuGet](https://img.shields.io/nuget/v/Microsoft.Data.SqlClient.Extensions.Abstractions.svg?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Abstractions)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Microsoft.Data.SqlClient.Extensions.Abstractions?style=flat-square)](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Abstractions)

## Description

This package provides **abstraction interfaces** for [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient) extensions. It defines the core contracts and interfaces that enable extensibility in the SqlClient driver without requiring direct dependencies on implementation packages.

The abstractions package allows for:
- Custom authentication providers
- Logging and diagnostics integration
- Extensibility points for third-party integrations

## Supportability

This package supports:

- .NET Standard 2.0 (compatible with .NET Framework 4.6.1+, .NET Core 2.0+, and .NET 5+)
- .NET 10.0 (trim/AOT-analyzed authentication registration)

## Authentication registration

The provider registry and bootstrap live in this package. `GetProvider` and `SetProvider`
work directly, including under NativeAOT, without loading SqlClient through reflection.
Configuration providers and configured initializers take precedence over application
registrations; application registrations take precedence over the discovered Azure default.
Registry initialization failures return `null` and `false`, respectively. Null providers,
unsupported methods and throwing registration callbacks also return `false`, without replacing
the existing provider. These failures are traced through `Microsoft.Data.SqlClient.EventSource`
(Informational level, Trace keyword `0x2`) with corrective guidance.
Bootstrap failures are cached and logged on subsequent accesses; correct the cause and restart
the application.

Recoverable resource lookup or formatting failures fall back to the resource key and
culture-formatted argument values, identifying unformattable arguments by type and preserving
the original exception cause. Fatal runtime errors propagate instead of being masked.

Untrimmed applications retain app.config loading and automatic Azure discovery. These switches
default to `true` and are cached independently on first access for the lifetime of the process:

- `Switch.Microsoft.Data.SqlClient.EnableAppConfig`
- `Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery`

Set switches before first registry access. On .NET 10 trimmed and NativeAOT publishes, both
reflective paths are removed automatically. Explicitly construct and register a provider for
each required authentication method before opening connections. The Azure provider is supplied
by `Microsoft.Data.SqlClient.Extensions.Azure`. Publish-time overrides use
`RuntimeHostConfigurationOption` with `Trim="true"`; runtime changes cannot affect trimming.

## Authentication configuration

The public `SqlAuthenticationProviderConfigurationSection` (legacy
`SqlAuthenticationProviders`) and `SqlClientAuthenticationProviderConfigurationSection`
(`SqlClientAuthenticationProviders`) handlers are defined in this package and forwarded
from SqlClient. Existing app.config section declarations naming `Microsoft.Data.SqlClient`
remain valid. Section names, properties (`providers`, `initializerType`,
`applicationClientId`, `useWamBroker`) and empty defaults are unchanged.

`System.Configuration.ConfigurationManager` is a transitive compile dependency, so
consumers can use the handlers and their configuration base types without adding a
separate package reference. `SqlAuthenticationInitializer` is also defined in Abstractions
and forwarded from SqlClient, preserving existing compiled consumers and initializer declarations.

See the repository's `doc/samples/SqlAuthenticationProviders.config` for a section
declaration and custom provider example.

## Installation

Install the package via NuGet:

```bash
dotnet add package Microsoft.Data.SqlClient.Extensions.Abstractions
```

Or via the Package Manager Console:

```powershell
Install-Package Microsoft.Data.SqlClient.Extensions.Abstractions
```

## Purpose

This package is primarily intended for:

1. **Library Authors**: Building extensions that integrate with Microsoft.Data.SqlClient
2. **Framework Developers**: Creating custom authentication or logging implementations
3. **Enterprise Scenarios**: Implementing organization-specific security or monitoring requirements

Most application developers will not need to reference this package directly—instead, use the concrete implementation packages that depend on these abstractions.

## Documentation

- [Microsoft.Data.SqlClient Documentation](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)
- [SqlClient GitHub Repository](https://github.com/dotnet/SqlClient)

## License

This package is licensed under the [MIT License](https://licenses.nuget.org/MIT).

## Related Packages

- [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient) - The main SqlClient driver
- [Microsoft.Data.SqlClient.Internal.Logging](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Internal.Logging) - Logging internals
- [Microsoft.Data.SqlClient.Extensions.Azure](https://www.nuget.org/packages/Microsoft.Data.SqlClient.Extensions.Azure) - Azure integration extensions

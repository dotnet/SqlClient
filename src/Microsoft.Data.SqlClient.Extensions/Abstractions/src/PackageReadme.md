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
Registry initialization failures preserve the existing `GetProvider`/`SetProvider` results:
`null` and `false`, respectively. Invalid provider arguments and throwing registration callbacks
also return `false`, without replacing the existing registration. Failures are logged with the
authentication method, exception details and corrective guidance through
`Microsoft.Data.SqlClient.EventSource` (Informational level, Trace keyword `0x2`).
Enable this tracing to diagnose unsuccessful registration or initialization. Bootstrap failures
are cached and logged on every subsequent access, even if tracing was enabled after the first
failure; correct the configuration or package versions and restart the application.

If resource lookup or error-message formatting fails, the message falls back to the resource
key and culture-formatted argument values, preserving the intended exception and its original
cause. Arguments that cannot be formatted are identified by type. These recoverable
message-generation failures are also traced.

On untrimmed applications, Azure discovery and the existing `app.config` sections still work.
The public `SqlAuthenticationProviderConfigurationSection` and
`SqlClientAuthenticationProviderConfigurationSection` handlers are defined in Abstractions
and type-forwarded from SqlClient. Existing section declarations that name
`Microsoft.Data.SqlClient` remain valid; their section names, properties and defaults are unchanged.
The `System.Configuration.ConfigurationManager` dependency supplies their public configuration
base types and is available to consuming projects at compile time.

The following AppContext switches disable those paths before the first registry access:

- `Switch.Microsoft.Data.SqlClient.EnableAppConfig` (default `true`)
- `Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery` (default `true`)

Each switch is read and cached independently on first access for the lifetime of the
process, matching SqlClient's other switches. Set them before using SqlClient;
subsequent runtime changes do not affect cached values.

On .NET 10 trimmed and NativeAOT publishes, both reflective paths are removed automatically.
Register an explicitly constructed provider for each required method before opening a connection:

```csharp
SqlAuthenticationProvider.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryDefault,
    new ActiveDirectoryAuthenticationProvider());
```

The provider in this example is supplied by `Microsoft.Data.SqlClient.Extensions.Azure`.
For publish-time switch overrides, use `RuntimeHostConfigurationOption` with `Trim="true"`;
runtime `AppContext.SetSwitch` calls cannot affect trimming.

## Package version policy

Upgrade all SqlClient family packages together, to the same exact version (including prerelease
labels): SqlClient, Extensions.Abstractions, Extensions.Azure, Internal.Logging, and
AlwaysEncrypted.AzureKeyVaultProvider. `Microsoft.SqlServer.Server` is versioned separately.
The extensions accompanying SqlClient 7.0.0/7.0.1 were numbered 1.0.0; those old versions
must also be upgraded when upgrading SqlClient.

This package ships build and transitive checks for PackageReference and `packages.config`
consumers. A mismatch fails with `SQLCLIENT001`. Setting
`<SqlClientEnforceFamilyVersions>false</SqlClientEnforceFamilyVersions>` is an emergency build
opt-out, not support for mixed versions. Untrimmed registry bootstrap also checks the loaded
SqlClient and discovered Azure assemblies' exact informational versions. A mismatch is logged
with upgrade guidance and causes `GetProvider`/`SetProvider` to return `null`/`false`.

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

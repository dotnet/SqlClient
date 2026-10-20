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

- .NET Framework 4.6.2 (uses the framework's `System.Configuration` assembly)
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

Untrimmed applications retain app.config loading and automatic Azure discovery. These switches
default to `true` and are cached independently on first access for the lifetime of the process:

- `Switch.Microsoft.Data.SqlClient.EnableAppConfig`
- `Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery`

Automatic Azure discovery requires the `ActiveDirectoryAuthenticationProviderOptions` constructor
from a compatible `Microsoft.Data.SqlClient.Extensions.Azure` package, even when no overrides are
configured. Discovery does not fall back to parameterless or string-based provider constructors.

Set switches before first registry access. On .NET 10 trimmed and NativeAOT publishes, both
reflective paths are removed automatically. Explicitly construct and register a provider for
each required authentication method before opening connections. The Azure provider is supplied
by `Microsoft.Data.SqlClient.Extensions.Azure`. Authentication discovery loads the optional Azure
assembly within Abstractions. The driver independently loads it for Azure Attestation, without
processing authentication configuration or consulting the authentication discovery switch.
Both loaders enforce the same assembly identity checks in signed builds.
Publish-time overrides use
`RuntimeHostConfigurationOption` with `Trim="true"`; runtime changes cannot affect trimming.

## Authentication configuration

The public `SqlAuthenticationProviderConfigurationSection` (legacy
`SqlAuthenticationProviders`) and `SqlClientAuthenticationProviderConfigurationSection`
(`SqlClientAuthenticationProviders`) handlers are defined in this package and forwarded
from SqlClient. Both are sealed and derive directly from `ConfigurationSection`, with
the same configuration schema and no inheritance relationship between the handlers.
These are infrastructure types, public to support use across SqlClient
assemblies and configuration section activation, not APIs for direct use by application
code. Applications should not instantiate, derive from, or access them directly.
Referencing the handlers in app.config section declarations is supported, and existing
declarations naming `Microsoft.Data.SqlClient` remain valid.
Section names, properties (`providers`, `initializerType`,
`applicationClientId`, `useWamBroker`) and empty defaults are unchanged.

For the .NET Standard target, `System.Configuration.ConfigurationManager` is a transitive
compile dependency required by the handlers' configuration base types. The .NET Framework
target uses the framework's `System.Configuration` instead, without introducing a
ConfigurationManager package dependency. Authentication initialization and provider loading
are owned by this package, and the modern section takes precedence over the legacy one.

## Configuration loading

Authentication configuration in app.config is optional. The loader reads the
`SqlClientAuthenticationProviders` section first and falls back to the legacy
`SqlAuthenticationProviders` section. If neither is present, it returns
empty settings and the driver uses its defaults.

Loading traces configuration errors and continues with defaults:

- **Unreadable or malformed configuration:** `ConfigurationErrorsException` is traced, and
  empty settings are returned. The legacy section isn't checked in this case.
- **Section handled by a different type:** traced and skipped. If the modern section
  is skipped this way, the loader checks the legacy section.
- **Invalid `useWamBroker` values:** traced and ignored.
  The default is kept, and other valid settings still apply.
- **Omitted optional attributes:** the default value is used.

A declared modern section takes precedence over legacy, even if its element is absent.

Internal handler metadata mismatches trigger a Debug assertion. In Release, the affected
string setting or provider list is treated as empty while other valid settings are retained.
Omitting optional XML attributes does not cause a metadata mismatch.

Loading only reads settings. It doesn't create or activate initializers or providers.
Other exception types propagate, and initializer or provider validation and activation
can still fail later.

Use `SqlClientEventSource` tracing to see settings that were skipped or ignored.

> **Warning:** Relaxed loading and parsing can permit the driver to install and configure
> unexpected providers or provider settings. Ignoring broken
> authentication configuration can select legacy providers, allow default providers to be
> installed, or retain default broker settings instead of the application's intended settings.
> Tracing these errors does not prevent those unintended configurations.

As in the driver, all `ConfigurationErrorsException` failures are traced and ignored, even
when they wrap fatal failures. Other exception types propagate to registry bootstrap.
Reading settings does not activate initializers or providers; registry bootstrap applies
the resulting settings afterward.

A declared modern section takes precedence even when its element is absent from app.config.
These policies apply to registry bootstrap through the Abstractions loader.
The `EnableAppConfig` switch gates configuration reading; it does not change parsing policy.

`SqlAuthenticationInitializer` is also defined in Abstractions and forwarded from SqlClient,
preserving existing compiled consumers and initializer declarations.

If an explicitly configured initializer or provider cannot be loaded, instantiated, or applied,
configuration application stops and registry bootstrap fails. Configured entries are not silently
skipped. Recoverable bootstrap failures are cached as described above; earlier registrations
and initializer/provider callback side effects are not rolled back.

Missing sections and empty optional settings remain valid.
Registration errors inside a configured initializer propagate to fail bootstrap,
rather than being converted to an ignorable `false` result. Successful registrations are traced
with their authentication method, provider type and precedence tier; refused registrations are
traced separately and do not produce success events.

Merge the following example into the application's app.config, replacing
`ExampleAuthenticationProvider, ExampleApp` with the assembly-qualified name of an
application-defined `SqlAuthenticationProvider`. The handler declaration continues to
name `Microsoft.Data.SqlClient`; the type forward resolves it to Abstractions.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="SqlClientAuthenticationProviders"
             type="Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection, Microsoft.Data.SqlClient" />
  </configSections>
  <SqlClientAuthenticationProviders>
    <providers>
      <add name="active directory default" type="ExampleAuthenticationProvider, ExampleApp" />
    </providers>
  </SqlClientAuthenticationProviders>
</configuration>
```

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

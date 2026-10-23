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
remain in the driver, and the modern section continues to take precedence over the legacy one.

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

Loading only reads settings. It doesn't create or activate initializers or providers.
Other exception types propagate, and initializer or provider validation and activation
can still fail later.

Use `SqlClientEventSource` tracing to see settings that were skipped or ignored.

These policies apply to the new Abstractions loader; the driver continues using its existing
reader until the authentication registry migration in
[dotnet/SqlClient#4821](https://github.com/dotnet/SqlClient/pull/4821).

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

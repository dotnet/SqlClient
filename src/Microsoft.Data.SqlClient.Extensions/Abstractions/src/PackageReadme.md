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

## Authentication configuration

The public `SqlAuthenticationProviderConfigurationSection` (legacy
`SqlAuthenticationProviders`) and `SqlClientAuthenticationProviderConfigurationSection`
(`SqlClientAuthenticationProviders`) handlers are defined in this package and forwarded
from SqlClient. These are infrastructure types, public to support use across SqlClient
assemblies and configuration section activation, not APIs for direct use by application
code. Applications should not instantiate, derive from, or access them directly.
Referencing the handlers in app.config section declarations is supported, and existing
declarations naming `Microsoft.Data.SqlClient` remain valid.
Section names, properties (`providers`, `initializerType`,
`applicationClientId`, `useWamBroker`) and empty defaults are unchanged.

`System.Configuration.ConfigurationManager` is a transitive compile dependency required
by the handlers' configuration base types. Authentication initialization and provider
loading remain in the driver.

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

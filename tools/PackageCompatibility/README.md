# PackageCompatibility Tool

A minimal console application that verifies that a set of SqlClient packages can coexist without
transitive dependency conflicts, API surface mismatches, or broken runtime functionality.  It loads
assemblies from each package and then opens a `SqlConnection` to confirm that the resolved package
graph works end-to-end against a real SQL Server instance.

The following SqlClient packages are verified, either directly or transitively:

- `Microsoft.Data.SqlClient`
- `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider`
- `Microsoft.Data.SqlClient.Internal.Logging`
- `Microsoft.Data.SqlClient.Extensions.Abstractions`
- `Microsoft.Data.SqlClient.Extensions.Azure` *(optional — included when `AzureVersion` is set)*
- `Microsoft.SqlServer.Server`

## Purpose

This tool is a smoke test for inter-package compatibility across the SqlClient suite.  It catches
problems such as:

- Assembly binding conflicts caused by mismatched transitive dependencies.
- API surface mismatches between independently-versioned packages.
- Runtime failures that only appear when multiple packages are loaded together.

Specifically, it:

1. Instantiates `SqlColumnEncryptionAzureKeyVaultProvider` to force the AKV provider assembly and
   all its transitive dependencies to load alongside SqlClient.
2. Opens a `SqlConnection` using a connection string you provide, exercising the authentication
   and network code paths end-to-end.

The app is designed to run against both **published NuGet packages** and **locally-built packages**
(via the `packages/` directory configured in `NuGet.config`).

## Authentication Modes

The authentication mode embedded in the connection string controls which code paths and packages are
exercised during the connectivity test.  Use different modes to broaden coverage:

| Authentication mode | `Authentication=` value | Packages exercised |
| --- | --- | --- |
| SQL Server auth | *(omit or `SqlPassword`)* | `Microsoft.Data.SqlClient` only |
| Windows / Integrated | `ActiveDirectoryIntegrated` | `Microsoft.Data.SqlClient` + SSPI |
| Entra ID — default chain | `ActiveDirectoryDefault` | `Microsoft.Data.SqlClient` + `Extensions.Azure` (requires `AzureVersion`) |
| Entra ID — password | `ActiveDirectoryPassword` | `Microsoft.Data.SqlClient` + `Extensions.Azure` (requires `AzureVersion`) |
| Entra ID — interactive | `ActiveDirectoryInteractive` | `Microsoft.Data.SqlClient` + `Extensions.Azure` (requires `AzureVersion`) |
| Entra ID — service principal | `ActiveDirectoryServicePrincipal` | `Microsoft.Data.SqlClient` + `Extensions.Azure` (requires `AzureVersion`) |
| Entra ID — managed identity | `ActiveDirectoryManagedIdentity` | `Microsoft.Data.SqlClient` + `Extensions.Azure` (requires `AzureVersion`) |

> **Note:** All Entra ID modes require the `Extensions.Azure` package to be referenced (pass
> `-p:AzureVersion=<version>`).  Without it, SqlClient will throw at runtime because no
> authentication provider is registered for those modes.

## Build Parameters

Package versions are controlled through MSBuild properties. Pass them on the command line with `-p:`
(or `/p:`) to override the defaults defined in `Directory.Packages.props`.

| Property | Default | Description |
| --- | --- | --- |
| `AbstractionsVersion` | `1.0.0` | Version of `Microsoft.Data.SqlClient.Extensions.Abstractions` to reference. |
| `AkvProviderVersion` | `7.0.0` | Version of `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider` to reference. |
| `AzureVersion` | None | Version of `Microsoft.Data.SqlClient.Extensions.Azure` to reference.  When omitted, the `Azure` package will not be referenced. |
| `LoggingVersion` | `1.0.0` | Version of `Microsoft.Data.SqlClient.Internal.Logging` to reference. |
| `SqlClientVersion` | `7.0.0` | Version of `Microsoft.Data.SqlClient` to reference. |
| `SqlServerVersion` | `1.0.0` | Version of `Microsoft.SqlServer.Server` to reference. |

## Local Package Source

The `NuGet.config` adds a `packages/` directory as a local package source. To test against packages
that haven't been published to NuGet yet, copy the `.nupkg` files into this folder and specify the
matching version via the build properties above.

NuGet will cache copies of the packages it finds in `packages/` after a successful restore.  If you
update the `.nupkg` files in `packages/` without incrementing their version numbers (and referencing
those new version numbers) you will have to clear the NuGet caches in order for the next restore
operation to pick them up:

```bash
dotnet nuget locals all --clear
```

## Running the App

The app has built-in help:

```bash
dotnet run -- --help

Description:
  Package Compatibility Tester
  ----------------------------

  Validates SqlClient connectivity using EntraID (formerly Azure Active Directory) authentication.
  Connects to SQL Server using the supplied connection string, which must specify the authentication method.

  ...
```

The app requires a connection string.  Use SQL authentication for a basic connectivity check:

```bash
dotnet run -- -c "Server=myserver;Database=mydb;User ID=sa;Password=<pw>;Encrypt=Mandatory;TrustServerCertificate=true"
```

To exercise Entra ID flows, include an `Authentication` keyword and reference the `Extensions.Azure`
package:

```bash
dotnet run -p:AzureVersion=1.0.0 -- -c "Server=myserver.database.windows.net;Database=mydb;Authentication=ActiveDirectoryDefault"
```

On success the app emits to standard out:

```bash
Package Compatibility Tester
----------------------------

Packages used:
  Abstractions:  1.0.1
  AKV Provider:  7.0.0
  Azure:         1.1.0-preview1
  Logging:       1.0.1
  SqlClient:     7.1.0.preview1
  SqlServer:     1.0.0

Connection details:
  Data Source:      adotest.database.windows.net
  Initial Catalog:  Northwind
  Authentication:   ActiveDirectoryPassword

Testing connectivity...
Connected successfully!
    Server version: 12.00.1017
```

Errors will be emitted to standard error:

```bash
Testing connectivity...
Connection failed:
  Cannot find an authentication provider for 'ActiveDirectoryPassword'.
```

### Examples

Run a basic SQL authentication check using the default package versions:

```bash
dotnet run -- -c "Server=myserver;Database=mydb;User ID=sa;Password=<pw>;Encrypt=Mandatory;TrustServerCertificate=true"
```

Run with no `Azure` package — Entra ID modes will fail at runtime if specified in the connection
string, which is itself useful for confirming the error path:

```bash
dotnet run -- -c "<connection string>"
```

Include the `Azure` package to enable Entra ID authentication flows:

```bash
dotnet run -p:AzureVersion=1.0.0 -- -c "<connection string>"
```

Run against locally-built packages (drop `.nupkg` files into `packages/` first):

```bash
dotnet run -p:SqlClientVersion=7.1.0-preview1 -- -c "<connection string>"
```

Override all five package versions at once:

```bash
dotnet run \
  -p:AbstractionsVersion=1.0.1 \
  -p:AkvProviderVersion=7.1.0-preview1 \
  -p:AzureVersion=1.0.0 \
  -p:LoggingVersion=1.0.1 \
  -p:SqlClientVersion=7.1.0-preview1 \
  -p:SqlServerVersio=1.0.0 \
  -- -c "<connection string>"
```

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) and .NET Framework 4.8.1 or later.
- A SQL Server or Azure SQL instance reachable from the machine running the tool.
- For Entra ID authentication modes: Azure credentials available to `DefaultAzureCredential`
  (e.g. Azure CLI login, environment variables, or managed identity), and the `AzureVersion`
  build property set so the `Extensions.Azure` package is included.

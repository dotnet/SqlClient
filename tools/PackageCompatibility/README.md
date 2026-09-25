# PackageCompatibility Tool

A minimal console application for smoke-testing a selected set of SqlClient package versions.
It references the AKV provider type and opens a `SqlConnection` against a real SQL Server instance.
A successful connection validates the exercised authentication and network paths, not every API or
transitive dependency in the package graph.

The app references these SqlClient packages:

- `Microsoft.Data.SqlClient`
- `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider`
- `Microsoft.Data.SqlClient.Internal.Logging`
- `Microsoft.Data.SqlClient.Extensions.Abstractions`
- `Microsoft.Data.SqlClient.Extensions.Azure` *(optional — included when `AzureVersion` is set)*
- `Microsoft.SqlServer.Server`

## Purpose

This tool is a smoke test for inter-package compatibility across the SqlClient suite. It can reveal
problems such as:

- Assembly binding conflicts caused by mismatched transitive dependencies.
- API surface mismatches between independently-versioned packages.
- Runtime failures that only appear when multiple packages are loaded together.

Specifically, it:

1. References `typeof(SqlColumnEncryptionAzureKeyVaultProvider)` without constructing a provider or
   performing Key Vault operations.
2. Opens a `SqlConnection` using a connection string you provide, exercising the authentication
   and network code paths end-to-end.

The app is designed to run against both **published NuGet packages** and **locally-built packages**
(via the [`packages/`](packages/) directory configured in [NuGet.config](NuGet.config)).

Run the commands below from `tools/PackageCompatibility/src`. The app targets `net481` and `net10.0`,
so `dotnet run` requires a framework selection. The examples use `-f net10.0`; use `-f net481` to
exercise .NET Framework on Windows.

## How This Differs From the Existing Test Suite

The SqlClient repository has a large suite of unit, functional, and manual tests.  This tool
complements — but does not replace — those tests.  The key differences are:

### Heterogeneous package versions

The main build orchestrator uses one `PackageVersionSqlClient` value for the SqlClient family,
with a separate `PackageVersionSqlServer` value. Its package-mode test targets do not expose
independent version switches for every family package.

This tool accepts any combination of independent version numbers — including versions that have not
been published yet — at the command line:

```bash
dotnet run -f net10.0 \
  -p:SqlClientVersion=7.0.1 \
  -p:AkvProviderVersion=7.1.0-preview1 \
  -- -c "<connection string>"
```

This makes it straightforward to answer questions like *"does the new AKV provider build work
against the last published SqlClient release?"* without modifying any source files.

### Pre-release and locally-built packages

You can drop pre-release `.nupkg` files in `tools/PackageCompatibility/packages/` and reference them
immediately — even before they have been published. NuGet can resolve the SqlClient package IDs from
either that local feed or NuGet.org; source order does not guarantee preference for a local copy.
Use a unique package version to distinguish local builds. The main test suite also supports local
packages through `ReferenceType=Package`; this tool is useful for independently selecting versions.

### Connectivity with a selected package graph

Unit and functional tests exercise individual classes and APIs without a live SQL Server.
Manual tests open real server connections. These projects can run against source projects or
package references.

This tool opens a live `SqlConnection` using the selected package versions. Assemblies load as needed
by the exercised code paths; merely referencing a package does not exercise all its dependencies.
It can expose issues such as:

- **Binding redirect conflicts**: two packages pulling in incompatible versions of a shared
  dependency (`Azure.Core`, `Microsoft.Identity.*`, etc.) that only manifest when all packages are
  present in the same AppDomain.
- **Transitive version mismatches**: a package expecting an internal API surface that has changed in
  a sibling package across a version boundary.
- **Registration side-effects**: authentication providers or other singleton registrations that
  interfere when packages are composed in an unexpected order or version combination.

### Diagnostic console output

Use `--log-events` (or `-l`) to write SqlClient EventSource events to the console during the run.
`--trace` (or `-t`) pauses before connecting and prints a `dotnet-trace` attachment command; it
does not start trace collection itself.

`--verbose` (or `-v`) includes exception details and the full connection string. Do not capture or
share verbose output containing real credentials.

The test suite's diagnostics are routed through `EventSource`/`DiagnosticListener` and are only
visible if a listener is attached (e.g. via `dotnet-trace` or a custom test initializer).

### What this tool does NOT do

- It does not assert on individual API behaviours, query results, or error messages.  For that, use
  the existing unit and functional tests.
- It cannot run without a real SQL Server instance.  The manual test suite has the same constraint
  for connectivity tests, but unit and functional tests run without a server.
- It does not cover every authentication mode automatically.  You must provide a suitable connection
  string for each mode you want to validate.
- It does not encrypt or decrypt data through the AKV provider, or validate every referenced assembly.

## Project Layout

- `src/` contains the tool source files and project file.
- `test/` contains the tool's xUnit v3 tests.

## Authentication Modes

The authentication mode embedded in the connection string controls which code paths and packages are
exercised during the connectivity test.  Use different modes to broaden coverage:

| Authentication mode | `Authentication=` value | Packages exercised |
| --- | --- | --- |
| SQL Server auth | *(omit or `SqlPassword`)* | `Microsoft.Data.SqlClient` only |
| Entra ID — integrated | `ActiveDirectoryIntegrated` | `Microsoft.Data.SqlClient` + `Extensions.Azure` (requires `AzureVersion`) |
| Entra ID — default chain | `ActiveDirectoryDefault` | `Microsoft.Data.SqlClient` + `Extensions.Azure` (requires `AzureVersion`) |
| Entra ID — password | `ActiveDirectoryPassword` | `Microsoft.Data.SqlClient` + `Extensions.Azure` (requires `AzureVersion`) |
| Entra ID — interactive | `ActiveDirectoryInteractive` | `Microsoft.Data.SqlClient` + `Extensions.Azure` (requires `AzureVersion`) |
| Entra ID — service principal | `ActiveDirectoryServicePrincipal` | `Microsoft.Data.SqlClient` + `Extensions.Azure` (requires `AzureVersion`) |
| Entra ID — managed identity | `ActiveDirectoryManagedIdentity` | `Microsoft.Data.SqlClient` + `Extensions.Azure` (requires `AzureVersion`) |

> **Note:** Starting with SqlClient 7.0, driver-provided Entra ID authentication requires
> `Extensions.Azure` (pass `-p:AzureVersion=<version>`). This app does not register a custom
> authentication provider, so those modes fail without the extension.

## Build Parameters

Package versions are controlled through build properties. Pass them on the command line with `-p:`
(or `/p:`) to override the defaults defined in `Directory.Packages.props`.

| Property | Default | Description |
| --- | --- | --- |
| `AbstractionsVersion` | `1.0.0` | Version of `Microsoft.Data.SqlClient.Extensions.Abstractions` to reference. |
| `AkvProviderVersion` | `7.0.0` | Version of `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider` to reference. |
| `AzureVersion` | None | Version of `Microsoft.Data.SqlClient.Extensions.Azure` to reference.  When omitted, the `Azure` package will not be referenced. |
| `LoggingVersion` | `1.0.0` | Version of `Microsoft.Data.SqlClient.Internal.Logging` to reference. |
| `SqlClientVersion` | `7.0.1` | Version of `Microsoft.Data.SqlClient` to reference. |
| `SqlServerVersion` | `1.0.0` | Version of `Microsoft.SqlServer.Server` to reference. |

## Local Package Source

The [NuGet.config](NuGet.config) adds `tools/PackageCompatibility/packages/` as a local package source,
relative to that config file (not the shell's working directory). Copy locally built `.nupkg` files into
this folder and specify their matching versions via the build properties above.

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
dotnet run -f net10.0 -- --help
```

Example output:

```text
Description:
  Package Compatibility Tester
  ----------------------------

  Validates SqlClient connectivity using EntraID (formerly Azure Active Directory) authentication.
  Connects to SQL Server using the supplied connection string, which must specify the authentication method.

  ...
```

The app requires a connection string.  Use SQL authentication for a basic connectivity check:

```bash
dotnet run -f net10.0 -- -c "Server=myserver;Database=mydb;User ID=sa;Password=<pw>;Encrypt=Mandatory;TrustServerCertificate=true"
```

To exercise Entra ID flows, include an `Authentication` keyword and reference the `Extensions.Azure`
package:

```bash
dotnet run -f net10.0 -p:AzureVersion=1.0.0 -- -c "Server=myserver.database.windows.net;Database=mydb;Authentication=ActiveDirectoryDefault"
```

Example success output (package versions and server details vary):

```text
Package Compatibility Tester
----------------------------

Packages used:
  Abstractions:  1.0.1
  AKV Provider:  7.0.0
  Azure:         1.1.0-preview1
  Logging:       1.0.1
  SqlClient:     7.1.0-preview1
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

```text
Connection failed:
  Cannot find an authentication provider for 'ActiveDirectoryPassword'.
```

### Examples

Run a basic SQL authentication check using the default package versions:

```bash
dotnet run -f net10.0 -- -c "Server=myserver;Database=mydb;User ID=sa;Password=<pw>;Encrypt=Mandatory;TrustServerCertificate=true"
```

Run with no `Azure` package. With SqlClient 7.0 or later, driver-provided Entra ID modes fail
if specified in the connection string, which is useful for confirming the error path:

```bash
dotnet run -f net10.0 -- -c "<connection string>"
```

Include the `Azure` package to enable Entra ID authentication flows:

```bash
dotnet run -f net10.0 -p:AzureVersion=1.0.0-preview1 -- -c "<connection string>"
```

Run against locally-built packages (copy `.nupkg` files to `tools/PackageCompatibility/packages/`
first, then run this command from `tools/PackageCompatibility/src`):

```bash
dotnet run -f net10.0 -p:SqlClientVersion=7.1.0-preview1 -- -c "<connection string>"
```

Override all six package versions at once:

```bash
dotnet run -f net10.0 \
  -p:AbstractionsVersion=1.0.1 \
  -p:AkvProviderVersion=7.1.0-preview1 \
  -p:AzureVersion=1.0.0 \
  -p:LoggingVersion=1.0.1 \
  -p:SqlClientVersion=7.1.0-preview1 \
  -p:SqlServerVersion=1.0.0 \
  -- -c "<connection string>"
```

## Prerequisites

- The .NET SDK pinned in [global.json](global.json), plus the .NET 10 runtime for `net10.0`.
- Windows and .NET Framework 4.8.1 to run the `net481` target.
- A SQL Server or Azure SQL instance reachable from the machine running the tool.
- For Entra ID authentication: credentials appropriate to the selected mode, plus `AzureVersion`
  for the extension package when using SqlClient 7.0 or later. `ActiveDirectoryDefault` uses the
  `DefaultAzureCredential` chain (for example, Azure CLI login or managed identity); other modes
  have their own credential requirements.

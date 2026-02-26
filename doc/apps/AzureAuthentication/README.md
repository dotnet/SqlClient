# AzureAuthentication Sample App

A minimal console application that verifies **SqlClient** can connect to a SQL Server using Entra ID
authentication (formerly Azure Active Directory authentication) via the **Azure** package. It also
references the **Azure Key Vault Provider** package to confirm there are no transitive dependency
conflicts between the packages.

The following SqlClient packages are used, either directly or transitively:

- `Microsoft.Data.SqlClient`
- `Microsoft.SqlServer.Server`
- `Microsoft.Data.SqlClient.Extensions.Logging`
- `Microsoft.Data.SqlClient.Extensions.Abstractions`
- `Microsoft.Data.SqlClient.Extensions.Azure`
- `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider`

## Purpose

This app serves as a smoke test for package compatibility. It:

1. Instantiates `SqlColumnEncryptionAzureKeyVaultProvider` to ensure the AKV provider assembly loads
   without conflicts.
2. Opens a `SqlConnection` using a connection string you provide, validating that authentication and
   connectivity work end-to-end.

The app is designed to run against both **published NuGet packages** and **locally-built packages**
(via the `packages/` directory configured in `NuGet.config`).

## Build Parameters

Package versions are controlled through MSBuild properties. Pass them on the command line with `-p:`
(or `/p:`) to override the defaults defined in `Directory.Packages.props`.

| Property | Default | Description |
| --- | --- | --- |
| `SqlClientVersion` | `6.1.4` | Version of `Microsoft.Data.SqlClient` to reference. |
| `AkvProviderVersion` | `6.1.2` | Version of `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider` to reference. |
| `AzureVersion` | None | Version of `Microsoft.Data.SqlClient.Extensions.Azure` to reference.  When omitted, the `Azure` package will not be referenced. |

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
  Azure Authentication Tester
  ---------------------------

  Validates SqlClient connectivity using EntraID (formerly Azure Active Directory) authentication.
  Connects to SQL Server using the supplied connection string, which must specify the authentication method.

  Supply specific package versions when building to test different versions of the SqlClient suite, for example:

    -p:SqlClientVersion=7.0.0.preview4
    -p:AkvProviderVersion=7.0.1-preview2
    -p:AzureVersion=1.0.0-preview1

Usage:
  AzureAuthentication [options]

Options:
  -c, --connection-string <connection-string> (REQUIRED)  The ADO.NET connection string used to connect to SQL Server.
                                                          Supports SQL, Azure AD, and integrated authentication modes.
  -l, --log-events                                        Enable SqlClient event emission to the console.
  -t, --trace                                             Pauses execution to allow dotnet-trace to be attached.
  -v, --verbose                                           Enable verbose output with detailed error information.
  -?, -h, --help                                          Show help and usage information
  --version                                               Show version information
```

The app expects a single argument: a full connection string.

```bash
dotnet run -- -c "<connection string>"
```

For Azure AD authentication, use an `Authentication` keyword in the connection string. For example:

```bash
dotnet run -- -c "Server=myserver.database.windows.net;Database=mydb;Authentication=ActiveDirectoryDefault"
```

On success the app emits to standard out:

```bash
Azure Authentication Tester
---------------------------

Packages used:
  SqlClient:     7.0.0-preview4.26055.1
  AKV Provider:  6.1.2
  Azure:         1.0.0-preview1.26055.1

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

Run with the default (published) package versions, and no `Azure` package:

```bash
dotnet run -- -c "<connection string>"
```

If the connection string specifies one of the Azure Active Directory authentication methods,
`SqlClient` will fail with an error indicating that no authentication provider has been registered.
This is because the `Azure` package was not referenced, and the app did not provide its own custom
authentication provider.

Run against locally-built packages (drop `.nupkg` files into the `packages/` folder first):

```bash
dotnet run -p:SqlClientVersion=7.0.0-preview4 -- -c "<connection string>"
```

Run including the `Azure` extensions package:

```bash
dotnet run -p:AzureVersion=1.0.0-preview1 -- -c "<connection string>"
```

Override all three versions at once:

```bash
dotnet run -p:SqlClientVersion=7.0.0-preview1 -p:AkvProviderVersion=7.0.0-preview1 -p:AzureVersion=1.0.0-preview1 -- -c "<connection string>"
```

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) and .NET Framework 4.8.1 or later.
- A SQL Server or Azure SQL instance accessible with Azure AD credentials.
- Azure credentials available to `DefaultAzureCredential` (e.g. Azure CLI login, environment
  variables, or managed identity).

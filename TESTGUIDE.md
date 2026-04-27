# Test Guide for Microsoft.Data.SqlClient

This guide describes how to run the test projects in this repository and how to configure the SQL Server-backed manual
tests.

For build prerequisites and general `build.proj` usage, see [BUILDGUIDE.md](BUILDGUIDE.md).

## Test Projects

The primary test projects for Microsoft.Data.SqlClient are under
[src/Microsoft.Data.SqlClient/tests](src/Microsoft.Data.SqlClient/tests):

| Project          | Path                                                                                                                                                                                                     | Purpose                                                                                                           |
|------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------|
| Unit tests       | [src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj](src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj)                         | Unit tests and tests against simulated servers.                                                                   |
| Functional tests | [src/Microsoft.Data.SqlClient/tests/FunctionalTests/Microsoft.Data.SqlClient.FunctionalTests.csproj](src/Microsoft.Data.SqlClient/tests/FunctionalTests/Microsoft.Data.SqlClient.FunctionalTests.csproj) | Functional tests for public and internal behavior. Some tests use simulated servers or local test infrastructure. |
| Manual tests     | [src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTests.csproj](src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTests.csproj)                 | Integration tests that generally require a configured SQL Server or Azure SQL target.                             |

These projects target `net8.0`, `net9.0`, and `net10.0` on all platforms. On Windows, they also target `net462`.

## Recommended Entry Point

Use [build.proj](build.proj) from the repository root:

```bash
msbuild build.proj -t:<test_target> [optional_parameters]
```

If `msbuild` is not available, use `dotnet msbuild`:

```bash
dotnet msbuild build.proj -t:<test_target> [optional_parameters]
```

Test targets build the projects they depend on, so a separate build step is not required for normal test runs.

## Test Targets

| Target                    | Description                                                                                                              |
|---------------------------|--------------------------------------------------------------------------------------------------------------------------|
| `Test`                    | Runs all test targets in the repository. This can take a long time and is not recommended for routine local development. |
| `TestAbstractions`        | Runs Microsoft.Data.SqlClient.Extensions.Abstractions tests.                                                             |
| `TestAzure`               | Runs Microsoft.Data.SqlClient.Extensions.Azure tests.                                                                    |
| `TestSqlClient`           | Runs all Microsoft.Data.SqlClient test projects.                                                                         |
| `TestSqlClientUnit`       | Runs Microsoft.Data.SqlClient unit tests.                                                                                |
| `TestSqlClientFunctional` | Runs Microsoft.Data.SqlClient functional tests.                                                                          |
| `TestSqlClientManual`     | Runs Microsoft.Data.SqlClient manual tests.                                                                              |

## Common Commands

Run the SqlClient unit tests:

```bash
msbuild build.proj -t:TestSqlClientUnit
```

Run the SqlClient functional tests:

```bash
msbuild build.proj -t:TestSqlClientFunctional
```

Run the SqlClient manual tests:

```bash
msbuild build.proj -t:TestSqlClientManual
```

Run only manual test set 2:

```bash
msbuild build.proj -t:TestSqlClientManual -p:TestSet=2
```

Run manual test sets 1 and 3:

```bash
msbuild build.proj -t:TestSqlClientManual -p:TestSet=13
```

Run Always Encrypted manual tests:

```bash
msbuild build.proj -t:TestSqlClientManual -p:TestSet=AE
```

Run a specific target framework:

```bash
msbuild build.proj -t:TestSqlClientFunctional -p:TestFramework=net8.0
```

Run functional tests against an x86 `dotnet` installation:

```bash
msbuild build.proj -t:TestSqlClientFunctional -p:DotnetPath='C:\path\to\dotnet\x86\'
```

Run all Azure extension tests, including `interactive` tests, while still excluding tests marked `failing` or `flaky`:

```bash
msbuild build.proj -t:TestAzure -p:TestFilters=category!=failing
```

## Test Parameters

The most commonly used test parameters are:

| Parameter                   | Default                                                   | Description                                                                                                                   |
|-----------------------------|-----------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------|
| `-p:Configuration=`         | `Debug`                                                   | Build configuration. Use `Debug` or `Release`.                                                                                |
| `-p:DotnetPath=`            | Empty                                                     | Path to the folder containing the `dotnet` binary. The path must end with `\` or `/`.                                         |
| `-p:ReferenceType=`         | `Project`                                                 | For functional and manual SqlClient tests, use `Project` to test the source project or `Package` to test a package reference. |
| `-p:TestBlameTimeout=`      | `10m`                                                     | Enables hang blame collection with the specified timeout. Use `0` to disable hang timeouts.                                   |
| `-p:TestCodeCoverage=`      | `true`                                                    | Collects code coverage when set to `true`.                                                                                    |
| `-p:TestFilters=`           | `category!=failing&category!=flaky&category!=interactive` | xUnit filter expression. Use `none` to run without the default filter.                                                        |
| `-p:TestFramework=`         | Empty                                                     | Target framework to run. If omitted, all target frameworks supported by the project and host OS are run.                      |
| `-p:TestResultsFolderPath=` | `test_results`                                            | Directory where test results are written.                                                                                     |
| `-p:TestSet=`               | Empty                                                     | Selects manual test sets. Supported values include `1`, `2`, `3`, `AE`, and combinations such as `13` or `12AE`.              |

## Test Filters

`build.proj` passes `TestFilters` to `dotnet test --filter`. By default, tests marked with these categories are excluded:

| Category      | Why it is excluded by default                                                       |
|---------------|-------------------------------------------------------------------------------------|
| `failing`     | Known failing tests.                                                                |
| `flaky`       | Intermittently failing tests.                                                       |
| `interactive` | Tests that require user interaction or external setup not suitable for normal runs. |

Examples:

```bash
# Run a single test by fully-qualified name.
msbuild build.proj -t:TestSqlClientUnit -p:TestFilters=FullyQualifiedName=Namespace.ClassName.MethodName

# Run only flaky tests while investigating quarantine failures.
msbuild build.proj -t:TestSqlClientManual -p:TestFilters=category=flaky

# Disable the default filter.
msbuild build.proj -t:TestSqlClientFunctional -p:TestFilters=none
```

When passing filter expressions that contain shell-sensitive characters such as `&`, quote or escape the value as
required by your shell.

## Running Test Projects Directly

`build.proj` is the recommended entry point because it keeps logging, code coverage, package-reference mode, and
common parameters consistent. For quick local investigation, you can run a test project directly:

```bash
dotnet test src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj \
  -p:Configuration=Debug \
  --filter "category!=failing&category!=flaky&category!=interactive"
```

For manual tests, pass `TestSet` to the test project when needed:

```bash
dotnet test src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTests.csproj \
  -p:Configuration=Debug \
  -p:TestSet=2 \
  --filter "category!=failing&category!=flaky&category!=interactive"
```

## Manual Test Prerequisites

Manual tests require SQL Server or Azure SQL resources and a local test configuration file.

For a basic local SQL Server run, prepare:

- A SQL Server instance that the test machine can reach.
- Shared Memory, TCP, and Named Pipes protocols enabled when testing local Windows SQL Server scenarios.
- The `NORTHWIND` database created from [tools/testsql/createNorthwindDb.sql](tools/testsql/createNorthwindDb.sql). For
  Azure SQL, use [tools/testsql/createNorthwindAzureDb.sql](tools/testsql/createNorthwindAzureDb.sql).
- The `UdtTestDb` database created from [tools/testsql/createUdtTestDb.sql](tools/testsql/createUdtTestDb.sql) if you
  want UDT tests to run.
- A login or integrated-security principal with permissions to create and drop the temporary objects used by the tests.

Feature-specific tests require additional resources. If those resources are not configured, the corresponding
conditional tests are skipped.

## Manual Test Configuration

Edit the source configuration file at `src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities/
config.json`. The test utilities project copies that file to the test output directory, where the manual tests load it
by default.

The template file is:

[src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities/config.default.json](src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities/config.default.json)

`config.json` is git-ignored. If it does not exist, the test utilities project copies `config.default.json` to
`config.json` before compile. You can also create it manually:

```bash
cp src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities/config.default.json \
  src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities/config.json
```

Update `config.json` for your environment before running manual tests. The most important values for a basic run are `TCPConnectionString` and `NPConnectionString`.

```jsonc
{
  "TCPConnectionString": "Data Source=tcp:localhost;Database=Northwind;Integrated Security=true;Encrypt=false;",
  "NPConnectionString": "Data Source=np:localhost;Database=Northwind;Integrated Security=true;Encrypt=false;",
  "EnclaveEnabled": false,
  "TracingEnabled": false,
  "SupportsIntegratedSecurity": true
}
```

For SQL Server in a Linux container, WSL, or another host where SQL authentication is easier than integrated security, use a TCP connection string like:

```jsonc
{
  "TCPConnectionString": "Data Source=tcp:127.0.0.1;User Id=sa;Password=<password>;Database=Northwind;Encrypt=false;TrustServerCertificate=true"
}
```

You can override the config file path with the `MDS_TEST_CONFIG` environment variable:

```bash
MDS_TEST_CONFIG=/path/to/config.json msbuild build.proj -t:TestSqlClientManual -p:TestSet=2
```

On PowerShell:

```powershell
$env:MDS_TEST_CONFIG = "C:\path\to\config.json"
msbuild build.proj -t:TestSqlClientManual -p:TestSet=2
```

## Configuration Properties

| Property                         | Description                                                                                 | Example or notes                                                                       |
|----------------------------------|---------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------|
| `TCPConnectionString`            | Connection string for a TCP-enabled SQL Server or Azure SQL database.                       | `Data Source=tcp:localhost;Database=Northwind;Integrated Security=true;Encrypt=false;` |
| `NPConnectionString`             | Connection string for a Named Pipes-enabled SQL Server instance.                            | `Data Source=np:localhost;Database=Northwind;Integrated Security=true;Encrypt=false;`  |
| `TCPConnectionStringHGSVBS`      | Optional connection string for SQL Server with VBS enclave and HGS attestation.             | Include `Attestation Protocol=HGS` and `Enclave Attestation Url`.                      |
| `TCPConnectionStringNoneVBS`     | Optional connection string for SQL Server with VBS enclave and no attestation.              | Include `Attestation Protocol=None`.                                                   |
| `TCPConnectionStringAASSGX`      | Optional connection string for SQL Server with SGX enclave and Microsoft Azure Attestation. | Include `Attestation Protocol=AAS` and `Enclave Attestation Url`.                      |
| `EnclaveEnabled`                 | Enables tests that require an enclave-configured server.                                    | `true` or `false`.                                                                     |
| `TracingEnabled`                 | Enables tracing-related tests.                                                              | `true` or `false`.                                                                     |
| `AADAuthorityURL`                | Optional OAuth authority for `AADPasswordConnectionString`.                                 | `https://login.windows.net/<tenant>`                                                   |
| `AADPasswordConnectionString`    | Optional connection string for Microsoft Entra ID password authentication tests.            | Uses `Authentication=Active Directory Password`.                                       |
| `AADServicePrincipalId`          | Optional application ID for service-principal authentication tests.                         | Former docs may refer to this as a secure principal ID.                                |
| `AADServicePrincipalSecret`      | Optional application secret for service-principal authentication tests.                     | Keep this only in local, ignored config files or secure pipeline variables.            |
| `AzureKeyVaultURL`               | Optional Azure Key Vault URL for Always Encrypted tests.                                    | `https://<keyvaultname>.vault.azure.net/`                                              |
| `AzureKeyVaultTenantId`          | Optional Entra ID tenant ID for Azure Key Vault tests.                                      | Tenant ID GUID.                                                                        |
| `SupportsIntegratedSecurity`     | Whether the user running tests has integrated-security access to the target SQL Server.     | `true` or `false`.                                                                     |
| `LocalDbAppName`                 | Optional LocalDB instance name. Empty disables LocalDB testing.                             | `MSSQLLocalDB` or another local instance.                                              |
| `LocalDbSharedInstanceName`      | Optional shared LocalDB instance name.                                                      | Used only when testing shared LocalDB.                                                 |
| `SupportsFileStream`             | Whether FileStream tests are supported by the target.                                       | `true` or `false`.                                                                     |
| `FileStreamDirectory`            | Directory used for FileStream database setup.                                               | Use an escaped absolute path in JSON.                                                  |
| `UseManagedSNIOnWindows`         | Enables Managed SNI on Windows test coverage.                                               | `true` or `false`.                                                                     |
| `DNSCachingConnString`           | Optional connection string for DNS caching tests.                                           | Used with DNS caching server settings.                                                 |
| `DNSCachingServerCR`             | Optional DNS caching control-ring server.                                                   | Feature-specific tests only.                                                           |
| `DNSCachingServerTR`             | Optional DNS caching tenant-ring server.                                                    | Feature-specific tests only.                                                           |
| `IsDNSCachingSupportedCR`        | Enables DNS caching control-ring tests.                                                     | `true` or `false`.                                                                     |
| `IsDNSCachingSupportedTR`        | Enables DNS caching tenant-ring tests.                                                      | `true` or `false`.                                                                     |
| `IsAzureSynapse`                 | Marks the target as Azure Synapse.                                                          | Some SQL Server-specific tests are skipped when `true`.                                |
| `EnclaveAzureDatabaseConnString` | Optional Azure SQL database connection string for enclave tests.                            | Feature-specific tests only.                                                           |
| `ManagedIdentitySupported`       | Whether managed identity tests should run.                                                  | Defaults to `true`. Set `false` if unavailable.                                        |
| `UserManagedIdentityClientId`    | Optional client ID for user-assigned managed identity tests.                                | Feature-specific tests only.                                                           |
| `KerberosDomainUser`             | Optional Kerberos test domain user.                                                         | Feature-specific tests only.                                                           |
| `KerberosDomainPassword`         | Optional Kerberos test domain password.                                                     | Keep only in local, ignored config files or secure pipeline variables.                 |
| `IsManagedInstance`              | Marks the target as Azure SQL Managed Instance.                                             | Set `true` for Managed Instance to use non-Azure TVP baseline files in test set 3.     |
| `PowerShellPath`                 | Full path to PowerShell if it is not on `PATH`.                                             | `C:\\escaped\\path\\to\\powershell.exe`                                                |
| `AliasName`                      | Optional SQL Server alias used by alias-related tests.                                      | Feature-specific tests only.                                                           |

## Manual Test Sets

The manual test project is split into compile-time sets so large runs can be parallelized.

| TestSet | Coverage                                                                                                                                         |
|---------|--------------------------------------------------------------------------------------------------------------------------------------------------|
| `1`     | Smaller SQL connectivity and command scenarios.                                                                                                  |
| `2`     | Broad data access coverage, including adapters, bulk copy, retry logic, data reader, schema, DNS caching, and related scenarios.                 |
| `3`     | Additional integration coverage, including LocalDB, pooling, parameters, transactions, JSON, Kerberos, UDT, vector, and other SQL feature tests. |
| `AE`    | Always Encrypted tests.                                                                                                                          |

If `TestSet` is omitted, all sets are compiled and run. You can combine sets by concatenating values, for example
`-p:TestSet=23` or `-p:TestSet=12AE`.

## Results and Diagnostics

Test results are written to `test_results` by default. Override the location with `TestResultsFolderPath`:

```bash
msbuild build.proj -t:TestSqlClientUnit -p:TestResultsFolderPath=/tmp/sqlclient-test-results
```

Hang blame collection is enabled by default with a `10m` timeout. To increase the timeout:

```bash
msbuild build.proj -t:TestSqlClientManual -p:TestBlameTimeout=30m
```

To disable hang blame collection:

```bash
msbuild build.proj -t:TestSqlClientManual -p:TestBlameTimeout=0
```

Code coverage is enabled by default. To disable it for a faster local run:

```bash
msbuild build.proj -t:TestSqlClientUnit -p:TestCodeCoverage=false
```

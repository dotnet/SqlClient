# Guidelines for Building Microsoft.Data.SqlClient

This document provides all the necessary details to build the driver and run tests present in the repository.

## Visual Studio Pre-Requisites

This project should be built with Visual Studio 2019+ for the best compatibility. The required set of components are provided in the below file:

- **Visual Studio 2019** with imported components: [VS19Components](/tools/vsconfig/VS19Components.vsconfig)

- **Powershell**: To build SqlClient on Linux, powershell is needed as well. Follow the distro specific instructions at [Install Powershell on Linux](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-linux?view=powershell-7.4)

Once the environment is setup properly, execute the desired set of commands below from the _root_ folder to perform the respective operations:

## MSBuild Reference

### Targets

The following build targets are defined in `build.proj`:

|Target|Description|
|-|-|
|`BuildAllConfigurations`|Default target. Builds the .NET Framework and .NET drivers for all target frameworks and operating systems.|
|`BuildAbstractions`|Restore, build, and pack the Abstractions package, publishing the resulting NuGet into `packages/`.|
|`BuildNetCore`|Builds the .NET driver for all target frameworks.|
|`BuildNetCoreAllOS`|Builds the .NET driver for all target frameworks and operating systems.|
|`BuildNetFx`|Builds the .NET Framework driver for all target frameworks.|
|`BuildTestsNetCore`|Builds tests for the .NET driver.|
|`BuildTestsNetFx`|Builds tests for the .NET Framework driver.|
|`Clean`|Cleans all generated files.|
|`Restore`|Restores NuGet packages.|
|`RunTests`|Runs the unit, functional, and manual tests for the .NET Framework and .NET drivers|
|`RunUnitTests`|Runs just the unit tests for the .NET Framework and .NET drivers|
|`RunFunctionalTests`|Runs just the functional tests for the .NET Framework and .NET drivers|
|`RunManualTests`|Runs just the manual tests for the .NET Framework and .NET drivers|
|`BuildAkv`|Builds the Azure Key Vault Provider package for all supported platforms.|

### Parameters

The following parameters may be defined as MSBuild properties to configure the
build:

|Name|Supported Values|Default|Description|
|-|-|-|-|
|`Configuration`|`Debug`, `Release`|`Debug`|Sets the release configuration.|
|`OSGroup`|`Unix`, `Windows_NT`, `AnyOS`|typically defaults to the client system's OS, unless using `BuildAllConfigurations` or an `AnyOS` specific target|The operating system to target.|
|`Platform`|`AnyCPU`, `x86`, `x64`, `ARM`, `ARM64`|`AnyCPU`|May only be set when using package reference type or running tests.|
|`TestSet`|`1`, `2`, `3`, `AE`|all|Build or run a subset of the manual tests. Omit (default) to target all tests.|
|`DotnetPath`|Absolute file path to an installed `dotnet` version.|The system default specified by the path variable|Set to run tests using a specific dotnet version (e.g. C:\net6-win-x86\)|
|`TF`|`net8.0`, `net462`, `net47`, `net471`, `net472`, `net48`, `net481`|`net8.0` in netcore, `net462` in netfx|Sets the target framework when building or running tests. Not applicable when building the drivers.|
|`ResultsDirectory`|An absolute file path|./TestResults relative to current directory|Specifies where to write test results.|

## Example Workflows using MSBuild (Recommended)

Using the default configuration and running all tests:

```bash
msbuild -t:BuildTestsNetFx -p:TF=net462
msbuild -t:BuildTestsNetCore
msbuild -t:RunTests
```

Using the Release configuration:

```bash
msbuild -t:BuildTestsNetFx -p:TF=net462 -p:Configuration=Release
msbuild -t:BuildTestsNetCore -p:Configuration=Release
msbuild -t:RunTests -p:Configuration=Release
```

Running only the unit tests:

```bash
msbuild -t:BuildTestsNetFx -p:TF=net462
msbuild -t:BuildTestsNetCore
msbuild -t:RunUnitTests
```

Using a specific .NET runtime to run tests:

```bash
msbuild -t:BuildTestsNetFx -p:TF=net462
msbuild -t:BuildTestsNetCore
msbuild -t:RunTests -p:DotnetPath=C:\net8-win-x86\
```

### Running Manual Tests

#### Pre-Requisites for running Manual tests

Manual Tests require the below setup to run:

- SQL Server with enabled Shared Memory, TCP and Named Pipes Protocols and access to the Client OS.
- Databases "NORTHWIND" and "UdtTestDb" present in SQL Server, created using SQL scripts [createNorthwindDb.sql](tools/testsql/createNorthwindDb.sql) and [createUdtTestDb.sql](tools/testsql/createUdtTestDb.sql). To setup an Azure Database with "NORTHWIND" tables, use SQL Script: [createNorthwindAzureDb.sql](tools/testsql/createNorthwindAzureDb.sql).
- Make a copy of the configuration file [config.default.json](src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities/config.default.json) and rename it to `config.json`. Update the values in `config.json`:

  |Property|Description|Value|
  |------|--------|-------------------|
  |TCPConnectionString | Connection String for a TCP enabled SQL Server instance. | `Server={servername};Database={Database_Name};Trusted_Connection=True;` <br/> OR `Data Source={servername};Initial Catalog={Database_Name};Integrated Security=True;`|
  |NPConnectionString | Connection String for a Named Pipes enabled SQL Server instance.| `Server=\\{servername}\pipe\sql\query;Database={Database_Name};Trusted_Connection=True;` <br/> OR <br/> `Data Source=np:{servername};Initial Catalog={Database_Name};Integrated Security=True;`|
  |TCPConnectionStringHGSVBS | (Optional) Connection String for a TCP enabled SQL Server with Host Guardian Service (HGS) attestation protocol configuration. | `Server=tcp:{servername}; Database={Database_Name}; UID={UID}; PWD={PWD}; Attestation Protocol = HGS; Enclave Attestation Url = {AttestationURL};`|
  |TCPConnectionStringNoneVBS | (Optional) Connection String for a TCP enabled SQL Server with a VBS Enclave and using None Attestation protocol configuration. | `Server=tcp:{servername}; Database={Database_Name}; UID={UID}; PWD={PWD}; Attestation Protocol = NONE;`|
  |TCPConnectionStringAASSGX | (Optional) Connection String for a TCP enabled SQL Server with a SGX Enclave and using Microsoft Azure Attestation (AAS) attestation protocol configuration. | `Server=tcp:{servername}; Database={Database_Name}; UID={UID}; PWD={PWD}; Attestation Protocol = AAS; Enclave Attestation Url = {AttestationURL};`|
  |EnclaveEnabled | Enables tests requiring an enclave-configured server.|
  |TracingEnabled | Enables EventSource related tests |
  |AADAuthorityURL | (Optional) Identifies the OAuth2 authority resource for `Server` specified in `AADPasswordConnectionString` | `https://login.windows.net/<tenant>`, where `<tenant>` is the tenant ID of the Azure Active Directory (Azure AD) tenant |
  |AADPasswordConnectionString | (Optional) Connection String for testing Azure Active Directory Password Authentication. | `Data Source={server.database.windows.net}; Initial Catalog={Azure_DB_Name};Authentication=Active Directory Password; User ID={AAD_User}; Password={AAD_User_Password};`|
  |AADSecurePrincipalId | (Optional) The Application Id of a registered application which has been granted permission to the database defined in the AADPasswordConnectionString. | {Application ID} |
  |AADSecurePrincipalSecret | (Optional) A Secret defined for a registered application which has been granted permission to the database defined in the AADPasswordConnectionString. | {Secret} |
  |AzureKeyVaultURL | (Optional) Azure Key Vault Identifier URL | `https://{keyvaultname}.vault.azure.net/` |
  |AzureKeyVaultTenantId | (Optional) The Azure Active Directory tenant (directory) Id of the service principal. | _{Tenant ID of Active Directory}_ |
  |SupportsIntegratedSecurity | (Optional) Whether or not the USER running tests has integrated security access to the target SQL Server.| `true` OR `false`|  
  |LocalDbAppName | (Optional) If Local Db Testing is supported, this property configures the name of Local DB App instance available in client environment. Empty string value disables Local Db testing. | Name of Local Db App to connect to.|
  |LocalDbSharedInstanceName | (Optional) If LocalDB testing is supported and the instance is shared, this property configures the name of the shared instance of LocalDB to connect to. | Name of shared instance of LocalDB. |
  |FileStreamDirectory | (Optional) If File Stream is enabled on SQL Server, pass local directory path to be used for setting up File Stream enabled database. |  `D:\\escaped\\absolute\\path\\to\\directory\\` |
  |UseManagedSNIOnWindows | (Optional) Enables testing with Managed SNI on Windows| `true` OR `false`|
  |DNSCachingConnString | Connection string for a server that supports DNS Caching|
  |EnclaveAzureDatabaseConnString | (Optional) Connection string for Azure database with enclaves |
  |ManagedIdentitySupported | (Optional) When set to `false` **Managed Identity** related tests won't run. The default value is `true`. |
  |IsManagedInstance | (Optional) When set to `true` **TVP** related tests will use on non-Azure bs files to compare test results. this is needed when testing against Managed Instances or TVP Tests will fail on Test set 3. The default value is `false`. |
  |PowerShellPath | The full path to PowerShell.exe. This is not required if the path is present in the PATH environment variable. | `D:\\escaped\\absolute\\path\\to\\PowerShell.exe` |

## Example workflows using the Dotnet SDK

### Run Functional Tests

- Windows (`netfx x86`):

```bash
dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" -p:Platform="x86" -p:Configuration="Release" -p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

- Windows (`netfx x64`):

```bash
dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" -p:Platform="x64" -p:Configuration="Release" -p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

- AnyCPU:
  
  Project reference only builds Driver with `AnyCPU` platform, and underlying process decides to run the tests with a compatible architecture (x64, x86, ARM64).

  Windows (`netcoreapp`):
  
```bash
dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" -p:Platform="AnyCPU" -p:Configuration="Release" -p:TestTargetOS="Windowsnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"
```

  Unix (`netcoreapp`):

```bash
dotnet test "src/Microsoft.Data.SqlClient/tests/FunctionalTests/Microsoft.Data.SqlClient.Tests.csproj" -p:Platform="AnyCPU" -p:Configuration="Release" -p:TestTargetOS="Unixnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"
```

### Run Manual Tests

- Windows (`netfx x86`):

```bash
dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Platform="x86" -p:Configuration="Release" -p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
  ```

- Windows (`netfx x64`):

```bash
dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Platform="x64" -p:Configuration="Release" -p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

- Windows (`netfx`):

```bash
dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Platform="AnyCPU" -p:Configuration="Release" -p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

- Windows (`netcoreapp`):

```bash
dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Platform="AnyCPU" -p:Configuration="Release" -p:TestTargetOS="Windowsnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"
```

- Unix (`netcoreapp`):

```bash
dotnet test "src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Platform="AnyCPU" -p:Configuration="Release" -p:TestTargetOS="Unixnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"
```

## Run A Single Test

```bash
dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Platform="AnyCPU" -p:Configuration="Release" -p:TestTargetOS="Windowsnetcoreapp" --no-build -v n --filter "FullyQualifiedName=Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.CspProviderExt.TestKeysFromCertificatesCreatedWithMultipleCryptoProviders"
```

## Testing with Custom ReferenceType

The MDS driver consists of several components, each of which produces its own
NuGet package.  During development, components reference each other via
`<ProjectReference>` properties by default.  This means that building
and testing one component will implicitly build its project referenced
dependencies.

Alternatively, the `ReferenceType` build property property may be specified with
a value of `Package`.  This will change inter-component dependencies to use
`<PackageReference>` dependencies, and require that dependent components be
built and packaged before building the depending component.  In this scenario,
the root `NuGet.Config` file must be updated to include the following entry
under the `<packageSources>` element:

```xml
<configuration>
  <packageSources>
    ...  
    <add key="local" value="packages/" />
  </packageSources>
</configuration>
```

Then, you can specify `Package` references be used, for example:

```bash
dotnet build -t:BuildAbstractions
dotnet build -t:BuildAzure -p:ReferenceType=Package
dotnet build -t:BuildAll -p:ReferenceType=Package
dotnet build -t:BuildAKVNetCore -p:ReferenceType=Package
dotnet build -t:GenerateMdsPackage
dotnet build -t:GenerateAkvPackage
dotnet build -t:BuildTestsNetCore -p:ReferenceType=Package
```

The above will build the Abstractions, Azure, MDS, and AKV components, place
their NuGet packages into the `packages/` directory, and then build the tests
using those packages.

A non-AnyCPU platform reference can only be used with package reference type.
Otherwise, the specified platform will be replaced with AnyCPU in the build
process.

### Building Tests with Reference Type

For .NET:

```bash
# Project is the default reference type.  The below commands are equivalent:
msbuild -t:BuildTestsNetCore
msbuild -t:BuildTestsNetCore -p:ReferenceType=Project

# Package reference type:
msbuild -t:BuildTestsNetCore -p:ReferenceType=Package
```

For .NET Framework:

```bash
# Project is the default reference type.  The below commands are equivalent:
msbuild -t:BuildTestsNetFx -p:TF=net462
msbuild -t:BuildTestsNetFx -p:TF=net462 -p:ReferenceType=Project

# Package reference type:
msbuild -t:BuildTestsNetFx -p:TF=net462 -p:ReferenceType=Package
```

### Running Tests with Reference Type

Provide property to `dotnet test` commands for testing desired reference type.

```bash
dotnet test -p:ReferenceType=Project ...
```

## Testing with Custom TargetFramework (traditional)

Tests can be built and run with custom Target Frameworks. See the below examples.

### Building Tests with custom target framework

```bash
# Build the tests for custom .NET Framework target
msbuild -t:BuildTestsNetFx -p:TF=net462
```

```bash
# Build the tests for custom .NET target
msbuild -t:BuildTestsNetCore -p:TF=net8.0
```

### Running Tests with custom target framework (traditional)

```bash
# Run tests with custom .NET Framework target
dotnet test -p:TargetNetFxVersion=net462 ...

# Run tests with custom .NET target
dotnet test -p:TargetNetCoreVersion=net8.0 ...
```

## Using Managed SNI on Windows

Managed SNI can be enabled on Windows by enabling the below AppContext switch:

`Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows`

## Set truncation on for scaled decimal parameters

Scaled decimal parameter truncation can be enabled by enabling the below AppContext switch:

`Switch.Microsoft.Data.SqlClient.TruncateScaledDecimal`

## Enabling row version null behavior

`SqlDataReader` returns a `DBNull` value instead of an empty `byte[]`. To enable the legacy behavior, you must enable the following AppContext switch on application startup:

`Switch.Microsoft.Data.SqlClient.LegacyRowVersionNullBehavior`

## Suppressing TLS security warning

When connecting to a server, if a protocol lower than TLS 1.2 is negotiated, a security warning is output to the console. This warning can be suppressed on SQL connections with `Encrypt = false` by enabling the following AppContext switch on application startup:

`Switch.Microsoft.Data.SqlClient.SuppressInsecureTLSWarning`

## Collecting Code Coverage

### Using VSTest

```bash
dotnet test <test_properties...> --collect:"Code Coverage"
```

### Using Coverlet Collector

```bash
dotnet test <test_properties...> --collect:"XPlat Code Coverage"
```

## Run Performance Tests

The performance tests live here:
`src\Microsoft.Data.SqlClient\tests\PerformanceTests\`

They can be run from the command line by following the instructions below.

Launch a shell and change into the project directory:

PowerShell:

```pwsh
> cd src\Microsoft.Data.SqlClient\tests\PerformanceTests
```

Bash:

```bash
$ cd src/Microsoft.Data.SqlClient/tests/PerformanceTests
```

### Create Database

Create an empty database for the benchmarks to use.  This example assumes
a local SQL server instance using SQL authentication:

```bash
$ sqlcmd -S localhost -U sa -P password
1> create database [sqlclient-perf-db]
2> go
1> quit
```

The default `runnerconfig.json` expects a database named `sqlclient-perf-db`,
but you may change the config to use any existing database.  All tables in
the database will be dropped when running the benchmarks.

### Configure Runner

Configure the benchmarks by editing the `runnerconfig.json` file directly in the
`PerformanceTests` directory with an appropriate connection string and benchmark
settings:

```json
{
  "ConnectionString": "Server=tcp:localhost; Integrated Security=true; Initial Catalog=sqlclient-perf-db;",
  "UseManagedSniOnWindows": false,
  "Benchmarks":
  {
    "SqlConnectionRunnerConfig":
    {
      "Enabled": true,
      "LaunchCount": 1,
      "IterationCount": 50,
      "InvocationCount":30,
      "WarmupCount": 5,
      "RowCount": 0
    },
    ...
  }
}
```

Individual benchmarks may be enabled or disabled, and each has several
benchmarking options for fine tuning.

After making edits to `runnerconfig.json` you must perform a build which will
copy the file into the `artifacts` directory alongside the benchmark DLL.  By
default, the benchmarks look for `runnerconfig.json` in the same directory as
the DLL.

Optionally, to avoid polluting your git workspace and requring a build after
each config change, copy `runnerconfig.json` to a new file, make your edits
there, and then specify the new file with the RUNNER_CONFIG environment
variable.

PowerShell:

```pwsh
> copy runnerconfig.json $HOME\.configs\runnerconfig.json

# Make edits to $HOME\.configs\runnerconfig.json

# You must set the RUNNER_CONFIG environment variable for the current shell.
> $env:RUNNER_CONFIG="${HOME}\.configs\runnerconfig.json"
```

Bash:

```bash
$ cp runnerconfig.json ~/.configs/runnerconfig.json

# Make edits to ~/.configs/runnerconfig.json

# Optionally export RUNNER_CONFIG.
$ export RUNNER_CONFIG=~/.configs/runnerconfig.json
```

### Run Benchmarks

All benchmarks must be compiled and run in **Release** configuration.

PowerShell:

```pwsh
> dotnet run -c Release -f net9.0
```

Bash:

```bash
# Omit RUNNER_CONFIG if you exported it earlier, or if you're using the
# copy prepared by the build.
$ dotnet run -c Release -f net9.0

$ RUNNER_CONFIG=~/.configs/runnerconfig.json dotnet run -c Release -f net9.0
```

# Guidelines for Building Microsoft.Data.SqlClient

This document provides all the necessary details to build the driver and run tests present in the repository.

## Visual Studio Pre-Requisites

This project should be built with Visual Studio 2019+ for the best compatibility. The required set of components are provided in the below file:

- **Visual Studio 2019** with imported components: [VS19Components](/tools/vsconfig/VS19Components.vsconfig)

Once the environment is setup properly, execute the desired set of commands below from the _root_ folder to perform the respective operations:

## Building the driver

```bash
# Default Build Configuration:

msbuild
# Builds the driver for the Client OS in 'Debug' Configuration for 'AnyCPU' platform.
# Both .NET Framework (NetFx) and .NET (CoreFx) drivers are built by default (as supported by Client OS).
```

```bash
msbuild -t:clean
# Cleans all build directories.
```

```bash
msbuild -p:Configuration=Release
# Builds the driver in 'Release' Configuration for `AnyCPU` platform.
```

```bash
msbuild -t:restore
# Restores Nuget Packages.
```

```bash
msbuild -t:BuildAllConfigurations
# Builds the driver for all target OSes and supported platforms.
```

```bash
msbuild -p:BuildNetFx=false
# Skips building the .NET Framework (NetFx) Driver on Windows.
# On Unix the netfx driver build is automatically skipped.
```

```bash
msbuild -p:OSGroup=Unix
# Builds the driver for the Unix platform.
```

```bash
msbuild -t:BuildNetCoreAllOS
# Builds the .NET driver for all Operating Systems.
```

## Building Tests

```bash
msbuild -t:BuildTestsNetCore
# Build the tests for the .NET driver in 'Debug' Configuration. Default .NET version is 6.0.
```

```bash
msbuild -t:BuildTestsNetFx
# Build the tests for the .NET Framework (NetFx) driver in 'Debug' Configuration. Default .NET Framework version is 4.6.2.
```

```bash
msbuild -t:BuildTestsNetCore -p:TestSet=1
# Build a subset of the manual tests. Valid values: '1', '2', '3', 'AE'. Omit to build all tests.
```

## Running Tests

There are 2 ways to run tests, using MsBuild or Dotnet SDK.

### Running from Build.proj

```bash
msbuild -t:RunFunctionalTests
# Run all functional tests in Debug configuration for *default* target framework (.NET 6.0).
```

```bash
msbuild -t:RunManualTests
# Run all manual tests in Debug configuration for *default* target framework (.NET 6.0).
```

```bash
msbuild -t:RunTests -p:configuration=Release
# Run both functional and manual tests in Release configuration for *default* target framework (.NET 6.0).
```

```bash
msbuild -t:RunTests -p:configuration=Release -p:DotnetPath=C:\net6-win-x86\
# Run both functional and manual tests in Release configuration for *default* target framework (.NET 6.0) against the installed dotnet tool in the provided path.
```

To specify custom target framework, use `TF` property:

```bash
msbuild -t:RunTests -p:configuration=Release -p:TF=net7.0
msbuild -t:RunTests -p:configuration=Release -p:TF=net48
# Runs tests for specified target framework. 
# TargetNetCoreVersion and TargetNetFxVersion are not to be used with TF property, they will take precedence over TF if provided.
```

To capture test and code coverage results in a custom directory:

```bash
msbuild -t:RunTests -p:ResultsDirectory=MyDirectory
# Runs tests with test and code coverage results placed in provided results directory.
# Default results directory is "TestResults".
```

Other properties can be set alongside as needed.

### Running using Dotnet SDK (traditional)

#### Run Functional Tests

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

#### Run Manual Tests

### Pre-Requisites for running Manual tests

Manual Tests require the below setup to run:

- SQL Server with enabled Shared Memory, TCP and Named Pipes Protocols and access to the Client OS.
- Databases "NORTHWIND" and "UdtTestDb" present in SQL Server, created using SQL scripts [createNorthwindDb.sql](tools/testsql/createNorthwindDb.sql) and [createUdtTestDb.sql](tools/testsql/createUdtTestDb.sql). To setup an Azure Database with "NORTHWIND" tables, use SQL Script: [createNorthwindAzureDb.sql](tools/testsql/createNorthwindAzureDb.sql).
- Make a copy of the configuration file [config.default.json](src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities/config.default.json) and rename it to `config.json`. Update the values in `config.json`:

  |Property|Description|Value|
  |------|--------|-------------------|
  |TCPConnectionString | Connection String for a TCP enabled SQL Server instance. | `Server={servername};Database={Database_Name};Trusted_Connection=True;` <br/> OR `Data Source={servername};Initial Catalog={Database_Name};Integrated Security=True;`|
  |NPConnectionString | Connection String for a Named Pipes enabled SQL Server instance.| `Server=\\{servername}\pipe\sql\query;Database={Database_Name};Trusted_Connection=True;` <br/> OR <br/> `Data Source=np:{servername};Initial Catalog={Database_Name};Integrated Security=True;`|
  |TCPConnectionStringHGSVBS | (Optional) Connection String for a TCP enabled SQL Server with Host Guardian Service (HGS) attestation protocol configuration. | `Server=tcp:{servername}; Database={Database_Name}; UID={UID}; PWD={PWD}; Attestation Protocol = HGS; Enclave Attestation Url = {AttestationURL};`|
  |TCPConnectionStringAASVBS | (Optional) Connection String for a TCP enabled SQL Server with a VBS Enclave and using Microsoft Azure Attestation (AAS) attestation protocol configuration. | `Server=tcp:{servername}; Database={Database_Name}; UID={UID}; PWD={PWD}; Attestation Protocol = AAS; Enclave Attestation Url = {AttestationURL};`|
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
  |AzureKeyVaultClientId | (Optional) "Application (client) ID" of an Active Directory registered application, granted access to the Azure Key Vault specified in `AZURE_KEY_VAULT_URL`. Requires the key permissions Get, List, Import, Decrypt, Encrypt, Unwrap, Wrap, Verify, and Sign. | _{Client Application ID}_ |
  |AzureKeyVaultClientSecret | (Optional) "Client Secret" of the Active Directory registered application, granted access to the Azure Key Vault specified in `AZURE_KEY_VAULT_URL` | _{Client Application Secret}_ |
  |SupportsIntegratedSecurity | (Optional) Whether or not the USER running tests has integrated security access to the target SQL Server.| `true` OR `false`|  
  |LocalDbAppName | (Optional) If Local Db Testing is supported, this property configures the name of Local DB App instance available in client environment. Empty string value disables Local Db testing. | Name of Local Db App to connect to.|
  |LocalDbSharedInstanceName | (Optional) If LocalDB testing is supported and the instance is shared, this property configures the name of the shared instance of LocalDB to connect to. | Name of shared instance of LocalDB. |
  |FileStreamDirectory | (Optional) If File Stream is enabled on SQL Server, pass local directory path to be used for setting up File Stream enabled database. |  `D:\\escaped\\absolute\\path\\to\\directory\\` |
  |UseManagedSNIOnWindows | (Optional) Enables testing with Managed SNI on Windows| `true` OR `false`|
  |DNSCachingConnString | Connection string for a server that supports DNS Caching|
  |IsAzureSynpase | (Optional) When set to 'true', test suite runs compatible tests for Azure Synapse/Parallel Data Warehouse. | `true` OR `false`|
  |EnclaveAzureDatabaseConnString | (Optional) Connection string for Azure database with enclaves |
  |ManagedIdentitySupported | (Optional) When set to `false` **Managed Identity** related tests won't run. The default value is `true`. |
  |IsManagedInstance | (Optional) When set to `true` **TVP** related tests will use on non-Azure bs files to compare test results. this is needed when testing against Managed Instances or TVP Tests will fail on Test set 3. The default value is `false`. |
  |PowerShellPath | The full path to PowerShell.exe. This is not required if the path is present in the PATH environment variable. | `D:\\escaped\\absolute\\path\\to\\PowerShell.exe` |

### Commands to run Manual Tests

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

Tests can be built and run with custom "Reference Type" property that enables different styles of testing:

- "Project" => Build and run tests with Microsoft.Data.SqlClient as Project Reference
- "Package" => Build and run tests with Microsoft.Data.SqlClient as Package Reference with configured "TestMicrosoftDataSqlClientVersion" in "Versions.props" file.
- "NetStandard" => Build and run tests with Microsoft.Data.SqlClient as Project Reference via .NET Standard Library
- "NetStandardPackage" => Build and run tests with Microsoft.Data.SqlClient as Package Reference via .NET Standard Library

> ************** IMPORTANT NOTE BEFORE PROCEEDING WITH "PACKAGE" AND "NETSTANDARDPACKAGE" REFERENCE TYPES ***************
> CREATE A NUGET PACKAGE WITH BELOW COMMAND AND ADD TO LOCAL FOLDER + UPDATE NUGET CONFIG FILE TO READ FROM THAT LOCATION
>
> ```bash
>  msbuild -p:configuration=Release
> ```

A non-AnyCPU platform reference can only be used with package and NetStandardPackage reference types. Otherwise, the specified platform will be replaced with AnyCPU in the build process.

### Building Tests with Reference Type

For .NET, all 4 reference types are supported:

```bash
msbuild -t:BuildTestsNetCore -p:ReferenceType=Project
# Default setting uses Project Reference.

msbuild -t:BuildTestsNetCore -p:ReferenceType=Package

msbuild -t:BuildTestsNetCore -p:ReferenceType=NetStandard

msbuild -t:BuildTestsNetCore -p:ReferenceType=NetStandardPackage
```

For .NET Framework, below reference types are supported:

```bash
msbuild -t:BuildTestsNetFx -p:ReferenceType=Project
# Default setting uses Project Reference.

msbuild -t:BuildTestsNetFx -p:ReferenceType=Package
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
msbuild -t:BuildTestsNetFx -p:TargetNetFxVersion=net462
# Build the tests for custom TargetFramework (.NET Framework)
# Applicable values: net462 (Default) | net47 | net471  net472 | net48 | net481
```

```bash
msbuild -t:BuildTestsNetCore -p:TargetNetCoreVersion=net6.0
# Build the tests for custom TargetFramework (.NET)
# Applicable values: net6.0 | net7.0
```

### Running Tests with custom target framework (traditional)

```bash
dotnet test -p:TargetNetFxVersion=net462 ...
# Use above property to run Functional Tests with custom TargetFramework (.NET Framework)
# Applicable values: net462 (Default) | net47 | net471  net472 | net48 | net481

dotnet test -p:TargetNetCoreVersion=net6.0 ...
# Use above property to run Functional Tests with custom TargetFramework (.NET)
# Applicable values: net6.0 | net7.0
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

## Debugging SqlClient on Linux from Windows

For enhanced developer experience, we support debugging SqlClient on Linux from Windows, using the project "**Microsoft.Data.SqlClient.DockerLinuxTest**" that requires "Container Tools" to be enabled in Visual Studio. You may import configuration: [VS19Components.vsconfig](./tools/vsconfig/VS19Components.vsconfig) if not enabled already.

This project is also included in `docker-compose.yml` to demonstrate connectivity with SQL Server docker image.

To run the same:

1. Build the Solution in Visual Studio
2. Set  `docker-compose` as Startup Project
3. Run "Docker-Compose" launch configuration.
4. You will see similar message in Debug window:

    ```log
    Connected to SQL Server v15.00.4023 from Unix 4.19.76.0
    The program 'dotnet' has exited with code 0 (0x0).
    ```

5. Now you can write code in [Program.cs](/src/Microsoft.Data.SqlClient/tests/DockerLinuxTest/Program.cs) to debug SqlClient on Linux!

### Troubleshooting Docker issues

There may be times where connection cannot be made to SQL Server, we found below ideas helpful:

- Clear Docker images to create clean image from time-to-time, and clear docker cache if needed by running `docker system prune` in Command Prompt.

- If you face `Microsoft.Data.SqlClient.SNI.dll not found` errors when debugging, try updating the below properties in the netcore\Microsoft.Data.SqlClient.csproj file and try again:

  ```xml
    <OSGroup>Unix</OSGroup>
    <TargetsWindows>false</TargetsWindows>
    <TargetsUnix>true</TargetsUnix>
  ```

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

### Running Performance test project directly

Project location from Root: `src\Microsoft.Data.SqlClient\tests\PerformanceTests\Microsoft.Data.SqlClient.PerformanceTests.csproj`
Configure `runnerconfig.json` file with connection string and preferred settings to run Benchmark Jobs.

```bash
cd src\Microsoft.Data.SqlClient\tests\PerformanceTests
dotnet run -c Release -f net6.0|net7.0
```

_Only "**Release** Configuration" applies to Performance Tests_

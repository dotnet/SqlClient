# Guidelines for Building Microsoft.Data.SqlClient

This document provides all the necessary details to build the driver and run tests present in the repository.

## Visual Studio Pre-Requisites

This project should be ideally built with Visual Studio 2017+ for the best compatibility. Use either of the two environments with their required set of compoenents as mentioned below:
- **Visual Studio 2017** with imported components: [VS17Components](/tools/vsconfig/VS17Components.vsconfig)
- **Visual Studio 2019** with imported components: [VS19Components](/tools/vsconfig/VS19Components.vsconfig)

Once the environment is setup properly, execute the desired set of commands below from the _root_ folder to perform the respective operations:

## Building the driver

```bash
# Default Build Configuration:

> msbuild
# Builds the driver for the Client OS in 'Debug' Configuration for 'AnyCPU' platform.
# Both .NET Framework  (NetFx) and .NET Core drivers are built by default (as supported by Client OS).
```

```bash
> msbuild /p:Configuration=Release
# Builds the driver in 'Release' Configuration.
```

```bash
> msbuild /p:Platform=x86
# Builds the .NET Framework  (NetFx) driver for Win32 (x86) platform on Windows.
```

```bash
> msbuild /t:clean
# Cleans all build directories.
```

```bash
> msbuild /t:restore
# Restores Nuget Packages.
```

```bash
> msbuild /t:BuildAllConfigurations
# Builds the driver for all target OSes and supported platforms.
```

```bash
> msbuild /p:BuildNetFx=false
# Skips building the .NET Framework (NetFx) Driver on Windows.
# On Unix the netfx driver build is automatically skipped.
```

```bash
> msbuild /p:OSGroup=Unix
# Builds the driver for the Unix platform.
```

```bash
> msbuild /t:BuildAllOSes
# Builds the driver for all Operating Systems.
```

## Building Tests

```bash
> msbuild /t:BuildTestsNetCore
# Build the tests for the .NET Core driver. Default .NET Core version is 2.1.
```

```bash
> msbuild /t:BuildTestsNetFx
# Build the tests for the .NET Framework (NetFx) driver. Default .NET Framework version is 4.6.
```

## Run Functional Tests

Windows (`netfx x86`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="Win32" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

Windows (`netfx x64`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="x64" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

Windows (`netcoreapp`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Windowsnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"
```

Unix (`netcoreapp`):  
```bash
> dotnet test "src/Microsoft.Data.SqlClient/tests/FunctionalTests/Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Unixnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"
```

## Run Manual Tests

### Pre-Requisites for running Manual tests:
Manual Tests require the below setup to run:
* SQL Server with enabled Shared Memory, TCP and Named Pipes Protocols and access to the Client OS.
* Databases "NORTHWIND" and "UdtTestDb" present in SQL Server, created using SQL scripts [createNorthwindDb.sql](tools\testsql\createNorthwindDb.sql) and [createUdtTestDb.sql](tools\testsql\createUdtTestDb.sql).
* Configuration file [config.json](src\Microsoft.Data.SqlClient\tests\ManualTests\config.json) updated with values:

|Property|Description|Value|
|------|--------|-------------------|
|TCPConnectionString | Connection String for a TCP enabled SQL Server instance. | `Server={servername};Database={Database_Name};Trusted_Connection=True;` <br/> OR `Data Source={servername};Initial Catalog={Database_Name};Integrated Security=True;`|
|NPConnectionString | Connection String for a Named Pipes enabled SQL Server instance.| `Server=\\{servername}\pipe\sql\query;Database={Database_Name};Trusted_Connection=True;` <br/> OR <br/> `Data Source=np:{servername};Initial Catalog={Database_Name};Integrated Security=True;`|
|AADAccessToken | (Optional) Contains the Access Token to be used for tests.| _<OAuth 2.0 Access Token>_ |
|AADPasswordConnectionString | (Optional) Connection String for testing Azure Active Directory Password Authentication. | `Data Source={server.database.windows.net}; Initial Catalog={Azure_DB_Name};Authentication=Active Directory Password; User ID={AAD_User}; Password={AAD_User_Password};`|
|AzureKeyVaultURL | (Optional) Azure Key Vault Identifier URL | `https://{keyvaultname}.vault.azure.net/` |
|AzureKeyVaultClientId | (Optional) "Application (client) ID" of an Active Directory registered application, granted access to the Azure Key Vault specified in `AZURE_KEY_VAULT_URL`. Requires the key permissions Get, List, Import, Decrypt, Encrypt, Unwrap, Wrap, Verify, and Sign. | _{Client Application ID}_ |
|AzureKeyVaultClientSecret | (Optional) "Client Secret" of the Active Directory registered application, granted access to the Azure Key Vault specified in `AZURE_KEY_VAULT_URL` | _{Client Application Secret}_ |
|SupportsLocalDb | (Optional) Whether or not a LocalDb instance of SQL Server is installed on the machine running the tests. |`true` OR `false`|
|SupportsIntegratedSecurity | (Optional) Whether or not the USER running tests has integrated security access to the target SQL Server.| `true` OR `false`|
|SupportsFileStream | (Optional) Whether or not FileStream is enabled on SQL Server| `true` OR `false`|

Commands to run tests:

Windows (`netfx x86`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="Win32" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

Windows (`netfx x64`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="x64" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

Windows (`netcoreapp`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Windowsnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"
```

Unix (`netcoreapp`):  
```bash
> dotnet test "src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Unixnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"
```

## Run A Single Test
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Debug" /p:TestTargetOS="Windowsnetcoreapp" --no-build -v n --filter "FullyQualifiedName=Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.CspProviderExt.TestKeysFromCertificatesCreatedWithMultipleCryptoProviders"
```

## Testing with Custom TargetFramework

Tests can be built and run with custom Target Frameworks. See the below examples.

### Building Tests:

```bash
> msbuild /t:BuildTestsNetFx /p:TargetNetFxVersion=net461
# Build the tests for custom TargetFramework (.NET Framework)
# Applicable values: net46 (Default) | net461 | net462 | net47 | net471  net472 | net48
```

```bash
> msbuild /t:BuildTestsNetCore /p:TargetNetCoreVersion=netcoreapp3.0
# Build the tests for custom TargetFramework (.NET Core)
# Applicable values: netcoreapp2.1 | netcoreapp2.2 | netcoreapp3.0
```
### Running Tests:

```bash
> dotnet test /p:TargetNetFxVersion=net461 ...
# Use above property to run Functional Tests with custom TargetFramework (.NET Framework)
# Applicable values: net46 (Default) | net461 | net462 | net47 | net471  net472 | net48

> dotnet test /p:TargetNetCoreVersion=netcoreapp3.0 ...
# Use above property to run Functional Tests with custom TargetFramework (.NET Core)
# Applicable values: netcoreapp2.1 | netcoreapp2.2 | netcoreapp3.0
```

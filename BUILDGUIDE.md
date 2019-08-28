# Guidelines for Building Microsoft.Data.SqlClient

This document provides all necessary details to build driver and run tests present in the repository.

## Visual Studio Pre-Requisites

This project should be ideally built with Visual Studio 2017+ for best compatibility. Use either of the two environments with their required set of compoenents as mentioned below:
- **Visual Studio 2017** with imported components: [VS17Components](/tools/vsconfig/VS17Components.vsconfig)
- **Visual Studio 2019** with imported components: [VS19Components](/tools/vsconfig/VS19Components.vsconfig)

Once environment is setup properly, execute the desired set of commands as mentioned below from _root_ folder to perform respective operations:

## Building driver

```bash
# Default Build Configuration:

> msbuild
# Builds driver for Client OS in 'Debug' Configuration for 'AnyCPU' platform.
# Both .Net Framework (NetFx) and .Net Core drivers are built by default (as supported by Client OS).
```

```bash
> msbuild /p:Configuration=Release
# Builds driver in 'Release' Configuration.
```

```bash
> msbuild /p:Platform=x86
# Builds .Net Framework (NetFx) driver for Win32 (x86) platform on Windows.
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
# Builds both driver for all target OS and supported platforms.
```

```bash
> msbuild /p:BuildNetFx=false
# Skips building .Net Framework (NetFx) Driver on Windows.
# On Unix the netfx driver build is automatically skipped.
```

```bash
> msbuild /p:OSGroup=Unix
# Builds driver for Unix platform.
```

```bash
> msbuild /t:BuildAllOSes
# Builds driver for all Operating Systems.
```

## Building Tests

```bash
> msbuild /t:BuildTestsNetCore
# Build tests with .Net Core Driver project reference.
```

```bash
> msbuild /t:BuildTestsNetFx
# Build tests with .Net Framework (NetFx) Driver project reference.
```

## Run Functional Tests

Windows (`netcoreapp2.1`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Windowsnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"
```
 
Windows (`net46 x86`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="Win32" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
 ```

Windows (`net46 x64`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="x64" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

Unix (`netcoreapp2.1`):  
```bash
> dotnet test "src/Microsoft.Data.SqlClient/tests/FunctionalTests/Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Unixnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"
```

## Run Manual Tests

### Pre-Requisites for running Manual tests:
Manual Tests require below setup to run:
* SQL Server with enabled Shared Memory, TCP and Named Pipes Protocols and access to Client OS.
* Databases "NORTHWIND" and "UdtTestDb" present in SQL Server, created using SQL scripts [createNorthwindDb.sql](tools\testsql\createNorthwindDb.sql) and [createUdtTestDb.sql](tools\testsql\createUdtTestDb.sql).
* Environment variables configured in Client OS as under:

|Env Variable|Description|Value|
|------|--------|-------------------|
|TEST_NP_CONN_STR | Connection String for Named Pipes enabled SQL Server instance.| `Server=\\{servername}\pipe\sql\query;Database={Database_Name};Trusted_Connection=True;` <br/> OR <br/> `Data Source=np:{servername};Initial Catalog={Database_Name};Integrated Security=True;`|
|TEST_TCP_CONN_STR | Connection String for TCP enabled SQL Server instance. | `Server={servername};Database={Database_Name};Trusted_Connection=True;` <br/> OR `Data Source={servername};Initial Catalog={Database_Name};Integrated Security=True;`|
|AAD_PASSWORD_CONN_STR | (Optional) Connection String for testing Azure Active Directory Password Authentication. | `Data Source={server.database.windows.net}; Initial Catalog={Azure_DB_Name};Authentication=Active Directory Password; User ID={AAD_User}; Password={AAD_User_Password};`|
|TEST_ACCESSTOKEN_SETUP| (Optional) Contains Access Token to be used for tests.| _<OAuth 2.0 Access Token>_ |
|TEST_LOCALDB_INSTALLED| (Optional) Whether or not LocalDb instance of SQL Server is installed on the machine running the tests. |`1` OR `0`|
|TEST_INTEGRATEDSECURITY_SETUP| (Optional) Whether or not USER running tests has integrated security access to target SQL Server.| `1` OR `0`|
|TEST_FILESTREAM_SETUP| (Optional) Whether or not FileStream is enabled on SQL Server| `1` OR `0`|

Commands to run tests are as under:

Windows (`netcoreapp2.1`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Windowsnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"
```

Windows (`net46 x86`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="Win32" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

Windows (`net46 x64`):  
```bash
> dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="x64" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

Unix (`netcoreapp2.1`):  
```bash
> dotnet test "src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Unixnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"
```

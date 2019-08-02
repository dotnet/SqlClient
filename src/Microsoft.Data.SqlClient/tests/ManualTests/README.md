# SqlClient Manual Tests

These tests require dedicated test servers, so they're designed to be run manually using a custom set of connection strings. 

## Prerequisites

 - If you want to run the EFCore tests later you will need to build -allconfigurations to generate the NuGet packages, build -allconfigurations works only on windows.

 - an [MS SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-editions-express) (any edition) 2012 or later that you can connect to with tcp and named pipes, 

   **N.B**. if you want to run the EFCore tests it should be a dedicated instance because they create a lot of databases.

 - The `Northwind Sample Database`

 - The `UDT Test Database`

 - TCP and Named Pipe [connection strings](https://msdn.microsoft.com/en-us/library/system.data.sqlclient.sqlconnection.connectionstring.aspx) to your instance with Northwind set as the initial catalog



 ## Running All Tests

 1. Set the environment variables needed for the tests you want. At the minimum you need to set
    `TEST_NP_CONN_STR` and `TEST_TCP_CONN_STR` to the connection strings. 

 2. Optionally you may also want to setup other environment variables to test specific optional features such as `TEST_LOCALDB_INSTALLED` or `TEST_INTEGRATEDSECURITY_SETUP`. Other scenarios like azure tests may need configuration so if you see those being skipped and you want to run them invesigate the skipped test code to identify how to configure it.

 3. Run `msbuild /t:BuildTestsNetCore` for netcoreapp or `msbuild /t:BuildTestsNetFx` for net46 to build the debug version with all the assertions.

 4. Run Manual Tests
 `Windows` (netcoreapp2.1):  
```
dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Windowsnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"
```

`Windows` (net46 x86):  
```
dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="Win32" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

`Windows` (net46 x64):  
```
dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="x64" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```

`Unix` (netcoreapp2.1):  
```
dotnet test "src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Unixnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"
```


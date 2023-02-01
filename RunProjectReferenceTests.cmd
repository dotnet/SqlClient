@echo off

:: Default target frameworks
set netfxVersion=net462
set netcoreVersion=net6.0

:: Accept two parameters for .NET Framework and .NET versions.
:: Examples: 
::  - uses net48 and net7.0:
::      > RunProjectReferenceTests.cmd net48 net7.0
::  - uses default target frameworks:
::      > RunProjectReferenceTests.cmd 
::  - uses net48 and default target frameworks for netcore:
::      > RunProjectReferenceTests.cmd net48

if not "%~1" == "" set netfxVersion=%1
if not "%~2" == "" set netcoreVersion=%2

echo .NET Framework = %netfxVersion%
echo .NET Core = %netcoreVersion%

call :pauseOnError msbuild -t:Clean
:: .NET FRAMEWORK - REFERENCE TYPE "PROJECT"
:: Only Builds AnyCPU for project reference!

:: Based on `dotnet test` documentation, the `Platform` property has no effect on choosing the underlying architecture for the test execution environment.
:: You need to install and run the `dotnet` command for a specific architecture (x64, x86, Arm64).

echo Building .NET Framework %netfxVersion% Tests ...
call :pauseOnError msbuild -p:Configuration="Release"
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildAKVNetStAllOS
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildAKVNetFx
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildTestsNetFx -p:TargetNetFxVersion=%netfxVersion%

echo Running .NET Framework %netfxVersion% Tests ...
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" -p:Configuration="Release" -p:TestTargetOS="Windowsnetfx" -p:TargetNetFxVersion=%netfxVersion% --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests" -l:"trx;LogFileName=..\..\..\..\..\artifacts\Results\project-%netfxVersion%-functional-anycpu.xml"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Configuration="Release" -p:TestTargetOS="Windowsnetfx" -p:TargetNetFxVersion=%netfxVersion% --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests" -l:"trx;LogFileName=..\..\..\..\..\artifacts\Results\project-%netfxVersion%-manual-anycpu.xml"

echo Building .NET %netcoreVersion% Tests ...
call pause
call :pauseOnError msbuild -t:Clean
call :pauseOnError msbuild -p:Configuration="Release"
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildAKVNetStAllOS
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildAKVNetCoreAllOS
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildTestsNetCore -p:TargetNetCoreVersion=%netcoreVersion%

echo Running .NET %netcoreVersion% Tests ...
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" -p:Configuration="Release" -p:TestTargetOS="Windowsnetcoreapp" -p:TargetNetCoreVersion=%netcoreVersion% --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests" -l:"trx;LogFileName=..\..\..\..\..\artifacts\Results\project-%netcoreVersion%-functional-anycpu.xml"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Configuration="Release" -p:TestTargetOS="Windowsnetcoreapp" -p:TargetNetCoreVersion=%netcoreVersion% --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests" -l:"trx;LogFileName=..\..\..\..\..\artifacts\Results\project-%netcoreVersion%-manual-anycpu.xml"

goto :eof

:pauseOnError
%*
if ERRORLEVEL 1 pause
goto :eof

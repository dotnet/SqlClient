@echo off

call :pauseOnError msbuild -t:Clean
:: .NET FRAMEWORK - REFERENCE TYPE "PROJECT"
:: Only Builds AnyCPU for project reference!

:: Based on `dotnet test` documentation, the `Platform` property has no effect on choosing the underlying architecture for the test execution environment.
:: You need to install and run the `dotnet` command for a specific architecture (x64, x86, Arm64).

echo Building .NET Framework Tests ...
call :pauseOnError msbuild -p:Configuration="Release"
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildAKVNetStAllOS
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildAKVNetFx
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildTestsNetFx -p:TargetNetFxVersion=net462
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildTestsNetFx -p:TargetNetFxVersion=net48

echo Running .NET Framework Tests ...
call pause
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" -p:Configuration="Release" -p:TestTargetOS="Windowsnetfx" -p:TargetNetFxVersion=net462 --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests" -l:trx;LogFileName=..\..\..\..\..\artifacts\Results\project-net462-functional-anycpu.xml
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Configuration="Release" -p:TestTargetOS="Windowsnetfx" -p:TargetNetFxVersion=net462 --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests" -l:trx;LogFileName=..\..\..\..\..\artifacts\Results\project-net462-manual-anycpu.xml

call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj"-p:Configuration="Release" -p:TestTargetOS="Windowsnetfx" -p:TargetNetFxVersion=net48 --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests" -l:trx;LogFileName=..\..\..\..\..\artifacts\Results\project-net48-functional-anycpu.xml
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Configuration="Release" -p:TestTargetOS="Windowsnetfx"  -p:TargetNetFxVersion=net48 --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests" -l:trx;LogFileName=..\..\..\..\..\artifacts\Results\project-net48-manual-anycpu.xml

echo Building .NET Tests ...
call pause
call :pauseOnError msbuild -t:Clean
call :pauseOnError msbuild -p:Configuration="Release"
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildAKVNetStAllOS
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildAKVNetCoreAllOS
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildTestsNetCore -p:TargetNetCoreVersion=net6.0
call :pauseOnError msbuild -p:Configuration="Release" -t:BuildTestsNetCore -p:TargetNetCoreVersion=net7.0

echo Running .NET Tests ...
call pause
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" -p:Configuration="Release" -p:TestTargetOS="Windowsnetcoreapp" -p:TargetNetCoreVersion=net6.0 --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests" -l:trx;LogFileName=..\..\..\..\..\artifacts\Results\project-net6.0-functional-anycpu.xml
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Configuration="Release" -p:TestTargetOS="Windowsnetcoreapp" -p:TargetNetCoreVersion=net6.0 --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests" -l:trx;LogFileName=..\..\..\..\..\artifacts\Results\project-net6.0-manual-anycpu.xml

call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" -p:Configuration="Release" -p:TestTargetOS="Windowsnetcoreapp" -p:TargetNetCoreVersion=net7.0 --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests" -l:trx;LogFileName=..\..\..\..\..\artifacts\Results\project-net7.0-functional-anycpu.xml
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" -p:Configuration="Release" -p:TestTargetOS="Windowsnetcoreapp" -p:TargetNetCoreVersion=net7.0 --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests" -l:trx;LogFileName=..\..\..\..\..\artifacts\Results\project-net7.0-manual-anycpu.xml

goto :eof

:pauseOnError
%*
if ERRORLEVEL 1 pause
goto :eof

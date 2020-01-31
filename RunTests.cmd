@echo off
call :pauseOnError msbuild /p:Configuration="Release" /t:Clean,BuildAllConfigurations
echo Building Add-Ons
call :pauseOnError msbuild /p:Configuration="Release" /t:BuildAKVNetFx
call :pauseOnError msbuild /p:Configuration="Release" /t:BuildAKVNetCoreAllOS
call :pauseOnError msbuild /p:configuration="Release" /t:GenerateAKVProviderNugetPackage
echo Building tests (Net46 + NetCoreApp2.1)
call :pauseOnError msbuild /p:Configuration="Release" /t:BuildTestsNetFx
call :pauseOnError msbuild /p:Configuration="Release" /t:BuildTestsNetCore
echo Running SqlClient test suite (Net46 + NetCoreApp2.1)
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Windowsnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="Win32" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="x64" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Windowsnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="Win32" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="x64" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
echo Building tests (Net48 + NetCoreApp3.1)
call :pauseOnError msbuild /p:Configuration="Release" /t:BuildTestsNetFx /p:TargetNetFxVersion=net48
call :pauseOnError msbuild /p:Configuration="Release" /t:BuildTestsNetCore /p:TargetNetCoreVersion=netcoreapp3.1
echo Running SqlClient test suite (Net48 + NetCoreApp3.1)
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Windowsnetcoreapp" /p:TargetNetCoreVersion=netcoreapp3.1 --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="Win32" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" /p:TargetNetFxVersion=net48 --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="x64" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" /p:TargetNetFxVersion=net48 --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Windowsnetcoreapp" /p:TargetNetCoreVersion=netcoreapp3.1 --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="Win32" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" /p:TargetNetFxVersion=net48 --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
call :pauseOnError dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="x64" /p:Configuration="Release" /p:TestTargetOS="Windowsnetfx" /p:TargetNetFxVersion=net48 --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"

goto :eof

:pauseOnError
%*
if ERRORLEVEL 1 pause
goto :eof

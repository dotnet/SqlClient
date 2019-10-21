dotnet msbuild /p:Configuration="Release" /t:Clean,BuildAll /p:GenerateDocumentationFile=false
echo Building Add-Ons
dotnet msbuild /p:Configuration="Release" /t:BuildAKVNetCore /p:OSGroup=Unix /p:Platform=AnyCPU
echo Building tests
dotnet msbuild /p:Configuration="Release" /t:BuildTestsNetCore /p:OSGroup=Unix /p:Platform=AnyCPU
echo Running SqlClient test suite
dotnet test "src/Microsoft.Data.SqlClient/tests/FunctionalTests/Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Unixnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"
dotnet test "src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="Unixnetcoreapp" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"

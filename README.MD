Home of Microsoft.Data.SqlClient

# Building

## Pre-Requisites

Install [PowerShell-Core](https://github.com/PowerShell/PowerShell) on your local Machine 

Visual Studio 2017 with the following profile components. This can be saved to a text file and imported into the Visual Studio installer.

```
{
	"version": "1.0",
	"components": [
		"Microsoft.VisualStudio.Workload.ManagedDesktop",
		"Microsoft.VisualStudio.Workload.NativeDesktop",
		"Microsoft.VisualStudio.Workload.Azure",
		"Microsoft.VisualStudio.Workload.NetCoreTools",
		"microsoft.net.componentgroup.targetingpacks.common",
		"microsoft.visualstudio.component.debugger.justintime",
		"microsoft.visualstudio.component.liveunittesting",
		"microsoft.visualstudio.component.vc.tools.x86.x64",
		"microsoft.visualstudio.component.windows10sdk.17763",
		"microsoft.visualstudio.componentgroup.nativedesktop.win81",
		"microsoft.visualstudio.component.vc.140",
		"microsoft.visualstudio.component.appinsights.tools",
		"microsoft.visualstudio.component.debugger.snapshot",
		"microsoft.visualstudio.component.aspnet45",
		"microsoft.visualstudio.component.webdeploy",
		"microsoft.netcore.componentgroup.web",
		"microsoft.visualstudio.component.testtools.webloadtest",
		"microsoft.component.azure.datalake.tools",
		"microsoft.visualstudio.componentgroup.azure.resourcemanager.tools",
		"microsoft.visualstudio.componentgroup.azure.cloudservices",
		"microsoft.visualstudio.component.azure.mobileappssdk",
		"microsoft.visualstudio.component.azure.servicefabric.tools",
		"microsoft.visualstudio.component.testtools.core",
		"microsoft.visualstudio.component.typescript.2.3",
		"microsoft.visualstudio.component.testtools.microsofttestmanager",
		"microsoft.visualstudio.component.typescript.3.0"
	]
}
```


* Open Powershell-Core Shell
* Go to the root folder of the Git Repo
* Execute the following commands.
 
### Build Driver:
`WindowsNetFx` (Builds Microsoft.Data.SqlClient netfx(x86,x64), Functional and Manual Tests)(x86,x64):  
```
./build.ps1 -Configuration "Release" -TestTargetOS "WindowsNetFx" -AssemblyFileVersion "1.0.0.0"
```

`WindowsNetCore` (Builds Microsoft.Data.SqlClient netcore(AnyCPU), Functional and Manual Tests)(AnyCPU):  
```
./build.ps1 -Configuration "Release" -TestTargetOS "WindowsNetCore" -AssemblyFileVersion "1.0.0.0"
```

`UnixNetCore` (Builds Microsoft.Data.SqlClient netcore(AnyCPU), Functional and Manual Tests)(AnyCPU):  
```
./build.ps1 -Configuration "Release" -TestTargetOS "UnixNetCore" -AssemblyFileVersion "1.0.0.0"
```
 
### Run Functional Tests:  
`Windows` (netcoreapp2.1):  
```
dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="WindowsNetCore" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"
```
 
`Windows` (net46 x86):  
```
dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="Win32" /p:Configuration="Release" /p:TestTargetOS="WindowsNetFx" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"`
 ```

`Windows` (net46 x64):  
```
dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="x64" /p:Configuration="Release" /p:TestTargetOS="WindowsNetFx" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"`
```

`Unix` (netcoreapp2.1):  
```
dotnet test "src/Microsoft.Data.SqlClient/tests/FunctionalTests/Microsoft.Data.SqlClient.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="UnixNetCore" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"`
```

### Run Manual Tests:  
`Windows` (netcoreapp2.1):  
```
dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="WindowsNetCore" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonwindowstests"`
```

`Windows` (net46 x86):  
```
dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="Win32" /p:Configuration="Release" /p:TestTargetOS="WindowsNetFx" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"`
```

`Windows` (net46 x64):  
```
dotnet test "src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="x64" /p:Configuration="Release" /p:TestTargetOS="WindowsNetFx" --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests"`
```

`Unix` (netcoreapp2.1):  
```
dotnet test "src/Microsoft.Data.SqlClient/tests/ManualTests/Microsoft.Data.SqlClient.ManualTesting.Tests.csproj" /p:Platform="AnyCPU" /p:Configuration="Release" /p:TestTargetOS="UnixNetCore" --no-build -v n --filter "category!=nonnetfxtests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"`
```
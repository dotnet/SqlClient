Home of Microsoft.Data.SqlClient

#Building

##Pre-req

PowerShell-Core on your local Machine 

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
 
###Building Driver

Windows (Builds netfx(x86,x64), netcore(Any CPU), and Functional Tests)(x86,x64,Any CPU):
`./build.ps1 -Configuration "Release" -TargetOSGroup "Windows"`
 
Unix (netcore(Any CPU), and Functional Tests)(Any CPU):
`./build.ps1 -Configuration "Release" -TargetOSGroup "Unix"`
 
 
###Functional Testing:
Windows (netcoreapp2.1):
`dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" "/p:Platform=Any CPU" /p:Configuration=Release /p:TargetOSGroup=Windows -f netcoreapp2.1 --no-build  --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests" -v n`
 
Windows (net46 x86):
`dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform=x86 /p:Configuration=Release /p:TargetOSGroup=Windows -f net46 --no-build  --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests" -v n`
 
Windows (net46 x64):
`dotnet test "src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.Tests.csproj" /p:Platform=x64 /p:Configuration=Release /p:TargetOSGroup=Windows -f net46 --no-build  --filter "category!=nonnetcoreapptests&category!=failing&category!=nonwindowstests" -v n`

Unix (netcoreapp2.1):
`dotnet test "src/Microsoft.Data.SqlClient/tests/FunctionalTests/Microsoft.Data.SqlClient.Tests.csproj" "/p:Platform=Any CPU" /p:Configuration=Release -f netcoreapp2.1 --no-build -v n --filter "category!=nonnetcoreapptests&category!=failing&category!=nonlinuxtests&category!=nonuaptests"`
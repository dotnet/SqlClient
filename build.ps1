# Script: build.ps1
# Author: Keerat Singh
# Date:   14-Nov-2018
# Comments: This build script invokes the build scripts to build netfx,netcore and tests.
#

param(
    [Parameter(Mandatory=$true)]
    [string]$TestTargetOS,
    [string]$Configuration='Release',
    [string]$ProjectRoot=$PWD,
    [Parameter(Mandatory=$true)]
    [string]$AssemblyFileVersion
    )
    
    # Check if MsBuild exists in the path, if not then setup Enviornment Variables.
    Function CheckMSBuild()
    {
        
        if(![bool](Get-Command -Name "msbuild.exe" -ErrorAction SilentlyContinue))
        {
            Invoke-Expression "& `"$ProjectRoot/tools/setupEnvVariables.ps1`""
        }
    }
    Function BuildDriverAndTests()
    {
        $cmd = ""
        switch ( $TestTargetOS )
        {
            'WindowsNetFx'
            {
                CheckMSBuild
                $buildcmd     = "msbuild /p:Configuration='$Configuration'"
                $testbuildcmd = "msbuild /t:BuildTestsNetFx /p:Configuration='$Configuration'"
                break
            }
            'WindowsNetCore'
            {
                $buildcmd     = "msbuild /p:Configuration='$Configuration' /p:BuildNetFx=false"
                $testbuildcmd =  "msbuild /t:BuildTestsCore /p:Configuration='$Configuration' /p:OSGroup=Windows_NT"
                break;
            }
            'UnixNetCore'
            {
                $buildcmd     =  "dotnet msbuild /p:Configuration='$Configuration'"
                $testbuildcmd =  "dotnet msbuild /t:BuildTestsCore /p:Configuration='$Configuration' /p:OSGroup=Unix"
                break;
            }
        }
        Invoke-Expression $buildcmd
        if($LASTEXITCODE -eq 1) {
            Write-Error "Failed $buildcmd with Status: $LASTEXITCODE"
        }

        Invoke-Expression $testbuildcmd
        if($LASTEXITCODE -eq 1) {
            Write-Error "Failed $testbuildcmd with Status: $LASTEXITCODE"
        }
    }

    $ErrorActionPreference = "Stop"
    BuildDriverAndTests

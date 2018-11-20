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
        switch ( $TestTargetOS )
        {
            'WindowsNetFx'
            {
                CheckMSBuild
                Invoke-Expression "& `"$ProjectRoot/tools/buildnetfx.ps1`" -Platform 'Win32' -Configuration $Configuration -ProjectRoot $ProjectRoot -AssemblyFileVersion $AssemblyFileVersion"
                Invoke-Expression "& `"$ProjectRoot/tools/buildnetfx.ps1`" -Platform 'x64' -Configuration $Configuration -ProjectRoot $ProjectRoot -AssemblyFileVersion $AssemblyFileVersion"
                Invoke-Expression "& `"$ProjectRoot/tools/buildfunctionaltests.ps1`" -Platform 'x86' -Configuration $Configuration -ProjectRoot $ProjectRoot -TestTargetOS $TestTargetOS"
                Invoke-Expression "& `"$ProjectRoot/tools/buildfunctionaltests.ps1`" -Platform 'x64' -Configuration $Configuration -ProjectRoot $ProjectRoot -TestTargetOS $TestTargetOS"
                Invoke-Expression "& `"$ProjectRoot/tools/buildmanualtests.ps1`" -Platform 'x86' -Configuration $Configuration -ProjectRoot $ProjectRoot -TestTargetOS $TestTargetOS"
                Invoke-Expression "& `"$ProjectRoot/tools/buildmanualtests.ps1`" -Platform 'x64' -Configuration $Configuration -ProjectRoot $ProjectRoot -TestTargetOS $TestTargetOS"
                break
            }
            { 'WindowsNetCore', 'UnixNetCore'}
            {
                Invoke-Expression "& `"$ProjectRoot/tools/buildnetcore.ps1`" -Platform 'AnyCPU' -Configuration $Configuration -ProjectRoot $ProjectRoot -TestTargetOS $TestTargetOS"
                Invoke-Expression "& `"$ProjectRoot/tools/buildfunctionaltests.ps1`" -Platform 'AnyCPU' -Configuration $Configuration -ProjectRoot $ProjectRoot -TestTargetOS $TestTargetOS"
                Invoke-Expression "& `"$ProjectRoot/tools/buildmanualtests.ps1`" -Platform 'AnyCPU' -Configuration $Configuration -ProjectRoot $ProjectRoot -TestTargetOS $TestTargetOS"
                break;
            }
        }
    }

    BuildDriverAndTests

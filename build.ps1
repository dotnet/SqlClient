# Script: build.ps1
# Author: Keerat Singh
# Date:   14-Nov-2018
# Comments: This build script invokes the build scripts to build netfx,netcore and tests.
#

param(
    [Parameter(Mandatory=$true)]
    [string]$TargetOSGroup,
    [string]$Configuration='Release',
    [string]$ProjectRoot=$PWD
    )
    
    #Build NetFx and Functional Tests(x86,x64) only if targeting Windows
    if($TargetOSGroup -like "Windows")
    {
        Invoke-Expression "& `"$ProjectRoot/tools/buildnetfx.ps1`" -Platform 'Win32' -Configuration $Configuration -ProjectRoot $ProjectRoot"
        Invoke-Expression "& `"$ProjectRoot/tools/buildnetfx.ps1`" -Platform 'x64' -Configuration $Configuration -ProjectRoot $ProjectRoot"
        Invoke-Expression "& `"$ProjectRoot/tools/buildfunctionaltests.ps1`" -Platform 'x86' -Configuration $Configuration -ProjectRoot $ProjectRoot -TargetOSGroup $TargetOSGroup"
        Invoke-Expression "& `"$ProjectRoot/tools/buildfunctionaltests.ps1`" -Platform 'x64' -Configuration $Configuration -ProjectRoot $ProjectRoot -TargetOSGroup $TargetOSGroup"
    }

    Invoke-Expression "& `"$ProjectRoot/tools/buildnetcore.ps1`" -Platform 'Any CPU' -Configuration $Configuration -ProjectRoot $ProjectRoot -TargetOSGroup $TargetOSGroup"
    Invoke-Expression "& `"$ProjectRoot/tools/buildfunctionaltests.ps1`" -Platform 'Any CPU' -Configuration $Configuration -ProjectRoot $ProjectRoot -TargetOSGroup $TargetOSGroup"
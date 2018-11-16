# Script: buildnetfx.ps1
# Author: Keerat Singh
# Date:   14-Nov-2018
# Comments: This script builds the netfx and its dependencies with specified arguments.
#

param(
    [Parameter(Mandatory=$true)]
    [string]$Configuration,
    [Parameter(Mandatory=$true)]
    [string]$Platform,
    [Parameter(Mandatory=$true)]
    [string]$ProjectRoot
    )

    $buildTool = 'msbuild'
    $netfxSrcPath = "$ProjectRoot/src/Microsoft.Data.SqlClient/netfx/src"
    $projectPaths = "$netfxSrcPath/bidinit/src/bidinit.vcxproj",
                    "$netfxSrcPath/SNI/NLRegC/ascii/ascii.vcxproj",
                    "$netfxSrcPath/SNI/NLRegC/unicode/unicode.vcxproj ",
                    "$netfxSrcPath/SNI/SNI.vcxproj",
                    "$netfxSrcPath/managedwrapper/SNIManagedWrapper.vcxproj",
                    "$netfxSrcPath/System/Data/SqlClient/Microsoft.Data.SqlClient.csproj"
    $buildArguments = "/p:Platform='$Platform' /p:Configuration='$Configuration'"

    foreach ($projectPath in $projectPaths)
    {
        $buildCmd = "$buildTool $projectPath $buildArguments"
        Write-Output "*************************************** Build Command ***************************************"
        Write-Output $buildCmd
        Write-Output "******************************************************************************"
        Invoke-Expression  $buildCmd
    }
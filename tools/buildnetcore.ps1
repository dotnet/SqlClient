# Script: buildnetcore.ps1
# Author: Keerat Singh
# Date:   14-Nov-2018
# Comments: This script builds the netcore with specified arguments.
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
    $netcoreSrcPath = "$ProjectRoot/src/Microsoft.Data.SqlClient/netcore/src"
    $projectPaths = "$netcoreSrcPath/Microsoft.Data.SqlClient.csproj"
    $buildArguments = "/p:Platform='$Platform' /p:Configuration='$Configuration'"

    foreach ($projectPath in $projectPaths)
    {
        $buildCmd = "$buildTool $projectPath $buildArguments"
        Invoke-Expression  $buildCmd
    }
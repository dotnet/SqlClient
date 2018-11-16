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
    [string]$ProjectRoot,
    [Parameter(Mandatory=$true)]
    [string]$TargetOSGroup
    )

    $buildTool = 'dotnet build'
    $netcoreSrcPath = "$ProjectRoot/src/Microsoft.Data.SqlClient/netcore/src"
    $projectPaths = "$netcoreSrcPath/Microsoft.Data.SqlClient.csproj"
    $buildArguments = "/p:Platform='$Platform' /p:Configuration='$Configuration' /p:TargetOSGroup='$TargetOSGroup'"
    
    if ($TargetOSGroup -like "Unix")
    {
        $buildArguments = $buildArguments + " /p:OSGroup=Unix"
    }

    foreach ($projectPath in $projectPaths)
    {
        $buildCmd = "$buildTool $projectPath $buildArguments"
        Write-Output "*************************************** Build Command ***************************************"
        Write-Output $buildCmd
        Write-Output "******************************************************************************"
        Invoke-Expression  $buildCmd
    }
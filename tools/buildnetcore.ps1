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
    [string]$TestTargetOS
    )

    $buildTool ='dotnet msbuild'
    $netcoreSrcPath = "$ProjectRoot/src/Microsoft.Data.SqlClient/netcore/src"
    $netcoreRefPath = "$ProjectRoot/src/Microsoft.Data.SqlClient/netcore/ref"

    $projectPaths = "$netcoreSrcPath/Microsoft.Data.SqlClient.csproj",
                    "$netcoreRefPath/Microsoft.Data.SqlClient.csproj"

    Function SetBuildArguments()
    {
        $buildArguments = "/p:Platform='$Platform' /p:Configuration='$Configuration' /p:TestTargetOS='$TestTargetOS'"
        if($TestTargetOS -like "*Unix*")
        {
            $buildArguments = $buildArguments + " /p:OSGroup=Unix"
        }
        return $buildArguments
    }
    Function BuildDriver()
    {
        $buildArguments = SetBuildArguments
        foreach ($projectPath in $projectPaths)
        {
            $buildCmd = "$buildTool $projectPath $buildArguments"
            Write-Output "*************************************** Build Command ***************************************"
            Write-Output $buildCmd
            Write-Output "******************************************************************************"
            Invoke-Expression  $buildCmd
        }
    }

    BuildDriver
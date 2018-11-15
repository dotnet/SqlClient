# Script: buildfunctionaltests.ps1
# Author: Keerat Singh
# Date:   14-Nov-2018
# Comments: This script builds the functional tests and its dependencies with specified arguments.
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
    $testPath = "$ProjectRoot/src/Microsoft.Data.SqlClient/tests"
    $projectPaths = "$testPath/tools/TDS/TDS/TDS.csproj",
                    "$testPath/tools/TDS/TDS.EndPoint/TDS.EndPoint.csproj",
                    "$testPath/tools/TDS/TDS.Servers/TDS.Servers.csproj",
                    "$testPath/tools/CoreFx.Private.TestUtilities/CoreFx.Private.TestUtilities.csproj",
                    "$testPath/ManualTests/SQL/UdtTest/UDTs/Address/Address.csproj",
                    "$testPath/FunctionalTests/Microsoft.Data.SqlClient.Tests.csproj"
    $buildArguments = "/p:Platform='$Platform' /p:Configuration='$Configuration' /p:TargetOSGroup='$TargetOSGroup'"

    foreach ($projectPath in $projectPaths)
    {
        $buildCmd = "$buildTool $projectPath $buildArguments"
        Invoke-Expression  $buildCmd
    }
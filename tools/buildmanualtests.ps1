# Script: buildmanualtests.ps1
# Author: Afsaneh Rafighi, Keerat Singh
# Date:   16-Nov-2018
# Comments: This script builds the manual tests and its dependencies with specified arguments.
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

    $buildTool = 'dotnet msbuild'
    $testPath = "$ProjectRoot/src/Microsoft.Data.SqlClient/tests"
    # Restore required dependencies
    Function RestorePackages()
    {
        $buildCmd = "dotnet restore '$testPath/ManualTests/Microsoft.Data.SqlClient.ManualTesting.Tests.csproj' /p:TestTargetOS='$TestTargetOS'"
        Write-Output "*************************************** Restoring Packages ***************************************"
        Write-Output $buildCmd
        Write-Output "******************************************************************************"
        Invoke-Expression  $buildCmd
    }

    Function SetBuildArguments()
    {
        if ($TestTargetOS -like "*Windows*" -and  $Platform -like 'x86')
        {
            $Platform = 'Win32'
        }

        $buildArguments = "/p:Platform='$Platform' /p:Configuration='$Configuration' /p:TestTargetOS='$TestTargetOS' /p:BuildProjectReferences=false"
        
        if ($TestTargetOS -like "*Unix*")
        {
            $buildArguments = $buildArguments + " /p:TargetsWindows=false /p:TargetsUnix=true"
        }

        return $buildArguments

    }
    Function BuildTests()
    {
        $buildArguments = SetBuildArguments
        $projectPaths =  "$testPath/ManualTests/SQL/UdtTest/UDTs/Address/Address.csproj",
                        "$testPath/ManualTests/SQL/UdtTest/UDTs/Circle/Circle.csproj",
                        "$testPath/ManualTests/SQL/UdtTest/UDTs/Shapes/Shapes.csproj",
                        "$testPath/ManualTests/SQL/UdtTest/UDTs/Utf8String/Utf8String.csproj","$testPath/tools/CoreFx.Private.TestUtilities/CoreFx.Private.TestUtilities.csproj",
                        "$testPath/ManualTests/Microsoft.Data.SqlClient.ManualTesting.Tests.csproj"
        foreach ($projectPath in $projectPaths)
        {
            $buildCmd = "$buildTool $projectPath $buildArguments"
            Write-Output "*************************************** Build Command ***************************************"
            Write-Output $buildCmd
            Write-Output "******************************************************************************"
            Invoke-Expression  $buildCmd
        }
    }

    RestorePackages
    BuildTests
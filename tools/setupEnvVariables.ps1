# Script: setupEnvVariables.ps1
# Author: Keerat Singh
# Date:   16-Nov-2018
# Comments: This script sets up the enviornment variables using vswhere and vsdevcmd utilities.
#

Function SetupVariables()
{
    # Find the location of Visual Studio using VSWhere.exe
    $VsWherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    #Write-Output $VsWherePath
    $VSInstallationPath = &"$VsWherePath" -prerelease -latest -property installationPath
    Write-Output "VSInstallationPath=$VSInstallationPath"

    # Update the Env Variables using vsdevcmd.bat
    if ($VSInstallationPath -and (Test-Path "$VSInstallationPath\Common7\Tools\vsdevcmd.bat"))
    {
        & "${env:COMSPEC}" /s /c "`"$VSInstallationPath\Common7\Tools\vsdevcmd.bat`" -no_logo && set" | Foreach-Object {
        $name, $value = $_ -split '=', 2
        Set-Content env:\"$name" $value
        }
        Write-Output "************** Environment variables setup successfully **************"
    }
    else
    {
        Write-Output "************** Could not setup the Environment variables **************"
    }
}

Function PrintVariables()
{
    # Print the Environment variables.
    Get-ChildItem ENV:
    Write-Output "*********************************************************************"
}

SetupVariables
PrintVariables
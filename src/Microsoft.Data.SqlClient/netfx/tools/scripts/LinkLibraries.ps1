# Script: LinkLibraries.ps1
# Author: Keerat Singh
# Date:   10-Oct-2018
# Comments: This script loads the VS Dev Environment variables and executes the linker to link
# the Native Libs and SqlClient NetModule
#

param(
    [Parameter(Mandatory=$true)]
    [string]$OutputPlatform,
    [Parameter(Mandatory=$true)]
    [string]$OutputConfiguration,
    [Parameter(Mandatory=$true)]
    [string]$BinPath,
    [Parameter(Mandatory=$true)]
    [string]$ObjPath,
    [Parameter(Mandatory=$true)]
    [string]$AssemblyVersion,
    [Parameter(Mandatory=$true)]
    [string]$AssemblyName,
    [Parameter(Mandatory=$true)]
    [string]$ResourceFileName
    )

# Global variables
$global:WindowsSdkLibPath = ""
$global:NETFXSdkLibPath = ""
$global:VCToolsLibPath = ""
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

    $global:WindowsSdkLibPath = "${Env:WindowsSdkDir}lib\$Env:WindowsSDKLibVersion"
    $global:NETFXSdkLibPath = "${Env:NETFXSDKDir}lib\"
    $global:VCToolsLibPath = "${Env:VCToolsInstallDir}lib\"
    Write-Output "************** Global variables setup successfully **************"
  }
  else
  {
    Write-Output "************** Could not setup the Environment variables **************"
  }
}

Function SetupResourceCompileCommand()
{
  $RcPath = "&`'${Env:WindowsSdkVerBinPath}\${OutputPlatform}\rc.exe`'"
  $RcArguments = '/l"0x0409" /nologo /fo"${BinPath}${AssemblyName}\netfx\${AssemblyName}.res" "${ObjPath}${AssemblyName}\netfx\${AssemblyName}.rc"'
  return "$RcPath $RcArguments"
}

Function SetupLinkerCommand()
{
  $LinkerPath = "&`'${Env:VCToolsInstallDir}bin\Host${OutputPlatform}\${OutputPlatform}\link.exe`'"
  $LinkerArguments = AddLinkerArguments
  $LibraryDependencies = AddLibraryDependencies
  return "$LinkerPath $LinkerArguments $LibraryDependencies"
}

Function AddLinkerArguments()
{
  $LinkerArguments = "/OUT:`"${BinPath}${AssemblyName}\netfx\${AssemblyName}.dll`" /VERSION:`"10.0`" /INCREMENTAL:NO /NOLOGO /WX /NODEFAULTLIB /ASSEMBLYRESOURCE:`"${ObjPath}${AssemblyName}\netfx\${ResourceFileName}.resources`" /ASSEMBLYRESOURCE:`"${BinPath}${AssemblyName}\netfx\Resources\Microsoft.Data.SqlClient.SqlMetaData.xml`" /MANIFESTFILE:${AssemblyName}.dll.mt /DEBUG /PDB:${BinPath}${AssemblyName}\netfx\${AssemblyName}.pdb /SUBSYSTEM:CONSOLE,`"6.00`" /STACK:`"0x100000`",`"0x1000`" /LARGEADDRESSAWARE /OPT:REF /RELEASE /MACHINE:${OutputPlatform} /LTCG /CLRUNMANAGEDCODECHECK:NO /CLRIMAGETYPE:IJW /CLRTHREADATTRIBUTE:STA /DYNAMICBASE /guard:cf /DEBUGTYPE:`"cv,fixup`" /OSVERSION:`"6.00`" /PDBCOMPRESS /IGNORE:`"4248,4070,4221`" /DLL "

  if($OutputPlatform -ieq "x86") 
  {
    $LinkerArguments =  $LinkerArguments + " /SAFESEH /NXCOMPAT /ENTRY:`"_DllMainCRTStartup@12`""
  }
  if($OutputPlatform -ieq "x64") 
  {
    $LinkerArguments =  $LinkerArguments + " /ENTRY:`"_DllMainCRTStartup`""
  }
  if($OutputConfiguration -ieq "Debug")
  {
    $LinkerArguments =  $LinkerArguments + " /ASSEMBLYDEBUG"
  }
  if($OutputConfiguration -ieq "Release")
  {
    $LinkerArguments =  $LinkerArguments + " /OPT:ICF"
  }

  return $LinkerArguments
}

Function RenameAssembly()
{
  # Create of copy of Microsoft.Data.SqlClient.dll as Microsoft.Data.SqlClient.netmodule.
  Write-Output "************************** RENAMING ${BinPath}${AssemblyName}\netfx\${AssemblyName}.dll TO NETMODULE ***************************"
  Copy-Item -Path "${BinPath}${AssemblyName}\netfx\${AssemblyName}.dll" -Destination "${BinPath}${AssemblyName}\netfx\${AssemblyName}.netmodule"
}

Function AddLibraryDependencies()
{
  $LibSuffix = ""
  if($OutputConfiguration -ieq "Debug") { $LibSuffix = "d" }
    
  $LibraryDependencies = @(
    "${BinPath}${AssemblyName}\netfx\SniManagedWrapper.obj",
    "${BinPath}${AssemblyName}\netfx\${AssemblyName}.netmodule",
    "${BinPath}${AssemblyName}\netfx\${AssemblyName}.res",
    "${BinPath}SNI\sni.lib",
    "${BinPath}ascii\NLRegCA.lib",
    "${BinPath}bidinit\bidinit.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\advapi32.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\kernel32.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\version.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\ws2_32.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\mswsock.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\crypt32.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\Shlwapi.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\netapi32.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\user32.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\uuid.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\ole32.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\Rpcrt4.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\secur32.lib",
    "${NETFXSdkLibPath}um\${OutputPlatform}\mscoree.lib",
    "${WindowsSdkLibPath}um\${OutputPlatform}\version.lib",
    "${VCToolsLibPath}${OutputPlatform}\ptrustm${LibSuffix}.lib",
    "${VCToolsLibPath}${OutputPlatform}\msvcrt${LibSuffix}.lib",
    "${VCToolsLibPath}${OutputPlatform}\msvcmrt${LibSuffix}.lib",
    "${VCToolsLibPath}${OutputPlatform}\nothrownew.obj",
    "${VCToolsLibPath}${OutputPlatform}\vcruntime${LibSuffix}.lib",
    "${VCToolsLibPath}${OutputPlatform}\legacy_stdio_wide_specifiers.lib",
    "${WindowsSdkLibPath}ucrt\${OutputPlatform}\ucrt${LibSuffix}.lib"
    )
  return '"' + ($LibraryDependencies -join '" "') + '"'
}

Function PrintVariables()
{
  Write-Output "************************** Script variables ***************************"
  # Print the Script variables.
  Write-Output "OutputPlatform : $OutputPlatform"
  Write-Output "OutputConfiguration : $OutputConfiguration"
  Write-Output "BinPath : $BinPath"
  Write-Output "ObjPath : $ObjPath"
  Write-Output "AssemblyVersion : $AssemblyVersion"
  Write-Output "AssemblyName : $AssemblyName"
  Write-Output "ResourceFileName : $ResourceFileName"
  Write-Output "WindowsSdkLibPath : $WindowsSdkLibPath"
  Write-Output "NETFXSdkLibPath : $NETFXSdkLibPath"
  Write-Output "VCToolsLibPath : $VCToolsLibPath"
  Write-Output "************************** Environment variables ***************************"
  # Print the Environment variables.
  Get-ChildItem ENV:
  Write-Output "*********************************************************************"
}

SetupVariables
PrintVariables
RenameAssembly

$ResourceCompileCommand = SetupResourceCompileCommand

Write-Output "************************** RESOURCE COMPILE COMMAND ***************************"
Write-Output $ResourceCompileCommand
Write-Output "*********************************************************************"

Write-Output "************** Resource Compile Command **************"
Invoke-Expression $ResourceCompileCommand

$LinkerCommand = SetupLinkerCommand

# Print the Linker Command during Debug Mode
Write-Output "************************** LINKER COMMAND ***************************"
Write-Output $LinkerCommand
Write-Output "*********************************************************************"

#Excecute the Linker Command and Link the libraries
Write-Output "************** Executing Linker Command **************"
Invoke-Expression $LinkerCommand
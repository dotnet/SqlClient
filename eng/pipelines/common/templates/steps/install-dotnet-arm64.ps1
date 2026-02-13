<#
.SYNOPSIS
  This script installs dotnet SDKs and Runtimes for ARM64.

.DESCRIPTION
  Special handling is required for ARM64 due to a bug in UseDotNet@2:

  [BUG]: UseDotNet@2 task installs x86 build
  https://github.com/microsoft/azure-pipelines-tasks/issues/20300

  The downloaded dotnet-install.ps1 script is kept in the $InstallDir to avoid
  downloading it multiple times during the pipeline job.

  The following environment variables are set for subsequent steps in the pipeline:

    DOTNET_ROOT: Set to $InstallDir.
    PATH: $DOTNET_ROOT is prepended to the PATH environment variable.

.PARAMETER Debug
  True to emit debug messages.  Default is false.

.PARAMETER DryRun
  True to perform a dry run of the installation.  Default is false.

.PARAMETER GlobalJson
  The path to the global.json file that specifies the exact SDK version to
  install.  Default is 'global.json'.

.PARAMETER InstallDir
  The directory to install the SDKs and Runtimes into, typically the
  pipeline's $(Agent.ToolsDirectory)/dotnet directory.

  The dotnet-install.ps1 script is downloaded into this directory if it is not
  already present.

  Default is '.'.

.PARAMETER Runtimes
  The versions of the .NET Runtimes to install.  These must be in the runtime
  channel format of X.Y expected by the dotnet-install.ps1 script.  Default is
  an empty array.

.NOTES
  Licensed to the .NET Foundation under one or more agreements.
  The .NET Foundation licenses this file to you under the MIT license.
  See the LICENSE file in the project root for more information.
#>

param
(
  [switch]$Debug,
  [switch]$DryRun,
  [string]$GlobalJson = "global.json",
  [string]$InstallDir = '.',
  [string[]]$Runtimes = @()
)

# Stop on all errors.
$ErrorActionPreference = 'Stop'

#------------------------------------------------------------------------------
# Emit our command-line arguments.

if ($Debug)
{
  Write-Host "Command-line arguments:"
  Write-Host ($PSBoundParameters | ConvertTo-Json -Depth 1)
}

#------------------------------------------------------------------------------
# Download the dotnet-install.ps1 script if not already present.

if (-not (Test-Path -Path "$InstallDir/dotnet-install.ps1" -PathType Leaf))
{
  if (-not (Test-Path -PathType Container -Path "$InstallDir"))
  {
    Write-Host "Creating install dir: $InstallDir ..."

    New-Item -ItemType Directory -Force -Path "$InstallDir"
  }

  Write-Host "Downloading dotnet-install.ps1..."

  $params =
  @{
    Uri = "https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.ps1"
    OutFile = "$InstallDir/dotnet-install.ps1"
  }

  Invoke-WebRequest @params -Verbose:$Debug

  if ($Debug)
  {
    Write-Host "Emitting dotnet-install.ps1 help:"
    Get-Help "$InstallDir/dotnet-install.ps1"
  }
}

#------------------------------------------------------------------------------
# Read the SDK versions from global.json.

$globalJsonContent = Get-Content -Raw -Path "$GlobalJson" | ConvertFrom-Json
$sdkVersion = $globalJsonContent.sdk.version

if ($Debug)
{
  Write-Host "global.json content:"
  Write-Host ($globalJsonContent | ConvertTo-Json -Depth 1)

  Write-Host "SDK version: $sdkVersion"
}

#------------------------------------------------------------------------------
# Install the SDK.

Write-Host "Installing .NET SDK version: $sdkVersion"

$installParams =
@{
  Architecture = "arm64"
  Version =      "$sdkVersion"
  InstallDir =   "$InstallDir"
}

if ($Debug)
{
  Write-Host "dotnet-install.ps1 parameters:"
  Write-Host ($installParams | ConvertTo-Json -Depth 1)
}

& "$InstallDir/dotnet-install.ps1" -Verbose:$Debug -DryRun:$DryRun @installParams

#------------------------------------------------------------------------------
# Install the Runtimes, if any.

foreach ($channel in $Runtimes)
{
  Write-Host "Installing .NET Runtime GA channel: $channel"

  $installParams =
  @{
    Architecture = "arm64"
    Channel =      "$channel"
    InstallDir =   "$InstallDir"
    Quality =      "GA"
    Runtime =      "dotnet"
  }

  if ($Debug)
  {
    Write-Host "dotnet-install.ps1 parameters:"
    Write-Host ($installParams | ConvertTo-Json -Depth 1)
  }

  & "$InstallDir/dotnet-install.ps1" -Verbose:$Debug -DryRun:$DryRun @installParams
}

#------------------------------------------------------------------------------
# Set the DOTNET_ROOT environment variable, and add the tools dir to the path.
# These values propagate back out to the pipeline and are used by subsequent
# steps.

Write-Host "##vso[task.setvariable variable=DOTNET_ROOT]$InstallDir"
Write-Host "##vso[task.prependpath]$InstallDir"

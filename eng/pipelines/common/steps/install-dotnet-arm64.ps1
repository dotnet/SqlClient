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

# Maximum number of retry attempts for transient install failures (e.g.
# corrupt downloads, network timeouts).
$maxAttempts = 3
$retryDelaySec = 10

#------------------------------------------------------------------------------
# Invoke dotnet-install.ps1 with retry logic.  On each attempt the script is
# called with the supplied $Params.  If a non-zero exit code is returned, or
# if the optional $Verify script-block throws, the attempt is considered failed
# and will be retried after a short delay.

function Invoke-DotNetInstall
{
  param
  (
    [Parameter(Mandatory)]
    [string]$Description,

    [Parameter(Mandatory)]
    [hashtable]$Params,

    [scriptblock]$Verify = $null
  )

  for ($attempt = 1; $attempt -le $maxAttempts; $attempt++)
  {
    try
    {
      Write-Host "$Description (attempt $attempt of $maxAttempts)"

      $global:LASTEXITCODE = 0
      & "$InstallDir/dotnet-install.ps1" -Verbose:$Debug -DryRun:$DryRun @Params
      $installSucceeded = $?

      if (-not $installSucceeded)
      {
        throw "dotnet-install.ps1 failed."
      }

      if ($global:LASTEXITCODE -ne 0)
      {
        throw "dotnet-install.ps1 failed with exit code $global:LASTEXITCODE"
      }

      if ($Verify)
      {
        & $Verify
      }

      return
    }
    catch
    {
      Write-Warning "Attempt $attempt failed: $_"

      if ($attempt -ge $maxAttempts)
      {
        throw "$Description failed after $maxAttempts attempts. Last error: $_"
      }

      Write-Host "Retrying in $retryDelaySec seconds..."
      Start-Sleep -Seconds $retryDelaySec
    }
  }
}

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

# Verify the SDK was actually installed.  dotnet-install.ps1 can silently fail
# with exit code 0 when the package download is corrupt or the size cannot be
# measured.
$verifySdk =
  if (-not $DryRun)
  {
    {
      $dotnetExe = Join-Path $InstallDir "dotnet"
      $installedSdks = & $dotnetExe --list-sdks 2>&1
      $installedSdksText = [string]::Join("`n", @($installedSdks))

      Write-Host "Installed SDKs:`n$installedSdksText"

      $sdkPattern = "(?m)^$([regex]::Escape($sdkVersion))\s+\["

      if (-not [regex]::IsMatch($installedSdksText, $sdkPattern))
      {
        throw "SDK $sdkVersion is not present after installation."
      }

      Write-Host "Verified SDK $sdkVersion is installed."
    }
  }

Invoke-DotNetInstall `
  -Description "Installing .NET SDK version: $sdkVersion" `
  -Params $installParams `
  -Verify $verifySdk

#------------------------------------------------------------------------------
# Install the Runtimes, if any.

foreach ($channel in $Runtimes)
{
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

  # Verify the runtime was actually installed.  Use the same guard against
  # silent corruption that we use for the SDK.
  $verifyRuntime =
    if (-not $DryRun)
    {
      # Capture $channel in a local variable so the script-block closure
      # binds to the current iteration value.
      $ch = $channel
      {
        $dotnetExe = Join-Path $InstallDir "dotnet"
        $installedRuntimes = & $dotnetExe --list-runtimes 2>&1
        $installedRuntimesText = [string]::Join("`n", @($installedRuntimes))

        Write-Host "Installed runtimes:`n$installedRuntimesText"

        $runtimePattern = "Microsoft\.NETCore\.App $([regex]::Escape($ch))\."

        if (-not [regex]::IsMatch($installedRuntimesText, $runtimePattern))
        {
          throw "Runtime $ch is not present after installation."
        }

        Write-Host "Verified runtime $ch is installed."
      }
    }

  Invoke-DotNetInstall `
    -Description "Installing .NET Runtime GA channel: $channel" `
    -Params $installParams `
    -Verify $verifyRuntime
}

#------------------------------------------------------------------------------
# Set the DOTNET_ROOT environment variable, and add the tools dir to the path.
# These values propagate back out to the pipeline and are used by subsequent
# steps.

Write-Host "##vso[task.setvariable variable=DOTNET_ROOT]$InstallDir"
Write-Host "##vso[task.prependpath]$InstallDir"

<#
.SYNOPSIS
    Stages SqlClient driver packages and exposes their exact versions to Azure Pipelines.

.DESCRIPTION
    Copies the .nupkg and .snupkg files downloaded from an upstream SqlClient package artifact into
    a local NuGet feed. It resolves package versions from the .nupkg filenames, verifies that all
    required SqlClient-family packages share one version, and emits Azure Pipelines logging commands
    that create these job-scoped variables for downstream tasks:

      sqlClientPackageVersion  SqlClient family
      sqlServerPackageVersion  Microsoft.SqlServer.Server

    Every required .nupkg must be present in ArtifactDirectory. Symbol packages are optional. The
    script stops on missing required packages, copy failures, and ambiguous filesystem errors.

.PARAMETER FeedPath
    Directory used as the local NuGet feed. The script creates it when it does not exist and
    overwrites packages with matching filenames.

.PARAMETER ArtifactDirectory
    Directory containing the .nupkg files downloaded from the upstream pipeline artifact. Optional
    .snupkg files in this directory are copied when present.

.EXAMPLE
    ./download-driver-packages.ps1 `
        -FeedPath 'C:\agent\_work\1\s\packages' `
        -ArtifactDirectory 'C:\agent\_work\1\sqlclient-ci-package\SqlClient-Driver-Packages'

    Stages all driver packages and resolves every version from its package filename.

.OUTPUTS
    None. Results are emitted as Azure Pipelines task.setvariable logging commands.

.NOTES
    This script is designed for PowerShell Core in Azure Pipelines. Package filenames must use the
    conventional <package-id>.<version>.nupkg format, and versions must begin with a digit.
#>

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$FeedPath,

    [Parameter(Mandatory)]
    [string]$ArtifactDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path $FeedPath | Out-Null

Copy-Item "$ArtifactDirectory/*.nupkg" $FeedPath -Force
Copy-Item "$ArtifactDirectory/*.snupkg" $FeedPath -Force -ErrorAction SilentlyContinue

function Resolve-PackageVersion {
    <#
    .SYNOPSIS
        Resolves a package version from a matching .nupkg filename.
    #>
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$PackageName
    )

    $packages = @(Get-ChildItem "$Path/*.nupkg" | Where-Object { $_.Name -match $Pattern })

    if ($packages.Count -ne 1) {
        throw "Expected exactly one $PackageName package in $Path, found $($packages.Count)"
    }

    return [regex]::Match($packages[0].Name, $Pattern).Groups[1].Value
}

# Patterns are anchored so Microsoft.Data.SqlClient does not also match family packages whose IDs
# begin with Microsoft.Data.SqlClient.
$sqlClientFamilyPackages = @(
    @{
        Name = 'Microsoft.Data.SqlClient'
        Pattern = '^Microsoft\.Data\.SqlClient\.(\d[^\/]*)\.nupkg$'
    },
    @{
        Name = 'Microsoft.Data.SqlClient.Extensions.Abstractions'
        Pattern = '^Microsoft\.Data\.SqlClient\.Extensions\.Abstractions\.(\d[^\/]*)\.nupkg$'
    },
    @{
        Name = 'Microsoft.Data.SqlClient.Internal.Logging'
        Pattern = '^Microsoft\.Data\.SqlClient\.Internal\.Logging\.(\d[^\/]*)\.nupkg$'
    },
    @{
        Name = 'Microsoft.Data.SqlClient.Extensions.Azure'
        Pattern = '^Microsoft\.Data\.SqlClient\.Extensions\.Azure\.(\d[^\/]*)\.nupkg$'
    },
    @{
        Name = 'Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider'
        Pattern = '^Microsoft\.Data\.SqlClient\.AlwaysEncrypted\.AzureKeyVaultProvider\.(\d[^\/]*)\.nupkg$'
    }
)

$sqlClientPackageVersion = Resolve-PackageVersion `
    $ArtifactDirectory `
    $sqlClientFamilyPackages[0].Pattern `
    $sqlClientFamilyPackages[0].Name

foreach ($package in $sqlClientFamilyPackages) {
    $version = Resolve-PackageVersion $ArtifactDirectory $package.Pattern $package.Name

    if ($version -ne $sqlClientPackageVersion) {
        throw "$($package.Name) version $version does not match SqlClient family version $sqlClientPackageVersion"
    }

    Write-Host "Validated $($package.Name) version: $version"
}

$sqlServerPackageVersion = Resolve-PackageVersion `
    $ArtifactDirectory `
    '^Microsoft\.SqlServer\.Server\.(\d[^\/]*)\.nupkg$' `
    'Microsoft.SqlServer.Server'

Write-Host "Resolved Microsoft.SqlServer.Server version: $sqlServerPackageVersion"
Write-Host "##vso[task.setvariable variable=sqlClientPackageVersion]$sqlClientPackageVersion"
Write-Host "##vso[task.setvariable variable=sqlServerPackageVersion]$sqlServerPackageVersion"

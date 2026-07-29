<#
.SYNOPSIS
    Stages SqlClient driver packages and exposes their exact versions to Azure Pipelines.

.DESCRIPTION
    Copies the .nupkg and .snupkg files downloaded from an upstream SqlClient package artifact into
    a local NuGet feed. It then resolves package versions from the .nupkg filenames and emits Azure
    Pipelines logging commands that create these job-scoped variables for downstream tasks:

      sqlClientPackageVersion     Microsoft.Data.SqlClient
      sqlServerPackageVersion     Microsoft.SqlServer.Server
      abstractionsPackageVersion  Microsoft.Data.SqlClient.Extensions.Abstractions
      loggingPackageVersion       Microsoft.Data.SqlClient.Internal.Logging
      azurePackageVersion         Microsoft.Data.SqlClient.Extensions.Azure

    Every required .nupkg must be present in ArtifactDirectory. Symbol packages are optional. The
    script stops on missing required packages, copy failures, and ambiguous filesystem errors.

.PARAMETER FeedPath
    Directory used as the local NuGet feed. The script creates it when it does not exist and
    overwrites packages with matching filenames.

.PARAMETER ArtifactDirectory
    Directory containing the .nupkg files downloaded from the upstream pipeline artifact. Optional
    .snupkg files in this directory are copied when present.

.PARAMETER SqlServerVersionOverride
    Optional version to expose as sqlServerPackageVersion instead of resolving the version from the
    Microsoft.SqlServer.Server .nupkg filename. The package itself is still required and staged.

.EXAMPLE
    ./download-driver-packages.ps1 `
        -FeedPath 'C:\agent\_work\1\s\packages' `
        -ArtifactDirectory 'C:\agent\_work\1\sqlclient-ci-package\SqlClient-Driver-Packages'

    Stages all driver packages and resolves every version from its package filename.

.EXAMPLE
    ./download-driver-packages.ps1 `
        -FeedPath '/agent/_work/1/s/packages' `
        -ArtifactDirectory '/agent/_work/1/sqlclient-ci-package/SqlClient-Driver-Packages' `
        -SqlServerVersionOverride '1.0.0'

    Stages all packages but exposes 1.0.0 as sqlServerPackageVersion.

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
    [string]$ArtifactDirectory,

    [string]$SqlServerVersionOverride = ''
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

    $package = Get-ChildItem "$Path/*.nupkg" |
        Where-Object { $_.Name -match $Pattern } |
        Select-Object -First 1

    if (-not $package) {
        throw "$PackageName package not found in $Path"
    }

    return [regex]::Match($package.Name, $Pattern).Groups[1].Value
}

# Patterns are anchored so the Microsoft.Data.SqlClient package does not also match extension
# packages whose IDs begin with Microsoft.Data.SqlClient.
$packages = @(
    @{
        Variable = 'sqlClientPackageVersion'
        Name = 'Microsoft.Data.SqlClient'
        Pattern = '^Microsoft\.Data\.SqlClient\.(\d[^\/]*)\.nupkg$'
    },
    @{
        Variable = 'sqlServerPackageVersion'
        Name = 'Microsoft.SqlServer.Server'
        Pattern = '^Microsoft\.SqlServer\.Server\.(\d[^\/]*)\.nupkg$'
    },
    @{
        Variable = 'abstractionsPackageVersion'
        Name = 'Microsoft.Data.SqlClient.Extensions.Abstractions'
        Pattern = '^Microsoft\.Data\.SqlClient\.Extensions\.Abstractions\.(\d[^\/]*)\.nupkg$'
    },
    @{
        Variable = 'loggingPackageVersion'
        Name = 'Microsoft.Data.SqlClient.Internal.Logging'
        Pattern = '^Microsoft\.Data\.SqlClient\.Internal\.Logging\.(\d[^\/]*)\.nupkg$'
    },
    @{
        Variable = 'azurePackageVersion'
        Name = 'Microsoft.Data.SqlClient.Extensions.Azure'
        Pattern = '^Microsoft\.Data\.SqlClient\.Extensions\.Azure\.(\d[^\/]*)\.nupkg$'
    }
)

foreach ($package in $packages) {
    if ($package.Variable -eq 'sqlServerPackageVersion' -and
        -not [string]::IsNullOrWhiteSpace($SqlServerVersionOverride)) {
        $version = $SqlServerVersionOverride
        Write-Host "Overriding $($package.Name) version: $version"
    } else {
        $version = Resolve-PackageVersion $FeedPath $package.Pattern $package.Name
        Write-Host "Resolved $($package.Name) version: $version"
    }

    Write-Host "##vso[task.setvariable variable=$($package.Variable)]$version"
}

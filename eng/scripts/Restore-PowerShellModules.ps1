# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Restores the exact module versions listed in powershell-modules.json.
.DESCRIPTION
Reuses installed versions and installs missing versions for the current user from
PowerShell.config. Registration and installation failures stop without switching sources.
#>
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $ManifestPath = (Join-Path $PSScriptRoot '..\..\powershell-modules.json')
)

$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath $ManifestPath -Raw -ErrorAction Stop | ConvertFrom-Json
if ($manifest.version -ne 1 -or $manifest.modules -isnot [pscustomobject]) {
    throw "Expected a version 1 module manifest in '$ManifestPath'."
}
$entries = @($manifest.modules.PSObject.Properties)
if ($entries.Count -eq 0) {
    throw "No PowerShell modules are defined in '$ManifestPath'."
}

$modules = @(
    foreach ($entry in $entries) {
        [version] $version = $null
        if ($entry.Name -notmatch '\A[A-Za-z0-9][A-Za-z0-9_.-]*\z' -or
            $entry.Value -isnot [pscustomobject] -or
            $entry.Value.version -isnot [string] -or
            -not [version]::TryParse($entry.Value.version, [ref] $version)) {
            throw "Module '$($entry.Name)' must have a valid name and exact version in '$ManifestPath'."
        }
        $allowClobber = $entry.Value.PSObject.Properties['allowClobber']
        if ($allowClobber -and $allowClobber.Value -isnot [bool]) {
            throw "Module '$($entry.Name)' must have a boolean allowClobber in '$ManifestPath'."
        }
        $skipPublisherCheck = $entry.Value.PSObject.Properties['skipPublisherCheck']
        if ($skipPublisherCheck -and $skipPublisherCheck.Value -isnot [bool]) {
            throw "Module '$($entry.Name)' must have a boolean skipPublisherCheck in '$ManifestPath'."
        }
        if ($skipPublisherCheck -and $skipPublisherCheck.Value -and $entry.Name -ne 'Pester') {
            throw "Only Pester may enable skipPublisherCheck in '$ManifestPath'."
        }
        [pscustomobject]@{
            Name = $entry.Name
            Version = $version
            AllowClobber = $allowClobber -and $allowClobber.Value
            SkipPublisherCheck = $skipPublisherCheck -and $skipPublisherCheck.Value
        }
    }
)

$repositoryName = $null
foreach ($module in $modules) {
    $specification = @{ ModuleName = $module.Name; RequiredVersion = $module.Version }
    if (Get-Module -ListAvailable -FullyQualifiedName $specification) {
        Write-Host "$($module.Name) $($module.Version) is already installed."
        continue
    }
    if (-not $repositoryName) {
        $repositoryName = & (Join-Path $PSScriptRoot 'Register-SqlClientPSRepository.ps1') `
            -RepositoryName SqlClient
    }
    Write-Host "Restoring $($module.Name) $($module.Version) from $repositoryName."
    Install-Module -Name $module.Name -RequiredVersion $module.Version -Repository $repositoryName `
        -Scope CurrentUser -Force -AllowClobber:$module.AllowClobber `
        -SkipPublisherCheck:$module.SkipPublisherCheck -ErrorAction Stop
}

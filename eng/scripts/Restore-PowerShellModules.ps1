# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Restores the exact resource versions listed in powershell-modules.json using PSResourceGet.
.DESCRIPTION
Reuses installed versions and installs missing versions for the current user from
PowerShell.config. Registration and installation failures stop without switching sources.
Transient download failures use up to three total attempts with bounded backoff.
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

$resources = @(
    foreach ($entry in $entries) {
        [version] $version = $null
        if ($entry.Name -notmatch '\A[A-Za-z0-9][A-Za-z0-9_.-]*\z' -or
            $entry.Value -isnot [pscustomobject] -or
            @($entry.Value.PSObject.Properties).Count -ne 1 -or
            $entry.Value.version -isnot [string] -or
            -not [version]::TryParse($entry.Value.version, [ref] $version)) {
            throw "Module '$($entry.Name)' must have a valid name and only an exact version in '$ManifestPath'."
        }
        [pscustomobject]@{
            Name = $entry.Name
            Version = $version
        }
    }
)

$repositoryName = $null
foreach ($resource in $resources) {
    $requiredVersion = "[$($resource.Version)]"
    try {
        $installed = Get-InstalledPSResource -Name $resource.Name -Version $requiredVersion `
            -ErrorAction Stop
    }
    catch [Microsoft.PowerShell.PSResourceGet.UtilClasses.ResourceNotFoundException] {
        $installed = $null
    }
    if ($installed) {
        Write-Host "$($resource.Name) $($resource.Version) is already installed."
        continue
    }
    if (-not $repositoryName) {
        $repositoryName = & (Join-Path $PSScriptRoot 'Register-SqlClientPowerShellRepository.ps1') `
            -RepositoryName SqlClient
    }
    Write-Host "Restoring $($resource.Name) $($resource.Version) from $repositoryName."
    $maxAttempts = 3
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            Install-PSResource -Name $resource.Name -Version $requiredVersion `
                -Repository $repositoryName -Scope CurrentUser -NoClobber -ErrorAction Stop
            break
        }
        catch {
            # PSResourceGet can wrap transport exceptions in PSInvalidOperationException.
            $isTransient = $false
            for ($exception = $_.Exception; $null -ne $exception;
                $exception = $exception.InnerException) {
                if ($exception -is [TimeoutException] -or
                    $exception -is [System.Threading.Tasks.TaskCanceledException]) {
                    $isTransient = $true
                    break
                }
                if ($exception -is [System.Net.Http.HttpRequestException]) {
                    $statusCode = $exception.StatusCode
                    $isTransient = $null -eq $statusCode -or
                        [int] $statusCode -in 408, 429 -or
                        ([int] $statusCode -ge 500 -and [int] $statusCode -le 599)
                    break
                }
            }
            if (-not $isTransient -or $attempt -eq $maxAttempts) {
                throw
            }
            $delaySeconds = 5 * $attempt
            Write-Warning ("Restore of $($resource.Name) $($resource.Version) from " +
                "$repositoryName failed on attempt $attempt/${maxAttempts}: " +
                "$($_.Exception.Message) Retrying in $delaySeconds seconds.")
            Start-Sleep -Seconds $delaySeconds
        }
    }
}

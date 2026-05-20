<#
.SYNOPSIS
    Resolves package versions from the local feed and sets ADO pipeline output variables.

.DESCRIPTION
    Parses .nupkg filenames in the local feed directory to determine exact package versions,
    then emits Azure DevOps logging commands to set the versions as output variables on the
    current job step.

    The output variables are consumed by downstream compile and test stages via:
      stageDependencies.setup_stage.setup_artifacts.outputs['resolve_versions.<variable>']

    Output variables set:
      abstractionsPackageVersion  - Microsoft.Data.SqlClient.Extensions.Abstractions
      loggingPackageVersion       - Microsoft.Data.SqlClient.Internal.Logging
      sqlClientPackageVersion     - Microsoft.Data.SqlClient
      sqlServerPackageVersion     - Microsoft.SqlServer.Server

.PARAMETER LocalFeedPath
    Path to the local NuGet feed directory containing .nupkg files.

.EXAMPLE
    ./resolve-package-versions.ps1 -LocalFeedPath '$(localFeedPath)'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$LocalFeedPath
)

$ErrorActionPreference = 'Stop'

function Get-PackageVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackagesPath,

        [Parameter(Mandatory = $true)]
        [string]$PackageId
    )

    $pattern = "^$([regex]::Escape($PackageId))\.(?<version>\d.+)\.nupkg$"

    $match = Get-ChildItem -Path $PackagesPath -Filter "$PackageId.*.nupkg" -File |
        Where-Object { $_.Name -notlike '*.symbols.nupkg' } |
        Where-Object { $_.Name -match $pattern } |
        Sort-Object Name -Descending |
        Select-Object -First 1

    if ($null -eq $match) {
        throw "Package '$PackageId' was not found in '$PackagesPath'."
    }

    if ($match.Name -notmatch $pattern) {
        throw "Could not parse version from package file '$($match.Name)'."
    }

    return $Matches.version
}

$versions = [pscustomobject]@{
    Abstractions = Get-PackageVersion -PackagesPath $LocalFeedPath -PackageId 'Microsoft.Data.SqlClient.Extensions.Abstractions'
    Logging      = Get-PackageVersion -PackagesPath $LocalFeedPath -PackageId 'Microsoft.Data.SqlClient.Internal.Logging'
    Mds          = Get-PackageVersion -PackagesPath $LocalFeedPath -PackageId 'Microsoft.Data.SqlClient'
    SqlServer    = Get-PackageVersion -PackagesPath $LocalFeedPath -PackageId 'Microsoft.SqlServer.Server'
}

# Emit ADO output variables (isOutput=true makes them available to downstream stages).
Write-Host "##vso[task.setvariable variable=abstractionsPackageVersion;isOutput=true]$($versions.Abstractions)"
Write-Host "##vso[task.setvariable variable=loggingPackageVersion;isOutput=true]$($versions.Logging)"
Write-Host "##vso[task.setvariable variable=sqlClientPackageVersion;isOutput=true]$($versions.Mds)"
Write-Host "##vso[task.setvariable variable=sqlServerPackageVersion;isOutput=true]$($versions.SqlServer)"

Write-Host "Resolved package versions:"
Write-Host "  Microsoft.Data.SqlClient.Extensions.Abstractions: $($versions.Abstractions)"
Write-Host "  Microsoft.Data.SqlClient.Internal.Logging:        $($versions.Logging)"
Write-Host "  Microsoft.Data.SqlClient:                         $($versions.Mds)"
Write-Host "  Microsoft.SqlServer.Server:                       $($versions.SqlServer)"

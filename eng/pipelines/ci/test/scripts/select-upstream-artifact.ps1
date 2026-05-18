<#
.SYNOPSIS
    Selects the upstream NuGet package artifact and copies it to the local feed directory.

.DESCRIPTION
    After the ADO 'download' steps fetch artifacts from one or both pipeline resources
    (sqlclientPackagePublic and/or sqlclientPackageAdo), this script determines which
    downloaded artifact directory to use and copies its contents into the local NuGet
    feed path for downstream restore operations.

    Selection logic:
      - If only one resource produced a download, that one is used.
      - If both are present, the triggering alias is preferred.
      - If neither is present, the script throws an error (manual run without a
        resource version selected).

.PARAMETER PipelineWorkspace
    The $(Pipeline.Workspace) directory where ADO places downloaded artifacts.
    Each resource alias gets a subdirectory under this path.

.PARAMETER PackageArtifactName
    The artifact name within each pipeline resource (e.g. 'Packages').
    The expected download path is: <PipelineWorkspace>/<alias>/<PackageArtifactName>

.PARAMETER LocalFeedPath
    The destination directory where .nupkg files are copied to form the local NuGet feed.
    Created if it does not exist.

.PARAMETER TriggeringAlias
    The value of $(Resources.TriggeringAlias) — the pipeline resource alias that
    triggered this run. Used to disambiguate when both resources have artifacts.
    May be empty for manually queued runs.

.EXAMPLE
    ./select-upstream-artifact.ps1 `
        -PipelineWorkspace '$(Pipeline.Workspace)' `
        -PackageArtifactName '$(packageArtifactName)' `
        -LocalFeedPath '$(localFeedPath)' `
        -TriggeringAlias '$(Resources.TriggeringAlias)'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PipelineWorkspace,

    [Parameter(Mandatory = $true)]
    [string]$PackageArtifactName,

    [Parameter(Mandatory = $true)]
    [string]$LocalFeedPath,

    [Parameter(Mandatory = $false)]
    [string]$TriggeringAlias = ''
)

$ErrorActionPreference = 'Stop'

# Ensure the local feed directory exists.
New-Item -ItemType Directory -Path $LocalFeedPath -Force | Out-Null

# The download: shorthand places artifacts at
# $(Pipeline.Workspace)/<resource-alias>/<artifact-name>.
$publicPath = Join-Path $PipelineWorkspace "sqlclientPackagePublic/$PackageArtifactName"
$adoPath    = Join-Path $PipelineWorkspace "sqlclientPackageAdo/$PackageArtifactName"
$selectedPath = $null

if (Test-Path $publicPath) {
    $selectedPath = $publicPath
}

if (Test-Path $adoPath) {
    # If both are present, prefer the triggering alias when available.
    if ($TriggeringAlias -eq 'sqlclientPackageAdo' -or $null -eq $selectedPath) {
        $selectedPath = $adoPath
    }
}

if ($null -eq $selectedPath) {
    throw 'No upstream package artifacts were found. For manual runs, select a pipeline resource version in the Run Pipeline UI.'
}

Write-Host "Using upstream package artifact path: $selectedPath"
Copy-Item -Path "$selectedPath/*" -Destination $LocalFeedPath -Recurse -Force

$packages = Get-ChildItem -Path $LocalFeedPath -Filter '*.nupkg' -File | Sort-Object Name
if ($packages.Count -eq 0) {
    throw 'No .nupkg files were copied to the local package feed.'
}

Write-Host "Copied $($packages.Count) package file(s):"
$packages | ForEach-Object { Write-Host "  $($_.Name)" }

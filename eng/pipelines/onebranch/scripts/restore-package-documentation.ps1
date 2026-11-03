#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################

<#
.SYNOPSIS
    Restores a published package and collects the XML documentation it ships.

.DESCRIPTION
    A NuGet package carries only its own XML documentation. A cref from one package into a sibling
    package therefore resolves against nothing when that sibling was not built in the same run,
    which is the state the pipeline is in whenever it depends on a published package instead of
    building it.

    This collects the documentation of that published dependency so the packaged documentation gate
    can resolve against it, using the very version the packages under validation depend on.

    The restore is deliberately driven through the repository's NuGet.config so the package comes
    from the same governed feed every other restore in the build uses, rather than from an
    arbitrary source.

.PARAMETER PackageId
    Package to restore, for example Microsoft.SqlServer.Server.

.PARAMETER Version
    Exact version to restore. A floating or range notation is rejected: the point of this step is
    to resolve against one known version, and a range would leave the version that answered a
    reference undetermined.

.PARAMETER DestinationPath
    Directory the documentation is collected into, one subdirectory per target framework. Replaced
    if it already exists, so a rerun cannot mix versions.

.PARAMETER ConfigFile
    NuGet.config governing the restore. Relative sources inside it resolve against its own
    directory, so it may live outside the working directory.

.PARAMETER TargetFramework
    Target framework of the throwaway project used to drive the restore. Only affects which
    dependency graph NuGet walks; documentation is collected from every framework the package
    ships, not just this one.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageId,

    [Parameter(Mandatory)][string]$Version,

    [Parameter(Mandatory)][string]$DestinationPath,

    [string]$ConfigFile,

    [string]$TargetFramework = 'netstandard2.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Version -match '[\*\[\]\(\),]') {
    throw "Version '$Version' is not an exact version. Supply the single version to resolve against."
}

if (-not [string]::IsNullOrWhiteSpace($ConfigFile) -and -not (Test-Path -LiteralPath $ConfigFile -PathType Leaf)) {
    throw "NuGet configuration file '$ConfigFile' was not found."
}

# Built beneath the destination rather than in TEMP so that everything this step produces is
# removed together, and a rerun cannot read a package folder left behind by an earlier version.
if (Test-Path -LiteralPath $DestinationPath) {
    Remove-Item -LiteralPath $DestinationPath -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $DestinationPath | Out-Null

$workingPath = Join-Path $DestinationPath '.restore'
New-Item -ItemType Directory -Force -Path $workingPath | Out-Null

$packagesPath = Join-Path $workingPath 'packages'
$projectPath = Join-Path $workingPath 'DependencyDocumentation.csproj'

# ManagePackageVersionsCentrally is disabled explicitly because this project carries its own
# version: were it to pick up a Directory.Packages.props, a version here would be an error.
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFramework</TargetFramework>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$PackageId" Version="[$Version]" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $projectPath -Encoding utf8

$arguments = @('restore', $projectPath, '--packages', $packagesPath)
if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) {
    $arguments += @('--configfile', $ConfigFile)
}

Write-Host "Restoring $PackageId $Version to collect its XML documentation."
& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Restore of $PackageId $Version failed with exit code $LASTEXITCODE."
}

# NuGet lowercases the identifier and version on disk, so the folder is located by search rather
# than by assuming a casing convention.
$packageRoot = Get-ChildItem -LiteralPath $packagesPath -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq $PackageId.ToLowerInvariant() } |
    Select-Object -First 1

if ($null -eq $packageRoot) {
    throw "Restore reported success but no folder for $PackageId was found under '$packagesPath'."
}

# Only the package's own documentation is collected. Its dependencies were restored too, and
# indexing their members would resolve references against packages this build does not depend on.
$documentation = @(Get-ChildItem -LiteralPath $packageRoot.FullName -Recurse -File -Filter "$PackageId.xml")

if ($documentation.Count -eq 0) {
    throw "$PackageId $Version ships no XML documentation, so there is nothing to resolve against."
}

foreach ($file in $documentation) {
    # Keyed by the containing framework folder so that two frameworks shipping the same file name
    # cannot overwrite one another.
    $frameworkName = $file.Directory.Name
    $frameworkPath = Join-Path $DestinationPath $frameworkName
    New-Item -ItemType Directory -Force -Path $frameworkPath | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $frameworkPath $file.Name) -Force
}

# The restore tree is large and is not an input to anything downstream; only the collected
# documentation is. Removing it also keeps the scan that follows from walking the dependencies.
Remove-Item -LiteralPath $workingPath -Recurse -Force

Write-Host ("Collected $($documentation.Count) XML documentation file(s) for $PackageId $Version " +
    "into '$DestinationPath'.")

<#
.SYNOPSIS
    Verifies that NuGet packages comply with Microsoft metadata requirements for nuget.org.

.DESCRIPTION
    This script downloads the NuGet.VerifyMicrosoftPackage tool and runs it against the specified
    .nupkg files to validate that they meet Microsoft's metadata policies for publishing to
    nuget.org.

    The verification tool checks package metadata such as authors, copyright, license URL, and
    project URL.

    The NuGet.VerifyMicrosoftPackage NuGet package is acquired via dotnet restore using the
    PackageDownload mechanism, which places it in the global NuGet packages cache.  A temporary
    .csproj file is created to facilitate the download, since PackageDownload must be declared in
    a project file (it cannot be passed as an MSBuild property on the command line).

    This script requires Windows because the NuGet.VerifyMicrosoftPackage executable is
    Windows-only.

    See: https://github.com/NuGet/NuGetGallery/tree/main/src/VerifyMicrosoftPackage

.PARAMETER PackagePath
    The path (including glob pattern) to the .nupkg files to verify.
    For example: 'C:\output\*.nupkg' or '$(Build.ArtifactStagingDirectory)\*.nupkg'.

.PARAMETER NugetConfig
    The full path to the NuGet.config file that dotnet restore must use to resolve package feeds.

.EXAMPLE
    .\verify-nuget-package.ps1 -PackagePath 'C:\output\*.nupkg' -NugetConfig 'C:\repo\NuGet.config'

    Verifies all .nupkg files in C:\output against Microsoft metadata requirements,
    using the specified NuGet.config.

.EXAMPLE
    .\verify-nuget-package.ps1 -PackagePath 'C:\packages\Microsoft.Data.SqlClient.7.0.0.nupkg' -NugetConfig 'C:\repo\NuGet.config'

    Verifies a single specific package.

.NOTES
    The NuGet.VerifyMicrosoftPackage package must be available from the NuGet feeds configured in
    the specified NuGet.config file.  In CI pipelines, ensure the governed feed includes this
    package.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$NugetConfig
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$packageName = "NuGet.VerifyMicrosoftPackage"
$packageVersion = "1.0.0"

# Create a temporary project file with a PackageDownload element.  dotnet restore will download the
# package into the global NuGet cache.
$tempDir = Join-Path $env:TEMP $packageName
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageDownload Include="$packageName" Version="[$packageVersion]" />
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $tempDir "_.csproj")

if (!(Test-Path $NugetConfig)) {
    Write-Error "NuGet.config not found at $NugetConfig"
    exit 1
}

Write-Host "Downloading $packageName $packageVersion..."
Write-Host "Using NuGet.config: $NugetConfig"
dotnet restore (Join-Path $tempDir "_.csproj") --configfile $NugetConfig --verbosity normal -tl:off
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to download $packageName package."
    exit 1
}

# Resolve the exe from the global NuGet packages cache.
$globalPkgs = ((dotnet nuget locals global-packages --list) -replace 'global-packages:\s*','').Trim()
$exe = Join-Path $globalPkgs $packageName.ToLower() $packageVersion "tools" "$packageName.exe"

if (!(Test-Path $exe)) {
    Write-Error "Could not find $packageName.exe at $exe"
    exit 1
}

# Verify that the PackagePath parent directory exists before searching for .nupkg files.
$packageDir = Split-Path -Path $PackagePath -Parent
if (!(Test-Path $packageDir)) {
    Write-Error "Package directory not found: $packageDir"
    exit 1
}

# Find .nupkg files to verify.  Wrap in @() to ensure $packages is always an array, so .Count is
# accurate for 0, 1, or N results.
$packages = @(Get-ChildItem -Path $PackagePath -Filter *.nupkg -Recurse |
    Where-Object { $_.Extension -eq '.nupkg' })

if ($packages.Count -eq 0) {
    Write-Host "No .nupkg files found matching '$PackagePath'. Skipping verification."
    exit 0
}

Write-Host "Verifying $($packages.Count) package(s)..."
foreach ($pkg in $packages) {
    Write-Host "  - $($pkg.FullName)"
}

& $exe $packages.FullName
if ($LASTEXITCODE -ne 0) {
    Write-Error "NuGet package verification failed."
    exit 1
}

Write-Host "All packages passed Microsoft metadata verification."

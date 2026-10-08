<#
.SYNOPSIS
    Verifies that every assembly shipped inside an official build's NuGet packages is Authenticode
    signed.

.DESCRIPTION
    Expands each .nupkg beneath a directory and checks the Authenticode signature of every assembly
    it contains, including native binaries under runtimes/.

    Packages are expanded rather than installed through NuGet so that every produced package is
    covered without resolving dependencies, and so that the check does not depend on any single
    package id.

    This complements PackageValidator, which reports strong-name state from assembly metadata
    cross-platform. Authenticode verification requires the Windows trust store, so this script runs
    only on Windows agents and only for official builds; non-official builds deliberately produce
    unsigned assemblies.

    Every assembly is checked before failing, so a single run reports all unsigned assemblies
    rather than stopping at the first.

.PARAMETER PackagesPath
    Directory scanned recursively for .nupkg files to expand.

.PARAMETER ExtractPath
    Directory the packages are expanded into. Each package is expanded into its own subdirectory so
    that identically-named assemblies from different packages cannot collide. Existing content for
    a package is replaced.

.EXAMPLE
    ./verify-assembly-signatures.ps1 -PackagesPath ./packages -ExtractPath ./extract

    Expands every package beneath ./packages and verifies the signature of each assembly.

.NOTES
    File Name : verify-assembly-signatures.ps1
    Requires  : PowerShell 7+ on Windows (Get-AuthenticodeSignature).
    Called by : validate-packages-job.yml
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Directory scanned recursively for .nupkg files.")]
    [ValidateNotNullOrEmpty()]
    [string]$PackagesPath,

    [Parameter(Mandatory = $true, HelpMessage = "Directory the packages are expanded into.")]
    [ValidateNotNullOrEmpty()]
    [string]$ExtractPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "=== Verify Assembly Signatures Parameters ==="
Write-Host "PackagesPath: ${PackagesPath}"
Write-Host "ExtractPath:  ${ExtractPath}"
Write-Host "============================================="

$packages = @(Get-ChildItem -Path $PackagesPath -Recurse -File -Filter *.nupkg -ErrorAction SilentlyContinue)
if ($packages.Count -eq 0) {
    throw "No .nupkg files were found under '${PackagesPath}'."
}

New-Item -ItemType Directory -Force -Path $ExtractPath | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($package in $packages) {
    $destination = Join-Path $ExtractPath $package.BaseName
    if (Test-Path -LiteralPath $destination) {
        Remove-Item -LiteralPath $destination -Recurse -Force
    }

    Write-Host "Expanding $($package.Name)"
    [System.IO.Compression.ZipFile]::ExtractToDirectory($package.FullName, $destination)
}

$assemblies = @(Get-ChildItem -Path $ExtractPath -Recurse -File -Filter *.dll)
if ($assemblies.Count -eq 0) {
    throw "No assemblies were found under '${ExtractPath}'."
}

# Every assembly is checked before throwing so one run reports all failures.
$unsigned = @()
foreach ($signature in @(Get-AuthenticodeSignature -FilePath $assemblies.FullName)) {
    if ($signature.Status -eq "Valid") {
        Write-Host "  OK   $($signature.Path)"
    }
    else {
        Write-Host "  FAIL $($signature.Path) - $($signature.Status)"
        $unsigned += $signature.Path
    }
}

if ($unsigned.Count -gt 0) {
    throw "Authenticode verification failed for $($unsigned.Count) of $($assemblies.Count) assemblies."
}

Write-Host "All $($assemblies.Count) assemblies are Authenticode signed."

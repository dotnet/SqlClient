<#
.SYNOPSIS
    Verifies the NuGet signatures of every package produced by an official OneBranch build.

.DESCRIPTION
    Runs `dotnet nuget verify --all` over every .nupkg and .snupkg found beneath a directory,
    confirming that each carries a valid, trusted signature.

    This complements PackageValidator, which reports signature *presence* from package metadata
    cross-platform. Establishing that a signature is trusted requires the platform trust store,
    which is why this runs separately and only on official builds. Non-official builds deliberately
    produce unsigned packages, so verifying them would always fail.

    Every package is verified before failing, so a single run reports all unsigned packages rather
    than stopping at the first.

.PARAMETER PackagesPath
    Directory scanned recursively for .nupkg and .snupkg files.

.PARAMETER DotnetPath
    dotnet executable to invoke. Defaults to the dotnet command resolved from PATH. This parameter
    primarily supports isolated testing.

.EXAMPLE
    ./verify-package-signatures.ps1 -PackagesPath ./packages

    Verifies every package and symbol package beneath ./packages.

.NOTES
    File Name : verify-package-signatures.ps1
    Requires  : PowerShell 7+ and the repository-pinned .NET SDK.
    Called by : validate-packages-job.yml
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Directory scanned recursively for package files.")]
    [ValidateNotNullOrEmpty()]
    [string]$PackagesPath,

    [Parameter(HelpMessage = "dotnet executable to invoke.")]
    [ValidateNotNullOrEmpty()]
    [string]$DotnetPath = "dotnet"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "=== Verify Package Signatures Parameters ==="
Write-Host "PackagesPath: ${PackagesPath}"
Write-Host "============================================"

$packages = @(Get-ChildItem -Path $PackagesPath -Recurse -File -Include *.nupkg, *.snupkg -ErrorAction SilentlyContinue)
if ($packages.Count -eq 0) {
    throw "No package files were found under '${PackagesPath}'."
}

# Every package is checked before throwing so one run reports all failures.
$failed = @()
foreach ($package in $packages) {
    Write-Host "Verifying $($package.Name)"
    & $DotnetPath nuget verify --all $package.FullName
    if ($LASTEXITCODE -ne 0) {
        $failed += $package.Name
    }
}

if ($failed.Count -gt 0) {
    throw "NuGet signature verification failed for $($failed.Count) of $($packages.Count) package(s): $($failed -join ', ')"
}

Write-Host "All $($packages.Count) package signature(s) verified."

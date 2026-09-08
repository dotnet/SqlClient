<#
.SYNOPSIS
    Runs the PackageValidator tool over the NuGet packages produced by a OneBranch build.

.DESCRIPTION
    Invokes tools/PackageValidator once for a whole directory of packages rather than once per
    package, because its most valuable checks are cross-package: every package in the SqlClient
    family must carry the same version, and their inter-package dependency ranges must agree.
    Validating one package at a time would silently skip all of those findings.

    The validator runs twice over the same inputs. The first run writes a machine-readable report
    and never gates, so the report exists even when validation fails. The second run renders the
    human-readable report and applies the gate, so a failing build shows its findings in its own
    log rather than only in an artifact.

    Expected versions are supplied by the caller rather than derived here. The compute-versions
    stage already computes every version the build stamps, and re-deriving them would reintroduce
    the drift this validation exists to catch.

    Microsoft.SqlServer.Server is versioned separately from the SqlClient family, so its expected
    versions are applied as a per-id override of the family wildcard. When it is not built in a
    run, its package is absent from the drop and its expectations must be omitted entirely: the
    validator rejects an expectation whose value is empty.

.PARAMETER ValidatorPath
    Path to the built PackageValidator.dll. Invoked through the managed assembly rather than the
    native apphost so the same command works regardless of agent OS.

.PARAMETER PackagesPath
    Directory scanned recursively for .nupkg files. Sibling .snupkg files must sit beside their
    .nupkg for symbol matching to resolve, which is how the build jobs publish them.

.PARAMETER ReportPath
    Path of the JSON report to write. Parent directories are created as needed.

.PARAMETER SqlClientPackageVersion
    Package version expected of every package in the SqlClient family, applied as a wildcard.
    Pointing every package at one value is what proves they agree, and also catches the case where
    all of them are consistently wrong.

.PARAMETER SqlClientFileVersion
    Assembly file version expected of every assembly in the SqlClient family.

.PARAMETER SqlServerPackageVersion
    Package version expected of Microsoft.SqlServer.Server. Omit when SqlServer is not built.

.PARAMETER SqlServerFileVersion
    Assembly file version expected of Microsoft.SqlServer.Server. Omit when SqlServer is not built.

.PARAMETER FailOn
    Finding severities and/or categories that fail the build. Run the validator with --help to see
    the available categories. Note that missing-symbols is a warning and package-unsigned is info,
    so neither is covered by the error severity and both must be named explicitly.

    Accepts either an array or a single comma-separated string, because an Azure Pipelines task
    argument line collapses to one token and PowerShell's -File mode does not split it.

.PARAMETER DotnetPath
    dotnet executable to invoke. Defaults to the dotnet command resolved from PATH. This parameter
    primarily supports isolated testing.

.EXAMPLE
    ./validate-packages.ps1 `
        -ValidatorPath ./PackageValidator.dll `
        -PackagesPath ./packages `
        -ReportPath ./out/report.json `
        -SqlClientPackageVersion 7.1.0-preview3.26238.3 `
        -SqlClientFileVersion 7.1.0.26238 `
        -FailOn error,missing-symbols

    Validates a family-only drop, failing on any error and on missing symbols.

.NOTES
    File Name : validate-packages.ps1
    Requires  : PowerShell 7+ and the repository-pinned .NET SDK.
    Called by : validate-packages-step.yml

    PackageValidator exit codes:
      0 - No gating findings.
      1 - The validator itself failed.
      2 - A --fail-on gate was tripped.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Path to the built PackageValidator.dll.")]
    [ValidateNotNullOrEmpty()]
    [string]$ValidatorPath,

    [Parameter(Mandatory = $true, HelpMessage = "Directory scanned recursively for .nupkg files.")]
    [ValidateNotNullOrEmpty()]
    [string]$PackagesPath,

    [Parameter(Mandatory = $true, HelpMessage = "Path of the JSON report to write.")]
    [ValidateNotNullOrEmpty()]
    [string]$ReportPath,

    [Parameter(Mandatory = $true, HelpMessage = "Package version expected of the SqlClient family.")]
    [ValidateNotNullOrEmpty()]
    [string]$SqlClientPackageVersion,

    [Parameter(Mandatory = $true, HelpMessage = "File version expected of the SqlClient family.")]
    [ValidateNotNullOrEmpty()]
    [string]$SqlClientFileVersion,

    [Parameter(HelpMessage = "Package version expected of Microsoft.SqlServer.Server, when built.")]
    [string]$SqlServerPackageVersion = "",

    [Parameter(HelpMessage = "File version expected of Microsoft.SqlServer.Server, when built.")]
    [string]$SqlServerFileVersion = "",

    [Parameter(Mandatory = $true, HelpMessage = "Severities and/or categories that fail the build.")]
    [ValidateNotNullOrEmpty()]
    [string[]]$FailOn,

    [Parameter(HelpMessage = "dotnet executable to invoke.")]
    [ValidateNotNullOrEmpty()]
    [string]$DotnetPath = "dotnet"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Split on commas so a single "error,missing-symbols" token behaves like a two-element array.
$failOnTokens = @($FailOn -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($failOnTokens.Count -eq 0) {
    throw "FailOn must name at least one severity or category."
}

Write-Host "=== Validate Packages Parameters ==="
Write-Host "ValidatorPath:           ${ValidatorPath}"
Write-Host "PackagesPath:            ${PackagesPath}"
Write-Host "ReportPath:              ${ReportPath}"
Write-Host "SqlClientPackageVersion: ${SqlClientPackageVersion}"
Write-Host "SqlClientFileVersion:    ${SqlClientFileVersion}"
Write-Host "SqlServerPackageVersion: ${SqlServerPackageVersion}"
Write-Host "SqlServerFileVersion:    ${SqlServerFileVersion}"
Write-Host "FailOn:                  $($failOnTokens -join ', ')"
Write-Host "===================================="

if (-not (Test-Path -LiteralPath $ValidatorPath)) {
    throw "PackageValidator was not found at '${ValidatorPath}'."
}

$packages = @(Get-ChildItem -Path $PackagesPath -Recurse -File -Filter *.nupkg -ErrorAction SilentlyContinue)
if ($packages.Count -eq 0) {
    throw "No .nupkg files were found under '${PackagesPath}'."
}

Write-Host "Validating $($packages.Count) package(s):"
$packages | ForEach-Object { Write-Host "  $($_.Name)" }

# A bare value applies to every package; an id=value pair overrides it for that package only.
$expectations = @(
    "--expect-package-version", "*=${SqlClientPackageVersion}"
    "--expect-file-version", "*=${SqlClientFileVersion}"
)

# Both SqlServer versions travel together: supplying only one would assert half a package.
$hasSqlServerPackageVersion = -not [string]::IsNullOrWhiteSpace($SqlServerPackageVersion)
$hasSqlServerFileVersion = -not [string]::IsNullOrWhiteSpace($SqlServerFileVersion)
if ($hasSqlServerPackageVersion -ne $hasSqlServerFileVersion) {
    throw "SqlServerPackageVersion and SqlServerFileVersion must be supplied together, or not at all."
}

if ($hasSqlServerPackageVersion) {
    $expectations += @(
        "--expect-package-version", "Microsoft.SqlServer.Server=${SqlServerPackageVersion}"
        "--expect-file-version", "Microsoft.SqlServer.Server=${SqlServerFileVersion}"
    )
}

$gate = @()
foreach ($token in $failOnTokens) {
    $gate += @("--fail-on", $token)
}

Write-Host "Expectations: $($expectations -join ' ')"
Write-Host "Gate:         $($gate -join ' ')"

$reportDirectory = Split-Path -Parent $ReportPath
if ($reportDirectory) {
    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
}

# Reported before gating so the JSON exists even for a failing run.
& $DotnetPath $ValidatorPath $PackagesPath --json @expectations |
    Set-Content -LiteralPath $ReportPath -Encoding utf8
$reportExitCode = $LASTEXITCODE

# This run is ungated, so any non-zero code means the validator itself failed and the report it
# produced cannot be trusted.  Fail here rather than let the gated run obscure the real cause.
if ($reportExitCode -ne 0) {
    throw "PackageValidator failed while writing the JSON report (exit code ${reportExitCode})."
}

Write-Host "Wrote JSON report to ${ReportPath}"

Write-Host ""
Write-Host "=== Package validation report ==="
& $DotnetPath $ValidatorPath $PackagesPath @expectations @gate
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host ""
    Write-Host "Package validation passed."
}
elseif ($exitCode -eq 2) {
    throw "Package validation failed: one or more findings matched the gate ($($failOnTokens -join ', '))."
}
else {
    throw "PackageValidator exited unexpectedly with code ${exitCode}."
}

####################################################################################################
# Licensed to the .NET Foundation under one or more agreements.  The .NET Foundation licenses this
# file to you under the MIT license.  See the LICENSE file in the project root for more information.
####################################################################################################
#
# run-perf-tests.ps1
#
# Entry point executed ON the Perf Test Lab Windows VM by the InternalDriverTools/PerfTest extends
# template (v1/Perf.Test.Job.yml).  The template SCPs the driver source tree to the VM, runs this
# script over SSH, then SCPs the results sub-directory back and publishes it as a pipeline artifact.
#
# This is the Windows counterpart of run-perf-tests.sh.  See that file for the full description of
# responsibilities.  On Windows the benchmark client is pinned to the reserved CPU set via the
# process ProcessorAffinity mask (derived from PERF_CLIENT_CPUS) instead of taskset.
#
# Environment variables injected by the template (see wiki "Performance Test Automation"):
#   SQL_SERVER         Host/IP of the SQL Server on the perf VM (e.g. localhost).
#   SQL_PASSWORD       SQL Server 'sa' password.
#   PERF_CLIENT_CPUS   Core range reserved for the test client, e.g. "16-31".
#   PERF_SQL_CPUS      Core range SQL Server is pinned to, e.g. "0-15" (informational).
#
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net9.0",
    [string]$ResultsSubdir = "perf-results"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

####################################################################################################
# Resolve paths
####################################################################################################

# This script lives at <repo>/eng/pipelines/perf/scripts/run-perf-tests.ps1, so the repo root is
# four levels up.
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir "..\..\..\..")).Path
$PerfProject = Join-Path $RepoRoot "src\Microsoft.Data.SqlClient\tests\PerformanceTests\Microsoft.Data.SqlClient.PerformanceTests.csproj"
$PerfDir = Split-Path -Parent $PerfProject
$ResultsDir = Join-Path $RepoRoot $ResultsSubdir

$SqlServer = if ($env:SQL_SERVER) { $env:SQL_SERVER } else { "localhost" }
$SqlPassword = $env:SQL_PASSWORD
$DbName = "sqlclient-perf-db"

Write-Host "=================================================================="
Write-Host " SqlClient Performance Tests"
Write-Host "=================================================================="
Write-Host "  Repo root       : $RepoRoot"
Write-Host "  Perf project    : $PerfProject"
Write-Host "  Configuration   : $Configuration"
Write-Host "  Framework       : $Framework"
Write-Host "  Results dir     : $ResultsDir"
Write-Host "  SQL_SERVER      : $SqlServer"
Write-Host "  PERF_CLIENT_CPUS: $($env:PERF_CLIENT_CPUS)"
Write-Host "  PERF_SQL_CPUS   : $($env:PERF_SQL_CPUS)"
Write-Host "=================================================================="

if (-not (Test-Path $PerfProject)) {
    throw "Performance test project not found at $PerfProject"
}
if ([string]::IsNullOrEmpty($SqlPassword)) {
    throw "SQL_PASSWORD environment variable is not set (expected from the perf template)."
}

New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

$env:DOTNET_NOLOGO = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

####################################################################################################
# 1. Install the .NET SDK (pinned by global.json) and the runtimes for the target frameworks.
####################################################################################################

function Install-DotNet {
    $globalJson = Get-Content (Join-Path $RepoRoot "global.json") -Raw
    # Strip // comments so ConvertFrom-Json accepts the file.
    $globalJson = ($globalJson -split "`n" | ForEach-Object { $_ -replace '//.*$', '' }) -join "`n"
    $sdkVersion = (ConvertFrom-Json $globalJson).sdk.version
    if ([string]::IsNullOrEmpty($sdkVersion)) {
        throw "Could not determine SDK version from global.json"
    }

    $dotnetRoot = Join-Path $env:USERPROFILE ".dotnet"
    $env:DOTNET_ROOT = $dotnetRoot
    $env:PATH = "$dotnetRoot;$dotnetRoot\tools;$env:PATH"

    Write-Host "Installing .NET SDK $sdkVersion into $dotnetRoot ..."
    $installScript = Join-Path $env:TEMP "dotnet-install.ps1"
    Invoke-WebRequest -UseBasicParsing "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript

    & $installScript -Version $sdkVersion -InstallDir $dotnetRoot
    foreach ($channel in @("8.0", "9.0", "10.0")) {
        & $installScript -Channel $channel -Runtime dotnet -InstallDir $dotnetRoot
    }
}

$hasNet10Sdk = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    if ((dotnet --list-sdks) -match '^10\.0\.') { $hasNet10Sdk = $true }
}
if ($hasNet10Sdk) {
    Write-Host "Using pre-installed dotnet: $((Get-Command dotnet).Source)"
} else {
    Install-DotNet
}

dotnet --info

####################################################################################################
# 2. Create the perf database on the VM's SQL Server.
#
# The benchmark runners create their own tables but not the database, so create it here
# (idempotently) using the Microsoft.Data.SqlClient assembly that ships with the SDK-less runtime,
# invoked through a tiny inline program.  sqlcmd is used when available; otherwise we fall back to a
# .NET one-liner via the perf project's own driver reference.
####################################################################################################

Write-Host "Ensuring database [$DbName] exists on $SqlServer ..."

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if ($sqlcmd) {
    & $sqlcmd.Source -S $SqlServer -U sa -P $SqlPassword -C -b -l 30 `
        -Q "IF DB_ID('$DbName') IS NULL CREATE DATABASE [$DbName];"
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed to create database [$DbName] (exit $LASTEXITCODE)." }
    Write-Host "Database [$DbName] is ready."
} else {
    throw "sqlcmd was not found on the VM; cannot create the perf database [$DbName]."
}

####################################################################################################
# 3. Inject the VM's SQL Server connection string into the benchmark runner config.
####################################################################################################

$RunnerConfig = Join-Path $RepoRoot "perf-runnerconfig.json"
$env:RUNNER_CONFIG = $RunnerConfig

$srcConfig = Join-Path $PerfDir "runnerconfig.jsonc"
$rawConfig = Get-Content $srcConfig -Raw
# Strip // line comments so ConvertFrom-Json accepts the .jsonc content.
$rawConfig = ($rawConfig -split "`n" | ForEach-Object { $_ -replace '(?m)^\s*//.*$', '' }) -join "`n"
$cfg = ConvertFrom-Json $rawConfig

$cfg.ConnectionString = "Server=tcp:$SqlServer,1433;User ID=sa;Password=$SqlPassword;Initial Catalog=$DbName;TrustServerCertificate=True;Encrypt=False;"
$cfg | ConvertTo-Json -Depth 10 | Set-Content -Path $RunnerConfig -Encoding UTF8
Write-Host "Wrote runner config to $RunnerConfig (Server=tcp:$SqlServer,1433; Initial Catalog=$DbName)"

####################################################################################################
# 4 & 5. Run the benchmarks, pinned to the reserved client CPU set.
####################################################################################################

$RunDir = Join-Path $RepoRoot "perf-run"
if (Test-Path $RunDir) { Remove-Item -Recurse -Force $RunDir }
New-Item -ItemType Directory -Force -Path $RunDir | Out-Null

Write-Host "Building performance tests ($Configuration, $Framework) ..."
dotnet build $PerfProject -c $Configuration -f $Framework --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

# Convert a CPU range like "16-31" (or a comma list "16,17,18") into an affinity bitmask.
function Get-AffinityMask([string]$cpuSpec) {
    if ([string]::IsNullOrEmpty($cpuSpec)) { return $null }
    [long]$mask = 0
    foreach ($part in $cpuSpec.Split(",")) {
        if ($part -match '^\s*(\d+)\s*-\s*(\d+)\s*$') {
            for ($c = [int]$Matches[1]; $c -le [int]$Matches[2]; $c++) { $mask = $mask -bor ([long]1 -shl $c) }
        } elseif ($part -match '^\s*(\d+)\s*$') {
            $mask = $mask -bor ([long]1 -shl [int]$Matches[1])
        }
    }
    return $mask
}

Push-Location $RunDir
try {
    Write-Host "Starting benchmarks: dotnet run --project $PerfProject -c $Configuration -f $Framework --no-build"
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "dotnet"
    $psi.Arguments = "run --project `"$PerfProject`" -c $Configuration -f $Framework --no-build"
    $psi.UseShellExecute = $false
    $psi.WorkingDirectory = $RunDir

    $proc = [System.Diagnostics.Process]::Start($psi)

    $mask = Get-AffinityMask $env:PERF_CLIENT_CPUS
    if ($null -ne $mask -and $mask -gt 0) {
        try {
            $proc.ProcessorAffinity = [System.IntPtr]$mask
            Write-Host "Pinned benchmark client (PID $($proc.Id)) to CPUs $($env:PERF_CLIENT_CPUS) (mask 0x$($mask.ToString('X')))."
        } catch {
            Write-Warning "Failed to set ProcessorAffinity: $_"
        }
    } else {
        Write-Warning "PERF_CLIENT_CPUS unset; running without CPU pinning."
    }

    $proc.WaitForExit()
    if ($proc.ExitCode -ne 0) { throw "Benchmark run failed (exit $($proc.ExitCode))." }
} finally {
    Pop-Location
}

####################################################################################################
# 6. Collect BenchmarkDotNet artifacts into the results sub-directory.
####################################################################################################

$ArtifactsDir = Join-Path $RunDir "BenchmarkDotNet.Artifacts"
if (Test-Path $ArtifactsDir) {
    Write-Host "Collecting BenchmarkDotNet artifacts into $ResultsDir ..."
    # Keep the full artifact tree (logs + per-run report folder) for detailed inspection.
    Copy-Item -Recurse -Force $ArtifactsDir (Join-Path $ResultsDir "BenchmarkDotNet.Artifacts")
    # Also flatten the report files (github markdown, csv, html) to the TOP of the results folder.
    # The collect-results template auto-attaches top-level results/*.md files as run summaries, so
    # placing the *-report-github.md reports here makes them show up on the pipeline run.
    $ReportsDir = Join-Path $ArtifactsDir "results"
    if (Test-Path $ReportsDir) {
        Copy-Item -Recurse -Force (Join-Path $ReportsDir "*") $ResultsDir
    }
} else {
    Write-Warning "No BenchmarkDotNet.Artifacts directory was produced at $ArtifactsDir."
}

Write-Host "Collected results:"
Get-ChildItem -Recurse -File $ResultsDir | ForEach-Object { $_.FullName } | Sort-Object

Write-Host "=================================================================="
Write-Host " Performance run complete."
Write-Host "=================================================================="

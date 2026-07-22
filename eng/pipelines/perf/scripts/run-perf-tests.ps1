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
    [string]$ResultsSubdir = "perf-results",
    [string]$BaselineVersion = "",
    [string]$RegressionThreshold = "10"
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
Write-Host "  Baseline ver    : $(if ($BaselineVersion) { $BaselineVersion } else { '<none, current-only>' })"
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

# Record VM-side run metadata (e.g. the perf VM hostname) for the agent-side Kusto translation.
"MACHINE_NAME=$env:COMPUTERNAME" | Set-Content -Path (Join-Path $ResultsDir "runinfo.env") -Encoding ASCII

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
#
# Two passes are executed so the pipeline can compare the branch under test against a released
# baseline:
#   * baseline  -> Microsoft.Data.SqlClient restored from NuGet.org at $BaselineVersion
#                  (ReferenceType=Package + CPM VersionOverride).  Skipped when no baseline is given.
#   * current   -> Microsoft.Data.SqlClient built from the source tree in this repo (ProjectReference).
#
# Each pass runs from its own directory; its BenchmarkDotNet artifacts are collected into
# results\<label>\.
####################################################################################################

# NuGet.config on the VM exposes only the governed feed; the baseline package (and its public deps)
# live on NuGet.org.  Central Package Management rejects multiple unmapped sources (NU1507), so the
# baseline restore uses a dedicated single-source config pointing only at NuGet.org.
$BaselineNuGetConfig = Join-Path $RepoRoot "perf-baseline-nuget.config"
function Write-BaselineNuGetConfig {
    @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
'@ | Set-Content -Path $BaselineNuGetConfig -Encoding UTF8
}

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

# Runs one benchmark pass (build + run pinned to PERF_CLIENT_CPUS) and collects its artifacts into
# results\<label>. $ExtraArgs are appended to both the build and run invocations.
function Invoke-PerfPass([string]$Label, [string[]]$ExtraArgs) {
    $runDir = Join-Path $RepoRoot "perf-run-$Label"
    if (Test-Path $runDir) { Remove-Item -Recurse -Force $runDir }
    New-Item -ItemType Directory -Force -Path $runDir | Out-Null

    Write-Host "------------------------------------------------------------------"
    Write-Host " Pass: $Label"
    Write-Host "   Extra args: $($ExtraArgs -join ' ')"
    Write-Host "------------------------------------------------------------------"

    Write-Host "Building performance tests ($Configuration, $Framework) for '$Label' ..."
    dotnet build $PerfProject -c $Configuration -f $Framework --nologo -v minimal @ExtraArgs
    if ($LASTEXITCODE -ne 0) { throw "Build failed for '$Label' (exit $LASTEXITCODE)." }

    Push-Location $runDir
    try {
        $runArgs = @("run", "--project", $PerfProject, "-c", $Configuration, "-f", $Framework, "--no-build") + $ExtraArgs
        Write-Host "Starting benchmarks ($Label): dotnet $($runArgs -join ' ')"
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "dotnet"
        foreach ($a in $runArgs) { $psi.ArgumentList.Add($a) }
        $psi.UseShellExecute = $false
        $psi.WorkingDirectory = $runDir

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
        if ($proc.ExitCode -ne 0) { throw "Benchmark run '$Label' failed (exit $($proc.ExitCode))." }
    } finally {
        Pop-Location
    }

    $artifactsDir = Join-Path $runDir "BenchmarkDotNet.Artifacts"
    $dest = Join-Path $ResultsDir $Label
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    if (Test-Path $artifactsDir) {
        Write-Host "Collecting '$Label' BenchmarkDotNet artifacts into $dest ..."
        Copy-Item -Recurse -Force $artifactsDir (Join-Path $dest "BenchmarkDotNet.Artifacts")
        $reportsDir = Join-Path $artifactsDir "results"
        if (Test-Path $reportsDir) {
            Copy-Item -Recurse -Force (Join-Path $reportsDir "*") $dest
        }
    } else {
        Write-Warning "No BenchmarkDotNet.Artifacts directory was produced for '$Label' at $artifactsDir."
    }
}

# --- Baseline pass (released NuGet package) -------------------------------------------------------
if (-not [string]::IsNullOrEmpty($BaselineVersion)) {
    Write-BaselineNuGetConfig
    Invoke-PerfPass "baseline" @(
        "-p:ReferenceType=Package",
        "-p:MdsPackageVersion=$BaselineVersion",
        "-p:RestoreConfigFile=$BaselineNuGetConfig"
    )
} else {
    Write-Host "No -BaselineVersion supplied; skipping the baseline pass."
}

# --- Current pass (branch under test, built from source) ------------------------------------------
Invoke-PerfPass "current" @()

####################################################################################################
# 6. Compare the two passes and surface a delta (only when a baseline pass ran).
####################################################################################################

if (-not [string]::IsNullOrEmpty($BaselineVersion)) {
    Write-Host "Comparing current branch against baseline $BaselineVersion ..."
    $comparisonDir = Join-Path $ResultsDir "comparison"
    New-Item -ItemType Directory -Force -Path $comparisonDir | Out-Null
    python3 (Join-Path $ScriptDir "compare_perf.py") `
        --baseline-dir (Join-Path $ResultsDir "baseline") `
        --current-dir (Join-Path $ResultsDir "current") `
        --baseline-version $BaselineVersion `
        --threshold $RegressionThreshold `
        --out-md (Join-Path $comparisonDir "comparison.md") `
        --out-json (Join-Path $comparisonDir "comparison.json")
    if ($LASTEXITCODE -ne 0) { throw "Comparison failed (exit $LASTEXITCODE)." }
    # Surface the comparison as the top-level run summary (collect-results.yml attaches results\*.md).
    Copy-Item -Force (Join-Path $comparisonDir "comparison.md") (Join-Path $ResultsDir "summary.md")
}

Write-Host "Collected results:"
Get-ChildItem -Recurse -File $ResultsDir | ForEach-Object { $_.FullName } | Sort-Object

Write-Host "=================================================================="
Write-Host " Performance run complete."
Write-Host "=================================================================="

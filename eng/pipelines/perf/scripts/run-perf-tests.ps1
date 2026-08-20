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
    # Alternative to -BaselineVersion: benchmark against Microsoft.Data.SqlClient built from ANOTHER
    # git ref of this repository (e.g. 'main') instead of a released NuGet package.  Used by the PR
    # perf pipeline, which compares the branch under test against the source it would merge into.
    # The two baseline selectors are mutually exclusive.
    [string]$BaselineSourceRef = "",
    # Remote used to obtain the baseline ref when it cannot be fetched from the copied checkout's own
    # 'origin' (e.g. the source tree reached the VM without its .git directory, or origin needs auth).
    [string]$BaselineRepoUrl = "https://github.com/dotnet/SqlClient.git",
    [string]$RegressionThreshold = "10",
    # When set, a candidate-slower-than-baseline regression fails the run (wiki 339 §3 gate).
    # Off by default so deltas are reported without blocking until the gate is trusted.
    [switch]$FailOnRegression,
    # Benchmark run model (wiki 339 §2.2/§2.3/§2.6):
    #   interleaved -> run one unit at a time, baseline and candidate back-to-back, with best-of-N
    #                  confirmation of flagged regressions (the noise-resistant default).
    #   sequential  -> legacy: run the whole baseline suite, then the whole candidate suite, compare.
    [ValidateSet("interleaved", "sequential")]
    [string]$RunMode = "interleaved",
    # Best-of-N: total interleaved passes for a flagged unit before a regression is confirmed.
    [ValidateRange(1, [int]::MaxValue)]
    [int]$ConfirmationRuns = 3,
    # Optional SqlClient behaviour flags (true/false, or empty to leave the checked-in
    # runnerconfig.jsonc default untouched).  Written into the runner config the benchmarks run
    # against and, via the pipeline's Kusto translation, recorded in PerfRun.Config.
    [ValidateSet("", "true", "false")]
    [string]$UseManagedSniOnWindows = "",
    [ValidateSet("", "true", "false")]
    [string]$UseOptimizedAsyncBehaviour = "",
    [ValidateSet("", "true", "false")]
    [string]$UseConnectionPoolV2 = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

####################################################################################################
# Native-command error handling
#
# The perf VM runs Windows PowerShell 5.1.  There, with $ErrorActionPreference = 'Stop', ANY write
# to stderr by a native command (dotnet, python) is promoted to a TERMINATING error - even when the
# command's exit code is 0.  The .NET CLI and Python routinely emit non-fatal diagnostics (SDK
# resolution notes, restore/build warnings, progress) to stderr, so an unguarded 'dotnet ...' or
# 'python3 ...' aborts the whole run and surfaces the tool's stderr text as the failure.
#
# PowerShell 7.3+ exposes $PSNativeCommandUseErrorActionPreference to opt out; set it where present.
# On 5.1 there is no such switch, so native tools are invoked through Invoke-Native, which relaxes
# the preference for the duration of the call and judges success solely by the process exit code.
####################################################################################################

if (Test-Path Variable:\PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

# Keep the checkout clean: never let the helper scripts drop __pycache__/*.pyc into eng/.
$env:PYTHONDONTWRITEBYTECODE = "1"

# Run a native command (in a scriptblock) with stderr-as-terminating-error suppressed, then throw
# $FailureMessage if it exited non-zero.  Use for native calls whose non-zero exit must fail the run.
function Invoke-Native {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)][scriptblock] $Command,
        [Parameter(Position = 1)][string] $FailureMessage = "Native command failed"
    )
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Command
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit $LASTEXITCODE)."
    }
}

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
Write-Host "  Run mode        : $RunMode (confirmation runs: $ConfirmationRuns)"
Write-Host "  Baseline ver    : $(if ($BaselineVersion) { $BaselineVersion } else { '<none, current-only>' })"
Write-Host "  Baseline ref    : $(if ($BaselineSourceRef) { $BaselineSourceRef } else { '<none>' })"
Write-Host "  SQL_SERVER      : $SqlServer"
Write-Host "  PERF_CLIENT_CPUS: $($env:PERF_CLIENT_CPUS)"
Write-Host "  PERF_SQL_CPUS   : $($env:PERF_SQL_CPUS)"
Write-Host "=================================================================="

if (-not (Test-Path $PerfProject)) {
    throw "Performance test project not found at $PerfProject"
}
# The two baseline selectors describe different builds of the same "baseline" pass, so requesting
# both is always a mistake; fail fast rather than silently honouring one of them.
if ((-not [string]::IsNullOrEmpty($BaselineVersion)) -and (-not [string]::IsNullOrEmpty($BaselineSourceRef))) {
    throw "-BaselineVersion and -BaselineSourceRef are mutually exclusive."
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
    # 'dotnet --version' evaluated from the repo root honours global.json (including rollForward), so
    # it succeeds only when the pinned SDK is actually installed.  A bare '10.0.*' match would accept
    # the wrong SDK band and skip installing the pinned one.
    Push-Location $RepoRoot
    try {
        # Relax Stop here: a missing/mismatched pinned SDK makes 'dotnet --version' write to stderr
        # and exit non-zero, which under Stop would abort before we can fall through to Install-DotNet.
        $previousPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            dotnet --version *> $null
            if ($LASTEXITCODE -eq 0) { $hasNet10Sdk = $true }
        } finally {
            $ErrorActionPreference = $previousPreference
        }
    } finally {
        Pop-Location
    }
}
if ($hasNet10Sdk) {
    Write-Host "Using pre-installed dotnet: $((Get-Command dotnet).Source)"
} else {
    Install-DotNet
}

# Informational only: never let 'dotnet --info' (or its stderr) fail the run.
try { Invoke-Native { dotnet --info } "dotnet --info failed" } catch { Write-Warning $_.Exception.Message }

####################################################################################################
# 2. Create the perf database on the VM's SQL Server.
#
# The benchmark runners create their own tables but not the database, so create it here
# (idempotently) using sqlcmd.  sqlcmd is required on the VM; if it is not present the script fails
# fast (throws below) rather than continuing on to run benchmarks against a missing database.
####################################################################################################

Write-Host "Ensuring database [$DbName] exists on $SqlServer ..."

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if ($sqlcmd) {
    # Relax Stop around the native sqlcmd call so a benign stderr write cannot abort the run before
    # the explicit exit-code check below (Windows PowerShell 5.1 promotes native stderr under Stop).
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $sqlcmd.Source -S $SqlServer -U sa -P $SqlPassword -C -b -l 30 `
            -Q "IF DB_ID('$DbName') IS NULL CREATE DATABASE [$DbName];"
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed to create database [$DbName] (exit $LASTEXITCODE)." }
    Write-Host "Database [$DbName] is ready."
} else {
    throw "sqlcmd was not found on the VM; cannot create the perf database [$DbName]."
}

####################################################################################################
# Noise-reduction controls (InternalDriverTools wiki 339, "Reducing Noise in Performance Tests").
#
# The Perf Test Lab already provides the isolated dedicated host, the tuned SQL instance and the
# disjoint client CPU set (PERF_CLIENT_CPUS, pinned per pass below).  These are the remaining
# harness-owned controls: per-run diagnostics, a fail-loud preflight and a warm-up, so a run's
# mean/variance is steadier and a broken run cannot masquerade as a pass.  (The glibc allocator and
# sysctl tuning from the Linux harness are Linux-only and intentionally omitted here.)
####################################################################################################

$DiagDir = Join-Path $ResultsDir "diagnostics"
New-Item -ItemType Directory -Force -Path $DiagDir | Out-Null

# --- §2.11 Capture host CPU topology (static, once per run) ---------------------------------------
try {
    Get-CimInstance Win32_Processor |
        Select-Object Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, CurrentClockSpeed |
        Format-List | Out-File -FilePath (Join-Path $DiagDir "cpu-info.txt") -Encoding UTF8
} catch { Write-Warning "Could not capture CPU info: $_" }

# --- §2.11 Capture the SQL instance configuration (confirm the lab tuning actually took effect) ---
try {
    & $sqlcmd.Source -S $SqlServer -U sa -P $SqlPassword -C -b -l 30 -h -1 -W `
        -Q "SET NOCOUNT ON;
            SELECT name, value_in_use FROM sys.configurations
              WHERE name IN ('max degree of parallelism','cost threshold for parallelism',
                             'max server memory (MB)','min server memory (MB)','affinity mask',
                             'affinity I/O mask');
            SELECT 'tempdb_data_files' AS setting, COUNT(*) AS value FROM tempdb.sys.database_files WHERE type = 0;
            SELECT @@VERSION;" `
        *> (Join-Path $DiagDir "sql-config.txt")
    Write-Host "Captured SQL instance config -> $(Join-Path $DiagDir 'sql-config.txt')"
} catch { Write-Warning "Could not capture SQL instance config: $_" }

# --- §2.10 / §2.5 Fail loud on an unreachable server, and warm the buffer pool / plan cache -------
# A benchmark suite that "skips" when the server is down produces an empty comparison that reads
# green; verify connectivity up front and touch the target DB before the first measured benchmark.
$previousPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    & $sqlcmd.Source -S $SqlServer -U sa -P $SqlPassword -C -b -l 15 `
        -Q "SET NOCOUNT ON; USE [$DbName]; SELECT 1;" *> $null
} finally {
    $ErrorActionPreference = $previousPreference
}
if ($LASTEXITCODE -ne 0) {
    throw "SQL Server $SqlServer (db $DbName) is unreachable; refusing to run so an empty perf comparison cannot be reported as a pass."
}
Write-Host "Preflight: SQL Server $SqlServer (db $DbName) is reachable and warmed."

####################################################################################################
# 3. Inject the VM's SQL Server connection string into the benchmark runner config.
####################################################################################################

$RunnerConfig = Join-Path $RepoRoot "perf-runnerconfig.json"
$env:RUNNER_CONFIG = $RunnerConfig

# The perf app also loads datatypes.json via the DATATYPES_CONFIG env var, falling back to
# "datatypes.json" in the working directory.  Each pass runs from an otherwise-empty
# perf-run-<label> dir, so without this the app throws FileNotFoundException for datatypes.json.
# It needs no per-run modification, so point the env var at the checked-in file directly.
$env:DATATYPES_CONFIG = Join-Path $PerfDir "datatypes.json"

$srcConfig = Join-Path $PerfDir "runnerconfig.jsonc"
$rawConfig = Get-Content $srcConfig -Raw
# Strip // line comments so ConvertFrom-Json accepts the .jsonc content.
$rawConfig = ($rawConfig -split "`n" | ForEach-Object { $_ -replace '(?m)^\s*//.*$', '' }) -join "`n"
$cfg = ConvertFrom-Json $rawConfig

# SqlClient connection-string values may be wrapped in double quotes; doubling any embedded double
# quote lets a password containing ';', '=', spaces or single quotes be parsed as a single literal
# value instead of corrupting the connection string.
$escapedPassword = '"' + ($SqlPassword -replace '"', '""') + '"'
$cfg.ConnectionString = "Server=tcp:$SqlServer,1433;User ID=sa;Password=$escapedPassword;Initial Catalog=$DbName;TrustServerCertificate=True;Encrypt=False;"
# Apply the optional SqlClient behaviour overrides supplied by the pipeline.  An empty value leaves
# the checked-in default untouched; otherwise the flag is forced to the requested boolean so the
# benchmarks run with (and PerfRun.Config records) exactly the requested behaviour.
function Set-CfgBool {
    param($Config, [string]$Name, [string]$Value)
    if (-not [string]::IsNullOrEmpty($Value)) {
        $b = [System.Boolean]::Parse($Value)
        if ($Config.PSObject.Properties.Name -contains $Name) { $Config.$Name = $b }
        else { $Config | Add-Member -NotePropertyName $Name -NotePropertyValue $b }
    }
}
Set-CfgBool $cfg "UseManagedSniOnWindows" $UseManagedSniOnWindows
Set-CfgBool $cfg "UseOptimizedAsyncBehaviour" $UseOptimizedAsyncBehaviour
Set-CfgBool $cfg "UseConnectionPoolV2" $UseConnectionPoolV2
$cfg | ConvertTo-Json -Depth 10 | Set-Content -Path $RunnerConfig -Encoding UTF8
Write-Host "Wrote runner config to $RunnerConfig (Server=tcp:$SqlServer,1433; Initial Catalog=$DbName)"

####################################################################################################
# 4 & 5. Run the benchmarks, pinned to the reserved client CPU set.
#
# Two passes are executed so the pipeline can compare the branch under test against a baseline:
#   * baseline  -> either Microsoft.Data.SqlClient restored from NuGet.org at $BaselineVersion
#                  (ReferenceType=Package + CPM VersionOverride), or - when $BaselineSourceRef is
#                  given instead - the driver built from another git ref of this repository (e.g.
#                  'main', used by the PR perf pipeline).  Skipped when neither is given.
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

# --- Source baseline (-BaselineSourceRef) ---------------------------------------------------------
# Materialises another git ref of THIS repository (e.g. 'main') next to the checkout so the baseline
# pass can build the driver from that source instead of restoring a released package.  The tree is
# placed OUTSIDE the checkout so it is never picked up by the candidate build, by the results copy,
# or by a repo-root glob.
$BaselineSrcDir = Join-Path (Split-Path -Parent $RepoRoot) "sqlclient-perf-baseline-src"

# Prefers the copied checkout's own 'origin' (no extra network round-trip, and it resolves the ref
# exactly as the pipeline's repository does); falls back to a shallow clone of -BaselineRepoUrl when
# the source tree arrived without .git, or when origin is unreachable/needs credentials.
# Returns a hashtable with the baseline perf project path and the '<ref>@<sha>' label.
# --- Non-interactive, time-bounded git ------------------------------------------------------------
# The checkout is copied to the VM WITHOUT credentials (ADO's checkout task defaults to
# persistCredentials:false), so a fetch from an authenticated origin has no way to succeed.  Every
# git command that may touch the network goes through Invoke-GitNetwork so it fails fast and loudly
# instead of blocking the job on a credential prompt.
$GitNetTimeoutSecs = if ($env:GIT_NET_TIMEOUT_SECS) { [int]$env:GIT_NET_TIMEOUT_SECS } else { 300 }

# Invoke-GitNetwork <log-name> <git args>
# Runs git non-interactively under a hard timeout, capturing all output to
# <DiagDir>/git-<log-name>.log.  Returns the exit code (124 if the timeout fired, matching the
# convention GNU timeout uses in the bash script).
#
#   * GIT_TERMINAL_PROMPT=0 turns "needs credentials" into an immediate error rather than a
#     'Username for ...' prompt that waits forever with nothing on the console.
#   * '-c credential.helper=' clears any configured helper (Git Credential Manager is the default on
#     Windows), which would otherwise pop its own prompt and hang in exactly the same way.
#   * The hard kill is a last-resort backstop for any OTHER network stall (DNS, proxy blackhole,
#     ...) so a single git call can never again burn the entire job.
function Invoke-GitNetwork {
    param(
        [Parameter(Mandatory)][string]$LogName,
        [Parameter(Mandatory)][string[]]$GitArgs
    )

    $log = Join-Path $DiagDir "git-$LogName.log"
    $errLog = "$log.err"
    $arguments = @("-c", "credential.helper=") + $GitArgs

    $previousPrompt = $env:GIT_TERMINAL_PROMPT
    $env:GIT_TERMINAL_PROMPT = "0"
    try {
        $proc = Start-Process -FilePath "git" -ArgumentList $arguments -NoNewWindow -PassThru `
            -RedirectStandardOutput $log -RedirectStandardError $errLog
        if (-not $proc.WaitForExit($GitNetTimeoutSecs * 1000)) {
            try { $proc.Kill() } catch { }
            # Give the kill a moment so the redirected handles are released before the log is read.
            [void]$proc.WaitForExit(30 * 1000)
            return 124
        }
        return $proc.ExitCode
    } finally {
        $env:GIT_TERMINAL_PROMPT = $previousPrompt
        # Fold stderr into the single log so callers have one file to report.
        if (Test-Path $errLog) {
            Get-Content $errLog -ErrorAction SilentlyContinue | Add-Content -Path $log
            Remove-Item $errLog -Force -ErrorAction SilentlyContinue
        }
    }
}

# Echo the tail of an Invoke-GitNetwork log so a failure is explained in the build log.
function Write-GitFailure {
    param([Parameter(Mandatory)][string]$LogName, [Parameter(Mandatory)][int]$ExitCode)

    if ($ExitCode -eq 124) {
        Write-Host "  (git $LogName timed out after ${GitNetTimeoutSecs}s)"
    } else {
        Write-Host "  (git $LogName exited $ExitCode)"
    }
    $log = Join-Path $DiagDir "git-$LogName.log"
    if (Test-Path $log) {
        Get-Content $log -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  | $_" }
    }
}

function Initialize-BaselineSource {
    param([Parameter(Mandatory)][string]$Ref)

    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw "git is required for -BaselineSourceRef but was not found on the VM."
    }

    if (Test-Path $BaselineSrcDir) { Remove-Item -Recurse -Force $BaselineSrcDir }

    $acquired = $false
    # git writes progress/diagnostics to stderr, which Windows PowerShell 5.1 promotes to a
    # TERMINATING error under $ErrorActionPreference='Stop' even on success (see Invoke-Native
    # above).  These probes are allowed to fail - that is what the clone fallback is for - so relax
    # the preference and judge them solely by the exit code.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        # '.git' is a directory in a normal clone but a FILE in a git worktree, so Test-Path (which
        # matches either) is what is wanted here.
        if (Test-Path (Join-Path $RepoRoot ".git")) {
            Write-Host "Fetching baseline ref '$Ref' from the checkout's origin ..."
            # Fetch into a private remote-tracking namespace so an existing local branch of the same
            # name (the checkout may itself be on 'main') is never used in place of the fetched ref.
            $exit = Invoke-GitNetwork "fetch" @(
                "-C", $RepoRoot, "fetch", "--no-tags", "--depth", "1", "origin",
                "+refs/heads/${Ref}:refs/remotes/perfbaseline/$Ref")
            if ($exit -eq 0) {
                & git -C $RepoRoot worktree prune 2>&1 | Out-Null
                & git -C $RepoRoot worktree add --detach $BaselineSrcDir "refs/remotes/perfbaseline/$Ref" 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) { $acquired = $true }
            } else {
                Write-GitFailure "fetch" $exit
            }
            if (-not $acquired) {
                Write-Warning "Could not materialise '$Ref' from the checkout's origin; falling back to $BaselineRepoUrl."
            }
        }

        if (-not $acquired) {
            Write-Host "Cloning baseline ref '$Ref' from $BaselineRepoUrl ..."
            if (Test-Path $BaselineSrcDir) { Remove-Item -Recurse -Force $BaselineSrcDir }
            $exit = Invoke-GitNetwork "clone" @(
                "clone", "--quiet", "--depth", "1", "--branch", $Ref, $BaselineRepoUrl, $BaselineSrcDir)
            if ($exit -ne 0) {
                Write-GitFailure "clone" $exit
                throw "Could not obtain baseline ref '$Ref' from either the checkout's origin or $BaselineRepoUrl."
            }
        }

        $sha = & git -C $BaselineSrcDir rev-parse --short HEAD 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sha)) { $sha = "unknown" }
    } finally {
        $ErrorActionPreference = $previousPreference
    }

    $baselineProject = Join-Path $BaselineSrcDir "src\Microsoft.Data.SqlClient\tests\PerformanceTests\Microsoft.Data.SqlClient.PerformanceTests.csproj"
    if (-not (Test-Path $baselineProject)) {
        throw "Baseline source tree at $BaselineSrcDir has no performance test project ($baselineProject)."
    }

    # Label recorded in the comparison output so a run states exactly which baseline commit it used.
    $label = "$Ref@$($sha.Trim())"
    Write-Host "Baseline source ready: $BaselineSrcDir ($label)"

    return @{ Project = $baselineProject; Label = $label }
}

# Convert a CPU range like "16-31" (or a comma list "16,17,18") into an affinity bitmask.
function Get-AffinityMask([string]$cpuSpec) {
    if ([string]::IsNullOrEmpty($cpuSpec)) { return $null }
    # Expand the spec ("16-31", "0,2,4", "0-3,8", ...) into individual CPU indices.
    $cpus = New-Object System.Collections.Generic.List[int]
    foreach ($part in $cpuSpec.Split(",")) {
        if ($part -match '^\s*(\d+)\s*-\s*(\d+)\s*$') {
            for ($c = [int]$Matches[1]; $c -le [int]$Matches[2]; $c++) { $cpus.Add($c) }
        } elseif ($part -match '^\s*(\d+)\s*$') {
            $cpus.Add([int]$Matches[1])
        }
    }
    if ($cpus.Count -eq 0) { return $null }
    # A single-word ProcessorAffinity mask only addresses CPUs 0-63; higher indices require
    # processor-group APIs, so skip pinning rather than build a mask that silently targets the
    # wrong CPUs.
    if (($cpus | Where-Object { $_ -ge 64 }).Count -gt 0) {
        Write-Warning "PERF_CLIENT_CPUS contains a CPU index >= 64; ProcessorAffinity cannot address processor groups, so running without CPU pinning."
        return $null
    }
    [long]$mask = 0
    foreach ($c in $cpus) { $mask = $mask -bor ([long]1 -shl $c) }
    return $mask
}

# Save-CpuTelemetry <label> <before|after>
# §2.11 telemetry snapshot: per-core current clock speed around each measured pass, so a
# drifting/throttling result can be explained after the fact.  Best-effort; never fails the run.
function Save-CpuTelemetry([string]$Label, [string]$When) {
    try {
        $stamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        $speeds = Get-CimInstance Win32_Processor |
            Select-Object DeviceID, CurrentClockSpeed, MaxClockSpeed | Format-Table -AutoSize | Out-String
        "$stamp`n$speeds" | Out-File -FilePath (Join-Path $DiagDir "cpu-$Label-$When.txt") -Encoding UTF8
    } catch { }
}

# Get-BenchmarkResultCount <dir>
# Counts the BenchmarkDotNet results recorded under <dir> (the "Benchmarks" array of every
# *-report-full.json).  Used to fail loud when a pass produced nothing.
function Get-BenchmarkResultCount([string]$Root) {
    $py = @'
import glob, json, os, sys
root = sys.argv[1]
total = 0
for f in glob.glob(os.path.join(root, "**", "*-report-full.json"), recursive=True):
    try:
        with open(f, encoding="utf-8-sig") as fh:
            total += len(json.load(fh).get("Benchmarks", []))
    except Exception:
        pass
print(total)
'@
    # Relax Stop: this native python call tolerates failure (returns 0 below); its stderr must not
    # promote to a terminating error under Windows PowerShell 5.1.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = $py | python3 - $Root
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($out)) { return 0 }
    return [int]($out.Trim())
}

# Runs one benchmark pass (build + run pinned to PERF_CLIENT_CPUS) from $Project and collects its
# artifacts into results\<label>. $ExtraArgs are appended to both the build and run invocations.
# $Project is the candidate's perf project for every pass except a source-baseline pass, which
# builds the baseline ref's own perf project.
function Invoke-PerfPass([string]$Label, [string]$Project, [string[]]$ExtraArgs) {
    $runDir = Join-Path $RepoRoot "perf-run-$Label"
    if (Test-Path $runDir) { Remove-Item -Recurse -Force $runDir }
    New-Item -ItemType Directory -Force -Path $runDir | Out-Null

    Write-Host "------------------------------------------------------------------"
    Write-Host " Pass: $Label"
    Write-Host "   Project   : $Project"
    Write-Host "   Extra args: $($ExtraArgs -join ' ')"
    Write-Host "------------------------------------------------------------------"

    Write-Host "Building performance tests ($Configuration, $Framework) for '$Label' ..."
    Invoke-Native { dotnet build $Project -c $Configuration -f $Framework --nologo -v minimal @ExtraArgs } "Build failed for '$Label'"

    Push-Location $runDir
    try {
        $runArgs = @("run", "--project", $Project, "-c", $Configuration, "-f", $Framework, "--no-build") + $ExtraArgs
        Write-Host "Starting benchmarks ($Label): dotnet $($runArgs -join ' ')"
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "dotnet"
        # Windows PowerShell 5.1 / .NET Framework has no ProcessStartInfo.ArgumentList, so build a
        # quoted argument string for .Arguments (quoting any arg that contains whitespace).
        $psi.Arguments = ($runArgs | ForEach-Object {
            if ($_ -match '\s') { '"' + $_ + '"' } else { $_ }
        }) -join ' '
        $psi.UseShellExecute = $false
        $psi.WorkingDirectory = $runDir

        $proc = [System.Diagnostics.Process]::Start($psi)

        $mask = Get-AffinityMask $env:PERF_CLIENT_CPUS
        # Use a non-zero check, not '-gt 0': a mask that pins CPU 63 sets the [long] sign bit and
        # is therefore negative, yet is still a valid ProcessorAffinity value.
        if ($null -ne $mask -and $mask -ne 0) {
            try {
                $proc.ProcessorAffinity = [System.IntPtr]$mask
                Write-Host "Pinned benchmark client (PID $($proc.Id)) to CPUs $($env:PERF_CLIENT_CPUS) (mask 0x$($mask.ToString('X')))."
            } catch {
                Write-Warning "Failed to set ProcessorAffinity: $_"
            }
        } else {
            Write-Warning "PERF_CLIENT_CPUS unset; running without CPU pinning."
        }

        Save-CpuTelemetry $Label "before"
        $proc.WaitForExit()
        Save-CpuTelemetry $Label "after"
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

    # §2.10 Fail loud: a pass that produced zero benchmark results (server dropped, all benches
    # errored, exporter disabled) must not flow through to an empty comparison that reads green.
    $nresults = Get-BenchmarkResultCount $dest
    Write-Host "Pass '$Label' produced $nresults benchmark result(s)."
    if ($nresults -eq 0) {
        throw "Pass '$Label' produced no benchmark results; failing the run (a broken benchmark pass must not be reported as a pass)."
    }
}

# Build-Variant <label> <project> <extra build args...>
# Builds the PerformanceTests app once into perf-build-<label> so the interleaved orchestrator can
# invoke it repeatedly without rebuilding.  Distinct output dirs are mandatory (both variants would
# otherwise share the project's default bin path).
function Build-Variant {
    param([string]$Label, [string]$Project, [string[]]$ExtraArgs = @())
    $outDir = Join-Path $RepoRoot "perf-build-$Label"
    if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    Write-Host "Building '$Label' variant ($Configuration, $Framework) from $Project into $outDir ..."
    $buildArgs = @("build", $Project, "-c", $Configuration, "-f", $Framework, "--nologo", "-v", "minimal", "-o", $outDir) + $ExtraArgs
    # Route the build output to the host/transcript (Out-Host) so it is visible AND does not land on
    # this function's success stream: '$dir = Build-Variant ...' otherwise captures the entire build
    # log into the return value, hiding build errors and corrupting the returned exe-dir path.
    Invoke-Native { dotnet @buildArgs } "Build failed for '$Label' variant" | Out-Host
    return $outDir
}

# --- Resolve how the baseline pass is produced ----------------------------------------------------
# Both baseline flavours end up as "a perf project plus build args"; resolving them once here keeps
# the interleaved and sequential paths below identical for the package and source baselines.
$BaselineLabel = ""
$BaselineProject = $PerfProject
$BaselineBuildArgs = @()

if (-not [string]::IsNullOrEmpty($BaselineVersion)) {
    # Released package baseline: candidate's perf project, MDS swapped to a NuGet package reference.
    Write-BaselineNuGetConfig
    $BaselineLabel = $BaselineVersion
    $BaselineBuildArgs = @(
        "-p:ReferenceType=Package",
        "-p:MdsPackageVersion=$BaselineVersion",
        "-p:RestoreConfigFile=$BaselineNuGetConfig"
    )
} elseif (-not [string]::IsNullOrEmpty($BaselineSourceRef)) {
    # Source baseline: build the baseline ref's OWN perf project so the driver under measurement is
    # that ref's source (ProjectReference), exactly as the candidate pass builds this branch's.
    $baselineSource = Initialize-BaselineSource -Ref $BaselineSourceRef
    $BaselineLabel = $baselineSource.Label
    $BaselineProject = $baselineSource.Project
}

# Record the resolved baseline label (for a source baseline this is '<ref>@<sha>') in the results
# tree.  The results directory is copied back to the agent, so a post-test step can read this and
# tag the build with the exact baseline that was measured - something the pipeline itself cannot do,
# since the SHA is only known once the ref has been resolved here on the VM.
if (-not [string]::IsNullOrEmpty($BaselineLabel)) {
    Set-Content -Path (Join-Path $ResultsDir "baseline-label.txt") -Value $BaselineLabel -Encoding ascii
}

if ((-not [string]::IsNullOrEmpty($BaselineLabel)) -and ($RunMode -eq "interleaved")) {
    ####################################################################################################
    # Interleaved + best-of-N (wiki 339 §2.2/§2.3/§2.6).  Build both variants once, then let the
    # orchestrator run one unit at a time (baseline then candidate) and confirm any flagged
    # regression across N passes before it counts toward the gate.
    ####################################################################################################
    $baselineExeDir = Build-Variant "baseline" $BaselineProject $BaselineBuildArgs
    $currentExeDir = Build-Variant "current" $PerfProject @()

    $interleaveArgs = @(
        "--baseline-exe-dir", $baselineExeDir,
        "--current-exe-dir", $currentExeDir,
        "--assembly", "PerformanceTests.dll",
        "--results-dir", $ResultsDir,
        "--threshold", $RegressionThreshold,
        "--reps", $ConfirmationRuns,
        "--baseline-version", $BaselineLabel,
        "--client-cpus", "$($env:PERF_CLIENT_CPUS)"
    )
    if ($FailOnRegression) {
        Write-Host "Regression gate ENABLED: a CONFIRMED candidate-slower regression (> $RegressionThreshold%) will fail the run."
        $interleaveArgs += "--fail-on-regression"
    }
    Write-Host "Running interleaved benchmarks (best-of-$ConfirmationRuns) ..."
    Invoke-Native { python3 (Join-Path $ScriptDir "interleave_perf.py") @interleaveArgs } "Interleaved run failed"

} elseif (-not [string]::IsNullOrEmpty($BaselineLabel)) {
    # --- Legacy sequential path: full baseline pass, then full candidate pass, then compare -------
    Invoke-PerfPass "baseline" $BaselineProject $BaselineBuildArgs
    Invoke-PerfPass "current" $PerfProject @()

    Write-Host "Comparing current branch against baseline $BaselineLabel ..."
    $comparisonDir = Join-Path $ResultsDir "comparison"
    New-Item -ItemType Directory -Force -Path $comparisonDir | Out-Null
    $compareArgs = @(
        "--baseline-dir", (Join-Path $ResultsDir "baseline"),
        "--current-dir", (Join-Path $ResultsDir "current"),
        "--baseline-version", $BaselineLabel,
        "--threshold", $RegressionThreshold,
        "--out-md", (Join-Path $comparisonDir "comparison.md"),
        "--out-json", (Join-Path $comparisonDir "comparison.json")
    )
    # §3 gate: only a candidate-slower regression fails, and only when explicitly enabled.
    if ($FailOnRegression) {
        Write-Host "Regression gate ENABLED: a candidate-slower regression (> $RegressionThreshold%) will fail the run."
        $compareArgs += "--fail-on-regression"
    }
    Invoke-Native { python3 (Join-Path $ScriptDir "compare_perf.py") @compareArgs } "Comparison failed"
    # Surface the comparison as the top-level run summary (collect-results.yml attaches results\*.md).
    Copy-Item -Force (Join-Path $comparisonDir "comparison.md") (Join-Path $ResultsDir "summary.md")

} else {
    # --- No baseline: current-only run (no comparison) --------------------------------------------
    Write-Host "Neither -BaselineVersion nor -BaselineSourceRef supplied; running current only (no comparison)."
    Invoke-PerfPass "current" $PerfProject @()
}

Write-Host "Collected results:"
Get-ChildItem -Recurse -File $ResultsDir | ForEach-Object { $_.FullName } | Sort-Object

Write-Host "=================================================================="
Write-Host " Performance run complete."
Write-Host "=================================================================="

<#
.SYNOPSIS
    Verifies that symbols (PDBs) for a single DLL are available on a symbol server.

.DESCRIPTION
    This script is called by the validate-symbols-job.yml Azure Pipelines job,
    once per package per symbol server.  It:
      1. Locates the .nupkg file in the downloaded artifact directory.
      2. Extracts the package contents (skipped if already extracted).
      3. Runs symchk.exe to verify that matching PDBs are available on the
         specified symbol server.

    The script exits with a non-zero exit code if verification fails.

.PARAMETER ArtifactPath
    The directory containing the downloaded pipeline artifact (.nupkg files).

.PARAMETER ExtractPath
    The directory where the package will be extracted.

.PARAMETER PackageName
    The NuGet package name prefix used to locate the .nupkg file
    (e.g. "Microsoft.Data.SqlClient").

.PARAMETER DllPath
    The relative path to the DLL inside the extracted package
    (e.g. "lib\net8.0\Microsoft.Data.SqlClient.dll").

.PARAMETER SymbolServerUrl
    The symbol server URL that symchk can query
    (e.g. "https://msdl.microsoft.com/download/symbols").

.PARAMETER SymbolServerName
    A friendly display name for the symbol server, used in log output.

.PARAMETER MaxRetries
    Maximum number of attempts when symbols are not yet available.  The
    first attempt runs immediately; subsequent attempts wait
    RetryIntervalSeconds between them.  Defaults to 10 (~5 minutes total
    with default interval).

.PARAMETER RetryIntervalSeconds
    Seconds to wait between retry attempts (default 30).

.EXAMPLE
    .\validate-symbols.ps1 `
        -ArtifactPath    "C:\agent\_work\1\drop_SqlClient" `
        -ExtractPath     "C:\agent\_work\1\s\symchk_packages\Microsoft.Data.SqlClient" `
        -PackageName     "Microsoft.Data.SqlClient" `
        -DllPath         "lib\net8.0\Microsoft.Data.SqlClient.dll" `
        -SymbolServerUrl "https://msdl.microsoft.com/download/symbols" `
        -SymbolServerName "MSDL (Public)"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ArtifactPath,

    [Parameter(Mandatory)]
    [string]$ExtractPath,

    [Parameter(Mandatory)]
    [string]$PackageName,

    [Parameter(Mandatory)]
    [string]$DllPath,

    [Parameter(Mandatory)]
    [string]$SymbolServerUrl,

    [Parameter(Mandatory)]
    [string]$SymbolServerName,

    [int]$MaxRetries = 10,

    [int]$RetryIntervalSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Extract the package (skip if already done) ────────────────────────────────

$dllFullPath = Join-Path $ExtractPath $DllPath

if (-not (Test-Path $dllFullPath)) {
    Write-Host "Extracting $PackageName"

    New-Item -ItemType Directory -Force -Path $ExtractPath | Out-Null

    $nupkg = Get-ChildItem -Path $ArtifactPath -Filter "$PackageName.*.nupkg" `
        | Where-Object { $_.Name -notlike '*.snupkg' } `
        | Select-Object -First 1

    if (-not $nupkg) {
        Write-Host "##vso[task.logissue type=error]No $PackageName nupkg found in $ArtifactPath"
        exit 1
    }

    Write-Host "Found: $($nupkg.FullName)"

    $zipPath = Join-Path $ExtractPath 'package.zip'
    Copy-Item $nupkg.FullName $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $ExtractPath -Force
    Remove-Item $zipPath
}

if (-not (Test-Path $dllFullPath)) {
    Write-Host "##vso[task.logissue type=error]DLL not found after extraction: $dllFullPath"
    exit 1
}

# ── Locate symchk.exe ────────────────────────────────────────────────────────

$symchkCandidates = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\Debuggers\x64\symchk.exe"
    "${env:ProgramFiles}\Windows Kits\10\Debuggers\x64\symchk.exe"
)

$symchkPath = $null
foreach ($candidate in $symchkCandidates) {
    if (Test-Path $candidate) {
        $symchkPath = $candidate
        break
    }
}

if (-not $symchkPath) {
    Write-Host "##vso[task.logissue type=error]symchk.exe not found. Ensure Debugging Tools for Windows are installed."
    exit 1
}

# ── Verify symbols (with retries for publishing latency) ──────────────────────

$dllLeaf = Split-Path $dllFullPath -Leaf

Write-Host "Verifying symbols for $dllLeaf on $SymbolServerName ($SymbolServerUrl)"
Write-Host "Using symchk: $symchkPath"
Write-Host "Max attempts: $MaxRetries, interval: ${RetryIntervalSeconds}s"

$symchkArgs = @(
    $dllFullPath,
    "/s", "srv*$SymbolServerUrl",
    "/os"
)

for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
    Write-Host "Attempt $attempt of $MaxRetries — running: symchk $($symchkArgs -join ' ')"
    $output = & $symchkPath @symchkArgs 2>&1 | Out-String
    $symchkExit = $LASTEXITCODE

    Write-Host $output

    $passed = ($symchkExit -eq 0) -and
             ($output -match "FAILED files = 0") -and
             ($output -match "PASSED \+ IGNORED files = [1-9]")

    if ($passed) {
        Write-Host "Symbols verified successfully for $dllLeaf on $SymbolServerName"
        exit 0
    }

    if ($attempt -lt $MaxRetries) {
        Write-Host "Symbols not yet available. Retrying in $RetryIntervalSeconds seconds..."
        Start-Sleep -Seconds $RetryIntervalSeconds
    }
}

Write-Host "##vso[task.logissue type=error]symchk could not verify symbols for $dllLeaf on $SymbolServerName after $MaxRetries attempts."
exit 1

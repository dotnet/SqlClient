# Runs the net47 build of this harness inside a .NET Framework Windows container,
# so the tests bind the container's OLD in-box System.Data.dll (e.g. 4.7.4081.0)
# instead of the host's. The framework container has no dotnet CLI, so we run the
# tests with xunit.console.exe.
#
# Examples:
#   # in-process tests only (no SQL Server needed):
#   .\run-in-container.ps1
#
#   # include the live MARS + stress tests against a host SQL Server:
#   $env:SNICLOSE_CONNSTR = "Server=127.0.0.1,1434;Database=master;User ID=sa;Password=***;TrustServerCertificate=true"
#   .\run-in-container.ps1
#
#   # a different framework image / version:
#   .\run-in-container.ps1 -Image mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019

[CmdletBinding()]
param(
    [string]$Framework = 'net47',
    [string]$Image = 'mcr.microsoft.com/dotnet/framework/runtime:4.7-windowsservercore-ltsc2016',
    [string]$ConnectionString = $env:SNICLOSE_CONNSTR,
    # Container-reachable host IP that the loopback SQL Server is bridged to.
    # Defaults to the Docker NAT gateway (the host's 'vEthernet (nat)' adapter).
    [string]$SqlHost,
    [string]$RunnerVersion = '2.9.3',
    # Constrain the hyperv container VM to this many processors (0 = unconstrained).
    # The ICM environment had 4 procs; races can be CPU-count sensitive.
    [int]$Cpus = 0,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$projDir = $PSScriptRoot
$proj = Join-Path $projDir 'SniCloseLegacyRepro.csproj'

# --- Build the net47 output (host-side; independent of the docker engine) ------
if (-not $SkipBuild) {
    Write-Host "Building $Framework ..." -ForegroundColor Cyan
    dotnet build $proj -c Debug -f $Framework | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}

$outDir = Join-Path $projDir "bin\Debug\$Framework"
if (-not (Test-Path (Join-Path $outDir 'SniCloseLegacyRepro.dll'))) {
    throw "Build output not found: $outDir (build first, or omit -SkipBuild)."
}

# --- Stage xunit.console.exe next to the test assembly -------------------------
# The framework container has no dotnet CLI, so run tests with the console runner.
$gp = (dotnet nuget locals global-packages --list) -replace '^.*?:\s*', ''
$runnerTools = Join-Path $gp "xunit.runner.console\$RunnerVersion\tools\$Framework"
if (-not (Test-Path $runnerTools)) {
    $runnerTools = Join-Path $gp "xunit.runner.console\$RunnerVersion\tools\net472"
}
if (-not (Test-Path (Join-Path $runnerTools 'xunit.console.exe'))) {
    throw "xunit.console.exe not found under $runnerTools. Is xunit.runner.console $RunnerVersion restored?"
}
Copy-Item (Join-Path $runnerTools '*') $outDir -Force

# --- Ensure Docker is on the Windows engine -----------------------------------
$null = docker context use desktop-windows 2>$null
$serverOs = docker version --format '{{.Server.Os}}' 2>$null
if ($serverOs -ne 'windows') {
    throw "Docker is not on the Windows engine (Server OS='$serverOs'). Switch with: & 'C:\Program Files\Docker\Docker\DockerCli.exe' -SwitchWindowsEngine"
}

# --- Build the container args --------------------------------------------------
# A Windows container cannot reach the host's loopback SQL Server directly, and
# host.docker.internal is not injected for hyperv-isolated Windows containers.
# Instead we target the Docker NAT gateway IP (the host's 'vEthernet (nat)'
# adapter); a one-time elevated host portproxy must bridge that IP to loopback:
#   netsh interface portproxy add v4tov4 listenaddress=<natIP> listenport=1434 `
#     connectaddress=127.0.0.1 connectport=1434
$envArgs = @()
if ($ConnectionString) {
    if (-not $SqlHost) {
        $SqlHost = Get-NetIPAddress -InterfaceAlias 'vEthernet (nat)' -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty IPAddress
    }
    if (-not $SqlHost) {
        throw "Could not determine a container-reachable host IP; pass -SqlHost explicitly."
    }
    $cs = $ConnectionString `
        -replace '127\.0\.0\.1', $SqlHost `
        -replace '(?i)(?<=[=;\s])localhost(?=[,;]|$)', $SqlHost
    $envArgs = @('-e', "SNICLOSE_CONNSTR=$cs")
    Write-Host "Live tests enabled (SQL Server via $SqlHost; requires a host portproxy to 127.0.0.1)." -ForegroundColor Cyan
}
else {
    Write-Host "No connection string: live MARS + stress tests will be skipped." -ForegroundColor Yellow
}

Write-Host "Running tests in $Image (isolation=hyperv) ..." -ForegroundColor Cyan
$cpuArgs = @()
if ($Cpus -gt 0) {
    $cpuArgs = @('--cpu-count', "$Cpus")
    Write-Host "Container VM constrained to $Cpus processor(s)." -ForegroundColor Cyan
}
docker run --rm --isolation=hyperv @cpuArgs @envArgs -v "${outDir}:C:\app" -w 'C:\app' $Image `
    C:\app\xunit.console.exe C:\app\SniCloseLegacyRepro.dll -parallel none

$code = $LASTEXITCODE
Write-Host "xunit.console.exe exit code: $code" -ForegroundColor $(if ($code -eq 0) { 'Green' } else { 'Red' })
exit $code

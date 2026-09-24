<#
.SYNOPSIS
    Reports whether TCP endpoints are reachable, and why they are not.

.DESCRIPTION
    For each host the probe separates the layers at which a connection can fail, because
    each implies a different owner:

      - DNS resolution failing   -> name resolution or split-horizon DNS.
      - DNS resolving but TCP connect being refused or denied -> an egress policy or
        firewall between the agent and the destination.
      - TCP succeeding but TLS or HTTP failing -> an interception proxy or the service.

    A socket error code is reported verbatim (for example AccessDenied, which is the
    EACCES errno an egress block produces) so the failure cannot be confused with an
    application-level authorization error of the same wording. The error identifies what
    happened; it does not by itself identify who owns it.

    An unreachable endpoint is a finding, not a script failure: probing continues through
    the remaining hosts and the script exits 0. Invalid invocation is different and does
    throw, for example when no usable hostname survives parsing.

.PARAMETER HostName
    One or more hostnames to probe.

.PARAMETER Port
    TCP port to connect to. Defaults to 443.

.PARAMETER TimeoutSeconds
    Per-connection timeout. Defaults to 10.

.EXAMPLE
    .\test-endpoint-reachability.ps1 -HostName symbolrequestppe.trafficmanager.net,symbolrequestprod.trafficmanager.net

    Probes both symbol servers and prints a comparison summary.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Hostnames to probe.")]
    [ValidateNotNullOrEmpty()]
    [string[]]$HostName,

    [Parameter(Mandatory = $false, HelpMessage = "TCP port to connect to.")]
    [int]$Port = 443,

    [Parameter(Mandatory = $false, HelpMessage = "Per-connection timeout in seconds.")]
    [int]$TimeoutSeconds = 10
)

Set-StrictMode -Version Latest
# A failing endpoint is diagnostic output and must be reported rather than thrown, so that
# the remaining hosts are still probed. Invalid invocation, such as a host list that parses
# to nothing, still throws.
$ErrorActionPreference = 'Continue'

# When invoked through 'pwsh -File' (which is how pipeline tasks run a script path) every
# argument arrives as a plain string, so a comma-separated list is not split into an array.
# Normalising here keeps both invocation styles working.
$HostName = @(
    $HostName |
        ForEach-Object { $_ -split ',' } |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
)

if ($HostName.Count -eq 0) {
    throw "No hostnames were supplied after parsing the -HostName argument."
}

function Resolve-ExceptionDetail {
    param($Exception)

    $current = $Exception
    # TcpClient surfaces connection failures wrapped in AggregateException.
    while ($null -ne $current -and
           ($current -is [System.AggregateException] -or
            ($current -isnot [System.Net.Sockets.SocketException] -and $null -ne $current.InnerException))) {
        if ($null -eq $current.InnerException) { break }
        $current = $current.InnerException
    }

    if ($current -is [System.Net.Sockets.SocketException]) {
        return "$($current.SocketErrorCode) (errno $($current.NativeErrorCode)): $($current.Message)"
    }
    if ($null -ne $current) {
        return "$($current.GetType().Name): $($current.Message)"
    }
    return 'unknown failure'
}

function Test-TcpConnect {
    param([string]$Target, [int]$TargetPort, [int]$Timeout)

    $client = [System.Net.Sockets.TcpClient]::new()
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $connect = $client.ConnectAsync($Target, $TargetPort)
        if (-not $connect.Wait([TimeSpan]::FromSeconds($Timeout))) {
            return [pscustomobject]@{ Success = $false; Detail = "timed out after ${Timeout}s"; ElapsedMs = $stopwatch.ElapsedMilliseconds }
        }
        if ($connect.IsFaulted) {
            return [pscustomobject]@{ Success = $false; Detail = (Resolve-ExceptionDetail $connect.Exception); ElapsedMs = $stopwatch.ElapsedMilliseconds }
        }
        return [pscustomobject]@{ Success = $true; Detail = 'connected'; ElapsedMs = $stopwatch.ElapsedMilliseconds }
    } catch {
        return [pscustomobject]@{ Success = $false; Detail = (Resolve-ExceptionDetail $_.Exception); ElapsedMs = $stopwatch.ElapsedMilliseconds }
    } finally {
        $stopwatch.Stop()
        $client.Dispose()
    }
}

function Test-Endpoint {
    param([string]$Target, [int]$TargetPort, [int]$Timeout)

    Write-Host ""
    Write-Host "=== ${Target}:${TargetPort} ==="

    $addresses = @()
    try {
        $addresses = @([System.Net.Dns]::GetHostAddresses($Target))
        Write-Host "DNS      : $($addresses.IPAddressToString -join ', ')"
    } catch {
        Write-Host "DNS      : FAILED - $(Resolve-ExceptionDetail $_.Exception)"
        Write-Host "Verdict  : name resolution failed; nothing was attempted at the TCP layer."
        return [pscustomobject]@{ HostName = $Target; Dns = $false; Tcp = $false; Detail = 'DNS failure' }
    }

    # Connect by name first, which is what the application itself does.
    $byName = Test-TcpConnect -Target $Target -TargetPort $TargetPort -Timeout $Timeout
    Write-Host "TCP      : by name -> $(if ($byName.Success) { 'connected' } else { 'FAILED' }) in $($byName.ElapsedMs) ms - $($byName.Detail)"

    # Then each resolved address, which distinguishes a host-based policy from one that
    # targets particular address ranges.
    foreach ($address in $addresses) {
        $ip = $address.IPAddressToString
        $byIp = Test-TcpConnect -Target $ip -TargetPort $TargetPort -Timeout $Timeout
        Write-Host "TCP      : ${ip} -> $(if ($byIp.Success) { 'connected' } else { 'FAILED' }) in $($byIp.ElapsedMs) ms - $($byIp.Detail)"
    }

    if ($byName.Success) {
        Write-Host "Verdict  : reachable at the TCP layer."
    } else {
        Write-Host "Verdict  : DNS resolved but the TCP connection did not succeed, so no HTTP exchange"
        Write-Host "           took place and the service never received a request. The socket error"
        Write-Host "           reported above identifies the failure; different errors have different"
        Write-Host "           owners, so diagnose from that error rather than assuming a cause."
    }

    return [pscustomobject]@{ HostName = $Target; Dns = $true; Tcp = $byName.Success; Detail = $byName.Detail }
}

Write-Host "=== Endpoint Reachability Probe ==="
Write-Host "Port           : ${Port}"
Write-Host "Timeout        : ${TimeoutSeconds}s"
Write-Host "Hosts          : $($HostName -join ', ')"
Write-Host "PowerShell     : $($PSVersionTable.PSVersion)"
Write-Host "OS             : $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription.Trim())"

$results = foreach ($target in $HostName) {
    Test-Endpoint -Target $target -TargetPort $Port -Timeout $TimeoutSeconds
}

Write-Host ""
Write-Host "=== Summary ==="
foreach ($result in $results) {
    $status = if ($result.Tcp) { 'REACHABLE  ' } elseif (-not $result.Dns) { 'DNS FAILURE' } else { 'BLOCKED    ' }
    Write-Host ("  {0} {1} - {2}" -f $status, $result.HostName, $result.Detail)
}

$reachable = @($results | Where-Object { $_.Tcp })
$blocked = @($results | Where-Object { -not $_.Tcp })

if ($blocked.Count -gt 0 -and $reachable.Count -gt 0) {
    Write-Host ""
    Write-Host "Some hosts are reachable from this agent and others are not. Because the agent,"
    Write-Host "identity and network path are otherwise identical, the difference is attributable"
    Write-Host "to the destination host rather than to the caller."
}

Write-Host "==============="

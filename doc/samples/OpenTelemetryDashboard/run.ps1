# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Local-only SqlClient OpenTelemetry dashboard demo.
#   .\run.ps1                 start the stack, build and run the workload (180 s)
#   .\run.ps1 -Seconds 600    run the workload longer
#   .\run.ps1 -Action Workload  rerun only the workload against a running stack
#   .\run.ps1 -Action Down    stop the stack and remove its containers, network and volumes
# The SQL password is generated in process memory and passed only through the environment.
# It is visible to local Docker administrators (docker inspect); this is a local demo only.
[CmdletBinding()]
param(
    [ValidateSet('Up', 'Workload', 'Down')] [string] $Action = 'Up',
    [int] $Seconds = 180
)

$ErrorActionPreference = 'Stop'
$Here = $PSScriptRoot
$Project = 'sqlclient-otel-demo'
$Compose = @('compose', '--project-name', $Project, '--env-file', (Join-Path $Here 'empty.env'),
    '-f', (Join-Path $Here 'compose.yaml'))

function Invoke-Docker([string[]] $Arguments) {
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw "docker $($Arguments -join ' ') failed ($LASTEXITCODE)." }
}

function Wait-Until([scriptblock] $Test, [int] $TimeoutSeconds, [string] $What) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try { if (& $Test) { return } } catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $What."
}

function Get-SqlContainer {
    $id = & docker @Compose ps -q sql
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($id)) { throw 'The sql service is not running.' }
    return $id.Trim()
}

try {
    if ($Action -ne 'Up') {
        # Compose requires the variables to be set, but existing containers keep their values.
        $env:SQLCLIENT_DEMO_SQL_PASSWORD = 'unused'
        $env:SQLCLIENT_DEMO_OWNER_ID = 'unused'
    }
    if ($Action -eq 'Down') {
        Invoke-Docker ($Compose + @('down', '--volumes', '--remove-orphans'))
        return
    }

    if ($Action -eq 'Up') {
        $env:SQLCLIENT_DEMO_SQL_PASSWORD = 'D3mo!' + [guid]::NewGuid().ToString('N')
        $env:SQLCLIENT_DEMO_OWNER_ID = [guid]::NewGuid().ToString('N')
        Invoke-Docker ($Compose + @('up', '-d'))
        Wait-Until { (Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:13133').StatusCode -eq 200 } 120 'collector health'
        Wait-Until { (Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:9090/-/ready').StatusCode -eq 200 } 120 'Prometheus'
        Wait-Until { (Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:16686/api/services').StatusCode -eq 200 } 120 'Jaeger'
        Wait-Until { (Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:3000/api/health').StatusCode -eq 200 } 120 'Grafana'
    }

    $sql = Get-SqlContainer
    # Reads the password back from the owned container, so -Action Workload needs no file.
    $env:SQLCLIENT_DEMO_PASSWORD = (& docker exec $sql printenv MSSQL_SA_PASSWORD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($env:SQLCLIENT_DEMO_PASSWORD)) {
        throw 'The SQL password is unavailable.'
    }
    Wait-Until {
        & docker exec $sql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa `
            -P $env:SQLCLIENT_DEMO_PASSWORD -Q 'SELECT 1' -b *> $null
        $LASTEXITCODE -eq 0
    } 180 'SQL Server login readiness'

    $env:OTEL_SERVICE_NAME = 'sqlclient-poc'
    $workload = Join-Path $Here 'Workload\OpenTelemetryDashboardWorkload.csproj'
    & dotnet run --project $workload -c Release -- $Seconds
    if ($LASTEXITCODE -ne 0) { throw "Workload failed ($LASTEXITCODE)." }

    Write-Host ''
    Write-Host 'Grafana:    http://127.0.0.1:3000/d/sqlclient-user-endpoint'
    Write-Host 'Jaeger:     http://127.0.0.1:16686/search?service=sqlclient-poc'
    Write-Host 'Prometheus: http://127.0.0.1:9090'
    Write-Host "Stop with:  .\run.ps1 -Action Down"
}
finally {
    $env:SQLCLIENT_DEMO_PASSWORD = $null
    $env:SQLCLIENT_DEMO_SQL_PASSWORD = $null
}

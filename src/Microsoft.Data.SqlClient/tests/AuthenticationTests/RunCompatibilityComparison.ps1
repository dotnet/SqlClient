# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param(
    [string]$BaselineVersion = '7.1.2',
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$project = Join-Path $PSScriptRoot 'CompatibilityTest/CompatibilityTest.csproj'
if (!$ReportPath) {
    $ReportPath = Join-Path $repoRoot 'artifacts/AuthenticationCompatibility/comparison.json'
}
$ReportPath = [System.IO.Path]::GetFullPath($ReportPath)
$currentCommit = & git -C $repoRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not identify the current comparison commit.' }
$temporary = Join-Path ([System.IO.Path]::GetTempPath()) (
    'sqlclient-compatibility-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
$sharedScenarios = @('configured', 'legacy', 'application-registration')
$scenarios = $sharedScenarios + @(
    'initializer-registration', 'bad-provider', 'bad-initializer', 'bad-unsupported')
$runs = @{}
$baselinePackages = [ordered]@{}

try {
    foreach ($label in @('baseline', 'current')) {
        [string[]]$options = if ($label -eq 'baseline') {
            @('-p:ReferenceType=Package', "-p:CompatibilityPackageVersion=$BaselineVersion")
        } else {
            @('-p:ReferenceType=Project')
        }
        & dotnet build $project -c Release -v:minimal @options
        if ($LASTEXITCODE -ne 0) { throw "Compatibility consumer build failed: $label" }
        $output = & dotnet msbuild $project -nologo -p:Configuration=Release `
            -getProperty:TargetDir @options
        if ($LASTEXITCODE -ne 0 -or !$output) {
            throw "Could not evaluate compatibility output: $label"
        }
        $output = "$output".Trim()
        if ($label -eq 'baseline') {
            $assetsPath = Join-Path $PSScriptRoot 'CompatibilityTest/obj/project.assets.json'
            $assets = Get-Content $assetsPath -Raw | ConvertFrom-Json
            foreach ($id in @('Microsoft.Data.SqlClient',
                'Microsoft.Data.SqlClient.Extensions.Azure',
                'Microsoft.Data.SqlClient.Extensions.Abstractions',
                'Microsoft.Data.SqlClient.Internal.Logging')) {
                $library = $assets.libraries.PSObject.Properties["$id/$BaselineVersion"]
                if (!$library -or $library.Value.type -ne 'package') {
                    throw "Baseline did not restore $id at exactly $BaselineVersion."
                }
                $baselinePackages[$id] = $BaselineVersion
            }
        }
        $results = @()
        foreach ($scenario in $scenarios) {
            $directory = Join-Path $temporary "$label/$scenario"
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            Copy-Item (Join-Path $output '*') $directory -Recurse
            $app = Join-Path $directory 'AuthenticationConfigurationTest.dll'
            $configPath = "$app.config"
            $generated = [System.Xml.XmlDocument]::new()
            $generated.Load($configPath)
            $runtime = $generated.SelectSingleNode('/configuration/runtime')
            $config = [System.Xml.XmlDocument]::new()
            if ($scenario -eq 'application-registration') {
                $config.LoadXml('<configuration />')
            } else {
                $configName = if ($scenario -in @('configured', 'initializer-registration')) {
                    'app.config'
                } else { "$scenario.config" }
                $config.Load((Join-Path $directory $configName))
            }
            if ($runtime) {
                $config.DocumentElement.AppendChild($config.ImportNode($runtime, $true)) | Out-Null
            }
            $config.Save($configPath)
            $start = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
            $start.ArgumentList.Add($app)
            $start.ArgumentList.Add($scenario)
            $start.WorkingDirectory = $directory
            $start.UseShellExecute = $false
            $start.RedirectStandardOutput = $true
            $start.RedirectStandardError = $true
            $process = [System.Diagnostics.Process]::new()
            $process.StartInfo = $start
            try {
                if (!$process.Start()) { throw "Could not start $label/$scenario." }
                $stdout = $process.StandardOutput.ReadToEndAsync()
                $stderr = $process.StandardError.ReadToEndAsync()
                if (!$process.WaitForExit(60000)) {
                    $process.Kill($true)
                    $process.WaitForExit()
                    throw "Compatibility scenario timed out: $label/$scenario"
                }
                $text = $stdout.GetAwaiter().GetResult()
                $diagnostics = $stderr.GetAwaiter().GetResult()
                if ($process.ExitCode -ne 0) {
                    throw "Compatibility scenario failed: $label/$scenario`n$text`n$diagnostics"
                }
                $observations = @($text -split '\r?\n' | ForEach-Object {
                    if ($_.StartsWith('OBSERVATION:')) {
                        $_.Substring('OBSERVATION:'.Length) | ConvertFrom-Json
                    }
                })
                if (!$observations.Count) { throw "No observations from $label/$scenario." }
                $results += [pscustomobject]@{
                    Scenario = $scenario
                    Observations = $observations
                    Output = $text
                    Diagnostics = $diagnostics
                }
            } finally {
                $process.Dispose()
            }
        }
        $runs[$label] = $results
    }
    $comparison = @(
        foreach ($scenario in $scenarios) {
            $baseline = @($runs.baseline | Where-Object Scenario -EQ $scenario)[0]
            $current = @($runs.current | Where-Object Scenario -EQ $scenario)[0]
            $before = ConvertTo-Json -InputObject $baseline.Observations -Compress
            $after = ConvertTo-Json -InputObject $current.Observations -Compress
            $shared = $scenario -in $sharedScenarios
            $contractPassed = if ($shared) {
                @(
                    @($baseline.Observations) + @($current.Observations) | Where-Object {
                        $_.Outcome -ne 'Return' -or $_.Value -ne 'True'
                    }
                ).Count -eq 0
            } else { $null }
            [pscustomobject]@{
                Scenario = $scenario
                SharedContract = $shared
                ContractPassed = $contractPassed
                Identical = $before -ceq $after
            }
        }
    )
    $report = [ordered]@{
        BaselineVersion = $BaselineVersion
        BaselinePackages = $baselinePackages
        CurrentCommit = "$currentCommit".Trim()
        CurrentReferenceType = 'Project'
        Framework = 'net10.0'
        OperatingSystem = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        Comparison = $comparison
        Runs = $runs
    }
    New-Item -ItemType Directory -Path (Split-Path $ReportPath) -Force | Out-Null
    $report | ConvertTo-Json -Depth 10 | Set-Content $ReportPath
    $comparison | Format-Table -AutoSize | Out-Host
    Write-Host "Compatibility report: $ReportPath"
    if (@($comparison | Where-Object {
        ($_.SharedContract -and !$_.ContractPassed) -or !$_.Identical
    }).Count) {
        throw 'Compatibility differences or shared-contract failures were found; inspect the report.'
    }
} finally {
    Remove-Item $temporary -Recurse -Force
}

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param(
    [ValidateSet('net462', 'net10.0')]
    [string]$Framework = 'net10.0',
    [ValidateSet('Project', 'Package')]
    [string]$ReferenceType = 'Project',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$PackageVersionSqlClient
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Framework -eq 'net462' -and ![System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)) {
    throw 'Executing net462 authentication scenarios requires Windows; cross-building is not runtime validation.'
}

function Invoke-DotNet {
    param([string[]]$CommandArguments)
    & dotnet @CommandArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed ($LASTEXITCODE): $($CommandArguments -join ' ')"
    }
}

$project = Join-Path $PSScriptRoot 'ConfigurationTest/ConfigurationTest.csproj'
$buildOptions = @("-p:ReferenceType=$ReferenceType")
if ($PackageVersionSqlClient) {
    $buildOptions += "-p:SqlClientPackageVersion=$PackageVersionSqlClient"
}
Invoke-DotNet -CommandArguments (@('build', $project, '-c', $Configuration, '-f', $Framework) + $buildOptions)
$output = Join-Path $PSScriptRoot "ConfigurationTest/bin/$Configuration/$Framework"
$extension = if ($Framework -eq 'net462') { 'exe' } else { 'dll' }
$app = Join-Path $output "AuthenticationConfigurationTest.$extension"
$configPath = "$app.config"
$originalConfig = [System.IO.File]::ReadAllBytes($configPath)
try {
    foreach ($scenario in @('configured', 'legacy')) {
        $config = if ($scenario -eq 'configured') { 'app.config' } else { 'legacy.config' }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "ConfigurationTest/$config") -Destination $configPath -Force
        if ($Framework -eq 'net462') {
            & $app $scenario
            if ($LASTEXITCODE -ne 0) { throw "Configuration test failed: $scenario" }
        } else {
            Invoke-DotNet -CommandArguments @($app, $scenario)
        }
    }
} finally {
    [System.IO.File]::WriteAllBytes($configPath, $originalConfig)
}

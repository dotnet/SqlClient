# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param(
    [ValidateSet('net462', 'net10.0')]
    [string]$Framework = 'net10.0',
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param([string[]]$CommandArguments)
    & dotnet @CommandArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed ($LASTEXITCODE): $($CommandArguments -join ' ')"
    }
}

$project = Join-Path $PSScriptRoot 'ConfigurationTest/ConfigurationTest.csproj'
Invoke-DotNet -CommandArguments @('build', $project, '-c', 'Release', '-f', $Framework)
$output = Join-Path $PSScriptRoot "ConfigurationTest/bin/Release/$Framework"
$appName = 'AuthenticationConfigurationTest'
$extension = if ($Framework -eq 'net462') { 'exe' } else { 'dll' }
$app = Join-Path $output "$appName.$extension"
$configPath = "$app.config"
foreach ($scenario in @('configured', 'legacy', 'bad-provider', 'bad-initializer', 'bad-unsupported', 'disabled')) {
    $config = if ($scenario -in @('configured', 'disabled')) { 'app.config' } else { "$scenario.config" }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "ConfigurationTest/$config") -Destination $configPath -Force
    if ($Framework -eq 'net462') {
        & $app $scenario
        if ($LASTEXITCODE -ne 0) { throw "Configuration test failed: $scenario" }
    } else {
        Invoke-DotNet -CommandArguments @($app, $scenario)
    }
}

$replacementProject = Join-Path $PSScriptRoot 'RuntimeVersionTest/RuntimeVersionTest.csproj'
Invoke-DotNet -CommandArguments @('build', $replacementProject, '-c', 'Release', '-f', $Framework)
$driver = Join-Path $output 'Microsoft.Data.SqlClient.dll'
$backup = "$driver.original"
Copy-Item -LiteralPath $driver -Destination $backup
try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "RuntimeVersionTest/bin/Release/$Framework/Microsoft.Data.SqlClient.dll") `
        -Destination $driver -Force
    foreach ($scenario in @('bad-runtime', 'bad-runtime-disabled')) {
        if ($Framework -eq 'net462') {
            & $app $scenario
            if ($LASTEXITCODE -ne 0) { throw "Runtime version test failed: $scenario" }
        } else {
            Invoke-DotNet -CommandArguments @($app, $scenario)
        }
    }
} finally {
    Copy-Item -LiteralPath $backup -Destination $driver -Force
    Remove-Item -LiteralPath $backup
}

if ($Publish) {
    if ($Framework -ne 'net10.0' -or !$IsLinux) {
        throw 'The trimmed/NativeAOT publish checks require net10.0 on Linux.'
    }
    $publishProject = Join-Path $PSScriptRoot 'PublishTest/PublishTest.csproj'
    foreach ($mode in @('trim', 'aot')) {
        $publishOutput = Join-Path $PSScriptRoot "PublishTest/bin/publish-$mode"
        Invoke-DotNet -CommandArguments @('publish', $publishProject, '-c', 'Release', '-r', 'linux-x64',
            "-p:PublishMode=$mode", '-p:CppCompilerAndLinker=gcc', '-p:StripSymbols=false', '-o', $publishOutput)
        & (Join-Path $publishOutput 'PublishTest')
        if ($LASTEXITCODE -ne 0) { throw "$mode authentication test failed." }
    }
}

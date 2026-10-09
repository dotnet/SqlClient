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
    [string]$PackageVersionSqlClient,
    [string]$FileVersionSqlClient,
    [string]$PackageVersionSqlServer,
    [string]$BuildNumber,
    [string]$BuildSuffix,
    [switch]$RuntimeVersions,
    [switch]$AllScenarios,
    [switch]$Publish,
    [switch]$BuildOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (!$BuildOnly -and $Framework -eq 'net462' -and ![System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)) {
    throw 'Executing net462 authentication scenarios requires Windows; cross-building is not runtime validation.'
}
if ($Publish -and ($Framework -ne 'net10.0' -or !$IsLinux -or $BuildOnly)) {
    throw 'Trimmed/NativeAOT execution requires net10.0 on Linux, without BuildOnly.'
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
foreach ($property in @(
    @('SqlClientPackageVersion', $PackageVersionSqlClient),
    @('SqlClientFileVersion', $FileVersionSqlClient),
    @('SqlServerPackageVersion', $PackageVersionSqlServer),
    @('BuildNumber', $BuildNumber),
    @('BuildSuffix', $BuildSuffix)
)) {
    if ($property[1]) { $buildOptions += "-p:$($property[0])=$($property[1])" }
}
# Freeze the effective family version, including build-number/suffix defaults, for every fixture.
$version = & dotnet msbuild $project -nologo '-getProperty:SqlClientPackageVersion' @buildOptions
if ($LASTEXITCODE -ne 0 -or !$version) { throw 'Failed to evaluate the authentication family version.' }
$version = "$version".Trim()
$buildOptions += "-p:SqlClientPackageVersion=$version"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
if ($ReferenceType -eq 'Package') {
    foreach ($id in @('Microsoft.Data.SqlClient', 'Microsoft.Data.SqlClient.Extensions.Azure',
        'Microsoft.Data.SqlClient.Extensions.Abstractions', 'Microsoft.Data.SqlClient.Internal.Logging')) {
        if (!(Test-Path (Join-Path $repoRoot "packages/$id.$version.nupkg"))) {
            throw "Missing locally built $id $version. Use build.proj authentication targets to prepare packages."
        }
    }
}
function Assert-PackageAssets {
    param([string]$Fixture)
    if ($ReferenceType -ne 'Package') { return }
    $assets = Get-Content (Join-Path $PSScriptRoot "$Fixture/obj/project.assets.json") -Raw | ConvertFrom-Json
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Name.Split('/')[0] -in @('Microsoft.Data.SqlClient',
            'Microsoft.Data.SqlClient.Extensions.Abstractions', 'Microsoft.Data.SqlClient.Extensions.Azure',
            'Microsoft.Data.SqlClient.Internal.Logging')) {
            if ($library.Value.type -ne 'package' -or !$library.Name.EndsWith("/$version")) {
                throw "Fixture $Fixture did not consume family package $version : $($library.Name)"
            }
            $packageFolder = @($assets.packageFolders.PSObject.Properties)[0].Name
            $metadataPath = Join-Path $packageFolder "$($library.Value.path)/.nupkg.metadata"
            $metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json
            if ([System.IO.Path]::GetFullPath($metadata.source).TrimEnd('/', '\') -ne
                (Join-Path $repoRoot 'packages').TrimEnd('/', '\')) {
                throw "Fixture $Fixture used a non-local family package: $($library.Name) from $($metadata.source)"
            }
        }
    }
}
Invoke-DotNet -CommandArguments (@('build', $project, '-c', $Configuration, '-f', $Framework) + $buildOptions)
Assert-PackageAssets 'ConfigurationTest'
if ($BuildOnly) {
    Invoke-DotNet -CommandArguments (@('build',
        (Join-Path $PSScriptRoot 'RuntimeVersionTest/RuntimeVersionTest.csproj'),
        '-c', $Configuration, '-f', $Framework) + $buildOptions)
    if ($Framework -eq 'net10.0') {
        Invoke-DotNet -CommandArguments (@('build',
            (Join-Path $PSScriptRoot 'PublishTest/PublishTest.csproj'),
            '-c', $Configuration, '-f', $Framework) + $buildOptions)
        Assert-PackageAssets 'PublishTest'
    }
    return
}
$output = Join-Path $PSScriptRoot "ConfigurationTest/bin/$Configuration/$Framework"
$extension = if ($Framework -eq 'net462') { 'exe' } else { 'dll' }
$app = Join-Path $output "AuthenticationConfigurationTest.$extension"
$configPath = "$app.config"
$originalConfig = [System.IO.File]::ReadAllBytes($configPath)
try {
    $scenarios = @('configured', 'legacy')
    if ($AllScenarios) { $scenarios += @('bad-provider', 'bad-initializer', 'bad-unsupported', 'disabled') }
    foreach ($scenario in $scenarios) {
        $config = if ($scenario -in @('configured', 'disabled')) { 'app.config' } else { "$scenario.config" }
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

if ($RuntimeVersions) {
    $replacementProject = Join-Path $PSScriptRoot 'RuntimeVersionTest/RuntimeVersionTest.csproj'
    Invoke-DotNet -CommandArguments (@('build', $replacementProject, '-c', $Configuration, '-f', $Framework) + $buildOptions)
    $driver = Join-Path $output 'Microsoft.Data.SqlClient.dll'
    $backup = "$driver.$([guid]::NewGuid().ToString('n')).original"
    Copy-Item -LiteralPath $driver -Destination $backup
    try {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "RuntimeVersionTest/bin/$Configuration/$Framework/Microsoft.Data.SqlClient.dll") `
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
}

if ($Publish) {
    $publishProject = Join-Path $PSScriptRoot 'PublishTest/PublishTest.csproj'
    foreach ($mode in @('trim', 'aot')) {
        $publishOutput = Join-Path $PSScriptRoot "PublishTest/bin/$ReferenceType-$Configuration/publish-$mode"
        # A clean destination prevents an earlier untrimmed Azure DLL from invalidating the proof.
        if (Test-Path $publishOutput) { Remove-Item $publishOutput -Recurse -Force }
        Invoke-DotNet -CommandArguments (@('publish', $publishProject, '-c', $Configuration, '-r', 'linux-x64',
            "-p:PublishMode=$mode", '-p:CppCompilerAndLinker=gcc', '-p:StripSymbols=false',
            '-p:TreatWarningsAsErrors=true', '-p:ILLinkTreatWarningsAsErrors=true',
            '-p:IlcTreatWarningsAsErrors=true', '-o', $publishOutput) + $buildOptions)
        Assert-PackageAssets 'PublishTest'
        if (Get-ChildItem $publishOutput -Filter 'Microsoft.Data.SqlClient.Extensions.Azure.dll') {
            throw "$mode retained the optional Azure assembly."
        }
        $deps = Join-Path $publishOutput 'PublishTest.deps.json'
        if (Test-Path $deps) {
            $dependencyGraph = Get-Content $deps -Raw | ConvertFrom-Json
            # Package metadata can remain after trimming; only executable runtime assets matter.
            foreach ($target in $dependencyGraph.targets.PSObject.Properties) {
                foreach ($library in $target.Value.PSObject.Properties) {
                    if ($library.Name.StartsWith('Microsoft.Data.SqlClient.Extensions.Azure/')) {
                        foreach ($kind in @('runtime', 'runtimeTargets', 'native')) {
                            $assets = $library.Value.PSObject.Properties[$kind]
                            if ($assets -and @($assets.Value.PSObject.Properties).Count -gt 0) {
                                throw "$mode retained Azure runtime assets in the dependency graph."
                            }
                        }
                    }
                }
            }
        }
        & (Join-Path $publishOutput 'PublishTest')
        if ($LASTEXITCODE -ne 0) { throw "$mode authentication test failed." }
        Write-Host "PASS: $ReferenceType $mode Azure absent from published output."
    }
}

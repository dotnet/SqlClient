# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Checks exact family versions and automatic NuGet imports using the real Logging package.
.DESCRIPTION
Synthetic projects cover every asset group and packages.config without a database.
Isolated SDK consumers restore from a private fixture feed/cache, never manually import
the target, and prove direct and transitive package imports. Other family packages are
small test fixtures, not production builds. No ordinary project-reference propagation
is asserted. All scratch files stay under this test directory and are removed.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$targets = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../build/Microsoft.Data.SqlClient.Internal.Logging.targets'))
$loggingProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/Logging.csproj'))
$work = Join-Path $PSScriptRoot ("version-checks-" + [guid]::NewGuid())
$previousTmp = $env:TMPDIR
$previousPackages = $env:NUGET_PACKAGES
$family = @('Microsoft.Data.SqlClient.Extensions.Abstractions', 'Microsoft.Data.SqlClient',
    'Microsoft.Data.SqlClient.Extensions.Azure', 'Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider')
$logging = 'Microsoft.Data.SqlClient.Internal.Logging'
$excluded = @('Microsoft.SqlServer.Server', 'Microsoft.Data.SqlClient.SNI', 'Microsoft.Data.SqlClient.SNI.runtime')
$script:checks = 0

# Execute one command, checking both the exit code and the expected diagnostic.
function Invoke-CheckedDotnet {
    param([string[]] $Arguments, [bool] $Mismatch = $false, [string] $Evidence = '')
    $output = & dotnet @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $text = $output -join "`n"
    if ($Mismatch) {
        if ($exitCode -eq 0 -or $text -notmatch 'SQLCLIENT001' -or
            ($Evidence -and -not $text.Contains($Evidence)) -or
            $text -match 'MSB4096|error (?!SQLCLIENT001)[A-Z]+\d+') {
            throw "Expected only SQLCLIENT001: dotnet $($Arguments -join ' ')`n$text"
        }
    } elseif ($exitCode -ne 0 -or ($Evidence -and -not $text.Contains($Evidence))) {
        throw "Command failed or evidence missing: dotnet $($Arguments -join ' ')`n$text"
    }
    # Keep warnings visible even when a command succeeds.
    $output | Where-Object { "$_" -match '\bwarning\b' } | ForEach-Object { Write-Host "$_" }
    $script:checks++
}

# Exercise the target with no SDK: these inputs isolate metadata/filtering regressions.
function Test-Synthetic {
    param([string] $Directory, [string] $AssetKind, [object[]] $Items,
          [bool] $Mismatch = $false, [string[]] $Properties = @(), [string] $Evidence = '')
    $project = Join-Path $Directory 'VersionTest.proj'
    $config = Join-Path $Directory 'packages.config'
    if (Test-Path $config) { Remove-Item $config }
    $assets = ''
    if ($AssetKind -eq 'packages.config') {
        $entries = ($Items | ForEach-Object { "<package id='$($_.Id)' version='$($_.Version)' />" }) -join ''
        Set-Content $config "<packages>$entries</packages>"
    } else {
        $entries = ($Items | ForEach-Object {
            "<$AssetKind Include='$($_.Id).dll'><NuGetPackageId>$($_.Id)</NuGetPackageId><NuGetPackageVersion>$($_.Version)</NuGetPackageVersion></$AssetKind>"
        }) -join ''
        # All collected item kinds can include framework/project assets with no package metadata.
        $bare = (@('ResolvedCompileFileDefinitions', 'ReferencePath', 'RuntimeCopyLocalItems', 'RuntimeTargetsCopyLocalItems') |
            ForEach-Object { "<$_ Include='MetadataFree-$_.dll' />" }) -join ''
        $assets = "<ItemGroup>$bare$entries</ItemGroup>"
    }
    Set-Content $project "<Project>$assets<Import Project='$targets' /></Project>"
    Invoke-CheckedDotnet (@('msbuild', $project, '-t:ValidateSqlClientFamilyPackageVersions', '-v:quiet', '-nologo') + $Properties) $Mismatch $Evidence
}

# Pack a minimal SDK library, optionally depending on Logging, into the private feed.
function New-FixturePackage {
    param([string] $Id, [string] $Version, [bool] $Wrapper = $false)
    $directory = Join-Path $work "$Id-$Version"
    New-Item -ItemType Directory $directory | Out-Null
    $dependency = if ($Wrapper) { "<PackageReference Include='$logging' Version='[$anchor]' />" } else { '' }
    Set-Content (Join-Path $directory 'Fixture.csproj') @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PackageId>$Id</PackageId><AssemblyName>$Id</AssemblyName><Version>$Version</Version>
    <Authors>SqlClient tests</Authors><Description>Isolated version-validation test fixture.</Description>
  </PropertyGroup>
  <ItemGroup>$dependency</ItemGroup>
</Project>
"@
    Invoke-CheckedDotnet @('pack', (Join-Path $directory 'Fixture.csproj'), '-o', $feed,
        '--configfile', $configFile, '-v:quiet', '-nologo')
}

# Build without an explicit target import, then verify NuGet's generated import and the version anchor.
function Test-Consumer {
    param([string] $Name, [bool] $Transitive, [string] $FamilyVersion,
          [string] $Package = '', [bool] $Mismatch = $false, [string[]] $Properties = @(),
          [bool] $RuntimeOnly = $false, [bool] $ExcludeBuild = $false)
    $directory = Join-Path $work $Name
    New-Item -ItemType Directory $directory | Out-Null
    $rootId = if ($Transitive) { 'SqlClient.VersionChecks.Wrapper' } else { $logging }
    $rootVersion = if ($Transitive) { '1.0.0' } else { $anchor }
    $exclude = if ($ExcludeBuild) { "ExcludeAssets='build;buildTransitive'" } else { '' }
    $references = "<PackageReference Include='$rootId' Version='[$rootVersion]' $exclude />"
    $installed = if ($Package) { @($Package) } else { $family }
    foreach ($id in $installed) {
        $runtime = if ($RuntimeOnly) { "ExcludeAssets='compile'" } else { '' }
        $references += "<PackageReference Include='$id' Version='[$FamilyVersion]' $runtime />"
    }
    foreach ($id in $excluded) {
        $references += "<PackageReference Include='$id' Version='[1.0.0]' />"
    }
    $project = Join-Path $directory 'Consumer.csproj'
    Set-Content $project @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>$references</ItemGroup>
  <Target Name="ReportVersionCheck" AfterTargets="ValidateSqlClientFamilyPackageVersions">
    <Message Importance="high" Text="VERSION_CHECK_ANCHOR=`$(_SqlClientExpectedFamilyVersion)" />
  </Target>
</Project>
"@
    Invoke-CheckedDotnet @('restore', $project, '--configfile', $configFile, '-v:quiet', '-nologo')
    $import = Get-Content (Join-Path $directory 'obj/Consumer.csproj.nuget.g.targets') -Raw
    if ($ExcludeBuild) {
        if ($import.Contains('Microsoft.Data.SqlClient.Internal.Logging.targets')) {
            throw 'Excluded build assets unexpectedly imported the target.'
        }
    } else {
        if (-not $import.Contains('buildTransitive/Microsoft.Data.SqlClient.Internal.Logging.targets') -and
            -not $import.Contains('build/Microsoft.Data.SqlClient.Internal.Logging.targets')) {
            throw "NuGet did not import Logging's packaged target for $Name."
        }
        if ($Transitive -and -not $import.Contains('buildTransitive/Microsoft.Data.SqlClient.Internal.Logging.targets')) {
            throw 'Transitive consumer did not import buildTransitive.'
        }
        if ($Transitive) {
            $assets = Get-Content (Join-Path $directory 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
            if ($assets.project.frameworks['net10.0'].dependencies.ContainsKey($logging)) {
                throw 'Transitive test accidentally references Logging directly.'
            }
        }
    }
    $evidence = if ($Mismatch) { $Package } elseif (-not $ExcludeBuild -and $Properties.Count -eq 0) { "VERSION_CHECK_ANCHOR=$anchor" } else { '' }
    Invoke-CheckedDotnet (@('build', $project, '--no-restore', '-v:minimal', '-nologo') + $Properties) $Mismatch $evidence
    Write-Host "PASS: consumer $Name (automatic NuGet import, expected mismatch=$Mismatch)"
}

New-Item -ItemType Directory $work | Out-Null
try {
    $env:TMPDIR = $work
    $synthetic = Join-Path $work 'synthetic'
    New-Item -ItemType Directory $synthetic | Out-Null
    foreach ($kind in @('ResolvedCompileFileDefinitions', 'ReferencePath', 'RuntimeCopyLocalItems', 'RuntimeTargetsCopyLocalItems', 'packages.config')) {
        foreach ($anchorVersion in @('8.0.0', '8.0.0-preview1')) {
            foreach ($id in $family) {
                foreach ($version in @('8.0.0', '8.0.1', '7.1.1', '8.0.0-preview1', '8.0.0-preview2', '1.0.0')) {
                    $items = @(@{ Id = $logging; Version = $anchorVersion }, @{ Id = $id; Version = $version })
                    $items += $excluded | ForEach-Object { @{ Id = $_; Version = '1.0.0' } }
                    $mismatch = $version -ne $anchorVersion
                    $evidence = if ($mismatch) { $id } else { '' }
                    Test-Synthetic $synthetic $kind $items $mismatch @() $evidence
                    if ($mismatch) {
                        Test-Synthetic $synthetic $kind $items $false @('-p:SqlClientEnforceFamilyVersions=false')
                    }
                }
            }
        }
        Test-Synthetic $synthetic $kind @(@{ Id = $logging; Version = '8.0.0' })
        Test-Synthetic $synthetic $kind @(@{ Id = $family[0]; Version = '1.0.0' })
        if ($kind -ne 'packages.config') {
            Test-Synthetic $synthetic $kind @(@{ Id = $logging; Version = '8.0.0' }, @{ Id = $family[0]; Version = '' })
        }
        Test-Synthetic $synthetic $kind @(@{ Id = $logging; Version = '8.0.0' }, @{ Id = $family[0]; Version = '1.0.0' }) $false @('-p:DesignTimeBuild=true')
    }
    # Duplicate Logging assets from compile and runtime resolution must not create a false mismatch.
    Test-Synthetic $synthetic 'ResolvedCompileFileDefinitions' @(
        @{ Id = $logging; Version = '8.0.0' }, @{ Id = $logging; Version = '8.0.0' },
        @{ Id = $family[0]; Version = '8.0.0' })
    Write-Host "PASS: synthetic matrix ($script:checks commands; all asset groups, stable/prerelease, opt-out, metadata-free assets, packages.config, exclusions)."

    $feed = Join-Path $work 'feed'
    New-Item -ItemType Directory $feed | Out-Null
    $anchor = '8.0.0-preview1'
    # Use the real packaging project, preserving its repository props/targets and versioning.
    Invoke-CheckedDotnet @('pack', $loggingProject, '-c', 'Release', '-o', $feed,
        "-p:SqlClientPackageVersion=$anchor", '-v:minimal', '-nologo')
    $packageFile = Join-Path $feed "$logging.$anchor.nupkg"
    $archive = [IO.Compression.ZipFile]::OpenRead($packageFile)
    try {
        foreach ($folder in @('build', 'buildTransitive')) {
            $entry = $archive.GetEntry("$folder/$logging.targets")
            if (-not $entry) { throw "Missing $folder target in the real Logging package." }
            $reader = [IO.StreamReader]::new($entry.Open())
            try { $packed = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($packed -ne (Get-Content $targets -Raw)) { throw "Stale $folder target packaged." }
        }
    } finally { $archive.Dispose() }
    Write-Host 'PASS: real Logging nupkg contains identical build/ and buildTransitive/ targets.'

    # Stop ancestor props/targets/CPM and user package caches/feeds from affecting fixture consumers.
    Set-Content (Join-Path $work 'Directory.Build.props') '<Project />'
    Set-Content (Join-Path $work 'Directory.Build.targets') '<Project />'
    Set-Content (Join-Path $work 'Directory.Packages.props') '<Project />'
    $env:NUGET_PACKAGES = Join-Path $work 'cache'
    $configFile = Join-Path $work 'NuGet.Config'
    $escapedFeed = [Security.SecurityElement]::Escape($feed)
    Set-Content $configFile "<configuration><packageSources><clear /><add key='fixtures' value='$escapedFeed' /></packageSources></configuration>"
    foreach ($id in $family) {
        New-FixturePackage $id $anchor
        New-FixturePackage $id '8.0.0-preview2'
    }
    foreach ($id in $excluded) { New-FixturePackage $id '1.0.0' }
    New-FixturePackage 'SqlClient.VersionChecks.Wrapper' '1.0.0' $true
    foreach ($transitive in @($false, $true)) {
        $mode = if ($transitive) { 'transitive' } else { 'direct' }
        Test-Consumer "$mode-matching" $transitive $anchor
        foreach ($id in $family) {
            Test-Consumer "$mode-mixed-$id" $transitive '8.0.0-preview2' $id $true
        }
        Test-Consumer "$mode-opt-out" $transitive '8.0.0-preview2' $family[0] $false @('-p:SqlClientEnforceFamilyVersions=false')
        Test-Consumer "$mode-runtime-only" $transitive '8.0.0-preview2' $family[3] $true @() $true
    }
    Test-Consumer 'direct-excluded-build-assets' $false '8.0.0-preview2' $family[0] $false @() $false $true
    Write-Host "PASS: $script:checks checked dotnet commands; synthetic and isolated direct/transitive NuGet consumer regressions."
} finally {
    $env:TMPDIR = $previousTmp
    $env:NUGET_PACKAGES = $previousPackages
    Remove-Item -LiteralPath $work -Recurse -Force
}

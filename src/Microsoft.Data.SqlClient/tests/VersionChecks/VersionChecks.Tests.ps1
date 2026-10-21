# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Checks exact family versions and automatic NuGet imports of the Microsoft.Data.SqlClient target.
.DESCRIPTION
Synthetic projects cover every asset group and packages.config without a database.
Isolated SDK consumers restore from a private fixture feed/cache, never manually import
the target, and prove direct and transitive package imports. Every family package is a
small test fixture; the SqlClient fixture carries the real shipped target, and a separate
check proves the real nuspec packs that same file. No ordinary project-reference propagation
is asserted. All scratch files stay under this test directory and are removed.
#>
BeforeDiscovery {
    # Expand the matrix during discovery so each contract has a named Pester result.
    $family = @('Microsoft.Data.SqlClient.Extensions.Abstractions', 'Microsoft.Data.SqlClient.Internal.Logging',
        'Microsoft.Data.SqlClient.Extensions.Azure', 'Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider')
    $syntheticCases = @(
        foreach ($kind in @('ResolvedCompileFileDefinitions', 'ReferencePath', 'RuntimeCopyLocalItems',
                'RuntimeTargetsCopyLocalItems', 'packages.config', 'packages.VersionTest.config')) {
            foreach ($anchorVersion in @('8.0.0', '8.0.0-preview1')) {
                foreach ($id in $family) {
                    foreach ($version in @('8.0.0', '8.0.1', '7.1.1', '8.0.0-preview1', '8.0.0-preview2', '1.0.0')) {
                        @{
                            Kind = $kind; AnchorVersion = $anchorVersion; Id = $id; Version = $version
                            OptOut = $false
                        }
                        if ($version -ne $anchorVersion) {
                            # Reuse each mismatching input to prove the emergency opt-out bypasses it.
                            @{
                                Kind = $kind; AnchorVersion = $anchorVersion; Id = $id; Version = $version
                                OptOut = $true
                            }
                        }
                    }
                }
            }
        }
    )
    $assetKinds = @(
        @{ Kind = 'ResolvedCompileFileDefinitions' }
        @{ Kind = 'ReferencePath' }
        @{ Kind = 'RuntimeCopyLocalItems' }
        @{ Kind = 'RuntimeTargetsCopyLocalItems' }
        @{ Kind = 'packages.config' }
        @{ Kind = 'packages.VersionTest.config' }
    )
    $legacyCaseCases = @(
        foreach ($kind in @('packages.config', 'packages.VersionTest.config')) {
            foreach ($id in $family) {
                foreach ($casing in @('lowercase anchor', 'uppercase family', 'mixed casing')) {
                    foreach ($scenario in @('matching', 'mismatching', 'opt-out')) {
                        @{
                            Kind = $kind; Casing = $casing; Scenario = $scenario
                            AnchorId = if ($casing -eq 'uppercase family') {
                                'Microsoft.Data.SqlClient'
                            } else {
                                'microsoft.data.sqlclient'
                            }
                            Id = if ($casing -eq 'lowercase anchor') { $id } else { $id.ToUpperInvariant() }
                            Version = if ($scenario -eq 'matching') { '8.0.0' } else { '8.0.1' }
                            Mismatch = $scenario -eq 'mismatching'
                            OptOut = $scenario -eq 'opt-out'
                        }
                    }
                }
            }
        }
    )
    # Exercise the same contracts with SqlClient referenced directly and through a wrapper package.
    $consumerCases = @(
        foreach ($transitive in @($false, $true)) {
            $mode = if ($transitive) { 'transitive' } else { 'direct' }
            @{ Name = "$mode-matching"; Transitive = $transitive; Version = '8.0.0-preview1'; Package = ''; Mismatch = $false; Properties = @(); RuntimeOnly = $false; ExcludeBuild = $false }
            foreach ($id in $family) {
                @{ Name = "$mode-mixed-$id"; Transitive = $transitive; Version = '8.0.0-preview2'; Package = $id; Mismatch = $true; Properties = @(); RuntimeOnly = $false; ExcludeBuild = $false }
            }
            @{ Name = "$mode-opt-out"; Transitive = $transitive; Version = '8.0.0-preview2'; Package = $family[0]; Mismatch = $false; Properties = @('-p:SqlClientEnforceFamilyVersions=false'); RuntimeOnly = $false; ExcludeBuild = $false }
            @{ Name = "$mode-runtime-only"; Transitive = $transitive; Version = '8.0.0-preview2'; Package = $family[3]; Mismatch = $true; Properties = @(); RuntimeOnly = $true; ExcludeBuild = $false }
        }
        @{ Name = 'direct-excluded-build-assets'; Transitive = $false; Version = '8.0.0-preview2'; Package = $family[0]; Mismatch = $false; Properties = @(); RuntimeOnly = $false; ExcludeBuild = $true }
    )
}

BeforeAll {
    $ErrorActionPreference = 'Stop'
    $targets = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../build/Microsoft.Data.SqlClient.targets'))
    $nuspec = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../src/Microsoft.Data.SqlClient.nuspec'))
    # Isolate each run from existing fixtures in a unique scratch directory.
    $work = Join-Path $PSScriptRoot ("version-checks-" + [guid]::NewGuid())
    $previousTmp = $env:TMPDIR
    $previousPackages = $env:NUGET_PACKAGES
    $family = @('Microsoft.Data.SqlClient.Extensions.Abstractions', 'Microsoft.Data.SqlClient.Internal.Logging',
        'Microsoft.Data.SqlClient.Extensions.Azure', 'Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider')
    $sqlClient = 'Microsoft.Data.SqlClient'
    $excluded = @('Microsoft.SqlServer.Server', 'Microsoft.Data.SqlClient.SNI', 'Microsoft.Data.SqlClient.SNI.runtime')

    # Execute one command, checking both the exit code and the expected diagnostic.
    function Invoke-CheckedDotnet {
        param([string[]] $Arguments, [bool] $Mismatch = $false, [string] $Evidence = '')
        # Do not leave reusable build workers writing fixture outputs after cleanup.
        if ($Arguments[0] -in @('pack', 'build', 'restore')) {
            $Arguments += '--disable-build-servers'
        } else {
            $Arguments += '-nodeReuse:false'
        }
        $output = & dotnet @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        $text = $output -join "`n"
        if ($Mismatch) {
            # A mismatch is a warning now; reject build failures and unrelated MSBuild errors.
            if ($exitCode -ne 0 -or $text -notmatch '(?im)\bwarning\s+SQLCLIENT001\b' -or
                ($Evidence -and -not $text.Contains($Evidence)) -or
                $text -match '(?im)\berror\s+[A-Z]+\d+\b') {
                throw "Expected a successful build with a SQLCLIENT001 warning: dotnet $($Arguments -join ' ')`n$text"
            }
        } elseif ($exitCode -ne 0 -or ($Evidence -and -not $text.Contains($Evidence)) -or
            $text -match '(?im)\b(?:warning|error)\s+SQLCLIENT001\b') {
            throw "Command failed or evidence missing: dotnet $($Arguments -join ' ')`n$text"
        }
        # Keep warnings visible even when a command succeeds.
        $output | Where-Object { "$_" -match '\bwarning\b' } | ForEach-Object { Write-Host "$_" }
    }

    # Exercise the target with no SDK: these inputs isolate metadata/filtering regressions.
    function Test-Synthetic {
        param([string] $Directory, [string] $AssetKind, [object[]] $Items,
            [bool] $Mismatch = $false, [string[]] $Properties = @(), [string] $Evidence = '',
            [string] $ProjectName = 'VersionTest', [object[]] $FallbackItems = @())
        # Arrange: a fresh project prevents one manifest or asset group leaking into the next case.
        $project = Join-Path $Directory "$ProjectName.proj"
        foreach ($name in @('packages.config', 'packages.VersionTest.config', 'packages.Version_Test.config')) {
            $config = Join-Path $Directory $name
            if (Test-Path $config) { Remove-Item -LiteralPath $config }
        }
        $assets = ''
        if ($AssetKind -like 'packages*.config') {
            $entries = ($Items | ForEach-Object { "<package id='$($_.Id)' version='$($_.Version)' />" }) -join ''
            Set-Content (Join-Path $Directory $AssetKind) "<packages>$entries</packages>"
            if ($FallbackItems.Count -gt 0) {
                $entries = ($FallbackItems | ForEach-Object { "<package id='$($_.Id)' version='$($_.Version)' />" }) -join ''
                Set-Content (Join-Path $Directory 'packages.config') "<packages>$entries</packages>"
            }
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
        # Act/assert: run the real shipped target and require the expected diagnostic, not just failure.
        Invoke-CheckedDotnet (@('msbuild', $project, '-t:ValidateSqlClientFamilyPackageVersions', '-v:quiet', '-nologo') + $Properties) $Mismatch $Evidence
    }

    # Pack a minimal SDK library into the private feed. The wrapper depends on SqlClient, and the
    # SqlClient fixture ships the real target exactly where the nuspec places it.
    function New-FixturePackage {
        param([string] $Id, [string] $Version, [bool] $Wrapper = $false)
        $directory = Join-Path $work "$Id-$Version"
        New-Item -ItemType Directory $directory | Out-Null
        # Exact ranges prevent restore from choosing another fixture version to repair a mismatch.
        $dependency = if ($Wrapper) { "<PackageReference Include='$sqlClient' Version='[$anchor]' />" } else { '' }
        if ($Id -eq $sqlClient) {
            $dependency += "<None Include='$targets' Pack='true' PackagePath='build/;buildTransitive/' />"
        }
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
            [bool] $RuntimeOnly = $false, [bool] $ExcludeBuild = $false,
            [bool] $NeighboringLegacyManifest = $false)
        $directory = Join-Path $work $Name
        New-Item -ItemType Directory $directory | Out-Null
        # Arrange: the wrapper makes SqlClient transitive without importing the target by hand.
        $rootId = if ($Transitive) { 'SqlClient.VersionChecks.Wrapper' } else { $sqlClient }
        $rootVersion = if ($Transitive) { '1.0.0' } else { $anchor }
        $exclude = if ($ExcludeBuild) { "ExcludeAssets='build;buildTransitive'" } else { '' }
        $references = "<PackageReference Include='$rootId' Version='[$rootVersion]' $exclude />"
        $installed = if ($Package) { @($Package) } else { $family }
        foreach ($id in $installed) {
            # Excluding compile assets forces validation to find runtime-only package evidence.
            $runtime = if ($RuntimeOnly) { "ExcludeAssets='compile'" } else { '' }
            $references += "<PackageReference Include='$id' Version='[$FamilyVersion]' $runtime />"
        }
        foreach ($id in $excluded) {
            $references += "<PackageReference Include='$id' Version='[1.0.0]' />"
        }
        $project = Join-Path $directory 'Consumer.csproj'
        if ($NeighboringLegacyManifest) {
            $manifest = "<packages><package id='$sqlClient' version='$anchor' /><package id='$($family[0])' version='8.0.0-preview2' /></packages>"
            Set-Content (Join-Path $directory 'packages.config') $manifest
        }
        Set-Content $project @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>$references</ItemGroup>
  <Target Name="ReportVersionCheck" AfterTargets="ValidateSqlClientFamilyPackageVersions">
    <Message Importance="high" Text="RESTORE_PROJECT_STYLE=`$(RestoreProjectStyle)" />
    <Message Importance="high" Text="VERSION_CHECK_ANCHOR=`$(_SqlClientExpectedFamilyVersion)" />
  </Target>
</Project>
"@
        # Act: restore normally so NuGet, rather than the test, imports the shipped target.
        Invoke-CheckedDotnet @('restore', $project, '--configfile', $configFile, '-v:quiet', '-nologo')
        $import = (Get-Content (Join-Path $directory 'obj/Consumer.csproj.nuget.g.targets') -Raw) -replace '\\', '/'
        if ($ExcludeBuild) {
            if ($import.Contains('Microsoft.Data.SqlClient.targets')) {
                throw 'Excluded build assets unexpectedly imported the target.'
            }
        } else {
            if (-not $import.Contains('buildTransitive/Microsoft.Data.SqlClient.targets') -and
                -not $import.Contains('build/Microsoft.Data.SqlClient.targets')) {
                throw "NuGet did not import SqlClient's packaged target for $Name."
            }
            if ($Transitive -and -not $import.Contains('buildTransitive/Microsoft.Data.SqlClient.targets')) {
                throw 'Transitive consumer did not import buildTransitive.'
            }
            if ($Transitive) {
                # Prove the transitive case was not accidentally turned into a direct reference.
                $assets = Get-Content (Join-Path $directory 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
                if ($assets.project.frameworks['net10.0'].dependencies.ContainsKey($sqlClient)) {
                    throw 'Transitive test accidentally references SqlClient directly.'
                }
            }
        }
        # Assert both automatic import and the resolved anchor; an arbitrary build error is not success.
        $evidence = if ($NeighboringLegacyManifest) {
            'RESTORE_PROJECT_STYLE=PackageReference'
        } elseif ($Mismatch) {
            $Package
        } elseif (-not $ExcludeBuild -and $Properties.Count -eq 0) {
            "VERSION_CHECK_ANCHOR=$anchor"
        } else {
            ''
        }
        Invoke-CheckedDotnet (@('build', $project, '--no-restore', '-v:minimal', '-nologo') + $Properties) $Mismatch $evidence
        Write-Host "PASS: consumer $Name (automatic NuGet import, expected mismatch=$Mismatch)"
    }

    New-Item -ItemType Directory $work | Out-Null
    $env:TMPDIR = $work
    $synthetic = Join-Path $work 'synthetic'
    New-Item -ItemType Directory $synthetic | Out-Null
}

AfterAll {
    # Restore process-wide settings even after failures; delete only this run's unique directory.
    $env:TMPDIR = $previousTmp
    $env:NUGET_PACKAGES = $previousPackages
    if ($work -and (Test-Path -LiteralPath $work)) {
        Remove-Item -LiteralPath $work -Recurse -Force
    }
}

Describe 'Synthetic family-version validation' -Tag 'Synthetic' {
    It '<Kind>: <Id> <Version> against SqlClient <AnchorVersion> (opt-out: <OptOut>)' -ForEach $syntheticCases {
        # Arrange matching/mixed family metadata alongside independently versioned packages.
        $items = @(@{ Id = $sqlClient; Version = $AnchorVersion }, @{ Id = $Id; Version = $Version })
        $items += $excluded | ForEach-Object { @{ Id = $_; Version = '1.0.0' } }
        $mismatch = $Version -ne $AnchorVersion -and -not $OptOut
        $properties = if ($OptOut) { @('-p:SqlClientEnforceFamilyVersions=false') } else { @() }
        $evidence = if ($mismatch) { $Id } else { '' }
        # Act/assert through the shipped target rather than reproducing its comparison logic.
        Test-Synthetic $synthetic $Kind $items $mismatch $properties $evidence
    }

    It '<Kind>: does not require optional family packages' -ForEach $assetKinds {
        Test-Synthetic $synthetic $Kind @(@{ Id = $sqlClient; Version = '8.0.0' })
    }

    It '<Kind>: does not guess a missing SqlClient anchor' -ForEach $assetKinds {
        Test-Synthetic $synthetic $Kind @(@{ Id = $family[0]; Version = '1.0.0' })
    }

    It '<Kind>: skips design-time builds' -ForEach $assetKinds {
        Test-Synthetic $synthetic $Kind @(
            @{ Id = $sqlClient; Version = '8.0.0' }, @{ Id = $family[0]; Version = '1.0.0' }
        ) $false @('-p:DesignTimeBuild=true')
    }

    It '<Kind>: does not guess missing asset version metadata' -ForEach @(
        @{ Kind = 'ResolvedCompileFileDefinitions' }
        @{ Kind = 'ReferencePath' }
        @{ Kind = 'RuntimeCopyLocalItems' }
        @{ Kind = 'RuntimeTargetsCopyLocalItems' }
    ) {
        Test-Synthetic $synthetic $Kind @(
            @{ Id = $sqlClient; Version = '8.0.0' }, @{ Id = $family[0]; Version = '' })
    }

    # Duplicate SqlClient assets from compile and runtime resolution must not create a false mismatch.
    It 'accepts duplicate matching SqlClient assets' {
        Test-Synthetic $synthetic 'ResolvedCompileFileDefinitions' @(
            @{ Id = $sqlClient; Version = '8.0.0' }, @{ Id = $sqlClient; Version = '8.0.0' },
            @{ Id = $family[0]; Version = '8.0.0' })
    }

    It 'skips a consumer with neither manifest nor package metadata' {
        Test-Synthetic $synthetic 'ReferencePath' @()
    }
}

Describe 'Project-specific legacy manifests' -Tag 'Synthetic', 'LegacyManifest' {
    It 'detects mismatches in packages.config when RestoreProjectStyle is unset' {
        Test-Synthetic $synthetic 'packages.config' @(
            @{ Id = $sqlClient; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.1' }
        ) $true @() $family[0]
    }

    # Conflicting manifests expose incorrect selection; matching files alone cannot prove precedence.
    It 'detects mismatches without a fallback packages.config' {
        Test-Synthetic $synthetic 'packages.VersionTest.config' @(
            @{ Id = $sqlClient; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.1' }
        ) $true @() $family[0]
    }

    It 'prefers a mismatching project manifest over a matching fallback' {
        Test-Synthetic $synthetic 'packages.VersionTest.config' @(
            @{ Id = $sqlClient; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.1' }
        ) $true @() $family[0] -FallbackItems @(
            @{ Id = $sqlClient; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.0' })
    }

    It 'ignores a mismatching fallback when the project manifest matches' {
        Test-Synthetic $synthetic 'packages.VersionTest.config' @(
            @{ Id = $sqlClient; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.0' }
        ) -FallbackItems @(
            @{ Id = $sqlClient; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.1' })
    }

    It 'does not take a SqlClient anchor from an inactive fallback' {
        # Selection applies to the anchor as well as mismatches; never merge the two manifests.
        Test-Synthetic $synthetic 'packages.VersionTest.config' @(
            @{ Id = $family[0]; Version = '8.0.1' }
        ) -FallbackItems @(
            @{ Id = $sqlClient; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.1' })
    }

    It 'normalizes spaces in the project name to underscores' {
        Test-Synthetic $synthetic 'packages.Version_Test.config' @(
            @{ Id = $sqlClient; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.1' }
        ) $true @() $family[0] -ProjectName 'Version Test'
    }
}

Describe 'Case-insensitive legacy package IDs' -Tag 'Synthetic', 'LegacyCase' {
    It '<Kind>: <Casing>, <Id> (<Scenario>)' -ForEach $legacyCaseCases {
        $items = @(@{ Id = $AnchorId; Version = '8.0.0' }, @{ Id = $Id; Version = $Version })
        $properties = if ($OptOut) { @('-p:SqlClientEnforceFamilyVersions=false') } else { @() }
        $evidence = if ($Mismatch) { $Id } else { '' }
        Test-Synthetic $synthetic $Kind $items $Mismatch $properties $evidence
    }
}

Describe 'Packaged direct and transitive consumers' -Tag 'Consumer' {
    BeforeAll {
        # Arrange once: fixture packages only, so no driver build is needed.
        $feed = Join-Path $work 'feed'
        New-Item -ItemType Directory $feed | Out-Null
        $anchor = '8.0.0-preview1'
        # Stop ancestor props/targets/CPM and user package caches/feeds from affecting fixtures.
        Set-Content (Join-Path $work 'Directory.Build.props') '<Project />'
        Set-Content (Join-Path $work 'Directory.Build.targets') '<Project />'
        Set-Content (Join-Path $work 'Directory.Packages.props') '<Project />'
        $env:NUGET_PACKAGES = Join-Path $work 'cache'
        $configFile = Join-Path $work 'NuGet.Config'
        $escapedFeed = [Security.SecurityElement]::Escape($feed)
        Set-Content $configFile "<configuration><packageSources><clear /><add key='fixtures' value='$escapedFeed' /></packageSources></configuration>"
        New-FixturePackage $sqlClient $anchor
        foreach ($id in $family) {
            New-FixturePackage $id $anchor
            New-FixturePackage $id '8.0.0-preview2'
        }
        foreach ($id in $excluded) { New-FixturePackage $id '1.0.0' }
        New-FixturePackage 'SqlClient.VersionChecks.Wrapper' '1.0.0' $true
    }

    It 'packs the current target into <Folder> of the real nuspec' -ForEach @(
        @{ Folder = 'build' }
        @{ Folder = 'buildTransitive' }
    ) {
        # The fixture SqlClient package proves NuGet imports the target; this proves the real
        # package ships that same file. Nuspec paths are relative to the artifacts directory.
        $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
        $folder = $Folder
        $entries = ([xml](Get-Content $nuspec -Raw)).package.files.file | Where-Object {
            $_.target.Trim('\', '/') -eq $folder -and
            [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts' ($_.src -replace '\\', '/'))) -eq $targets
        }
        if (@($entries).Count -ne 1) { throw "Real nuspec does not pack $targets into $folder/." }
    }

    It 'validates <Name>' -ForEach $consumerCases {
        # Restore and build a fresh consumer so generated imports and resolved assets are real.
        Test-Consumer $Name $Transitive $Version $Package $Mismatch $Properties $RuntimeOnly $ExcludeBuild
    }

    It 'ignores a mismatching neighboring packages.config for a PackageReference consumer' {
        Test-Consumer 'direct-neighboring-packages-config' $false $anchor -NeighboringLegacyManifest $true
    }
}

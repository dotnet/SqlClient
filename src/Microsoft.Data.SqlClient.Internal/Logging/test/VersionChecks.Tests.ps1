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
BeforeDiscovery {
    # Expand the matrix during discovery so each contract has a named Pester result.
    $family = @('Microsoft.Data.SqlClient.Extensions.Abstractions', 'Microsoft.Data.SqlClient',
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
    # Exercise the same contracts with Logging referenced directly and through a wrapper package.
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
    $targets = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../build/Microsoft.Data.SqlClient.Internal.Logging.targets'))
    $loggingProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/Logging.csproj'))
    # Stay inside the repo for the real pack's imports; isolate each run from existing fixtures.
    $work = Join-Path $PSScriptRoot ("version-checks-" + [guid]::NewGuid())
    $previousTmp = $env:TMPDIR
    $previousPackages = $env:NUGET_PACKAGES
    $family = @('Microsoft.Data.SqlClient.Extensions.Abstractions', 'Microsoft.Data.SqlClient',
        'Microsoft.Data.SqlClient.Extensions.Azure', 'Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider')
    $logging = 'Microsoft.Data.SqlClient.Internal.Logging'
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
            # A restore/compiler failure must not masquerade as the expected version diagnostic.
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

    # Pack a minimal SDK library, optionally depending on Logging, into the private feed.
    function New-FixturePackage {
        param([string] $Id, [string] $Version, [bool] $Wrapper = $false)
        $directory = Join-Path $work "$Id-$Version"
        New-Item -ItemType Directory $directory | Out-Null
        # Exact ranges prevent restore from choosing another fixture version to repair a mismatch.
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
        # Arrange: the wrapper makes Logging transitive without importing the target by hand.
        $rootId = if ($Transitive) { 'SqlClient.VersionChecks.Wrapper' } else { $logging }
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
        Set-Content $project @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>$references</ItemGroup>
  <Target Name="ReportVersionCheck" AfterTargets="ValidateSqlClientFamilyPackageVersions">
    <Message Importance="high" Text="VERSION_CHECK_ANCHOR=`$(_SqlClientExpectedFamilyVersion)" />
  </Target>
</Project>
"@
        # Act: restore normally so NuGet, rather than the test, imports the shipped target.
        Invoke-CheckedDotnet @('restore', $project, '--configfile', $configFile, '-v:quiet', '-nologo')
        $import = (Get-Content (Join-Path $directory 'obj/Consumer.csproj.nuget.g.targets') -Raw) -replace '\\', '/'
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
                # Prove the transitive case was not accidentally turned into a direct reference.
                $assets = Get-Content (Join-Path $directory 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
                if ($assets.project.frameworks['net10.0'].dependencies.ContainsKey($logging)) {
                    throw 'Transitive test accidentally references Logging directly.'
                }
            }
        }
        # Assert both automatic import and the resolved anchor; an arbitrary build error is not success.
        $evidence = if ($Mismatch) { $Package } elseif (-not $ExcludeBuild -and $Properties.Count -eq 0) { "VERSION_CHECK_ANCHOR=$anchor" } else { '' }
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
    It '<Kind>: <Id> <Version> against Logging <AnchorVersion> (opt-out: <OptOut>)' -ForEach $syntheticCases {
        # Arrange matching/mixed family metadata alongside independently versioned packages.
        $items = @(@{ Id = $logging; Version = $AnchorVersion }, @{ Id = $Id; Version = $Version })
        $items += $excluded | ForEach-Object { @{ Id = $_; Version = '1.0.0' } }
        $mismatch = $Version -ne $AnchorVersion -and -not $OptOut
        $properties = if ($OptOut) { @('-p:SqlClientEnforceFamilyVersions=false') } else { @() }
        $evidence = if ($mismatch) { $Id } else { '' }
        # Act/assert through the shipped target rather than reproducing its comparison logic.
        Test-Synthetic $synthetic $Kind $items $mismatch $properties $evidence
    }

    It '<Kind>: does not require optional family packages' -ForEach $assetKinds {
        Test-Synthetic $synthetic $Kind @(@{ Id = $logging; Version = '8.0.0' })
    }

    It '<Kind>: does not guess a missing Logging anchor' -ForEach $assetKinds {
        Test-Synthetic $synthetic $Kind @(@{ Id = $family[0]; Version = '1.0.0' })
    }

    It '<Kind>: skips design-time builds' -ForEach $assetKinds {
        Test-Synthetic $synthetic $Kind @(
            @{ Id = $logging; Version = '8.0.0' }, @{ Id = $family[0]; Version = '1.0.0' }
        ) $false @('-p:DesignTimeBuild=true')
    }

    It '<Kind>: does not guess missing asset version metadata' -ForEach @(
        @{ Kind = 'ResolvedCompileFileDefinitions' }
        @{ Kind = 'ReferencePath' }
        @{ Kind = 'RuntimeCopyLocalItems' }
        @{ Kind = 'RuntimeTargetsCopyLocalItems' }
    ) {
        Test-Synthetic $synthetic $Kind @(
            @{ Id = $logging; Version = '8.0.0' }, @{ Id = $family[0]; Version = '' })
    }

    # Duplicate Logging assets from compile and runtime resolution must not create a false mismatch.
    It 'accepts duplicate matching Logging assets' {
        Test-Synthetic $synthetic 'ResolvedCompileFileDefinitions' @(
            @{ Id = $logging; Version = '8.0.0' }, @{ Id = $logging; Version = '8.0.0' },
            @{ Id = $family[0]; Version = '8.0.0' })
    }

    It 'skips a consumer with neither manifest nor package metadata' {
        Test-Synthetic $synthetic 'ReferencePath' @()
    }
}

Describe 'Project-specific legacy manifests' -Tag 'Synthetic', 'LegacyManifest' {
    # Conflicting manifests expose incorrect selection; matching files alone cannot prove precedence.
    It 'detects mismatches without a fallback packages.config' {
        Test-Synthetic $synthetic 'packages.VersionTest.config' @(
            @{ Id = $logging; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.1' }
        ) $true @() $family[0]
    }

    It 'prefers a mismatching project manifest over a matching fallback' {
        Test-Synthetic $synthetic 'packages.VersionTest.config' @(
            @{ Id = $logging; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.1' }
        ) $true @() $family[0] -FallbackItems @(
            @{ Id = $logging; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.0' })
    }

    It 'ignores a mismatching fallback when the project manifest matches' {
        Test-Synthetic $synthetic 'packages.VersionTest.config' @(
            @{ Id = $logging; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.0' }
        ) -FallbackItems @(
            @{ Id = $logging; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.1' })
    }

    It 'does not take a Logging anchor from an inactive fallback' {
        # Selection applies to the anchor as well as mismatches; never merge the two manifests.
        Test-Synthetic $synthetic 'packages.VersionTest.config' @(
            @{ Id = $family[0]; Version = '8.0.1' }
        ) -FallbackItems @(
            @{ Id = $logging; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.1' })
    }

    It 'normalizes spaces in the project name to underscores' {
        Test-Synthetic $synthetic 'packages.Version_Test.config' @(
            @{ Id = $logging; Version = '8.0.0' }, @{ Id = $family[0]; Version = '8.0.1' }
        ) $true @() $family[0] -ProjectName 'Version Test'
    }
}

Describe 'Packaged direct and transitive consumers' -Tag 'Consumer' {
    BeforeAll {
        # Arrange once: package the real Logging project, not a substitute targets fixture.
        $feed = Join-Path $work 'feed'
        New-Item -ItemType Directory $feed | Out-Null
        $anchor = '8.0.0-preview1'
        # Use the real packaging project, preserving its repository props/targets and versioning.
        Invoke-CheckedDotnet @('pack', $loggingProject, '-c', 'Release', '-o', $feed,
            "-p:SqlClientPackageVersion=$anchor", '-v:minimal', '-nologo')
        $packageFile = Join-Path $feed "$logging.$anchor.nupkg"
        # Install isolation boundaries only after the real pack has consumed repository settings.
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
    }

    It 'ships the current targets unchanged in <Folder>' -ForEach @(
        @{ Folder = 'build' }
        @{ Folder = 'buildTransitive' }
    ) {
        # Assert the archive contents, so a stale or omitted target cannot pass consumer checks.
        $archive = [IO.Compression.ZipFile]::OpenRead($packageFile)
        try {
            $folder = $Folder
            $entry = $archive.GetEntry("$folder/$logging.targets")
            if (-not $entry) { throw "Missing $folder target in the real Logging package." }
            $reader = [IO.StreamReader]::new($entry.Open())
            try { $packed = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($packed -ne (Get-Content $targets -Raw)) { throw "Stale $folder target packaged." }
        } finally { $archive.Dispose() }
    }

    It 'validates <Name>' -ForEach $consumerCases {
        # Restore and build a fresh consumer so generated imports and resolved assets are real.
        Test-Consumer $Name $Transitive $Version $Package $Mismatch $Properties $RuntimeOnly $ExcludeBuild
    }
}

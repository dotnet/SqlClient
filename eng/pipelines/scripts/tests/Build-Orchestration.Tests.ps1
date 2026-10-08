# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

BeforeAll {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
    $buildProject = Join-Path $repoRoot 'build.proj'
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source

    # Record child CLI arguments without restoring, building, or packing driver projects.
    $stubDirectory = Join-Path $TestDrive 'dotnet stub'
    New-Item -ItemType Directory -Path $stubDirectory | Out-Null
    if ($IsWindows) {
        @'
@echo off
:next
if "%~1"=="" exit /b 0
echo ARG:"%~1"
shift
goto next
'@ | Set-Content (Join-Path $stubDirectory 'dotnet.cmd')
    }
    else {
        @'
#!/bin/sh
printf 'ARG:%s\n' "$@"
'@ | Set-Content (Join-Path $stubDirectory 'dotnet')
        & chmod +x (Join-Path $stubDirectory 'dotnet')
        if ($LASTEXITCODE -ne 0) { throw 'Failed to make the dotnet stub executable.' }
    }

    function Invoke-BuildProbe {
        param([string[]]$BuildArguments)

        $output = & $dotnet msbuild $buildProject -nologo @BuildArguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "MSBuild probe failed:`n$($output -join [Environment]::NewLine)"
        }
        $output
    }

    # Evaluate the real SDK project without restoring packages or executing build targets.
    # The returned properties/items include imported props and TFM-specific conditions,
    # so callers verify effective build behavior rather than just matching project XML.
    # Properties supplies optional command-line overrides (for example, TargetFramework).
    function Get-ProjectEvaluation {
        param(
            [string]$Project,
            [string[]]$Properties = @()
        )

        $output = & $dotnet msbuild (Join-Path $repoRoot $Project) -nologo @Properties `
            -getProperty:TargetFramework,TargetFrameworks,_SqlClientPackageTfm `
            -getItem:PackageReference,PackageVersion,Reference 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Project evaluation failed for ${Project}:`n$($output -join [Environment]::NewLine)"
        }
        $output -join "`n" | ConvertFrom-Json
    }

    # Populate only the baseline asset paths the resolver needs, then run its selection
    # target directly. Empty DLL placeholders keep these cases offline and inexpensive;
    # returning the exit code lets missing-asset cases assert the expected failure.
    function Invoke-RefBaselineProbe {
        param(
            [string[]]$AvailableFrameworks,
            [string[]]$Properties = @()
        )

        $artifacts = Join-Path $TestDrive ([guid]::NewGuid().ToString())
        $baseline = Join-Path $artifacts 'apicompat\extracted\1.0.0-probe\ref'
        foreach ($framework in $AvailableFrameworks) {
            $directory = Join-Path $baseline $framework
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            New-Item -ItemType File -Path (Join-Path $directory 'Microsoft.Data.SqlClient.dll') | Out-Null
        }

        $output = & $dotnet msbuild (Join-Path $repoRoot 'tools\targets\CompareMdsRefAssemblies.targets') `
            -nologo -v:quiet -t:_ResolveRefBaselineFrameworks -getItem:_RefTfm `
            "-p:Artifacts=$artifacts" "-p:RepoRoot=$repoRoot" `
            -p:BaselinePackageVersion=1.0.0-probe @Properties 2>&1
        [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output = $output -join "`n"
        }
    }

    function Get-ChildArguments {
        param(
            [string]$Target,
            [string[]]$Properties = @()
        )

        $arguments = @(
            "-t:$Target",
            '-v:normal',
            "-p:DotnetPath=$stubDirectory/",
            "-p:PackagesDir=$TestDrive/packages/"
        )
        foreach ($property in @(
            'LoggingArtifactRoot', 'AbstractionsArtifactRoot', 'AzureArtifactRoot',
            'AkvProviderArtifactRoot', 'SqlServerArtifactRoot', 'SqlClientPackageArtifactRoot'
        )) {
            $arguments += "-p:$property=$TestDrive/empty/"
        }

        @(Invoke-BuildProbe ($arguments + $Properties) |
            ForEach-Object {
                if ("$_".Trim() -match '^ARG:(.*)$') { $Matches[1].Trim('"') }
            })
    }

    function Get-ProjectTargetArguments {
        param(
            [string]$Project,
            [string[]]$Targets,
            [string[]]$Properties,
            [string]$EntryTarget = $Targets[-1]
        )

        # Run the real command-generation targets without compiling their SDK projects.
        [xml]$source = Get-Content (Join-Path $repoRoot $Project) -Raw
        [xml]$probe = @'
<Project>
  <Target Name="ResolveReferences" />
  <Target Name="CopyFilesToOutputDirectory">
    <Message Text="ARG:CopyFilesToOutputDirectory" Importance="High" />
  </Target>
  <Target Name="Build" DependsOnTargets="CopyFilesToOutputDirectory" />
</Project>
'@
        foreach ($target in $Targets) {
            $node = $source.SelectSingleNode("/Project/Target[@Name='$target']")
            $probe.DocumentElement.AppendChild($probe.ImportNode($node, $true)) | Out-Null
        }
        $probePath = Join-Path $TestDrive 'command-probe.proj'
        $probe.Save($probePath)

        $output = & $dotnet msbuild $probePath -nologo -v:normal "-t:$EntryTarget" `
            "-p:DotnetPath=$stubDirectory/" @Properties 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "MSBuild target probe failed:`n$($output -join [Environment]::NewLine)"
        }
        @($output | ForEach-Object {
            if ("$_".Trim() -match '^ARG:(.*)$') { $Matches[1].Trim('"') }
        })
    }
}

Describe 'supported target frameworks' {
    # Pipeline matrices and standalone VM installers must agree: a clean benchmark VM
    # should not download retired runtimes just because a script bypasses YAML setup.
    It 'does not schedule retired frameworks or install their runtimes in active pipelines' {
        $pipelines = @(
            Get-ChildItem (Join-Path $repoRoot 'eng\pipelines') -Recurse -File -Include *.yml, *.yaml
            Get-ChildItem (Join-Path $repoRoot '.github\workflows') -Recurse -File -Include *.yml, *.yaml
        )
        foreach ($pipeline in $pipelines) {
            $content = Get-Content $pipeline.FullName -Raw
            $content | Should -Not -Match '\bnet(?:47|8\.0|9\.0)\b' -Because $pipeline.FullName
            $content | Should -Not -Match 'runtimes:\s*\[[^\]]*\b[89]\.x\b' -Because $pipeline.FullName
        }
        foreach ($script in @('run-perf-tests.ps1', 'run-perf-tests.sh')) {
            $content = Get-Content (Join-Path $repoRoot "eng\pipelines\perf\scripts\$script") -Raw
            $content | Should -Not -Match '\bnet(?:47|8\.0|9\.0)\b' -Because $script
            # These scripts use bare channel numbers, not TFMs or the YAML "8.x" syntax.
            $content | Should -Not -Match '(?<![\w.])[89]\.0(?![\w.])' -Because $script
        }
    }

    # Enumerate target sets explicitly so neither inherited stress targets nor the
    # deliberate Standard-only, net481, and historical-repro exceptions get lost.
    It 'evaluates <Project> to <Frameworks>' -ForEach @(
        foreach ($group in @(
            @{
                Frameworks = 'net462;net10.0'
                Projects = @(
                    'doc\samples\Microsoft.Data.SqlClient.Samples.csproj'
                    'src\Microsoft.Data.SqlClient\src\Microsoft.Data.SqlClient.csproj'
                    'src\Microsoft.Data.SqlClient\tests\UnitTests\Microsoft.Data.SqlClient.UnitTests.csproj'
                    'src\Microsoft.Data.SqlClient\tests\FunctionalTests\Microsoft.Data.SqlClient.FunctionalTests.csproj'
                    'src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTests.csproj'
                    'src\Microsoft.Data.SqlClient\tests\Common\Microsoft.Data.SqlClient.TestCommon.csproj'
                    'src\Microsoft.Data.SqlClient\tests\CustomConfigurableRetryLogic\CustomRetryLogicProvider.csproj'
                    'src\Microsoft.Data.SqlClient\tests\TestUdts\Address\Address.csproj'
                    'src\Microsoft.Data.SqlClient\tests\TestUdts\Circle\Circle.csproj'
                    'src\Microsoft.Data.SqlClient\tests\TestUdts\Shapes\Shapes.csproj'
                    'src\Microsoft.Data.SqlClient\tests\TestUdts\Utf8String\Utf8String.csproj'
                    'src\Microsoft.Data.SqlClient\tests\StressTests\IMonitorLoader\IMonitorLoader.csproj'
                    'src\Microsoft.Data.SqlClient\tests\StressTests\SqlClient.Stress.Common\SqlClient.Stress.Common.csproj'
                    'src\Microsoft.Data.SqlClient\tests\StressTests\SqlClient.Stress.Framework\SqlClient.Stress.Framework.csproj'
                    'src\Microsoft.Data.SqlClient\tests\StressTests\SqlClient.Stress.Runner\SqlClient.Stress.Runner.csproj'
                    'src\Microsoft.Data.SqlClient\tests\StressTests\SqlClient.Stress.Tests\SqlClient.Stress.Tests.csproj'
                    'src\Microsoft.Data.SqlClient.Extensions\Abstractions\test\Abstractions.Test.csproj'
                    'src\Microsoft.Data.SqlClient.Extensions\Abstractions\test\ConfigurationTest\ConfigurationTest.csproj'
                    'src\Microsoft.Data.SqlClient.Extensions\Abstractions\test\RuntimeVersionTest\RuntimeVersionTest.csproj'
                    'src\Microsoft.Data.SqlClient.Extensions\Azure\test\Azure.Test.csproj'
                    'src\Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider\test\Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.Test.csproj'
                )
            }
            @{
                Frameworks = 'net462;net10.0;netstandard2.0'
                Projects = @(
                    'src\Microsoft.Data.SqlClient\ref\Microsoft.Data.SqlClient.csproj'
                    'src\Microsoft.Data.SqlClient\notsupported\Microsoft.Data.SqlClient.csproj'
                )
            }
            @{
                Frameworks = 'net462;netstandard2.0'
                Projects = @(
                    'src\Microsoft.Data.SqlClient.Extensions\Azure\src\Azure.csproj'
                )
            }
            @{
                # This independently versioned package needs its own major-version PR.
                Frameworks = 'net46;netstandard2.0'
                Projects = @('src\Microsoft.SqlServer.Server\Microsoft.SqlServer.Server.csproj')
            }
            @{
                Frameworks = 'netstandard2.0'
                Projects = @(
                    'src\Microsoft.Data.SqlClient.Internal\Logging\src\Logging.csproj'
                    'src\Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider\src\Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.csproj'
                    'src\Microsoft.Data.SqlClient\tests\tools\Microsoft.Data.SqlClient.TestUtilities\Microsoft.Data.SqlClient.TestUtilities.csproj'
                    'src\Microsoft.Data.SqlClient\tests\tools\TDS\TDS\TDS.csproj'
                    'src\Microsoft.Data.SqlClient\tests\tools\TDS\TDS.EndPoint\TDS.EndPoint.csproj'
                    'src\Microsoft.Data.SqlClient\tests\tools\TDS\TDS.Servers\TDS.Servers.csproj'
                    'tools\GenAPI\Microsoft.Cci.Extensions\Microsoft.Cci.Extensions.csproj'
                )
            }
            @{
                Frameworks = 'netstandard2.0;net10.0'
                Projects = @('src\Microsoft.Data.SqlClient.Extensions\Abstractions\src\Abstractions.csproj')
            }
            @{
                Frameworks = 'net10.0'
                Projects = @(
                    'src\Microsoft.Data.SqlClient\tests\PerformanceTests\Microsoft.Data.SqlClient.PerformanceTests.csproj'
                    'src\Microsoft.Data.SqlClient.Extensions\Abstractions\test\PublishTest\PublishTest.csproj'
                    'src\Microsoft.Data.SqlClient\tests\tools\Microsoft.Data.SqlClient.ExtUtilities\Microsoft.Data.SqlClient.ExtUtilities.csproj'
                    'tools\GenAPI\Microsoft.DotNet.GenAPI\Microsoft.DotNet.GenAPI.csproj'
                    'tools\PackageValidator\src\PackageValidator.csproj'
                    'tools\PackageValidator\test\PackageValidator.Test.csproj'
                )
            }
            @{
                Frameworks = 'net481;net10.0'
                Projects = @(
                    'tools\PackageCompatibility\src\PackageCompatibility.csproj'
                    'tools\PackageCompatibility\test\PackageCompatibility.Test.csproj'
                )
            }
            @{
                Frameworks = 'net462;net47;net472;net481;net10.0'
                Projects = @('tools\SniCloseLegacyRepro\SniCloseLegacyRepro.csproj')
            }
        )) {
            foreach ($project in $group.Projects) {
                @{ Project = $project; Frameworks = $group.Frameworks }
            }
        }
    ) {
        $result = Get-ProjectEvaluation $Project
        $actual = if ($result.Properties.TargetFrameworks) {
            $result.Properties.TargetFrameworks
        }
        else {
            $result.Properties.TargetFramework
        }
        ($actual.Split(';') | Sort-Object) -join ';' |
            Should -Be (($Frameworks.Split(';') | Sort-Object) -join ';')
    }

    # Evaluate both OS branches even on a Windows host; cross-compilation must not
    # introduce a Framework target on Unix or downgrade the sample's net481 target.
    It 'preserves the Windows sample targets for <OperatingSystem>' -ForEach @(
        @{ OperatingSystem = 'Windows_NT'; Frameworks = 'net481;net10.0-windows' }
        @{ OperatingSystem = 'Unix'; Frameworks = 'net10.0-windows' }
    ) {
        $result = Get-ProjectEvaluation 'doc\apps\AzureSqlConnector\AzureSqlConnector.csproj' @(
            "-p:OS=$OperatingSystem"
        )
        $actual = if ($result.Properties.TargetFrameworks) {
            $result.Properties.TargetFrameworks
        }
        else {
            $result.Properties.TargetFramework
        }
        $actual | Should -Be $Frameworks
    }

    # Unit tests use internal driver APIs unavailable in ref assemblies. Check both
    # the NuGet compile exclusion and the explicit lib path, not just the TFM property.
    It 'maps Package-mode UnitTests to the <Framework> implementation, not the reference assembly' -ForEach @(
        @{ Framework = 'net462' }
        @{ Framework = 'net10.0' }
    ) {
        $result = Get-ProjectEvaluation 'src\Microsoft.Data.SqlClient\tests\UnitTests\Microsoft.Data.SqlClient.UnitTests.csproj' @(
            "-p:TargetFramework=$Framework", '-p:ReferenceType=Package',
            "-p:PkgMicrosoft_Data_SqlClient=$TestDrive/package"
        )
        $result.Properties._SqlClientPackageTfm | Should -Be $Framework
        $package = @($result.Items.PackageReference | Where-Object Identity -EQ 'Microsoft.Data.SqlClient')
        $package.Count | Should -Be 1
        $package[0].ExcludeAssets | Should -Be 'compile'
        $package[0].GeneratePathProperty | Should -Be 'true'
        $reference = @($result.Items.Reference | Where-Object Identity -EQ 'Microsoft.Data.SqlClient')
        $reference.Count | Should -Be 1
        $reference[0].HintPath.Replace('\', '/') | Should -Be "$($TestDrive.Replace('\', '/'))/package/lib/$Framework/Microsoft.Data.SqlClient.dll"
    }

    It 'defaults Package-mode UnitTests to net10.0 during the outer build' {
        $result = Get-ProjectEvaluation 'src\Microsoft.Data.SqlClient\tests\UnitTests\Microsoft.Data.SqlClient.UnitTests.csproj' @(
            '-p:ReferenceType=Package'
        )
        $result.Properties.TargetFramework | Should -BeNullOrEmpty
        $result.Properties._SqlClientPackageTfm | Should -Be 'net10.0'
    }

    # Framework/Standard targets use the same lowest supported modern dependency
    # band. Assert the major rather than a patch, allowing normal servicing updates.
    It 'uses the .NET 10 dependency band for <Framework>, including preserved framework targets' -ForEach @(
        @{ Framework = 'net462' }
        @{ Framework = 'net10.0' }
        @{ Framework = 'netstandard2.0' }
    ) {
        $result = Get-ProjectEvaluation 'src\Microsoft.Data.SqlClient\ref\Microsoft.Data.SqlClient.csproj' @(
            "-p:TargetFramework=$Framework"
        )
        foreach ($id in @(
            'Microsoft.Extensions.Caching.Memory'
            'Microsoft.Bcl.Cryptography'
            'Microsoft.Bcl.TimeProvider'
            'System.Threading.RateLimiting'
            'System.Configuration.ConfigurationManager'
            'System.Security.Cryptography.Pkcs'
            'System.Diagnostics.DiagnosticSource'
            'System.Text.Json'
            'System.Threading.Channels'
        )) {
            $central = @($result.Items.PackageVersion | Where-Object Identity -EQ $id)
            $central.Count | Should -Be 1
            $central[0].Version | Should -Match '^10\.\d+\.\d+$'
        }
    }

    It 'uses the .NET 10 Hosting and Asn1 dependencies for <Framework> tests' -ForEach @(
        @{ Framework = 'net462' }
        @{ Framework = 'net10.0' }
    ) {
        $result = Get-ProjectEvaluation 'src\Microsoft.Data.SqlClient\tests\UnitTests\Microsoft.Data.SqlClient.UnitTests.csproj' @(
            "-p:TargetFramework=$Framework"
        )
        foreach ($id in @('Microsoft.Extensions.Hosting', 'System.Formats.Asn1')) {
            $dependency = @($result.Items.PackageVersion | Where-Object Identity -EQ $id)
            $dependency.Count | Should -Be 1
            $dependency[0].Version | Should -Match '^10\.\d+\.\d+$'
        }
    }

    # The hand-authored nuspec is independent of project targeting. Detect missing or
    # extra package groups and ensure only Standard uses the unsupported-platform DLL.
    It 'aligns package dependency groups and assembly paths with the declared reference targets' {
        [xml]$nuspec = Get-Content (Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\src\Microsoft.Data.SqlClient.nuspec') -Raw
        $result = Get-ProjectEvaluation 'src\Microsoft.Data.SqlClient\ref\Microsoft.Data.SqlClient.csproj'
        $frameworks = $result.Properties.TargetFrameworks.Split(';') | Sort-Object
        ($nuspec.package.metadata.dependencies.group.targetFramework | Sort-Object) -join ';' |
            Should -Be ($frameworks -join ';')
        ($nuspec.package.metadata.references.group.targetFramework | Sort-Object) -join ';' |
            Should -Be ($frameworks -join ';')
        @($nuspec.package.metadata.frameworkAssemblies.frameworkAssembly.targetFramework | Select-Object -Unique) |
            Should -Be @('net462')
        foreach ($kind in @('lib', 'ref')) {
            $assemblyFiles = @($nuspec.package.files.file | Where-Object {
                $_.src.EndsWith('\Microsoft.Data.SqlClient.dll') -and $_.target.StartsWith("$kind\")
            })
            $assemblyFiles.Count | Should -Be $frameworks.Count
            foreach ($framework in $frameworks) {
                $file = @($assemblyFiles | Where-Object target -EQ "$kind\$framework\")
                $file.Count | Should -Be 1
                $file[0].src | Should -Match ([regex]::Escape("\$framework\Microsoft.Data.SqlClient.dll"))
                $implementation = if ($kind -eq 'ref') { 'Microsoft.Data.SqlClient.ref' }
                    elseif ($framework -eq 'netstandard2.0') { 'Microsoft.Data.SqlClient.notsupported' }
                    else { 'Microsoft.Data.SqlClient' }
                $file[0].src | Should -BeLike "$implementation\*"
            }
        }
    }

    # A successful project build cannot detect stale nuspec dependency versions.
    # Compare the actual per-TFM package references and central versions to the manifest.
    It 'aligns <Framework> driver dependencies and central versions with the package metadata' -ForEach @(
        @{ Framework = 'net462'; Sni = 'Microsoft.Data.SqlClient.SNI' }
        @{ Framework = 'net10.0'; Sni = 'Microsoft.Data.SqlClient.SNI.runtime' }
    ) {
        $result = Get-ProjectEvaluation 'src\Microsoft.Data.SqlClient\src\Microsoft.Data.SqlClient.csproj' @(
            "-p:TargetFramework=$Framework", '-p:ReferenceType=Package'
        )
        [xml]$nuspec = Get-Content (Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\src\Microsoft.Data.SqlClient.nuspec') -Raw
        $dependencies = ($nuspec.package.metadata.dependencies.group | Where-Object targetFramework -EQ $Framework).dependency
        $references = @($result.Items.PackageReference | Where-Object IsImplicitlyDefined -NE 'true')
        if ($Framework -eq 'net462') {
            # SQL CLR types are forwarded to System.Data on .NET Framework, so the sibling
            # project reference does not require a runtime package dependency.
            $references = @($references | Where-Object Identity -NE 'Microsoft.SqlServer.Server')
        }
        ($references.Identity | Sort-Object) -join ';' | Should -Be (($dependencies.id | Sort-Object) -join ';')
        $references.Identity | Should -Contain $Sni
        foreach ($dependency in $dependencies) {
            if ($dependency.version.StartsWith('$')) { continue }
            $central = @($result.Items.PackageVersion | Where-Object Identity -EQ $dependency.id)
            $central.Count | Should -Be 1
            $central[0].Version | Should -Be $dependency.version
        }
    }
}

Describe 'reference assembly baseline framework resolution' {
    # Prefer exact baseline assets, then exercise both previous modern target layouts.
    # Overrides must take precedence even when the baseline also contains exact matches.
    It 'selects <Scenario> assets without building or downloading a baseline' -ForEach @(
        @{
            Scenario = 'matching'
            Available = @('net462', 'net10.0', 'net47', 'net9.0', 'net8.0', 'netstandard2.0')
            FrameworkBaseline = 'net462'
            NetBaseline = 'net10.0'
            Properties = @()
        }
        @{
            Scenario = 'historical net9'
            Available = @('net462', 'net9.0', 'net8.0', 'netstandard2.0')
            FrameworkBaseline = 'net462'
            NetBaseline = 'net9.0'
            Properties = @()
        }
        @{
            Scenario = 'historical net8'
            Available = @('net462', 'net8.0', 'netstandard2.0')
            FrameworkBaseline = 'net462'
            NetBaseline = 'net8.0'
            Properties = @()
        }
        @{
            Scenario = 'explicit override despite matching'
            Available = @('net462', 'net10.0', 'net472', 'net8.0', 'netstandard2.0')
            FrameworkBaseline = 'net472'
            NetBaseline = 'net8.0'
            Properties = @('-p:BaselineNetFrameworkTfm=net472', '-p:BaselineNetTfm=net8.0')
        }
    ) {
        $result = Invoke-RefBaselineProbe $Available $Properties
        $result.ExitCode | Should -Be 0 -Because $result.Output
        $items = @(($result.Output | ConvertFrom-Json).Items._RefTfm)
        ($items.Identity | Sort-Object) -join ';' | Should -Be 'net10.0;net462;netstandard2.0'
        ($items | Where-Object Identity -EQ 'net462').BaselineTfm | Should -Be $FrameworkBaseline
        ($items | Where-Object Identity -EQ 'net10.0').BaselineTfm | Should -Be $NetBaseline
        ($items | Where-Object Identity -EQ 'netstandard2.0').BaselineTfm | Should -Be 'netstandard2.0'
    }

    # Missing assets must fail before ApiCompat runs; silently omitting a comparison
    # would make a partially checked API surface appear compatible.
    It 'fails clearly when the <Scenario> asset is missing' -ForEach @(
        @{
            Scenario = 'Framework default'
            Available = @('net10.0', 'netstandard2.0')
            Missing = 'net462'
            Local = 'net462'
            Properties = @()
        }
        @{
            Scenario = 'modern fallback'
            Available = @('net462', 'netstandard2.0')
            Missing = 'net8.0'
            Local = 'net10.0'
            Properties = @()
        }
        @{
            Scenario = 'preserved Standard'
            Available = @('net462', 'net10.0')
            Missing = 'netstandard2.0'
            Local = 'netstandard2.0'
            Properties = @()
        }
        @{
            Scenario = 'Framework override'
            Available = @('net462', 'net10.0', 'netstandard2.0')
            Missing = 'net472'
            Local = 'net462'
            Properties = @('-p:BaselineNetFrameworkTfm=net472')
        }
        @{
            Scenario = 'modern override'
            Available = @('net462', 'net10.0', 'netstandard2.0')
            Missing = 'net9.0'
            Local = 'net10.0'
            Properties = @('-p:BaselineNetTfm=net9.0')
        }
    ) {
        $result = Invoke-RefBaselineProbe $Available $Properties
        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match ([regex]::Escape("has no ref/$Missing/Microsoft.Data.SqlClient.dll for local $Local"))
    }
}

Describe 'build.proj command-line filters' {
    It 'disables filtering when TestFilters=none is supplied on the command line' {
        $result = (Invoke-BuildProbe @(
            '-getProperty:TestFilters,TestFiltersArgument', '-p:TestFilters=none'
        ) -join "`n") | ConvertFrom-Json

        $result.Properties.TestFilters | Should -BeNullOrEmpty
        $result.Properties.TestFiltersArgument | Should -BeNullOrEmpty
    }

    It 'applies unsigned exclusions to the whole custom OR filter' {
        $result = (Invoke-BuildProbe @(
            '-getProperty:TestFilters,TestFiltersArgument',
            '-p:TestFilters=category=one|category=two'
        ) -join "`n") | ConvertFrom-Json

        $result.Properties.TestFilters |
            Should -Be '(category=one|category=two)&category!=signed'
    }

    It 'combines an OR filter with the manual test set without changing precedence' {
        $arguments = Get-ChildArguments 'TestSqlClientManual' @(
            '-p:TestFilters=category=one|category=two', '-p:TestSet=2',
            '-p:SigningKeyPath=test-key.snk'
        )
        $arguments | Should -Contain '(category=one|category=two)&(Set=2)'
    }

    It 'preserves manual test set selection when general filtering is disabled' {
        $arguments = Get-ChildArguments 'TestSqlClientManual' @(
            '-p:TestFilters=none', '-p:TestSet=2'
        )
        $arguments | Should -Contain 'Set=2'
        $arguments | Should -Not -Contain 'none'
    }
}

Describe 'build.proj dependency packing' {
    It 'forwards each local version pair to the dependency chain for <Target>' -ForEach @(
        @{ Target = 'PackAbstractions' }
        @{ Target = 'PackAzure' }
        @{ Target = 'PackAkvProvider' }
    ) {
        foreach ($run in @('first', 'second')) {
            $familyVersion = "8.0.0-preview1-local-$run"
            $serverVersion = "1.1.0-preview1-local-$run"
            $arguments = Get-ChildArguments $Target @(
                '-p:ReferenceType=Package',
                "-p:PackageVersionSqlClient=$familyVersion",
                "-p:PackageVersionSqlServer=$serverVersion"
            )
            $commandStarts = @(
                for ($i = 0; $i -lt $arguments.Count; $i++) {
                    if ($arguments[$i] -in @('build', 'pack')) { $i }
                }
            )
            $commandStarts.Count | Should -BeGreaterThan 1
            for ($i = 0; $i -lt $commandStarts.Count; $i++) {
                $start = $commandStarts[$i]
                $end = if ($i + 1 -lt $commandStarts.Count) { $commandStarts[$i + 1] } else { $arguments.Count }
                $command = $arguments[$start..($end - 1)]
                $project = Split-Path $command[1] -Leaf
                if ($project -in @(
                    'Logging.csproj', 'Abstractions.csproj', 'Azure.csproj', 'Microsoft.Data.SqlClient.csproj',
                    'Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.csproj'
                )) {
                    $command | Should -Contain "-p:SqlClientPackageVersion=$familyVersion"
                }
                if ($project -in @(
                    'Microsoft.SqlServer.Server.csproj', 'Microsoft.Data.SqlClient.csproj',
                    'Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.csproj'
                )) {
                    $command | Should -Contain "-p:SqlServerPackageVersion=$serverVersion"
                }
            }
        }
    }

    It 'packs dependencies before <Target> in Package mode' -ForEach @(
        @{ Target = 'PackAbstractions'; Projects = @('Logging.csproj', 'Abstractions.csproj') }
        @{ Target = 'PackAzure'; Projects = @('Logging.csproj', 'Abstractions.csproj', 'Azure.csproj') }
        @{
            Target = 'PackAkvProvider'
            Projects = @(
                'Logging.csproj', 'Abstractions.csproj', 'Microsoft.SqlServer.Server.csproj',
                'Microsoft.Data.SqlClient.csproj', 'Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.csproj'
            )
        }
    ) {
        $arguments = Get-ChildArguments $Target @('-p:ReferenceType=Package')
        $packedProjects = @(
            for ($i = 0; $i -lt $arguments.Count - 1; $i++) {
                if ($arguments[$i] -eq 'pack') { Split-Path $arguments[$i + 1] -Leaf }
            }
        )

        ($packedProjects -join ',') | Should -Be ($Projects -join ',')
    }

    It 'does not pack dependencies for <Target> with <Property>' -ForEach @(
        @{ Target = 'PackAbstractions'; Property = '-p:PackBuild=false' }
        @{ Target = 'PackAzure'; Property = '-p:PackBuild=false' }
        @{ Target = 'PackAkvProvider'; Property = '-p:PackBuild=false' }
        @{ Target = 'PackAbstractions'; Property = '-p:SkipDependencyPack=true' }
        @{ Target = 'PackAzure'; Property = '-p:SkipDependencyPack=true' }
        @{ Target = 'PackAkvProvider'; Property = '-p:SkipDependencyPack=true' }
        @{ Target = 'PackAbstractions'; Property = '-p:ReferenceType=Project' }
        @{ Target = 'PackAzure'; Property = '-p:ReferenceType=Project' }
        @{ Target = 'PackAkvProvider'; Property = '-p:ReferenceType=Project' }
    ) {
        $properties = @($Property)
        if ($Property -ne '-p:ReferenceType=Project') { $properties += '-p:ReferenceType=Package' }
        $arguments = Get-ChildArguments $Target $properties

        @($arguments | Where-Object { $_ -eq 'pack' }).Count | Should -Be 1
        $arguments | Should -Not -Contain 'build'
        if ($Property -eq '-p:PackBuild=false') {
            $arguments | Should -Contain '--no-build'
        }
    }
}

Describe 'test project family versions' {
    It 'uses the forwarded family version for central dependencies in <Target>' -ForEach @(
        @{ Target = 'BuildSqlClientTestsFunctional' }
        @{ Target = 'BuildSqlClientTestsManual' }
        @{ Target = 'BuildSqlClientTestsPerformance' }
        @{ Target = 'BuildSqlClientTestsStress' }
    ) {
        $familyVersion = '8.1.2-review'
        $arguments = Get-ChildArguments $Target @(
            '-p:ReferenceType=Package', "-p:PackageVersionSqlClient=$familyVersion"
        )
        $arguments | Should -Contain '-p:ReferenceType=Package'
        $arguments | Should -Contain "-p:SqlClientPackageVersion=$familyVersion"

        # Evaluate the actual consumer with the properties emitted by the orchestrator.
        $properties = @($arguments | Where-Object { $_ -like '-p:*' })
        $output = & $dotnet msbuild $arguments[1] -nologo @properties `
            -p:TargetFramework=net10.0 -getItem:PackageVersion 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Test project evaluation failed:`n$($output -join [Environment]::NewLine)"
        }
        $versions = ($output -join "`n" | ConvertFrom-Json).Items.PackageVersion
        $packageIds = @(
            'Microsoft.Data.SqlClient', 'Microsoft.Data.SqlClient.Extensions.Abstractions',
            'Microsoft.Data.SqlClient.Internal.Logging'
        )
        $expectedVersion = "[$familyVersion, 9.0.0)"
        if ($Target -eq 'BuildSqlClientTestsStress') {
            # Stress has its own central file; Logging and Abstractions are transitive dependencies.
            $packageIds = @('Microsoft.Data.SqlClient')
            $expectedVersion = $familyVersion
            $versions.Identity | Should -Not -Contain 'Microsoft.Data.SqlClient.Extensions.Abstractions'
            $versions.Identity | Should -Not -Contain 'Microsoft.Data.SqlClient.Internal.Logging'
        }
        foreach ($id in $packageIds) {
            $version = @($versions | Where-Object { $_.Identity -eq $id })
            $version.Count | Should -Be 1
            $version[0].Version | Should -Be $expectedVersion
        }
    }
}

Describe 'local package version freshness' {
    It 'restores changed sibling contents with a new version after a same-version repack' {
        $fixture = Join-Path $TestDrive 'nuget freshness'
        $feed = Join-Path $fixture 'feed'
        $package = Join-Path $fixture 'package'
        New-Item -ItemType Directory -Path $feed, "$package/build" -Force | Out-Null
        @'
<configuration>
  <packageSources><clear /><add key="fixture" value="feed" /></packageSources>
  <auditSources><clear /><add key="fixture" value="feed" /></auditSources>
</configuration>
'@ | Set-Content (Join-Path $fixture 'NuGet.config')
        @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RestorePackagesPath>$(MSBuildThisFileDirectory)cache</RestorePackagesPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BuildReview.Sibling" Version="[$(SiblingVersion)]" />
  </ItemGroup>
</Project>
'@ | Set-Content (Join-Path $fixture 'Consumer.csproj')

        foreach ($step in @(
            @{ Version = '1.0.0-local-first'; Content = 'original'; Restored = 'original' }
            @{ Version = '1.0.0-local-first'; Content = 'changed'; Restored = 'original' }
            @{ Version = '1.0.0-local-second'; Content = 'changed'; Restored = 'changed' }
        )) {
            @"
<package>
  <metadata>
    <id>BuildReview.Sibling</id>
    <version>$($step.Version)</version>
    <authors>SqlClient tests</authors>
    <description>Local restore regression fixture.</description>
  </metadata>
</package>
"@ | Set-Content (Join-Path $package 'BuildReview.Sibling.nuspec')
            "<Project><PropertyGroup><BuildReviewMarker>$($step.Content)</BuildReviewMarker></PropertyGroup></Project>" |
                Set-Content (Join-Path $package 'build/BuildReview.Sibling.props')
            $archive = Join-Path $feed "BuildReview.Sibling.$($step.Version).nupkg"
            if (Test-Path $archive) { Remove-Item $archive }
            [System.IO.Compression.ZipFile]::CreateFromDirectory($package, $archive)

            $output = & $dotnet restore (Join-Path $fixture 'Consumer.csproj') `
                --configfile (Join-Path $fixture 'NuGet.config') `
                "-p:SiblingVersion=$($step.Version)" --verbosity quiet 2>&1
            if ($LASTEXITCODE -ne 0) {
                throw "Fixture restore failed:`n$($output -join [Environment]::NewLine)"
            }
            [xml]$restored = Get-Content (Join-Path $fixture "cache/buildreview.sibling/$($step.Version)/build/BuildReview.Sibling.props") -Raw
            $restored.Project.PropertyGroup.BuildReviewMarker | Should -Be $step.Restored
            $assets = Get-Content (Join-Path $fixture 'obj/project.assets.json') -Raw | ConvertFrom-Json
            $assets.libraries.PSObject.Properties.Name | Should -Contain "BuildReview.Sibling/$($step.Version)"
        }
    }
}

Describe 'build.proj project path quoting' {
    It 'passes the project path as one argument for <Target>' -ForEach @(
        @{ Target = 'BuildSqlClientRef'; Property = 'SqlClientRefProjectPath' }
        @{ Target = 'BuildSqlClientImpl'; Property = 'SqlClientProjectPath' }
        @{ Target = 'BuildLogging'; Property = 'LoggingProjectPath' }
        @{ Target = 'PackLogging'; Property = 'LoggingProjectPath' }
        @{ Target = 'BuildSqlServer'; Property = 'SqlServerProjectPath' }
        @{ Target = 'PackSqlServer'; Property = 'SqlServerProjectPath' }
    ) {
        $projectPath = Join-Path $TestDrive 'repo with  spaces/Example.csproj'
        $arguments = Get-ChildArguments $Target @("-p:$Property=$projectPath")

        $arguments | Should -Contain $projectPath
    }
}

Describe 'SqlClient build helper path quoting' {
    It 'resolves relative documentation paths and trims before copying build outputs' {
        $documentation = 'obj/docs with  spaces.xml'
        $arguments = Get-ProjectTargetArguments `
            'src/Microsoft.Data.SqlClient/ref/Microsoft.Data.SqlClient.csproj' `
            @('_CheckPwshToolRestored', 'TrimDocsForIntelliSense') `
            @("-p:RepoRoot=$repoRoot/", "-p:DocumentationFile=$documentation",
              '-p:GenerateDocumentationFile=true') `
            -EntryTarget 'Build'

        $arguments | Should -Contain (Join-Path $TestDrive $documentation)
        $arguments | Should -Contain '-File'
        $arguments[-1] | Should -Be 'CopyFilesToOutputDirectory'
    }

    It 'passes the documentation script and XML paths as separate arguments' {
        $root = "$TestDrive/repo with  spaces/"
        $documentation = "${root}output/SqlClient.xml"
        $arguments = Get-ProjectTargetArguments `
            'src/Microsoft.Data.SqlClient/ref/Microsoft.Data.SqlClient.csproj' `
            @('_CheckPwshToolRestored', 'TrimDocsForIntelliSense') `
            @("-p:RepoRoot=$repoRoot/", "-p:DocumentationFile=$documentation",
              '-p:GenerateDocumentationFile=true')

        $arguments | Should -Contain '-File'
        $arguments | Should -Contain "$repoRoot/tools/intellisense/TrimDocs.ps1"
        $arguments | Should -Contain ([System.IO.Path]::GetFullPath($documentation))
    }

    It 'passes the GenAPI reference assembly as one argument' {
        $referenceAssembly = Join-Path $TestDrive 'ref with  spaces.dll'
        $genApi = Join-Path $TestDrive 'GenAPI with  spaces.dll'
        New-Item -ItemType File -Path $referenceAssembly, $genApi | Out-Null

        $arguments = Get-ProjectTargetArguments `
            'src/Microsoft.Data.SqlClient/notsupported/Microsoft.Data.SqlClient.csproj' `
            @('GenerateNotSupportedSource') `
            @("-p:RefArtifactPath=$referenceAssembly", "-p:GenApiPath=$genApi",
              '-p:NotSupportedSourceFile=generated.cs')

        $arguments | Should -Contain $referenceAssembly
        $arguments | Should -Contain $genApi
    }
}

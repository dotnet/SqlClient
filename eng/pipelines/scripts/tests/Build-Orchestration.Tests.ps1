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

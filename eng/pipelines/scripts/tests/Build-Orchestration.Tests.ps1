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

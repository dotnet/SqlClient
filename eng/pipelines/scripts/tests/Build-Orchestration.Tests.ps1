# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Verifies the release branch's explicit PowerShell restore targets and pipeline wiring.
.DESCRIPTION
Runs MSBuild against a local PowerShell stub, without installing modules or building the driver.
#>

BeforeAll {
    $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
    $buildProject = Join-Path $repoRoot 'build.proj'
    $standaloneTarget = Join-Path $repoRoot 'tools\targets\RestorePowerShellModules.targets'
    $dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop |
        Select-Object -First 1).Source
    $originalPath = $env:PATH
    $originalExitCode = $env:SQLCLIENT_TEST_RESTORE_EXIT_CODE
    $stubDirectory = Join-Path $TestDrive 'PowerShell stub'
    New-Item -ItemType Directory -Path $stubDirectory | Out-Null

    if ($IsWindows) {
        @'
@echo off
echo CWD:"%CD%"
:next
if [%1]==[] exit /b %SQLCLIENT_TEST_RESTORE_EXIT_CODE%
echo ARG:"%~1"
shift
goto next
'@ | Set-Content (Join-Path $stubDirectory 'pwsh.cmd')
    }
    else {
        @'
#!/bin/sh
printf 'CWD:%s\n' "$PWD"
printf 'ARG:%s\n' "$@"
exit "$SQLCLIENT_TEST_RESTORE_EXIT_CODE"
'@ | Set-Content (Join-Path $stubDirectory 'pwsh')
        & chmod +x (Join-Path $stubDirectory 'pwsh')
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not make the PowerShell stub executable.'
        }
    }

    $env:PATH = "$stubDirectory$([IO.Path]::PathSeparator)$originalPath"

    function Invoke-RestoreProbe {
        param([string] $Project)

        $output = & $dotnet build $Project --no-restore -nologo -v:normal `
            -t:RestorePowerShellModules 2>&1
        $exitCode = $LASTEXITCODE
        [pscustomobject]@{
            ExitCode = $exitCode
            Output = $output -join "`n"
            Arguments = @($output | ForEach-Object {
                if ("$_".Trim() -match '^ARG:(.*)$') { $Matches[1].Trim('"') }
            })
            WorkingDirectories = @($output | ForEach-Object {
                if ("$_".Trim() -match '^CWD:(.*)$') { $Matches[1].Trim('"') }
            })
        }
    }
}

AfterAll {
    $env:PATH = $originalPath
    $env:SQLCLIENT_TEST_RESTORE_EXIT_CODE = $originalExitCode
}

Describe 'PowerShell module restore orchestration' {
    BeforeEach {
        $env:SQLCLIENT_TEST_RESTORE_EXIT_CODE = '0'
    }

    It 'runs only the restore script from build.proj' {
        $result = Invoke-RestoreProbe $buildProject

        $result.ExitCode | Should -Be 0
        $result.Arguments.Count | Should -Be 4
        $result.Arguments[0..2] -join '|' | Should -Be '-NoProfile|-NonInteractive|-File'
        [IO.Path]::GetFullPath($result.Arguments[3]) |
            Should -Be (Join-Path $repoRoot 'eng\scripts\Restore-PowerShellModules.ps1')
        $result.WorkingDirectories.Count | Should -Be 1
        $result.WorkingDirectories[0] | Should -Be $repoRoot
    }

    It 'runs the standalone target from another working directory' {
        Push-Location $TestDrive
        try {
            $result = Invoke-RestoreProbe $standaloneTarget
        }
        finally {
            Pop-Location
        }

        $result.ExitCode | Should -Be 0
        $result.Arguments.Count | Should -Be 4
        $result.WorkingDirectories.Count | Should -Be 1
        $result.WorkingDirectories[0] | Should -Be $repoRoot
    }

    It 'propagates a failed restore exit code' {
        $env:SQLCLIENT_TEST_RESTORE_EXIT_CODE = '17'
        $result = Invoke-RestoreProbe $standaloneTarget

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match 'exited with code 17'
    }

    It 'keeps module restoration explicit rather than adding build or test dependencies' {
        [xml] $project = Get-Content $buildProject -Raw
        $project.Project.Target.DependsOnTargets -join ';' |
            Should -Not -Match 'RestorePowerShellModules'

        [xml] $target = Get-Content $standaloneTarget -Raw
        $target.Project.Target.BeforeTargets | Should -BeNullOrEmpty
        $target.Project.Target.AfterTargets | Should -BeNullOrEmpty
        $target.Project.Target.DependsOnTargets | Should -BeNullOrEmpty
    }

    It 'restores modules before common CI prebuild and test setup' {
        $pipeline = Get-Content (Join-Path $repoRoot `
            'eng\pipelines\common\templates\jobs\ci-run-tests-job.yml') -Raw
        $restore = $pipeline.IndexOf('/eng/pipelines/common/steps/restore-powershell-modules.yml')

        $restore | Should -BeGreaterThan $pipeline.IndexOf('/eng/pipelines/steps/install-dotnet.yml')
        $restore | Should -BeLessThan $pipeline.IndexOf('${{ if ne(parameters.prebuildSteps,')
        [regex]::Matches($pipeline, 'restore-powershell-modules.yml').Count | Should -Be 1
    }

    It 'restores modules in all three Kerberos jobs without inline module installation' {
        $pipeline = Get-Content (Join-Path $repoRoot `
            'eng\pipelines\ci\kerberos\sqlclient-ci-kerberos-pipeline.yml') -Raw

        [regex]::Matches($pipeline, 'restore-powershell-modules.yml').Count | Should -Be 3
        $pipeline | Should -Not -Match 'Install-Module'
        $pipeline | Should -Match 'Import-Module SqlServer'
        $pipeline.LastIndexOf('restore-powershell-modules.yml') |
            Should -BeLessThan $pipeline.IndexOf('Import-Module SqlServer')
    }

    It 'includes module configuration changes in project and package PR triggers' {
        foreach ($kind in @('project', 'package')) {
            $pipeline = Get-Content (Join-Path $repoRoot `
                "eng\pipelines\dotnet-sqlclient-ci-$kind-reference-pipeline.yml") -Raw
            $trigger = $pipeline.Substring($pipeline.IndexOf('pr:'),
                $pipeline.IndexOf('trigger:') - $pipeline.IndexOf('pr:'))

            $trigger | Should -Match '(?m)^\s+- PowerShell\.config\s*$'
            $trigger | Should -Match '(?m)^\s+- powershell-modules\.json\s*$'
        }
    }

    It 'uses the native OpenSSL CLI without installing the unused module' {
        $script = Get-Content (Join-Path $repoRoot `
            'src\Microsoft.Data.SqlClient\tests\ManualTests\makepfxcert.ps1') -Raw

        $script | Should -Not -Match 'Install-Module|PSGallery'
        $script | Should -Match 'openssl version'
    }
}

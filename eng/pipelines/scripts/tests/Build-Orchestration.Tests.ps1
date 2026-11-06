# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Verifies explicit PowerShell module restore and release-branch pipeline wiring.
#>
BeforeAll {
    $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
    $dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
    $stubDirectory = Join-Path $TestDrive 'dotnet stub'
    New-Item -ItemType Directory -Path $stubDirectory | Out-Null
    if ($IsWindows) {
        @'
@echo off
echo CWD:%CD%
for %%a in (%*) do echo ARG:%%~a
'@ | Set-Content (Join-Path $stubDirectory 'dotnet.cmd')
    }
    else {
        @'
#!/bin/sh
echo "CWD:$PWD"
for arg in "$@"; do echo "ARG:$arg"; done
'@ | Set-Content (Join-Path $stubDirectory 'dotnet')
        & chmod +x (Join-Path $stubDirectory 'dotnet')
        if ($LASTEXITCODE -ne 0) { throw 'Failed to make the dotnet stub executable.' }
    }
}

Describe 'PowerShell module restore orchestration' {
    It 'runs only the restored tool through <Project>' -ForEach @(
        @{ Project = 'build.proj' }
        @{ Project = 'tools/targets/RestorePowerShellModules.targets' }
    ) {
        Push-Location $TestDrive
        try {
            $output = & $dotnet build (Join-Path $repoRoot $Project) --no-restore -nologo -v:normal `
                -t:RestorePowerShellModules "-p:DotnetPath=$stubDirectory/" 2>&1
            $LASTEXITCODE | Should -Be 0 -Because ($output -join "`n")
        }
        finally {
            Pop-Location
        }

        $arguments = @($output | ForEach-Object {
            if ("$_".Trim() -match '^ARG:(.*)$') { $Matches[1] }
        })
        $arguments.Count | Should -Be 8
        $arguments[0..6] -join '|' |
            Should -Be 'tool|run|pwsh|--|-NoProfile|-NonInteractive|-File'
        [IO.Path]::GetFullPath($arguments[7]) |
            Should -Be (Join-Path $repoRoot 'eng/scripts/Restore-PowerShellModules.ps1')
        $workingDirectory = @($output | ForEach-Object {
            if ("$_".Trim() -match '^CWD:(.*)$') { $Matches[1] }
        })
        $workingDirectory | Should -HaveCount 1
        $workingDirectory[0] | Should -Be $repoRoot
    }

    It 'propagates a failed restore tool exit code' {
        $directory = Join-Path $TestDrive 'failed restore'
        New-Item -ItemType Directory -Path $directory | Out-Null
        if ($IsWindows) {
            "@echo off`nexit /b 17" | Set-Content (Join-Path $directory 'dotnet.cmd')
        }
        else {
            "#!/bin/sh`nexit 17" | Set-Content (Join-Path $directory 'dotnet')
            & chmod +x (Join-Path $directory 'dotnet')
            $LASTEXITCODE | Should -Be 0
        }

        $output = & $dotnet msbuild (Join-Path $repoRoot 'build.proj') -nologo -v:quiet `
            -t:RestorePowerShellModules "-p:DotnetPath=$directory/" 2>&1
        $LASTEXITCODE | Should -Not -Be 0
        $output -join "`n" | Should -Match 'exited with code 17'
    }

    It 'restores tools before modules in the shared prerequisite step' {
        $template = Get-Content (Join-Path $repoRoot `
            'eng/pipelines/common/templates/steps/restore-powershell-modules-step.yml') -Raw
        $tools = $template.IndexOf('dotnet tool restore')
        $modules = $template.IndexOf('dotnet build tools/targets/RestorePowerShellModules.targets')
        $tools | Should -BeGreaterOrEqual 0
        $modules | Should -BeGreaterThan $tools
        (Get-Content (Join-Path $repoRoot 'dotnet-tools.json') -Raw |
            ConvertFrom-Json).tools.powershell.version | Should -Be '7.6.2'
    }

    It 'restores modules before prebuild steps for Project and Package test jobs' {
        $template = Get-Content (Join-Path $repoRoot `
            'eng/pipelines/common/templates/jobs/ci-run-tests-job.yml') -Raw
        $sdk = $template.IndexOf('/eng/pipelines/steps/install-dotnet.yml@self')
        $restore = $template.IndexOf(
            '/eng/pipelines/common/templates/steps/restore-powershell-modules-step.yml@self')
        $prebuild = $template.IndexOf('- ${{ if ne(parameters.prebuildSteps')
        $template | Should -Match (
            '(?m)^  - template: /eng/pipelines/common/templates/steps/' +
            'restore-powershell-modules-step.yml@self\r?$')
        $restore | Should -BeGreaterThan $sdk
        $prebuild | Should -BeGreaterThan $restore
    }

    It 'restores modules before Linux Kerberos setup and SQL connectivity' {
        $template = Get-Content (Join-Path $repoRoot `
            'eng/pipelines/ci/kerberos/sqlclient-ci-kerberos-pipeline.yml') -Raw
        $linux = $template.Substring($template.IndexOf('- stage: linux'))
        $sdk = $linux.IndexOf('/eng/pipelines/steps/install-dotnet.yml@self')
        $restore = $linux.IndexOf('restore-powershell-modules-step.yml@self')
        $restore | Should -BeGreaterThan $sdk
        $linux.IndexOf('linux-init-step.yml@self') | Should -BeGreaterThan $restore
        $linux.IndexOf('Import-Module SqlServer') | Should -BeGreaterThan $restore
        $linux | Should -Not -Match 'Install-Module'
    }

    It 'validates module configuration changes in <Pipeline>' -ForEach @(
        @{ Pipeline = 'sqlclient-pr-project-ref-pipeline.yml' }
        @{ Pipeline = 'sqlclient-pr-package-ref-pipeline.yml' }
    ) {
        $template = Get-Content (Join-Path $repoRoot "eng/pipelines/$Pipeline") -Raw
        foreach ($path in @('eng/scripts/*', 'PowerShell.config', 'powershell-modules.json')) {
            $template | Should -Match (
                '(?m)^      - ' + [regex]::Escape($path) + '\r?$')
        }
    }

    It 'keeps certificate generation on the native OpenSSL CLI without installing modules' {
        $certificate = Get-Content (Join-Path $repoRoot `
            'src/Microsoft.Data.SqlClient/tests/ManualTests/makepfxcert.ps1') -Raw
        $certificate | Should -Not -Match 'Install-Module|PSGallery'
        $certificate | Should -Match 'openssl version'
        $manifest = Get-Content (Join-Path $repoRoot 'powershell-modules.json') -Raw |
            ConvertFrom-Json
        $manifest.modules.PSObject.Properties.Name | Should -Not -Contain 'OpenSSL'
    }
}

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

BeforeAll {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
    $buildProject = Join-Path $repoRoot 'build.proj'
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    $stubDirectory = Join-Path $TestDrive 'dotnet stub'
    New-Item -ItemType Directory -Path $stubDirectory | Out-Null
    if ($IsWindows) {
        @'
@echo off
echo CWD:"%CD%"
:next
if [%1]==[] exit /b 0
echo ARG:"%~1"
shift
goto next
'@ | Set-Content (Join-Path $stubDirectory 'dotnet.cmd')
    }
    else {
        @'
#!/bin/sh
printf 'CWD:%s\n' "$PWD"
printf 'ARG:%s\n' "$@"
'@ | Set-Content (Join-Path $stubDirectory 'dotnet')
        & chmod +x (Join-Path $stubDirectory 'dotnet')
        if ($LASTEXITCODE -ne 0) { throw 'Failed to make the dotnet stub executable.' }
    }

    function Get-ChildArguments {
        param(
            [string] $Target,
            [string[]] $Properties = @()
        )

        $output = & $dotnet msbuild $buildProject -nologo -v:normal "-t:$Target" `
            "-p:DotnetPath=$stubDirectory/" @Properties 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "MSBuild probe failed:`n$($output -join [Environment]::NewLine)"
        }
        foreach ($line in $output) {
            if ("$line".Trim() -match '^ARG:(.*)$') { $Matches[1].Trim('"') }
        }
    }
}

Describe 'build.proj PowerShell module restore' {
    It 'runs the restored PowerShell tool without building or testing any projects' {
        $arguments = @(Get-ChildArguments 'RestorePowerShellModules')

        $arguments[0..6] -join '|' |
            Should -Be 'tool|run|pwsh|--|-NoProfile|-NonInteractive|-File'
        [IO.Path]::GetFullPath($arguments[7]) |
            Should -Be (Join-Path $repoRoot 'eng\scripts\Restore-PowerShellModules.ps1')
        $arguments.Count | Should -Be 8
    }

    It 'runs the standalone target from another working directory' {
        $target = Join-Path $repoRoot 'tools\targets\RestorePowerShellModules.targets'
        Push-Location $TestDrive
        try {
            $output = & $dotnet build $target -nologo -v:normal -t:RestorePowerShellModules `
                "-p:DotnetPath=$stubDirectory/" 2>&1
            $LASTEXITCODE | Should -Be 0
        }
        finally {
            Pop-Location
        }
        $workingDirectory = @($output | ForEach-Object {
            if ("$_".Trim() -match '^CWD:(.*)$') { $Matches[1].Trim('"') }
        })
        $workingDirectory.Count | Should -Be 1
        $workingDirectory[0].TrimEnd('\', '/') | Should -Be $repoRoot.TrimEnd('\', '/')
    }

    It 'does not restore modules implicitly during manual tests' {
        $arguments = @(Get-ChildArguments 'TestSqlClientManual' @('-p:TestFramework=net8.0'))

        $arguments | Should -Not -Contain 'tool'
        $arguments | Should -Not -Contain 'pwsh'
        $arguments | Should -Contain 'test'
    }

    It 'propagates a failed restore tool exit code' {
        $directory = Join-Path $TestDrive 'failed module restore'
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        if ($IsWindows) {
            "@echo off`nexit /b 17" | Set-Content (Join-Path $directory 'dotnet.cmd')
        }
        else {
            "#!/bin/sh`nexit 17" | Set-Content (Join-Path $directory 'dotnet')
            & chmod +x (Join-Path $directory 'dotnet')
            $LASTEXITCODE | Should -Be 0
        }
        $output = & $dotnet msbuild $buildProject -nologo -v:quiet -t:RestorePowerShellModules `
            "-p:DotnetPath=$directory/" 2>&1

        $LASTEXITCODE | Should -Not -Be 0
        $output -join "`n" | Should -Match 'exited with code 17'
    }
}

Describe 'PowerShell pipeline prerequisites' {
    It 'includes module configuration and helpers in <Pipeline> PR path filters' -ForEach @(
        @{ Pipeline = 'eng/pipelines/sqlclient-pr-project-ref-pipeline.yml'; Disabled = $false }
        @{ Pipeline = 'eng/pipelines/sqlclient-pr-package-ref-pipeline.yml'; Disabled = $true }
        @{ Pipeline = 'eng/pipelines/pr/sqlclient-pr-pipeline.yml'; Disabled = $false }
    ) {
        $content = Get-Content (Join-Path $repoRoot $Pipeline) -Raw
        if ($Disabled) {
            $content | Should -Match '(?m)^pr: none\s*$'
            $content = $content -replace '(?m)^# ?', ''
        }
        $includePattern = '(?ms)^pr:\r?\n.*?^  paths:\r?\n    include:\r?\n' +
            '(?<paths>(?:      - [^\r\n]+\r?\n)+)'
        $content | Should -Match $includePattern
        $includedPaths = [regex]::Match($content, $includePattern).Groups['paths'].Value `
            -split '\r?\n' | ForEach-Object { $_ -replace '^      - ', '' }

        foreach ($path in @('PowerShell.config', 'powershell-modules.json', 'eng/scripts/*')) {
            $includedPaths | Should -Contain $path
        }
    }

    It 'restores modules after tools and before setup in <Job>' -ForEach @(
        @{
            Job = 'eng/pipelines/common/templates/jobs/ci-run-tests-job.yml'
            Setup = 'parameters.prebuildSteps'
        }
        @{
            Job = 'eng/pipelines/pr/jobs/test-sqlclientmanual-job.yml'
            Setup = 'configure-sqlserver-step.yml'
        }
        @{
            Job = 'eng/pipelines/ci/kerberos/sqlclient-ci-kerberos-job.yml'
            Setup = 'linux-setup-step.yml'
        }
    ) {
        $content = Get-Content (Join-Path $repoRoot $Job) -Raw
        $toolsIndex = $content.IndexOf('restore-dotnet-tools.yml@self')
        $modulesIndex = $content.IndexOf('restore-powershell-modules.yml@self')

        $toolsIndex | Should -BeGreaterOrEqual 0
        $modulesIndex | Should -BeGreaterThan $toolsIndex
        $content.IndexOf($Setup, $modulesIndex) | Should -BeGreaterThan $modulesIndex
    }

    It 'retains all standalone restore inputs when aligning to an older package commit' {
        $content = Get-Content (Join-Path $repoRoot `
            'eng/pipelines/common/steps/align-source-with-upstream-step.yml') -Raw
        $restoreLine = ($content -split '\r?\n' |
            Where-Object { $_ -match 'git checkout --force \$pipelineSourceSha --' }).Trim()
        foreach ($path in @(
            'eng/pipelines',
            'eng/scripts/Register-SqlClientPowerShellRepository.ps1',
            'eng/scripts/Restore-PowerShellModules.ps1',
            'PowerShell.config',
            'powershell-modules.json',
            'tools/targets/RestorePowerShellModules.targets'
        )) {
            $restoreLine.Split(' ') | Should -Contain $path
            Join-Path $repoRoot $path | Should -Exist
        }
    }

    It 'keeps module installation out of consumers and does not restore the OpenSSL wrapper' {
        foreach ($path in @(
            'eng/pipelines/ci/kerberos/linux-setup-step.yml',
            'src/Microsoft.Data.SqlClient/tests/ManualTests/makepfxcert.ps1'
        )) {
            Get-Content (Join-Path $repoRoot $path) -Raw |
                Should -Not -Match 'Install-Module|Install-PSResource|PSGallery'
        }
        $manifest = Get-Content (Join-Path $repoRoot 'powershell-modules.json') -Raw |
            ConvertFrom-Json
        $manifest.modules.PSObject.Properties.Name | Should -Not -Contain 'OpenSSL'
    }
}

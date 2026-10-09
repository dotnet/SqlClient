<#
.SYNOPSIS
    Regression checks for arguments passed through DotNetCoreCLI tasks.

.DESCRIPTION
    DotNetCoreCLI parses its argument string without a shell: double quotes group arguments,
    but single quotes become part of MSBuild property values. Pipeline variable names must also
    follow the repository's convention of avoiding {COMMAND}ARGUMENTS names.
    These checks read templates only; no SDK, YAML module, network, or test directory is needed.
    Authentication checks also pin the dedicated stage's wiring, platform coverage and setup order.
#>

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

Describe 'PR build.proj arguments in <RelativePath>' -ForEach @(
    @{ RelativePath = 'pr/steps/pack-buildproj-step.yml' }
    @{ RelativePath = 'pr/jobs/test-buildproj-job.yml' }
    @{ RelativePath = 'pr/jobs/test-sqlclientmanual-job.yml' }
) {
    BeforeAll {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $template = Get-Content -LiteralPath (Join-Path $pipelineRoot $RelativePath) -Raw
    }

    It 'passes the build number without literal apostrophes' {
        $template.Contains('-p:BuildNumber="$(Build.BuildNumber)"') | Should -BeTrue
        ($template -match "-p:BuildNumber='") | Should -BeFalse
    }

    It 'passes the prerelease suffix without literal apostrophes' {
        $template.Contains('-p:BuildSuffix="${{ parameters.buildSuffix }}"') | Should -BeTrue
        ($template -match "-p:BuildSuffix='") | Should -BeFalse
    }
}

Describe 'PR test result paths in <RelativePath>' -ForEach @(
    @{ RelativePath = 'pr/jobs/test-buildproj-job.yml' }
    @{ RelativePath = 'pr/jobs/test-sqlclientmanual-job.yml' }
) {
    It 'keeps a result path containing spaces in one argument' {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $template = Get-Content -LiteralPath (Join-Path $pipelineRoot $RelativePath) -Raw

        $template.Contains('-p:TestResultsFolderPath="${{ variables.testResultsPath }}"') | Should -BeTrue
    }
}

Describe 'CI dotnet argument variables in <RelativePath>' -ForEach @(
    @{ RelativePath = 'jobs/test-abstractions-package-ci-job.yml' }
    @{ RelativePath = 'jobs/test-azure-package-ci-job.yml' }
) {
    It 'uses an argument variable that follows the repository naming convention' {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $template = Get-Content -LiteralPath (Join-Path $pipelineRoot $RelativePath) -Raw

        ($template -match '(?im)^\s*-\s*name:\s*(build|test|run)Arguments\s*$') | Should -BeFalse
        ($template -match '\$\((build|test|run)Arguments\)') | Should -BeFalse
        ($template -match '(?m)^\s*-\s*name:\s*dotnetBuildOpts\s*$') | Should -BeTrue
        ([regex]::Matches($template, '\$\(dotnetBuildOpts\)')).Count | Should -Be 5
    }
}

Describe 'PR authentication validation stage' {
    BeforeAll {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $pipeline = Get-Content -LiteralPath (Join-Path $pipelineRoot 'pr/sqlclient-pr-pipeline.yml') -Raw
        $testJob = Get-Content -LiteralPath (Join-Path $pipelineRoot 'pr/jobs/test-buildproj-job.yml') -Raw
        $stage = Get-Content -LiteralPath (Join-Path $pipelineRoot 'pr/stages/authentication-validation-stage.yml') -Raw
    }

    It 'is wired into the modern PR pipeline' {
        $pipeline | Should -Match '/eng/pipelines/pr/stages/authentication-validation-stage\.yml@self'
        $stage | Should -Match '(?m)^  - stage: test_authentication\s*$'
    }

    It 'runs independently without package artifacts or SQL Server secrets' {
        $stage | Should -Match '(?m)^\s+dependsOn: \[\]\s*$'
        $stage | Should -Not -Match 'DownloadPipelineArtifact|stageNamePack|stageNameSecrets'
    }

    It 'keeps script-driven checks out of the shared test job' {
        $testJob | Should -Not -Match 'RunAuthenticationTests\.ps1|RunVersionChecks\.ps1'
    }

    It 'runs Linux configuration, trimmed, NativeAOT and family-version checks' {
        $linux = [regex]::Match(
            $stage,
            '(?ms)^      - job: test_authentication_linux\r?\n(?<job>.*?)(?=^      - job:|\z)')

        $linux.Success | Should -BeTrue
        $linux.Groups['job'].Value | Should -Match 'RunAuthenticationTests\.ps1\s+-Publish'
        $linux.Groups['job'].Value | Should -Match 'RunVersionChecks\.ps1'
    }

    It 'runs Windows net462 checks without Linux publishing or duplicate version checks' {
        $windows = [regex]::Match(
            $stage,
            '(?ms)^      - job: test_authentication_netfx\r?\n(?<job>.*?)(?=^      - job:|\z)')

        $windows.Success | Should -BeTrue
        $windows.Groups['job'].Value | Should -Match 'RunAuthenticationTests\.ps1\s+-Framework net462'
        $windows.Groups['job'].Value | Should -Not -Match '-Publish|RunVersionChecks\.ps1'
    }

    It 'installs the SDK and restores tools before scripts in <Job>' -ForEach @(
        @{ Job = 'test_authentication_linux' }
        @{ Job = 'test_authentication_netfx' }
    ) {
        $pattern = '(?ms)^      - job: ' + $Job + '\r?\n(?<job>.*?)(?=^      - job:|\z)'
        $jobContent = [regex]::Match($stage, $pattern).Groups['job'].Value
        $installIndex = $jobContent.IndexOf('/eng/pipelines/common/steps/install-dotnet.yml@self')
        $restoreIndex = $jobContent.IndexOf('/eng/pipelines/common/steps/restore-dotnet-tools.yml@self')
        $scriptIndex = $jobContent.IndexOf('RunAuthenticationTests.ps1')

        $installIndex | Should -BeGreaterOrEqual 0
        $restoreIndex | Should -BeGreaterThan $installIndex
        $scriptIndex | Should -BeGreaterThan $restoreIndex
    }
}

<#
.SYNOPSIS
    Regression checks for arguments passed through DotNetCoreCLI tasks.

.DESCRIPTION
    DotNetCoreCLI parses its argument string without a shell: double quotes group arguments,
    but single quotes become part of MSBuild property values. Pipeline variable names must also
    follow the repository's convention of avoiding {COMMAND}ARGUMENTS names.
    These checks read templates only; no SDK, YAML module, network, or test directory is needed.
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

Describe 'signed-package manual-test jobs in <RelativePath>' -ForEach @(
    @{ RelativePath = 'ci/managed-instance/sqlclient-ci-managed-instance-job.yml' }
    @{ RelativePath = 'ci/kerberos/sqlclient-ci-kerberos-job.yml' }
) {
    It 'downloads the shared secure test key and provides its path to build.proj' {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $template = Get-Content -LiteralPath (Join-Path $pipelineRoot $RelativePath) -Raw

        $template | Should -Match '(?ms)- task: DownloadSecureFile@1\s+displayName: Download Test Signing Key\s+name: testKeyFile\s+inputs:\s+secureFile: sqlclient-test-key\.snk'
        $template.Contains('-p:TestSigningKeyPath="$(testKeyFile.secureFilePath)"') |
            Should -BeTrue
        $template | Should -Not -Match 'TransactionTest\.snk'
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

Describe 'perf checkout paths in <RelativePath>' -ForEach @(
    @{ RelativePath = 'perf/sqlclient-perf-pipeline.yml' }
    @{ RelativePath = 'perf/sqlclient-perf-pr-pipeline.yml' }
    @{ RelativePath = 'perf/sqlclient-perf-experiment.yml' }
) {
    BeforeAll {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $template = Get-Content -LiteralPath (Join-Path $pipelineRoot $RelativePath) -Raw
    }

    It 'handles both owner-qualified GitHub names and Azure Repos names' {
        # Act / Assert
        $template.Contains("`${{ if contains(variables['Build.Repository.Name'], '/') }}:") |
            Should -BeTrue
        $template.Contains("value: `$(Build.SourcesDirectory)/`${{ split(variables['Build.Repository.Name'], '/')[1] }}") |
            Should -BeTrue
        $template.Contains('value: $(Build.SourcesDirectory)/$(Build.Repository.Name)') |
            Should -BeTrue
        ([regex]::Matches($template, 'name: PerfSourceRoot')).Count | Should -Be 2
    }

    It 'uses the resolved checkout for the VM source and post-test scripts' {
        # Act / Assert
        $template.Contains('testRootDir: $(PerfSourceRoot)') | Should -BeTrue
        $template.Contains('filePath: $(PerfSourceRoot)/eng/pipelines/perf/scripts/show_perf_results.sh') |
            Should -BeTrue
        $template | Should -Not -Match '\$\(Build\.SourcesDirectory\)/dotnet-sqlclient'
        if ($RelativePath -eq 'perf/sqlclient-perf-pipeline.yml') {
            $template.Contains('filePath: $(PerfSourceRoot)/eng/pipelines/perf/scripts/translate_results_to_kusto.sh') |
                Should -BeTrue
            $template.Contains('REPO_DIR: $(PerfSourceRoot)') | Should -BeTrue
            $template.Contains('scriptsDir="$(PerfSourceRoot)/eng/pipelines/perf/scripts"') |
                Should -BeTrue
        }
    }
}

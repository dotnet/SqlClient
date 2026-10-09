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

    Describe 'authentication CI matrix' {
        BeforeAll {
            $pipelineRoot = Join-Path $PSScriptRoot '../..'
            $template = Get-Content (Join-Path $pipelineRoot 'pr/stages/authentication-validation-stage.yml') -Raw
            $core = Get-Content (Join-Path $pipelineRoot 'dotnet-sqlclient-ci-core.yml') -Raw
        }

        It 'runs both modes on Linux and Windows through standard package-preparing targets' {
            $template | Should -Match 'default: \[Project, Package\]'
            $template | Should -Match 'each referenceType in parameters.referenceTypes'
            ([regex]::Matches($template, 'build.proj -t:TestAuthentication')).Count | Should -Be 2
            ([regex]::Matches($template, '-p:ReferenceType=\$\{\{ referenceType \}\}')).Count | Should -Be 2
            $template | Should -Match '-p:TestFramework=net10.0'
            $template | Should -Match '-p:TestFramework=net462'
            ([regex]::Matches($template, '-p:AuthenticationPublish=true')).Count | Should -Be 1
            $template | Should -Not -Match 'SkipDependencyPack|download:|Abstractions/test/RunAuthenticationTests'
            $template | Should -Not -Match '(?i)(run|build|test)Arguments'
        }

        It 'isolates caches and versions and passes the pool and configuration parameters' {
            $template | Should -Match 'authentication-linux-\$\{\{ referenceType \}\}/nuget'
            $template | Should -Match 'authentication-windows-\$\{\{ referenceType \}\}/nuget'
            ([regex]::Matches($template, '-p:PackageVersionSqlClient=8.0.0-auth.\$\(Build.BuildId\)')).Count | Should -Be 2
            $core | Should -Match '/eng/pipelines/pr/stages/authentication-validation-stage.yml@self'
            $core | Should -Match 'poolName: \$\{\{ parameters.defaultPoolName \}\}'
            $core | Should -Match 'linuxPoolImage: ADO-UB24'
            $core | Should -Match 'windowsPoolImage: ADO-Win25'
            $core | Should -Match 'buildConfiguration: \$\{\{ parameters.buildConfiguration \}\}'
        }
    }
}

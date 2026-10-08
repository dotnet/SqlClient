<#
.SYNOPSIS
    Regression checks for PR and CI platform selection.

.DESCRIPTION
    Checks that PR validation excludes macOS jobs and their packaging dependencies,
    while shared templates retain macOS coverage by default for CI.
    These checks read templates only; no SDK, YAML module, or network is needed.
#>

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

Describe 'PR platform selection in <RelativePath>' -ForEach @(
    @{ RelativePath = 'sqlclient-pr-project-ref-pipeline.yml' }
    @{ RelativePath = 'sqlclient-pr-package-ref-pipeline.yml' }
) {
    It 'disables all inherited macOS tests' {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $template = Get-Content -LiteralPath (Join-Path $pipelineRoot $RelativePath) -Raw

        $template | Should -Match '(?m)^    runMacOSTests: false\s*$'
    }
}

Describe 'Shared platform selection in <RelativePath>' -ForEach @(
    @{ RelativePath = 'dotnet-sqlclient-ci-core.yml'; ConditionalCount = 1 }
    @{ RelativePath = 'stages/build-abstractions-package-ci-stage.yml'; ConditionalCount = 2 }
    @{ RelativePath = 'stages/build-azure-package-ci-stage.yml'; ConditionalCount = 2 }
) {
    BeforeAll {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $template = Get-Content -LiteralPath (Join-Path $pipelineRoot $RelativePath) -Raw
    }

    It 'keeps macOS tests enabled by default for CI' {
        $template | Should -Match '(?m)^  - name: runMacOSTests\r?\n    type: boolean\r?\n    default: true\s*$'
    }

    It 'retains the macOS test definition for CI' {
        $template | Should -Match '(?m)^ +(?:poolImage|MacOSLatest_Sql25): macos-latest\s*$'
    }

    It 'excludes macOS jobs and dependencies when disabled' {
        # Match each compile-time guard and its indented contents, not sibling jobs.
        $conditional = '(?m)^(?<indent> +)(?:- )?\$\{\{ if eq\(parameters\.runMacOSTests, true\) \}\}:\r?\n(?:(?:\k<indent> +[^\r\n]*|[ \t]*)\r?\n)*'
        ([regex]::Matches($template, $conditional)).Count | Should -Be $ConditionalCount
        $withoutMacOS = [regex]::Replace($template, $conditional, '')

        $withoutMacOS | Should -Not -Match 'macos-latest|mac_sql25|test_(abstractions|azure)_package_job_macos'
        $withoutMacOS | Should -Match 'ADO-UB24'
        $withoutMacOS | Should -Match 'ADO-(MMS25-SQL25|Win25)'
    }
}

Describe 'CI core platform forwarding' {
    It 'passes the macOS switch to both extension build stages' {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $template = Get-Content -LiteralPath (Join-Path $pipelineRoot 'dotnet-sqlclient-ci-core.yml') -Raw

        foreach ($package in @('abstractions', 'azure')) {
            $stage = [regex]::Match($template, "(?ms)^  - template: /eng/pipelines/stages/build-$package-package-ci-stage\.yml@self\r?\n.*?(?=^  - template:|\z)").Value
            $stage | Should -Match 'runMacOSTests: \$\{\{ parameters\.runMacOSTests \}\}'
        }
    }
}

Describe 'CI platform selection in <RelativePath>' -ForEach @(
    @{ RelativePath = 'dotnet-sqlclient-ci-project-reference-pipeline.yml' }
    @{ RelativePath = 'dotnet-sqlclient-ci-package-reference-pipeline.yml' }
) {
    It 'retains the shared default macOS coverage' {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $template = Get-Content -LiteralPath (Join-Path $pipelineRoot $RelativePath) -Raw

        $template | Should -Not -Match 'runMacOSTests: false'
    }
}

Describe 'Unified PR platform selection' {
    It 'runs only Windows and Linux tests' {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $template = Get-Content -LiteralPath (Join-Path $pipelineRoot 'pr/sqlclient-pr-pipeline.yml') -Raw
        $platforms = [regex]::Matches($template, '(?m)^        operatingSystem: "([^"]+)"')

        $platforms.Count | Should -BeGreaterThan 0
        $operatingSystems = @($platforms | ForEach-Object { $_.Groups[1].Value })
        $operatingSystems | Should -Contain 'Windows'
        $operatingSystems | Should -Contain 'Linux'
        foreach ($platform in $platforms) {
            $platform.Groups[1].Value | Should -BeIn @('Windows', 'Linux')
        }
    }
}

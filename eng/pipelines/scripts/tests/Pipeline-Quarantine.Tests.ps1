# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

Describe 'Quarantine filtering in <RelativePath>' -ForEach @(
    @{ RelativePath = 'common/templates/steps/run-all-tests-step.yml' }
) {
    It 'excludes failing tests even when they are also flaky' {
        $pipelineRoot = Join-Path $PSScriptRoot '../..'
        $template = Get-Content -LiteralPath (Join-Path $pipelineRoot $RelativePath) -Raw

        $filters = [regex]::Matches(
            $template, '(?:-p:(?:TestFilters|Filter)=|--filter )"([^"]*category=flaky[^"]*)"')

        $filters.Count | Should -BeGreaterThan 0
        foreach ($filter in $filters) {
            $filter.Groups[1].Value | Should -Match '(^|&)category!=failing(&|$)'
        }
    }
}

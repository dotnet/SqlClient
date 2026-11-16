<#
.SYNOPSIS
    Regression checks for XML documentation validation in the unified PR pipeline.

.DESCRIPTION
    Checks parallel scheduling and parameterized artifact routing across retries without
    requiring a YAML module or a running pipeline. Shared script tests cover validator behavior.
#>

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

BeforeAll {
    $pipelineRoot = Join-Path $PSScriptRoot '../..'
    $pipeline = Get-Content (Join-Path $pipelineRoot 'pr/sqlclient-pr-pipeline.yml') -Raw
    $validation = Get-Content (Join-Path $pipelineRoot 'pr/stages/validate-xml-docs-stage.yml') -Raw
    $pack = Get-Content (Join-Path $pipelineRoot 'pr/stages/pack-stage.yml') -Raw
    $tests = Get-Content (Join-Path $pipelineRoot 'pr/stages/test-stages.yml') -Raw
    $coverage = Get-Content (Join-Path $pipelineRoot 'pr/stages/collect-coverage-stage.yml') -Raw
}

Describe 'PR XML documentation validation' {
    It 'adds a stage depending only on packing, independently of tests and secrets' {
        # Act
        $dependencies = [regex]::Match(
            $validation, '(?ms)^    dependsOn:\r?\n(.*?)(?=^    \w)'
        ).Groups[1].Value.Trim()

        # Assert
        $pipeline | Should -Match (
            'template: /eng/pipelines/pr/stages/validate-xml-docs-stage.yml@self\r?\n' +
            '    parameters:\r?\n      stageNamePack: \$\{\{ variables.stageNamePackDebug \}\}'
        )
        $dependencies | Should -Be '- ${{ parameters.stageNamePack }}'
        $tests | Should -Not -Match 'validate_xml_docs'
        $coverage | Should -Not -Match 'validate_xml_docs'
    }

    It 'downloads packages from the pack job attempt that published them' {
        # Act
        $artifactOutput = [regex]::Match(
            $pack, '(?ms)^          - pwsh: \|\r?\n(.*?)(?=^          - publish:)'
        ).Value

        # Assert
        $artifactOutput | Should -Match (
            [regex]::Escape(
                '${{ parameters.packArtifactBaseName }}_${{ parameters.buildConfiguration }}_attempt$(System.JobAttempt)'
            )
        )
        $artifactOutput | Should -Match 'variable=artifactName;isOutput=true'
        $artifactOutput | Should -Match '(?m)^            name: packOutput$'
        $pack | Should -Match 'artifact: \$\(packOutput.artifactName\)'
        $pipeline | Should -Match ([regex]::Escape(
            "stageDependencies." + '${{ variables.stageNamePackDebug }}' +
            ".pack_job.outputs['packOutput.artifactName']"
        ))
        $validation | Should -Not -Match 'stageDependencies'
        $validation | Should -Match 'value: \$\{\{ parameters.packArtifactName \}\}'
        $validation | Should -Match 'artifactName: \$\(packArtifactName\)'
        $validation | Should -Match "itemPattern: '\*\*/\*\.nupkg'"
        $validation | Should -Match 'targetPath: \$\(xmlDocsPackagesRoot\)'
    }

    It 'validates every snippet directory configured by OneBranch' {
        # Arrange
        $oneBranch = Get-Content (
            Join-Path $pipelineRoot 'onebranch/stages/build-stages.yml'
        ) -Raw
        $expected = @([regex]::Matches($oneBranch, "documentationSnippetsPath: '([^']+)'") |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)

        # Act
        $snippetBlock = [regex]::Match(
            $validation, '(?ms)^              snippetsDirectory:\r?\n(.*?)(?=^              \w)'
        ).Groups[1].Value
        $actual = @([regex]::Matches($snippetBlock, '(?m)^                - (.+)\r?$') |
            ForEach-Object { $_.Groups[1].Value.Trim() } | Sort-Object -Unique)

        # Assert
        $expected.Count | Should -Be 3
        $actual.Count | Should -Be $expected.Count
        Compare-Object $actual $expected | Should -BeNullOrEmpty
    }
}

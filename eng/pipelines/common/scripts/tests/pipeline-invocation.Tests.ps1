<#
.SYNOPSIS
    Checks the shared XML validation step and its PowerShell task invocation.

.DESCRIPTION
    The PowerShell task dot-sources scripts under -Command. These checks ensure that the
    template emits a bare -ReportOnly switch and that the script accepts that invocation.
#>

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '../validate-xml-docs.ps1'
    $stepPath = Join-Path $PSScriptRoot '../../steps/validate-xml-docs-step.yml'
    $step = Get-Content -LiteralPath $stepPath -Raw

    function Invoke-AsPipelineTask {
        param([Parameter(Mandatory)][string]$CommandLine)

        $wrapper = Join-Path $TestDrive ([guid]::NewGuid().ToString('n') + '.ps1')
        Set-Content -LiteralPath $wrapper -Value $CommandLine -Encoding utf8
        # A local tool may be hosted by dotnet rather than the pwsh executable.
        $pwshPath = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
        & $pwshPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Unrestricted `
            -Command ". '$wrapper'" *> $null
        return $LASTEXITCODE
    }
}

Describe 'Shared XML validation step' {
    It 'never passes a switch using the -Switch:Value form' {
        $step | Should -Not -Match '-\w+:\$?\{\{'
        $step | Should -Not -Match '-ReportOnly:'
    }

    It 'appends -ReportOnly as a bare token' {
        $step | Should -Match "\+= ' -ReportOnly'"
    }

    It 'builds one argument list rather than repeating it per branch' {
        $step | Should -Match 'task\.setvariable variable=\w+Arguments'
        $step | Should -Match '(?m)^\s*arguments:\s*\$\(\w+Arguments\)\s*$'
    }

    It 'rejects an unexpected failOnValidationError value rather than assuming report-only' {
        $step | Should -Match 'Unexpected failOnValidationError value'
    }

    It 'validates multiple snippet directories from the composed task arguments: <InvalidIndex>' `
        -ForEach @(@{ InvalidIndex = 1 }, @{ InvalidIndex = 2 }) {
        # Arrange
        $directories = @(0..2 | ForEach-Object {
            $directory = Join-Path $TestDrive "snippets $_"
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            $cref = if ($_ -eq $InvalidIndex) { 'T:System.Byte[]' } else { 'T:System.String' }
            "<docs><members name=`"S`"><S><see cref=`"$cref`" /></S></members></docs>" |
                Set-Content -LiteralPath (Join-Path $directory 'Sample.xml') -Encoding utf8
            $directory
        })
        $report = Join-Path $TestDrive 'multiple-snippets.json'
        $compose = [regex]::Match(
            $step, '(?ms)^  - pwsh: \|\r?\n(.*?)(?=^    displayName:)'
        ).Groups[1].Value
        $values = @{
            '${{ parameters.reportPath }}' = $report
            '${{ join('','', parameters.failOn) }}' = 'error'
            '${{ join(''","'', parameters.snippetsDirectory) }}' = $directories -join '","'
            '${{ parameters.documentationPath }}' = ''
            '${{ parameters.packagesPath }}' = ''
            '${{ parameters.extractPath }}' = ''
            '${{ parameters.dependencyDocumentationPath }}' = ''
            '${{ parameters.projectPath }}' = ''
            '${{ parameters.projectSearchRoot }}' = ''
            '${{ parameters.failOnValidationError }}' = 'False'
        }
        foreach ($entry in $values.GetEnumerator()) {
            $compose = $compose.Replace($entry.Key, $entry.Value)
        }

        # Act
        $output = & ([scriptblock]::Create($compose)) 6>&1
        $arguments = $output -replace '^##vso\[task.setvariable variable=validateXmlDocsArguments\]', ''
        $exitCode = Invoke-AsPipelineTask -CommandLine ". '$scriptPath' $arguments"

        # Assert
        $exitCode | Should -Be 0
        $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        $result.FilesValidated | Should -Be 3
        $result.Findings.Count | Should -Be 1
        $result.Findings[0].Category | Should -Be 'invalid-docid'
        $result.Findings[0].Path | Should -Be (Join-Path $directories[$InvalidIndex] 'Sample.xml')
    }
}

Describe 'Shared XML validator under the pipeline invocation form' {
    BeforeEach {
        $snippets = Join-Path $TestDrive ([guid]::NewGuid().ToString('n'))
        New-Item -ItemType Directory -Path $snippets | Out-Null
        '<docs><members name="S"><S><see cref="T:System.Byte[]" /></S></members></docs>' |
            Set-Content -LiteralPath (Join-Path $snippets 'Sample.xml') -Encoding utf8
        $report = Join-Path $TestDrive ([guid]::NewGuid().ToString('n') + '.json')
        $line = ". '$scriptPath' -ReportPath `"$report`" -FailOn `"error`" " +
            "-SnippetsDirectory `"$snippets`" -DocumentationPath `"`" -PackagesPath `"`" " +
            "-ExtractPath `"`""
    }

    It 'accepts a bare -ReportOnly switch when dot-sourced' {
        # Act
        $exitCode = Invoke-AsPipelineTask -CommandLine "$line -ReportOnly"

        # Assert
        $exitCode | Should -Be 0
        Test-Path -LiteralPath $report | Should -BeTrue
    }

    It 'still fails when dot-sourced without -ReportOnly' {
        # Act
        $exitCode = Invoke-AsPipelineTask -CommandLine $line

        # Assert
        $exitCode | Should -Not -Be 0
    }
}

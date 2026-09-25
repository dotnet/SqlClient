<#
.SYNOPSIS
    Pester tests for the OneBranch validation step templates and their script invocation form.

.DESCRIPTION
    The PowerShell task dot-sources a filePath script under -Command rather than running it with
    -File. The two parse switch arguments differently: -Command requires a bare token or an
    explicit $true, and rejects the -Switch:Value form that -File accepts. A step template that
    renders -ReportOnly:True therefore fails at argument binding before the script runs at all.

    These tests pin both halves of that contract: the templates must not emit a -Switch:Value form,
    and the scripts must accept a bare switch when dot-sourced the way the task does.
#>

BeforeAll {
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..' '..' '..')).Path
    $script:jobsPath = Join-Path $script:repoRoot 'eng/pipelines/onebranch/jobs'
    $script:stagesPath = Join-Path $script:repoRoot 'eng/pipelines/onebranch/stages'
    $script:stepsPath = Join-Path $script:repoRoot 'eng/pipelines/onebranch/steps'
    $script:scriptsPath = Join-Path $script:repoRoot 'eng/pipelines/onebranch/scripts'

    # Runs a command line the way the PowerShell task does: a generated wrapper, dot-sourced by
    # pwsh -Command. Returns the exit code.
    function Invoke-AsPipelineTask {
        param([Parameter(Mandatory)][string]$CommandLine)

        $wrapper = Join-Path $TestDrive ([guid]::NewGuid().ToString('n') + '.ps1')
        Set-Content -LiteralPath $wrapper -Value $CommandLine -Encoding utf8

        $pwshPath = (Get-Process -Id $PID).Path
        & $pwshPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Unrestricted `
            -Command ". '$wrapper'" *> $null
        return $LASTEXITCODE
    }

    function New-LocalizationResources {
        param([switch]$WithFindings)

        $path = Join-Path $TestDrive ([guid]::NewGuid().ToString('n'))
        New-Item -ItemType Directory -Path $path | Out-Null
        '<root><data name="G"><value>Hello</value></data></root>' |
            Set-Content -LiteralPath (Join-Path $path 'Strings.resx') -Encoding utf8
        $french = if ($WithFindings) { 'Hello' } else { 'Bonjour' }
        "<root><data name=`"G`"><value>$french</value></data></root>" |
            Set-Content -LiteralPath (Join-Path $path 'Strings.fr.resx') -Encoding utf8
        return $path
    }
}

Describe 'Validation step templates' {
    It 'never passes a switch using the -Switch:Value form' -ForEach @(
        @{ Template = 'validate-xml-docs-step.yml' }
        @{ Template = 'validate-localization-step.yml' }
        @{ Template = 'validate-packages-step.yml' }
    ) {
        # -Switch:Value binds as a string under -Command and fails before the script runs.
        $content = Get-Content -LiteralPath (Join-Path $script:stepsPath $Template) -Raw
        $content | Should -Not -Match '-\w+:\$?\{\{'
        $content | Should -Not -Match '-ReportOnly:'
    }

    It 'appends -ReportOnly as a bare token' -ForEach @(
        @{ Template = 'validate-xml-docs-step.yml' }
        @{ Template = 'validate-localization-step.yml' }
        @{ Template = 'validate-packages-step.yml' }
    ) {
        $content = Get-Content -LiteralPath (Join-Path $script:stepsPath $Template) -Raw
        $content | Should -Match "\+= ' -ReportOnly'"
    }

    It 'builds one argument list rather than repeating it per branch' -ForEach @(
        @{ Template = 'validate-xml-docs-step.yml' }
        @{ Template = 'validate-localization-step.yml' }
        @{ Template = 'validate-packages-step.yml' }
    ) {
        # A duplicated arguments block drifts silently when only one copy is updated, so the
        # validation script's arguments must come from a single composed variable. Counting
        # 'arguments:' keys would be wrong here: a template may invoke other tasks that carry
        # their own unrelated arguments.
        $content = Get-Content -LiteralPath (Join-Path $script:stepsPath $Template) -Raw
        $content | Should -Match 'task\.setvariable variable=\w+Arguments'
        $content | Should -Match '(?m)^\s*arguments:\s*\$\(\w+Arguments\)\s*$'
    }

    It 'rejects an unexpected failOnValidationError value rather than assuming report-only' -ForEach @(
        @{ Template = 'validate-xml-docs-step.yml' }
        @{ Template = 'validate-localization-step.yml' }
        @{ Template = 'validate-packages-step.yml' }
    ) {
        # Defaulting an unrecognised value to report-only would silently disable gating.
        $content = Get-Content -LiteralPath (Join-Path $script:stepsPath $Template) -Raw
        $content | Should -Match 'Unexpected failOnValidationError value'
    }

    It 'enables source validation from the configured snippet path' {
        $content = Get-Content -LiteralPath (Join-Path $script:jobsPath 'build-buildproj-job.yml') -Raw

        $content | Should -Match "\$\{\{ if ne\(parameters\.documentationSnippetsPath, ''\) \}\}"
        $content | Should -Match "snippetsDirectory: '\$\{\{ parameters\.documentationSnippetsPath \}\}'"
    }

    It 'configures every snippet-consuming project with an existing directory' {
        $content = Get-Content -LiteralPath (Join-Path $script:stagesPath 'build-stages.yml') -Raw
        $expectedPaths = @(
            'doc/snippets'
            'src/Microsoft.Data.SqlClient.Extensions/Abstractions/doc'
            'src/Microsoft.Data.SqlClient.Extensions/Azure/doc'
        )
        $matches = [regex]::Matches(
            $content,
            "documentationSnippetsPath: '\`$\(REPO_ROOT\)/([^']+)'")
        $configuredPaths = @($matches | ForEach-Object { $_.Groups[1].Value })

        $configuredPaths.Count | Should -Be 4
        foreach ($path in $expectedPaths) {
            $configuredPaths | Should -Contain $path
            Test-Path -LiteralPath (Join-Path $script:repoRoot $path) | Should -BeTrue
        }
    }
}

Describe 'Validation scripts under the pipeline invocation form' {
    It 'accepts a bare -ReportOnly switch when dot-sourced: validate-xml-docs.ps1' {
        $target = Join-Path $script:scriptsPath 'validate-xml-docs.ps1'
        $snippets = Join-Path $TestDrive ([guid]::NewGuid().ToString('n'))
        New-Item -ItemType Directory -Path $snippets | Out-Null
        '<docs><members name="S"><S><see cref="T:System.Byte[]" /></S></members></docs>' |
            Set-Content -LiteralPath (Join-Path $snippets 'Sample.xml') -Encoding utf8
        $report = Join-Path $TestDrive ([guid]::NewGuid().ToString('n') + '.json')

        $line = ". '$target' -ReportPath `"$report`" -FailOn `"error`" " +
            "-SnippetsDirectory `"$snippets`" -DocumentationPath `"`" -PackagesPath `"`" " +
            "-ExtractPath `"`" -ReportOnly"

        Invoke-AsPipelineTask -CommandLine $line | Should -Be 0
        Test-Path -LiteralPath $report | Should -BeTrue
    }

    It 'still fails when dot-sourced without -ReportOnly: validate-xml-docs.ps1' {
        $target = Join-Path $script:scriptsPath 'validate-xml-docs.ps1'
        $snippets = Join-Path $TestDrive ([guid]::NewGuid().ToString('n'))
        New-Item -ItemType Directory -Path $snippets | Out-Null
        '<docs><members name="S"><S><see cref="T:System.Byte[]" /></S></members></docs>' |
            Set-Content -LiteralPath (Join-Path $snippets 'Sample.xml') -Encoding utf8
        $report = Join-Path $TestDrive ([guid]::NewGuid().ToString('n') + '.json')

        $line = ". '$target' -ReportPath `"$report`" -FailOn `"error`" " +
            "-SnippetsDirectory `"$snippets`" -DocumentationPath `"`" -PackagesPath `"`" " +
            "-ExtractPath `"`""

        Invoke-AsPipelineTask -CommandLine $line | Should -Not -Be 0
    }

    It 'accepts a bare -ReportOnly switch when dot-sourced: validate-localization.ps1' {
        $target = Join-Path $script:scriptsPath 'validate-localization.ps1'
        $resources = New-LocalizationResources -WithFindings

        Invoke-AsPipelineTask -CommandLine ". '$target' -ResourcesDirectory `"$resources`" -ReportOnly" |
            Should -Be 0
    }

    It 'still fails when dot-sourced without -ReportOnly: validate-localization.ps1' {
        $target = Join-Path $script:scriptsPath 'validate-localization.ps1'
        $resources = New-LocalizationResources -WithFindings

        Invoke-AsPipelineTask -CommandLine ". '$target' -ResourcesDirectory `"$resources`"" |
            Should -Not -Be 0
    }
}

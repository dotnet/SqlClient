# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Verifies the release branch's explicit PowerShell restore entry points and consumer ordering.
#>

BeforeAll {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
}

Describe 'PowerShell repository dynamic parameter binding' {
    BeforeAll {
        function Get-PSResourceRepository {
            [CmdletBinding()]
            param()
        }

        function Register-PSResourceRepository {
            [CmdletBinding()]
            param(
                [Parameter(Position = 0)]
                [string] $Name,
                [Parameter(Position = 1)]
                [uri] $Uri,
                [switch] $Trusted
            )

            dynamicparam {
                $parameters = [System.Management.Automation.RuntimeDefinedParameterDictionary]::new()
                if ($Uri) {
                    $attributes = [System.Collections.ObjectModel.Collection[System.Attribute]]::new()
                    $attributes.Add([System.Management.Automation.ParameterAttribute]::new())
                    $parameter = [System.Management.Automation.RuntimeDefinedParameter]::new(
                        'CredentialProvider', [string], $attributes)
                    $parameters.Add('CredentialProvider', $parameter)
                }

                $parameters
            }

            process {
                if ($PSBoundParameters.CredentialProvider -ne 'None') {
                    throw 'Anonymous registration must explicitly disable the credential provider.'
                }
            }
        }
    }

    It 'binds Name and Uri before discovering the dynamic CredentialProvider parameter' {
        $result = @(& (Join-Path $repoRoot 'eng/scripts/Register-SqlClientPowerShellRepository.ps1') `
            -RepositoryName SqlClient)
        $result.Count | Should -Be 1
        $result[0] | Should -Be 'SqlClient'
    }
}

Describe 'PowerShell module restore wiring' {
    It 'validates manifest-only changes in the <Reference> PR pipeline' -ForEach @(
        @{ Reference = 'project' }
        @{ Reference = 'package' }
    ) {
        $pipeline = Get-Content `
            (Join-Path $repoRoot "eng/pipelines/dotnet-sqlclient-ci-$Reference-reference-pipeline.yml") -Raw
        $prIncludes = [regex]::Match($pipeline, '(?s)pr:.*?paths:\s+include:(.*?)exclude:').Groups[1].Value
        $prIncludes | Should -Match '(?m)^\s+- PowerShell\.config\s*$'
        $prIncludes | Should -Match '(?m)^\s+- powershell-modules\.json\s*$'
    }

    It 'exposes restore without adding it to ordinary driver build dependencies' {
        [xml] $build = Get-Content (Join-Path $repoRoot 'build.proj') -Raw
        @($build.Project.Import.Project) | Should -Contain `
            '$(ToolsDir)targets\RestorePowerShellModules.targets'
        @($build.Project.Target.DependsOnTargets) -join ';' |
            Should -Not -Match 'RestorePowerShellModules'
    }

    It 'uses the installed pwsh from a standalone target with a quoted script path' {
        [xml] $targets = Get-Content `
            (Join-Path $repoRoot 'tools/targets/RestorePowerShellModules.targets') -Raw
        $targets.Project.Target.Name | Should -Be 'RestorePowerShellModules'
        $targets.Project.Target.Exec.Command | Should -Be `
            'pwsh -NoProfile -NonInteractive -File "$(_PowerShellModulesRepoRoot)eng/scripts/Restore-PowerShellModules.ps1"'
        $targets.Project.Target.Exec.WorkingDirectory | Should -Be '$(_PowerShellModulesRepoRoot)'
    }

    It 'restores modules after SDK installation and before common CI prebuild steps' {
        $job = Get-Content `
            (Join-Path $repoRoot 'eng/pipelines/common/templates/jobs/ci-run-tests-job.yml') -Raw
        $restore = $job.IndexOf('template: /eng/pipelines/common/steps/restore-powershell-modules.yml')
        $restore | Should -BeGreaterThan $job.IndexOf('template: /eng/pipelines/steps/install-dotnet.yml')
        $restore | Should -BeLessThan $job.IndexOf("- " + '${{ if ne(parameters.prebuildSteps')
    }

    It 'restores modules once in each existing Kerberos test job before SQL connectivity' {
        $pipeline = Get-Content `
            (Join-Path $repoRoot 'eng/pipelines/ci/kerberos/sqlclient-ci-kerberos-pipeline.yml') -Raw
        $jobs = $pipeline -split '(?m)^      - job: '
        $jobs.Count | Should -Be 4
        foreach ($job in $jobs[1..3]) {
            $restore = 'template: /eng/pipelines/common/steps/restore-powershell-modules.yml'
            [regex]::Matches($job, [regex]::Escape($restore)).Count | Should -Be 1
            $job.IndexOf($restore) | Should -BeGreaterThan `
                $job.IndexOf('template: /eng/pipelines/steps/install-dotnet.yml')
        }

        $pipeline | Should -Not -Match 'Install-Module'
        $jobs[3].IndexOf($restore) | Should -BeLessThan $jobs[3].IndexOf('Import-Module SqlServer')
    }

    It 'does not restore the unused OpenSSL wrapper or install modules during certificate setup' {
        $manifest = Get-Content (Join-Path $repoRoot 'powershell-modules.json') -Raw | ConvertFrom-Json
        $manifest.modules.PSObject.Properties.Name | Should -Not -Contain 'OpenSSL'
        $certificate = Get-Content `
            (Join-Path $repoRoot 'src/Microsoft.Data.SqlClient/tests/ManualTests/makepfxcert.ps1') -Raw
        $certificate | Should -Not -Match 'Install-(Module|PSResource)'
        $certificate | Should -Match 'openssl version'
    }
}

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

Describe 'Kerberos pipeline test configuration' {
    It 'preserves Kerberos credentials and SNI selection for <JobName> (managed SNI: <ManagedSni>)' -ForEach @(
        @{ JobName = 'windows_netcore'; ManagedSni = $true }
        @{ JobName = 'windows_netcore'; ManagedSni = $false }
        @{ JobName = 'windows_netfx'; ManagedSni = $false }
        @{ JobName = 'linux'; ManagedSni = $false }
    ) {
        $pipeline = Get-Content `
            (Join-Path $repoRoot 'eng/pipelines/ci/kerberos/sqlclient-ci-kerberos-pipeline.yml') -Raw
        $jobs = $pipeline -split '(?m)^      - job: '
        $job = @($jobs | Where-Object { $_ -match "^$JobName\r?\n" })
        $job.Count | Should -Be 1
        $configuration = [regex]::Match($job[0],
            '(?ms)^          - pwsh: \|\r?\n(?<script>.*?)^            workingDirectory:')
        $configuration.Success | Should -BeTrue

        $fixture = Join-Path $TestDrive ([guid]::NewGuid().ToString())
        New-Item -ItemType Directory -Path $fixture | Out-Null
        Copy-Item (Join-Path $repoRoot `
            'src/Microsoft.Data.SqlClient/tests/tools/Microsoft.Data.SqlClient.TestUtilities/config.default.json') `
            (Join-Path $fixture 'config.default.json')

        $environment = @{
            REMOTE_TCP_CONN_STRING = 'Server=tcp:test.invalid;Integrated Security=true'
            REMOTE_NP_CONN_STRING = 'Server=np:test.invalid;Integrated Security=true'
            KERBEROS_DOMAIN_USER = 'fixture-user'
            KERBEROS_DOMAIN_PASSWORD = [guid]::NewGuid().ToString()
            MANAGED_SNI = [string] $ManagedSni
        }
        $originalEnvironment = @{}
        foreach ($name in $environment.Keys) {
            $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        }

        Push-Location $fixture
        try {
            foreach ($name in $environment.Keys) {
                [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process')
            }

            & ([scriptblock]::Create($configuration.Groups['script'].Value))
            $config = Get-Content 'config.json' -Raw | ConvertFrom-Json
            $config.KerberosDomainUser | Should -Be $environment.KERBEROS_DOMAIN_USER
            $config.KerberosDomainPassword | Should -Be $environment.KERBEROS_DOMAIN_PASSWORD
            $config.TCPConnectionString | Should -Be $environment.REMOTE_TCP_CONN_STRING
            $config.NPConnectionString | Should -Be $environment.REMOTE_NP_CONN_STRING
            $config.SupportsIntegratedSecurity | Should -BeTrue
            $config.UseManagedSNIOnWindows | Should -Be $ManagedSni
        }
        finally {
            Pop-Location
            foreach ($name in $originalEnvironment.Keys) {
                [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name], 'Process')
            }
        }

        $job[0] | Should -Match '(?m)^\s+KERBEROS_DOMAIN_USER: \$\(KerberosDomainUser\)\s*$'
        $job[0] | Should -Match '(?m)^\s+KERBEROS_DOMAIN_PASSWORD: \$\(KerberosDomainPassword\)\s*$'
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

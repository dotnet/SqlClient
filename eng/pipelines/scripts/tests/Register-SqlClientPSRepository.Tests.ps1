# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

BeforeAll {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
    $scriptPath = Join-Path $repoRoot 'eng\scripts\Register-SqlClientPSRepository.ps1'
    $sourceLocation = 'https://example.invalid/public/_packaging/modules/nuget/v2'
}

Describe 'SqlClient PowerShell repository registration' {
    BeforeEach {
        $configPath = Join-Path $TestDrive 'PowerShell.config'
        '<configuration><repository sourceLocation="https://example.invalid/public/_packaging/modules/nuget/v2" /></configuration>' |
            Set-Content -LiteralPath $configPath
        $registerParameters = @{ RepositoryName = 'CustomModules'; ConfigPath = $configPath }
        Mock Get-PSResourceRepository {}
        Mock Register-PSResourceRepository {}
    }

    It 'passes the configured URI to PSResourceGet without endpoint restrictions (<Source>)' -ForEach @(
        @{ Source = 'https://example.invalid/nuget/v3/index.json' }
        @{ Source = 'https://example.invalid/custom?mode=resources' }
        @{ Source = 'http://example.invalid/nuget/v2' }
    ) {
        "<configuration><repository sourceLocation=`"$Source`" /></configuration>" |
            Set-Content -LiteralPath $configPath

        & $scriptPath @registerParameters | Should -BeExactly 'CustomModules'

        Should -Invoke Register-PSResourceRepository -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'CustomModules' -and
            $Uri -eq $Source -and $Trusted
        }
    }

    It 'reads the explicit source from PowerShell.config and registers the supplied name anonymously' {
        $result = & $scriptPath @registerParameters

        @($result).Count | Should -Be 1
        $result | Should -BeExactly 'CustomModules'
        Should -Invoke Register-PSResourceRepository -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'CustomModules' -and $Uri -eq $sourceLocation -and $Trusted -and
            $ErrorAction -eq 'Stop' -and -not $CredentialInfo
        }
    }

    It 'reuses a matching registration without changing its trust policy (<Suffix>)' -ForEach @(
        @{ Suffix = '' }
        @{ Suffix = '/' }
    ) {
        Mock Get-PSResourceRepository {
            [pscustomobject]@{
                Name = 'CustomModules'
                Uri = [uri] ($sourceLocation + $Suffix)
                Trusted = $false
            }
        }

        & $scriptPath @registerParameters | Should -BeExactly 'CustomModules'
        Should -Invoke Register-PSResourceRepository -Times 0 -Exactly
    }

    It 'returns only the repository name when registration emits success output' {
        Mock Register-PSResourceRepository {
            "Registered repository $Name."
            [pscustomobject]@{ Name = $Name; Uri = $Uri }
        }

        $result = @(& $scriptPath @registerParameters)

        $result.Count | Should -Be 1
        $result[0] | Should -BeExactly 'CustomModules'
    }

    It 'ignores unrelated repository registrations' {
        Mock Get-PSResourceRepository {
            [pscustomobject]@{ Name = 'AnotherFeed'; Uri = [uri] 'https://example.invalid/nuget/v2' }
        }

        & $scriptPath @registerParameters | Should -BeExactly 'CustomModules'
        Should -Invoke Register-PSResourceRepository -Times 1 -Exactly
    }

    It 'rejects a conflicting source without overwriting it' {
        Mock Get-PSResourceRepository {
            [pscustomobject]@{
                Name = 'CustomModules'
                Uri = [uri] 'https://example.invalid/other/nuget/v2'
            }
        }

        { & $scriptPath @registerParameters } | Should -Throw '*conflicting registration*'
        Should -Invoke Register-PSResourceRepository -Times 0 -Exactly
    }

    It 'rejects <Problem> configuration before registering anything' -ForEach @(
        @{ Problem = 'missing repository'; Xml = '<configuration />' }
        @{ Problem = 'duplicate repositories'; Xml = '<configuration><repository sourceLocation="https://example.invalid/a" /><repository sourceLocation="https://example.invalid/b" /></configuration>' }
        @{ Problem = 'missing source location'; Xml = '<configuration><repository /></configuration>' }
        @{ Problem = 'malformed XML'; Xml = '<configuration>' }
    ) {
        $Xml | Set-Content -LiteralPath $configPath

        { & $scriptPath @registerParameters } | Should -Throw
        Should -Invoke Get-PSResourceRepository -Times 0 -Exactly
        Should -Invoke Register-PSResourceRepository -Times 0 -Exactly
    }

    It 'fails explicitly when the config file is missing' {
        $registerParameters.ConfigPath = Join-Path $TestDrive 'missing.config'

        { & $scriptPath @registerParameters } | Should -Throw
        Should -Invoke Register-PSResourceRepository -Times 0 -Exactly
    }

    It 'uses the checked-in config by default without depending on the working directory' {
        Push-Location $TestDrive
        try {
            & $scriptPath -RepositoryName CustomModules | Should -BeExactly 'CustomModules'
        }
        finally {
            Pop-Location
        }
        [xml] $config = Get-Content (Join-Path $repoRoot 'PowerShell.config') -Raw
        $expected = $config.configuration.repository.sourceLocation
        Should -Invoke Register-PSResourceRepository -Times 1 -Exactly -ParameterFilter {
            $Uri -eq $expected
        }
        Get-Content $scriptPath -Raw | Should -Not -Match 'NuGet\.config|NuGetConfigPath'
    }

    It 'propagates repository enumeration failures' {
        Mock Get-PSResourceRepository { throw 'repository lookup failed' }

        { & $scriptPath @registerParameters } | Should -Throw '*repository lookup failed*'
        Should -Invoke Register-PSResourceRepository -Times 0 -Exactly
    }

    It 'propagates registration failures without returning a repository name' {
        Mock Register-PSResourceRepository { throw 'repository registration failed' }

        { & $scriptPath @registerParameters } | Should -Throw '*repository registration failed*'
    }
}

Describe 'Module prerequisite pipeline wiring' {
    It 'restores the queued module config and helpers when aligning to an older package commit' {
        $source = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\steps\align-source-with-upstream-step.yml') -Raw
        $source | Should -Match 'git checkout --force \$pipelineSourceSha -- eng/pipelines eng/scripts/Register-SqlClientPSRepository.ps1 eng/scripts/Restore-PowerShellModules.ps1 PowerShell.config powershell-modules.json tools/targets/RestorePowerShellModules.targets'
        $directory = Join-Path $TestDrive 'alignment'
        New-Item -ItemType Directory -Path $directory | Out-Null
        Push-Location $directory
        try {
            git init --quiet
            $LASTEXITCODE | Should -Be 0
            git config user.name 'Fixture'
            git config user.email 'fixture@example.invalid'
            New-Item -ItemType Directory -Path 'eng\pipelines' -Force | Out-Null
            'old pipeline' | Set-Content 'eng\pipelines\fixture.yml'
            'old source' | Set-Content 'source.txt'
            '<Project><Target Name="OldBuild" /></Project>' | Set-Content 'build.proj'
            git add .
            git -c commit.gpgsign=false commit --quiet -m old
            $LASTEXITCODE | Should -Be 0
            $upstream = git rev-parse HEAD
            New-Item -ItemType Directory -Path 'eng\scripts' -Force | Out-Null
            Copy-Item -LiteralPath $scriptPath -Destination 'eng\scripts'
            Copy-Item (Join-Path $repoRoot 'eng\scripts\Restore-PowerShellModules.ps1') 'eng\scripts'
            Copy-Item (Join-Path $repoRoot 'PowerShell.config') '.'
            Copy-Item (Join-Path $repoRoot 'powershell-modules.json') '.'
            New-Item -ItemType Directory -Path 'tools\targets' -Force | Out-Null
            Copy-Item (Join-Path $repoRoot 'tools\targets\RestorePowerShellModules.targets') 'tools\targets'
            '<Project><Target Name="NewBuild" /></Project>' | Set-Content 'build.proj'
            'queued pipeline' | Set-Content 'eng\pipelines\fixture.yml'
            'new source' | Set-Content 'source.txt'
            git add .
            git -c commit.gpgsign=false commit --quiet -m queued
            $LASTEXITCODE | Should -Be 0
            $pipelineSourceSha = git rev-parse HEAD
            git checkout --quiet --force $upstream
            $LASTEXITCODE | Should -Be 0
            Test-Path 'eng\scripts\Register-SqlClientPSRepository.ps1' | Should -BeFalse
            Test-Path 'eng\scripts\Restore-PowerShellModules.ps1' | Should -BeFalse
            Test-Path 'PowerShell.config' | Should -BeFalse
            Test-Path 'powershell-modules.json' | Should -BeFalse
            Test-Path 'tools\targets\RestorePowerShellModules.targets' | Should -BeFalse
            $restore = [regex]::Match($source, '(?m)^\s*(git checkout --force \$pipelineSourceSha -- .+)$').Groups[1].Value
            & ([scriptblock]::Create($restore))
            $LASTEXITCODE | Should -Be 0
            Get-Content 'source.txt' | Should -Be 'old source'
            Get-Content 'build.proj' | Should -Be '<Project><Target Name="OldBuild" /></Project>'
            Get-Content 'eng\pipelines\fixture.yml' | Should -Be 'queued pipeline'
            Get-Content 'PowerShell.config' -Raw |
                Should -Be (Get-Content (Join-Path $repoRoot 'PowerShell.config') -Raw)
            Get-Content 'eng\scripts\Register-SqlClientPSRepository.ps1' -Raw |
                Should -Be (Get-Content $scriptPath -Raw)
            foreach ($path in @('eng\scripts\Restore-PowerShellModules.ps1',
                    'powershell-modules.json', 'tools\targets\RestorePowerShellModules.targets')) {
                Get-Content $path -Raw | Should -Be (Get-Content (Join-Path $repoRoot $path) -Raw)
            }
        }
        finally {
            Pop-Location
        }
    }

    It 'restores modules once after dotnet tools and before setup in <Path>' -ForEach @(
        @{ Path = 'eng\pipelines\common\templates\jobs\ci-run-tests-job.yml'; Next = 'ci-project-build-step.yml' }
        @{ Path = 'eng\pipelines\pr\jobs\test-sqlclientmanual-job.yml'; Next = 'configure-sqlserver-step.yml' }
        @{ Path = 'eng\pipelines\ci\kerberos\sqlclient-ci-kerberos-job.yml'; Next = 'linux-setup-step.yml' }
    ) {
        $source = Get-Content (Join-Path $repoRoot $Path) -Raw
        ([regex]::Matches($source, 'restore-powershell-modules.yml@self')).Count | Should -Be 1
        $source.IndexOf('restore-dotnet-tools.yml') |
            Should -BeLessThan $source.IndexOf('restore-powershell-modules.yml')
        $source.IndexOf('restore-powershell-modules.yml') | Should -BeLessThan $source.IndexOf($Next)
        $source | Should -Not -Match 'install-powershell-module-step|(?m:^\s+name: (OpenSSL|SqlServer)\s*$)'
    }

    It 'preserves existing prebuild steps without module-specific stage gates' {
        $stage = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\stages\ci-run-tests-stage.yml') -Raw
        ([regex]::Matches($stage, 'prebuildSteps: \$\{\{ parameters.prebuildSteps \}\}')).Count |
            Should -Be 2
        $stage | Should -Not -Match 'OpenSSL|SqlServer\r?\n|install-powershell-module-step'
        $job = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\jobs\ci-run-tests-job.yml') -Raw
        $job.IndexOf('- ${{ parameters.prebuildSteps }}') |
            Should -BeLessThan $job.IndexOf('ci-project-build-step.yml')
    }

    It 'keeps restore out of account setup and cleanup' {
        $account = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\steps\configure-netonly-account-step.yml') -Raw
        $account | Should -Not -Match 'restore-powershell|install-powershell|Install-(Module|PSResource)'
    }

    It 'imports the restored SqlServer module for Kerberos connectivity' {
        $setup = Get-Content (Join-Path $repoRoot 'eng\pipelines\ci\kerberos\linux-setup-step.yml') -Raw
        $setup | Should -Not -Match 'restore-powershell|install-powershell|Install-(Module|PSResource)'
        $setup | Should -Not -Match 'powershell-modules.json|RequiredVersion'
        $setup | Should -Match '(?m)^\s+Import-Module SqlServer\r?$'
    }

    It 'keeps module installation and repository plumbing out of <Path>' -ForEach @(
        @{ Path = 'eng\scripts\Configure-NetOnlyAccount.ps1' }
        @{ Path = 'src\Microsoft.Data.SqlClient\tests\ManualTests\makepfxcert.ps1' }
        @{ Path = 'src\Microsoft.Data.SqlClient\tests\ManualTests\SQL\ConnectionTestWithSSLCert\CertificateTestWithTdsServer.cs' }
        @{ Path = 'src\Microsoft.Data.SqlClient\tests\ManualTests\DataCommon\DataTestUtility.cs' }
        @{ Path = 'src\Microsoft.Data.SqlClient\tests\tools\Microsoft.Data.SqlClient.TestUtilities\Config.cs' }
        @{ Path = 'eng\pipelines\common\templates\steps\update-config-file-step.yml' }
    ) {
        Get-Content (Join-Path $repoRoot $Path) -Raw |
            Should -Not -Match 'Install-(Module|PSResource)|Register-.*PS(Resource)?Repository|[Pp]owerShellRepository'
    }

    It 'calls the standalone build hook without interpolating module data into YAML' {
        $source = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\steps\restore-powershell-modules.yml') -Raw
        $source | Should -Match 'dotnet msbuild tools/targets/RestorePowerShellModules.targets -t:RestorePowerShellModules'
        $source | Should -Not -Match 'Install-(Module|PSResource)|Register-.*PS(Resource)?Repository|parameters:|env:|replace\('
    }
}

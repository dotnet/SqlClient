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
        $configPath = Join-Path $TestDrive 'NuGet.config'
        '<configuration><packageSources><add key="governed" value="https://example.invalid/public/_packaging/modules/nuget/v3/index.json" /></packageSources></configuration>' |
            Set-Content -LiteralPath $configPath
        $registerParameters = @{ RepositoryName = 'CustomModules'; NuGetConfigPath = $configPath }
        Mock Get-PSRepository {}
        Mock Register-PSRepository {}
    }

    It 'derives the v2 source from NuGet.config and registers the supplied name anonymously' {
        $result = & $scriptPath @registerParameters

        @($result).Count | Should -Be 1
        $result | Should -BeExactly 'CustomModules'
        Should -Invoke Register-PSRepository -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'CustomModules' -and $SourceLocation -eq $sourceLocation -and
            $PackageManagementProvider -eq 'NuGet' -and $InstallationPolicy -eq 'Trusted' -and
            $ErrorAction -eq 'Stop' -and -not $Credential
        }
    }

    It 'reuses a matching registration without changing its trust policy (<Suffix>)' -ForEach @(
        @{ Suffix = '' }
        @{ Suffix = '/' }
    ) {
        Mock Get-PSRepository {
            [pscustomobject]@{
                Name = 'CustomModules'
                SourceLocation = $sourceLocation + $Suffix
                PackageManagementProvider = 'NuGet'
                InstallationPolicy = 'Untrusted'
            }
        }

        & $scriptPath @registerParameters | Should -BeExactly 'CustomModules'
        Should -Invoke Register-PSRepository -Times 0 -Exactly
    }

    It 'ignores unrelated repository registrations' {
        Mock Get-PSRepository {
            [pscustomobject]@{ Name = 'AnotherFeed'; SourceLocation = 'https://example.invalid/nuget/v2' }
        }

        & $scriptPath @registerParameters | Should -BeExactly 'CustomModules'
        Should -Invoke Register-PSRepository -Times 1 -Exactly
    }

    It 'rejects a conflicting <Conflict> without overwriting it' -ForEach @(
        @{ Conflict = 'source'; Location = 'https://example.invalid/other/nuget/v2'; Provider = 'NuGet' }
        @{ Conflict = 'provider'; Location = 'https://example.invalid/public/_packaging/modules/nuget/v2'; Provider = 'Other' }
    ) {
        Mock Get-PSRepository {
            [pscustomobject]@{
                Name = 'CustomModules'
                SourceLocation = $Location
                PackageManagementProvider = $Provider
            }
        }

        { & $scriptPath @registerParameters } | Should -Throw '*conflicting registration*'
        Should -Invoke Register-PSRepository -Times 0 -Exactly
    }

    It 'rejects <Problem> configuration before registering anything' -ForEach @(
        @{ Problem = 'missing governed source'; Xml = '<configuration><packageSources /></configuration>' }
        @{ Problem = 'duplicate governed sources'; Xml = '<configuration><packageSources><add key="governed" value="https://example.invalid/a" /><add key="governed" value="https://example.invalid/b" /></packageSources></configuration>' }
        @{ Problem = 'malformed XML'; Xml = '<configuration>' }
        @{ Problem = 'relative URL'; Xml = '<configuration><packageSources><add key="governed" value="packages" /></packageSources></configuration>' }
        @{ Problem = 'HTTP URL'; Xml = '<configuration><packageSources><add key="governed" value="http://example.invalid/_packaging/modules/nuget/v3/index.json" /></packageSources></configuration>' }
        @{ Problem = 'unsupported endpoint'; Xml = '<configuration><packageSources><add key="governed" value="https://example.invalid/nuget/v3/index.json" /></packageSources></configuration>' }
        @{ Problem = 'query string'; Xml = '<configuration><packageSources><add key="governed" value="https://example.invalid/_packaging/modules/nuget/v3/index.json?query=value" /></packageSources></configuration>' }
    ) {
        $Xml | Set-Content -LiteralPath $configPath

        { & $scriptPath @registerParameters } | Should -Throw
        Should -Invoke Get-PSRepository -Times 0 -Exactly
        Should -Invoke Register-PSRepository -Times 0 -Exactly
    }

    It 'fails explicitly when the config file is missing' {
        $registerParameters.NuGetConfigPath = Join-Path $TestDrive 'missing.config'

        { & $scriptPath @registerParameters } | Should -Throw
        Should -Invoke Register-PSRepository -Times 0 -Exactly
    }

    It 'uses the checked-in config by default without depending on the working directory' {
        Push-Location $TestDrive
        try {
            & $scriptPath -RepositoryName CustomModules | Should -BeExactly 'CustomModules'
        }
        finally {
            Pop-Location
        }
        [xml] $config = Get-Content (Join-Path $repoRoot 'NuGet.config') -Raw
        $expected = $config.SelectSingleNode('/configuration/packageSources/add[@key="governed"]').value -replace '/nuget/v3/index\.json$', '/nuget/v2'
        Should -Invoke Register-PSRepository -Times 1 -Exactly -ParameterFilter { $SourceLocation -eq $expected }
    }

    It 'propagates repository enumeration failures' {
        Mock Get-PSRepository { throw 'repository lookup failed' }

        { & $scriptPath @registerParameters } | Should -Throw '*repository lookup failed*'
        Should -Invoke Register-PSRepository -Times 0 -Exactly
    }

    It 'propagates registration failures without returning a repository name' {
        Mock Register-PSRepository { throw 'repository registration failed' }

        { & $scriptPath @registerParameters } | Should -Throw '*repository registration failed*'
    }
}

Describe 'Module prerequisite pipeline wiring' {
    It 'restores both queued helpers when aligning to an older package commit' {
        $source = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\steps\align-source-with-upstream-step.yml') -Raw
        $source | Should -Match 'git checkout --force \$pipelineSourceSha -- eng/pipelines eng/scripts/Register-SqlClientPSRepository.ps1 eng/scripts/Install-SqlClientPSModule.ps1'
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
            git add .
            git -c commit.gpgsign=false commit --quiet -m old
            $LASTEXITCODE | Should -Be 0
            $upstream = git rev-parse HEAD
            New-Item -ItemType Directory -Path 'eng\scripts' -Force | Out-Null
            Copy-Item -LiteralPath $scriptPath -Destination 'eng\scripts'
            Copy-Item (Join-Path $repoRoot 'eng\scripts\Install-SqlClientPSModule.ps1') 'eng\scripts'
            'queued pipeline' | Set-Content 'eng\pipelines\fixture.yml'
            'new source' | Set-Content 'source.txt'
            git add .
            git -c commit.gpgsign=false commit --quiet -m queued
            $LASTEXITCODE | Should -Be 0
            $pipelineSourceSha = git rev-parse HEAD
            git checkout --quiet --force $upstream
            $LASTEXITCODE | Should -Be 0
            Test-Path 'eng\scripts\Register-SqlClientPSRepository.ps1' | Should -BeFalse
            Test-Path 'eng\scripts\Install-SqlClientPSModule.ps1' | Should -BeFalse
            $restore = [regex]::Match($source, '(?m)^\s*(git checkout --force \$pipelineSourceSha -- .+)$').Groups[1].Value
            & ([scriptblock]::Create($restore))
            $LASTEXITCODE | Should -Be 0
            Get-Content 'source.txt' | Should -Be 'old source'
            Get-Content 'eng\pipelines\fixture.yml' | Should -Be 'queued pipeline'
            foreach ($helper in @('Register-SqlClientPSRepository.ps1', 'Install-SqlClientPSModule.ps1')) {
                Get-Content "eng\scripts\$helper" -Raw |
                    Should -Be (Get-Content (Join-Path $repoRoot "eng\scripts\$helper") -Raw)
            }
        }
        finally {
            Pop-Location
        }
    }

    It 'installs OpenSSL only for Linux local-server set-1 jobs in <Path>' -ForEach @(
        @{ Path = 'eng\pipelines\common\templates\jobs\ci-run-tests-job.yml'; OS = 'operatingSystem'; Local = "eq(parameters.configSqlFor, 'local')" }
        @{ Path = 'eng\pipelines\pr\jobs\test-sqlclientmanual-job.yml'; OS = 'platformOperatingSystem'; Local = 'parameters.isLocalServer' }
    ) {
        $source = Get-Content (Join-Path $repoRoot $Path) -Raw
        $condition = "and(eq(parameters.$OS, 'Linux'), $Local, contains(parameters.testSet, '1'))"
        $source | Should -Match ([regex]::Escape($condition))
        $source | Should -Match 'install-powershell-module-step.yml@self\r?\n\s+parameters:\r?\n\s+name: OpenSSL\r?\n\s+skipIfInstalled: true'
        $source.IndexOf('install-powershell-module-step.yml') |
            Should -BeLessThan ([regex]::Match($source, 'configure-netonly-account-step.yml|TestSqlClientManual|run-all-tests-step.yml').Index)
        $source | Should -Not -Match '[Pp]owerShellRepository|Register-.*PSRepository'
    }

    It 'installs the pinned SqlServer prerequisite only before net-only setup' {
        $source = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\steps\configure-netonly-account-step.yml') -Raw
        $source | Should -Match "if eq\(parameters.mode, 'setup'\)"
        $source | Should -Match 'name: SqlServer\r?\n\s+requiredVersion: 22.4.5.1\r?\n\s+skipIfInstalled: true\r?\n\s+allowClobber: true'
        $source.IndexOf('install-powershell-module-step.yml') | Should -BeLessThan $source.IndexOf('task: PowerShell@2')
        $source | Should -Not -Match '[Pp]owerShellRepository'
    }

    It 'installs unpinned SqlServer before the Kerberos connectivity check' {
        $source = Get-Content (Join-Path $repoRoot 'eng\pipelines\ci\kerberos\linux-setup-step.yml') -Raw
        $source | Should -Match 'install-powershell-module-step.yml@self\r?\n\s+parameters:\r?\n\s+name: SqlServer'
        $source.IndexOf('install-powershell-module-step.yml') | Should -BeLessThan $source.IndexOf('Import-Module SqlServer')
        $source | Should -Not -Match 'requiredVersion|Install-Module|[Pp]owerShellRepository'
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
            Should -Not -Match 'Install-Module|Register-.*PSRepository|[Pp]owerShellRepository'
    }

    It 'escapes module inputs in one shared template' {
        $source = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\steps\install-powershell-module-step.yml') -Raw
        $source | Should -Match 'Install-SqlClientPSModule.ps1'
        $source | Should -Match 'Module name and version must be literal template parameters'
        $source | Should -Not -Match 'RepositoryName|https://|env:'
        foreach ($parameter in @('name', 'requiredVersion')) {
            $pattern = "'\$\{\{ replace\(parameters\.$parameter, '{4}, '{6}\) \}\}'"
            $literal = [regex]::Match($source, $pattern).Value
            $literal | Should -Not -BeNullOrEmpty
            foreach ($value in @('SqlServer', "a'; throw 'injected'; #", 'a`";${value};#',
                    "a'`nthrow 'injected")) {
                $expanded = [regex]::Replace($literal, '\$\{\{.*?\}\}', { $value.Replace("'", "''") })
                & ([scriptblock]::Create($expanded)) | Should -BeExactly $value
            }
        }
    }
}

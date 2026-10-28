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
        Mock Get-PSRepository {}
        Mock Register-PSRepository {}
    }

    It 'reads the explicit source from PowerShell.config and registers the supplied name anonymously' {
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
        @{ Problem = 'missing repository'; Xml = '<configuration />' }
        @{ Problem = 'duplicate repositories'; Xml = '<configuration><repository sourceLocation="https://example.invalid/a" /><repository sourceLocation="https://example.invalid/b" /></configuration>' }
        @{ Problem = 'missing source location'; Xml = '<configuration><repository /></configuration>' }
        @{ Problem = 'malformed XML'; Xml = '<configuration>' }
        @{ Problem = 'relative URL'; Xml = '<configuration><repository sourceLocation="packages" /></configuration>' }
        @{ Problem = 'HTTP URL'; Xml = '<configuration><repository sourceLocation="http://example.invalid/_packaging/modules/nuget/v2" /></configuration>' }
        @{ Problem = 'unsupported endpoint'; Xml = '<configuration><repository sourceLocation="https://example.invalid/nuget/v3/index.json" /></configuration>' }
        @{ Problem = 'query string'; Xml = '<configuration><repository sourceLocation="https://example.invalid/_packaging/modules/nuget/v2?query=value" /></configuration>' }
    ) {
        $Xml | Set-Content -LiteralPath $configPath

        { & $scriptPath @registerParameters } | Should -Throw
        Should -Invoke Get-PSRepository -Times 0 -Exactly
        Should -Invoke Register-PSRepository -Times 0 -Exactly
    }

    It 'fails explicitly when the config file is missing' {
        $registerParameters.ConfigPath = Join-Path $TestDrive 'missing.config'

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
        [xml] $config = Get-Content (Join-Path $repoRoot 'PowerShell.config') -Raw
        $expected = $config.configuration.repository.sourceLocation
        Should -Invoke Register-PSRepository -Times 1 -Exactly -ParameterFilter { $SourceLocation -eq $expected }
        Get-Content $scriptPath -Raw | Should -Not -Match 'NuGet\.config|NuGetConfigPath'
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
    It 'restores the queued module config and helpers when aligning to an older package commit' {
        $source = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\steps\align-source-with-upstream-step.yml') -Raw
        $source | Should -Match 'git checkout --force \$pipelineSourceSha -- eng/pipelines eng/scripts/Register-SqlClientPSRepository.ps1 eng/scripts/Install-SqlClientPSModule.ps1 PowerShell.config'
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
            Copy-Item (Join-Path $repoRoot 'PowerShell.config') '.'
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
            Test-Path 'PowerShell.config' | Should -BeFalse
            $restore = [regex]::Match($source, '(?m)^\s*(git checkout --force \$pipelineSourceSha -- .+)$').Groups[1].Value
            & ([scriptblock]::Create($restore))
            $LASTEXITCODE | Should -Be 0
            Get-Content 'source.txt' | Should -Be 'old source'
            Get-Content 'eng\pipelines\fixture.yml' | Should -Be 'queued pipeline'
            Get-Content 'PowerShell.config' -Raw |
                Should -Be (Get-Content (Join-Path $repoRoot 'PowerShell.config') -Raw)
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
        @{ Path = 'eng\pipelines\common\templates\stages\ci-run-tests-stage.yml'; Condition = "and(eq(config.value.operatingSystem, 'Linux'), eq(config.value.configSqlFor, 'local'), contains(testSet, '1'))" }
        @{ Path = 'eng\pipelines\pr\jobs\test-sqlclientmanual-job.yml'; Condition = "and(eq(parameters.platformOperatingSystem, 'Linux'), parameters.isLocalServer, contains(parameters.testSet, '1'))" }
    ) {
        $source = Get-Content (Join-Path $repoRoot $Path) -Raw
        $source | Should -Match ([regex]::Escape($Condition))
        $source | Should -Match 'install-powershell-module-step.yml@self\r?\n\s+parameters:\r?\n\s+name: OpenSSL\r?\n\s+skipIfInstalled: true'
        if ($Path -like '*\pr\*') {
            $source.IndexOf('install-powershell-module-step.yml') |
                Should -BeLessThan $source.IndexOf('TestSqlClientManual')
        }
        $source | Should -Not -Match '[Pp]owerShellRepository|Register-.*PSRepository'
    }

    It 'adds the Linux prerequisite through prebuild steps without specializing the shared job' {
        $stage = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\stages\ci-run-tests-stage.yml') -Raw
        $modernStart = $stage.IndexOf('- ${{ else }}: # .NET')
        $modernStart | Should -BeGreaterThan 0
        $stage.Substring(0, $modernStart) | Should -Not -Match 'name: OpenSSL'
        $modern = $stage.Substring($modernStart)
        $modern | Should -Match 'prebuildSteps:\r?\n\s+- \$\{\{ if and\(eq\(config.value.operatingSystem'
        $modern | Should -Match 'skipIfInstalled: true\r?\n\s+- \$\{\{ parameters.prebuildSteps \}\}'
        $job = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\jobs\ci-run-tests-job.yml') -Raw
        $job | Should -Not -Match 'OpenSSL|install-powershell-module-step'
        $job.IndexOf('- ${{ parameters.prebuildSteps }}') |
            Should -BeLessThan $job.IndexOf('ci-project-build-step.yml')
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
        @{ Path = 'src\Microsoft.Data.SqlClient\tests\ManualTests\SQL\ConnectionTestWithSSLCert\CertificateTestWithTdsServer.cs' }
        @{ Path = 'src\Microsoft.Data.SqlClient\tests\ManualTests\DataCommon\DataTestUtility.cs' }
        @{ Path = 'src\Microsoft.Data.SqlClient\tests\tools\Microsoft.Data.SqlClient.TestUtilities\Config.cs' }
        @{ Path = 'eng\pipelines\common\templates\steps\update-config-file-step.yml' }
    ) {
        Get-Content (Join-Path $repoRoot $Path) -Raw |
            Should -Not -Match 'Install-Module|Register-.*PSRepository|[Pp]owerShellRepository'
    }

    Describe 'Certificate script local module fallback' {
        BeforeAll {
            $certificateScript = Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\ManualTests\makepfxcert.ps1'
            $tokens = $null
            $parseErrors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $certificateScript, [ref] $tokens, [ref] $parseErrors)
            $parseErrors.Count | Should -Be 0
            $unixBranch = $ast.Find({
                param($node)
                $node -is [System.Management.Automation.Language.IfStatementAst] -and
                    $node.Clauses[0].Item1.Extent.Text -eq '$OS -eq "Unix"'
            }, $true)
            $fallback = $unixBranch.Clauses[0].Item2.Statements[0]
            $fallback.Extent.Text | Should -Match 'Get-Module -ListAvailable -Name OpenSSL'
            $invokeFallback = [scriptblock]::Create(
                'param($PSScriptRoot)' + "`n" + $fallback.Extent.Text)
        }

        BeforeEach {
            $checkout = Join-Path $TestDrive 'checkout'
            $installerDirectory = Join-Path $checkout 'eng\scripts'
            $outputDirectory = Join-Path $checkout 'artifacts\manualtests'
            New-Item -ItemType Directory -Path $installerDirectory, $outputDirectory -Force | Out-Null
            $fixtureInstaller = Join-Path $installerDirectory 'Install-SqlClientPSModule.ps1'
            'param($Name, [switch] $SkipIfInstalled); [pscustomobject]@{ Name = $Name; Skip = [bool] $SkipIfInstalled }' |
                Set-Content -LiteralPath $fixtureInstaller
            Mock Get-Module {}
        }

        It 'installs a missing module through the shared installer from <Location>' -ForEach @(
            @{ Location = 'source'; RelativePath = 'src\Microsoft.Data.SqlClient\tests\ManualTests' }
            @{ Location = 'build output'; RelativePath = 'artifacts\manualtests' }
        ) {
            $scriptDirectory = Join-Path $checkout $RelativePath
            New-Item -ItemType Directory -Path $scriptDirectory -Force | Out-Null
            Push-Location $TestDrive
            try {
                $result = & $invokeFallback $scriptDirectory
            }
            finally {
                Pop-Location
            }
            $result.Name | Should -BeExactly 'OpenSSL'
            $result.Skip | Should -BeTrue
        }

        It 'does not invoke the installer when OpenSSL is already available' {
            Mock Get-Module { [pscustomobject]@{ Name = 'OpenSSL' } }
            'throw "installer must not run"' | Set-Content -LiteralPath $fixtureInstaller

            & $invokeFallback $outputDirectory | Should -BeNullOrEmpty
        }

        It 'reports a missing checkout instead of switching repositories' {
            $outside = Join-Path $TestDrive 'outside'
            New-Item -ItemType Directory -Path $outside -Force | Out-Null

            { & $invokeFallback $outside } | Should -Throw '*Cannot find the SqlClient module installer*'
        }

        It 'propagates installation failures to certificate generation error handling' {
            'throw "module install failed"' | Set-Content -LiteralPath $fixtureInstaller

            { & $invokeFallback $outputDirectory } | Should -Throw '*module install failed*'
        }
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

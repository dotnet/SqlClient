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

Describe 'Explicit repository registration and module consumers' {
    It 'passes the repository name into the reusable registration step' {
        $source = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\steps\register-powershell-repository-step.yml') -Raw
        $source | Should -Match 'name: repositoryName'
        $source | Should -Match 'Register-SqlClientPSRepository.ps1" -RepositoryName \$env:POWERSHELL_REPOSITORY_NAME'
        $source | Should -Match 'POWERSHELL_REPOSITORY_NAME: \$\{\{ parameters.repositoryName \}\}'
        $source | Should -Not -Match 'https://'
    }

    It 'registers once before installs/tests and forwards the same name in <Path>' -ForEach @(
        @{ Path = 'eng\pipelines\common\templates\jobs\ci-run-tests-job.yml' }
        @{ Path = 'eng\pipelines\pr\jobs\test-sqlclientmanual-job.yml' }
        @{ Path = 'eng\pipelines\ci\kerberos\linux-setup-step.yml' }
    ) {
        $source = Get-Content (Join-Path $repoRoot $Path) -Raw
        ([regex]::Matches($source, 'template: /eng/pipelines/common/templates/steps/register-powershell-repository-step.yml@self')).Count |
            Should -Be 1
        $source | Should -Match 'repositoryName: \$\{\{ parameters.powerShellRepository \}\}'
        $source.IndexOf('register-powershell-repository-step.yml') |
            Should -BeLessThan ([regex]::Match($source, 'configure-netonly-account-step.yml|Install-Module|TestSqlClientManual|run-all-tests-step.yml').Index)
        if ($Path -notlike '*linux-setup*') {
            $source | Should -Match 'name: SQLCLIENT_POWERSHELL_REPOSITORY\r?\n\s+value: \$\{\{ parameters.powerShellRepository \}\}'
            ([regex]::Matches($source, 'powerShellRepository: \$\{\{ parameters.powerShellRepository \}\}')).Count |
                Should -Be 2
        }
        else {
            $source | Should -Match 'Install-Module -Name SqlServer -Repository \$env:POWERSHELL_REPOSITORY_NAME'
            $source | Should -Match 'POWERSHELL_REPOSITORY_NAME: \$\{\{ parameters.powerShellRepository \}\}'
        }
    }

    It 'passes the name through the net-only and Kerberos templates' {
        $netOnly = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\steps\configure-netonly-account-step.yml') -Raw
        $netOnly | Should -Match "-PowerShellRepository '\$\{\{ parameters.powerShellRepository \}\}'"
        $kerberos = Get-Content (Join-Path $repoRoot 'eng\pipelines\ci\kerberos\sqlclient-ci-kerberos-job.yml') -Raw
        $kerberos | Should -Match 'powerShellRepository: \$\{\{ parameters.powerShellRepository \}\}'
    }

    It 'keeps registration and feed addresses out of <Path>' -ForEach @(
        @{ Path = 'eng\scripts\Configure-NetOnlyAccount.ps1' }
        @{ Path = 'src\Microsoft.Data.SqlClient\tests\ManualTests\makepfxcert.ps1' }
    ) {
        $source = Get-Content (Join-Path $repoRoot $Path) -Raw
        $source | Should -Not -Match 'Register-.*PSRepository|Get-PSRepository|https://|PSGallery'
        $source | Should -Match '-Repository \$PowerShellRepository'
    }

    It 'installs certificate modules with the supplied name and propagates install failures' {
        Mock Install-Module {}
        $path = Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\ManualTests\makepfxcert.ps1'
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$null, [ref]$null)
        $provision = $ast.Find({
            param($node)
            $node -is [System.Management.Automation.Language.IfStatementAst] -and
            $node.Clauses[0].Item1.Extent.Text -like '*Get-Module*OpenSSL*'
        }, $true)
        $body = $provision.Clauses[0].Item2.Extent.Text
        $run = [scriptblock]::Create("param(`$PowerShellRepository) & $body")
        & $run CustomModules
        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'OpenSSL' -and $Repository -eq 'CustomModules' -and $Force -and $ErrorAction -eq 'Stop'
        }
        { & $run '' } | Should -Throw '*PowerShellRepository must name*'
        Mock Install-Module { throw 'feed unavailable' }
        { & $run CustomModules } | Should -Throw '*feed unavailable*'
        $parent = $provision.Parent
        while ($parent) {
            $parent | Should -Not -BeOfType ([System.Management.Automation.Language.TryStatementAst])
            $parent = $parent.Parent
        }
    }

    It 'receives the pipeline repository name through the certificate script parameter default' {
        $path = Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\ManualTests\makepfxcert.ps1'
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$null, [ref]$null)
        $parameter = $ast.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'PowerShellRepository' }
        $parameter.DefaultValue.Extent.Text | Should -Be '$env:SQLCLIENT_POWERSHELL_REPOSITORY'
        $bind = [scriptblock]::Create($ast.ParamBlock.Extent.Text + "`n`$PowerShellRepository")
        $original = $env:SQLCLIENT_POWERSHELL_REPOSITORY
        try {
            $env:SQLCLIENT_POWERSHELL_REPOSITORY = 'PipelineModules'
            & $bind | Should -BeExactly 'PipelineModules'
            & $bind -PowerShellRepository ExplicitModules | Should -BeExactly 'ExplicitModules'
        }
        finally {
            $env:SQLCLIENT_POWERSHELL_REPOSITORY = $original
        }
    }
}

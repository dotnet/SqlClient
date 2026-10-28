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
    It 'restores the queued registration helper when aligning to an older package commit' {
        $source = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\steps\align-source-with-upstream-step.yml') -Raw
        $source | Should -Match 'git checkout --force \$pipelineSourceSha -- eng/pipelines eng/scripts/Register-SqlClientPSRepository.ps1'
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
            'queued pipeline' | Set-Content 'eng\pipelines\fixture.yml'
            'new source' | Set-Content 'source.txt'
            git add .
            git -c commit.gpgsign=false commit --quiet -m queued
            $LASTEXITCODE | Should -Be 0
            $pipelineSourceSha = git rev-parse HEAD
            git checkout --quiet --force $upstream
            $LASTEXITCODE | Should -Be 0
            Test-Path 'eng\scripts\Register-SqlClientPSRepository.ps1' | Should -BeFalse
            $restore = [regex]::Match($source, '(?m)^\s*(git checkout --force \$pipelineSourceSha -- .+)$').Groups[1].Value
            & ([scriptblock]::Create($restore))
            $LASTEXITCODE | Should -Be 0
            Get-Content 'source.txt' | Should -Be 'old source'
            Get-Content 'eng\pipelines\fixture.yml' | Should -Be 'queued pipeline'
            Get-Content 'eng\scripts\Register-SqlClientPSRepository.ps1' -Raw |
                Should -Be (Get-Content $scriptPath -Raw)
        }
        finally {
            Pop-Location
        }
    }

    It 'passes the repository name into the reusable registration step' {
        $source = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\steps\register-powershell-repository-step.yml') -Raw
        $source | Should -Match 'name: repositoryName'
        $source | Should -Match 'Register-SqlClientPSRepository.ps1" -RepositoryName'
        $source | Should -Match ([regex]::Escape("contains(parameters.repositoryName, '`$(')"))
        $source | Should -Match 'repositoryName must be a literal template parameter'
        $source | Should -Not -Match 'POWERSHELL_REPOSITORY_NAME|env:'
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
            $source | Should -Not -Match 'SQLCLIENT_POWERSHELL_REPOSITORY'
            $source | Should -Match 'PowerShellRepository[: ]'
            ([regex]::Matches($source, 'powerShellRepository: \$\{\{ parameters.powerShellRepository \}\}')).Count |
                Should -Be 2
        }
        else {
            $source | Should -Match 'Install-Module -Name SqlServer -Repository'
            $source | Should -Not -Match 'POWERSHELL_REPOSITORY_NAME'
        }
    }

    It 'passes the name through the net-only and Kerberos templates' {
        $netOnly = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\steps\configure-netonly-account-step.yml') -Raw
        $netOnly | Should -Match "-PowerShellRepository '\$\{\{ replace\(parameters.powerShellRepository,"
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

    It 'binds the certificate repository only from an explicit parameter' {
        $path = Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\ManualTests\makepfxcert.ps1'
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$null, [ref]$null)
        $parameter = $ast.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'PowerShellRepository' }
        $parameter.DefaultValue | Should -BeNullOrEmpty
        $bind = [scriptblock]::Create($ast.ParamBlock.Extent.Text + "`n`$PowerShellRepository")
        & $bind | Should -BeNullOrEmpty
        & $bind -PowerShellRepository ExplicitModules | Should -BeExactly 'ExplicitModules'
    }

    It 'escapes template parameters as literal PowerShell data in <Path>' -ForEach @(
        @{ Path = 'eng\pipelines\common\templates\steps\register-powershell-repository-step.yml' }
        @{ Path = 'eng\pipelines\ci\kerberos\linux-setup-step.yml' }
        @{ Path = 'eng\pipelines\common\templates\steps\configure-netonly-account-step.yml' }
        @{ Path = 'eng\pipelines\common\templates\steps\update-config-file-step.yml' }
        @{ Path = 'eng\pipelines\pr\jobs\test-sqlclientmanual-job.yml' }
    ) {
        $source = Get-Content (Join-Path $repoRoot $Path) -Raw
        $pattern = "'\$\{\{ replace\(parameters\.[Pp]owerShellRepository, '{4}, '{6}\) \}\}'"
        if ($Path -like '*register-powershell*') {
            $pattern = "'\$\{\{ replace\(parameters\.repositoryName, '{4}, '{6}\) \}\}'"
        }
        $literal = [regex]::Match($source, $pattern).Value
        $literal | Should -Not -BeNullOrEmpty
        foreach ($value in @('CustomModules', "a'; throw 'injected'; #", 'a`";${value};#',
                "a'`nthrow 'injected")) {
            $expanded = [regex]::Replace($literal, '\$\{\{.*?\}\}', { $value.Replace("'", "''") })
            & ([scriptblock]::Create($expanded)) | Should -BeExactly $value
        }
    }

    It 'writes the selected repository into test configuration and reads it in the certificate caller' {
        $update = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\steps\update-config-file-step.yml') -Raw
        $update | Should -Match '\$p.PowerShellRepository='
        $ci = Get-Content (Join-Path $repoRoot 'eng\pipelines\common\templates\jobs\ci-run-tests-job.yml') -Raw
        $ci | Should -Match 'PowerShellRepository: \$\{\{ parameters.powerShellRepository \}\}'
        $pr = Get-Content (Join-Path $repoRoot 'eng\pipelines\pr\jobs\test-sqlclientmanual-job.yml') -Raw
        $pr | Should -Match '\$config.PowerShellRepository ='
        $config = Get-Content (Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\tools\Microsoft.Data.SqlClient.TestUtilities\Config.cs') -Raw
        $config | Should -Match 'public string\? PowerShellRepository = null;'
        $defaults = Get-Content (Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\tools\Microsoft.Data.SqlClient.TestUtilities\config.default.jsonc') -Raw | ConvertFrom-Json
        $defaults.PowerShellRepository | Should -Be ''
        $utility = Get-Content (Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\ManualTests\DataCommon\DataTestUtility.cs') -Raw
        $utility | Should -Match 'PowerShellRepository = c.PowerShellRepository;'
        $caller = Get-Content (Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\ManualTests\SQL\ConnectionTestWithSSLCert\CertificateTestWithTdsServer.cs') -Raw
        $caller | Should -Match 'RunPowershellScript\(s_fullPathToPowershellScript, DataTestUtility.PowerShellRepository\)'
    }
}

Describe 'Certificate subprocess repository arguments' {
    BeforeAll {
        $source = Get-Content (Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\ManualTests\SQL\ConnectionTestWithSSLCert\CertificateTestWithTdsServer.cs') -Raw
        $method = [regex]::Match($source, '(?s)        private static void RunPowershellScript.*?(?=        /// <summary>)').Value
        $method | Should -Not -BeNullOrEmpty
        $method = $method.Replace('private static', 'public static')
        Add-Type -TypeDefinition @"
#nullable enable
using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
public static class CertificateProcessFixture {
$method
}
"@
    }

    BeforeEach {
        $stub = Join-Path $TestDrive 'capture.ps1'
        $capture = Join-Path $TestDrive 'repository-name.txt'
        if (Test-Path -LiteralPath $capture) {
            Remove-Item -LiteralPath $capture
        }
        @'
param($OutDir, [switch] $SkipTrust, [string] $PowerShellRepository)
$PowerShellRepository | Set-Content (Join-Path $OutDir 'repository-name.txt')
'@ | Set-Content -LiteralPath $stub
    }

    It 'passes <Name> explicitly to a real child PowerShell process' -ForEach @(
        @{ Name = 'CustomModules' }
        @{ Name = 'Feed_6.2-0' }
    ) {
        $original = [Environment]::CurrentDirectory
        try {
            [Environment]::CurrentDirectory = $TestDrive
            [CertificateProcessFixture]::RunPowershellScript($stub, $Name)
            Get-Content (Join-Path $TestDrive 'repository-name.txt') | Should -BeExactly $Name
        }
        finally {
            [Environment]::CurrentDirectory = $original
        }
    }

    It 'rejects unsafe repository argument <Name> before launching a child' -ForEach @(
        @{ Name = 'two words' }
        @{ Name = 'a"; throw "injected' }
        @{ Name = "a`nInjected" }
        @{ Name = '-option' }
        @{ Name = '$(injected)' }
    ) {
        { [CertificateProcessFixture]::RunPowershellScript($stub, $Name) } |
            Should -Throw '*Expected a PowerShell repository name*'
        Test-Path (Join-Path $TestDrive 'repository-name.txt') | Should -BeFalse
    }
}

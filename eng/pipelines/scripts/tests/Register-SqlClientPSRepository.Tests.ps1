# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

BeforeAll {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
    $scriptPath = Join-Path $repoRoot 'eng\scripts\Register-SqlClientPSRepository.ps1'
    $sourceLocation = 'https://sqlclientdrivers.pkgs.visualstudio.com/public/_packaging/sqlclient/nuget/v2'
}

Describe 'SqlClient PowerShell repository registration' {
    BeforeEach {
        Mock Get-PSRepository {}
        Mock Register-PSRepository {}
    }

    It 'registers the anonymous v2 feed as trusted and returns only its name' {
        $result = & $scriptPath

        @($result).Count | Should -Be 1
        $result | Should -BeExactly 'SqlClient'
        Should -Invoke Register-PSRepository -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'SqlClient' -and $SourceLocation -eq $sourceLocation -and
            $PackageManagementProvider -eq 'NuGet' -and $InstallationPolicy -eq 'Trusted' -and
            $ErrorAction -eq 'Stop'
        }
    }

    It 'reuses a matching registration without changing its trust policy (<Suffix>)' -ForEach @(
        @{ Suffix = '' }
        @{ Suffix = '/' }
    ) {
        Mock Get-PSRepository {
            [pscustomobject]@{
                Name = 'SqlClient'
                SourceLocation = $sourceLocation + $Suffix
                PackageManagementProvider = 'NuGet'
                InstallationPolicy = 'Untrusted'
            }
        }

        & $scriptPath | Should -BeExactly 'SqlClient'
        Should -Invoke Register-PSRepository -Times 0 -Exactly
    }

    It 'ignores unrelated repository registrations' {
        Mock Get-PSRepository {
            [pscustomobject]@{ Name = 'AnotherFeed'; SourceLocation = 'https://example.invalid/nuget/v2' }
        }

        & $scriptPath | Should -BeExactly 'SqlClient'
        Should -Invoke Register-PSRepository -Times 1 -Exactly
    }

    It 'rejects a conflicting <Conflict> without overwriting it' -ForEach @(
        @{ Conflict = 'source'; Location = 'https://example.invalid/nuget/v2'; Provider = 'NuGet' }
        @{ Conflict = 'provider'; Location = 'https://sqlclientdrivers.pkgs.visualstudio.com/public/_packaging/sqlclient/nuget/v2'; Provider = 'Other' }
    ) {
        Mock Get-PSRepository {
            [pscustomobject]@{
                Name = 'SqlClient'
                SourceLocation = $Location
                PackageManagementProvider = $Provider
            }
        }

        { & $scriptPath } | Should -Throw '*conflicting registration*'
        Should -Invoke Register-PSRepository -Times 0 -Exactly
    }

    It 'propagates repository enumeration failures' {
        Mock Get-PSRepository { throw 'repository lookup failed' }

        { & $scriptPath } | Should -Throw '*repository lookup failed*'
        Should -Invoke Register-PSRepository -Times 0 -Exactly
    }

    It 'propagates registration failures without returning a repository name' {
        Mock Register-PSRepository { throw 'repository registration failed' }

        { & $scriptPath } | Should -Throw '*repository registration failed*'
    }
}

Describe 'Governed module installation sites' {
    It 'resolves the shared helper relative to each script, independently of working directory' -ForEach @(
        @{ Path = 'eng\scripts\Configure-NetOnlyAccount.ps1' }
        @{ Path = 'src\Microsoft.Data.SqlClient\tests\ManualTests\makepfxcert.ps1' }
    ) {
        $path = Join-Path $repoRoot $Path
        $tokens = $null
        $errors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
        $errors.Count | Should -Be 0
        $joins = $ast.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.CommandAst] -and
            $node.GetCommandName() -eq 'Join-Path' -and
            $node.Extent.Text -like '*Register-SqlClientPSRepository.ps1*'
        }, $true)
        $joins.Count | Should -BeGreaterThan 0
        $resolved = foreach ($join in $joins) {
            $join.CommandElements[1].Extent.Text | Should -Be '$PSScriptRoot'
            $candidate = Join-Path (Split-Path $path) $join.CommandElements[2].Value
            if (Test-Path -LiteralPath $candidate) { $candidate; break }
        }
        (Resolve-Path $resolved).Path | Should -Be $scriptPath
        Get-Content $path -Raw | Should -Match 'Install-Module[^\r\n]+-Repository \$repository[^\r\n]+-ErrorAction Stop|(?s)Install-Module[^\r\n]+`\r?\n\s+-Repository \$repository[^\r\n]+-ErrorAction Stop'
    }

    It 'targets the governed repository in the Kerberos connectivity step' {
        $source = Get-Content (Join-Path $repoRoot 'eng\pipelines\ci\kerberos\linux-setup-step.yml') -Raw
        $source | Should -Match '\$repository = & "\$\(Build.SourcesDirectory\)/eng/scripts/Register-SqlClientPSRepository.ps1"'
        $source | Should -Match 'Install-Module -Name SqlServer -Repository \$repository -Force -Confirm:\$false -ErrorAction Stop'
    }

    It 'lets certificate module provisioning failures escape the certificate error handler' {
        $path = Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\ManualTests\makepfxcert.ps1'
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$null, [ref]$null)
        $install = $ast.Find({
            param($node)
            $node -is [System.Management.Automation.Language.CommandAst] -and
            $node.GetCommandName() -eq 'Install-Module'
        }, $true)
        $parent = $install.Parent
        while ($parent) {
            $parent | Should -Not -BeOfType ([System.Management.Automation.Language.TryStatementAst])
            $parent = $parent.Parent
        }
    }

    It 'provisions certificate modules from the <Layout> layout' -ForEach @(
        @{ Layout = 'source' }
        @{ Layout = 'test output' }
    ) {
        Mock Get-PSRepository {
            [pscustomobject]@{
                Name = 'SqlClient'
                SourceLocation = $sourceLocation
                PackageManagementProvider = 'NuGet'
            }
        }
        Mock Install-Module {}
        $path = Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\ManualTests\makepfxcert.ps1'
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$null, [ref]$null)
        $provision = $ast.Find({
            param($node)
            $node -is [System.Management.Automation.Language.IfStatementAst] -and
            $node.Clauses[0].Item1.Extent.Text -like '*Get-Module*OpenSSL*'
        }, $true)
        $directory = Split-Path $path
        if ($Layout -eq 'test output') {
            $directory = $TestDrive
            Copy-Item -LiteralPath $scriptPath -Destination $directory
        }
        $body = $provision.Clauses[0].Item2.Extent.Text.Replace('$PSScriptRoot', '$scriptDirectory')
        $run = [scriptblock]::Create("param(`$scriptDirectory) & $body")
        & $run $directory

        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'OpenSSL' -and $Repository -eq 'SqlClient' -and $Force -and $ErrorAction -eq 'Stop'
        }
        Mock Get-PSRepository { throw 'feed unavailable' }
        { & $run $directory } | Should -Throw '*feed unavailable*'
        Should -Invoke Install-Module -Times 1 -Exactly
    }

    It 'copies the shared helper alongside the certificate script in test output' {
        [xml] $project = Get-Content (Join-Path $repoRoot 'src\Microsoft.Data.SqlClient\tests\ManualTests\Microsoft.Data.SqlClient.ManualTests.csproj') -Raw
        $item = $project.Project.ItemGroup.None | Where-Object Link -eq 'Register-SqlClientPSRepository.ps1'
        $item.Include | Should -Be '$(RepoRoot)eng/scripts/Register-SqlClientPSRepository.ps1'
        $item.CopyToOutputDirectory | Should -Be 'PreserveNewest'
    }
}

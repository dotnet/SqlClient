<#
.SYNOPSIS
    Pester tests for verify-package-signatures.ps1.
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'verify-package-signatures.ps1'

    $script:packagesPath = Join-Path $TestDrive 'packages'
    New-Item -ItemType Directory -Force -Path (Join-Path $script:packagesPath 'SqlClient') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $script:packagesPath 'SqlServer') | Out-Null

    # Symbol packages are signed too, so both extensions must be picked up, and the nested layout
    # mirrors how each artifact is downloaded into its own subdirectory.
    Set-Content -LiteralPath (Join-Path $script:packagesPath 'SqlClient' 'Microsoft.Data.SqlClient.7.1.0.nupkg') -Value 'stub'
    Set-Content -LiteralPath (Join-Path $script:packagesPath 'SqlClient' 'Microsoft.Data.SqlClient.7.1.0.snupkg') -Value 'stub'
    Set-Content -LiteralPath (Join-Path $script:packagesPath 'SqlServer' 'Microsoft.SqlServer.Server.1.1.0.nupkg') -Value 'stub'

    function Invoke-VerifyPackageSignatures {
        param([string]$PackagesPath = $script:packagesPath)

        & $scriptPath -PackagesPath $PackagesPath -DotnetPath 'dotnet' *>&1 | Out-String
    }

    # Fails verification only for packages whose name matches, so tests can make a subset unsigned.
    function Set-DotnetMock {
        param([string]$FailPattern = '')

        $global:verifyPackageInvocations = @()
        Mock -CommandName 'dotnet' -MockWith {
            $global:verifyPackageInvocations += , @($args)
            $target = $args[-1]
            if ($FailPattern -and $target -match $FailPattern) {
                $global:LASTEXITCODE = 1
                return "unsigned"
            }

            $global:LASTEXITCODE = 0
            return "verified"
        }.GetNewClosure()
    }
}

AfterAll {
    Remove-Variable -Name 'verifyPackageInvocations' -Scope Global -ErrorAction SilentlyContinue
}

Describe 'verify-package-signatures.ps1' {
    It 'verifies every package and symbol package found' {
        Set-DotnetMock

        $output = Invoke-VerifyPackageSignatures
        $output | Should -Match 'All 3 package signature\(s\) verified'
        $global:verifyPackageInvocations.Count | Should -Be 3
    }

    It 'invokes dotnet nuget verify with --all' {
        Set-DotnetMock

        Invoke-VerifyPackageSignatures | Out-Null

        $first = $global:verifyPackageInvocations | Select-Object -First 1
        ($first -join ' ') | Should -Match 'nuget verify --all'
    }

    It 'fails when a package signature does not verify' {
        Set-DotnetMock -FailPattern 'SqlServer'

        { Invoke-VerifyPackageSignatures } | Should -Throw '*Microsoft.SqlServer.Server.1.1.0.nupkg*'
    }

    It 'checks every package before failing so all failures are reported' {
        Set-DotnetMock -FailPattern '\.nupkg$'

        # Two of the three files are .nupkg; both must appear rather than only the first.
        { Invoke-VerifyPackageSignatures } | Should -Throw '*failed for 2 of 3 package(s)*'
        $global:verifyPackageInvocations.Count | Should -Be 3
    }

    It 'throws when no packages are found' {
        Set-DotnetMock
        $empty = Join-Path $TestDrive 'empty'
        New-Item -ItemType Directory -Force -Path $empty | Out-Null

        { Invoke-VerifyPackageSignatures -PackagesPath $empty } | Should -Throw '*No package files were found*'
    }
}

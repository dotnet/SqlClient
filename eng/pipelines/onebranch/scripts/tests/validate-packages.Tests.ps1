<#
.SYNOPSIS
    Pester tests for validate-packages.ps1.
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'validate-packages.ps1'

    # Stands in for the built PackageValidator.dll; the script only checks that it exists.
    $script:validatorPath = Join-Path $TestDrive 'PackageValidator.dll'
    Set-Content -LiteralPath $script:validatorPath -Value 'stub'

    $script:packagesPath = Join-Path $TestDrive 'packages'
    New-Item -ItemType Directory -Force -Path $script:packagesPath | Out-Null
    Set-Content -LiteralPath (Join-Path $script:packagesPath 'Microsoft.Data.SqlClient.7.1.0.nupkg') -Value 'stub'
    Set-Content -LiteralPath (Join-Path $script:packagesPath 'Microsoft.SqlServer.Server.1.1.0.nupkg') -Value 'stub'

    $script:reportPath = Join-Path $TestDrive 'out' 'report.json'

    function Invoke-ValidatePackages {
        param(
            [string]$PackagesPath = $script:packagesPath,
            [string]$ValidatorPath = $script:validatorPath,
            [string]$SqlClientPackageVersion = '7.1.0-preview3.26238.3',
            [string]$SqlClientFileVersion = '7.1.0.26238',
            [string]$SqlServerPackageVersion = '',
            [string]$SqlServerFileVersion = '',
            [string[]]$FailOn = @('error')
        )

        & $scriptPath `
            -ValidatorPath $ValidatorPath `
            -PackagesPath $PackagesPath `
            -ReportPath $script:reportPath `
            -SqlClientPackageVersion $SqlClientPackageVersion `
            -SqlClientFileVersion $SqlClientFileVersion `
            -SqlServerPackageVersion $SqlServerPackageVersion `
            -SqlServerFileVersion $SqlServerFileVersion `
            -FailOn $FailOn `
            -DotnetPath 'dotnet' *>&1 | Out-String
    }

    # Captures the arguments of each invocation so tests can assert on what the validator was
    # asked to do, and controls the exit code of each run.
    function Set-DotnetMock {
        param(
            [int]$GateExitCode = 0,
            [int]$ReportExitCode = 0
        )

        $global:validatePackagesInvocations = @()
        Mock -CommandName 'dotnet' -MockWith {
            $global:validatePackagesInvocations += , @($args)
            # The first run carries --json and never gates; the second applies the gate.
            if ($args -contains '--json') {
                $global:LASTEXITCODE = $ReportExitCode
                return '{ "packages": [], "summary": {} }'
            }

            $global:LASTEXITCODE = $GateExitCode
            return 'validator output'
        }.GetNewClosure()
    }
}

AfterAll {
    Remove-Variable -Name 'validatePackagesInvocations' -Scope Global -ErrorAction SilentlyContinue
}

Describe 'validate-packages.ps1 Expectations' {
    BeforeEach {
        Set-DotnetMock
    }

    It 'applies the SqlClient family versions as wildcard expectations' {
        Invoke-ValidatePackages | Out-Null

        $gateArgs = $global:validatePackagesInvocations | Where-Object { $_ -notcontains '--json' } | Select-Object -First 1
        $gateArgs | Should -Contain '*=7.1.0-preview3.26238.3'
        $gateArgs | Should -Contain '*=7.1.0.26238'
    }

    It 'omits SqlServer expectations when its versions are not supplied' {
        Invoke-ValidatePackages | Out-Null

        $gateArgs = $global:validatePackagesInvocations | Where-Object { $_ -notcontains '--json' } | Select-Object -First 1
        ($gateArgs -join ' ') | Should -Not -Match 'Microsoft\.SqlServer\.Server='
    }

    It 'adds SqlServer expectations as a per-id override when supplied' {
        Invoke-ValidatePackages -SqlServerPackageVersion '1.1.0-preview1.26238.3' -SqlServerFileVersion '1.1.0.26238' | Out-Null

        $gateArgs = $global:validatePackagesInvocations | Where-Object { $_ -notcontains '--json' } | Select-Object -First 1
        $gateArgs | Should -Contain 'Microsoft.SqlServer.Server=1.1.0-preview1.26238.3'
        $gateArgs | Should -Contain 'Microsoft.SqlServer.Server=1.1.0.26238'
    }

    It 'passes each gate token as its own --fail-on argument' {
        Invoke-ValidatePackages -FailOn @('error', 'missing-symbols', 'package-unsigned') | Out-Null

        $gateArgs = $global:validatePackagesInvocations | Where-Object { $_ -notcontains '--json' } | Select-Object -First 1
        $joined = $gateArgs -join ' '
        $joined | Should -Match '--fail-on error'
        $joined | Should -Match '--fail-on missing-symbols'
        $joined | Should -Match '--fail-on package-unsigned'
    }

    It 'splits a single comma-separated gate token, as an Azure Pipelines argument line supplies it' {
        Invoke-ValidatePackages -FailOn 'error, missing-symbols' | Out-Null

        $gateArgs = $global:validatePackagesInvocations | Where-Object { $_ -notcontains '--json' } | Select-Object -First 1
        $joined = $gateArgs -join ' '
        $joined | Should -Match '--fail-on error'
        $joined | Should -Match '--fail-on missing-symbols'
        $joined | Should -Not -Match 'error,'
    }

    It 'writes the JSON report before applying the gate' {
        Invoke-ValidatePackages | Out-Null

        # The reporting run must come first so the report survives a failing gate.
        $firstInvocation = $global:validatePackagesInvocations | Select-Object -First 1
        $firstInvocation | Should -Contain '--json'
        Test-Path -LiteralPath $script:reportPath | Should -BeTrue
    }

    It 'does not gate the reporting run' {
        Invoke-ValidatePackages -FailOn @('error') | Out-Null

        $reportArgs = $global:validatePackagesInvocations | Where-Object { $_ -contains '--json' } | Select-Object -First 1
        ($reportArgs -join ' ') | Should -Not -Match '--fail-on'
    }
}

Describe 'validate-packages.ps1 Exit Codes' {
    It 'succeeds when the validator reports no gating findings' {
        Set-DotnetMock -GateExitCode 0

        $output = Invoke-ValidatePackages
        $output | Should -Match 'Package validation passed'
    }

    It 'fails when a gate is tripped' {
        Set-DotnetMock -GateExitCode 2

        { Invoke-ValidatePackages -FailOn @('error', 'missing-symbols') } |
            Should -Throw '*matched the gate (error, missing-symbols)*'
    }

    It 'reports an unexpected validator failure distinctly from a tripped gate' {
        Set-DotnetMock -GateExitCode 1

        { Invoke-ValidatePackages } | Should -Throw '*exited unexpectedly with code 1*'
    }

    It 'fails the reporting run before gating so the real cause is not obscured' {
        Set-DotnetMock -ReportExitCode 1

        { Invoke-ValidatePackages } | Should -Throw '*failed while writing the JSON report (exit code 1)*'

        # The gating run must not have been reached.
        $global:validatePackagesInvocations.Count | Should -Be 1
    }
}

Describe 'validate-packages.ps1 Error Handling' {
    BeforeEach {
        Set-DotnetMock
    }

    It 'throws when the validator is missing' {
        { Invoke-ValidatePackages -ValidatorPath (Join-Path $TestDrive 'absent.dll') } |
            Should -Throw '*PackageValidator was not found*'
    }

    It 'throws when no packages are found' {
        $empty = Join-Path $TestDrive 'empty'
        New-Item -ItemType Directory -Force -Path $empty | Out-Null

        { Invoke-ValidatePackages -PackagesPath $empty } | Should -Throw '*No .nupkg files were found*'
    }

    It 'rejects a half-supplied SqlServer expectation' {
        # Supplying only one would assert a package version without its file version.
        { Invoke-ValidatePackages -SqlServerPackageVersion '1.1.0' } |
            Should -Throw '*must be supplied together*'
    }
}

<#
.SYNOPSIS
    Pester tests for compute-versions.ps1.
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'compute-versions.ps1'
    $projectPath = Join-Path $TestDrive 'build.proj'
    Set-Content -LiteralPath $projectPath -Value '<Project />'

    # An arbitrary but well-formed pipeline build number, used only by the tests that exercise the
    # path which consumes one. The script never reads the ambient build number; the pipeline passes
    # $(Build.BuildNumber) as a parameter, so any well-formed value works here and a fixed one keeps
    # the expected output deterministic. Assertions derive from these rather than repeating literals
    # so the relationship between the two is visible.
    $script:testBuildNumber = '26238.3'
    $script:testBuildNumberPattern = [regex]::Escape($script:testBuildNumber)
    $script:testFileVersionBuildNumber = $script:testBuildNumber.Split('.')[0]

    function Invoke-ComputeVersions {
        param(
            [long]$Revision = 42,
            [string]$BuildNumber = '',
            [bool]$BuildSqlServer = $true,
            [bool]$AddRevision = $true
        )

        & $scriptPath `
            -ProjectPath $projectPath `
            -Revision $Revision `
            -BuildNumber $BuildNumber `
            -BuildSqlServer $BuildSqlServer `
            -AddRevision $AddRevision *>&1 | Out-String
    }

    # Alternates between the SqlClient and SqlServer GetVersions targets, which the script always
    # invokes in that order.
    function Set-DotnetMock {
        param(
            [string]$SqlClientPackageVersion = '7.1.0-preview3',
            [string]$SqlServerPackageVersion = '1.1.0-preview1'
        )

        $global:computeVersionsDotnetCallCount = 0
        Mock -CommandName 'dotnet' -MockWith {
            $global:LASTEXITCODE = 0
            $global:computeVersionsDotnetCallCount++
            if ($global:computeVersionsDotnetCallCount % 2 -eq 1) {
                return @(
                    "  PackageVersion: $SqlClientPackageVersion"
                    '  PublishedVersion: 7.0.0'
                )
            }

            return @(
                "  PackageVersion: $SqlServerPackageVersion"
                '  PublishedVersion: 1.0.0'
            )
        }.GetNewClosure()
    }

    function Set-SuccessfulDotnetMock {
        Set-DotnetMock
    }
}

AfterAll {
    Remove-Variable -Name 'computeVersionsDotnetCallCount' -Scope Global -ErrorAction SilentlyContinue
}

Describe 'compute-versions.ps1 Effective Versions' {
    BeforeEach {
        Set-SuccessfulDotnetMock
    }

    It 'appends the build number after the prerelease suffix when package revisioning is disabled' {
        $output = Invoke-ComputeVersions -AddRevision $false -BuildNumber $script:testBuildNumber

        $output | Should -Match "SqlClientPackageVersion;isOutput=true]7\.1\.0-preview3\.$script:testBuildNumberPattern"
        $output | Should -Match "SqlServerPackageVersion;isOutput=true]1\.1\.0-preview1\.$script:testBuildNumberPattern"
        $output | Should -Match "VersionRevision;isOutput=true]$script:testFileVersionBuildNumber"
    }

    It 'inserts the revision before prerelease suffixes for built packages' {
        $output = Invoke-ComputeVersions

        $output | Should -Match 'SqlClientPackageVersion;isOutput=true]7\.1\.0\.42-preview3'
        $output | Should -Match 'SqlServerPackageVersion;isOutput=true]1\.1\.0\.42-preview1'
    }

    It 'retains the published SqlServer package when SqlServer is not built' {
        $output = Invoke-ComputeVersions -BuildSqlServer $false

        $output | Should -Match 'SqlClientPackageVersion;isOutput=true]7\.1\.0\.42-preview3'
        $output | Should -Match 'SqlServerPackageVersion;isOutput=true]1\.0\.0'
        $output | Should -Not -Match 'SqlServerPackageVersion;isOutput=true]1\.0\.0\.42'
    }

    It 'wraps revisions above 65535 and logs the mapping as information' {
        $output = Invoke-ComputeVersions -Revision 65536

        $output | Should -Match 'Revision 65536.*wrapped to 1'
        $output | Should -Not -Match 'task\.logissue type=warning'
        $output | Should -Match 'SqlClientPackageVersion;isOutput=true]7\.1\.0\.1-preview3'
        $output | Should -Match 'SqlServerPackageVersion;isOutput=true]1\.1\.0\.1-preview1'
        $output | Should -Match 'VersionRevision;isOutput=true]1'
    }

    It 'emits the build number rather than the wrapped revision when package revisioning is disabled' {
        $output = Invoke-ComputeVersions -Revision 65536 -AddRevision $false -BuildNumber $script:testBuildNumber

        $output | Should -Not -Match 'task\.logissue type=warning'
        $output | Should -Not -Match 'wrapped to'
        $output | Should -Match "SqlClientPackageVersion;isOutput=true]7\.1\.0-preview3\.$script:testBuildNumberPattern"
        $output | Should -Match "SqlServerPackageVersion;isOutput=true]1\.1\.0-preview1\.$script:testBuildNumberPattern"
        $output | Should -Match "VersionRevision;isOutput=true]$script:testFileVersionBuildNumber"
    }

    It 'retains the published SqlServer package unstamped when SqlServer is not built' {
        $output = Invoke-ComputeVersions -BuildSqlServer $false -AddRevision $false -BuildNumber $script:testBuildNumber

        $output | Should -Match "SqlClientPackageVersion;isOutput=true]7\.1\.0-preview3\.$script:testBuildNumberPattern"
        $output | Should -Match 'SqlServerPackageVersion;isOutput=true]1\.0\.0'
        $output | Should -Not -Match "SqlServerPackageVersion;isOutput=true]1\.0\.0\.$script:testFileVersionBuildNumber"
    }

    It 'omits the build number from non-preview package versions when package revisioning is disabled' {
        Set-DotnetMock -SqlClientPackageVersion '7.1.0' -SqlServerPackageVersion '1.1.0'

        $output = Invoke-ComputeVersions -AddRevision $false -BuildNumber $script:testBuildNumber

        $output | Should -Match 'SqlClientPackageVersion;isOutput=true]7\.1\.0(\r?\n|$)'
        $output | Should -Match 'SqlServerPackageVersion;isOutput=true]1\.1\.0(\r?\n|$)'
        $output | Should -Not -Match "SqlClientPackageVersion;isOutput=true]7\.1\.0[\.-]$script:testFileVersionBuildNumber"
        $output | Should -Not -Match "SqlServerPackageVersion;isOutput=true]1\.1\.0[\.-]$script:testFileVersionBuildNumber"

        # The file version is still stamped so every build produces a distinct, date-encoded
        # file version even for non-preview releases.
        $output | Should -Match "VersionRevision;isOutput=true]$script:testFileVersionBuildNumber"
    }

    It 'revises non-preview package versions when package revisioning is enabled' {
        Set-DotnetMock -SqlClientPackageVersion '7.1.0' -SqlServerPackageVersion '1.1.0'

        $output = Invoke-ComputeVersions

        $output | Should -Match 'SqlClientPackageVersion;isOutput=true]7\.1\.0\.42'
        $output | Should -Match 'SqlServerPackageVersion;isOutput=true]1\.1\.0\.42'
        $output | Should -Match 'VersionRevision;isOutput=true]42'
    }
}

Describe 'compute-versions.ps1 Error Handling' {
    It 'rejects a non-positive revision' {
        { Invoke-ComputeVersions -Revision 0 } | Should -Throw
    }

    It 'requires a build number when package revisioning is disabled' {
        Set-SuccessfulDotnetMock

        { Invoke-ComputeVersions -AddRevision $false } |
            Should -Throw '*BuildNumber is required when AddRevision is false*'
    }

    It 'rejects a malformed build number' {
        { Invoke-ComputeVersions -AddRevision $false -BuildNumber 'not-a-build-number' } | Should -Throw
    }

    It 'throws when a GetVersions target fails' {
        Mock -CommandName 'dotnet' -MockWith {
            $global:LASTEXITCODE = 1
            return 'simulated target failure'
        }

        { Invoke-ComputeVersions } | Should -Throw '*simulated target failure*'
    }

    It 'throws when required version labels are absent' {
        Mock -CommandName 'dotnet' -MockWith {
            $global:LASTEXITCODE = 0
            return 'Build succeeded without version labels'
        }

        { Invoke-ComputeVersions } | Should -Throw '*Failed to extract PackageVersion*'
    }

    It 'throws when a revised package does not have a three-part numeric base' {
        $global:computeVersionsDotnetCallCount = 0
        Mock -CommandName 'dotnet' -MockWith {
            $global:LASTEXITCODE = 0
            $global:computeVersionsDotnetCallCount++
            if ($global:computeVersionsDotnetCallCount -eq 1) {
                return @('PackageVersion: 7.1-preview3')
            }

            return @('PackageVersion: 1.1.0-preview1', 'PublishedVersion: 1.0.0')
        }

        { Invoke-ComputeVersions } | Should -Throw "*Expected a three-part numeric version base*"
    }
}

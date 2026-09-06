<#
.SYNOPSIS
    Pester tests for compute-versions.ps1.
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'compute-versions.ps1'
    $buildProjectPath = Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..' '..' '..' 'build.proj')
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
            [string]$BuildNumber = $script:testBuildNumber,
            [bool]$BuildSqlServer = $true
        )

        & $scriptPath `
            -ProjectPath $projectPath `
            -BuildNumber $BuildNumber `
            -BuildSqlServer $BuildSqlServer *>&1 | Out-String
    }

    # Alternates between the SqlClient and SqlServer GetVersions targets, which the script always
    # invokes in that order.  The versions returned here are already stamped, because Versions.props
    # applies the build number before the script ever sees them.
    function Set-DotnetMock {
        param(
            [string]$SqlClientPackageVersion = "7.1.0-preview3.$script:testBuildNumber",
            [string]$SqlServerPackageVersion = "1.1.0-preview1.$script:testBuildNumber"
        )

        $global:computeVersionsDotnetCallCount = 0
        Mock -CommandName 'dotnet' -MockWith {
            $global:LASTEXITCODE = 0
            $global:computeVersionsDotnetCallCount++
            if ($global:computeVersionsDotnetCallCount % 2 -eq 1) {
                return @(
                    "  PackageVersion: $SqlClientPackageVersion"
                    '  FileVersion: 7.1.0.26238'
                    '  PublishedVersion: 7.0.0'
                )
            }

            return @(
                "  PackageVersion: $SqlServerPackageVersion"
                '  FileVersion: 1.1.0.26238'
                '  PublishedVersion: 1.0.0'
            )
        }.GetNewClosure()
    }

    function Set-SuccessfulDotnetMock {
        Set-DotnetMock
    }

    function Invoke-VersionTarget {
        param(
            [Parameter(Mandatory)]
            [string]$Target,

            [Parameter(Mandatory)]
            [string]$NextVersionProperty,

            [Parameter(Mandatory)]
            [string]$BaseVersion,

            [string]$BuildSuffix
        )

        $arguments = @(
            'build'
            $buildProjectPath
            "-t:$Target"
            '-v:m'
            '-nologo'
            "-p:BuildNumber=$script:testBuildNumber"
            "-p:$NextVersionProperty=$BaseVersion"
        )
        if ($BuildSuffix) {
            $arguments += "-p:BuildSuffix=$BuildSuffix"
        }

        $output = & dotnet @arguments 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) {
            throw "$Target failed with exit code ${LASTEXITCODE}:`n$output"
        }

        $output
    }
}

AfterAll {
    Remove-Variable -Name 'computeVersionsDotnetCallCount' -Scope Global -ErrorAction SilentlyContinue
}

Describe 'compute-versions.ps1 Effective Versions' {
    BeforeEach {
        Set-SuccessfulDotnetMock
    }

    It 'forwards the stamped prerelease versions from Versions.props' {
        $output = Invoke-ComputeVersions

        $output | Should -Match "SqlClientPackageVersion;isOutput=true]7\.1\.0-preview3\.$script:testBuildNumberPattern"
        $output | Should -Match "SqlServerPackageVersion;isOutput=true]1\.1\.0-preview1\.$script:testBuildNumberPattern"
        $output | Should -Match 'SqlClientApiScanVersion;isOutput=true]7\.1'
        $output | Should -Match 'SqlServerApiScanVersion;isOutput=true]1\.1'
        $output | Should -Match 'APIScan registration versions:\s+SqlClient \(family\): 7\.1\s+SqlServer:\s+1\.1'
        $output | Should -Match 'SqlClientFileVersion;isOutput=true]7\.1\.0\.26238'
        $output | Should -Match 'SqlServerFileVersion;isOutput=true]1\.1\.0\.26238'
    }

    It 'retains the published SqlServer package when SqlServer is not built' {
        $output = Invoke-ComputeVersions -BuildSqlServer $false

        $output | Should -Match "SqlClientPackageVersion;isOutput=true]7\.1\.0-preview3\.$script:testBuildNumberPattern"
        $output | Should -Match 'SqlServerPackageVersion;isOutput=true]1\.0\.0'
        $output | Should -Match 'SqlServerApiScanVersion;isOutput=true]1\.0'
        $output | Should -Not -Match "SqlServerPackageVersion;isOutput=true]1\.0\.0[\.-]$script:testFileVersionBuildNumber"

        # An unbuilt SqlServer is never stamped, so it has no effective file version.
        $output | Should -Match 'SqlServerFileVersion;isOutput=true](\r?\n|$)'
    }

    It 'forwards unstamped non-preview package versions' {
        Set-DotnetMock -SqlClientPackageVersion '7.1.0' -SqlServerPackageVersion '1.1.0'

        $output = Invoke-ComputeVersions

        $output | Should -Match 'SqlClientPackageVersion;isOutput=true]7\.1\.0(\r?\n|$)'
        $output | Should -Match 'SqlServerPackageVersion;isOutput=true]1\.1\.0(\r?\n|$)'
        $output | Should -Not -Match "SqlClientPackageVersion;isOutput=true]7\.1\.0[\.-]$script:testFileVersionBuildNumber"
        $output | Should -Not -Match "SqlServerPackageVersion;isOutput=true]1\.1\.0[\.-]$script:testFileVersionBuildNumber"

        # The file version is still stamped so every build produces a date-encoded file version even
        # for non-preview releases.
        $output | Should -Match 'SqlClientFileVersion;isOutput=true]7\.1\.0\.26238'
    }
}

Describe 'GetVersions target package composition' {
    It '<Target> composes <Case> package and file versions' -ForEach @(
        @{
            Target = 'GetVersionsSqlClient'; NextVersionProperty = 'SqlClientNextVersion'
            BaseVersion = '7.1.0'; BuildSuffix = ''; ExpectedPackageVersion = '7.1.0'
            ExpectedFileVersion = '7.1.0.26238'; Case = 'a stable base without a suffix'
        }
        @{
            Target = 'GetVersionsSqlClient'; NextVersionProperty = 'SqlClientNextVersion'
            BaseVersion = '7.1.0'; BuildSuffix = 'ci'; ExpectedPackageVersion = '7.1.0-ci.26238.3'
            ExpectedFileVersion = '7.1.0.26238'; Case = 'a stable base with a suffix'
        }
        @{
            Target = 'GetVersionsSqlClient'; NextVersionProperty = 'SqlClientNextVersion'
            BaseVersion = '7.1.0-preview3'; BuildSuffix = ''; ExpectedPackageVersion = '7.1.0-preview3.26238.3'
            ExpectedFileVersion = '7.1.0.26238'; Case = 'a prerelease base without a suffix'
        }
        @{
            Target = 'GetVersionsSqlClient'; NextVersionProperty = 'SqlClientNextVersion'
            BaseVersion = '7.1.0-preview3'; BuildSuffix = 'ci'; ExpectedPackageVersion = '7.1.0-preview3-ci.26238.3'
            ExpectedFileVersion = '7.1.0.26238'; Case = 'a prerelease base with a suffix'
        }
        @{
            Target = 'GetVersionsSqlServer'; NextVersionProperty = 'SqlServerNextVersion'
            BaseVersion = '1.1.0'; BuildSuffix = ''; ExpectedPackageVersion = '1.1.0'
            ExpectedFileVersion = '1.1.0.26238'; Case = 'a stable base without a suffix'
        }
        @{
            Target = 'GetVersionsSqlServer'; NextVersionProperty = 'SqlServerNextVersion'
            BaseVersion = '1.1.0'; BuildSuffix = 'ci'; ExpectedPackageVersion = '1.1.0-ci.26238.3'
            ExpectedFileVersion = '1.1.0.26238'; Case = 'a stable base with a suffix'
        }
        @{
            Target = 'GetVersionsSqlServer'; NextVersionProperty = 'SqlServerNextVersion'
            BaseVersion = '1.1.0-preview1'; BuildSuffix = ''; ExpectedPackageVersion = '1.1.0-preview1.26238.3'
            ExpectedFileVersion = '1.1.0.26238'; Case = 'a prerelease base without a suffix'
        }
        @{
            Target = 'GetVersionsSqlServer'; NextVersionProperty = 'SqlServerNextVersion'
            BaseVersion = '1.1.0-preview1'; BuildSuffix = 'ci'; ExpectedPackageVersion = '1.1.0-preview1-ci.26238.3'
            ExpectedFileVersion = '1.1.0.26238'; Case = 'a prerelease base with a suffix'
        }
    ) {
        $output = Invoke-VersionTarget `
            -Target $Target `
            -NextVersionProperty $NextVersionProperty `
            -BaseVersion $BaseVersion `
            -BuildSuffix $BuildSuffix

        $output | Should -Match "PackageVersion:\s+$([regex]::Escape($ExpectedPackageVersion))(\r?\n|$)"
        $output | Should -Match "FileVersion:\s+$([regex]::Escape($ExpectedFileVersion))(\r?\n|$)"
    }
}

Describe 'compute-versions.ps1 Error Handling' {
    It 'rejects a malformed build number' {
        { Invoke-ComputeVersions -BuildNumber 'not-a-build-number' } | Should -Throw
    }

    It 'requires a build number' {
        # Bound as empty rather than omitted; omitting a mandatory parameter prompts interactively.
        { Invoke-ComputeVersions -BuildNumber '' } | Should -Throw
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

    It 'throws when a FileVersion label is absent' {
        Mock -CommandName 'dotnet' -MockWith {
            $global:LASTEXITCODE = 0
            return 'PackageVersion: 7.1.0-preview3.26238.3'
        }

        { Invoke-ComputeVersions } | Should -Throw '*Failed to extract FileVersion*'
    }
}

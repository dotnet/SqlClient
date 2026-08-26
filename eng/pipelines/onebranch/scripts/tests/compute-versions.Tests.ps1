<#
.SYNOPSIS
    Pester tests for compute-versions.ps1.
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'compute-versions.ps1'
    $projectPath = Join-Path $TestDrive 'build.proj'
    Set-Content -LiteralPath $projectPath -Value '<Project />'

    function Invoke-ComputeVersions {
        param(
            [long]$Revision = 42,
            [string]$BuildNumber = '26238.3',
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

    function Set-SuccessfulDotnetMock {
        $global:computeVersionsDotnetCallCount = 0
        Mock -CommandName 'dotnet' -MockWith {
            $global:LASTEXITCODE = 0
            $global:computeVersionsDotnetCallCount++
            if ($global:computeVersionsDotnetCallCount % 2 -eq 1) {
                return @(
                    '  PackageVersion: 7.1.0-preview3'
                    '  PublishedVersion: 7.0.0'
                )
            }

            return @(
                '  PackageVersion: 1.1.0-preview1'
                '  PublishedVersion: 1.0.0'
            )
        }
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
        $output = Invoke-ComputeVersions -AddRevision $false

        $output | Should -Match 'SqlClientPackageVersion;isOutput=true]7\.1\.0-preview3\.26238\.3'
        $output | Should -Match 'SqlServerPackageVersion;isOutput=true]1\.1\.0-preview1\.26238\.3'
        $output | Should -Match 'VersionRevision;isOutput=true]26238'
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
        $output = Invoke-ComputeVersions -Revision 65536 -AddRevision $false

        $output | Should -Not -Match 'task\.logissue type=warning'
        $output | Should -Match 'SqlClientPackageVersion;isOutput=true]7\.1\.0-preview3\.26238\.3'
        $output | Should -Match 'SqlServerPackageVersion;isOutput=true]1\.1\.0-preview1\.26238\.3'
        $output | Should -Match 'VersionRevision;isOutput=true]26238'
    }

    It 'retains the published SqlServer package unstamped when SqlServer is not built' {
        $output = Invoke-ComputeVersions -BuildSqlServer $false -AddRevision $false

        $output | Should -Match 'SqlClientPackageVersion;isOutput=true]7\.1\.0-preview3\.26238\.3'
        $output | Should -Match 'SqlServerPackageVersion;isOutput=true]1\.0\.0'
        $output | Should -Not -Match 'SqlServerPackageVersion;isOutput=true]1\.0\.0\.26238'
    }

    It 'leaves stable package versions unstamped' {
        Mock -CommandName 'dotnet' -MockWith {
            $global:LASTEXITCODE = 0
            return @(
                '  PackageVersion: 7.1.0'
                '  PublishedVersion: 7.0.0'
            )
        }

        $output = Invoke-ComputeVersions -AddRevision $false

        $output | Should -Match 'SqlClientPackageVersion;isOutput=true]7\.1\.0\r?\n'
        $output | Should -Not -Match 'SqlClientPackageVersion;isOutput=true]7\.1\.0\.26238'
        $output | Should -Match 'VersionRevision;isOutput=true]26238'
    }
}

Describe 'compute-versions.ps1 Error Handling' {
    It 'rejects a non-positive revision' {
        { Invoke-ComputeVersions -Revision 0 } | Should -Throw
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

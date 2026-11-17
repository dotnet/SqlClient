<#
.SYNOPSIS
    Pester tests for new-version-bump-pr.ps1.
#>

BeforeAll {
    . (Join-Path $PSScriptRoot '..' 'new-version-bump-pr.ps1')

    $script:milestones = @(
        '6.1.9', '7.0.4', '7.1.2', '8.0.0',
        '8.1.0-preview1', '8.1.0-preview2', '8.1.0-preview3', '8.1.0'
    )
}

<#
.SYNOPSIS
    Verifies that only main and release/X.Y map to GitHub branches, without internal/.
#>
Describe 'Get-GitHubTargetBranch' {
    It 'Maps <Source> to <Expected>' -TestCases @(
        @{ Source = 'refs/heads/internal/main'; Expected = 'main' }
        @{ Source = 'refs/heads/internal/release/7.1'; Expected = 'release/7.1' }
        @{ Source = 'refs/heads/main'; Expected = 'main' }
        @{ Source = 'refs/heads/release/6.1'; Expected = 'release/6.1' }
    ) {
        # Act
        $result = Get-GitHubTargetBranch $Source

        # Assert
        $result | Should -Be $Expected
    }

    It 'Returns null for <Source>' -TestCases @(
        @{ Source = 'refs/heads/dev/cheena/feature' }
        @{ Source = 'refs/heads/internal/dev/cheena/feature' }
        @{ Source = 'refs/heads/internal/release/7.1-hotfix' }
        @{ Source = 'refs/heads/feature/main' }
        @{ Source = 'refs/pull/123/merge' }
        @{ Source = 'refs/tags/v7.1.1' }
        @{ Source = '' }
    ) {
        # Act
        $result = Get-GitHubTargetBranch $Source

        # Assert
        $result | Should -BeNullOrEmpty
    }
}

<#
.SYNOPSIS
    Verifies version ordering, including GA versus preview and numeric preview labels.
#>
Describe 'Compare-SqlClientVersion' {
    It 'Orders <Left> before <Right>' -TestCases @(
        @{ Left = '8.1.0-preview1'; Right = '8.1.0-preview2' }
        @{ Left = '8.1.0-preview2'; Right = '8.1.0-preview10' }
        @{ Left = '8.1.0-preview3'; Right = '8.1.0' }
        @{ Left = '8.1.0'; Right = '8.1.1' }
        @{ Left = '7.9.9'; Right = '8.0.0' }
    ) {
        # Act
        $result = Compare-SqlClientVersion `
            (ConvertTo-SqlClientVersion $Left) (ConvertTo-SqlClientVersion $Right)

        # Assert
        $result | Should -BeLessThan 0
    }
}

<#
.SYNOPSIS
    Verifies next-version selection for release branches and main.
#>
Describe 'Get-NextSqlClientVersion' {
    It 'Bumps the patch on <Branch> after GA <Published>' -TestCases @(
        @{ Branch = 'release/7.1'; Published = '7.1.1'; Expected = '7.1.2' }
        @{ Branch = 'release/7.0'; Published = '7.0.0'; Expected = '7.0.1' }
        @{ Branch = 'main'; Published = '8.0.0'; Expected = '8.0.1' }
        @{ Branch = 'main'; Published = '8.1.0'; Expected = '8.1.1' }
    ) {
        # Act
        $result = Get-NextSqlClientVersion -TargetBranch $Branch -PublishedVersion $Published `
            -MilestoneTitles $script:milestones

        # Assert
        $result | Should -Be $Expected
    }

    It 'Moves a release branch preview to its GA version' {
        # Act
        $result = Get-NextSqlClientVersion -TargetBranch 'release/7.1' `
            -PublishedVersion '7.1.0-preview3'

        # Assert
        $result | Should -Be '7.1.0'
    }

    It 'Picks the next open milestone on main after <Published>' -TestCases @(
        @{ Published = '8.1.0-preview1'; Expected = '8.1.0-preview2' }
        @{ Published = '8.1.0-preview3'; Expected = '8.1.0' }
    ) {
        # Act
        $result = Get-NextSqlClientVersion -TargetBranch 'main' -PublishedVersion $Published `
            -MilestoneTitles $script:milestones

        # Assert
        $result | Should -Be $Expected
    }

    It 'Moves a main preview to its GA version without a later milestone in its series' {
        # Act
        $result = Get-NextSqlClientVersion -TargetBranch 'main' -PublishedVersion '8.2.0-preview1' `
            -MilestoneTitles $script:milestones

        # Assert
        $result | Should -Be '8.2.0'
    }

    It 'Ignores milestone titles that are not versions' {
        # Act
        $result = Get-NextSqlClientVersion -TargetBranch 'main' -PublishedVersion '8.1.0-preview1' `
            -MilestoneTitles @('Future', '8.1.0-preview2')

        # Assert
        $result | Should -Be '8.1.0-preview2'
    }

    It 'Throws for an invalid published version' {
        # Act / Assert
        { Get-NextSqlClientVersion -TargetBranch 'main' -PublishedVersion 'abc' } |
            Should -Throw '*not a valid SqlClient version*'
    }
}

<#
.SYNOPSIS
    Verifies reading and updating SqlClientNextVersion in Versions.props content.
#>
Describe 'Versions.props content' {
    It 'Reads and replaces SqlClientNextVersion only' {
        # Arrange
        $content = "<Project>`n  <SqlClientNextVersion>7.1.1</SqlClientNextVersion>`n" +
            "  <SqlServerNextVersion>1.1.0</SqlServerNextVersion>`n</Project>"

        # Act
        $updated = Set-VersionInProps -Content $content -Version '7.1.2'

        # Assert
        Get-VersionFromProps $content | Should -Be '7.1.1'
        Get-VersionFromProps $updated | Should -Be '7.1.2'
        $updated | Should -Match '<SqlServerNextVersion>1.1.0</SqlServerNextVersion>'
    }

    It 'Returns null when the property is missing or duplicated' {
        # Arrange
        $duplicated = '<SqlClientNextVersion>1.0.0</SqlClientNextVersion>' * 2

        # Act / Assert
        Get-VersionFromProps '<Project />' | Should -BeNullOrEmpty
        Get-VersionFromProps $duplicated | Should -BeNullOrEmpty
    }
}

<#
.SYNOPSIS
    Verifies the PR milestone falls back to the closest earlier milestone.
#>
Describe 'Get-PrMilestoneTitle' {
    It 'Picks <Expected> for next version <Next>' -TestCases @(
        @{ Next = '8.1.0-preview2'; Expected = '8.1.0-preview2' }
        @{ Next = '8.1.1'; Expected = '8.1.0' }
        @{ Next = '7.1.3'; Expected = '7.1.2' }
        @{ Next = '8.0.0-preview1'; Expected = '7.1.2' }
    ) {
        # Act
        $result = Get-PrMilestoneTitle -NextVersion $Next `
            -MilestoneTitles (@('Future') + $script:milestones)

        # Assert
        $result | Should -Be $Expected
    }

    It 'Returns null when no milestone is at or below the next version' {
        # Act
        $result = Get-PrMilestoneTitle -NextVersion '1.0.0' -MilestoneTitles $script:milestones

        # Assert
        $result | Should -BeNullOrEmpty
    }
}

<#
.SYNOPSIS
    Verifies the end-to-end flow against a mocked GitHub API.
#>
Describe 'Invoke-VersionBumpPr' {
    BeforeEach {
        # Arrange: a published 8.1.0 build of main, and GitHub main still at 8.1.0.
        $VersionsPropsPath = Join-Path $TestDrive 'Versions.props'
        Set-Content -LiteralPath $VersionsPropsPath `
            -Value '<SqlClientNextVersion>8.1.0</SqlClientNextVersion>'
        $env:BUILD_SOURCEBRANCH = 'refs/heads/internal/main'
        $env:TF_BUILD = 'True'
        $GitHubRepository = 'dotnet/SqlClient'
        $Token = 'token'
        $DryRun = $false
        $props = [Convert]::ToBase64String(
            [Text.Encoding]::UTF8.GetBytes('<SqlClientNextVersion>8.1.0</SqlClientNextVersion>'))

        # Arrange: 100 unrelated milestones on page 1, so 8.1.0 is only found on page 2.
        $page1 = 1..100 | ForEach-Object {
            [pscustomobject]@{ title = "old-$_"; number = $_; state = 'closed' }
        }
        $page2 = @([pscustomobject]@{ title = '8.1.0'; number = 500; state = 'closed' })

        Mock Write-Host {}
        Mock Write-PipelineWarning {}
        Mock Invoke-GitHubApi { $page1 } -ParameterFilter { $Path -like '*milestones*page=1' }
        Mock Invoke-GitHubApi { $page2 } -ParameterFilter { $Path -like '*milestones*page=2' }
        Mock Invoke-GitHubApi { [pscustomobject]@{ content = $props; sha = 'filesha' } } `
            -ParameterFilter { $Path -like '*contents*' -and -not $Method }
        Mock Invoke-GitHubApi { ,@() } -ParameterFilter { $Path -like '*pulls?state=open*' }
        Mock Invoke-GitHubApi { [pscustomobject]@{ object = @{ sha = 'basesha' } } } `
            -ParameterFilter { $Path -like '*git/ref/heads/*' }
        Mock Invoke-GitHubApi {} -ParameterFilter { $Method -in 'PUT', 'PATCH' -or
            ($Method -eq 'POST' -and $Path -like '*git/refs') }
        Mock Invoke-GitHubApi { [pscustomobject]@{ number = 42; html_url = 'pr-url' } } `
            -ParameterFilter { $Method -eq 'POST' -and $Path -like '*/pulls' }
    }

    AfterEach {
        # Cleanup: restore the pipeline variable shared with other tests.
        $env:BUILD_SOURCEBRANCH = $script:savedSourceBranch
        $env:TF_BUILD = $script:savedTfBuild
    }

    BeforeAll {
        $script:savedSourceBranch = $env:BUILD_SOURCEBRANCH
        $script:savedTfBuild = $env:TF_BUILD
    }

    It 'Opens the PR against the built release branch without internal/' {
        # Arrange
        $env:BUILD_SOURCEBRANCH = 'refs/heads/internal/release/8.1'

        # Act
        Invoke-VersionBumpPr

        # Assert
        Should -Invoke Invoke-GitHubApi -Times 1 -ParameterFilter {
            $Method -eq 'POST' -and $Path -like '*/pulls' -and $Body.base -eq 'release/8.1'
        }
    }

    It 'Throws for a real run outside an Azure DevOps build' {
        # Arrange
        $env:TF_BUILD = $null

        # Act / Assert
        { Invoke-VersionBumpPr } | Should -Throw '*Only -DryRun*'
        Should -Invoke Invoke-GitHubApi -Times 0
    }

    It 'Skips with a warning when built from a dev branch' {
        # Arrange
        $env:BUILD_SOURCEBRANCH = 'refs/heads/dev/cheena/feature'

        # Act
        Invoke-VersionBumpPr

        # Assert
        Should -Invoke Write-PipelineWarning -Times 1 -ParameterFilter {
            $Message -like '*not main or a release*'
        }
        Should -Invoke Invoke-GitHubApi -Times 0
    }

    It 'Opens a patch bump PR with the previous milestone from a later page' {
        # Act
        Invoke-VersionBumpPr

        # Assert
        Should -Invoke Invoke-GitHubApi -Times 1 -ParameterFilter {
            $Method -eq 'PUT' -and
                $Body.branch -eq 'dev/automation/bump-sqlclient-version/main/8.1.1'
        }
        Should -Invoke Invoke-GitHubApi -Times 1 -ParameterFilter {
            $Method -eq 'POST' -and $Path -like '*/pulls' -and $Body.base -eq 'main'
        }
        Should -Invoke Invoke-GitHubApi -Times 1 -ParameterFilter {
            $Method -eq 'PATCH' -and $Path -like '*issues/42' -and $Body.milestone -eq 500
        }
    }

    It 'Writes nothing when the bump PR is already open' {
        # Arrange
        Mock Invoke-GitHubApi { @([pscustomobject]@{ html_url = 'existing' }) } `
            -ParameterFilter { $Path -like '*pulls?state=open*' }

        # Act
        Invoke-VersionBumpPr

        # Assert
        Should -Invoke Invoke-GitHubApi -Times 0 -ParameterFilter { [bool]$Method }
    }

    It 'Writes nothing in a dry run' {
        # Arrange
        $DryRun = $true

        # Act
        Invoke-VersionBumpPr

        # Assert
        Should -Invoke Invoke-GitHubApi -Times 0 -ParameterFilter { [bool]$Method }
    }

    It 'Skips with a warning when GitHub has no Versions.props' {
        # Arrange
        Mock Invoke-GitHubApi {
            $response = [pscustomobject]@{ StatusCode = [pscustomobject]@{ value__ = 404 } }
            $exception = [Exception]::new('Not Found')
            $exception | Add-Member -NotePropertyName Response -NotePropertyValue $response
            throw $exception
        } -ParameterFilter { $Path -like '*contents*' -and -not $Method }

        # Act
        Invoke-VersionBumpPr

        # Assert
        Should -Invoke Write-PipelineWarning -Times 1 -ParameterFilter {
            $Message -like '*Skipping*'
        }
        Should -Invoke Invoke-GitHubApi -Times 0 -ParameterFilter { [bool]$Method }
    }

    It 'Skips with a warning when the built commit has no Versions.props' {
        # Arrange
        $VersionsPropsPath = Join-Path $TestDrive 'missing.props'

        # Act
        Invoke-VersionBumpPr

        # Assert
        Should -Invoke Write-PipelineWarning -Times 1 -ParameterFilter {
            $Message -like '*Skipping*'
        }
        Should -Invoke Invoke-GitHubApi -Times 0
    }
}

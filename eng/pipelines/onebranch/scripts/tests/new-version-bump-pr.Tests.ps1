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
    Verifies that built ADO branches map to the GitHub branches the PR targets.
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
        @{ Source = 'refs/heads/internal/release/7.1-hotfix' }
        @{ Source = 'refs/pull/123/merge' }
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

    It 'Ignores milestones from other major.minor series on main' {
        # Act
        $result = Get-NextSqlClientVersion -TargetBranch 'main' -PublishedVersion '8.2.0-preview1' `
            -MilestoneTitles $script:milestones

        # Assert
        $result | Should -BeNullOrEmpty
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

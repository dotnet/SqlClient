<#
.SYNOPSIS
    Pester tests for Open-LocalizationPr.ps1.

.DESCRIPTION
    These tests focus on the de-duplication behaviour: a scheduled run must
    reuse the existing open localization pull request rather than opening a new
    one, and must do nothing at all when there is no localization delta.

    'git' and 'Invoke-RestMethod' are mocked, so no network or repository
    access is required.
#>

BeforeAll {
    $global:scriptPath = Join-Path $PSScriptRoot '..' 'Open-LocalizationPr.ps1'
    $global:resourcesPath = 'src/Microsoft.Data.SqlClient/src/Resources'

    function New-TempDirectoryPath {
        return Join-Path ([System.IO.Path]::GetTempPath()) "loc-test-$([guid]::NewGuid().ToString('n'))"
    }

    function New-SourceTree {
        $root = New-TempDirectoryPath
        $resources = Join-Path $root $global:resourcesPath
        New-Item -ItemType Directory -Path $resources -Force | Out-Null

        foreach ($culture in @('de', 'fr', 'ja')) {
            Set-Content -Path (Join-Path $resources "Strings.$culture.resx") -Value '<root />'
        }

        return $root
    }

    function Get-RestCallCount {
        param(
            [Parameter(Mandatory)][string]$Method,
            [Parameter(Mandatory)][string]$UriPattern
        )

        return @($global:restCalls | Where-Object { $_.Method -eq $Method -and $_.Uri -like $UriPattern }).Count
    }

    function Get-RestCallsTo {
        param(
            [Parameter(Mandatory)][string]$Method,
            [Parameter(Mandatory)][string]$UriPattern
        )

        return , @($global:restCalls | Where-Object { $_.Method -eq $Method -and $_.Uri -like $UriPattern })
    }
}

Describe 'Open-LocalizationPr.ps1' {

    BeforeAll {
        Mock -CommandName 'git' -MockWith {
            $joined = $args -join ' '
            $global:gitCalls += $joined
            $global:LASTEXITCODE = 0

            if ($joined -like 'clone*') {
                # Stand in for a real clone so the script finds the expected layout.
                New-Item -ItemType Directory -Force -Path (Join-Path $global:workDir $global:resourcesPath) | Out-Null
                return
            }

            if ($joined -like 'diff --cached --quiet*') {
                $global:LASTEXITCODE = $global:diffExitCode
                return
            }

            if ($joined -like 'rev-parse HEAD^{tree}*') { return $global:localTree }
            if ($joined -like 'rev-parse HEAD*') { return $global:baseSha }
            if ($joined -like 'rev-parse *origin/*^{tree}*') { return $global:remoteTree }
            if ($joined -like 'rev-parse *origin/*^') { return $global:remoteParent }
            if ($joined -like 'rev-parse *origin/*') { return $global:remoteSha }

            return $null
        }

        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            $global:restCalls += @{ Method = $Method; Uri = $Uri; Body = $Body }

            if ($Method -eq 'GET') {
                return $global:openPullRequests
            }

            return @{ number = 999; html_url = 'https://github.com/dotnet/SqlClient/pull/999' }
        }
    }

    BeforeEach {
        $global:gitCalls = @()
        $global:restCalls = @()
        $global:diffExitCode = 1          # by default, there is a localization delta
        $global:baseSha = 'base000000'
        $global:localTree = 'tree111111'
        $global:remoteSha = $null         # by default, the branch does not exist yet
        $global:remoteTree = $null
        $global:remoteParent = $null
        $global:openPullRequests = @()

        $global:sourceDir = New-SourceTree
        $global:workDir = New-TempDirectoryPath
    }

    AfterEach {
        foreach ($path in @($global:sourceDir, $global:workDir)) {
            if ($path -and (Test-Path -LiteralPath $path)) {
                Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Context 'Input validation' {

        It 'Requires an access token' {
            { & $global:scriptPath -GitHubRepository 'dotnet/SqlClient' -AccessToken '' -SourceDirectory $global:sourceDir } |
                Should -Throw '*access token is required*'
        }

        It 'Rejects a repository that is not in owner/repo form' {
            { & $global:scriptPath -GitHubRepository 'SqlClient' -AccessToken 'token' -SourceDirectory $global:sourceDir } |
                Should -Throw "*'owner/repo' form*"
        }

        It 'Fails when the localized resources are missing' {
            Remove-Item -Path (Join-Path $global:sourceDir $global:resourcesPath 'Strings.*.resx') -Force

            { & $global:scriptPath -GitHubRepository 'dotnet/SqlClient' -AccessToken 'token' -SourceDirectory $global:sourceDir } |
                Should -Throw '*Did the OneLocBuild step run?*'
        }
    }

    Context 'When there is no localization delta' {

        It 'Does not push a branch or touch the GitHub API' {
            $global:diffExitCode = 0

            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            $global:restCalls.Count | Should -Be 0
            @($global:gitCalls | Where-Object { $_ -like 'push*' }).Count | Should -Be 0
            @($global:gitCalls | Where-Object { $_ -like 'commit*' }).Count | Should -Be 0
        }
    }

    Context 'When a localization pull request is already open' {

        BeforeEach {
            $global:openPullRequests = @(
                @{ number = 4612; html_url = 'https://github.com/dotnet/SqlClient/pull/4612' }
            )
        }

        It 'Updates the existing pull request instead of opening another one' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            (Get-RestCallCount -Method 'POST' -UriPattern '*/pulls') | Should -Be 0
            (Get-RestCallCount -Method 'PATCH' -UriPattern '*/pulls/4612') | Should -Be 1
        }

        It 'Looks the pull request up by the stable head branch' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            $lookups = Get-RestCallsTo -Method 'GET' -UriPattern '*/pulls?*'
            $lookups.Count | Should -Be 1
            $lookups[0].Uri | Should -BeLike '*state=open*'
            $lookups[0].Uri | Should -BeLike '*dotnet%3Adev%2Fautomation%2Fonelocbuild*'
        }
    }

    Context 'When no localization pull request is open' {

        It 'Creates exactly one pull request' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            (Get-RestCallCount -Method 'POST' -UriPattern '*/repos/dotnet/SqlClient/pulls') | Should -Be 1
        }

        It 'Pushes a stable branch name rather than a timestamped one' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            $push = @($global:gitCalls | Where-Object { $_ -like 'push*' })
            $push.Count | Should -Be 1
            $push[0] | Should -BeLike '*dev/automation/onelocbuild:refs/heads/dev/automation/onelocbuild*'
            $push[0] | Should -Not -Match 'onelocbuild-\d'
        }

        It 'Normalizes a repository value that carries a .git suffix' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient.git' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            (Get-RestCallCount -Method 'POST' -UriPattern 'https://api.github.com/repos/dotnet/SqlClient/pulls') |
                Should -Be 1
        }
    }

    Context 'When the branch already carries the identical change' {

        BeforeEach {
            $global:remoteSha = 'remote0000'
            $global:remoteTree = $global:localTree
            $global:remoteParent = $global:baseSha
            $global:openPullRequests = @(
                @{ number = 4612; html_url = 'https://github.com/dotnet/SqlClient/pull/4612' }
            )
        }

        It 'Skips the force-push but still keeps the pull request current' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            @($global:gitCalls | Where-Object { $_ -like 'push*' }).Count | Should -Be 0
            (Get-RestCallCount -Method 'PATCH' -UriPattern '*/pulls/4612') | Should -Be 1
            (Get-RestCallCount -Method 'POST' -UriPattern '*/pulls') | Should -Be 0
        }
    }
}

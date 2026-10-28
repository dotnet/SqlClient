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
    $global:nativeGit = (Get-Command git -CommandType Application | Select-Object -First 1).Source

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
                $global:cloneGitConfigParameters = $env:GIT_CONFIG_PARAMETERS
                $global:cloneGitConfigCount = $env:GIT_CONFIG_COUNT
                $global:cloneEffectiveHeaders = @(& $global:nativeGit config --get-urlmatch http.extraheader https://github.com/dotnet/SqlClient.git)
                if ($global:cloneExitCode -ne 0) {
                    $global:LASTEXITCODE = $global:cloneExitCode
                    return
                }
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
        $global:cloneExitCode = 0
        $global:cloneGitConfigParameters = $null
        $global:cloneGitConfigCount = $null
        $global:savedGitEnvironment = @{}
        foreach ($name in @('GIT_CONFIG_PARAMETERS', 'GIT_CONFIG_COUNT', 'GIT_CONFIG_KEY_0', 'GIT_CONFIG_VALUE_0')) {
            $global:savedGitEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
            [Environment]::SetEnvironmentVariable($name, $null)
        }

        $global:sourceDir = New-SourceTree
        $global:workDir = New-TempDirectoryPath
    }

    AfterEach {
        foreach ($name in $global:savedGitEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $global:savedGitEnvironment[$name])
        }
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

    Context 'When pushing over an existing remote branch' {

        BeforeEach {
            $global:remoteSha = 'remote0000'
            $global:remoteTree = 'differenttree'
            $global:remoteParent = 'staleparent'
            $global:openPullRequests = @(
                @{ number = 4612; html_url = 'https://github.com/dotnet/SqlClient/pull/4612' }
            )
        }

        It 'Leases the push against the observed remote SHA' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            $push = @($global:gitCalls | Where-Object { $_ -like 'push*' })
            $push.Count | Should -Be 1
            $push[0] | Should -BeLike '*--force-with-lease=refs/heads/dev/automation/onelocbuild:remote0000*'
            $push[0] | Should -Not -Match '(^|\s)--force(\s|$)'
        }
    }

    Context 'When the remote branch does not exist yet' {

        It 'Pushes without forcing' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            $push = @($global:gitCalls | Where-Object { $_ -like 'push*' })
            $push.Count | Should -Be 1
            $push[0] | Should -Not -Match '--force'
        }
    }

    Context 'Credential handling' {

        It 'Keeps the access token out of the git remote URL' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'super-secret-token-value' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            $global:gitCalls | Should -Not -Match 'super-secret-token-value'
            @($global:gitCalls | Where-Object { $_ -like 'clone*' })[0] |
                Should -BeLike '*https://github.com/dotnet/SqlClient.git*'
        }

        It 'Supplies authentication through the portable Git configuration environment' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            $expected = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes('x-access-token:token'))
            $global:cloneGitConfigParameters |
                Should -Be "'http.https://github.com/.extraheader=' 'http.https://github.com/.extraheader=AUTHORIZATION: basic $expected'"
            $global:cloneGitConfigCount | Should -BeNullOrEmpty
            @($global:cloneEffectiveHeaders | Where-Object { $_ }).Count | Should -Be 1
            $global:cloneEffectiveHeaders[-1] | Should -Be "AUTHORIZATION: basic $expected"
        }

        It 'Clears its credential environment afterwards' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            $env:GIT_CONFIG_COUNT | Should -BeNullOrEmpty
            $env:GIT_CONFIG_VALUE_0 | Should -BeNullOrEmpty
            $env:GIT_CONFIG_PARAMETERS | Should -BeNullOrEmpty
        }

        It 'Preserves inherited Git configuration' {
            $original = "'test.inherited=value with spaces'"
            $env:GIT_CONFIG_PARAMETERS = $original
            $env:GIT_CONFIG_COUNT = '1'
            $env:GIT_CONFIG_KEY_0 = 'test.count'
            $env:GIT_CONFIG_VALUE_0 = 'inherited'

            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            $global:cloneGitConfigParameters.StartsWith("$original ") | Should -BeTrue
            $global:cloneGitConfigCount | Should -Be '1'
            $env:GIT_CONFIG_PARAMETERS | Should -Be $original
            $env:GIT_CONFIG_COUNT | Should -Be '1'
            $env:GIT_CONFIG_KEY_0 | Should -Be 'test.count'
            $env:GIT_CONFIG_VALUE_0 | Should -Be 'inherited'
        }

        It 'Restores inherited configuration when cloning fails' {
            $original = "'test.inherited=original'"
            $env:GIT_CONFIG_PARAMETERS = $original
            $global:cloneExitCode = 128

            { & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir } | Should -Throw '*failed with exit code 128*'

            $env:GIT_CONFIG_PARAMETERS | Should -Be $original
            $env:GIT_CONFIG_COUNT | Should -BeNullOrEmpty
        }

        It 'Resets inherited GitHub headers before authenticating' {
            $env:GIT_CONFIG_PARAMETERS = "'http.https://github.com/.extraheader=AUTHORIZATION: inherited'"
            $env:GIT_CONFIG_COUNT = '1'
            $env:GIT_CONFIG_KEY_0 = 'http.https://github.com/.extraheader'
            $env:GIT_CONFIG_VALUE_0 = 'AUTHORIZATION: another-inherited-header'

            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir

            # Git retains empty reset entries; the HTTP client discards preceding headers.
            $headers = $global:cloneEffectiveHeaders
            $headers[-2] | Should -BeNullOrEmpty
            $expected = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes('x-access-token:token'))
            $headers[-1] | Should -Be "AUTHORIZATION: basic $expected"
            $env:GIT_CONFIG_VALUE_0 | Should -Be 'AUTHORIZATION: another-inherited-header'
        }

        It 'Removes its credentials when cloning fails without inherited configuration' {
            $global:cloneExitCode = 128

            { & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir } | Should -Throw '*failed with exit code 128*'

            $env:GIT_CONFIG_PARAMETERS | Should -BeNullOrEmpty
        }
    }

    Context 'When running in dry-run mode' {

        It 'Neither pushes nor creates a pull request' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir `
                -DryRun

            @($global:gitCalls | Where-Object { $_ -like 'push*' }).Count | Should -Be 0
            (Get-RestCallCount -Method 'POST' -UriPattern '*/pulls') | Should -Be 0
            (Get-RestCallCount -Method 'PATCH' -UriPattern '*/pulls/*') | Should -Be 0
        }

        It 'Still performs the existing pull request lookup' {
            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir `
                -DryRun

            (Get-RestCallCount -Method 'GET' -UriPattern '*/pulls?*') | Should -Be 1
        }

        It 'Does not update an existing pull request' {
            $global:remoteSha = 'remote0000'
            $global:remoteTree = 'differenttree'
            $global:remoteParent = 'oldbase000'
            $global:openPullRequests = @(
                @{ number = 4612; html_url = 'https://github.com/dotnet/SqlClient/pull/4612' }
            )

            & $global:scriptPath `
                -GitHubRepository 'dotnet/SqlClient' `
                -AccessToken 'token' `
                -SourceDirectory $global:sourceDir `
                -WorkingDirectory $global:workDir `
                -DryRun

            @($global:gitCalls | Where-Object { $_ -like 'push*' }).Count | Should -Be 0
            (Get-RestCallCount -Method 'PATCH' -UriPattern '*/pulls/4612') | Should -Be 0
        }
    }
}

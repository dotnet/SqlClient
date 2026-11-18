#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################

<#
.SYNOPSIS
    Opens a GitHub PR that bumps SqlClientNextVersion after a SqlClient release is published.

.DESCRIPTION
    Runs from the version-bump-pr workflow when a GitHub release is published.

    The published version is the release tag without its leading v (v7.1.2 -> 7.1.2). Tags that
    are not SqlClient versions, such as AKV_6.1.1, are skipped. The PR targets release/X.Y when
    that branch exists, or else main. Outside GitHub Actions, only -DryRun is allowed.

    The next version is chosen as follows:
      - A GA release always bumps the patch (7.0.1 -> 7.0.2), on any branch.
      - A preview release on release/X.Y moves to its GA version (7.1.0-preview3 -> 7.1.0).
      - A preview release on main moves to the lowest open GitHub milestone in the same
        major.minor series that is greater than the published version
        (8.1.0-preview1 -> 8.1.0-preview2). With no such milestone, it moves to its GA version.

    The script also does nothing when the target branch already has a version at or above the next
    version, or when an open bump PR already exists. Otherwise it creates or resets the branch
    dev/automation/bump-sqlclient-version/<target>/<version>, commits the Versions.props change,
    and opens the PR. The PR gets the milestone matching the next version, or else the closest
    earlier milestone in any state.

.PARAMETER ReleaseTag
    The published release tag, such as v7.1.2. Defaults to RELEASE_TAG.

.PARAMETER GitHubRepository
    The GitHub repository in owner/name form. Defaults to GITHUB_REPOSITORY, or dotnet/SqlClient.

.PARAMETER Token
    A GitHub token with contents, pull request, and issue write access. Defaults to GITHUB_TOKEN.

.PARAMETER DryRun
    Logs the planned change without writing to GitHub.
#>

[CmdletBinding()]
param(
    [string]$ReleaseTag = $env:RELEASE_TAG,
    [string]$GitHubRepository = $env:GITHUB_REPOSITORY,
    [string]$Token = $env:GITHUB_TOKEN,
    [switch]$DryRun
)

if ([string]::IsNullOrWhiteSpace($GitHubRepository)) {
    $GitHubRepository = 'dotnet/SqlClient'
}

$script:VersionsPropsRepoPath = 'src/Microsoft.Data.SqlClient/Versions.props'
$script:VersionPattern = '<SqlClientNextVersion>([^<]*)</SqlClientNextVersion>'

function Write-ActionsWarning {
    param([string]$Message)

    Write-Host "::warning::$Message"
}

# Returns the SqlClient version of a release tag, or $null when the tag is not one.
function Get-ReleaseVersion {
    param([string]$Tag)

    if ($Tag -notmatch '^v(\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)$') {
        return $null
    }

    return $Matches[1]
}

# Parses a SemVer 2.0 version without build metadata, or returns $null when it is not one.
function ConvertTo-SqlClientVersion {
    param([string]$Text)

    if ($Text -notmatch '^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?$') {
        return $null
    }

    [pscustomobject]@{
        Text       = $Text
        Major      = [int]$Matches[1]
        Minor      = [int]$Matches[2]
        Patch      = [int]$Matches[3]
        Prerelease = $Matches[4]
    }
}

# Compares two prerelease identifiers. Labels such as preview2 and preview10 compare by their
# numeric suffix, so preview10 is greater than preview2.
function Compare-PrereleaseLabel {
    param([string]$Left, [string]$Right)

    $leftMatch = [regex]::Match($Left, '^([A-Za-z]*)(\d+)$')
    $rightMatch = [regex]::Match($Right, '^([A-Za-z]*)(\d+)$')

    if ($leftMatch.Success -and $rightMatch.Success) {
        $prefix = [string]::CompareOrdinal($leftMatch.Groups[1].Value, $rightMatch.Groups[1].Value)
        if ($prefix -ne 0) {
            return [Math]::Sign($prefix)
        }

        return ([long]$leftMatch.Groups[2].Value).CompareTo([long]$rightMatch.Groups[2].Value)
    }

    return [Math]::Sign([string]::CompareOrdinal($Left, $Right))
}

# Returns -1, 0, or 1. A GA version is greater than any prerelease of the same number.
function Compare-SqlClientVersion {
    param($Left, $Right)

    foreach ($part in 'Major', 'Minor', 'Patch') {
        $result = $Left.$part.CompareTo($Right.$part)
        if ($result -ne 0) {
            return $result
        }
    }

    $leftIsGa = [string]::IsNullOrEmpty($Left.Prerelease)
    $rightIsGa = [string]::IsNullOrEmpty($Right.Prerelease)

    if ($leftIsGa -and $rightIsGa) {
        return 0
    }
    if ($leftIsGa) {
        return 1
    }
    if ($rightIsGa) {
        return -1
    }

    return Compare-PrereleaseLabel -Left $Left.Prerelease -Right $Right.Prerelease
}

# Chooses the next version for the target branch.
function Get-NextSqlClientVersion {
    param(
        [string]$TargetBranch,
        [string]$PublishedVersion,
        [string[]]$MilestoneTitles = @()
    )

    $published = ConvertTo-SqlClientVersion $PublishedVersion
    if ($null -eq $published) {
        throw "Published version '$PublishedVersion' is not a valid SqlClient version."
    }

    if (-not $published.Prerelease) {
        return "$($published.Major).$($published.Minor).$($published.Patch + 1)"
    }

    if ($TargetBranch -like 'release/*') {
        return "$($published.Major).$($published.Minor).$($published.Patch)"
    }

    $candidates = foreach ($title in $MilestoneTitles) {
        $milestone = ConvertTo-SqlClientVersion $title
        if ($null -ne $milestone -and
            $milestone.Major -eq $published.Major -and
            $milestone.Minor -eq $published.Minor -and
            (Compare-SqlClientVersion $milestone $published) -gt 0) {
            $milestone
        }
    }

    $next = $null
    foreach ($candidate in $candidates) {
        if ($null -eq $next -or (Compare-SqlClientVersion $candidate $next) -lt 0) {
            $next = $candidate
        }
    }

    if ($null -eq $next) {
        return "$($published.Major).$($published.Minor).$($published.Patch)"
    }

    return $next.Text
}

# Picks the PR milestone title: the one matching the next version, or else the greatest one below
# it. Returns $null when neither exists.
function Get-PrMilestoneTitle {
    param(
        [string]$NextVersion,
        [string[]]$MilestoneTitles = @()
    )

    if ($MilestoneTitles -contains $NextVersion) {
        return $NextVersion
    }

    $next = ConvertTo-SqlClientVersion $NextVersion
    $previous = $null
    foreach ($title in $MilestoneTitles) {
        $milestone = ConvertTo-SqlClientVersion $title
        if ($null -ne $milestone -and
            (Compare-SqlClientVersion $milestone $next) -lt 0 -and
            ($null -eq $previous -or (Compare-SqlClientVersion $milestone $previous) -gt 0)) {
            $previous = $milestone
        }
    }

    if ($null -eq $previous) {
        return $null
    }

    return $previous.Text
}

# Reads SqlClientNextVersion from Versions.props content, requiring exactly one definition.
function Get-VersionFromProps {
    param([string]$Content)

    $found = [regex]::Matches($Content, $script:VersionPattern)
    if ($found.Count -ne 1) {
        return $null
    }

    return $found[0].Groups[1].Value.Trim()
}

function Set-VersionInProps {
    param([string]$Content, [string]$Version)

    return [regex]::Replace(
        $Content,
        $script:VersionPattern,
        "<SqlClientNextVersion>$Version</SqlClientNextVersion>")
}

function Invoke-GitHubApi {
    param(
        [string]$Method = 'GET',
        [string]$Path,
        $Body
    )

    $request = @{
        Method  = $Method
        Uri     = "https://api.github.com/$Path"
        Headers = @{
            Authorization          = "Bearer $Token"
            Accept                 = 'application/vnd.github+json'
            'X-GitHub-Api-Version' = '2022-11-28'
        }
    }

    if ($null -ne $Body) {
        $request.Body = $Body | ConvertTo-Json -Depth 5
        $request.ContentType = 'application/json'
    }

    Invoke-RestMethod @request
}

# Reads every milestone in any state, following pages until a short page is returned.
function Get-AllMilestones {
    param([string]$RepoApi)

    $page = 1
    do {
        # Invoke-RestMethod can return an empty JSON array as one item, so keep only real ones.
        $batch = @(Invoke-GitHubApi -Path "$RepoApi/milestones?state=all&per_page=100&page=$page" |
            ForEach-Object { $_ } | Where-Object { $_.title })
        $batch
        $page++
    } while ($batch.Count -eq 100)
}

# Returns true when the branch exists in the repository.
function Test-GitHubBranch {
    param([string]$RepoApi, [string]$Branch)

    try {
        Invoke-GitHubApi -Path "$RepoApi/git/ref/heads/$Branch" | Out-Null
        return $true
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 404) {
            throw
        }

        return $false
    }
}

function Invoke-VersionBumpPr {
    $publishedVersion = Get-ReleaseVersion $ReleaseTag
    if ($null -eq $publishedVersion) {
        Write-ActionsWarning "'$ReleaseTag' is not a SqlClient release tag. Skipping."
        return
    }

    if (-not $DryRun -and $env:GITHUB_ACTIONS -ne 'true') {
        throw 'Only -DryRun is allowed outside GitHub Actions.'
    }

    if (-not $DryRun -and [string]::IsNullOrWhiteSpace($Token)) {
        throw 'A GitHub token is required. Set GITHUB_TOKEN or pass -Token.'
    }

    $owner = $GitHubRepository.Split('/')[0]
    $repoApi = "repos/$GitHubRepository"
    $propsApi = "$repoApi/contents/$($script:VersionsPropsRepoPath)"

    $published = ConvertTo-SqlClientVersion $publishedVersion
    $releaseBranch = "release/$($published.Major).$($published.Minor)"
    $targetBranch = 'main'
    if (Test-GitHubBranch $repoApi $releaseBranch) {
        $targetBranch = $releaseBranch
    }

    $milestones = @(Get-AllMilestones $repoApi)
    $nextVersion = Get-NextSqlClientVersion `
        -TargetBranch $targetBranch `
        -PublishedVersion $publishedVersion `
        -MilestoneTitles @($milestones | Where-Object { $_.state -eq 'open' } |
            ForEach-Object { $_.title })

    Write-Host "Published $publishedVersion ($ReleaseTag)."
    Write-Host "Next version for $targetBranch is $nextVersion."

    $encodedTarget = [uri]::EscapeDataString($targetBranch)
    try {
        $file = Invoke-GitHubApi -Path "${propsApi}?ref=$encodedTarget"
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 404) {
            throw
        }

        Write-ActionsWarning ("$targetBranch has no $($script:VersionsPropsRepoPath). " +
            "Skipping.")
        return
    }

    $content = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($file.content))
    $currentText = Get-VersionFromProps $content
    $current = ConvertTo-SqlClientVersion $currentText

    if ($null -eq $current) {
        Write-ActionsWarning ("$targetBranch has no single valid SqlClientNextVersion. " +
            "Skipping.")
        return
    }

    if ((Compare-SqlClientVersion $current (ConvertTo-SqlClientVersion $nextVersion)) -ge 0) {
        Write-Host "$targetBranch is already at $currentText. Nothing to do."
        return
    }

    $headBranch = "dev/automation/bump-sqlclient-version/$targetBranch/$nextVersion"
    $encodedHead = [uri]::EscapeDataString("${owner}:$headBranch")
    # Invoke-RestMethod can return an empty JSON array as one item, so keep only real PRs.
    $existing = @(Invoke-GitHubApi -Path "$repoApi/pulls?state=open&head=$encodedHead" |
        ForEach-Object { $_ } | Where-Object { $_.html_url })
    if ($existing.Count -gt 0) {
        Write-Host "PR $($existing[0].html_url) already exists. Nothing to do."
        return
    }

    $title = "Bump SqlClient version to $nextVersion"
    $milestoneTitle = Get-PrMilestoneTitle -NextVersion $nextVersion `
        -MilestoneTitles @($milestones | ForEach-Object { $_.title })
    if ($DryRun) {
        Write-Host "Dry run: would open '$title' from $headBranch into $targetBranch."
        Write-Host "Dry run: would assign milestone '$milestoneTitle'."
        return
    }

    # Create the head branch at the target branch tip, or reset a stale one left by an earlier run.
    $baseSha = (Invoke-GitHubApi -Path "$repoApi/git/ref/heads/$targetBranch").object.sha
    try {
        Invoke-GitHubApi -Method POST -Path "$repoApi/git/refs" -Body @{
            ref = "refs/heads/$headBranch"
            sha = $baseSha
        } | Out-Null
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 422) {
            throw
        }

        Invoke-GitHubApi -Method PATCH -Path "$repoApi/git/refs/heads/$headBranch" -Body @{
            sha   = $baseSha
            force = $true
        } | Out-Null
    }

    $updated = Set-VersionInProps -Content $content -Version $nextVersion
    Invoke-GitHubApi -Method PUT -Path $propsApi -Body @{
        message = $title
        content = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($updated))
        sha     = $file.sha
        branch  = $headBranch
    } | Out-Null

    $body = "Sets ``SqlClientNextVersion`` to ``$nextVersion`` after ``$publishedVersion`` " +
        "was released ($ReleaseTag).`n`nOpened by the version-bump-pr workflow."
    $pr = Invoke-GitHubApi -Method POST -Path "$repoApi/pulls" -Body @{
        title = $title
        head  = $headBranch
        base  = $targetBranch
        body  = $body
    }
    Write-Host "Opened $($pr.html_url)."

    if ($null -eq $milestoneTitle) {
        Write-ActionsWarning "No milestone at or below $nextVersion for $($pr.html_url)."
        return
    }

    $milestone = $milestones | Where-Object { $_.title -eq $milestoneTitle } |
        Select-Object -First 1
    Invoke-GitHubApi -Method PATCH -Path "$repoApi/issues/$($pr.number)" -Body @{
        milestone = $milestone.number
    } | Out-Null
    Write-Host "Assigned milestone $milestoneTitle."
}

# Dot-sourcing loads the functions for tests without running the script.
if ($MyInvocation.InvocationName -ne '.') {
    Invoke-VersionBumpPr
}

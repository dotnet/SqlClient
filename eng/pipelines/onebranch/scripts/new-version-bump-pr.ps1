#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################

<#
.SYNOPSIS
    Opens a GitHub PR that bumps SqlClientNextVersion after a SqlClient release is published.

.DESCRIPTION
    Runs after the official pipeline publishes the SqlClient family to NuGet Production.

    The PR always targets the branch that was built (BUILD_SOURCEBRANCH), with an optional
    internal/ prefix removed: internal/release/7.1 and release/7.1 both target release/7.1. Only
    main and release/X.Y are allowed; other branches are skipped. Outside an Azure DevOps build,
    only -DryRun is allowed. The published version is read from that build's Versions.props.

    The next version is chosen as follows:
      - A GA release always bumps the patch (7.0.1 -> 7.0.2), on any branch.
      - A preview release on release/X.Y moves to its GA version (7.1.0-preview3 -> 7.1.0).
      - A preview release on main moves to the lowest open GitHub milestone in the same
        major.minor series that is greater than the published version
        (8.1.0-preview1 -> 8.1.0-preview2). With no such milestone, it moves to its GA version.

    The script also does nothing when the GitHub branch already has a version at or above the next
    version, or when an open bump PR already exists. Otherwise it creates or resets the branch
    dev/automation/bump-sqlclient-version/<target>/<version>, commits the Versions.props change,
    and opens the PR. The PR gets the milestone matching the next version, or else the closest
    earlier milestone in any state.

.PARAMETER VersionsPropsPath
    Path to the built commit's src/Microsoft.Data.SqlClient/Versions.props, from which the
    published version is read.

.PARAMETER GitHubRepository
    The GitHub repository in owner/name form.

.PARAMETER Token
    A GitHub token with contents, pull request, and issue write access. Defaults to GITHUB_TOKEN.

.PARAMETER DryRun
    Logs the planned change without writing to GitHub.
#>

[CmdletBinding()]
param(
    [string]$VersionsPropsPath,
    [string]$GitHubRepository = 'dotnet/SqlClient',
    [string]$Token = $env:GITHUB_TOKEN,
    [switch]$DryRun
)

$script:VersionsPropsRepoPath = 'src/Microsoft.Data.SqlClient/Versions.props'
$script:VersionPattern = '<SqlClientNextVersion>([^<]*)</SqlClientNextVersion>'

function Write-PipelineWarning {
    param([string]$Message)

    Write-Host "##vso[task.logissue type=warning]$Message"
}

# Maps the built ref to the GitHub branch the PR targets, or $null when it is not main or a
# release/X.Y branch.
function Get-GitHubTargetBranch {
    param([string]$SourceBranch)

    if ($SourceBranch -notmatch '^refs/heads/(?:internal/)?(main|release/\d+\.\d+)$') {
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

function Invoke-VersionBumpPr {
    $sourceBranch = $env:BUILD_SOURCEBRANCH
    $targetBranch = Get-GitHubTargetBranch $sourceBranch
    if ($null -eq $targetBranch) {
        Write-PipelineWarning "'$sourceBranch' is not main or a release/X.Y branch. Skipping."
        return
    }

    # Older release branches keep the version elsewhere, so they are skipped rather than failed.
    if ([string]::IsNullOrWhiteSpace($VersionsPropsPath) -or -not (Test-Path $VersionsPropsPath)) {
        Write-PipelineWarning "Versions.props was not found at '$VersionsPropsPath'. Skipping."
        return
    }

    $publishedVersion = Get-VersionFromProps (Get-Content -LiteralPath $VersionsPropsPath -Raw)
    if ($null -eq $publishedVersion) {
        Write-PipelineWarning ("No single SqlClientNextVersion in '$VersionsPropsPath'. " +
            "Skipping.")
        return
    }

    if (-not $DryRun -and $env:TF_BUILD -ne 'True') {
        throw 'Only -DryRun is allowed outside an Azure DevOps build.'
    }

    if (-not $DryRun -and [string]::IsNullOrWhiteSpace($Token)) {
        throw 'A GitHub token is required. Set GITHUB_TOKEN or pass -Token.'
    }

    $owner = $GitHubRepository.Split('/')[0]
    $repoApi = "repos/$GitHubRepository"
    $propsApi = "$repoApi/contents/$($script:VersionsPropsRepoPath)"
    $milestones = @(Get-AllMilestones $repoApi)
    $nextVersion = Get-NextSqlClientVersion `
        -TargetBranch $targetBranch `
        -PublishedVersion $publishedVersion `
        -MilestoneTitles @($milestones | Where-Object { $_.state -eq 'open' } |
            ForEach-Object { $_.title })

    Write-Host "Published $publishedVersion from $sourceBranch."
    Write-Host "Next version for GitHub $targetBranch is $nextVersion."

    $encodedTarget = [uri]::EscapeDataString($targetBranch)
    try {
        $file = Invoke-GitHubApi -Path "${propsApi}?ref=$encodedTarget"
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 404) {
            throw
        }

        Write-PipelineWarning ("GitHub $targetBranch has no $($script:VersionsPropsRepoPath). " +
            "Skipping.")
        return
    }

    $content = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($file.content))
    $currentText = Get-VersionFromProps $content
    $current = ConvertTo-SqlClientVersion $currentText

    if ($null -eq $current) {
        Write-PipelineWarning ("GitHub $targetBranch has no single valid SqlClientNextVersion. " +
            "Skipping.")
        return
    }

    if ((Compare-SqlClientVersion $current (ConvertTo-SqlClientVersion $nextVersion)) -ge 0) {
        Write-Host "GitHub $targetBranch is already at $currentText. Nothing to do."
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
        "was published to NuGet.`n`nOpened by the SqlClient official pipeline " +
        "(build $env:BUILD_BUILDNUMBER)."
    $pr = Invoke-GitHubApi -Method POST -Path "$repoApi/pulls" -Body @{
        title = $title
        head  = $headBranch
        base  = $targetBranch
        body  = $body
    }
    Write-Host "Opened $($pr.html_url)."

    if ($null -eq $milestoneTitle) {
        Write-PipelineWarning "No milestone at or below $nextVersion for $($pr.html_url)."
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

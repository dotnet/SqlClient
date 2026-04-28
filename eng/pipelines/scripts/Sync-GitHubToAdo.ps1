<#
.SYNOPSIS
    Syncs a GitHub branch into an Azure DevOps repository via a pull request.

.DESCRIPTION
    This script fetches the latest commits from a public GitHub repository,
    force-pushes them to a sync branch in the ADO repo, then creates or updates
    a pull request targeting the specified ADO branch.

    If there are no new commits (the branches are already in sync), the script
    exits cleanly with no changes.

.PARAMETER GitHubRepoUrl
    The HTTPS clone URL of the public GitHub repository.

.PARAMETER GitHubBranch
    The branch to sync from GitHub (e.g. "main").

.PARAMETER TargetBranch
    The ADO branch the PR should target (e.g. "internal/main").

.PARAMETER SyncBranchName
    The ADO branch name to push GitHub commits to (e.g. "dev/autosync/github-main").

.PARAMETER AdoOrgUrl
    The Azure DevOps organization URL (e.g. "https://dev.azure.com/org/").

.PARAMETER AdoProject
    The Azure DevOps project name.

.PARAMETER AdoRepoName
    The Azure DevOps repository name.

.PARAMETER AccessToken
    The access token for ADO REST API and git push operations.
    Defaults to the SYSTEM_ACCESSTOKEN environment variable.

.NOTES
    This pipeline is intended to be run only in the internal ADO.Net project.
    It must never be run in the Public project or triggered by changes in GitHub.
#>

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$GitHubRepoUrl,

    [Parameter(Mandatory)]
    [string]$GitHubBranch,

    [Parameter(Mandatory)]
    [string]$TargetBranch,

    [Parameter(Mandatory)]
    [string]$SyncBranchName,

    [Parameter(Mandatory)]
    [string]$AdoOrgUrl,

    [Parameter(Mandatory)]
    [string]$AdoProject,

    [Parameter(Mandatory)]
    [string]$AdoRepoName,

    [string]$AccessToken = $env:SYSTEM_ACCESSTOKEN
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

#region Validation
if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    throw "Access token is required. Set SYSTEM_ACCESSTOKEN or pass -AccessToken."
}
#endregion

#region Helper Functions

function Invoke-AdoApi {
    <#
    .SYNOPSIS
        Calls an Azure DevOps REST API endpoint.
    #>
    param(
        [Parameter(Mandatory)][string]$Uri,
        [string]$Method = 'GET',
        [object]$Body = $null
    )

    $headers = @{
        'Authorization' = "Bearer $AccessToken"
        'Content-Type'  = 'application/json'
    }

    $params = @{
        Uri     = $Uri
        Method  = $Method
        Headers = $headers
    }

    if ($null -ne $Body) {
        $params['Body'] = ($Body | ConvertTo-Json -Depth 10)
    }

    $response = Invoke-RestMethod @params -ErrorAction Stop
    return $response
}

function Get-CommitSummary {
    <#
    .SYNOPSIS
        Returns a markdown-formatted list of commits between two refs.
    #>
    param(
        [Parameter(Mandatory)][string]$BaseRef,
        [Parameter(Mandatory)][string]$HeadRef,
        [int]$MaxCommits = 50
    )

    $logOutput = git log --oneline "$BaseRef..$HeadRef" -n $MaxCommits 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not retrieve commit log: $logOutput"
        return "_(commit log unavailable)_"
    }

    $lines = @($logOutput -split "`n" | Where-Object { $_ -match '\S' })
    if ($lines.Count -eq 0) {
        return "_(no commits)_"
    }

    $summary = ($lines | ForEach-Object { "- ``$_``" }) -join "`n"

    $totalCount = (git rev-list --count "$BaseRef..$HeadRef" 2>&1)
    if ($LASTEXITCODE -eq 0 -and [int]$totalCount -gt $MaxCommits) {
        $summary += "`n`n_...and $($totalCount - $MaxCommits) more commit(s)._"
    }

    return $summary
}

#endregion

#region Git Operations

Write-Host "=== GitHub to ADO Sync ==="
Write-Host "GitHub : $GitHubRepoUrl @ $GitHubBranch"
Write-Host "ADO    : $AdoProject/$AdoRepoName @ $TargetBranch"
Write-Host "Sync   : $SyncBranchName"
Write-Host ""

# Configure git identity for any merge commits (shouldn't be needed for
# force-push, but set it defensively).
git config user.email "ado-sync-bot@microsoft.com"
git config user.name "ADO Sync Bot"

# Add GitHub as a remote and fetch the branch we want to sync.
Write-Host "Fetching GitHub branch '$GitHubBranch'..."
$remoteExists = git remote | Where-Object { $_ -eq 'github' }
if ($remoteExists) {
    git remote set-url github $GitHubRepoUrl
} else {
    git remote add github $GitHubRepoUrl
}

git fetch github $GitHubBranch --verbose
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to fetch '$GitHubBranch' from GitHub."
    exit 1
}

# Resolve the SHA of the fetched GitHub branch.
$githubSha = git rev-parse "github/$GitHubBranch"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to resolve SHA for 'github/$GitHubBranch'."
    exit 1
}
Write-Host "GitHub HEAD : $githubSha"

# Check if the target branch exists and compare SHAs.
$targetRef = git rev-parse "origin/$TargetBranch" 2>&1
$targetExists = ($LASTEXITCODE -eq 0)

if ($targetExists) {
    Write-Host "Target HEAD : $targetRef"
}

# Check if the sync branch already exists in origin.
$syncRef = git rev-parse "origin/$SyncBranchName" 2>&1
$syncBranchExists = ($LASTEXITCODE -eq 0)

if ($syncBranchExists -and $syncRef -eq $githubSha) {
    Write-Host ""
    Write-Host "Sync branch is already at GitHub HEAD. Nothing to do."
    exit 0
}

# Create the sync branch pointing to the GitHub HEAD and force-push it.
Write-Host ""
Write-Host "Updating sync branch '$SyncBranchName' to GitHub HEAD..."
git checkout -B $SyncBranchName "github/$GitHubBranch" --quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create sync branch."
    exit 1
}

git push origin $SyncBranchName --force --quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to push sync branch to ADO."
    exit 1
}
Write-Host "Sync branch pushed successfully."

#endregion

#region Pull Request Management

# Build the commit summary for the PR description / comment.
$commitSummary = if ($targetExists) {
    Get-CommitSummary -BaseRef "origin/$TargetBranch" -HeadRef "github/$GitHubBranch"
} else {
    "_(target branch does not exist yet — initial sync)_"
}

# Normalize the ADO org URL (remove trailing slash for consistent URI construction).
$AdoOrgUrl = $AdoOrgUrl.TrimEnd('/')

# URL-encode the project name for REST API calls.
$encodedProject = [Uri]::EscapeDataString($AdoProject)
$encodedRepo = [Uri]::EscapeDataString($AdoRepoName)
$apiBase = "$AdoOrgUrl/$encodedProject/_apis/git/repositories/$encodedRepo"

# Search for an existing active PR from the sync branch to the target branch.
Write-Host ""
Write-Host "Checking for existing pull requests..."

$encodedSyncBranch = [Uri]::EscapeDataString($SyncBranchName)
$encodedTargetBranch = [Uri]::EscapeDataString($TargetBranch)
$searchUri = "$apiBase/pullrequests?searchCriteria.sourceRefName=refs/heads/$encodedSyncBranch" +
             "&searchCriteria.targetRefName=refs/heads/$encodedTargetBranch" +
             "&searchCriteria.status=active" +
             "&api-version=7.1"

$existingPrs = Invoke-AdoApi -Uri $searchUri
$activePr = $existingPrs.value | Select-Object -First 1

if ($activePr) {
    # An active PR already exists — update its description and add a comment.
    $prId = $activePr.pullRequestId
    Write-Host "Active PR #$prId found. Updating description and posting comment..."

    # 1. PATCH the PR description with the refreshed commit summary.
    $patchUri = "$apiBase/pullrequests/$prId?api-version=7.1"
    $patchBody = @{
        description = "## Automated GitHub Sync`n`nThis PR was updated automatically by the GitHub sync pipeline.`n`nSource: [dotnet/SqlClient@$GitHubBranch](https://github.com/dotnet/SqlClient/tree/$GitHubBranch)`nSync branch: ``$SyncBranchName```n`n### Latest Commits`n`n$commitSummary`n`n---`n_This PR requires manual review and merge. Auto-complete is not enabled._"
    }
    Invoke-AdoApi -Uri $patchUri -Method 'PATCH' -Body $patchBody | Out-Null
    Write-Host "PR #$prId description updated."

    # 2. Post a comment thread summarising the new commits.
    $commentUri = "$apiBase/pullrequests/$prId/threads?api-version=7.1"
    $commentBody = @{
        comments = @(
            @{
                parentCommentId = 0
                content         = "## Sync Update`n`nPR updated by the GitHub sync pipeline on $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm')) UTC.`n`nNew commits from GitHub ``$GitHubBranch``:`n`n$commitSummary"
                commentType     = 1  # Text
            }
        )
        status = 1  # Active
    }
    Invoke-AdoApi -Uri $commentUri -Method 'POST' -Body $commentBody | Out-Null
    Write-Host "Update comment posted to PR #$prId."
} else {
    # No active PR — create a new one.
    Write-Host "No active PR found. Creating a new pull request..."

    $prUri = "$apiBase/pullrequests?api-version=7.1"
    $prBody = @{
        sourceRefName = "refs/heads/$SyncBranchName"
        targetRefName = "refs/heads/$TargetBranch"
        title         = "[GitHub Sync] Update $TargetBranch from GitHub $GitHubBranch"
        description   = "## Automated GitHub Sync`n`nThis PR was created automatically by the GitHub sync pipeline.`n`nSource: [dotnet/SqlClient@$GitHubBranch](https://github.com/dotnet/SqlClient/tree/$GitHubBranch)`nSync branch: ``$SyncBranchName```n`n### Commits`n`n$commitSummary`n`n---`n_This PR requires manual review and merge. Auto-complete is not enabled._"
    }

    $newPr = Invoke-AdoApi -Uri $prUri -Method 'POST' -Body $prBody
    Write-Host "Created PR #$($newPr.pullRequestId): $($newPr.title)"
}

#endregion

Write-Host ""
Write-Host "=== Sync complete ==="

<#
.SYNOPSIS
    Publishes OneLocBuild-generated localized resource files to GitHub as a
    single, continuously-updated pull request.

.DESCRIPTION
    The Localization-CI pipeline regenerates the localized `Strings.*.resx`
    files on every scheduled run. Because the generated content is identical
    until a previous localization PR is merged, naively opening a new PR per
    run produces a pile of byte-for-byte duplicate PRs.

    This script makes the publish step idempotent:

      1. Clones the target GitHub repository at the base branch.
      2. Copies the freshly generated localized resource files into the clone.
      3. If the content matches the base branch, exits without doing anything.
      4. Otherwise commits onto a *stable* branch name, force-pushes it, and
         reuses the existing open pull request if one is already present.

    The net effect is at most one open localization PR at any time. Subsequent
    runs refresh that PR in place instead of opening another one.

.PARAMETER GitHubRepository
    The target repository in "owner/repo" form. A trailing ".git" is tolerated
    so the existing $(GitHubRepository) pipeline variable can be passed as-is.

.PARAMETER AccessToken
    A GitHub token with "contents: write" and "pull requests: write" on the
    target repository. Defaults to the GITHUB_TOKEN environment variable.

.PARAMETER SourceDirectory
    The directory holding the OneLocBuild output. Localized files are resolved
    relative to this path using ResourcesPath. Defaults to the current
    directory.

.PARAMETER WorkingDirectory
    The directory the target repository is cloned into. Defaults to a new
    "loc-pr-<guid>" folder under the system temp path, which is removed when
    the script finishes.

.PARAMETER BaseBranch
    The branch the pull request targets. Defaults to "main".

.PARAMETER BranchName
    The stable branch the localized files are published to. Reusing one branch
    name across runs is what allows the pull request to be reused. Defaults to
    "dev/automation/onelocbuild".

.PARAMETER ResourcesPath
    Repository-relative path to the resources folder. Defaults to the
    Microsoft.Data.SqlClient resources folder.

.PARAMETER ResourceFilePattern
    Filename pattern for the localized resource files to publish. Defaults to
    "Strings.*.resx", which matches the localized files but not the English
    "Strings.resx" source.

.NOTES
    Intended to be invoked from the internal Localization-CI pipeline. It is
    safe to re-run: with no localization changes pending it is a no-op, and
    with changes pending it converges on a single open pull request.
#>

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$GitHubRepository,

    [string]$AccessToken = $env:GITHUB_TOKEN,

    [string]$SourceDirectory = (Get-Location).Path,

    [string]$WorkingDirectory,

    [string]$BaseBranch = 'main',

    [string]$BranchName = 'dev/automation/onelocbuild',

    [string]$ResourcesPath = 'src/Microsoft.Data.SqlClient/src/Resources',

    [string]$ResourceFilePattern = 'Strings.*.resx'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Several git invocations below use the exit code as a signal (for example
# "git diff --cached --quiet" returns 1 when there are staged changes). On
# PowerShell 7.3+ that would otherwise be turned into a terminating error.
$PSNativeCommandUseErrorActionPreference = $false

$PrTitle = '[Scheduled Run] Localized resource files from OneLocBuild'

#region Helper Functions

function Get-RepositorySlug {
    <#
    .SYNOPSIS
        Normalizes a repository reference into "owner/repo" form.
    #>
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Repository
    )

    $slug = $Repository.Trim().TrimEnd('/')
    if ($slug.EndsWith('.git', [StringComparison]::OrdinalIgnoreCase)) {
        $slug = $slug.Substring(0, $slug.Length - 4)
    }

    if ($slug -notmatch '^[^/\s]+/[^/\s]+$') {
        throw "GitHubRepository must be in 'owner/repo' form, but was '$Repository'."
    }

    return $slug
}

function Get-PullRequestBody {
    <#
    .SYNOPSIS
        Builds the pull request description.
    #>
    param(
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][datetime]$UpdatedUtc
    )

    $timestamp = $UpdatedUtc.ToString('yyyy-MM-dd HH:mm')

    return @"
Automated PR created from the OneLocBuild scheduled pipeline run.

Contains updated localized ``Strings.*.resx`` resource files.

This pull request is refreshed in place by every scheduled localization run, so
there is only ever one open localization PR. Merging it lets the next run start
from a clean base; leaving it open simply keeps it up to date.

- Branch: ``$Branch``
- Last updated: $timestamp UTC
"@
}

function Invoke-GitHubApi {
    <#
    .SYNOPSIS
        Calls a GitHub REST API endpoint, surfacing the response body on error.
    #>
    param(
        [Parameter(Mandatory)][string]$Uri,
        [string]$Method = 'GET',
        [object]$Body = $null,
        [Parameter(Mandatory)][hashtable]$Headers
    )

    $params = @{
        Uri     = $Uri
        Method  = $Method
        Headers = $Headers
    }

    if ($null -ne $Body) {
        $params['Body'] = ($Body | ConvertTo-Json -Depth 10 -Compress)
        $params['ContentType'] = 'application/json'
    }

    try {
        return Invoke-RestMethod @params -ErrorAction Stop
    }
    catch {
        $status = $null
        $responseBody = ''

        if ($_.Exception.PSObject.Properties.Name -contains 'Response' -and $null -ne $_.Exception.Response) {
            $status = $_.Exception.Response.StatusCode.value__
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $responseBody = $reader.ReadToEnd()
            }
            catch {
                # The response stream may already be consumed; the status code
                # alone is still useful.
            }
        }

        Write-Host "##vso[task.logissue type=error]GitHub API $Method $Uri failed (HTTP $status)."
        if ($responseBody) {
            Write-Host "##vso[task.logissue type=error]Response: $responseBody"
        }

        throw
    }
}

function Invoke-Git {
    <#
    .SYNOPSIS
        Runs a git command and throws if it reports failure.
    #>
    param(
        [Parameter(Mandatory, ValueFromRemainingArguments)][string[]]$Arguments
    )

    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

#endregion

#region Validation

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    throw 'A GitHub access token is required. Set GITHUB_TOKEN or pass -AccessToken.'
}

$repoSlug = Get-RepositorySlug -Repository $GitHubRepository
$repoOwner = $repoSlug.Split('/')[0]

$sourceResources = Join-Path $SourceDirectory $ResourcesPath
if (-not (Test-Path -LiteralPath $sourceResources)) {
    throw "Localized resources folder not found at '$sourceResources'."
}

$localizedFiles = @(Get-ChildItem -Path $sourceResources -Filter $ResourceFilePattern -File)
if ($localizedFiles.Count -eq 0) {
    throw "No files matching '$ResourceFilePattern' were found in '$sourceResources'. Did the OneLocBuild step run?"
}

$ownedWorkingDirectory = $false
if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
    $WorkingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "loc-pr-$([guid]::NewGuid().ToString('n'))"
    $ownedWorkingDirectory = $true
}

$headers = @{
    'Authorization'        = "Bearer $AccessToken"
    'Accept'               = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent'           = 'SqlClient-DevOps'
}

#endregion

Write-Host '=== Publish localized resources to GitHub ==='
Write-Host "Repository : $repoSlug"
Write-Host "Base       : $BaseBranch"
Write-Host "Branch     : $BranchName"
Write-Host "Resources  : $($localizedFiles.Count) file(s) from $sourceResources"
Write-Host ''

$originalLocation = Get-Location

try {
    #region Clone and stage

    if (Test-Path -LiteralPath $WorkingDirectory) {
        Remove-Item -LiteralPath $WorkingDirectory -Recurse -Force
    }

    # The token is embedded in the remote URL so git can push without an
    # interactive credential prompt. Git masks it in its own output, and the
    # pipeline masks the secret in task logs.
    $cloneUrl = "https://x-access-token:$AccessToken@github.com/$repoSlug.git"

    Write-Host "Cloning $repoSlug@$BaseBranch..."
    Invoke-Git clone --branch $BaseBranch --quiet $cloneUrl $WorkingDirectory

    Set-Location -LiteralPath $WorkingDirectory

    Invoke-Git config user.email 'sqlclient@microsoft.com'
    Invoke-Git config user.name 'SqlClient DevOps'

    $baseSha = (& git rev-parse HEAD).Trim()
    Write-Host "Base HEAD  : $baseSha"

    # Fetch the automation branch if it already exists so we can tell an actual
    # content change apart from a re-run that would produce an identical commit.
    & git fetch origin "refs/heads/${BranchName}:refs/remotes/origin/$BranchName" --quiet 2>&1 | Out-Null
    $remoteBranchSha = (& git rev-parse --verify --quiet "refs/remotes/origin/$BranchName")
    $remoteBranchExists = -not [string]::IsNullOrWhiteSpace($remoteBranchSha)

    if ($remoteBranchExists) {
        Write-Host "Branch HEAD: $($remoteBranchSha.Trim())"
    }

    $targetResources = Join-Path $WorkingDirectory $ResourcesPath
    if (-not (Test-Path -LiteralPath $targetResources)) {
        throw "Resources folder '$ResourcesPath' does not exist in $repoSlug@$BaseBranch."
    }

    Copy-Item -Path (Join-Path $sourceResources $ResourceFilePattern) -Destination $targetResources -Force

    Invoke-Git add --all -- $ResourcesPath

    & git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host ''
        Write-Host "No localization changes relative to '$BaseBranch'. Nothing to publish."
        exit 0
    }

    #endregion

    #region Commit and push

    Write-Host ''
    Write-Host "Committing localized resources onto '$BranchName'..."

    # Always rebuild the branch from the current base so the pull request stays
    # mergeable, rather than accumulating commits run over run.
    Invoke-Git checkout -B $BranchName --quiet
    Invoke-Git commit --quiet --message $PrTitle

    $newTree = (& git rev-parse 'HEAD^{tree}').Trim()

    $branchUpToDate = $false
    if ($remoteBranchExists) {
        $remoteTree = (& git rev-parse "refs/remotes/origin/$BranchName^{tree}" 2>$null)
        $remoteParent = (& git rev-parse --verify --quiet "refs/remotes/origin/$BranchName^")

        $branchUpToDate =
            $null -ne $remoteTree -and
            $remoteTree.Trim() -eq $newTree -and
            $null -ne $remoteParent -and
            $remoteParent.Trim() -eq $baseSha
    }

    if ($branchUpToDate) {
        Write-Host "Branch '$BranchName' already has these exact changes on top of '$BaseBranch'. Skipping push."
    }
    else {
        Invoke-Git push origin "${BranchName}:refs/heads/$BranchName" --force --quiet
        Write-Host "Pushed '$BranchName'."
    }

    #endregion

    #region Create or reuse the pull request

    Write-Host ''
    Write-Host 'Checking for an existing open pull request...'

    $encodedHead = [Uri]::EscapeDataString("${repoOwner}:$BranchName")
    $encodedBase = [Uri]::EscapeDataString($BaseBranch)
    $listUri = "https://api.github.com/repos/$repoSlug/pulls?state=open&head=$encodedHead&base=$encodedBase"

    $openPrs = @(Invoke-GitHubApi -Uri $listUri -Headers $headers)
    $existingPr = $openPrs | Select-Object -First 1

    $body = Get-PullRequestBody -Branch $BranchName -UpdatedUtc ([datetime]::UtcNow)

    if ($existingPr) {
        $prNumber = $existingPr.number
        Write-Host "Reusing open PR #$prNumber - refreshing its description."

        $patchUri = "https://api.github.com/repos/$repoSlug/pulls/$prNumber"
        Invoke-GitHubApi -Uri $patchUri -Method 'PATCH' -Headers $headers -Body @{
            title = $PrTitle
            body  = $body
        } | Out-Null

        Write-Host "Pull request updated: $($existingPr.html_url)"
    }
    else {
        Write-Host 'No open pull request found. Creating one...'

        $createUri = "https://api.github.com/repos/$repoSlug/pulls"
        $newPr = Invoke-GitHubApi -Uri $createUri -Method 'POST' -Headers $headers -Body @{
            title = $PrTitle
            head  = $BranchName
            base  = $BaseBranch
            body  = $body
        }

        Write-Host "Pull request created: $($newPr.html_url)"
    }

    #endregion

    Write-Host ''
    Write-Host '=== Done ==='
}
finally {
    Set-Location -LiteralPath $originalLocation

    if ($ownedWorkingDirectory -and (Test-Path -LiteralPath $WorkingDirectory)) {
        Remove-Item -LiteralPath $WorkingDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

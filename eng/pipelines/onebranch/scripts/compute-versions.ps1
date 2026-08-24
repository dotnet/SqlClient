<#
.SYNOPSIS
    Computes effective SqlClient and SqlServer package and file versions for OneBranch builds.

.DESCRIPTION
    Evaluates the canonical version properties through the GetVersionsSqlClient and
    GetVersionsSqlServer targets in build.proj, selects the versions that the current pipeline run
    should consume, and publishes those values as Azure DevOps job output variables.

    The SqlClient family always uses SqlClientNextVersion. Microsoft.SqlServer.Server uses
    SqlServerNextVersion when BuildSqlServer is true and SqlServerPublishedVersion when it is false.
    The published SqlServer version is needed when SqlServer is not built because downstream
    SqlClient projects restore that existing package from NuGet.

    The supplied revision is mapped to the range 1 through 65535 before canonical versions
    are evaluated so every file version has a valid fourth component. When AddRevision is true, that
    same mapped value is inserted as the fourth numeric component of package versions built during
    this run. Any prerelease suffix remains after the revision. For example, 1.2.3-preview1 with
    revision 165500 becomes package version 1.2.3.34430-preview1 and file version 1.2.3.34430.

    Revisions above 65535 wrap through the valid unsigned 16-bit file-version range. The script logs
    the mapping because the revision may then collide with an earlier run. An unbuilt SqlServer
    package is never revised because its effective version must continue to identify the package
    that already exists on NuGet.

    The script emits these output variables for downstream stages:
      - VersionRevision
      - SqlClientPackageVersion
      - SqlServerPackageVersion

.PARAMETER ProjectPath
    Absolute or relative path to the repository build.proj file.

.PARAMETER Revision
    Positive integer used to distinguish versions. Values above 65535 are wrapped into the unsigned
    16-bit revision range.

.PARAMETER BuildSqlServer
    Whether this run builds Microsoft.SqlServer.Server. When false, the effective SqlServer package
    version is its last published version and its file version is not consumed downstream.

.PARAMETER AddRevision
    Whether to insert the revision into package versions built during this run.
    Defaults to false in both top-level OneBranch pipelines.

.PARAMETER DotnetPath
    dotnet executable to invoke. Defaults to the dotnet command resolved from PATH. This parameter
    primarily supports isolated testing and specialized agent configurations.

.EXAMPLE
    ./compute-versions.ps1 `
        -ProjectPath ./build.proj `
        -Revision 165500 `
        -BuildSqlServer $true `
        -AddRevision $true

    Computes versions for a run that builds SqlServer and appends mapped revision 34430 to both
    package families and their file versions.

.EXAMPLE
    ./compute-versions.ps1 `
        -ProjectPath ./build.proj `
        -Revision 165500 `
        -BuildSqlServer $false `
        -AddRevision $true

    Revises the SqlClient family versions while retaining SqlServerPublishedVersion for dependency
    restore because SqlServer is not built in this run.

.NOTES
    File Name : compute-versions.ps1
    Requires  : PowerShell 7+ and the repository-pinned .NET SDK.
    Called by : compute-versions-stage.yml
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Path to the repository build.proj file.")]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$ProjectPath,

    [Parameter(Mandatory = $true, HelpMessage = "Positive integer version revision.")]
    [ValidateRange(1, [long]::MaxValue)]
    [long]$Revision,

    [Parameter(Mandatory = $true, HelpMessage = "Whether Microsoft.SqlServer.Server is built in this run.")]
    [bool]$BuildSqlServer,

    [Parameter(Mandatory = $true, HelpMessage = "Whether to append the revision to built package versions.")]
    [bool]$AddRevision,

    [Parameter(HelpMessage = "dotnet executable to invoke.")]
    [ValidateNotNullOrEmpty()]
    [string]$DotnetPath = "dotnet"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$wrappedRevision = (($Revision - 1) % 65535) + 1
if ($Revision -gt 65535) {
    Write-Host "Revision $Revision exceeds the unsigned 16-bit limit and wrapped to $wrappedRevision; this revision may collide with an earlier run."
}

<#
.SYNOPSIS
    Extracts the first value associated with a labeled GetVersions target output line.

.PARAMETER Output
    Output lines captured from dotnet build.

.PARAMETER Label
    Label prefix to find, such as PackageVersion or PublishedVersion.

.OUTPUTS
    The trimmed label value, or an empty string when the label is absent.
#>
function Get-LabeledValue {
    param(
        [string[]]$Output,
        [string]$Label
    )

    $match = $Output | Select-String -Pattern "^\s*${Label}:\s*(.*?)\s*$" | Select-Object -First 1
    if ($null -eq $match) {
        return ""
    }

    return $match.Matches[0].Groups[1].Value.Trim()
}

<#
.SYNOPSIS
    Evaluates one canonical package family's versions through build.proj.

.PARAMETER Label
    GetVersions target suffix: SqlClient or SqlServer.

.OUTPUTS
    An object containing PackageVersion and PublishedVersion.
#>
function Get-CanonicalVersions {
    param(
        [ValidateSet("SqlClient", "SqlServer")]
        [string]$Label
    )

    $output = & $DotnetPath build $ProjectPath `
        -t:"GetVersions${Label}" -v:m -nologo -p:BuildNumber=$wrappedRevision 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output -join [Environment]::NewLine)
    }

    $packageVersion = Get-LabeledValue -Output $output -Label "PackageVersion"
    $publishedVersion = Get-LabeledValue -Output $output -Label "PublishedVersion"
    if ([string]::IsNullOrWhiteSpace($packageVersion)) {
        throw "Failed to extract PackageVersion for ${Label}.`n$($output -join [Environment]::NewLine)"
    }

    [pscustomobject]@{
        PackageVersion = $packageVersion
        PublishedVersion = $publishedVersion
    }
}

<#
.SYNOPSIS
    Inserts a numeric revision before a package version's prerelease suffix.

.PARAMETER Version
    Package version with a three-part numeric base and optional prerelease suffix.

.PARAMETER Revision
    Revision in the unsigned 16-bit file-version range.

.OUTPUTS
    A four-part package version preserving the original prerelease suffix.
#>
function Add-VersionRevision {
    param(
        [string]$Version,
        [ValidateRange(1, 65535)]
        [int]$Revision
    )

    $parts = $Version -split "-", 2
    if ($parts[0] -notmatch "^\d+\.\d+\.\d+$") {
        throw "Expected a three-part numeric version base, but received '$Version'."
    }

    $versionWithRevision = "$($parts[0]).$Revision"
    if ($parts.Count -eq 2) {
        return "$versionWithRevision-$($parts[1])"
    }

    return $versionWithRevision
}

<#
.SYNOPSIS
    Emits an Azure DevOps job output variable for consumption by downstream stages.

.PARAMETER Name
    Output variable name.

.PARAMETER Value
    Output variable value.
#>
function Set-PipelineOutputVariable {
    param(
        [string]$Name,
        [string]$Value
    )

    Write-Host "##vso[task.setvariable variable=${Name};isOutput=true]$Value"
}

Write-Host "Extracting versions with revision=$Revision (wrapped=$wrappedRevision)..."
$sqlClientVersions = Get-CanonicalVersions -Label "SqlClient"
$sqlServerVersions = Get-CanonicalVersions -Label "SqlServer"

Write-Host "  SqlClient: pkg=$($sqlClientVersions.PackageVersion)"
Write-Host "  SqlServer: pkg=$($sqlServerVersions.PackageVersion) pub=$($sqlServerVersions.PublishedVersion)"

$sqlClientPackageVersion = $sqlClientVersions.PackageVersion
$sqlServerPackageVersion = if ($BuildSqlServer) {
    $sqlServerVersions.PackageVersion
} else {
    if ([string]::IsNullOrWhiteSpace($sqlServerVersions.PublishedVersion)) {
        throw "GetVersionsSqlServer did not emit PublishedVersion for an unbuilt SqlServer dependency."
    }
    $sqlServerVersions.PublishedVersion
}

if ($AddRevision) {
    $sqlClientPackageVersion = Add-VersionRevision `
        -Version $sqlClientPackageVersion `
        -Revision $wrappedRevision
    if ($BuildSqlServer) {
        $sqlServerPackageVersion = Add-VersionRevision `
            -Version $sqlServerPackageVersion `
            -Revision $wrappedRevision
    }

    Write-Host "Version revision: $wrappedRevision (input=$Revision)"
}

Write-Host "Effective versions:"
Write-Host "  SqlClient (family): $sqlClientPackageVersion"
Write-Host "  SqlServer:          $sqlServerPackageVersion"

Set-PipelineOutputVariable -Name "SqlClientPackageVersion" -Value $sqlClientPackageVersion
Set-PipelineOutputVariable -Name "SqlServerPackageVersion" -Value $sqlServerPackageVersion
Set-PipelineOutputVariable -Name "VersionRevision" -Value $wrappedRevision

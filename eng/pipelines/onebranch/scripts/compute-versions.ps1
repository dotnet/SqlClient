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

    Package versions take a single shape, produced by Versions.props from the pipeline build number.
    Preview versions carry the full build number after the prerelease suffix, such as
    1.2.3-preview1.26238.3. Stable versions are left exactly as declared in Versions.props, such as
    1.2.3, because released packages are not stamped with a build number.

    File versions are always four-part and always carry a build number in the fourth component, even
    when the package version does not. Versions.props derives that component from the date segment of
    BuildNumber, so a package version of 1.2.3-preview1.26238.3 has file version 1.2.3.26238, and a
    stable package version of 1.2.3 still has file version 1.2.3.26238. That segment is date-coded,
    so repeated runs on the same day share a file version even though their package versions differ.

    An unbuilt SqlServer package is never stamped with a build number because its effective version
    must continue to identify the package that already exists on NuGet.

    The script emits these output variables for downstream stages:
      - SqlClientPackageVersion
      - SqlClientFileVersion
      - SqlServerPackageVersion
<<<<<<< HEAD
      - SqlServerFileVersion
=======
>>>>>>> c0e03dd46 (Derive APIScan versions from package versions)
      - SqlClientApiScanVersion
      - SqlServerApiScanVersion

.PARAMETER ProjectPath
    Absolute or relative path to the repository build.proj file.

.PARAMETER BuildNumber
    Pipeline build number in the form <date>.<run>, such as 26238.3. Versions.props appends it to
    prerelease package versions and derives the file-version build number from its date segment.

.PARAMETER BuildSqlServer
    Whether this run builds Microsoft.SqlServer.Server. When false, the effective SqlServer package
    version is its last published version and its file version is not consumed downstream.

.PARAMETER DotnetPath
    dotnet executable to invoke. Defaults to the dotnet command resolved from PATH. This parameter
    primarily supports isolated testing and specialized agent configurations.

.EXAMPLE
    ./compute-versions.ps1 `
        -ProjectPath ./build.proj `
        -BuildNumber 26238.3 `
        -BuildSqlServer $true

    Computes versions for a run that builds SqlServer. Prerelease package versions become
    7.1.0-preview3.26238.3 and file versions become 7.1.0.26238.

.EXAMPLE
    ./compute-versions.ps1 `
        -ProjectPath ./build.proj `
        -BuildNumber 26238.3 `
        -BuildSqlServer $false

    Stamps the SqlClient family versions while retaining SqlServerPublishedVersion for dependency
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

    [Parameter(Mandatory = $true, HelpMessage = "Pipeline build number, such as 26238.3.")]
    [ValidatePattern("^\d+\.\d+$")]
    [string]$BuildNumber,

    [Parameter(Mandatory = $true, HelpMessage = "Whether Microsoft.SqlServer.Server is built in this run.")]
    [bool]$BuildSqlServer,

    [Parameter(HelpMessage = "dotnet executable to invoke.")]
    [ValidateNotNullOrEmpty()]
    [string]$DotnetPath = "dotnet"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
    An object containing PackageVersion, FileVersion, and PublishedVersion.
#>
function Get-CanonicalVersions {
    param(
        [ValidateSet("SqlClient", "SqlServer")]
        [string]$Label
    )

    $output = & $DotnetPath build $ProjectPath `
        -t:"GetVersions${Label}" -v:m -nologo -p:BuildNumber=$BuildNumber 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output -join [Environment]::NewLine)
    }

    $packageVersion = Get-LabeledValue -Output $output -Label "PackageVersion"
    $fileVersion = Get-LabeledValue -Output $output -Label "FileVersion"
    $publishedVersion = Get-LabeledValue -Output $output -Label "PublishedVersion"
    if ([string]::IsNullOrWhiteSpace($packageVersion)) {
        throw "Failed to extract PackageVersion for ${Label}.`n$($output -join [Environment]::NewLine)"
    }
    if ([string]::IsNullOrWhiteSpace($fileVersion)) {
        throw "Failed to extract FileVersion for ${Label}.`n$($output -join [Environment]::NewLine)"
    }

    [pscustomobject]@{
        PackageVersion = $packageVersion
        FileVersion = $fileVersion
        PublishedVersion = $publishedVersion
    }
}

<#
.SYNOPSIS
    Extracts the major.minor components from a package version.

.PARAMETER Version
    Package version beginning with a numeric major.minor pair.

.OUTPUTS
    The major.minor version pair.
#>
function Get-MajorMinorVersion {
    param(
        [string]$Version
    )

    if ($Version -notmatch "^(\d+)\.(\d+)(?:\.|-|$)") {
        throw "Unable to derive a major.minor version from package version '$Version'."
    }

    return "$($Matches[1]).$($Matches[2])"
}

<#
.SYNOPSIS
    Extracts the major.minor components from a package version.

.PARAMETER Version
    Package version beginning with a numeric major.minor pair.

.OUTPUTS
    The major.minor version pair.
#>
function Get-MajorMinorVersion {
    param(
        [string]$Version
    )

    if ($Version -notmatch "^(\d+)\.(\d+)(?:\.|-|$)") {
        throw "Unable to derive a major.minor version from package version '$Version'."
    }

    return "$($Matches[1]).$($Matches[2])"
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

Write-Host "Extracting versions with build number $BuildNumber..."
$sqlClientVersions = Get-CanonicalVersions -Label "SqlClient"
$sqlServerVersions = Get-CanonicalVersions -Label "SqlServer"

Write-Host "  SqlClient: pkg=$($sqlClientVersions.PackageVersion) file=$($sqlClientVersions.FileVersion)"
Write-Host "  SqlServer: pkg=$($sqlServerVersions.PackageVersion) file=$($sqlServerVersions.FileVersion) pub=$($sqlServerVersions.PublishedVersion)"

$sqlClientPackageVersion = $sqlClientVersions.PackageVersion
$sqlClientFileVersion = $sqlClientVersions.FileVersion

# An unbuilt SqlServer resolves to its published version, which no build job stamps, so it has no
# effective file version.
$sqlServerPackageVersion = if ($BuildSqlServer) {
    $sqlServerVersions.PackageVersion
} else {
    if ([string]::IsNullOrWhiteSpace($sqlServerVersions.PublishedVersion)) {
        throw "GetVersionsSqlServer did not emit PublishedVersion for an unbuilt SqlServer dependency."
    }
    $sqlServerVersions.PublishedVersion
}
$sqlServerFileVersion = if ($BuildSqlServer) { $sqlServerVersions.FileVersion } else { "" }

Write-Host "Effective versions:"
Write-Host "  SqlClient (family): $sqlClientPackageVersion (file $sqlClientFileVersion)"
Write-Host "  SqlServer:          $sqlServerPackageVersion (file $sqlServerFileVersion)"

$sqlClientApiScanVersion = Get-MajorMinorVersion -Version $sqlClientPackageVersion
$sqlServerApiScanVersion = Get-MajorMinorVersion -Version $sqlServerPackageVersion

Write-Host "APIScan registration versions:"
Write-Host "  SqlClient (family): $sqlClientApiScanVersion"
Write-Host "  SqlServer:          $sqlServerApiScanVersion"

Set-PipelineOutputVariable -Name "SqlClientPackageVersion" -Value $sqlClientPackageVersion
Set-PipelineOutputVariable -Name "SqlClientFileVersion" -Value $sqlClientFileVersion
Set-PipelineOutputVariable -Name "SqlServerPackageVersion" -Value $sqlServerPackageVersion
Set-PipelineOutputVariable -Name "SqlServerFileVersion" -Value $sqlServerFileVersion
Set-PipelineOutputVariable -Name "SqlClientApiScanVersion" -Value $sqlClientApiScanVersion
Set-PipelineOutputVariable -Name "SqlServerApiScanVersion" -Value $sqlServerApiScanVersion

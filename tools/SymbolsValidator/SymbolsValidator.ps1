<#
.SYNOPSIS
    Downloads a NuGet package and validates that symbols are available for all DLLs on configured symbol servers.

.DESCRIPTION
    This script automates the process of validating symbols for a NuGet package. It:
      1. Downloads the specified NuGet package from nuget.org to a temporary directory.
      2. Extracts the package contents.
      3. Enumerates all DLL files in the package (excluding resource assemblies).
      4. Calls the validate-symbols.ps1 script from the onebranch pipeline to verify each DLL
         has matching symbols available on the configured symbol servers.

    This tool is useful for verifying that symbols are properly published to public and internal
    symbol servers after package release.

    The validate-symbols.ps1 script includes built-in retry logic to handle symbol server publishing
    latency, so symbols may not be immediately available after package publication.

.PARAMETER PackageName
    The NuGet package name (e.g., "Microsoft.Data.SqlClient").
    This parameter is mandatory.

.PARAMETER PackageVersion
    The semantic version of the package to validate (e.g., "5.1.0", "6.0.0-beta1").
    This parameter is mandatory.

.PARAMETER SymbolServers
    An array of symbol servers to check. Each entry should have a 'name' (friendly display name)
    and 'url' (symbol server URL). If not provided, defaults to MSDL (public) and SymWeb (internal).

    Default servers:
      - MSDL (Public):    https://msdl.microsoft.com/download/symbols
      - SymWeb (Internal): https://symweb.azurefd.net

.PARAMETER ExtractionPath
    The directory where the package will be extracted. If not specified, a temporary directory
    is created. The temporary directory is removed after validation completes (use -KeepTemp to preserve).

.PARAMETER KeepTemp
    If specified, the temporary extraction directory is preserved after validation. This is useful
    for debugging package contents or manual inspection.

.PARAMETER ValidateScriptPath
    Path to the validate-symbols.ps1 script from the onebranch pipeline. If not specified,
    attempts to locate it relative to the repository root (../eng/pipelines/onebranch/scripts/validate-symbols.ps1).

    This parameter is mandatory if the script is not in the default location.

.PARAMETER MaxRetries
    Maximum number of attempts when symbols are not yet available (passed to validate-symbols.ps1).
    Each retry includes a wait interval. Defaults to 10 (~5 minutes with default interval).

.PARAMETER RetryIntervalSeconds
    Seconds to wait between retry attempts (passed to validate-symbols.ps1). Defaults to 30.

.PARAMETER SymchkPath
    Path to the symchk.exe tool. If not specified, attempts to locate it in standard Windows Kit
    installation directories. Required if symchk.exe is installed in a non-standard location.
    Type: `string`

.PARAMETER Force
    If specified, does not prompt for confirmation before downloading packages.

.PARAMETER Verbose
    Enables verbose output for debugging. This is the standard PowerShell -Verbose flag.

.EXAMPLE
    .\SymbolsValidator.ps1 -PackageName "Microsoft.Data.SqlClient" -PackageVersion "5.1.0"

    Downloads Microsoft.Data.SqlClient version 5.1.0 and validates symbols against the default
    symbol servers (MSDL and SymWeb).

.EXAMPLE
    .\SymbolsValidator.ps1 `
        -PackageName "Microsoft.Data.SqlClient" `
        -PackageVersion "6.0.0" `
        -SymbolServers @(
            @{ name = "MSDL"; url = "https://msdl.microsoft.com/download/symbols" }
        ) `
        -KeepTemp

    Validates symbols against only the MSDL server and preserves the temporary extraction directory
    for manual inspection.

.EXAMPLE
    .\SymbolsValidator.ps1 `
        -PackageName "Microsoft.Data.SqlClient" `
        -PackageVersion "5.1.0" `
        -ExtractionPath "C:\temp\SqlClient_validation" `
        -MaxRetries 20 `
        -RetryIntervalSeconds 15

    Downloads and validates the package, extracting to a specific directory with extended retry
    configuration (20 attempts, 15-second intervals).

.OUTPUTS
    None. Writes status information to the host. Exit code 0 indicates all DLLs passed validation;
    non-zero indicates at least one DLL failed validation.

.NOTES
    Prerequisites:
    - Internet connectivity to nuget.org
    - PowerShell 5.0 or later
    - Debugging Tools for Windows (for symchk.exe)
    - The validate-symbols.ps1 script must be available in the repository

    Symbol server availability:
    - MSDL (Public symbols): Always available from any internet-connected machine
    - SymWeb (Internal symbols): Only accessible from inside the Microsoft network or via VPN

    Temporary directories:
    - By default, a temporary directory is created in the system temp folder and deleted after
      validation completes. Use -KeepTemp to preserve it for debugging.

    Symchk Tool Discovery:
    - The script checks standard Windows Kit 10 and 11 installation paths
    - Use -SymchkPath to specify a custom location if installed elsewhere

.LINK
    https://github.com/dotnet/SqlClient
    https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace

#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$PackageName,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$PackageVersion,

    [object[]]$SymbolServers = @(
        @{ name = "MSDL (Public)"; url = "https://msdl.microsoft.com/download/symbols" }
        @{ name = "SymWeb (Internal)"; url = "https://symweb.azurefd.net" }
    ),

    [string]$ExtractionPath,

    [switch]$KeepTemp,

    [string]$ValidateScriptPath,

    [string]$SymchkPath,

    [int]$MaxRetries = 10,

    [int]$RetryIntervalSeconds = 30,

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# -- Configuration ---------------------------------------------------------------

$nugetUrl = "https://www.nuget.org/api/v2/package/$PackageName/$PackageVersion"

# Resource DLL patterns that don't typically have symbols published
$resourceDllPatterns = @(
    '\.resources\.dll$',  # Satellite assemblies (localized resources)
    '[Rr]esources\.dll$'   # Generic resources DLLs
)

# Symchk.exe search locations (Windows Kit standard paths)
$symchkSearchPaths = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\Debuggers\x64\symchk.exe"
    "${env:ProgramFiles}\Windows Kits\10\Debuggers\x64\symchk.exe"
    "${env:ProgramFiles(x86)}\Windows Kits\11\Debuggers\x64\symchk.exe"
    "${env:ProgramFiles}\Windows Kits\11\Debuggers\x64\symchk.exe"
)

# -- Helper functions -----------------------------------------------------------

function Write-Status {
    param([string]$Message)
    Write-Host "[$([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss UTC'))] $Message"
}

function Find-SymchkExecutable {
    Write-Status "Searching for symchk.exe in standard Windows Kit locations..."

    foreach ($candidate in $symchkSearchPaths) {
        if (Test-Path $candidate) {
            Write-Status "Found symchk.exe at: $candidate"
            return $candidate
        }
    }

    Write-Verbose "symchk.exe not found in standard locations:"
    foreach ($candidate in $symchkSearchPaths) {
        Write-Verbose "  - $candidate"
    }

    return $null
}

function Show-SymchkInstallationGuide {
    Write-Host ""
    Write-Host "symchk.exe is not found. It is part of Debugging Tools for Windows." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Install using one of these methods:"
    Write-Host ""
    Write-Host "1. Download Windows SDK:"
    Write-Host "   https://developer.microsoft.com/windows/downloads/windows-sdk/"
    Write-Host "   During installation, select only 'Debugging Tools for Windows'"
    Write-Host ""
    Write-Host "2. Or install via Visual Studio:"
    Write-Host "   Launch Visual Studio Installer"
    Write-Host "   Modify your Visual Studio installation"
    Write-Host "   Under Individual Components, search for and select 'Debugging Tools for Windows'"
    Write-Host ""
    Write-Host "3. Or use Windows Package Manager:"
    Write-Host "   winget install Microsoft.WindowsSDK"
    Write-Host ""
    Write-Host "Standard installation paths:"
    Write-Host "   $($symchkSearchPaths[0])"
    Write-Host "   $($symchkSearchPaths[1])"
    Write-Host ""
    Write-Host "After installation, either:"
    Write-Host "   a) Re-run this script (it will find symchk.exe automatically), or"
    Write-Host "   b) Use -SymchkPath to specify the location explicitly"
    Write-Host ""
}

function Ensure-SymchkAvailable {
    param(
        [string]$SymchkPath
    )

    # If path explicitly provided, use it
    if ($SymchkPath) {
        if (Test-Path $SymchkPath) {
            Write-Status "Using provided symchk.exe path: $SymchkPath"
            return $SymchkPath
        }
        else {
            Write-Error "Specified symchk.exe path not found: $SymchkPath"
        }
    }

    # Try to find in standard locations
    $found = Find-SymchkExecutable
    if ($found) {
        return $found
    }

    # Not found - show installation guide
    Show-SymchkInstallationGuide
    Write-Error "symchk.exe not found. Please install Debugging Tools for Windows and try again."
    return $null
}

function Get-NuGetPackage {
    param(
        [string]$PackageName,
        [string]$Version,
        [string]$OutputPath
    )

    Write-Status "Downloading $PackageName version $Version from nuget.org..."
    $nupkgFile = Join-Path $OutputPath "$PackageName.$Version.nupkg"

    try {
        Invoke-WebRequest -Uri $nugetUrl -OutFile $nupkgFile -ErrorAction Stop
        Write-Status "Downloaded: $nupkgFile"
        return $nupkgFile
    }
    catch {
        Write-Error "Failed to download package from $nugetUrl : $_"
    }
}

function Expand-NuGetPackage {
    param(
        [string]$NupkgFile,
        [string]$ExtractTo
    )

    Write-Status "Extracting package to: $ExtractTo"
    New-Item -ItemType Directory -Force -Path $ExtractTo | Out-Null

    # .nupkg is a ZIP file
    $zipPath = Join-Path $ExtractTo 'package.zip'
    Copy-Item $NupkgFile $zipPath

    try {
        Expand-Archive -Path $zipPath -DestinationPath $ExtractTo -Force
        Remove-Item $zipPath -Force
        Write-Status "Package extracted successfully"
    }
    catch {
        Write-Error "Failed to extract package: $_"
    }
}

function Get-DllsInPackage {
    param(
        [string]$ExtractedPath
    )

    Write-Status "Scanning for DLLs in extracted package..."

    # Find all .dll files in the lib directory
    $allDlls = Get-ChildItem -Path $ExtractedPath -Recurse -Filter "*.dll" -ErrorAction SilentlyContinue

    # Filter out resource DLLs
    $dlls = @()
    foreach ($dll in $allDlls) {
        $isResource = $false
        foreach ($pattern in $resourceDllPatterns) {
            if ($dll.Name -match $pattern) {
                Write-Verbose "Skipping resource DLL: $($dll.FullName)"
                $isResource = $true
                break
            }
        }

        if (-not $isResource) {
            $dlls += $dll
        }
    }

    if ($dlls.Count -eq 0) {
        Write-Warning "No DLLs found in package (excluding resources)"
        return @()
    }

    Write-Status "Found $($dlls.Count) DLL(s) to validate:"
    foreach ($dll in $dlls) {
        # Show relative path from extracted root
        $relativePath = $dll.FullName.Substring($ExtractedPath.Length + 1)
        Write-Host "  - $relativePath"
    }

    return $dlls
}

function Invoke-SymbolValidation {
    param(
        [string]$ValidateScriptPath,
        [string]$ArtifactPath,
        [string]$ExtractPath,
        [string]$PackageName,
        [System.IO.FileInfo]$DllFile,
        [object[]]$SymbolServers,
        [int]$MaxRetries,
        [int]$RetryIntervalSeconds,
        [string]$SymchkPath
    )

    $results = @()

    foreach ($server in $SymbolServers) {
        $serverName = $server.name
        $serverUrl = $server.url

        Write-Status "Validating $($DllFile.Name) on $serverName..."

        # Calculate relative path from extracted root to the DLL
        $dllRelativePath = $DllFile.FullName.Substring($ExtractPath.Length + 1)

        try {
            # Call validate-symbols.ps1
            $validateArgs = @{
                ArtifactPath        = $ArtifactPath
                ExtractPath         = $ExtractPath
                PackageName         = $PackageName
                DllPath             = $dllRelativePath
                SymbolServerUrl     = $serverUrl
                SymbolServerName    = $serverName
                MaxRetries          = $MaxRetries
                RetryIntervalSeconds = $RetryIntervalSeconds
            }

            # Pass symchk path if available
            if ($SymchkPath) {
                $validateArgs.SymchkPath = $SymchkPath
            }

            & $ValidateScriptPath @validateArgs

            $exitCode = $LASTEXITCODE
            if ($exitCode -eq 0) {
                Write-Status "✓ Symbols validated for $($DllFile.Name) on $serverName"
                $results += @{ DLL = $DllFile.Name; Server = $serverName; Status = "PASS" }
            }
            else {
                Write-Error "✗ Symbols validation failed for $($DllFile.Name) on $serverName (exit code: $exitCode)"
                $results += @{ DLL = $DllFile.Name; Server = $serverName; Status = "FAIL" }
            }
        }
        catch {
            Write-Error "✗ Error validating $($DllFile.Name) on $serverName : $_"
            $results += @{ DLL = $DllFile.Name; Server = $serverName; Status = "ERROR" }
        }
    }

    return $results
}

# -- Main script ---------------------------------------------------------------

try {
    Write-Status "=== Symbol Validation Starting ==="
    Write-Status "Package: $PackageName"
    Write-Status "Version: $PackageVersion"
    Write-Status "Symbol Servers: $($SymbolServers.Count)"
    foreach ($server in $SymbolServers) {
        Write-Host "  - $($server.name): $($server.url)"
    }

    # Resolve validate-symbols.ps1 script path
    if (-not $ValidateScriptPath) {
        # Try to find it relative to the repository root
        $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
        $repoRoot = $null

        # Walk up from tools/SymbolsValidator to find the repository root
        $candidate = $scriptDir
        for ($i = 0; $i -lt 5; $i++) {
            if (Test-Path (Join-Path $candidate "eng" "pipelines" "onebranch" "scripts" "validate-symbols.ps1")) {
                $repoRoot = $candidate
                break
            }
            $candidate = Split-Path -Parent $candidate
        }

        if ($repoRoot) {
            $ValidateScriptPath = Join-Path $repoRoot "eng" "pipelines" "onebranch" "scripts" "validate-symbols.ps1"
        }
    }

    if (-not (Test-Path $ValidateScriptPath)) {
        Write-Error "validate-symbols.ps1 script not found at: $ValidateScriptPath. Use -ValidateScriptPath to specify its location."
    }

    Write-Status "Using validate-symbols.ps1 at: $ValidateScriptPath"

    # Set up extraction directory
    if (-not $ExtractionPath) {
        $ExtractionPath = Join-Path $env:TEMP "NuGetValidator_$(Get-Random)"
        Write-Verbose "Using temporary directory: $ExtractionPath"
    }

    $tempDirCreated = -not (Test-Path $ExtractionPath)
    New-Item -ItemType Directory -Force -Path $ExtractionPath | Out-Null

    try {
        # Ensure symchk.exe is available
        Write-Status "=== Checking for symchk.exe ==="
        $resolvedSymchkPath = Ensure-SymchkAvailable -SymchkPath $SymchkPath

        if (-not $resolvedSymchkPath) {
            Write-Error "Cannot proceed without symchk.exe."
        }
        Write-Status "Symchk ready: $resolvedSymchkPath"

        # Download package to a subdirectory for artifact handling
        $artifactPath = Join-Path $ExtractionPath "artifact"
        New-Item -ItemType Directory -Force -Path $artifactPath | Out-Null

        # Download the package
        $nupkgFile = Get-NuGetPackage -PackageName $PackageName -Version $PackageVersion -OutputPath $artifactPath

        # Extract the package
        $packageExtractPath = Join-Path $ExtractionPath "extracted"
        Expand-NuGetPackage -NupkgFile $nupkgFile -ExtractTo $packageExtractPath

        # Find DLLs in the package
        $dlls = Get-DllsInPackage -ExtractedPath $packageExtractPath

        if ($dlls.Count -eq 0) {
            Write-Warning "No DLLs found in package. Nothing to validate."
            exit 0
        }

        # Validate symbols for each DLL
        Write-Status "=== Validating Symbols ==="
        $allResults = @()

        foreach ($dll in $dlls) {
            $results = Invoke-SymbolValidation `
                -ValidateScriptPath $ValidateScriptPath `
                -ArtifactPath $artifactPath `
                -ExtractPath $packageExtractPath `
                -PackageName $PackageName `
                -DllFile $dll `
                -SymbolServers $SymbolServers `
                -MaxRetries $MaxRetries `
                -RetryIntervalSeconds $RetryIntervalSeconds `
                -SymchkPath $resolvedSymchkPath

            $allResults += $results
        }

        # Summary
        Write-Status "=== Validation Summary ==="
        $passCount = ($allResults | Where-Object { $_.Status -eq "PASS" }).Count
        $failCount = ($allResults | Where-Object { $_.Status -eq "FAIL" }).Count
        $errorCount = ($allResults | Where-Object { $_.Status -eq "ERROR" }).Count

        Write-Host "Total checks: $($allResults.Count)"
        Write-Host "Passed: $passCount"
        Write-Host "Failed: $failCount"
        Write-Host "Errors: $errorCount"

        if ($failCount -gt 0 -or $errorCount -gt 0) {
            Write-Host ""
            Write-Host "Failed/Error results:"
            $allResults | Where-Object { $_.Status -ne "PASS" } | ForEach-Object {
                Write-Host "  - $($_.DLL) on $($_.Server): $($_.Status)"
            }
            exit 1
        }

        Write-Status "=== All Symbols Validated Successfully ==="
        exit 0
    }
    finally {
        # Clean up temporary directory if requested
        if ($tempDirCreated -and -not $KeepTemp -and (Test-Path $ExtractionPath)) {
            Write-Status "Cleaning up temporary directory: $ExtractionPath"
            Remove-Item -Recurse -Force -Path $ExtractionPath -ErrorAction SilentlyContinue
        }
        elseif ($KeepTemp) {
            Write-Status "Preserving extraction directory: $ExtractionPath"
        }
    }
}
catch {
    Write-Error "Fatal error: $_"
    exit 1
}

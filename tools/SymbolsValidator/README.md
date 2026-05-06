# SymbolsValidator

A PowerShell utility for validating that symbols are correctly published to symbol servers for NuGet packages.

## Overview

This tool automates the process of verifying symbol availability after publishing a NuGet package. It:

1. Downloads the specified NuGet package from nuget.org
2. Extracts the package contents
3. Identifies all managed DLL files (excluding resource assemblies)
4. Validates that symbols (PDBs) for each DLL are available on configured symbol servers
5. Reports results with detailed status

The tool integrates with the Azure DevOps onebranch pipeline's `validate-symbols.ps1` script and includes built-in retry logic to handle symbol server publishing latency.

## Quick Start

### Basic Usage

```powershell
.\SymbolsValidator.ps1 -PackageName "Microsoft.Data.SqlClient" -PackageVersion "5.1.0"
```

This downloads the specified package and validates symbols against the default symbol servers:
- **MSDL (Public)**: https://msdl.microsoft.com/download/symbols
- **SymWeb (Internal)**: https://symweb.azurefd.net (Microsoft network only)

### Save Extracted Package for Inspection

```powershell
.\SymbolsValidator.ps1 `
    -PackageName "Microsoft.Data.SqlClient" `
    -PackageVersion "5.1.0" `
    -KeepTemp
```

The extraction directory path is displayed in the output. Useful for debugging package contents or testing.

### Custom Symbol Servers

```powershell
.\SymbolsValidator.ps1 `
    -PackageName "Microsoft.Data.SqlClient" `
    -PackageVersion "5.1.0" `
    -SymbolServers @(
        @{ name = "MSDL"; url = "https://msdl.microsoft.com/download/symbols" }
    )
```

### Extended Retries for Slow Publishing

```powershell
.\SymbolsValidator.ps1 `
    -PackageName "Microsoft.Data.SqlClient" `
    -PackageVersion "5.1.0" `
    -MaxRetries 20 `
    -RetryIntervalSeconds 15
```

The default configuration allows ~5 minutes for symbols to be published. Use extended retries if symbols are taking longer to appear.

## Parameters

### Required Parameters

**`-PackageName`**
- The NuGet package name (e.g., `"Microsoft.Data.SqlClient"`)
- Type: `string`

**`-PackageVersion`**
- The semantic version of the package (e.g., `"5.1.0"`, `"6.0.0-beta1"`)
- Type: `string`

### Optional Parameters

**`-SymbolServers`**
- Array of symbol servers to validate against
- Default: MSDL (Public) and SymWeb (Internal)
- Each entry must have `name` and `url` properties
- Type: `object[]`

**`-ExtractionPath`**
- Directory where the package will be extracted
- Default: Temporary directory (auto-created and cleaned up)
- Type: `string`

**`-KeepTemp`**
- Preserve the temporary extraction directory after validation
- Useful for debugging or manual inspection
- Type: `switch`

**`-ValidateScriptPath`**
- Path to the `validate-symbols.ps1` script from the onebranch pipeline
- Default: Auto-detected relative to repository root
- Type: `string`

**`-MaxRetries`**
- Maximum number of validation attempts (default: 10)
- Accommodates symbol server publishing latency
- Type: `int`

**`-RetryIntervalSeconds`**
- Wait time between retry attempts in seconds (default: 30)
- Type: `int`

**`-SymchkPath`**
- Path to the symchk.exe tool
- Default: Auto-detected from standard Windows Kit installation locations
- Use this to specify a custom location if auto-detection fails
- Type: `string`

**`-Force`**
- Skip confirmation prompts
- Type: `switch`

**`-Verbose`**
- Enable verbose output for debugging
- Standard PowerShell flag
- Type: `switch`

## Output

The script writes timestamped status information to the console and provides a summary:

```
[2025-05-06 14:23:45 UTC] === Symbol Validation Starting ===
[2025-05-06 14:23:45 UTC] Package: Microsoft.Data.SqlClient
[2025-05-06 14:23:45 UTC] Version: 5.1.0
...
[2025-05-06 14:25:12 UTC] === Validation Summary ===
Total checks: 4
Passed: 4
Failed: 0
Errors: 0
[2025-05-06 14:25:12 UTC] === All Symbols Validated Successfully ===
```

### Exit Codes

- **0** — All symbols validated successfully
- **1** — One or more DLLs failed validation or an error occurred

## Prerequisites

### System Requirements

- **PowerShell 5.0** or later
- **Internet connectivity** to nuget.org
- **Debugging Tools for Windows** (symchk.exe):
  - Install via Windows SDK, Visual Studio, or Windows Package Manager
  - Or specify custom location with -SymchkPath parameter

### Auto-Discovery Behavior

The script automatically checks for symchk.exe in standard Windows Kit locations:
- `Program Files (x86)\Windows Kits\10\Debuggers\x64\symchk.exe`
- `Program Files\Windows Kits\10\Debuggers\x64\symchk.exe`
- `Program Files (x86)\Windows Kits\11\Debuggers\x64\symchk.exe`
- `Program Files\Windows Kits\11\Debuggers\x64\symchk.exe`

If symchk.exe is not found, the script provides installation instructions.

### Network Requirements

- **MSDL (Public Symbols)** — Accessible from any internet-connected machine
- **SymWeb (Internal Symbols)** — Requires:
  - Microsoft network access OR
  - VPN connection to Microsoft network

### Repository Context

The script must be able to locate the onebranch pipeline's `validate-symbols.ps1` script. It attempts to auto-detect this relative to the repository root. If auto-detection fails, use `-ValidateScriptPath` to specify the location explicitly.

## DLL Filtering

The script automatically filters out resource DLLs that don't typically have published symbols:

- `*.resources.dll` — Satellite assemblies (localized resources)
- `*resources.dll` — Generic resource assemblies

Only main product DLLs are validated.

## Examples

### Validate All Frameworks in a Package

```powershell
# Validates symbols for net462, net8.0, and net9.0 DLLs
.\SymbolsValidator.ps1 -PackageName "Microsoft.Data.SqlClient" -PackageVersion "5.1.0"
```

### Verbose Output with Temp Preservation

```powershell
.\SymbolsValidator.ps1 `
    -PackageName "Microsoft.Data.SqlClient" `
    -PackageVersion "5.1.0" `
    -KeepTemp `
    -Verbose
```

### Custom Extraction Location

```powershell
.\SymbolsValidator.ps1 `
    -PackageName "Microsoft.Data.SqlClient" `
    -PackageVersion "5.1.0" `
    -ExtractionPath "C:\temp\sql_validation_5.1.0"
```

The extraction directory is created if it doesn't exist. Useful for archiving validation runs or CI/CD pipelines.

### Post-Release Validation in CI/CD

```powershell
# Typical CI/CD usage with extended retry for publishing latency
.\SymbolsValidator.ps1 `
    -PackageName "Microsoft.Data.SqlClient" `
    -PackageVersion "5.1.0" `
    -MaxRetries 30 `
    -RetryIntervalSeconds 20 `
    -ErrorAction Stop
```

### With Custom symchk Location

```powershell
# Use a specific symchk.exe if not in standard location
.\SymbolsValidator.ps1 `
    -PackageName "Microsoft.Data.SqlClient" `
    -PackageVersion "5.1.0" `
    -SymchkPath "C:\DebugTools\symchk.exe"
```

## Symbol Server Publishing Latency

After a NuGet package is released, it may take time for symbols to be published to symbol servers:

- **MSDL**: Usually available within 10-15 minutes
- **SymWeb**: Internal publishing may have different timing

The script includes retry logic to accommodate this. The default configuration allows up to 5 minutes. For initial post-release validation, consider using extended retries.

## Troubleshooting

### Script Not Found

**Error**: `validate-symbols.ps1 script not found`

**Solution**: Run from the correct directory within the repository, or use `-ValidateScriptPath` to specify the location:

```powershell
.\SymbolsValidator.ps1 `
    -PackageName "Microsoft.Data.SqlClient" `
    -PackageVersion "5.1.0" `
    -ValidateScriptPath "C:\path\to\eng\pipelines\onebranch\scripts\validate-symbols.ps1"
```

### symchk.exe Not Found

**Error**: `symchk.exe not found. Please install Debugging Tools for Windows and try again.`

**Solution**: The script will provide installation instructions. You can also:

1. **Install Debugging Tools for Windows** — Choose one:
   - Download from [Windows SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/) and select only 'Debugging Tools for Windows'
   - Install via Visual Studio (includes Debugging Tools)
   - Use Windows Package Manager: `winget install Microsoft.WindowsSDK`

2. **Specify custom location** — If installed in a non-standard path:
   ```powershell
   .\SymbolsValidator.ps1 `
       -PackageName "Microsoft.Data.SqlClient" `
       -PackageVersion "5.1.0" `
       -SymchkPath "C:\path\to\symchk.exe"
   ```

### Package Not Found

**Error**: `HTTP 404 when downloading from nuget.org`

**Solution**:
1. Verify package name and version are correct
2. Check that the package is published on nuget.org
3. Wait if the package was very recently published (may take a few minutes)

### Symbols Not Yet Available

**Error**: `symchk could not verify symbols... after N attempts`

**Solution**: Increase retry count and interval:

```powershell
.\SymbolsValidator.ps1 `
    -PackageName "Microsoft.Data.SqlClient" `
    -PackageVersion "5.1.0" `
    -MaxRetries 60 `
    -RetryIntervalSeconds 30
```

This allows up to 30 minutes for symbols to be published.

### SymWeb Not Accessible

**Error**: Connection error when validating against SymWeb

**Solution**: SymWeb requires Microsoft network access:
- Connect to Microsoft VPN if off-network
- Or validate against MSDL only:

```powershell
.\SymbolsValidator.ps1 `
    -PackageName "Microsoft.Data.SqlClient" `
    -PackageVersion "5.1.0" `
    -SymbolServers @(
        @{ name = "MSDL"; url = "https://msdl.microsoft.com/download/symbols" }
    )
```

## Related Documentation

- [Microsoft.Data.SqlClient Documentation](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)
- [Azure Pipelines OneBranch Documentation](https://aka.ms/obpipelines)
- [Windows Debugging Tools Documentation](https://learn.microsoft.com/windows-hardware/drivers/debugger/)
- [MS-SSRP (Symbol Server Protocol)](https://learn.microsoft.com/openspecs/windows_protocols/ms-ssrp/)

## License

This script is part of the Microsoft.Data.SqlClient project and is licensed under the MIT License.

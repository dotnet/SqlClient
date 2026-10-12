# Pipeline Script Tests

Pester tests for the PowerShell helpers under `eng/pipelines/scripts/`.

## Prerequisites

These tests require **PowerShell 7+** and **Pester v5 or later**.
The Docker bottle tests run only in an Intel (x64) process.

```powershell
Install-Module Pester -MinimumVersion 5.0 -Scope CurrentUser -Force -SkipPublisherCheck
```

## Running the tests

```powershell
Import-Module Pester -MinimumVersion 5.0
Invoke-Pester ./eng/pipelines/scripts/tests/
```

Add `-Output Detailed` to see per-test results.

## Test files

| File | Covers |
| ---- | ------ |
| `Install-DockerCli.macos.Tests.ps1` | `Install-DockerCli.macos.ps1` — Homebrew bottle selection for the macOS docker CLI. |
| `Pipeline-Platforms.Tests.ps1` | Windows/Linux-only PR validation, macOS job/dependency exclusion, and unchanged CI macOS defaults. |

`tar`, `Invoke-RestMethod` and `Invoke-WebRequest` are mocked, so the
tests never touch the network or a real repository.

The platform checks only read the checked-in YAML templates and require neither a .NET SDK
nor a YAML parsing module. Run them alone without a test directory:

```powershell
$configuration = New-PesterConfiguration
$configuration.Run.Path = './eng/pipelines/scripts/tests/Pipeline-Platforms.Tests.ps1'
$configuration.TestDrive.Enabled = $false
$configuration.Run.Exit = $true
Invoke-Pester -Configuration $configuration
```

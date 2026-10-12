# Pipeline Script Tests

Pester tests for the PowerShell helpers under `eng/pipelines/scripts/`.

## Prerequisites

These tests require **Pester v5 or later**:

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
| `Open-LocalizationPr.Tests.ps1` | `Open-LocalizationPr.ps1` — de-duplication of the scheduled localization pull request. |
| `Install-DockerCli.macos.Tests.ps1` | `Install-DockerCli.macos.ps1` — Homebrew bottle selection for the macOS docker CLI. |
| `Pipeline-Platforms.Tests.ps1` | Windows/Linux-only PR validation, macOS job/dependency exclusion, and unchanged CI macOS defaults. |

The localization and Docker helper tests mock `git`, `tar`, `Invoke-RestMethod`,
and `Invoke-WebRequest`, so they do not access the network or modify a real
repository.

The platform checks only read the checked-in YAML templates and require
neither a .NET SDK nor a YAML parsing module. Run them alone without a test
directory:

```powershell
$configuration = New-PesterConfiguration
$configuration.Run.Path = './eng/pipelines/scripts/tests/Pipeline-Platforms.Tests.ps1'
$configuration.TestDrive.Enabled = $false
$configuration.Run.Exit = $true
Invoke-Pester -Configuration $configuration
```

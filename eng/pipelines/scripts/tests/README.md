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

`tar`, `Invoke-RestMethod` and `Invoke-WebRequest` are mocked, so the
tests never touch the network or a real repository.

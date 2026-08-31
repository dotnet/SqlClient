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

`git` and `Invoke-RestMethod` are mocked, so the tests never touch the network
or a real repository.

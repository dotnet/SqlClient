# Pipeline Script Tests

Pester tests for the PowerShell helpers under `eng/pipelines/scripts/`.

## Prerequisites

Restore the pinned PowerShell tool and module versions from the repository root:

```console
dotnet tool restore
dotnet build build.proj -t:RestorePowerShellModules --no-restore
```

The target restores `powershell-modules.json` from the anonymous public SqlClient feed in
`PowerShell.config`; see [module setup](../../../../BUILDGUIDE.md#powershell-modules).

## Running the tests

Run from the repository root:

```console
dotnet tool run pwsh -- -NoProfile
```

In that PowerShell session:

```powershell
Import-Module Pester
Invoke-Pester ./eng/pipelines/scripts/tests/
```

Add `-Output Detailed` to see per-test results.

## Test files

| File | Covers |
| ---- | ------ |
| `Build-Orchestration.Tests.ps1` | Explicit root/standalone PowerShell module restore, failure propagation, PR path filters, and pipeline prerequisite ordering. |
| `Open-LocalizationPr.Tests.ps1` | `Open-LocalizationPr.ps1` — de-duplication of the scheduled localization pull request. |
| `Install-DockerCli.macos.Tests.ps1` | `Install-DockerCli.macos.ps1` — Homebrew bottle selection for the macOS docker CLI. |

`git`, `tar`, `Invoke-RestMethod` and `Invoke-WebRequest` are mocked, so the
tests never touch the network or a real repository.

# Pipeline Script Tests

Pester tests for the PowerShell helpers under `eng/pipelines/scripts/`.

## Prerequisites

These tests require **PowerShell 7.4 or later**. Restore the pinned modules from
the repository root:

```console
dotnet build build.proj -t:RestorePowerShellModules
```

The target restores `powershell-modules.json` from the anonymous public SqlClient feed in
`PowerShell.config`; see [module setup](../../../../BUILDGUIDE.md#powershell-modules).

The Docker CLI tests require an x64 host; they are skipped on ARM64.

## Running the tests

```powershell
Import-Module Pester
Invoke-Pester ./eng/pipelines/scripts/tests/
```

Add `-Output Detailed` to see per-test results.

## Test files

| File | Covers |
| ---- | ------ |
| `Build-Orchestration.Tests.ps1` | Explicit root/standalone PowerShell restore and release pipeline wiring. |
| `Install-DockerCli.macos.Tests.ps1` | `Install-DockerCli.macos.ps1` — Homebrew bottle selection for the macOS docker CLI. |

`tar`, `Invoke-RestMethod` and `Invoke-WebRequest` are mocked, so the
tests never touch the network or a real repository.

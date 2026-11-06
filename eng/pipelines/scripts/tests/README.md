# Pipeline Script Tests

Pester tests for pipeline helpers and build orchestration.

## Prerequisites

Restore the pinned Pester module from the governed feed using the
[PowerShell module setup](../../../../BUILDGUIDE.md#powershell-modules):

```powershell
dotnet build build.proj -t:RestorePowerShellModules --no-restore
```

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
| `Install-DockerCli.macos.Tests.ps1` | `Install-DockerCli.macos.ps1` — Homebrew bottle selection for the macOS docker CLI. |
| `Build-Orchestration.Tests.ps1` | Standalone/root module restore targets, failure propagation, and release pipeline wiring. |

`tar`, `Invoke-RestMethod` and `Invoke-WebRequest` are mocked, so the
Docker tests never touch the network or a real repository. The module orchestration tests
require this release's .NET SDK and substitute a local `pwsh` stub; they do not install modules.

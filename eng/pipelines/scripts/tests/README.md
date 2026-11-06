# Pipeline Script Tests

Pester tests for pipeline helpers and build orchestration.

## Prerequisites

Restore the pinned PowerShell tool and Pester module from the governed feed.
The Docker bottle tests run only in an Intel (x64) process.

Run from the repository root:

```console
dotnet tool restore
dotnet build build.proj -t:RestorePowerShellModules --no-restore
```

See [PowerShell module setup](../../../../BUILDGUIDE.md#powershell-modules) for feed details.

## Running the tests

```powershell
dotnet tool run pwsh -- -NoProfile -Command '
    Import-Module Pester
    Invoke-Pester ./eng/pipelines/scripts/tests/ -CI
'
```

Add `-Output Detailed` to see per-test results.

## Test files

| File | Covers |
| ---- | ------ |
| `Build-Orchestration.Tests.ps1` | Explicit module restore, tool exit propagation, and release-branch pipeline wiring. |
| `Install-DockerCli.macos.Tests.ps1` | `Install-DockerCli.macos.ps1` — Homebrew bottle selection for the macOS docker CLI. |
| `Kerberos-Configuration.Tests.ps1` | Executes Windows and Linux config generation with placeholders; checks Kerberos markers, SNI selection, connection strings, and unchanged defaults. |

`tar`, `Invoke-RestMethod` and `Invoke-WebRequest` are mocked, and module restore
uses a stub `dotnet` command, so the tests never install modules or access the network.
Kerberos configuration tests use temporary files and placeholder environment variables only,
restore the environment afterward, and require no domain credentials or SQL Server.

# Pipeline Script Tests

Pester tests for pipeline helpers, argument templates, and build orchestration.

## Prerequisites

These tests require **PowerShell 7+** and **Pester v5 or later**:

```powershell
Install-Module Pester -MinimumVersion 5.0 -Scope CurrentUser -Force -SkipPublisherCheck
```

`Build-Orchestration.Tests.ps1` also requires the .NET SDK pinned by
[`global.json`](../../../../global.json), available as `dotnet` on `PATH`.

## Running the tests

Run these commands from the repository root:

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
| `Pipeline-Arguments.Tests.ps1` | PR build/pack/test argument quoting and the repository's CI variable naming convention. |
| `Build-Orchestration.Tests.ps1` | MSBuild test filters, dependency-pack ordering, and generated command-line argument quoting. |

The localization and Docker helper tests mock `git`, `tar`, `Invoke-RestMethod`,
and `Invoke-WebRequest`, so they do not access the network or modify a real
repository. The build-orchestration tests run real MSBuild evaluation and targets
with a recording child CLI stub; they do not restore, build, or pack the driver.

The pipeline-argument checks only read the checked-in YAML templates and require
neither a .NET SDK nor a YAML parsing module. Run them alone without a test
directory:

```powershell
$configuration = New-PesterConfiguration
$configuration.Run.Path = './eng/pipelines/scripts/tests/Pipeline-Arguments.Tests.ps1'
$configuration.TestDrive.Enabled = $false
$configuration.Run.Exit = $true
Invoke-Pester -Configuration $configuration
```

`DotNetCoreCLI@2` argument strings are not shell scripts: use double quotes to
group property values, particularly paths containing spaces. Single quotes are
passed literally into MSBuild values. Follow the repository's
[pipeline variable naming convention](../../../../.github/instructions/ado-pipelines.instructions.md#variable-naming--avoid-commandarguments-names):
use `dotnetBuildOpts` rather than `buildArguments`, `testArguments`, or `runArguments`.

# OneBranch Pipeline Tests

This directory contains [Pester](https://pester.dev/) tests for the
PowerShell scripts used by the OneBranch build and validation pipelines.

## Prerequisites

| Tool | Version | Install |
| ---- | ------- | ------- |
| PowerShell (pwsh) | 7.2+ | [Install PowerShell](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) |
| Pester | 5.x | `Install-Module Pester -Force -Scope CurrentUser -SkipPublisherCheck` |

## Running tests

From the repository root:

```powershell
pwsh -c "Invoke-Pester ./eng/pipelines/onebranch/tests/ -Output Detailed"
```

Or run a single test file:

```powershell
pwsh -c "Invoke-Pester ./eng/pipelines/onebranch/tests/validate-symbols.Tests.ps1 -Output Detailed"
```

## Writing tests

### File naming

Test files must follow Pester's naming convention:

```text
<ScriptUnderTest>.Tests.ps1
```

For example, tests for `jobs/validate-symbols.ps1` live in
`tests/validate-symbols.Tests.ps1`.

### Locating the script under test

Since scripts live in sibling directories (`jobs/`, `steps/`, etc.),
reference them relative to `$PSScriptRoot`:

```powershell
BeforeAll {
    $Script:ScriptPath = Join-Path $PSScriptRoot '..' 'jobs' 'my-script.ps1'
}
```

### Testing scripts that use `exit`

Pipeline scripts typically use `exit` for control flow, which terminates
the PowerShell host. To test exit codes, run the script as a **child
process** via `Start-Process`:

```powershell
$proc = Start-Process -FilePath 'pwsh' `
    -ArgumentList @('-NoProfile', '-NonInteractive', '-File', $scriptPath, <args...>) `
    -NoNewWindow -Wait -PassThru `
    -RedirectStandardOutput $stdoutFile `
    -RedirectStandardError  $stderrFile

$proc.ExitCode | Should -Be 0
```

### Mocking external tools

When a script calls an external tool (e.g. `symchk.exe`), create a
patched copy of the script that replaces the tool path with a mock `.ps1`
script. See `validate-symbols.Tests.ps1` for an example of this pattern.

## Test inventory

| Test file | Script under test | What it covers |
| --------- | ----------------- | -------------- |
| `validate-symbols.Tests.ps1` | `jobs/validate-symbols.ps1` | Syntax validation, package discovery/extraction, symchk detection, retry logic |

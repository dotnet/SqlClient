# OneBranch Script Tests

This directory contains [Pester](https://pester.dev/) tests for PowerShell
scripts used by the OneBranch pipelines.

## Prerequisites

| Tool | Version | Install |
| ---- | ------- | ------- |
| PowerShell (pwsh) | 7.2+ | [Install PowerShell](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) |
| Pester | 5.x | `Install-Module Pester -Force -Scope CurrentUser -SkipPublisherCheck` |

## Running tests

From the repository root:

```powershell
pwsh -c "Invoke-Pester ./eng/pipelines/onebranch/scripts/tests/ -Output Detailed"
```

Run a single test file:

```powershell
pwsh -c "Invoke-Pester ./eng/pipelines/onebranch/scripts/tests/publish-symbols.Tests.ps1 -Output Detailed"
pwsh -c "Invoke-Pester ./eng/pipelines/onebranch/scripts/tests/validate-symbols.Tests.ps1 -Output Detailed"
```

## Writing tests

### File naming

Test files must follow Pester naming conventions:

```text
<ScriptUnderTest>.Tests.ps1
```

### Locating the script under test

When scripts and tests are siblings under `scripts/` and `scripts/tests/`,
reference scripts relative to `$PSScriptRoot`:

```powershell
BeforeAll {
	$Script:ScriptPath = Join-Path $PSScriptRoot '..' 'my-script.ps1'
}
```

### Testing scripts that use `exit`

Pipeline scripts commonly use `exit` for control flow. To validate exit codes,
run scripts as child processes with `Start-Process`:

```powershell
$proc = Start-Process -FilePath 'pwsh' `
	-ArgumentList @('-NoProfile', '-NonInteractive', '-File', $scriptPath, <args...>) `
	-NoNewWindow -Wait -PassThru `
	-RedirectStandardOutput $stdoutFile `
	-RedirectStandardError  $stderrFile

$proc.ExitCode | Should -Be 0
```

### Mocking external tools

When a script calls external tools (for example `symchk.exe`, `az`, or
`Invoke-RestMethod`), mock those calls in tests. See
`validate-symbols.Tests.ps1` and `publish-symbols.Tests.ps1`.

## Test inventory

| Test file | Script under test | What it covers |
| --------- | ----------------- | -------------- |
| `publish-symbols.Tests.ps1` | `scripts/publish-symbols.ps1` | Parameter validation, URL construction, request bodies, status validation, error handling |
| `validate-symbols.Tests.ps1` | `scripts/validate-symbols.ps1` | Syntax validation, package discovery/extraction, symchk detection, retry logic |

## Notes

- Tests for `publish-symbols.ps1` mock all external calls (`az`, `Invoke-RestMethod`), so no network access or Azure credentials are required.

# Publish-Symbols Tests

Pester tests for the `Publish-Symbols.ps1` script used by the symbol publishing pipeline step.

## Prerequisites

- PowerShell 5.1+ or PowerShell 7+
- [Pester v5](https://pester.dev/) (`Install-Module Pester -MinimumVersion 5.0 -Scope CurrentUser`)

## Running the Tests

From this directory:

```powershell
Invoke-Pester ./Publish-Symbols.Tests.ps1
```

Or from the repository root:

```powershell
Invoke-Pester ./eng/pipelines/common/templates/steps/tests/
```

For detailed output:

```powershell
Invoke-Pester ./Publish-Symbols.Tests.ps1 -Output Detailed
```

## Test Coverage

| Area                 | What's tested                                                    |
|----------------------|------------------------------------------------------------------|
| Parameter validation | Empty strings rejected for all mandatory parameters              |
| URL construction     | Base URL, register URL, request URL built from parameters        |
| Request bodies       | Registration body, default publish flags, flag overrides         |
| Error handling       | Token failure, empty token, registration/publish/status failures |

## Notes

- All external calls (`az`, `Invoke-RestMethod`) are mocked — no network access or Azure credentials are required.
- Tests validate the script at `../Publish-Symbols.ps1` relative to this directory.

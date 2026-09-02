# OneBranch PowerShell Tests

Pester tests for PowerShell scripts used by OneBranch pipeline steps.

## Prerequisites

- PowerShell 5.1+ or PowerShell 7+
- [Pester v5](https://pester.dev/) (`Install-Module Pester -MinimumVersion 5.0 -Scope CurrentUser`)

## Running the Tests

From this directory:

```powershell
Invoke-Pester ./publish-symbols.Tests.ps1
```

Or from the repository root:

```powershell
Invoke-Pester ./eng/pipelines/onebranch/scripts/tests/
```

For detailed output:

```powershell
Invoke-Pester ./publish-symbols.Tests.ps1 -Output Detailed
```

## Test Coverage

| Area                  | What's tested                                                    |
| --------------------- | ---------------------------------------------------------------- |
| Version computation   | Canonical output parsing, revisions, wrapping, effective package selection, and failures |
| Localization validation | Missing or empty strings, English-value matches, and culture-specific allowlisting |
| Parameter validation  | Empty strings rejected for all mandatory parameters              |
| URL construction      | Base URL, register URL, request URL built from parameters        |
| Request bodies        | Registration body, default publish flags, flag overrides         |
| Error handling        | Token failure, registration failure, publish failure, status failure — all verify expanded URI in error message |
| Status validation     | Detects Failed/Cancelled results, respects PublishToInternal/PublishToPublic flags, passes on Succeeded/Pending |

## Notes

- All external calls (`az`, `Invoke-RestMethod`) are mocked — no network access or Azure credentials are required.
- Version tests mock `dotnet`, so they do not invoke MSBuild or require a restored repository.
- Tests validate scripts in the parent directory relative to this directory.

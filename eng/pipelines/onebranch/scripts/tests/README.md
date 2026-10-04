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
| Version computation   | Canonical output parsing, effective package selection, target version composition, and failures |
| Localization validation | Missing, obsolete, or empty strings, English-value matches, and culture-specific allowlisting |
| Parameter validation  | Empty strings rejected for all mandatory parameters              |
| URL construction      | Base URL, register URL, request URL built from parameters        |
| Request bodies        | Registration body, default publish flags, flag overrides         |
| Error handling        | Token failure, registration failure, publish failure, status failure — all verify expanded URI in error message |
| Status validation     | Detects Failed/Cancelled results, respects PublishToInternal/PublishToPublic flags, passes on Succeeded/Pending |
| Package validation    | Wildcard vs per-id version expectations, SqlServer omitted when unbuilt, gate tokens, report written before gating, exit-code handling |
| Package signatures    | Every package and symbol package verified, all failures reported before throwing |
| Assembly signatures   | Package expansion, native binaries under `runtimes/` included, stale expansions replaced, all unsigned assemblies reported |

## Notes

- All external calls (`az`, `Invoke-RestMethod`) are mocked — no network access or Azure credentials are required.
- Script-level version tests mock `dotnet`; package-composition tests invoke the real MSBuild
  `GetVersionsSqlClient` and `GetVersionsSqlServer` targets.
- `Get-AuthenticodeSignature` is Windows-only, so the assembly-signature tests declare a stub when
  it is absent. Only the signature lookup is substituted; package expansion and reporting run for
  real against packages built in the test's temporary directory.
- Tests validate scripts in the parent directory relative to this directory.

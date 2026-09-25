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
| Localization validation | Missing, obsolete, or empty strings, English-value matches, culture-specific allowlisting, and report-only mode |
| Parameter validation  | Empty strings rejected for all mandatory parameters              |
| URL construction      | Base URL, register URL, request URL built from parameters        |
| Request bodies        | Registration body, default publish flags, flag overrides         |
| Error handling        | Token failure, registration failure, publish failure, status failure — all verify expanded URI in error message |
| Status validation     | Detects Failed/Cancelled results, respects PublishToInternal/PublishToPublic flags, passes on Succeeded/Pending |
| Package validation    | Wildcard vs per-id version expectations, SqlServer omitted when unbuilt, gate tokens, report written before gating, exit-code handling, report-only mode suppressing the gate but not a broken validator |
| XML docs validation   | Every documentation-ID defect form reported by the API Docs build (array `T:` UIDs, empty parentheses, C# aliases, embedded whitespace, misspelled namespace roots) plus valid controls, compiler-unresolved `!:` crefs, local UID and wrong-prefix resolution, package expansion, allowlisting and staleness, report-only mode |
| XML docs lib/ref layout | Full `lib/` XML paired with trimmed `ref/` XML accepted; trimmed `lib/`, untrimmed `ref/`, and byte-identical `lib`/`ref` rejected; per-target-framework isolation; packages without a `ref/` folder ignored |
| XML docs dependencies | References into a package that was not built this run resolve against the published dependency's documentation; that documentation is not itself validated, does not establish the public API surface, and does not enable resolution when nothing is under validation |
| XML docs enum remarks | `<remarks>` on an enum field reported, since the documentation build discards it; type blocks and platform-variant type blocks accepted; members of non-enum types accepted; enum members read from source through attributes, initializers and comments; in generated documentation only public fields reported, and none when the public API surface is unknown |
| XML docs unresolved includes | An `<include>` surviving into generated documentation reported, since the compiler leaves it in place and the member ships undocumented; the requested path echoed back; every occurrence reported; includes inside snippets ignored |
| XML docs unexpected elements | Member containers emitted by an over-broad `<include>` reported, including an end-to-end compiler expansion; compiler-supported top-level documentation elements accepted |
| XML docs pipeline invocation | Source validation enabled by each job's configured snippet path; SqlClient, SqlServer, Abstractions, and Azure snippet directories wired and present; generated SqlClient reference documentation validated after build and before packing |
| Dependency documentation restore | Exact version pinned, central package management not inherited, documentation collected per target framework, restore tree removed, stale destination replaced, and failures reported for a failed restore, a missing package folder, or a package shipping no documentation |
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
- Validation scripts accept `-ReportOnly`, which downgrades findings to warnings. The pipelines
  drive it from the `failOnValidationError` parameter (inverted). Report-only suppresses
  *findings* only: malformed or missing inputs, and a validator that fails to run, still fail the
  step, because neither produced findings worth reporting.

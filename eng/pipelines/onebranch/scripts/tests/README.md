# OneBranch PowerShell Tests

Pester tests for PowerShell scripts used by OneBranch pipeline steps.

## Prerequisites

- PowerShell 7+, as restored by `dotnet tool restore` from `dotnet-tools.json`. Windows
  PowerShell 5.1 is not supported: these scripts and tests use types that exist only in
  PowerShell Core, such as `Microsoft.PowerShell.Commands.HttpResponseException`.
- [Pester](https://pester.dev/), pinned in the root `powershell-modules.json`.

From the repository root, restore tools and modules using anonymous feed access:

Run `dotnet tool restore` first to install the pinned `pwsh` tool, including on Linux;
the module-restore target uses that tool, so no globally installed PowerShell is required.

```console
dotnet tool restore
dotnet build build.proj -t:RestorePowerShellModules
```

## Running the Tests

Start a PowerShell session with the restored tool:

```console
dotnet tool run pwsh -NoProfile
```

Import Pester once in that session before running any of the test commands below:

```powershell
Import-Module Pester
```

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
| Skip polling          | Both publishing targets submitted without a status query, unverified-results warning, explicit false retains status checks, token/registration/submission errors still fail |
| Package validation    | Wildcard vs per-id version expectations, SqlServer omitted when unbuilt, gate tokens, report written before gating, exit-code handling, report-only mode suppressing the gate but not a broken validator |
| XML docs pipeline invocation | Source validation enabled by each job's configured snippet path; SqlClient, SqlServer, Abstractions, and Azure snippet directories wired and present; generated SqlClient reference documentation validated after build and before packing |
| Dependency documentation restore | Exact version pinned, central package management not inherited, documentation collected per target framework, restore tree removed, stale destination replaced, and failures reported for a failed restore, a missing package folder, or a package shipping no documentation |
| Package signatures    | Every package and symbol package verified, all failures reported before throwing |
| Assembly signatures   | Package expansion, native binaries under `runtimes/` included, stale expansions replaced, all unsigned assemblies reported |
| Token diagnostics     | Minimal troubleshooting claims, malformed payload handling, gated off by default, header/signature segments never logged |
| Command logging       | Commands logged only under `-VerboseDiagnostics`, `Authorization` redacted unconditionally either way |
| HTTP failure detail   | Status code and correlation ids captured, exception and inner-exception types reported, explicit when no response was received |
| Endpoint reachability | Comma-separated and array host lists, DNS failure distinguished from connection failure, socket error code and errno reported, never throws |

## Notes

- Shared XML documentation validator and task-invocation tests have moved to
  [`common/scripts/tests/`](../../../common/scripts/tests/). Run them separately from the root:
  `Invoke-Pester ./eng/pipelines/common/scripts/tests/`.
- All external calls (`az`, `Invoke-RestMethod`) are mocked — no network access or Azure credentials are required.
- The endpoint-reachability tests are the one exception: they exercise real sockets, but only
  against loopback and the RFC 2606 reserved `.invalid` TLD, so they need no external network.
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

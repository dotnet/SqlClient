# OneBranch / Guardian bug report — RoslynAnalyzers@3 Copy Logs Only

> Handoff note for filing with the OneBranch / Secure Development Tools team.
> Related pipeline step: `roslyn-analyzers-buildproj-step.yml` (in this directory).

## Title

RoslynAnalyzers@3 (Copy Logs Only): the Roslyn sanitizer only reads SARIF **v1**, so a SARIF 2.1.0
log throws `SanitizeRoslynAnalyzersScanLogFileException` with no actionable message, and the failed
sanitize then cascades into a Guardian Break crash
(`antimalware ... missing expected property ToolName`).

## Environment

- **Task:** `RoslynAnalyzers@3` v3.289.0, `copyLogsOnly: true`, `logRootDirectory: <repo>`
- **Guardian CLI:** `Microsoft.Guardian.Cli.win-x64` 0.287.0
- **Roslyn analyzer CLI:** `Microsoft.Security.CodeAnalysis.RoslynAnalyzers.Cli.win-x64` 1.23.0
  (`Microsoft.Guardian.RoslynAnalyzers.dll`)
- **Image:** `onebranch.azurecr.io/windows/ltsc2025/vse2026` (Windows Server 2025, .NET SDK MSBuild 18)
- **SARIF producer:** the .NET SDK C# compiler via `ErrorLog=<proj>.<guid>.sarif,version=2`
  (SARIF **2.1.0**, `tool.driver.name = "Microsoft (R) Visual C# Compiler"`)
- Repro builds (SqlClientDrivers/ADO.Net): 26203.1 (162341), 26203.2 (162361)

## Summary

We integrate the analyzers into our own build and use the task's documented **Copy Logs Only**
flow to collect the per-project SARIF. The task copies the SARIF successfully but then **fails to
sanitize every file**:

```text
1 scan logs have been copied from C:\__w\1\s to C:\__w\1\.gdn\.r\roslynanalyzers\001\Logs.
Start sanitizing Roslyn analysis results ...
[Warning] SanitizeRoslynAnalyzersScanLogFileException: Failed to sanitize scan log file:
  C:\__w\1\.gdn\.r\roslynanalyzers\001\Logs\...\Logging.csproj.<guid>.sarif
RoslynAnalyzers completed with exit code 0
```

Because sanitize fails, the results are never ingested, and downstream
**`Guardian: Post Analysis` (pipeline-break) then crashes** and fails the build:

```text
##[error]GuardianException: Result ... ruleId = NoThreatsFound,
  toolInfoId = antimalware>>0>>2026072212xx was determined to be missing expected property ToolName.
##[error]Error: Guardian exited with an error exit code: 1
```

## Root cause (Bug 1 — the sanitizer only supports SARIF v1)

Decompiling `Microsoft.Guardian.RoslynAnalyzers.dll`, the sanitizer reads each scan log with the
**SARIF v1** object model and throws on anything else:

```csharp
internal virtual void SanitizeScanLogFile(string scanLogFilePath, ...) {
    try {
        // Reads the log as SARIF v1 (Microsoft.CodeAnalysis.Sarif.VersionOne):
        SarifLogVersionOne sarifLogVersionOne = Serializer.Read<SarifLogVersionOne>(scanLogFilePath);
        ...  // filters results/rules by ruleset + compiler-warning regex
    }
    catch (Exception innerException) {
        throw new SanitizeRoslynAnalyzersScanLogFileException(scanLogFilePath, innerException);
    }
}
```

The .NET SDK C# compiler, when `ErrorLog` includes `,version=2`, emits **SARIF 2.1.0**.
`Serializer.Read<SarifLogVersionOne>` cannot deserialize a 2.1.0 document into the v1 model, so it
throws, and the catch wraps it as `SanitizeRoslynAnalyzersScanLogFileException`. The failure is at
the deserialize call -- path form, size, and structure are all irrelevant (confirmed by run 26203.3,
where the SARIF paths were pre-relativized and valid JSON: it still threw).

Note: this `SanitizeScanLogFile` method does **not** relativize paths (it only filters results/rules);
the `SetRelativePath`/`SourcesDirectory` members live elsewhere.

### The Roslyn converter is on SARIF v1, while the Guardian core and the tools we observed are on v2

This is not a Guardian-wide requirement. Within the RoslynAnalyzers CLI package (which bundles the
Guardian core plus the Roslyn converter), only `Microsoft.Guardian.RoslynAnalyzers.dll` uses the
SARIF **v1** (`SarifLogVersionOne`) model; the Guardian **core** uses **v2** (`SarifLog`). And in the
same run, BinSkim / AntiMalware / APIScan emit SARIF 2.1.0 and Guardian ingests them without a
sanitize failure -- only Roslyn fails:

| Guardian assembly | `SarifLogVersionOne` (v1) refs | `SarifLog` (v2) refs |
| --- | --- | --- |
| `Microsoft.Guardian.dll` (core) | 0 | 8 |
| `Microsoft.Guardian.LogConversion.dll` | 0 | 3 |
| `Microsoft.Guardian.Annotations.Sdk.dll` | 0 | 12 |
| `Microsoft.Guardian.Autofac.dll` | 0 | 5 |
| **`Microsoft.Guardian.RoslynAnalyzers.dll`** | **1** | **0** |

| Tool log in the same run | SARIF version | Ingested OK? |
| --- | --- | --- |
| `binskim.sarif` | 2.1.0 | yes |
| `antimalware.sarif` | 2.1.0 | yes |
| apiscan `Result.sarif` | 2.1.0 | yes |
| roslyn `*.csproj.*.sarif` (ours, v2.1.0) | 2.1.0 | **no (sanitize threw)** |

**Scope caveat:** the per-assembly counts above cover only the assemblies shipped in the
RoslynAnalyzers CLI package (the Guardian core plus the Roslyn converter). Each other tool's converter
ships in its own package, which we did not inspect -- so we cannot claim Roslyn is the *only* Guardian
tool on v1, only that it is demonstrably on v1 while the Guardian core and the three tools observed
above (BinSkim / AntiMalware / APIScan) are on 2.1.0.

- **Expected:** the sanitizer accepts SARIF 2.1.0 (the current OASIS standard and the SDK's modern
  default), or at least emits an actionable error naming the version mismatch.
- **Actual:** it silently requires SARIF **v1** (deprecated); any 2.1.0 log throws a generic wrapped
  exception that never mentions the version.

## Root cause (Bug 2 — Guardian Break, likely downstream of Bug 1)

`pipeline-break` (Tool Filters: armory, credscan, fxcop, roslynanalyzers, prefast, semmle, tslint;
Min Severity: Error) throws a `GuardianException` while iterating results, on an **AntiMalware**
`NoThreatsFound` result that is "missing expected property `ToolName`" (the antimalware
`tool.driver.fullName` is `null`). This is deterministic (identical signature `d58588c4...` across
jobs/runs).

**Correlation:** runs where the Roslyn task collected **0** SARIF pass Guardian Break; every run
that **produces** Roslyn SARIF (and hits the sanitize failure above) then fails Break on this
antimalware result. This strongly suggests the failed sanitize leaves the assorted results DB in a
state where the antimalware result loses its `ToolName`.

## Fix on our side

Produce **SARIF v1**: set `ErrorLog` to `$(MSBuildProjectFullPath).<guid>.sarif` with **no**
`,version=2` suffix (the compiler's default ErrorLog format is SARIF v1). That is what the
sanitizer's `Serializer.Read<SarifLogVersionOne>` expects and what the SDL Copy-Logs-Only docs use.
See the `ErrorLog` property in `src/Directory.Build.props`. (Our earlier `,version=2` was the
trigger; removing it is the fix.)

## Ask (enhancement for the SDL tooling team)

1. Support **SARIF 2.1.0** in the Roslyn sanitizer (it is the OASIS standard and the .NET SDK's
   modern default) -- the Guardian core (`Microsoft.Guardian.dll`) and the other tools observed in
   our runs (BinSkim / AntiMalware / APIScan) already handle the v2 `SarifLog` model, so the Roslyn
   converter is out of step with them. And/or emit an actionable error that names the version
   mismatch instead of a generic `SanitizeRoslynAnalyzersScanLogFileException`.
2. Document, in the Copy-Logs-Only guidance, that `ErrorLog` must produce SARIF **v1** (no
   `,version=2`); otherwise the sanitize silently fails.
3. Investigate the `pipeline-break` `GuardianException` on the AntiMalware `NoThreatsFound` result
   missing `ToolName`, which surfaced whenever a Roslyn scan log failed to sanitize. It is likely
   downstream of #1/#2 (a failed sanitize corrupts the assorted results), but the crash message is
   misleading.

Exact SARIF sample and full task logs are available from builds 162361 / 162383.

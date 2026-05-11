# ApiCompat Ref Assembly Validation Target

## Goal

Add a build target that compares locally-built ref assemblies against those published in a specified NuGet package version of Microsoft.Data.SqlClient. This detects any unintended API surface changes introduced during the ref project consolidation (PR #3963).

The comparison uses `Microsoft.DotNet.ApiCompat.Tool` in **strict mode**, which flags both additions and removals.

## Usage

```
dotnet msbuild build.proj /t:CompareRefAssemblies /p:BaselinePackageVersion=6.1.4
```

- `BaselinePackageVersion` is **required** (no default). The user must specify which published package to compare against.
- Both the **legacy** ref projects (`netcore/ref/`, `netfx/ref/`) and the **new consolidated** ref project (`ref/`) are built and compared independently, so you can distinguish whether a difference comes from source consolidation vs. project restructuring.
- All comparisons run to completion (errors don't short-circuit), so you see all differences at once.

## Implementation Steps

### 1. Register `Microsoft.DotNet.ApiCompat.Tool` in `dotnet-tools.json`

Add an entry for version `9.0.200` alongside the existing `dotnet-coverage` entry. This enables `dotnet apicompat` after `dotnet tool restore`.

### 2. Create `tools/targets/CompareRefAssemblies.targets`

A single new file containing all properties, items, and targets (steps 3–11 below). Follows the naming convention of existing files like `GenerateMdsPackage.targets`.

### 3. Define properties

| Property | Value | Notes |
|----------|-------|-------|
| `BaselinePackageVersion` | *(none)* | Required; validated by guard target |
| `BaselinePackageDir` | `$(Artifacts)apicompat\` | Working directory for downloads |
| `BaselineNupkgPath` | `$(BaselinePackageDir)microsoft.data.sqlclient.$(BaselinePackageVersion).nupkg` | Downloaded nupkg path |
| `BaselinePackageUrl` | `https://api.nuget.org/v3-flatcontainer/microsoft.data.sqlclient/$(BaselinePackageVersion)/microsoft.data.sqlclient.$(BaselinePackageVersion).nupkg` | NuGet flat container URL |
| `BaselineExtractDir` | `$(BaselinePackageDir)extracted\$(BaselinePackageVersion)\` | Extraction destination |
| `NewRefProjectPath` | `$(ManagedSourceCode)ref\Microsoft.Data.SqlClient.csproj` | New consolidated ref project |
| `NewRefOutputDir` | `$(ManagedSourceCode)ref\bin\$(Configuration)\` | SDK default output for new ref project |
| `LegacyNetFxRefDir` | `$(Artifacts)Project\bin\Windows_NT\$(Configuration)\Microsoft.Data.SqlClient\ref\` | Legacy netfx ref output |
| `LegacyNetCoreRefDir` | `$(Artifacts)Project\bin\AnyOS\$(Configuration)\Microsoft.Data.SqlClient\ref\` | Legacy netcore ref output |

### 4. `_ValidateBaselineVersion` target

Emits `<Error>` if `$(BaselinePackageVersion)` is empty, with a message instructing the user to pass `/p:BaselinePackageVersion=X.Y.Z`.

### 5. `_DownloadBaselinePackage` target

- Depends on `_ValidateBaselineVersion`
- Uses `<DownloadFile>` to fetch the nupkg from nuget.org
- Uses `<Unzip>` to extract it into `$(BaselineExtractDir)`
- Conditioned to skip if `$(BaselineExtractDir)` already exists

### 6. `_RestoreApiCompatTool` target

Runs `<Exec Command="dotnet tool restore" WorkingDirectory="$(RepoRoot)" />`.

### 7. `_BuildLegacyRefNetFx` target

- Depends on `RestoreNetFx`
- Builds `$(NetFxSource)ref\Microsoft.Data.SqlClient.csproj` for `net462`
- Conditioned on Windows (`$(IsEnabledWindows)`)
- Output: `$(LegacyNetFxRefDir)net462\Microsoft.Data.SqlClient.dll`

### 8. `_BuildLegacyRefNetCore` target

- Depends on `RestoreNetCore`
- Builds `$(NetCoreSource)ref\Microsoft.Data.SqlClient.csproj` with `/p:OSGroup=AnyOS`
- Output: `$(LegacyNetCoreRefDir){net8.0,net9.0,netstandard2.0}\Microsoft.Data.SqlClient.dll`

### 9. `_BuildNewRefProject` target

- Runs `dotnet build` on `$(NewRefProjectPath)`
- Builds all 4 TFMs (`net462`, `net8.0`, `net9.0`, `netstandard2.0`) in one shot
- Output: `$(NewRefOutputDir){tfm}\Microsoft.Data.SqlClient.dll`

### 10. `_RunRefApiCompat` target

- Depends on `_DownloadBaselinePackage;_RestoreApiCompatTool;_BuildLegacyRefNetFx;_BuildLegacyRefNetCore;_BuildNewRefProject`
- Iterates over 4 TFMs (`net462`, `net8.0`, `net9.0`, `netstandard2.0`) using item batching
- For each TFM, runs two `<Exec>` calls with `ContinueOnError="ErrorAndContinue"`:
  - **Legacy vs baseline**: `dotnet apicompat -l {baseline-ref-dll} -r {legacy-ref-dll} --strict-mode`
  - **New vs baseline**: `dotnet apicompat -l {baseline-ref-dll} -r {new-ref-dll} --strict-mode`
- Each `<Exec>` is conditioned on `Exists(...)` for both DLLs (gracefully skips missing TFMs)
- Preceded by `<Message Importance="high">` labelling each comparison
- Uses item metadata to map `net462` to `$(LegacyNetFxRefDir)` and others to `$(LegacyNetCoreRefDir)`

### 11. `CompareRefAssemblies` target (public entry point)

- Declared with `DependsOnTargets="_RunRefApiCompat"`
- Emits a final `<Message>` summarizing completion

### 12. Import into `build.proj`

Add one line after the existing `.targets` imports (after line 7):

```xml
<Import Project="$(ToolsDir)targets\CompareRefAssemblies.targets" />
```

## Design Decisions

- **Single new file** at `tools/targets/CompareRefAssemblies.targets` — only one `<Import>` line added to `build.proj`.
- **Internal targets prefixed with `_`** to signal they're not intended to be called directly.
- **Strict mode** ensures API additions are also flagged — important for detecting accidental public surface changes during file reorganization.
- **`ContinueOnError="ErrorAndContinue"`** on each apicompat `Exec` so all 8 comparisons run and all differences are reported together.
- **`_BuildLegacyRefNetFx`** is conditioned on Windows (net462 can't build on Unix), matching the existing `BuildNetFx` pattern.
- **No default baseline version** — the user must always explicitly specify which published package to compare against.
- **Download caching** — re-runs skip the download if the extracted directory already exists.

## Verification

```
dotnet msbuild build.proj /t:CompareRefAssemblies /p:BaselinePackageVersion=6.1.4
```

- Downloads 6.1.4 nupkg, builds both ref project variants, runs 8 comparisons (4 TFMs × 2 variants).
- If the ref consolidation introduced no API changes, all comparisons pass (exit code 0).
- If differences are found, apicompat reports them (e.g., `CP0001: Member 'X' exists on left but not on right`).
- To generate a suppression file for known/intended differences, run `dotnet apicompat ... --generate-suppression-file` manually.

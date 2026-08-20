# PackageValidator

A standalone .NET 10 command-line tool that inspects one or more NuGet packages (`.nupkg`) and
validates their **versions, signing, and symbols**.

PackageValidator is a first-class maintainer tool for the SqlClient family of packages. It reads
assemblies purely through **metadata** — it never loads them into the runtime — which makes it:

- **Cross-platform.** It runs on any OS that hosts .NET 10.
- **Framework-agnostic.** It can read assemblies built for target frameworks the host does not run
  (for example, inspecting `net462` binaries from a Linux host).
- **Side-effect free.** No module initializers or assembly-load events are ever
  executed.

It is designed to be run both interactively (to inspect a package) and in CI (to **gate** a build
with `--fail-on` and assert expected versions with `--expect-*`).

## What it checks

For every package it opens, PackageValidator reports and validates:

- **Package identity** — id, version, dependency groups, and NuGet signature, read from the embedded
  `.nuspec`.
- **Per-assembly versions** — `AssemblyVersion`, `FileVersion`, `InformationalVersion`, culture,
  public key token, strong-name signing status, and target framework.
- **Native binaries** — Win32 version-resource information and architecture.
- **Binary classification** — implementation / reference / satellite / native, so symbol coverage is
  judged only over implementation assemblies.
- **Symbols** — sibling `.snupkg` matching by debug GUID with portable-PDB checksum verification,
  embedded-symbol detection, and orphan/mismatch reporting.
- **Intrinsic rules** — severity-tagged findings for missing/mismatched symbols,
  unsigned/delay-signed assemblies, dependency inconsistencies, and more.
- **Expected versions** — optional `--expect-*` assertions for inter-package version-match
  validation.

## Building and running

The tool lives under `tools/PackageValidator` with its own `global.json` and `Directory.*.props`,
isolated from the main driver build.

```bash
# From tools/PackageValidator/src
dotnet run -- <paths>... [options]

# Or build once and invoke the produced binary
dotnet build -c Release
./bin/Release/net10.0/PackageValidator <paths>... [options]
```

It is also wired into the repository's `build.proj`:

- `BuildPackageValidator` / `BuildPackageValidatorTest` — build the tool and tests.
- `TestPackageValidator` — run the test suite.
- `BuildTools` — builds the tools and their test projects.

## Usage

```bash
PackageValidator <paths>... [options]
```

| Argument / option | Description |
| --- | --- |
| `<paths>...` | One or more `.nupkg` files, or directories scanned recursively for `.nupkg` files. |
| `--json` | Emit machine-readable JSON instead of the human-readable layout. |
| `--no-snupkg` | Do not look for or process sibling `.snupkg` symbol packages (embedded symbols are still evaluated). |
| `--fail-on <value>` | Exit non-zero when a finding matches the given value. Repeatable. |
| `--expect-package-version [id=]VALUE` | Confirm the package version equals `VALUE`. Repeatable. |
| `--expect-file-version [id=]VALUE` | Confirm every assembly's file version equals `VALUE`. Repeatable. |
| `--expect-assembly-version [id=]VALUE` | Confirm every assembly's assembly version equals `VALUE`. Repeatable. |

### `--fail-on` values

`--fail-on` accepts a **severity** (`error`, `warning`, `info`), the token `any`,
or one of the following finding **categories**:

- `version-inconsistency`
- `missing-symbols`
- `symbol-mismatch`
- `symbol-checksum-mismatch`
- `symbol-orphan`
- `symbol-duplicate`
- `delay-signed`
- `unsigned`
- `package-unsigned`
- `dependency-inconsistency`
- `unexpected-package-version`
- `unexpected-file-version`
- `unexpected-assembly-version`

### `--expect-*` values

Each `--expect-*` value is either `VALUE` (applies to every package in the run) or `id=VALUE`
(applies only to the package whose id is `id`). A specific id overrides a bare `VALUE` (wildcard),
and because the options repeat, a family-wide expectation can coexist with a per-package override —
for example, assert one family file version while letting `Microsoft.SqlServer.Server` differ.  A
missing version counts as a mismatch, so an assertion never silently passes.

### Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success; no gating findings. |
| `1` | Runtime/inspection error. |
| `2` | A `--fail-on` gate was tripped. |

(Argument-parsing errors return a non-zero `System.CommandLine` code.)

The same reference details are printed in a free-form **Notes** section appended to `--help`.

## Examples

Inspect a single package (human-readable):

```bash
PackageValidator Microsoft.Data.SqlClient.7.1.0-preview1.nupkg
```

Emit JSON for tooling:

```bash
PackageValidator Microsoft.Data.SqlClient.7.1.0-preview1.nupkg --json
```

Gate a directory of packages against an expected family file version, with a per-package override,
and fail on any error:

```bash
PackageValidator <dir-of-nupkgs> \
  --expect-file-version *=7.1.0.17604 \
  --expect-file-version Microsoft.SqlServer.Server=1.1.0.17604 \
  --fail-on error
```

> **Symbol packages from nuget.org:** the flat-container feed only serves `.nupkg` files. To
> exercise the `.snupkg` path, download the symbol package from the gallery's symbol endpoint
> (`https://www.nuget.org/api/v2/symbolpackage/{Id}/{Version}`) and save it beside the `.nupkg` with
> the same base name (for example `microsoft.data.sqlclient.5.2.2.snupkg`).

## Source layout

```text
tools/PackageValidator/
├── global.json                 # Pins the .NET SDK used to build the tool
├── Directory.Build.props       # Shared MSBuild properties for the tool + tests
├── Directory.Packages.props    # Central package versions (CPM)
├── src/
│   ├── PackageValidator.csproj # The console app project (net10.0)
│   ├── Program.cs              # Entry point: CLI parsing, orchestration, exit codes
│   ├── PackageInspector.cs     # Opens a .nupkg and builds a PackageReport
│   ├── AssemblyInspector.cs    # Metadata-only inspection of a single .dll entry
│   ├── NativeVersionReader.cs  # Win32 version-resource + architecture for native DLLs
│   ├── BinaryClassifier.cs     # Maps a DLL path to its role (impl/ref/satellite/native)
│   ├── SymbolResolver.cs       # Cross-checks the sibling .snupkg against assemblies
│   ├── PortablePdb.cs          # Low-level portable-PDB GUID + checksum reading
│   ├── VersionExpectations.cs  # Parses/resolves --expect-* assertions by package id
│   ├── VersionRange.cs         # SemVer 2.0-aware NuGet version-range evaluator
│   ├── Validator.cs            # The intrinsic rules engine + finding categories
│   ├── HumanReporter.cs        # Renders a run to human-readable text
│   ├── Json.cs                 # Source-generated JSON serialization context
│   ├── Models.Common.cs        # Shared enums + value types (BinaryKind, Severity, Finding, …)
│   └── Models.Report.cs        # Report DTOs (ValidationRun, PackageReport, BinaryReport, …)
└── test/
    ├── PackageValidator.Test.csproj
    ├── AssemblyInspectorTests.cs
    ├── BinaryClassifierTests.cs
    ├── ValidatorTests.cs
    ├── VersionExpectationsTests.cs
    └── VersionRangeTests.cs
```

### File responsibilities

| File | Responsibility |
| --- | --- |
| `Program.cs` | Console entry point. Defines the `System.CommandLine` surface (arguments, options, and the `--help` Notes section), enumerates input paths, drives inspection and validation, selects the output renderer, and maps results to process exit codes. |
| `PackageInspector.cs` | Opens a `.nupkg` (a zip archive), reads the `.nuspec` for id/version/dependencies, detects the `.signature.p7s` signature entry, inspects every `.dll` entry, and invokes the symbol resolver — producing a `PackageReport`. |
| `AssemblyInspector.cs` | Inspects a single `.dll` archive entry via `PEReader`/`MetadataReader` without loading it. Extracts assembly name, versions, culture, public key token, strong-name status, target framework, and debug (CodeView) GUID/checksums. Falls back to native reporting for non-managed DLLs. |
| `NativeVersionReader.cs` | Reads Win32 version-resource fields (file/product version, product name) and architecture from native binaries. Stages bytes to a temp file for `FileVersionInfo`, falling back to architecture-only when staging fails. |
| `BinaryClassifier.cs` | Classifies a managed DLL's package path into a `BinaryKind` (implementation under `lib/` or `runtimes/<rid>/lib/`, reference under `ref/`, satellite `*.resources.dll`, or other). |
| `SymbolResolver.cs` | Locates the sibling `.snupkg`, indexes its PDBs by debug GUID and path, matches each assembly (authoritative GUID match with checksum verification, or path-based mismatch detection), and reports orphaned/duplicate symbol files. Loads PDB bytes on demand to keep peak memory low. |
| `PortablePdb.cs` | Low-level portable-PDB helpers: read the debug GUID, detect legacy Windows PDBs, and verify a PDB against an assembly's recorded checksum (locating the `#Pdb` stream and zeroing the PDB id before hashing). |
| `VersionExpectations.cs` | Parses the `--expect-package/file/assembly-version` specs into per-id (and wildcard) expectations and resolves the applicable expected value for a given package, enabling inter-package version-match validation. |
| `VersionRange.cs` | Evaluates whether a concrete version satisfies a NuGet dependency range, following SemVer 2.0 precedence (prerelease ordering; build metadata ignored). Used by dependency-consistency checks. |
| `Validator.cs` | Defines the stable finding `Categories` and applies the intrinsic validation rules, producing per-package and cross-package `Finding` instances (symbols, signing, dependency and expected-version consistency). |
| `HumanReporter.cs` | Renders a `ValidationRun` to standard output in the human-readable layout: per-package details and findings, cross-package findings, and the run summary. |
| `Json.cs` | Source-generated `JsonSerializerContext` for trim/AOT-friendly, indented, camel-cased JSON output (nulls omitted, enums as strings). |
| `Models.Common.cs` | Shared enums and value types used across the tool: `BinaryKind`, `SigningStatus`, `Severity`, `Finding`, `NativeVersionInfo`, `PdbChecksum`, and related value types. |
| `Models.Report.cs` | The report DTO graph serialized to JSON and rendered as text: `ValidationRun`, `ValidationSummary`, `PackageReport`, `BinaryReport`, `SymbolPackageInfo`, and related types. |

## Related public tools

No single public tool combines everything PackageValidator does, but each area it covers has
established, general-purpose analogs. This tool intentionally folds them into one dependency-light,
cross-platform CLI tailored to the SqlClient package family.

| Area | Public tools | Relationship |
| --- | --- | --- |
| Package inspection & health rules | [NuGet Package Explorer](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer), [`dotnet validate`](https://github.com/meziantou/Meziantou.Framework) (Meziantou) | Closest all-round analogs: inspect `.nupkg` metadata, layout, symbols, and run package-hygiene rules. |
| Package/assembly signatures | `dotnet nuget verify`, `signtool`, `sn.exe` | Verify NuGet, Authenticode, and strong-name signing state. PackageValidator reports the same strong-name/NuGet-signature state inline. |
| API surface & compatibility | [`Microsoft.DotNet.ApiCompat`](https://github.com/dotnet/sdk), SDK `PackageValidation` (`<EnablePackageValidation>`), [`PublicApiAnalyzers`](https://github.com/dotnet/roslyn-analyzers) | Catch breaking API/TFM changes across versions — complementary to, not overlapping with, this tool's version checks. |
| Assembly/metadata inspection | [ILSpy / `ilspycmd`](https://github.com/icsharpcode/ILSpy), `dotnet-ildasm`, [`AsmSpy`](https://github.com/mikehadlow/AsmSpy) | Metadata-only inspection (same `System.Reflection.Metadata` approach); AsmSpy specifically reports version conflicts across binaries. |
| Symbols / Source Link | [`sourcelink`](https://github.com/dotnet/sourcelink), `Microsoft.SourceLink.*` | Verify portable-PDB checksums and Source Link — the same portable-PDB checksum verification this tool performs against the sibling `.snupkg`. |

**What makes PackageValidator distinct:**

- Metadata-only reading (like ILSpy) so it can inspect `net462` assemblies from a Linux host, with
  no assembly loading.
- Symbol matching by debug GUID **and** portable-PDB checksum (like sourcelink), including
  orphan/mismatch detection against the sibling `.snupkg`.
- Strong-name and NuGet signature state reported inline (like `sn` / `nuget verify`).
- **Inter-package version-match assertions** (`--expect-*`) — asserting a whole *family* of packages
  carries consistent versions, which the general-purpose tools above do not do out of the box.
- CI gating via `--fail-on` severity/category selectors.

## Tests

The `test/` project is an xUnit v3 suite running on Microsoft.Testing.Platform.  It covers
public-key-token computation, binary classification, SemVer 2.0 range evaluation (including
prerelease ordering and malformed-input rejection), the rules engine, and expected-version
assertions.

```bash
# From tools/PackageValidator/test
dotnet test
```

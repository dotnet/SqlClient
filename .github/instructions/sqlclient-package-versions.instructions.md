---
applyTo: "**/Versions.props,build.proj,eng/pipelines/**/*.yml"
---
# SqlClient Package Version Resolution

How package versions are determined across different build scenarios for the packages in this repository.

## Package families

The repository ships two independently-versioned units:

- **The SqlClient family** — `Microsoft.Data.SqlClient` plus the packages that version in lockstep with
  it: `Microsoft.Data.SqlClient.Internal.Logging`, `Microsoft.Data.SqlClient.Extensions.Abstractions`,
  `Microsoft.Data.SqlClient.Extensions.Azure`, and
  `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider`.  **All family packages use the
  SqlClient version numbers** — the same NuGet package version, file version, and assembly version —
  and are always built and released together.
- **`Microsoft.SqlServer.Server`** — versioned and released on its own cadence.

## Version Properties

The SqlClient family version lives in `src/Microsoft.Data.SqlClient/Versions.props`, which is imported
for every project by `src/Directory.Build.props` (so the `SqlClient*` version properties are always
available).  `Microsoft.SqlServer.Server` declares its own version in its own `Versions.props`.

| Property | Applies to | Purpose | Example |
|----------|-----------|---------|---------|
| `SqlClientNextVersion` | SqlClient family | Version being developed; used for the next release | `7.1.0-preview1` |
| `SqlServerNextVersion` | SqlServer | Version being developed for SqlServer | `1.1.0-preview1` |
| `SqlServerPublishedVersion` | SqlServer | Last SqlServer version shipped to NuGet | `1.0.0` |

The SqlClient family always ships its next version, so there is **no** family `PublishedVersion`.  Only
`Microsoft.SqlServer.Server` keeps a published version (used when it is built as a SqlClient dependency
but not itself released).

## Resolution Logic

Each `Versions.props` uses a 3-tier `<Choose>` block:

| Priority | Condition | PackageVersion | FileVersion |
|----------|-----------|----------------|-------------|
| 1 | `<Pkg>PackageVersion` explicitly provided | Used as-is | Strip prerelease + append BuildNumber |
| 2 | `BuildNumber` provided (non-zero) | `NextVersion[-BuildSuffix+BuildNumber]` | `NextVersion.Split('-')[0].BuildNumber` |
| 3 | Nothing provided | `NextVersion-dev` | `NextVersion.Split('-')[0].0` |

For every family package, `<Pkg>` is `SqlClient` (e.g. `-p:SqlClientPackageVersion=...`); for
Microsoft.SqlServer.Server it is `SqlServer`.

> **Two equivalent names for the same value.** `SqlClientPackageVersion` / `SqlServerPackageVersion`
> are the underlying project properties (read by `Versions.props`, `Directory.Packages.props`, the
> csproj files, and the nuspec). When building through `build.proj` — which the CI and OneBranch
> pipelines always do — pass the wrapper argument `-p:PackageVersionSqlClient=...` /
> `-p:PackageVersionSqlServer=...`; `build.proj` forwards it to the underlying
> `-p:SqlClientPackageVersion=...` / `-p:SqlServerPackageVersion=...`. Both set the same value —
> pipeline logs show `PackageVersion<Pkg>`, while project builds and props show `<Pkg>PackageVersion`.

### Developer (local `dotnet build`)

**Mode:** Project (default `ReferenceType=Project`)

- No `BuildNumber`, no `BuildSuffix`, no `PackageVersion*` passed.
- Falls into Priority 3 (the `<Otherwise>` branch).
- **Result:** `7.1.0-preview1-dev` / FileVersion `7.1.0.0`
- Dependencies are project references — no package versions needed for siblings.

**Mode:** Package (`-p:ReferenceType=Package`)

- Same version resolution for the package being built.
- Sibling dependencies are restored from local `packages/` feed (previously packed with `-dev` suffix).
- A developer would first `dotnet build build.proj -t:Pack` to produce local packages, then consume them.

### PR Pipeline (non-official CI)

**`buildSuffix`** set via core template parameter:
- `buildSuffix: 'pr'` (passed explicitly from PR pipeline)
- `BuildNumber` = `$(DayOfYear)$(Rev:rr)` (e.g. `15401` for day-of-year 154, run 01)

**Mode:** Project (typical PR validation)

- Versions computed in `compute-versions-ci-stage.yml` (runs `GetVersions*` targets with `-p:BuildSuffix=pr -p:BuildNumber=...`)
- Falls into Priority 2 with BuildSuffix present.
- **Result:** `7.1.0-preview1-pr15401` / FileVersion `7.1.0.15401`
- Dependencies are project references — all packages built together in-tree.

**Mode:** Package (PR package-ref validation)

- Same version computation via compute-versions stage.
- Downstream stages define stage-level variables from compute-versions output using `$[ stageDependencies... ]`.
- Each build step passes the family wrapper argument `-p:PackageVersionSqlClient=<value>` (and `-p:PackageVersionSqlServer=` where needed) to `build.proj`, which forwards it to the underlying `-p:SqlClientPackageVersion=` / `-p:SqlServerPackageVersion=` project property, hitting Priority 1.
- Sibling dependencies consumed from pipeline artifacts published by upstream stages.

### CI Pipeline (non-official, triggered on merge)

Same structure as PR but passes `buildSuffix: 'ci'` explicitly.

- **Result:** `7.1.0-preview1-ci15401` / FileVersion `7.1.0.15401`

### OneBranch Pipeline (official)

**Mode:** Always Package (`ReferenceType=Package`)

Uses the full `compute-versions-stage.yml` machinery:

#### Step A: Compute Versions (dedicated early stage)

1. Runs the `GetVersionsSqlClient` and `GetVersionsSqlServer` MSBuild targets against `build.proj`.
2. Each target calls `dotnet build <project> -getProperty:<Pkg>PackageVersion` with `BuildNumber` but **no BuildSuffix**.
3. Falls into Priority 2 without BuildSuffix → `PackageVersion = NextVersion` as-is (e.g. `7.1.0-preview1`).
4. `GetVersionsSqlServer` also extracts `SqlServerPublishedVersion` (the SqlClient family has no published version).

#### Step B: Resolve Effective Versions

- The **SqlClient family** always uses `SqlClientNextVersion`.
- **`Microsoft.SqlServer.Server`** uses its next or published version based on the
  `buildSqlServer` boolean:

| `buildSqlServer` | Effective SqlServer Version | Meaning |
|--------------------------|-----------------------------|---------|
| `True` | `SqlServerNextVersion` (e.g. `1.1.0-preview1`) | SqlServer is being released |
| `False` | `SqlServerPublishedVersion` (e.g. `1.0.0`) | Only built as a SqlClient dependency; use last-shipped |

> **The default differs by pipeline.** The non-official (nightly) pipeline defaults `buildSqlServer:
> true`, exercising the "build SqlServer + local-feed dependency" flow; the official pipeline defaults
> `buildSqlServer: false`, exercising the "depend on the published SqlServer package" flow that
> matches actual release intent. Either can be overridden at queue time. Requesting `releaseSqlServer`
> without `buildSqlServer` fails template expansion up front.

These are published as ADO output variables: `versions.SqlClientPackageVersion`,
`versions.SqlServerPackageVersion`, and their `*FileVersion` counterparts.

#### Step C: Build Stages Consume Pre-computed Versions

Each downstream build job receives:
- `packageVersion` parameter → passed as `-p:SqlClientPackageVersion=<value>` (or `-p:SqlServerPackageVersion=` for SqlServer)
- Dependency versions → family dependencies use the shared `SqlClientPackageVersion`; the SqlServer dependency uses `SqlServerPackageVersion`

Since an explicit `<Pkg>PackageVersion` is provided, Versions.props hits Priority 1 — uses the value verbatim.

#### Summary

| Package | Version Source | Example |
|---------|----------------|---------|
| SqlClient family (always released together) | `SqlClientNextVersion` | `7.1.0-preview1` |
| SqlServer, being released | `SqlServerNextVersion` | `1.1.0-preview1` |
| SqlServer, dependency only | `SqlServerPublishedVersion` | `1.0.0` |

## Key Architectural Difference

| Scenario | Who computes versions | How dependencies get versions |
|----------|----------------------|-------------------------------|
| Developer | Versions.props inline (Priority 3) | Project references (no version needed) |
| PR/CI (Project) | `compute-versions-ci-stage` up-front | Project references (no version needed) |
| PR/CI (Package) | `compute-versions-ci-stage` up-front | Stage variables via `$[ stageDependencies... ]` → `-p:SqlClientPackageVersion=` / `-p:SqlServerPackageVersion=` |
| OneBranch | `compute-versions-stage` up-front | Explicit `-p:SqlClientPackageVersion=` / `-p:SqlServerPackageVersion=` from stage outputs |

## Updating Versions

After releasing the **SqlClient family**:
1. Update `SqlClientNextVersion` in `src/Microsoft.Data.SqlClient/Versions.props` to the next planned
   version.  (There is no family published version to update.)

After releasing **`Microsoft.SqlServer.Server`**:
1. Update `SqlServerPublishedVersion` to the version just shipped.
2. Update `SqlServerNextVersion` to the next planned version.

The SqlServer properties live in `src/Microsoft.SqlServer.Server/Versions.props`.

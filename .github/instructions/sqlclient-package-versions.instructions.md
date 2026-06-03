---
applyTo: "**/Versions.props,build.proj,eng/pipelines/**/*.yml"
---
# SqlClient Package Version Resolution

How package versions are determined across different build scenarios for the packages in this repository.

## Version Properties (per package)

Each package has a `Versions.props` file declaring:

| Property | Purpose | Example |
|----------|---------|---------|
| `*NextVersion` | Version being developed; used for the next release | `7.1.0-preview1` |
| `*PublishedVersion` | Last version shipped to NuGet | `7.0.0` |

## Resolution Logic

Each `Versions.props` uses a 3-tier `<Choose>` block:

| Priority | Condition | PackageVersion | FileVersion |
|----------|-----------|----------------|-------------|
| 1 | `PackageVersion<Pkg>` explicitly provided | Used as-is | Strip prerelease + append BuildNumber |
| 2 | `BuildNumber` provided (non-zero) | `NextVersion[-BuildSuffix+BuildNumber]` | `NextVersion.Split('-')[0].BuildNumber` |
| 3 | Nothing provided | `NextVersion-dev` | `NextVersion.Split('-')[0].0` |

## Scenarios

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
- Each build step receives explicit `-p:PackageVersion<Pkg>=<value>` (hits Priority 1).
- Sibling dependencies consumed from pipeline artifacts published by upstream stages.

### CI Pipeline (non-official, triggered on merge)

Same structure as PR but passes `buildSuffix: 'ci'` explicitly.

- **Result:** `7.1.0-preview1-ci15401` / FileVersion `7.1.0.15401`

### OneBranch Pipeline (official)

**Mode:** Always Package (`ReferenceType=Package`)

Uses the full `compute-versions-stage.yml` machinery:

#### Step A: Compute Versions (dedicated early stage)

1. Runs 6 `GetVersions*` MSBuild targets against `build.proj`.
2. Each target calls `dotnet build <project> -getProperty:<Pkg>PackageVersion` with `BuildNumber` but **no BuildSuffix**.
3. Falls into Priority 2 without BuildSuffix → `PackageVersion = NextVersion` as-is (e.g. `7.1.0-preview1`).
4. Also extracts `PublishedVersion` (hardcoded in Versions.props, e.g. `7.0.0`).

#### Step B: Resolve Effective Versions

For each package, a `release<Pkg>` boolean parameter determines the outcome:

| `release<Pkg>` | Effective Version | Meaning |
|----------------|-------------------|---------|
| `True` | `NextVersion` (e.g. `7.1.0-preview1`) | This package is being released |
| `False` | `PublishedVersion` (e.g. `7.0.0`) | Only built as dependency; use last-shipped version |

These are published as ADO output variables (e.g. `versions.SqlClientPackageVersion`).

#### Step C: Build Stages Consume Pre-computed Versions

Each downstream build job receives:
- `packageVersion` parameter → passed as `-p:PackageVersion<Pkg>=<value>`
- Dependency versions → passed as `-p:PackageVersion<Dep>=<value>`

Since an explicit `PackageVersion<Pkg>` is provided, Versions.props hits Priority 1 — uses the value verbatim.

#### Summary

| Package Status | Version Source | Example |
|----------------|----------------|---------|
| Being released | `*NextVersion` from Versions.props | `7.1.0-preview1` |
| Dependency only | `*PublishedVersion` from Versions.props | `7.0.0` |

## Key Architectural Difference

| Scenario | Who computes versions | How dependencies get versions |
|----------|----------------------|-------------------------------|
| Developer | Versions.props inline (Priority 3) | Project references (no version needed) |
| PR/CI (Project) | `compute-versions-ci-stage` up-front | Project references (no version needed) |
| PR/CI (Package) | `compute-versions-ci-stage` up-front | Stage variables via `$[ stageDependencies... ]` → `-p:PackageVersion<Dep>=` |
| OneBranch | `compute-versions-stage` up-front | Explicit `-p:PackageVersion<Dep>=` from stage outputs |

## Updating Versions

After a release:
1. Update `*PublishedVersion` to the version just shipped.
2. Update `*NextVersion` to the next planned version.
3. Both properties live in each package's `Versions.props`.

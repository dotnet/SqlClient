---
applyTo: "eng/pipelines/**/*.yml"
---
# OneBranch Pipeline Guidelines

## Purpose

Rules and conventions for editing the OneBranch Azure DevOps YAML pipelines that build, sign, package, and release six NuGet packages with interdependencies.

## Pipeline Variants

- `sqlclient-official.yml` — Official pipeline; uses `OneBranch.Official.CrossPlat.yml`; runs on a daily schedule at 04:30 UTC on `internal/main`, with no activity-based trigger (`pr: none`, `trigger: none`)
- `sqlclient-non-official.yml` — Non-Official pipeline; uses `OneBranch.NonOfficial.CrossPlat.yml`; manual only (`pr: none`, `trigger: none`)
- Both live under `eng/pipelines/onebranch/` and extend OneBranch governed templates
- Never parameterize the OneBranch template name — hardcode it per pipeline for PRC compliance
- Official pipeline must never be run on PRs or dev branches.

## Package Dependency Order

Respect this graph when modifying build stages:

1. `Microsoft.SqlServer.Server` — no dependencies
2. `Microsoft.Data.SqlClient.Internal.Logging` — no dependencies
3. `Microsoft.Data.SqlClient.Extensions.Abstractions` — depends on Logging
4. `Microsoft.Data.SqlClient` — depends on Logging + Abstractions
5. `Microsoft.Data.SqlClient.Extensions.Azure` — depends on Abstractions + Logging
6. `Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider` — depends on SqlClient + Abstractions + Logging

## Build Stages

Defined in `stages/build-stages.yml`. Four build stages plus validation, ordered by dependency:

- **`build_independent`** (Stage 1) — Logging and SqlServer.Server in parallel; no inter-package dependencies
- **`build_abstractions`** (Stage 2) — Abstractions; `dependsOn: build_independent`; downloads Logging artifact
- **`build_dependent`** (Stage 3) — SqlClient and Extensions.Azure in parallel; `dependsOn: build_abstractions`; downloads Abstractions + Logging artifacts
- **`build_addons`** (Stage 4) — AKV Provider; `dependsOn: build_dependent`; downloads SqlClient + Abstractions + Logging artifacts
- **`package_validation`** (Stage 5) — Validates every package produced by the run; `dependsOn` all four build stages plus `compute_versions`

Each build job copies PDB files into `$(JOB_OUTPUT)/symbols/` so they are included in the auto-published pipeline artifact alongside the NuGet packages in `$(JOB_OUTPUT)/packages/`.

Stage conditional rules:
- The SqlClient family (Logging, Abstractions, SqlClient, Azure, AKV Provider) is **always built** — Stages 2, 3, and 4 and the Logging job in Stage 1 are unconditional. There is no `buildSqlClient`/`buildAKVProvider` toggle.
- `buildSqlServer` is the only build toggle; it controls just the SqlServer.Server job in Stage 1.
- When `buildSqlServer` is true, SqlClient/AKV depend on the freshly-built SqlServer artifact (downloaded into the local feed). When false, they depend on the most recently published SqlServer package — a version-only dependency (no artifact download) restored from NuGet.

## Job Templates

- **`build-buildproj-job.yml`** — Shared build.proj-driven package job used for all shipped packages. Flow: build via `build.proj` → optional ESRP DLL signing → pack via `build.proj` → optional ESRP NuGet signing → copy outputs for APIScan/artifacts
- **`validate-packages-job.yml`** — Validates every package produced by the run. Downloads all package artifacts into one tree and validates them together, so `tools/PackageValidator` can apply its cross-package rules (the SqlClient family must share one version, and inter-package dependency ranges must agree); validating per package would silently skip those findings. Runs on Windows because Authenticode verification has no Linux equivalent
- **`publish-nuget-package-job.yml`** — Reusable release job using OneBranch `templateContext.type: releaseJob` with `inputs` for artifact download; pushes via `NuGetCommand@2`
- **`publish-symbols-job.yml`** — Reusable symbols job: downloads a build artifact, locates PDBs under `symbols/`, and invokes `publish-symbols-step.yml`

When adding a new package to the OneBranch flow:
- Extend `build-buildproj-job.yml` inputs with the new package metadata and dependency artifacts
- Add or update the corresponding build/pack targets in `build.proj`
- Add version variables to `variables/common-variables.yml`
- Add artifact name variables to `variables/onebranch-variables.yml`

## Package Validation Stage

- Defined in `stages/build-stages.yml`; produces stage `package_validation`
- Consumes the package and file versions published by `compute_versions` and asserts the produced packages carry exactly those values, so nothing is re-derived
- All packages are validated together in one job so `tools/PackageValidator` can apply cross-package rules; the SqlServer artifact and its expectations are conditional on `buildSqlServer`
- Expectations use the validator's `[id=]value` form: the SqlClient family version is applied as a wildcard (proving the family agrees, and catching the case where all packages are consistently wrong), with `Microsoft.SqlServer.Server` as a per-id override
- When SqlServer is not built its expectations are **omitted entirely** rather than passed empty — the validator rejects an expectation with an empty value
- Gate categories are derived from `isOfficial`: `error` and `missing-symbols` always, plus `package-unsigned` on official runs only. `missing-symbols` is a warning and `package-unsigned` is info, so neither is covered by the `error` severity and both must be named explicitly; non-official runs are deliberately unsigned, so gating them on `package-unsigned` would always fail
- The validator runs twice: once with `--json` and no gate so the report exists even for a failing run, then once human-readable with the gate so failures appear in the job log
- Signature verification (`dotnet nuget verify --all`, Authenticode) runs on official builds only, and verifies that signatures are *trusted* — PackageValidator reports only their presence, from metadata
- The release stage `dependsOn: package_validation`, so a package that fails validation is never published
- Step and job logic lives in `scripts/validate-packages.ps1`, `scripts/verify-package-signatures.ps1`, and `scripts/verify-assembly-signatures.ps1`, each with Pester tests under `scripts/tests/`

## Symbols Publishing Stage

- Defined in `stages/publish-symbols-stage.yml`; produces stage `publish_symbols`
- Entire stage excluded at compile time when `publishSymbols` is false
- The SqlClient family symbols are always published; the SqlServer.Server symbols job is conditional on `buildSqlServer`
- `dependsOn` covers all family build stages (always present), plus `build_independent` for SqlServer
- One job per package (`publish-symbols-job.yml`), each downloading its build artifact and publishing PDBs from `symbols/`
- Each package's PDBs are published separately with unique artifact names and version information
- Build jobs copy PDBs into `$(JOB_OUTPUT)/symbols/` so they are included in the auto-published artifact
- The `publish-symbols-step.yml` accepts a `symbolsFolder` parameter to point at the downloaded PDB location
- The publish step calls an extracted `publish-symbols.ps1` script with structured error handling and diagnostic logging
- Symbols publishing credentials come from the `Symbols Publishing` variable group
- In the official pipeline, symbol server destination follows `releaseToProduction`: Production when true, PPE when false
- Non-official pipeline always targets the PPE symbol server

## Release Stage

- Defined in `stages/release-stages.yml`; produces stage `release_production` (official) or `release_test` (non-official) via `stageNameSuffix` parameter
- Entire stage excluded at compile time when no release parameters are true
- `dependsOn` is conditional based on which release parameters are set
- `releaseToProduction` parameter controls NuGet target feed:
  - `true` → service connection `ADO Nuget Org Connection` (NuGet Production)
  - `false` → service connection `ADO Nuget Org Test Connection` (NuGet Test)
- Non-official pipeline always sets `releaseToProduction: false`
- Environment gating:
  - Official: `ob_release_environment: Production`, `ob_deploymentjob_environment: NuGet-Production`
  - Non-official: `ob_release_environment: Test`, `ob_deploymentjob_environment: NuGet-DryRun`
- Each publish job uses OneBranch deployment job syntax (`templateContext.type: releaseJob` with `inputs` for artifact download)

## Parameters

Build parameters:
- `debug` — enable debug output (default `false`)
- `isPreview` — use preview version numbers (default `false`)
- `publishSymbols` — publish symbols to servers (default `false`)
- `buildSqlServer` — build the Microsoft.SqlServer.Server package (default `true` in the non-official/nightly pipeline, `false` in the official pipeline). The SqlClient family is always built, so this is the only build toggle. It also drives the SqlServer dependency version the family uses (built/next vs published). Requesting `releaseSqlServer` without `buildSqlServer` fails template expansion.

Release parameters (boolean, default `false`):
- `releaseSqlClient` — release the entire SqlClient family together (Logging, Abstractions, SqlClient, Azure, AKV Provider) at the shared version
- `releaseSqlServer` — release Microsoft.SqlServer.Server (versioned separately)

Official-only parameter:
- `releaseToProduction` — controls both NuGet target feed and symbol server destination (default `false`):
  - `true` → NuGet Production feed + Production symbol server
  - `false` → NuGet Test feed + PPE symbol server

When `isPreview` is true, pipeline resolves `effective*Version` variables to preview versions; otherwise GA versions. All versions defined in `variables/common-variables.yml`.

## Variables and Versions

- Variable chain: pipeline YAML → `variables/onebranch-variables.yml` → `variables/common-variables.yml`
- All package versions (GA, preview, assembly file) centralized in `variables/common-variables.yml`
- The `compute_versions` stage reads canonical versions from MSBuild and publishes effective package,
  file-build, and APIScan registration versions for downstream stages
- Artifact name variables defined in `variables/onebranch-variables.yml` following `drop_<stageName>_<jobName>` pattern
- `assemblyBuildNumber` derived from first segment of `Build.BuildNumber` only (16-bit limit)
- When adding a new package, add GA version, preview version, and assembly file version entries

Variable groups:
- `Symbols Publishing` — symbol publishing credentials (in `onebranch-variables.yml`)
- `ESRP Federated Creds (AME)` — ESRP signing credentials (in `common-variables.yml`)

## Code Signing (ESRP)

- Uses ESRP v6 tasks (`EsrpMalwareScanning@6`, `EsrpCodeSigning@6`) with MSI/federated identity authentication
- Signing only runs when `isOfficial: true` — non-official pipelines skip ESRP steps
- The shared OneBranch job signs DLLs before packing and signs the resulting NuGet package afterward so the published package contains signed binaries
- DLL signing uses keyCode `CP-230012` (Authenticode); NuGet signing uses keyCode `CP-401405`
- All ESRP credentials come from variable groups — never hardcode secrets in YAML

## SDL and Compliance

- TSA: enabled only in official pipeline; disabled in non-official to avoid spurious alerts
- ApiScan: enabled in both; `break` follows the `breakOnSdlError` parameter
- Each package is registered with APIScan under its own name/version pair, so the `globalSdl.apiscan` blocks deliberately omit `softwareName`/`versionNumber`. `build-buildproj-job.yml` is the single place they are set, via `ob_sdl_apiscan_softwareName` (the package's `packageFullName`) and `ob_sdl_apiscan_versionNumber` (the `apiScanSoftwareVersion` parameter)
- `compute-versions.ps1` derives APIScan registration versions as major.minor from the effective canonical package versions and publishes them as stage outputs. A package name/version pair must still be registered with APIScan before releasing a new major.minor. Consume these as runtime `$(...)` references so values such as `1.0` remain strings rather than being coerced to numbers by template expressions
- Jobs that produce no assemblies (symbol publishing, signed-package validation, version computation) set `ob_sdl_apiscan_enabled: false` rather than reporting a name/version
- Each build job also sets `ob_sdl_apiscan_softwareFolder` and `ob_sdl_apiscan_symbolsFolder` to its per-package `apiScan/<package>/dlls` and `apiScan/<package>/pdbs` paths
- CodeQL, SBOM, Policheck (`break: true`): enabled in both pipelines
- SBOM package name/version are resolvable **only** from the pipeline's `globalSdl.sbom` block — OneBranch's artifact-publishing path reads `globalSdl.sbom.packageName`/`packageVersion` directly and has no per-job equivalent (the `templateContext.sdl.sbom` override only applies to the native 1ES Stages entry point, which this repo does not use). Because the pipeline produces six differently-named and independently-versioned packages, `globalSdl.sbom` indirects through the `$(sbomPackageName)` / `$(sbomPackageVersion)` variables, which each build job sets to its own `packageFullName` and computed `packageVersion`. Jobs that publish no packages (version computation, symbol publishing) set `ob_sdl_sbom_enabled: false` alongside their existing APIScan/BinSkim opt-outs, so the variables never need pipeline-level defaults
- asyncSdl `enabled: false` in both; individual sub-tools (CredScan, BinSkim, Armory, Roslyn) configured underneath
- Policheck exclusions: `$(REPO_ROOT)\.config\PolicheckExclusions.xml`
- CredScan suppressions: `$(REPO_ROOT)/.config/CredScanSuppressions.json`

## Artifact Conventions

- `ob_outputDirectory` set to `$(JOB_OUTPUT)` (= `$(REPO_ROOT)/output`) — OneBranch auto-publishes this directory
- Each published artifact uses subdirectories to separate file types:
  - `assemblies/` — DLL assemblies for APIScan (preserving TFM folder structure)
  - `packages/` — NuGet packages (`.nupkg`, `.snupkg`)
  - `symbols/` — PDB symbol files (preserving TFM folder structure, shared by APIScan and symbol publishing)
- Artifact names follow `drop_<stageName>_<jobName>` — defined in `variables/onebranch-variables.yml`
- Downstream jobs download artifacts via `DownloadPipelineArtifact@2` into `$(Build.SourcesDirectory)/packages`
- Downloaded packages serve as a local NuGet source for `dotnet restore`
- If stage or job names change, update artifact name variables in `onebranch-variables.yml`

## Common Pitfalls

- Do not use `PublishPipelineArtifacts` task — OneBranch auto-publishes from `ob_outputDirectory`
- Do not add `NuGetToolInstaller@1` in OneBranch containers — NuGet is pre-installed
- Variable templates are under `variables/` not `libraries/`
- Always test parameter changes in the non-official pipeline first
- When modifying stage names, update all `dependsOn` references and artifact name variables
- Release jobs must use `templateContext.type: releaseJob` with `inputs` for artifact download — deployment jobs do not auto-download artifacts

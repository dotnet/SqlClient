---
applyTo: "eng/pipelines/**/*.yml"
---
# OneBranch Pipeline Guidelines

## Purpose

Rules and conventions for editing the OneBranch Azure DevOps YAML pipelines that build, sign, package, and release six NuGet packages with interdependencies.

## Pipeline Variants

- `sqlclient-official.yml` — Official pipeline; uses `OneBranch.Official.CrossPlat.yml`; CI trigger on `internal/main` + daily schedule at 04:30 UTC
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
- **`mds_package_validation`** — Validates signed SqlClient package; `dependsOn: build_dependent`; runs in parallel with Stage 4

Each build job copies PDB files into `$(JOB_OUTPUT)/symbols/` so they are included in the auto-published pipeline artifact alongside the NuGet packages in `$(JOB_OUTPUT)/packages/`.

Stage conditional rules:
- Wrap stages/jobs in `${{ if }}` compile-time conditionals based on build parameters
- `buildSqlClient` controls Stages 2, 3, validation, and Logging (when AKV is disabled)
- `buildAKVProvider AND buildSqlClient` controls Stage 4
- `buildSqlServerServer` controls SqlServer.Server job in Stage 1
- Logging builds when `buildAKVProvider OR buildSqlClient` is true

## Job Templates

- **`build-signed-csproj-package-job.yml`** — Generic job for csproj-based packages (Logging, SqlServer.Server, Abstractions, Azure, AKV Provider). Flow: Build DLLs → ESRP DLL signing → NuGet pack (`NoBuild=true`) → ESRP NuGet signing → Copy PDBs to artifact
- **`build-signed-sqlclient-package-job.yml`** — SqlClient-specific job (nuspec-based). Flow: Build all configurations → ESRP DLL signing (main + resource DLLs) → NuGet pack via nuspec → ESRP NuGet signing → Copy PDBs to artifact
- **`validate-signed-package-job.yml`** — Validates signed MDS package (signature, strong names, folder structure, target frameworks)
- **`publish-nuget-package-job.yml`** — Reusable release job using OneBranch `templateContext.type: releaseJob` with `inputs` for artifact download; pushes via `NuGetCommand@2`
- **`publish-symbols-job.yml`** — Reusable symbols job: downloads a build artifact, locates PDBs under `symbols/`, and invokes `publish-symbols-step.yml`

When adding a new csproj-based package:
- Use `build-signed-csproj-package-job.yml` with appropriate `packageName`, `packageFullName`, `versionProperties`, and `downloadArtifacts`
- Add build and pack targets to `build.proj`
- Add version variables to `variables/common-variables.yml`
- Add artifact name variable to `variables/onebranch-variables.yml`

## Symbols Publishing Stage

- Defined in `stages/publish-symbols-stage.yml`; produces stage `publish_symbols`
- Entire stage excluded at compile time when `publishSymbols` is false
- `dependsOn` is conditional based on which `build*` parameters are set, mirroring the build stage dependency graph
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

Build parameters (all boolean, default `true`):
- `debug` — enable debug output (default `false`)
- `isPreview` — use preview version numbers (default `false`)
- `publishSymbols` — publish symbols to servers (default `false`)
- `buildSqlServerServer` — build SqlServer.Server package
- `buildSqlClient` — build SqlClient, Extensions.Azure, Abstractions, and Logging
- `buildAKVProvider` — build AKV Provider (requires `buildSqlClient`)

Release parameters (all boolean, default `false`):
- `releaseSqlServerServer`, `releaseLogging`, `releaseAbstractions`, `releaseSqlClient`, `releaseAzure`, `releaseAKVProvider`

Official-only parameter:
- `releaseToProduction` — controls both NuGet target feed and symbol server destination (default `false`):
  - `true` → NuGet Production feed + Production symbol server
  - `false` → NuGet Test feed + PPE symbol server

When `isPreview` is true, pipeline resolves `effective*Version` variables to preview versions; otherwise GA versions. All versions defined in `variables/common-variables.yml`.

## Variables and Versions

- Variable chain: pipeline YAML → `variables/onebranch-variables.yml` → `variables/common-variables.yml`
- All package versions (GA, preview, assembly file) centralized in `variables/common-variables.yml`
- `effective*Version` pipeline variables map to selected version set based on `isPreview`
- Artifact name variables defined in `variables/onebranch-variables.yml` following `drop_<stageName>_<jobName>` pattern
- `assemblyBuildNumber` derived from first segment of `Build.BuildNumber` only (16-bit limit)
- When adding a new package, add GA version, preview version, and assembly file version entries

Variable groups:
- `Symbols Publishing` — symbol publishing credentials (in `onebranch-variables.yml`)
- `ESRP Federated Creds (AME)` — ESRP signing credentials (in `common-variables.yml`)

## Code Signing (ESRP)

- Uses ESRP v6 tasks (`EsrpMalwareScanning@6`, `EsrpCodeSigning@6`) with MSI/federated identity authentication
- Signing only runs when `isOfficial: true` — non-official pipelines skip ESRP steps
- csproj-based packages: sign DLLs first → pack with `NoBuild=true` → sign NuGet package (ensures NuGet contains signed DLLs)
- SqlClient: sign DLLs (including resource DLLs) → nuspec pack → sign NuGet package
- DLL signing uses keyCode `CP-230012` (Authenticode); NuGet signing uses keyCode `CP-401405`
- All ESRP credentials come from variable groups — never hardcode secrets in YAML

## SDL and Compliance

- TSA: enabled only in official pipeline; disabled in non-official to avoid spurious alerts
- ApiScan: enabled in both; currently `break: false` pending package registration
- Each build job sets `ob_sdl_apiscan_softwareFolder` to `$(JOB_OUTPUT)/assemblies` and `ob_sdl_apiscan_symbolsFolder` to `$(JOB_OUTPUT)/symbols`
- CodeQL, SBOM, Policheck (`break: true`): enabled in both pipelines
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

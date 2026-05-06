---
applyTo: "eng/pipelines/**/*.yml"
---
# Azure DevOps CI/CD Pipeline Guidelines

## Purpose

Rules and conventions for editing the Azure DevOps CI/CD pipelines that build, test, and validate Microsoft.Data.SqlClient. These pipelines live under `eng/pipelines/` (excluding `onebranch/`, which is covered by separate instructions).

**ADO Organization**: sqlclientdrivers | **ADO Project**: ADO.NET

## Pipeline Layout

Two categories of pipelines exist in this repository:

- **CI/PR pipelines** (`eng/pipelines/`) — Build, test, and validate on every push/PR
- **OneBranch pipelines** (`eng/pipelines/onebranch/`) — Official signing/release builds (separate instructions file)

Top-level CI/PR pipeline files:
- `dotnet-sqlclient-ci-core.yml` — Reusable core template; all CI and PR pipelines extend this
- `dotnet-sqlclient-ci-package-reference-pipeline.yml` — CI with Package references (Release)
- `dotnet-sqlclient-ci-project-reference-pipeline.yml` — CI with Project references (Release)
- `sqlclient-pr-package-ref-pipeline.yml` — PR validation with Package references
- `sqlclient-pr-project-ref-pipeline.yml` — PR validation with Project references
- `stress/stress-tests-pipeline.yml` — Stress test pipeline and templates

Reusable templates are organized under:
- `common/templates/jobs/` — Job templates (`ci-build-nugets-job`, `ci-code-coverage-job`, `ci-run-tests-job`)
- `common/templates/stages/` — Stage templates (`ci-run-tests-stage`)
- `common/templates/steps/` — Step templates (build, test, config, publish)
- `jobs/` — Package-specific CI jobs (pack/test Abstractions, Azure, Logging, stress)
- `stages/` — Package-specific CI stages (generate secrets, build SqlServer/Logging/Abstractions/SqlClient/Azure, verify packages)
- `libraries/` — Shared variables (`ci-build-variables.yml`)
- `steps/` — SDK install steps

## CI Core Template

`dotnet-sqlclient-ci-core.yml` is the central orchestrator. All CI and PR pipelines extend it with different parameters.

Key parameters:
- `referenceType` (required) — `Package` or `Project`; controls how sibling packages are referenced
- `buildConfiguration` (required) — `Debug` or `Release`
- `testJobTimeout` (required) — test job timeout in minutes
- `targetFrameworks` — Windows test TFMs; default `[net462, net8.0, net9.0, net10.0]`
- `targetFrameworksUnix` — Unix test TFMs; default `[net8.0, net9.0, net10.0]`
- `netcoreVersionTestUtils` — default runtime for shared test utilities; default `net10.0`
- `testSets` — test partitions; default `[1, 2, 3]`
- `useManagedSNI` — SNI variants to test; default `[false, true]`
- `runAlwaysEncryptedTests` — include AE test set; default `true`
- `runLegacySqlTests` — include SQL Server 2016/2017 manual-test legs; default `true`
- `debug` — enable debug output; default `false`
- `dotnetVerbosity` — MSBuild verbosity; default `normal`

## Build Stage Order

Stages execute in dependency order (Package reference mode requires artifacts from prior stages):
1. `generate_secrets` — Generate test secrets
2. `build_sqlserver_package_stage` — Build `Microsoft.SqlServer.Server`
3. `build_logging_package_stage` — Build Logging package
4. `build_abstractions_package_stage` — Build Abstractions (package mode depends on Logging)
5. `build_sqlclient_package_stage` — Build SqlClient and produce the AKV provider package
6. `build_azure_package_stage` — Build Azure extensions
7. `verify_nuget_packages_stage` — Verify NuGet package metadata
8. `ci_run_tests_stage` — Run MDS and AKV test suites

Stress testing is no longer a stage threaded through `dotnet-sqlclient-ci-core.yml`; it lives under `eng/pipelines/stress/` as a separate pipeline flow.

When adding a new build stage, respect the dependency graph and pass artifact names/versions to downstream stages.

## PR vs CI Pipeline Differences

PR pipelines:
- Trigger on PRs to `dev/*`, `feat/*`, `main`; exclude `eng/pipelines/onebranch/*` paths
- Use reduced TFM matrix: `[net462, net8.0, net9.0]` (excludes net10.0)
- Timeout: 90 minutes
- Package-ref PR disables Always Encrypted tests in Debug config and also disables legacy SQL Server test legs to keep validation fast

CI pipelines:
- Trigger on push to `main` (GitHub) and `internal/main` (ADO) with `batch: true`
- Scheduled weekday builds (see individual pipeline files for cron times)
- Full TFM matrix including net10.0 test legs and legacy SQL Server manual-test coverage

## Test Configuration

Test partitioning:
- Tests split into `TestSet=1`, `TestSet=2`, `TestSet=3` for parallelization
- `TestSet=AE` — Always Encrypted tests (controlled by `runAlwaysEncryptedTests`)

Test filters — default excludes `failing` and `flaky` categories:
- `failing` — known permanent failures, always excluded
- `flaky` — intermittent failures, quarantined in separate pipeline steps
- `nonnetfxtests` / `nonnetcoreapptests` — platform-specific exclusions
- `nonwindowstests` / `nonlinuxtests` — OS-specific exclusions

Flaky test quarantine:
- Quarantined tests (`[Trait("Category", "flaky")]`) run in separate steps after main tests
- Main test runs are not blocked by flaky failures
- No code coverage collected for flaky runs
- Configured in `common/templates/steps/build-and-run-tests-netcore-step.yml`, `build-and-run-tests-netfx-step.yml`, and `run-all-tests-step.yml`

SNI testing — `useManagedSNI` controls testing with native SNI (`false`) or managed SNI (`true`)

Test timeout — `--blame-hang-timeout 10m` (configured in `build.proj` and threaded through CI test steps); tests exceeding 10 minutes are killed

## Variables

- All CI build variables centralized in `libraries/ci-build-variables.yml`
- Package versions use `-ci` suffix (e.g., `7.0.0.$(Build.BuildNumber)-ci`)
- `assemblyBuildNumber` derived from first segment of `Build.BuildNumber` (16-bit safe)
- `localFeedPath` = `$(Build.SourcesDirectory)/packages` — local NuGet feed for inter-package deps
- `packagePath` = `$(Build.SourcesDirectory)/output` — NuGet pack output

## Conventions When Editing Pipelines

- Always use templates for reusable logic — do not inline complex steps
- Pass parameters explicitly; avoid relying on global variables
- Use descriptive stage/job/step display names
- When adding parameters, define them in the core template and thread through calling pipelines
- When adding test categories, update filter expressions in test step templates
- PR pipelines should run a minimal matrix for fast feedback
- Test changes via PR pipeline first — validation runs automatically
- Enable `debug: true` and `dotnetVerbosity: diagnostic` for troubleshooting
- Never commit credentials or secrets in pipeline files
- Signing and release are handled by OneBranch pipelines — not these CI/PR pipelines

# SqlClient Performance Test Pipeline

This directory contains the Azure DevOps pipeline and supporting scripts that run the
Microsoft.Data.SqlClient [BenchmarkDotNet](https://benchmarkdotnet.org/) performance tests on a
dedicated performance test lab (Azure Dedicated Hosts), compare the branch under test against a
released NuGet baseline, and optionally ingest the results into an Azure Data Explorer (Kusto)
database.

## Contents

| Path | Purpose |
| ---- | ------- |
| `sqlclient-perf-pipeline.yml` | The main (manual/nightly) pipeline. Extends `v1/Perf.Test.Job.yml@PerfTemplates`. Baseline = released NuGet package; ingests into Kusto. |
| `sqlclient-perf-pr-pipeline.yml` | PR pipeline. Same template, same scripts, same options; baseline = **`main` branch source**; **no Kusto ingestion**. |
| `scripts/run-perf-tests.sh` | Linux on-VM entry point: install SDK, create DB, run benchmarks (interleaved or sequential), compare. Baseline is either a released package (`--baseline-version`) or another git ref's source (`--baseline-source-ref`). |
| `scripts/run-perf-tests.ps1` | Windows equivalent (ProcessorAffinity instead of `taskset`). |
| `scripts/interleave_perf.py` | Interleaved + best-of-N orchestrator: runs each unit baseline↔candidate back-to-back and confirms regressions across N passes. |
| `scripts/compare_perf.py` | Compares baseline vs current BenchmarkDotNet JSON → delta (md + json). Reused by the orchestrator. |
| `scripts/perf_to_kusto.py` | Translates BenchmarkDotNet "full" JSON → Kusto `PerfRun` + `PerfBenchmarkResult` NDJSON. |
| `scripts/ingest_kusto.py` | Queued Kusto ingestion (az CLI auth, runs on the agent). |

## Architecture

```
Queue pipeline (ADO)
      │
      ▼
extends: v1/Perf.Test.Job.yml@PerfTemplates
      │  (provisions a dedicated-host VM, SCPs the repo, runs the script over SSH,
      │   SCPs <testResultsSubDir> back, publishes it, tears the VM down)
      ▼
ON THE VM  ── run-perf-tests.{sh,ps1}
      1. Install the .NET SDK pinned by global.json (+ runtimes).
      2. Create the perf database on the VM's SQL Server.
      3. Inject the VM SQL connection string into runnerconfig.
      4. Baseline pass  → MDS <baselineVersion> from NuGet.org (Package mode)   → results/baseline/
         (PR pipeline: MDS built from the <baselineSourceRef> source tree)
      5. Current  pass  → MDS built from source (ProjectReference)              → results/current/
         (both pinned to PERF_CLIENT_CPUS; interleaved per-unit by default, or two full
          sequential passes when benchmarkRunMode=sequential)
      6. interleave_perf.py / compare_perf.py → results/comparison/ + results/summary.md
      ▼
ON THE AGENT  ── pipeline post-test steps
      • Show the BenchmarkDotNet markdown reports in the log.
      • perf_to_kusto.py → NDJSON for both passes (published as 'perf-kusto-payloads').
      • (optional) AzureCLI@2 + ingest_kusto.py → Kusto database.
      (both Kusto steps are omitted entirely in the PR pipeline)
```

The extends template only exposes **post-test** steps to consumers (no pre-build hook), so **both
benchmark passes run inside the on-VM script**. Translation and ingestion run on the **agent**
because that is where the pipeline's AAD identity / service connection and the native pipeline
context variables are available (the VM is behind NAT and lacks the pipeline identity).

## Parameters

| Parameter | Default | Description |
| --------- | ------- | ----------- |
| `platform` | `linux` | `linux` or `windows` VM + client. |
| `dotnetFramework` | `net9.0` | TFM the benchmarks run against (`net8.0`/`net9.0`/`net10.0`). |
| `testTimeoutMinutes` | `180` | Template timeout waiting for the VM run. |
| `baselineVersion` | `7.0.2` | **Baseline Version** — released MDS the branch is compared against. Empty = current-only (no baseline pass / comparison). |
| `regressionThreshold` | `10` | Percent slowdown (current vs baseline mean) flagged as a regression. |
| `failOnRegression` | `false` | When `true`, a candidate-slower regression **fails** the run (gate). In interleaved mode only **confirmed** regressions (best-of-N majority) fail. Default off. |
| `benchmarkRunMode` | `interleaved` | `interleaved` (per-unit baseline↔candidate + best-of-N confirmation) or `sequential` (legacy two full passes). |
| `confirmationRuns` | `3` | Best-of-N: interleaved passes for a flagged unit before a regression is confirmed. `1` disables confirmation. Interleaved mode only. |
| `useManagedSniOnWindows` | `false` | SqlClient `UseManagedSniOnWindows` flag. Applied to the runner config for **both** passes on the VM and recorded in `PerfRun.Config`. Default matches the checked-in `runnerconfig.jsonc`. |
| `useOptimizedAsyncBehaviour` | `true` | SqlClient `UseOptimizedAsyncBehaviour` flag. Applied to both passes and recorded in `PerfRun.Config`. Default matches `runnerconfig.jsonc`. |
| `useConnectionPoolV2` | `false` | SqlClient `UseConnectionPoolV2` flag. Applied to both passes and recorded in `PerfRun.Config`. Default matches `runnerconfig.jsonc`. |
| `enableKustoIngestion` | `true` | **Ingest results into Kusto** — when `false`, the run still benchmarks + compares but skips ingesting into the perf database. When `true`, ingestion additionally requires the `ADX Cluster Variables` group to be populated. |

The following values are **fixed constants** in the pipeline (not parameters or variables), since they
are invariant for this pipeline: `buildConfiguration = Release`, `sourcesSubDir = dotnet-sqlclient`
(the multi-repo checkout folder for `self`, which must match the ADO repository name), and
`driverName = Microsoft.Data.SqlClient` (recorded on every row as `DriverName` / `DerivedRunId`).

### Kusto (Azure Data Explorer) ingestion variables

The ADX ingestion coordinates are **not** pipeline parameters — they come from a pipeline library
variable group named **`ADX Cluster Variables`** so no infrastructure identifiers are hard-coded in
the pipeline. The group must define:

| Variable | Description |
| -------- | ----------- |
| `KustoClusterUri` | ADX cluster URI, e.g. `https://<cluster>.<region>.kusto.windows.net`. Empty ⇒ ingestion skipped. |
| `KustoDatabase` | Target Kusto database. Empty ⇒ ingestion skipped. |
| `KustoServiceConnection` | Azure DevOps ARM service connection whose SP has ingest rights. Empty ⇒ ingestion skipped. |

Ingestion is gated at runtime: it only runs when `KustoClusterUri`, `KustoDatabase` and
`KustoServiceConnection` are all non-empty, so the pipeline still runs + compares before the
cluster/service connection exist.

### Managing the baseline version

`baselineVersion` is **manually managed**. After each stable release is published to NuGet.org,
bump the `default` in `sqlclient-perf-pipeline.yml` (e.g. `7.0.2` → the next stable). It can also be
overridden at queue time without editing the pipeline.

## PR pipeline (`sqlclient-perf-pr-pipeline.yml`)

`sqlclient-perf-pr-pipeline.yml` answers the question a PR author actually has — *does my branch
regress the branch I am merging into?* — by running the **same** benchmarks, on the **same** extends
template, through the **same** on-VM scripts, with the **same** configuration options as the main
pipeline. It differs in exactly two ways:

| | `sqlclient-perf-pipeline.yml` | `sqlclient-perf-pr-pipeline.yml` |
| --- | --- | --- |
| Candidate | branch the run is queued on | branch the run is queued on (unchanged) |
| Baseline | released NuGet package (`baselineVersion`, default `7.0.2`) | **source of another git ref** (`baselineSourceRef`, default `main`) |
| Kusto | translates + (optionally) ingests | **never** — no ADX variable group, no translate/ingest steps |

Both pipelines are **manual / queue-time only** (`pr: none`, `trigger: none`): a run occupies a
dedicated host for hours, so a PR opts into it explicitly.

PR-only parameters (everything else is identical to the table above):

| Parameter | Default | Description |
| --------- | ------- | ----------- |
| `baselineSourceRef` | `main` | Git ref of this repo whose **source** is the baseline. Empty = current-only (no baseline pass / comparison). |
| `baselineRepoUrl` | `https://github.com/dotnet/SqlClient.git` | Fallback remote used to obtain the baseline ref when the VM copy of the checkout cannot fetch it from its own `origin`. |
| `testTimeoutMinutes` | `210` | Higher than the main pipeline's `180` because **both** sides are built from source. |

### Source baseline (`--baseline-source-ref` / `-BaselineSourceRef`)

The run scripts accept the source baseline as an alternative to `--baseline-version` (the two are
mutually exclusive and the script fails fast if both are supplied). On the VM the script:

1. Materialises the baseline ref next to the checkout, in `../sqlclient-perf-baseline-src` — outside
   the checkout so it can never be picked up by the candidate build or the results copy. It first
   tries the copied checkout's own `origin` (`git fetch --depth 1` into a private
   `refs/remotes/perfbaseline/*` namespace, then `git worktree add --detach`), and falls back to a
   shallow `git clone` of `baselineRepoUrl` when the tree arrived without `.git` or `origin` needs
   credentials.
2. Builds **that ref's own `PerformanceTests` project** as the `baseline` variant, so the driver
   under measurement is the baseline ref's source via `ProjectReference` — mirroring how the
   `current` variant is built from this branch. No NuGet baseline config is involved.
3. Labels the comparison `"<ref>@<short-sha>"` (e.g. `main@a1b2c3d`) so a run states exactly which
   baseline commit it was measured against, and writes that label to
   `<results>/baseline-label.txt`. The PR pipeline reads that file after the results are copied
   back and tags the build `Baseline <ref>@<short-sha>`, so the exact baseline commit is visible in
   the ADO build list. (The pipeline cannot produce this tag on its own — at compile time it only
   knows the ref name, which would tag every run identically.)

Because each side builds its own harness, a benchmark added by the PR simply shows up as `new` in
the comparison (and one removed by the PR as `removed`) instead of failing the run. Both passes
share the single generated runner config (`RUNNER_CONFIG` / `DATATYPES_CONFIG` env vars), so
connection string and behaviour flags are identical on both sides.

## Two-pass build model

The `PerformanceTests` project references Microsoft.Data.SqlClient two ways, selected by MSBuild:

- **Current** (default): `ProjectReference` to the in-repo source — the branch under test.
- **Baseline (package)**: `ReferenceType=Package` turns the reference into a `PackageReference`. Because the
  repo uses **Central Package Management (CPM)**, the version is pinned with `VersionOverride` via
  `-p:MdsPackageVersion=<version>` (a plain `Version` is ignored under CPM).
- **Baseline (source)**: no reference switching at all — the baseline ref's own copy of the perf
  project is built from `../sqlclient-perf-baseline-src`, keeping its default `ProjectReference` to
  that ref's driver source. Used by the PR pipeline.

The VM's `NuGet.config` exposes only the governed feed, and CPM rejects multiple unmapped sources
(`NU1507`). The baseline pass therefore restores through a **dedicated single-source config**
(`perf-baseline-nuget.config`, generated at runtime) pointing only at `https://api.nuget.org/v3/index.json`.

## Comparison output

`compare_perf.py` matches benchmarks by `(Type, Method, Parameters)` and reports, per benchmark, the
baseline/current mean (ms), mean %Δ, allocation %Δ, and a status (`regression` / `improvement` /
`unchanged` / `new` / `removed`). Outputs:

- `results/comparison/comparison.md` (also copied to `results/summary.md`, which the template
  attaches as the run summary),
- `results/comparison/comparison.json` (structured, for tooling).

## Reducing noise

The harness applies a set of harness-owned controls to reduce measurement noise. The lab already
supplies the isolated dedicated host, the tuned SQL instance, and the disjoint client CPU set
(`PERF_CLIENT_CPUS`); the run scripts add:

| Control | What the harness does |
| ------- | --------------------- |
| Client CPU pin | Pins the benchmark process to `PERF_CLIENT_CPUS` (`taskset` on Linux, `ProcessorAffinity` on Windows). |
| Fail loud | Preflight `SELECT 1` before any pass, **and** a post-pass guard that fails the run if a pass produced **zero** benchmark results — so an empty comparison can never be reported green. |
| Warm-up | Touches the target DB in the preflight to warm the buffer pool / plan cache before the first measured benchmark. |
| Allocator tuning (Linux) | Exports `MALLOC_MMAP_THRESHOLD_=128MiB` and `MALLOC_TRIM_THRESHOLD_=-1` so large-buffer benches (`AsyncLargeDataRead`, `SqlBulkCopy`) stop re-`mmap`ing per iteration. |
| Network tuning (Linux) | Best-effort `sysctl` to widen the ephemeral port range and enable `tcp_tw_reuse` for churn benches (`ConnectionPoolStress`, `ParallelAsyncConnection`). Never fails the run. |
| Diagnostics | Writes `results/diagnostics/`: SQL instance config (MAXDOP, memory, affinity, tempdb files, `@@VERSION`), host CPU topology, and per-pass CPU-clock/thermal telemetry (before/after each pass). |
| Regression gate | `failOnRegression` threads `--fail-on-regression`; only a **candidate-slower** delta past the threshold fails, and in interleaved mode only after best-of-N confirmation. Default off. |
| Interleaving | In `interleaved` mode the harness runs **one benchmark unit at a time, baseline then candidate back-to-back**, so both sides see the same host state (see below). |
| Best-of-N confirmation | A unit flagged in the first interleaved pass is re-run `confirmationRuns` times; a regression is **confirmed** only on a strict majority. Unconfirmed flags are reported but never fail the gate. |

### Interleaving + best-of-N (run model)

`benchmarkRunMode` selects how the two variants are measured:

- **`interleaved`** (default) — `interleave_perf.py` orchestrates the run. Both variants are built
  **once** into separate output dirs (`perf-build-baseline`, `perf-build-current`), then for each
  benchmark unit the baseline and candidate builds run **back-to-back** before moving to the next
  unit. Because the same benchmark is measured on both sides within seconds, slow host drift affects
  both roughly equally and cancels out of the delta. This relies on the `PerformanceTests` runner
  supporting `PERF_LIST_BENCHMARKS` (enumerate enabled units) and `PERF_BENCHMARK=<unit>` (run a
  single unit) — see `Program.cs`.

  After the first interleaved pass, only the units containing a flagged regression are re-run
  `confirmationRuns` times (best-of-N). A regression is **confirmed** only when a strict majority of
  the N passes agree `(count * 2 > N)`; otherwise it is reported as `regression (unconfirmed)` and
  does **not** fail the `failOnRegression` gate. `confirmationRuns = 1` disables confirmation.

- **`sequential`** — legacy model: the whole baseline suite runs, then the whole candidate suite,
  then `compare_perf.py` diffs them. Kept as a fallback; produces the same `results/baseline`,
  `results/current`, and `results/comparison/` layout so Kusto ingestion is identical.

Both modes emit `results/comparison/comparison.md` + `comparison.json` and copy the markdown to
`results/summary.md`; interleaved mode adds a **Confirm** column and a confirmed/unconfirmed summary.

### Further tuning (not yet implemented)

- **Release-grade sampling / relaxed thresholds** — tune BenchmarkDotNet job counts and
  significance thresholds in `runnerconfig.jsonc` / `BenchmarkConfig.cs` now that interleaving and
  best-of-N are in place.

## Kusto schema & ingestion

Two tables:

- **`PerfRun`** — one row per run (baseline OR current): `DerivedRunId` (PK =
  `DriverName|CommitHash|PipelineRunId`), driver/machine/agent, `OperatingSystem` (`Windows`/`Linux`),
  `Architecture` (`x64`/`x86`), `RunType` (`Sequential`/`Interweaved`), pipeline id + build URL,
  branch + `BranchCategory`, `VersionString`, commit hash/date, `IsComparableBase`, `IngestedAt`.
  `BranchCategory` buckets the ref as `main`, `release`, `dev`, `feature`, `pull_request` or
  `other`. The internal ADO mirror's `internal/` prefix is stripped first, so `internal/main` and
  `internal/release/*` land in the same buckets as their public counterparts.
- **`PerfBenchmarkResult`** — one row per benchmark: `BenchmarkId` (PK =
  `DerivedRunId|BenchmarkName|MethodName|ParameterSignature`), timings in **milliseconds**
  (BenchmarkDotNet reports nanoseconds; values are divided by 1,000,000), percentiles, throughput,
  allocation, runtime/platform, and `DriverSpecificMetrics` (GC collections, lock contentions, …).

The baseline and current passes share the pipeline run id and (for the current pass) the commit, so
to keep their `DerivedRunId`s distinct the baseline row uses `CommitHash = v<baselineVersion>` and
`IsComparableBase = true`; the current row uses the real commit and `IsComparableBase = false` with
the triggering branch name.

Source = BenchmarkDotNet **JSON "full"** exporter files (`*-report-full.json`). The exporter is
enabled in `Config/BenchmarkConfig.cs` (`JsonExporter.Full`).

### One-time database setup

Before ingestion can run, create the two tables (`PerfRun`, `PerfBenchmarkResult`) in the target
database, with columns matching the schema summarized above. No server-side ingestion mappings need
to be created: `ingest_kusto.py` sends a **self-contained inline JSON column mapping** built from
each payload's own property names (which are identical to the table's column names), so ingestion
does not depend on any pre-created named mapping existing on the cluster.

### Authentication

Ingestion runs in an `AzureCLI@2` task using the ADO **ARM service connection**
(`KustoServiceConnection` from the `ADX Cluster Variables` group). That connection's **service
principal** must be granted, on the target database:

- **Database Ingestor** — required to queue the ingestion, and
- **Database Viewer** — required for the post-ingestion verification queries. With Ingestor-only
  rights the data still lands, but the verify step cannot read it back and logs a warning naming
  this missing role.

`ingest_kusto.py` authenticates to Kusto with `with_az_cli_authentication` (the service connection
is already `az login`'d inside the task) and performs a **queued** ingestion against the
data-management (`ingest-`) endpoint.

### Running before the cluster exists

Ingestion is **conditional**: it only runs when the `enableKustoIngestion` parameter is `true` (the
default) **and** `KustoClusterUri`, `KustoDatabase` and `KustoServiceConnection` (from the `ADX
Cluster Variables` group) are all non-empty. Set `enableKustoIngestion` to `false` to opt a run out
of ingestion explicitly. Until a cluster and service connection are
configured, the pipeline still runs both passes, produces the comparison, and publishes the
translated NDJSON as the `perf-kusto-payloads` artifact for manual/backfill ingestion.

## Running the pipeline

1. Open the performance test pipeline in Azure DevOps and select **Run pipeline**.
2. Choose the branch to benchmark; override `baselineVersion` only if needed. Ingestion is on by
   default (`enableKustoIngestion`) and uses the `ADX Cluster Variables` group — populate
   `KustoClusterUri` / `KustoDatabase` / `KustoServiceConnection` there to enable it, leave them empty
   to skip ingestion, or untick **Ingest results into Kusto** to skip it for a single run.
3. After the run, review the **run summary** (comparison) and the `perf-results` /
   `perf-kusto-payloads` artifacts. When a baseline pass ran, the build is tagged
   **`Baseline <version>`** so the baseline used is visible at a glance in the ADO build list.

### Running the PR pipeline

1. Open the **PR** performance test pipeline (`sqlclient-perf-pr-pipeline.yml`) in Azure DevOps and
   select **Run pipeline**.
2. Choose the PR's branch to benchmark. Leave `baselineSourceRef` at `main` unless the PR targets a
   different branch (e.g. a release branch). No Kusto configuration is involved — PR results are
   never ingested.
3. After the run, review the **run summary** (comparison, labelled `<ref>@<short-sha>`) and the
   `perf-results` artifact. The build is tagged **`Baseline <ref>`**.

## Troubleshooting

| Symptom | Likely cause / fix |
| ------- | ------------------ |
| `NU1507` during the baseline pass | Multiple NuGet sources under CPM. The baseline uses a single-source config; ensure `perf-baseline-nuget.config` is being passed via `-p:RestoreConfigFile`. |
| Baseline restore fails to find MDS | `baselineVersion` isn't a published NuGet.org version, or the VM has no outbound access to `api.nuget.org`. |
| No comparison / summary | The baseline pass was skipped (empty `baselineVersion` / `baselineSourceRef`) or one pass produced no `*-report-full.json`. |
| Baseline source ref not found (PR pipeline) | `baselineSourceRef` is not a branch on `origin`, and the fallback `git clone --branch <ref>` of `baselineRepoUrl` also failed (ref does not exist there, or the VM has no outbound access to the remote). |
| Ingestion step skipped | `enableKustoIngestion` is `false`, or `KustoClusterUri`, `KustoDatabase` or `KustoServiceConnection` (from `ADX Cluster Variables`) is empty (expected until the cluster is provisioned). |
| Ingestion auth error | The service connection's SP lacks **Database Ingestor** on the target database. |
| "Kusto ingestion was queued, but the ingestion principal is not authorized to query the database" | The SP has **Database Ingestor** but not **Database Viewer**. Ingestion succeeded; grant **Database Viewer** so the verify step can confirm the rows landed. |
| `Kusto ingestion not yet queryable after Ns ... no ingestion failures were reported` (warning, step passes) | Expected, harmless: queued ingestion is asynchronous and small perf payloads can take longer than the verify window to become queryable. The step **warns and passes** because `.show ingestion failures` is clean, so the rows will land shortly. The step only **fails** when `.show ingestion failures` actually reports failures — in that case confirm the `PerfRun` / `PerfBenchmarkResult` tables exist with columns matching the schema above (a schema/column-name mismatch is the usual cause; the self-contained inline JSON mapping rules out a missing server-side named mapping). |
| Benchmarks not CPU-pinned | `PERF_CLIENT_CPUS` was not injected, or `taskset` is unavailable on the VM. |

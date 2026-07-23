# SqlClient Performance Test Pipeline

This directory contains the Azure DevOps pipeline and supporting scripts that run the
Microsoft.Data.SqlClient [BenchmarkDotNet](https://benchmarkdotnet.org/) performance tests on a
dedicated performance test lab (Azure Dedicated Hosts), compare the branch under test against a
released NuGet baseline, and optionally ingest the results into an Azure Data Explorer (Kusto)
database.

## Contents

| Path | Purpose |
| ---- | ------- |
| `sqlclient-perf-pipeline.yml` | The pipeline. Extends `v1/Perf.Test.Job.yml@PerfTemplates`. |
| `scripts/run-perf-tests.sh` | Linux on-VM entry point: install SDK, create DB, run benchmarks (interleaved or sequential), compare. |
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
      5. Current  pass  → MDS built from source (ProjectReference)              → results/current/
         (both pinned to PERF_CLIENT_CPUS; interleaved per-unit by default, or two full
          sequential passes when benchmarkRunMode=sequential)
      6. interleave_perf.py / compare_perf.py → results/comparison/ + results/summary.md
      ▼
ON THE AGENT  ── pipeline post-test steps
      • Show the BenchmarkDotNet markdown reports in the log.
      • perf_to_kusto.py → NDJSON for both passes (published as 'perf-kusto-payloads').
      • (optional) AzureCLI@2 + ingest_kusto.py → Kusto database.
```

The extends template only exposes **post-test** steps to consumers (no pre-build hook), so **both
benchmark passes run inside the on-VM script**. Translation and ingestion run on the **agent**
because that is where the pipeline's AAD identity / service connection and the native pipeline
context variables are available (the VM is behind NAT and lacks the pipeline identity).

## Parameters

| Parameter | Default | Description |
| --------- | ------- | ----------- |
| `platform` | `linux` | `linux` or `windows` VM + client. |
| `buildConfiguration` | `Release` | Only `Release` produces meaningful numbers. |
| `dotnetFramework` | `net9.0` | TFM the benchmarks run against (`net8.0`/`net9.0`/`net10.0`). |
| `testTimeoutMinutes` | `180` | Template timeout waiting for the VM run. |
| `sourcesSubDir` | `dotnet-sqlclient` | Folder the repo (`self`) is checked out into under the template's multi-repo checkout. Must match the ADO repo name. |
| `baselineVersion` | `7.0.2` | **Baseline Version** — released MDS the branch is compared against. Empty = current-only (no baseline pass / comparison). |
| `regressionThreshold` | `10` | Percent slowdown (current vs baseline mean) flagged as a regression. |
| `failOnRegression` | `false` | When `true`, a candidate-slower regression **fails** the run (gate). In interleaved mode only **confirmed** regressions (best-of-N majority) fail. Default off. |
| `benchmarkRunMode` | `interleaved` | `interleaved` (per-unit baseline↔candidate + best-of-N confirmation) or `sequential` (legacy two full passes). |
| `confirmationRuns` | `3` | Best-of-N: interleaved passes for a flagged unit before a regression is confirmed. `1` disables confirmation. Interleaved mode only. |
| `driverName` | `Microsoft.Data.SqlClient` | Recorded on every row (`DriverName` / `DerivedRunId`). |

### Kusto (Azure Data Explorer) ingestion variables

The ADX ingestion coordinates are **not** pipeline parameters — they come from a pipeline library
variable group named **`ADX Cluster Variables`** so no infrastructure identifiers are hard-coded in
the pipeline. The group must define:

| Variable | Description |
| -------- | ----------- |
| `KustoClusterUri` | ADX cluster URI, e.g. `https://<cluster>.<region>.kusto.windows.net`. Empty ⇒ ingestion skipped. |
| `KustoDatabase` | Target Kusto database. |
| `KustoServiceConnection` | Azure DevOps ARM service connection whose SP has ingest rights. Empty ⇒ ingestion skipped. |

Ingestion is gated at runtime: it only runs when both `KustoClusterUri` and `KustoServiceConnection`
are non-empty, so the pipeline still runs + compares before the cluster/service connection exist.

### Managing the baseline version

`baselineVersion` is **manually managed**. After each stable release is published to NuGet.org,
bump the `default` in `sqlclient-perf-pipeline.yml` (e.g. `7.0.2` → the next stable). It can also be
overridden at queue time without editing the pipeline.

## Two-pass build model

The `PerformanceTests` project references Microsoft.Data.SqlClient two ways, selected by MSBuild:

- **Current** (default): `ProjectReference` to the in-repo source — the branch under test.
- **Baseline**: `ReferenceType=Package` turns the reference into a `PackageReference`. Because the
  repo uses **Central Package Management (CPM)**, the version is pinned with `VersionOverride` via
  `-p:MdsPackageVersion=<version>` (a plain `Version` is ignored under CPM).

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
  `DriverName|CommitHash|PipelineRunId`), driver/machine/agent, pipeline id + build URL, branch +
  `BranchCategory`, `VersionString`, commit hash/date, `IsComparableBase`, `IngestedAt`.
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

Before ingestion can run, create the two tables (`PerfRun`, `PerfBenchmarkResult`) and their JSON
ingestion mappings (`PerfRun_json_mapping`, `PerfBenchmarkResult_json_mapping`) in the target
database. The mapping names are the ones referenced by `ingest_kusto.py`; the table columns match
the schema summarized above.

### Authentication

Ingestion runs in an `AzureCLI@2` task using the ADO **ARM service connection**
(`KustoServiceConnection` from the `ADX Cluster Variables` group). That connection's **service
principal** must be granted, on the target database:

- **Database Ingestor** (to ingest), and
- **Database Viewer** (recommended, for verification queries).

`ingest_kusto.py` authenticates to Kusto with `with_az_cli_authentication` (the service connection
is already `az login`'d inside the task) and performs a **queued** ingestion against the
data-management (`ingest-`) endpoint.

### Running before the cluster exists

Ingestion is **conditional**: it only runs when both `KustoClusterUri` and `KustoServiceConnection`
(from the `ADX Cluster Variables` group) are non-empty. Until a cluster and service connection are
configured, the pipeline still runs both passes, produces the comparison, and publishes the
translated NDJSON as the `perf-kusto-payloads` artifact for manual/backfill ingestion.

## Running the pipeline

1. Open the performance test pipeline in Azure DevOps and select **Run pipeline**.
2. Choose the branch to benchmark; override `baselineVersion` only if needed. Ingestion uses the
   `ADX Cluster Variables` group — populate `KustoClusterUri` / `KustoServiceConnection` there to
   enable it, or leave them empty to skip ingestion.
3. After the run, review the **run summary** (comparison) and the `perf-results` /
   `perf-kusto-payloads` artifacts.

## Troubleshooting

| Symptom | Likely cause / fix |
| ------- | ------------------ |
| `NU1507` during the baseline pass | Multiple NuGet sources under CPM. The baseline uses a single-source config; ensure `perf-baseline-nuget.config` is being passed via `-p:RestoreConfigFile`. |
| Baseline restore fails to find MDS | `baselineVersion` isn't a published NuGet.org version, or the VM has no outbound access to `api.nuget.org`. |
| No comparison / summary | The baseline pass was skipped (empty `baselineVersion`) or one pass produced no `*-report-full.json`. |
| Ingestion step skipped | `KustoClusterUri` or `KustoServiceConnection` (from `ADX Cluster Variables`) is empty (expected until the cluster is provisioned). |
| Ingestion auth error | The service connection's SP lacks **Database Ingestor** on the target database. |
| Benchmarks not CPU-pinned | `PERF_CLIENT_CPUS` was not injected, or `taskset` is unavailable on the VM. |

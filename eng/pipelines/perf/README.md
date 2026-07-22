# SqlClient Performance Test Pipeline

This directory contains the Azure DevOps pipeline and supporting scripts that run the
Microsoft.Data.SqlClient [BenchmarkDotNet](https://benchmarkdotnet.org/) performance tests on the
internal **Perf Test Lab** (Azure Dedicated Hosts), compare the branch under test against a released
NuGet baseline, and ingest the results into the InternalDriverTools **perf-results** Kusto database.

> Internal references:
> - Performance Test Automation — InternalDriverTools wiki page **284**
> - Performance Results Database Specification — InternalDriverTools wiki page **270**

## Contents

| Path | Purpose |
| ---- | ------- |
| `sqlclient-perf-pipeline.yml` | The pipeline. Extends `v1/Perf.Test.Job.yml@PerfTemplates`. |
| `scripts/run-perf-tests.sh` | Linux on-VM entry point: install SDK, create DB, two benchmark passes, compare. |
| `scripts/run-perf-tests.ps1` | Windows equivalent (ProcessorAffinity instead of `taskset`). |
| `scripts/compare_perf.py` | Compares baseline vs current BenchmarkDotNet JSON → delta (md + json). |
| `scripts/perf_to_kusto.py` | Translates BenchmarkDotNet "full" JSON → Kusto `PerfRun` + `PerfBenchmarkResult` NDJSON. |
| `scripts/ingest_kusto.py` | Queued Kusto ingestion (az CLI auth, runs on the agent). |
| `kusto/PerfRun.kql` | Create-table + JSON ingestion mapping for `PerfRun`. |
| `kusto/PerfBenchmarkResult.kql` | Create-table + JSON ingestion mapping for `PerfBenchmarkResult`. |

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
         (both pinned to PERF_CLIENT_CPUS)
      6. compare_perf.py → results/comparison/ + results/summary.md
      ▼
ON THE AGENT  ── pipeline post-test steps
      • Show the BenchmarkDotNet markdown reports in the log.
      • perf_to_kusto.py → NDJSON for both passes (published as 'perf-kusto-payloads').
      • (optional) AzureCLI@2 + ingest_kusto.py → Kusto perf-results database.
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
| `failOnRegression` | `false` | When `true`, a candidate-slower regression **fails** the run (gate). Default off — reports deltas without blocking. |
| `kustoClusterUri` | `https://sqldrivers.westus2.kusto.windows.net` | Kusto cluster URI. Blank ⇒ ingestion skipped. |
| `kustoDatabase` | `PerfResultsTestDB` | Target Kusto database. |
| `kustoServiceConnection` | `PerfLab Infra Deployments` | ADO ARM service connection whose SP has ingest rights. Blank ⇒ ingestion skipped. |
| `driverName` | `Microsoft.Data.SqlClient` | Recorded on every row (`DriverName` / `DerivedRunId`). |

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

## Reducing noise (wiki 339)

The harness applies the "[Your harness]"-owned controls from InternalDriverTools wiki 339
("Reducing Noise in Performance Tests"). The lab already supplies the isolated dedicated host, the
tuned SQL instance, and the disjoint client CPU set (`PERF_CLIENT_CPUS`); the run scripts add:

| Control | What the harness does |
| ------- | --------------------- |
| Client CPU pin (§2.4) | Pins the benchmark process to `PERF_CLIENT_CPUS` (`taskset` on Linux, `ProcessorAffinity` on Windows). |
| Fail loud (§2.10) | Preflight `SELECT 1` before any pass, **and** a post-pass guard that fails the run if a pass produced **zero** benchmark results — so an empty comparison can never be reported green. |
| Warm-up (§2.5) | Touches the target DB in the preflight to warm the buffer pool / plan cache before the first measured benchmark. |
| Allocator tuning (§2.8, Linux) | Exports `MALLOC_MMAP_THRESHOLD_=128MiB` and `MALLOC_TRIM_THRESHOLD_=-1` so large-buffer benches (`AsyncLargeDataRead`, `SqlBulkCopy`) stop re-`mmap`ing per iteration. |
| Network tuning (§2.9, Linux) | Best-effort `sysctl` to widen the ephemeral port range and enable `tcp_tw_reuse` for churn benches (`ConnectionPoolStress`, `ParallelAsyncConnection`). Never fails the run. |
| Diagnostics (§2.11) | Writes `results/diagnostics/`: SQL instance config (MAXDOP, memory, affinity, tempdb files, `@@VERSION`), host CPU topology, and per-pass CPU-clock/thermal telemetry (before/after each pass). |
| Regression gate (§3) | `failOnRegression` threads `--fail-on-regression` to `compare_perf.py`; only a **candidate-slower** delta past the threshold fails. Default off. |

### Proposed follow-ups (not yet implemented — larger redesign)

These are the high-value but structural controls from wiki 339 §2.2/§2.3/§2.6/§2.7. They change the
run model and total runtime, so they are called out here for a deliberate decision rather than folded
into a working harness silently:

- **Interleave candidate/baseline per small unit (§2.2)** — the biggest correctness fix. Today the
  harness runs two *full sequential* passes (baseline entirely, then current entirely), so the same
  benchmark is measured tens of minutes apart and any host drift skews one side. Interleaving would
  measure baseline vs candidate for each small unit back-to-back.
- **Split the monolithic exe into small bench binaries (§2.3)** — prerequisite for clean interleaving
  and per-unit re-runs.
- **Best-of-N auto-confirm (§2.6)** — re-run only the flagged benchmarks N times and require a
  majority to agree before declaring a regression. This should land **before** the gate
  (`failOnRegression`) is turned on by default, to keep it from flapping on single-sample noise.
- **Release-grade sampling / relaxed thresholds (§2.7)** — tune BenchmarkDotNet job counts and
  significance thresholds in `runnerconfig.jsonc` / `BenchmarkConfig.cs` once the above are in place.

## Kusto schema & ingestion

Schema follows wiki 270. Two tables:

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

Run the KQL scripts once against the target database (Kusto query editor, or
`.execute database script`):

```
kusto/PerfRun.kql
kusto/PerfBenchmarkResult.kql
```

They create the tables (`.create-merge`, idempotent) and the JSON ingestion mappings
(`PerfRun_json_mapping`, `PerfBenchmarkResult_json_mapping`) referenced by `ingest_kusto.py`.

### Authentication

Ingestion runs in an `AzureCLI@2` task using the ADO **ARM service connection**
(`kustoServiceConnection`). That connection's **service principal** must be granted, on the target
database:

- **Database Ingestor** (to ingest), and
- **Database Viewer** (recommended, for verification queries).

`ingest_kusto.py` authenticates to Kusto with `with_az_cli_authentication` (the service connection
is already `az login`'d inside the task) and performs a **queued** ingestion against the
data-management (`ingest-`) endpoint.

### Running before the cluster exists

Ingestion is **conditional**: it only runs when both `kustoClusterUri` and `kustoServiceConnection`
are supplied. Until the cluster and service connection are provisioned, the pipeline still runs both
passes, produces the comparison, and publishes the translated NDJSON as the `perf-kusto-payloads`
artifact for manual/backfill ingestion.

## Running the pipeline

1. In ADO, open **SqlClient-Performance-Tests** and select **Run pipeline**.
2. Choose the branch to benchmark. The Kusto parameters are pre-filled (cluster
   `sqldrivers.westus2`, database `PerfResultsTestDB`, service connection
   `PerfLab Infra Deployments`); override `baselineVersion` or the Kusto values only if needed.
3. After the run, review the **run summary** (comparison) and the `perf-results` /
   `perf-kusto-payloads` artifacts.

## Troubleshooting

| Symptom | Likely cause / fix |
| ------- | ------------------ |
| `NU1507` during the baseline pass | Multiple NuGet sources under CPM. The baseline uses a single-source config; ensure `perf-baseline-nuget.config` is being passed via `-p:RestoreConfigFile`. |
| Baseline restore fails to find MDS | `baselineVersion` isn't a published NuGet.org version, or the VM has no outbound access to `api.nuget.org`. |
| No comparison / summary | The baseline pass was skipped (empty `baselineVersion`) or one pass produced no `*-report-full.json`. |
| Ingestion step skipped | `kustoClusterUri` or `kustoServiceConnection` is blank (expected until the cluster is provisioned). |
| Ingestion auth error | The service connection's SP lacks **Database Ingestor** on the target database. |
| Benchmarks not CPU-pinned | `PERF_CLIENT_CPUS` was not injected, or `taskset` is unavailable on the VM. |

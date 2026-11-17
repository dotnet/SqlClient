# Async health within the performance benchmarks

Async-health measurements are part of the ordinary BenchmarkDotNet perf tests.
There is no standalone sweep, ramp prerequisite, separate case harness, or network shaping.
The existing pipeline flow and trigger conventions are unchanged.

## Benchmark coverage

| Unit | Regression signals |
| --- | --- |
| `OpenAsyncBurst` | 64 queued workers against empty, warm, oversubscribed (16-slot), and non-pooled connections, with separate open/close and open/query/close methods |

Only the newly added `OpenAsyncBurstRunner` carries async-health instrumentation.
Existing single/parallel connection, pool-contention and synchronous-pressure benchmarks
retain their original workloads and ordinary BDN measurements, without async-health
annotations or operation-recorder wrappers.

`OpenAsyncBurst` adds eight benchmark cases, not a preliminary suite. Each has one warmup,
three timing iterations and one invocation per iteration by default. Each completed batch
contains 64 connections; BDN normalizes timing/allocation results using `OperationsPerInvoke`.
It pins the worker minimum to 16 during each case and restores it afterward; actual minimum,
maximum and observed thread counts are retained with the diagnostic evidence.
Warm setup holds all connections simultaneously before returning them. Empty-pool setup
clears only that benchmark's pool. Query work keeps contention and network-related effects
visible; open-only work isolates acquisition/return overhead.

Pool version, SNI and optimized async behavior come from the normal runner config.
Use separate process configurations to compare V1/V2; the cached pool switch is not flipped
between cases. A requested V2 diagnostic fails explicitly if the loaded driver cannot apply it.
No experimental LongRunning driver switch is added.

## Measurement boundaries

`AsyncHealthDiagnoser` observes two parts of BDN's existing lifecycle separately:

1. **Startup/warmup:** starts before the engine runs and ends before actual timing iterations.
   This retains first-open scheduling and thread-growth evidence that warmup would otherwise
   discard. This window includes fixture initialization, and is labeled `Startup`.
2. **Extra diagnostic iteration:** observes BDN's extra iteration after timing. This window
   excludes iteration setup/cleanup on successful execution and is labeled `ExtraIteration`.

Neither window adds a separate workload sweep. Probe production and operation recording are
disabled during normal timing iterations. Instrumented burst benchmark code takes a null-recorder
branch during timing; baseline and candidate use identical instrumentation.
Diagnostic resource/allocation counts include observer bookkeeping; they are not driver-only
allocation counts. For isolated allocation profiling, disable async health locally.

`FirstBenchmarkInProcess` identifies the first BDN case in that process. Only that case can
expose initial process/driver behavior; subsequent cases may reuse initialized driver/JIT/worker
state even when their SQL pool is empty. Startup is not a claim that every case is process-cold.
Interleaved pipeline execution already starts a fresh process for each benchmark unit and side.

## Captured signals

The shared dedicated-thread observer records intended probe cadence, actual posting, actual
execution, missing/rejected/dropped/outstanding probes, and a bounded recovery interval.
Raw resources include all process threads (including dedicated threads), ThreadPool threads,
queue depth, allocated bytes and Gen2 collection counts.

Marked benchmark methods also record their real operations during those diagnostic windows:

- Offer-to-worker-start queue delay, open duration and offer-to-open acquisition latency.
- Completed work throughput; started/opened/completed/failed/cancelled counters.
- Inline versus pending open completion, failed-open duration and cancelled-operation duration.
- Queued/in-flight operations and missing first-worker outcomes.
- Invocation and worker identities, so repeated invocations do not overwrite early evidence.

The recorder retains every worker's first record, including queued or incomplete work, even
after the normal 100,000-record cap. Dropped-record counts remain explicit. Snapshots freeze
the evidence so late completions cannot mutate a saved result. Exception types and SQL error
numbers are retained, never exception messages or connection strings.

Successful-only latency cannot establish health when failures or missing work exist.
Operation evidence is explicitly complete, truncated, incomplete, failed or not instrumented.
Short scheduler windows (less than 2.5 seconds or 100 probes) and censored windows are
insufficient, not healthy. The 100 ms dispatch-p99 finding is an investigative diagnostic
budget, not a product SLA or a new unconditional pipeline gate.

## Pipeline integration and artifacts

Both shared entrypoints explicitly enable the diagnoser and `OpenAsyncBurst`, including with
an older private runner config. Existing burst tuning values are preserved. BDN still runs
through the existing interleaved/sequential machinery; there is no preceding health phase.
The existing `failOnRegression` timing-comparison policy is unchanged.

Candidate-owned benchmark and diagnoser sources are compiled against both driver versions.
Source baselines use `MdsSourceRoot` to reference the baseline driver and matching TestCommon;
older baselines need not already contain the new benchmark unit or diagnostic hooks.
Package baselines use the existing `MdsPackageVersion` override.

Per-side/per-repetition `async-health/*.json` contains schema-versioned benchmark identity,
phase, settings, actual driver hash, raw probes/resources/operation records and derived summaries.
These are retained in the normal perf-results artifact, even after a failed interleaved run.
Successful runs also produce `benchmark-async-health.md` at the artifact root. Diagnostic
records are not extra timing rows in BDN comparison or Kusto ingestion.

### Compact comparison

The report contains one baseline/current row per burst method, parameters and compatible
recorded configuration. It reduces Startup, ExtraIteration and available repetitions to
independent maxima for dispatch p99, first-worker queue p99, peak ThreadPool threads and peak
total process threads. It compares existing per-window percentiles; it does not pool samples
or calculate a new percentile. Different metrics may attain their maxima in different cohorts.

Each metric cell shows `current maximum (signed percentage delta; signed raw difference)`.
The difference is current minus baseline; the percentage divides that difference by the
baseline. Missing values and every zero baseline, including zero-to-zero, produce `N/A`
percentages. Rows sort by descending unrounded dispatch-p99 percentage delta, undefined last,
with deterministic ties.

The leading status uses the regular comparison's indicators and the pipeline's actual
`regressionThreshold` (default 10%). Dispatch increases strictly above the threshold are red,
decreases strictly below its negative are green, and boundary equality is neutral/white.
Current-only, baseline-only and undefined comparisons retain the new/removed/unknown indicators.
This is an **observed relative change**, not the normal timing report's best-of-N majority
confirmation and not an absolute scheduler-health verdict. It adds no regression failure gate.

Pool/SNI/async selections, runtime, OS, CPU/worker counts, process-cold position and recorded
workload settings must match. Incompatible configurations remain unpaired, with explicit
new/removed rows rather than misleading deltas. This also applies to intentional switch
experiments whose settings differ. Process IDs and driver identities may differ across sides;
mixing multiple driver identities within one side/configuration is rejected.

Coverage warnings remain outside the compact table. Expanded provenance retains each side's
health classifications, phase completeness, sample/missing-probe counts, operation problems,
driver identity, repetition, process ID, raw artifact aliases and the cohort(s) supplying every
maximum. Identical aliases are counted once; conflicting data/origins fail explicitly. A green
relative indicator never hides insufficient, censored, incomplete or failed evidence.

To regenerate a historical preview without replacing the original report:

```powershell
python eng\pipelines\perf\scripts\benchmark_health_report.py `
    --results-dir <extracted-results> --threshold 10 `
    --out-md <preview-directory>\historical-comparison.md `
    --source-label "Historical run data, regenerated with the current report format"
```

### Execution and collection

Benchmark exceptions, missing diagnostic windows, observer faults and incomplete instrumented
work fail explicitly. Failed-window evidence and execution-failure markers are separate files.
The normal per-case timeout remains enforced. The test-only BDN packages use
`0.16.0-preview.1` because `0.15.8` lacks the necessary in-process lifecycle hooks.
SQL jobs explicitly use `JitTieringMode.Skip`: the preview's automatic tier-promotion stage
otherwise adds about 90 complete workload invocations, including repeated fixtures, beyond
the configured warmup. This does not disable runtime tiered compilation or reduce configured
warmups, timing iterations, operations, or diagnostic iterations. Reclassifying long-running
jobs as `Monitoring` is separate work; existing strategies are unchanged.
For local native/interactive profiling, disable `EnableAsyncHealth`; pipeline runs enable it.

The VM runner reserves the final ten minutes of `testTimeoutMinutes` for the template's
completion poll and artifact collection. Build/setup elapsed time counts against the remaining
benchmark budget. An expired benchmark budget terminates owned benchmark processes and fails
explicitly, but leaves the collector alive. Interleaved raw logs/reports live under
`perf-results/runs`; per-unit `.timing.json` records retain elapsed time and exit status.
This protects benchmark-timeout evidence; a hung SDK installation or an externally killed
agent/VM can still prevent collection.

## Limits

These are modern-.NET benchmarks on the selected endpoint/authentication/SNI configuration.
They do not cover every authentication, timeout, failover, transaction or Framework branch.
No artificial RTT is added. A short batch may have insufficient probe samples while still
providing useful queue/open/outcome measurements. Process thread peaks can occur between the
25 ms resource samples; raw data and explicit missing evidence must inform interpretation.

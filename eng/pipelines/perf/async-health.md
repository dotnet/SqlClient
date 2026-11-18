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

## Execution and workload settings

Every `RunnerJob`, including command and large-data subclasses, supports:

| Setting | Default | Meaning |
| --- | --- | --- |
| `ProcessIsolation` | `PerRunner` | `PerRunner` shares a benchmark process across the selected cases of one unit; `PerCase` starts a fresh process for each selected method/parameter case. There is no `Auto` and no diagnoser override. |
| `AdditionalRttMilliseconds` | `0` | Additional round-trip SQL delay. Positive values require supported Linux loopback TCP routing and successful owned netem setup. |

The checked-in burst job explicitly selects `PerCase`. The same dispatcher is used by direct
CLI and pipeline runs. It verifies an exact selected-case fingerprint in the child, retains
per-process manifests and separate artifact directories, and stops on child failure.
BenchmarkDotNet still uses its in-process toolchain inside each child, so AppContext switches
apply to the process doing the work.

`OpenAsyncBurstRunnerJob` also supports `SamplingProfile`:

- `Burst` (default) retains one 64-worker wave and one operation per worker.
- `Extended` preserves that single wave for `Empty`. Other states run 16 successive waves
  of 64 concurrent workers, giving 1,024 distinct worker starts per invocation.
  `SteadyOperationsPerWorker` supplies a fixed positive count keyed by
  `OpenClose/Warm`, `OpenClose/Oversubscribed`, `OpenClose/NonPooled`, and the corresponding
  `OpenQueryClose/...` cases. Missing counts fail rather than being silently calibrated.

Burst jobs require `Monitoring`, one launch, one warmup and one invocation per iteration.
Additional BDN warmups/invocations would mix later warm work into the startup cohort;
invalid settings fail explicitly. Timing iteration count remains configurable.

The same workload definition drives execution and BDN's `OperationsPerInvoke`. For example,
16 waves with 20 operations per worker execute 20,480 operations, not 64.
First records for every logical worker start remain retained even when ordinary operation
records reach their cap. Other dropped records are still reported as truncation.
Interleaved extended runs use all configured repetitions even without a timing regression;
there is no additional nested confirmation loop.

Fixed counts must be selected for the intended RTT and compared unchanged across both sides.
Sustained non-pooled work can exhaust local TCP source ports; that is a real failed workload,
not a reason to ignore socket errors or modify a shared host's TCP settings automatically.

### Calibrated 100ms example

[`profiles/openasync-extended-100ms.jsonc`](../../../src/Microsoft.Data.SqlClient/tests/PerformanceTests/profiles/openasync-extended-100ms.jsonc)
is an opt-in, burst-only example. It does not change the default pipeline profile.
On the local 16-CPU Linux test host, both the current driver and 7.1.2 produced
25-second-plus ExtraIteration windows with no missing probes for all six steady cases:

| Method | State | Operations per worker per wave | ExtraIteration duration, baseline/current |
| --- | --- | ---: | --- |
| OpenClose | Warm | 32768 | 36.0s / 35.2s |
| OpenClose | Oversubscribed | 23114 | 31.6s / 31.4s |
| OpenClose | NonPooled | 1 | 50.9s / 51.3s |
| OpenQueryClose | Warm | 20 | 32.9s / 32.9s |
| OpenQueryClose | Oversubscribed | 5 | 32.8s / 32.8s |
| OpenQueryClose | NonPooled | 1 | 52.6s / 52.5s |

These windows retained 1,258-2,104 dispatch probes and all 1,024 first-worker starts each.
The open-only warm/oversubscribed cases still reported scheduler-health failures on both
drivers: sustained hot-loop scheduling/CPU pressure is not the same scenario as the isolated
cold-start reproduction. Their ordinary operation records also reached the retention cap;
first-worker records remained complete. Adequacy is not a promise of health or a confidence
interval. Always retain those qualifications when comparing the results.

Calibration used one warmup, one timing iteration and the normal diagnostic extra iteration,
with identical fixed work on both sides. The example retains three timing iterations; its
full three-repetition runtime was not measured. Added RTT also lengthens fixture prewarming,
which took much more time than short open-only observations during initial calibration.
The zero-RTT extended matrix and additional cold-repeat/observer-overhead study were deferred.
Do not treat this example as a calibrated zero-RTT profile or extrapolate its evidence to a
different host without checking the recorded adequacy.

## Optional SQL-only latency

The outer dispatcher applies netem before starting the measured child and removes its owned
rules afterward, including failure and cooperative cancellation. A 100ms setting adds 50ms
per direction; odd millisecond values are represented exactly as half-delays in microseconds.
Only TCP packets to/from the selected local SQL address and port use the delayed band.

The initial implementation supports Linux loopback routes, not remote endpoints, named-instance
discovery or Windows shaping. An explicitly requested unsupported condition fails. A zero value
does not require Python, `tc`, elevated permissions, or any network mutation.
Positive values require Python 3, iproute2 and noninteractive permission to invoke `tc`.
Use an exclusive SQL benchmark environment: other processes using the same endpoint would also
experience its shaped traffic while the case runs.

The helper refuses foreign queues, serializes queue mutation, persists ownership before changing
the interface, verifies the applied delay, waits for queued packets to drain, and checks for
matched traffic, changed settings and packet drops. It never deletes a replacement owned by
another run. Forced process termination cannot guarantee `finally` execution; retained
`network-state.json` supports explicit recovery with `netem.py stop --state ... --output ...`.
An unrecovered queue is a conflict, not permission for the next run to overwrite it.

Requested/applied RTT, requested/effective isolation, workload sizes and observer settings are
recorded as comparison conditions. Rule handles and traffic counters remain per-process
`network.json` evidence. Credentials are not passed to the helper or written to these artifacts.
Ordinary BDN timing comparisons also use the retained execution conditions, including for
unmarked runners. Different RTT/isolation/workload settings remain unpaired; missing manifests
in the new isolated-process layout fail explicitly rather than allowing a misleading delta.

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
Scheduler p99 adequacy requires at least 25 seconds, 1,000 executed probes and no missing
dispatch evidence. Resource completeness remains a separate requirement for a health pass.
First-worker queue p99 records its own sample count and adequacy: all expected starts must be
observed and at least 1,000 samples retained. A cold 64-worker wave's nearest-rank p99 is its
maximum; it remains explicitly sparse rather than a precise population-p99 estimate.
These are evidence floors, not confidence-interval guarantees.
The producer retains its 25ms cadence and allows at most 256 outstanding probes.
Short or censored windows are insufficient, not healthy. The 100 ms dispatch-p99 finding is an investigative diagnostic
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
No artificial RTT is added by default. A short batch may have insufficient probe samples while still
providing useful queue/open/outcome measurements. Process thread peaks can occur between the
25 ms resource samples; raw data and explicit missing evidence must inform interpretation.

# Async health within the performance benchmarks

Async-health observation runs inside BenchmarkDotNet, not in a separate sweep or prerequisite.
Only `OpenAsyncBurstRunner.OpenQueryClose` is instrumented. Its four states are `Empty`, `Warm`,
`Oversubscribed` (16 pool slots), and `NonPooled`; other benchmarks and pipeline triggers are unchanged.
Open/close-only bursts were removed because tight synchronous checkout loops can themselves
saturate the ThreadPool, amplified by recorder contention, without identifying blocked async I/O.

## Execution and workload settings

Every `RunnerJob` supports these settings, independently of diagnoser annotations:

| Setting | Default | Behavior |
| --- | --- | --- |
| `ProcessIsolation` | `PerRunner` | Share one process per runner, or use `PerCase` for each selected method/parameter case. No `Auto` or annotation override. |
| `AdditionalRttMilliseconds` | `0` | Additional SQL RTT; positive values require verified Linux loopback netem. Unsupported requests fail. |

Direct CLI and pipeline execution share the dispatcher. It verifies the child's selected-case
fingerprint and keeps separate manifests/artifacts. BDN uses the in-process toolchain inside
each child so process-wide pool/SNI/async switches apply. Compare pool versions in separate
processes; requesting V2 fails if the driver cannot apply it. No experimental driver switch is added.

The burst job defaults to `PerCase`, `Monitoring`, one launch/warmup/invocation, three timing
iterations and a five-minute case timeout. Repeated launches/warmups/invocations are rejected
to protect startup observations. Each case sets the worker minimum to 16 and restores it afterward.
Warm setup holds all 64 connections (16 for Oversubscribed) before returning them; Empty clears
only its own pool. Query work keeps network waits and pool contention visible.

| `SamplingProfile` | Work per invocation |
| --- | --- |
| `Burst` (default) | One wave of 64 workers, one operation each. |
| `Extended` | Empty remains one cold wave. Other states use 16 waves (1,024 worker starts), with fixed `SteadyOperationsPerWorker` counts. |

Extended counts are required for `OpenQueryClose/Warm`, `OpenQueryClose/Oversubscribed` and
`OpenQueryClose/NonPooled`; remove retired `OpenClose/...` entries from private configs.
Invalid/missing counts fail rather than being auto-calibrated. The complete work count drives
BDN normalization: 16 waves of 64 workers doing 20 operations means 20,480, not 64.
Both sides use identical counts. Extended jobs use all configured interleaved repetitions,
without nesting another confirmation loop.

### Calibrated 100ms example

Select `platform=linux`, `runnerProfile=openasync-extended-100ms`, and `useConnectionPoolV2=true`
to use the [checked-in profile](../../../src/Microsoft.Data.SqlClient/tests/PerformanceTests/profiles/openasync-extended-100ms.jsonc).
It replaces the full-suite config; private credential injection/redaction and queue-time switch
overrides still apply. The normal `default` profile stays unshaped. `confirmationRuns=1` requests
one matched pass without reducing BDN iterations.

Local 16-CPU Linux calibration against current/7.1.2 used one warmup, one timing iteration and
the diagnostic extra iteration. These fixed counts produced:

| State | Operations/worker/wave | ExtraIteration duration, baseline/current |
| --- | ---: | --- |
| Warm | 20 | 32.9s / 32.9s |
| Oversubscribed | 5 | 32.8s / 32.8s |
| NonPooled | 1 | 52.6s / 52.5s |

Each window retained 1,312-2,104 dispatch probes, 1,024 first-worker starts and no missing probes.
The profile keeps three timing iterations and a ten-minute timeout; its full three-repetition
runtime was not calibrated. RTT also lengthens prewarming. This is not a calibrated zero-RTT
profile or a guarantee for other hosts; check the recorded sampling evidence. Sustained non-pooled
work can exhaust TCP source ports: retain that failure rather than changing shared-host TCP settings.

## Optional SQL-only latency

The dispatcher installs netem before starting the child and removes only its owned rules after
execution, including failure/cooperative cancellation. A 100ms RTT adds 50ms per direction;
odd milliseconds use exact half-delays in microseconds. Filters target the resolved local SQL
addresses and TCP port. Other clients using that endpoint are affected too: use an exclusive host.

Positive RTT requires Linux loopback routing, Python 3, iproute2 and noninteractive `tc` permission.
Remote endpoints, named-instance discovery and Windows shaping fail explicitly. Zero RTT needs
none of those tools or privileges and does not mutate networking.

The helper serializes mutation, refuses foreign queues, persists ownership before installation,
checks applied delay/settings, drains queued packets, and verifies matched traffic and no drops.
Forced termination cannot guarantee cleanup; use retained state with
`netem.py stop --state <network-state.json> --output <network.json>` for explicit recovery.
Never replace an unrecovered or foreign queue. Credentials never reach the helper/evidence.

## Measurement boundaries and evidence

The diagnoser records **Startup** from before BDN engine execution until actual timing starts
(including fixture initialization), and **ExtraIteration** around diagnostic workload only
(excluding successful setup/cleanup). Ordinary timing iterations have no producer/recorder.
`FirstBenchmarkInProcess` identifies process-cold position: later PerRunner cases can reuse
driver/JIT/thread state even after clearing their SQL pool.

A dedicated thread posts ThreadPool probes every 25ms, with at most 256 outstanding. Raw evidence
retains scheduled/posted/executed times; missing, rejected, dropped and outstanding probes;
recovery; process/ThreadPool thread counts; queue depth; allocations and Gen2 collections.
Missing thread peaks are null, not zero; the process peak includes its recorded start count.

Operation evidence retains offer/start/open/end times, inline/pending opens, worker/invocation
identity, throughput and completed/failed/cancelled/queued/in-flight outcomes. Every worker's
first record survives the ordinary 100,000-record cap; truncation remains explicit. Snapshots
freeze results. Errors include only types/SQL numbers, not private messages or connection strings.

Dispatch-p99 adequacy requires at least 25 seconds, 1,000 executed probes and no missing evidence.
First-worker adequacy requires 1,000 retained starts and complete outcomes; resource completeness
is separate. Cold 64-start p99 is its observed maximum, not a precise population estimate.
Short/censored/incomplete windows are insufficient. The raw per-window 100ms investigative budget
is not an SLA or CI gate; it remains distinct from the report's current-latency health bands.
Observer bookkeeping affects diagnostic allocation/CPU measurements; it is not driver-only cost.

## Pipeline integration and artifacts

Both entrypoints enable health and the burst unit, preserving existing tuning in private configs.
The candidate-owned harness builds against both source/package drivers (`MdsSourceRoot` or
`MdsPackageVersion`); old baselines need not already contain these benchmarks.
The test-only BDN preview supplies lifecycle hooks. `JitTieringMode.Skip` avoids about 90 extra
automatic preparation calls without disabling runtime tiering or reducing configured work.
Use `EnableAsyncHealth=false` for local native/interactive profiling.

Failed benchmarks, missing windows, observer faults and incomplete work fail explicitly. Window
and execution-failure records remain separate. Per-case cancellation and the shared run budget
leave ten minutes of `testTimeoutMinutes` for collection. Partial logs/reports under `results/runs`,
per-unit timing JSON and process/network manifests survive normal timeouts, though externally
killed VMs or hung setup can still prevent collection. Ordinary regression gates are unchanged.

### Compact comparison

`benchmark-async-health.md` pairs matching configurations by method/parameters and takes independent
maxima across phases/repetitions for dispatch p99, first-worker queue p99 and both thread peaks.
It never pools percentiles. Cells show current values with signed percentage/raw baseline deltas.
Rows sort by descending unrounded dispatch percentage delta, undefined last, with deterministic ties.
Missing/zero-baseline percentages are N/A. Current dispatch p99 is green below 1ms, yellow from
1-10ms inclusive, red above 10ms, and unknown when missing, independently of baseline/gating.

Coverage warnings and expanded provenance retain phase/sample/outcome completeness, driver hashes,
all aliases and each maximum's source. Conflicting aliases/origins or mixed driver identities fail;
incompatible execution conditions remain unpaired, including ordinary timing comparisons.
A green/yellow indicator does not waive missing evidence. Historical artifacts retain retired cases.
To regenerate without overwriting an original:

```powershell
python eng\pipelines\perf\scripts\benchmark_health_report.py `
    --results-dir <extracted-results> --out-md <preview>\historical-comparison.md `
    --source-label "Historical run data, regenerated with the current report format"
```

These are modern-.NET benchmarks for the selected endpoint/authentication/SNI configuration, not
coverage of every authentication, timeout, failover, transaction or Framework path. Thread peaks
can occur between samples; retain raw data and qualifications when interpreting any result.

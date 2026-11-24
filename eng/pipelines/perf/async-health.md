# Async health within the performance benchmarks

Async-health observation runs inside BenchmarkDotNet for suites annotated with
`[AsyncHealthDiagnoser]`, not in a separate sweep or prerequisite.

## Execution settings

Execution settings are independent of diagnoser annotations:

| Setting | Default | Behavior |
| --- | --- | --- |
| `ProcessIsolation` | `PerRunner` | Share one process per runner, or use `PerCase` for each selected method/parameter case. No `Auto` or annotation override. |
| `AdditionalRttMilliseconds` | `0` | Added SQL RTT. Positive values require verified Linux loopback netem. |

Direct CLI and pipeline execution share the dispatcher. It verifies the child's selected-case
fingerprint and keeps separate case artifacts. BDN uses the in-process toolchain inside
each child so process-wide pool/SNI/async switches apply. Compare pool versions in separate
processes; requesting V2 fails if the driver cannot apply it. No experimental driver switch is added.

## SQL-only latency

The dispatcher installs netem before starting the child and removes only its owned rules after
execution, including failure/cooperative cancellation. A 100ms RTT adds 50ms per direction;
odd milliseconds use exact half-delays in microseconds. Filters target the resolved local SQL
addresses and TCP port. Other clients using that endpoint are affected too: use an exclusive host.

Positive RTT requires Linux loopback routing, Python 3, iproute2 and noninteractive `tc` permission.
Remote endpoints and named-instance discovery fail explicitly when shaping is requested.
Zero added RTT needs no shaping tools or privileges. The parent passes the verified RTT directly
to the child, where the diagnoser records it in the health results.

The helper serializes mutation, refuses foreign queues, persists ownership before installation,
checks applied delay/settings, drains queued packets, and verifies matched traffic and no drops.
The ownership file is temporary, outside the artifacts directory. If cleanup fails, use
`netem.py stop --state <path>` on the same host. The state is in a `sqlclient-perf-netem-*`
directory under the system temp directory. Never replace an unrecovered or foreign queue.

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

Operation evidence retains offer/start/end times, worker/invocation identity, throughput and
completed/failed/cancelled/queued/in-flight outcomes. Workloads can also opt into recording
connection-open timing and inline/pending opens. Every worker's
first record survives the ordinary 100,000-record cap; truncation remains explicit. Snapshots
freeze results. Errors include only types/SQL numbers, not private messages or connection strings.

Dispatch-p99 adequacy requires at least 25 seconds, 1,000 executed probes and no missing evidence.
First-worker adequacy requires 1,000 retained starts and complete outcomes; resource completeness
is separate. A first-worker p99 below 100 samples is the observed maximum, not a precise estimate.
Short/censored/incomplete windows remain insufficient regardless of the report's health color.
The report derives complete/missing/truncated/invalid resource evidence from raw observations.
Observer bookkeeping affects diagnostic allocation/CPU measurements; it is not driver-only cost.

New artifacts use schema 3: summaries retain the four displayed metrics and completeness fields,
not the old 100ms `Health` verdict or extra producer/open/failure/cancellation latency percentiles.
Throughput remains in `Operations.CompletedPerSecond`; raw timestamps/outcomes are unchanged and
support recalculating other metrics from retained samples, subject to recorded truncation.
The reader still accepts schema 2; missing raw resource metadata is reported as `Unknown`.

## Pipeline integration and artifacts

The candidate-owned harness builds against both source/package drivers (`MdsSourceRoot` or
`MdsPackageVersion`); old baselines need not already contain these benchmarks.
The test-only BDN preview supplies lifecycle hooks. `JitTieringMode.Skip` avoids about 90 extra
automatic preparation calls without disabling runtime tiering or reducing configured work.
For native/interactive profiling, select a suite without `[AsyncHealthDiagnoser]`.

Failed benchmarks, missing windows, observer faults and incomplete work fail explicitly. Window
and execution-failure records remain separate. Completed diagnostic artifacts are collected even
when a benchmark process returns a failure. The pipeline task deadline can still interrupt
collection. Ordinary regression gates are unchanged.
After a timing comparison fails its gate, the runners still generate the async-health report
from collected evidence before returning failure. A report-generation failure also fails the run.

### Compact comparison

`benchmark-async-health-report-github.md` pairs matching configurations by method/parameters. It
takes independent maxima across phases/repetitions for dispatch p99, first-worker queue p99 and both
thread peaks.
It never pools percentiles. Cells show current values with signed percentage/raw baseline deltas.
Rows sort by descending unrounded dispatch percentage delta, undefined last, with deterministic ties.
Missing/zero-baseline percentages are N/A. Current dispatch p99 is green below 1ms, yellow from
1-10ms inclusive, red above 10ms, and unknown when missing, independently of baseline/gating.

Coverage warnings and expanded provenance retain phase/sample/outcome completeness, driver hashes,
all aliases and each maximum's source. Conflicting aliases/origins or mixed driver identities fail;
incompatible execution conditions remain unpaired in the async-health report.
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

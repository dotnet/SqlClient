# Authenticated OpenAsync health: RTT, concurrency, and scheduling

## Result

**The reported problem reproduced in all three repetitions.** With pooled V2, 64 workers,
pool capacity 100, ThreadPool min/max 16/32767, and **100 ms added RTT**, dispatch p99 was
**2.690-2.791 seconds** (median **2.777 seconds**). The largest one-second completion-cohort
p99 was 2.740-2.841 seconds. This is not just the earlier SQL-free control: these runs performed
successful authenticated `OpenAsync` / `SELECT 1` / close cycles against SQL Server.

The authenticated sweep completed **240 fresh processes**: 16 configurations, five added RTTs,
three repetitions each. Both pooled implementations and non-pooled opens were tested at 64
workers over the entire RTT ladder. V2 additionally covered 16, 32, 128, and 256 workers.
The sweep removed its netem rules successfully; loopback returned to `noqueue`.

The previous Windows zero-delay and stalled-prelogin reports remain separate historical controls.
They are not mixed into these Linux/WSL graphs. See **authenticated-report.html** for seven
graphs and every individual repetition, and `wsl-sql-rtt/` for unchanged raw JSON and manifests.

## Experiment definition

- Real local SQL Server inside WSL; managed SNI; 16 runtime-visible logical processors.
- Added RTT: 0, 20, 50, 100, and 200 ms. **100 ms RTT = 50 ms delay per direction.**
- Default case: pooled V2, cold process/pool, pool max 100, TP min 16 / max 32767,
  `OpenAsync` / `ExecuteScalarAsync("SELECT 1")` / close, continuous worker loops.
- Eight-second observation, 25-ms scheduler probes, capacity 64, one-second probe recovery,
  cancellation at the observation deadline, followed by bounded worker drain.
- Connect timeout: 15 seconds; retry count zero; pool blocking-period error caching disabled.
- One process per case/repetition; randomized order within each RTT block. RTT blocks ran in
  order 100, 0, 20, 50, 200, so elapsed-time/server-state effects are not perfectly randomized.
- Warm-pool control pre-opens and holds 64 connections before returning them, outside the
  observation. It also warms driver/runtime initialization; this is not an isolated pool-only change.
- Three repetitions are observed ranges, not confidence intervals. This is a developer workstation,
  not a reserved performance lab; no production capacity guarantee follows.

All 240 records completed and reconciled started operations with completed/failed/cancelled
outcomes. Nonzero-delay netem records show SQL packets traversing the queue, zero drops, and
successful cleanup. No password was persisted in experiment files.

## Concurrency and latency multiply cold-start disruption

Median dispatch p99 in milliseconds, three repetitions per cell:

| V2 workers | RTT 0 | RTT 20 | RTT 50 | RTT 100 | RTT 200 |
|---|---:|---:|---:|---:|---:|
| 16 | 698 | 606 | 753 | 904 | 904 |
| 32 | 586 | 1,072 | 1,253 | 1,119 | 1,588 |
| 64 | 1,286 | 1,940 | 2,106 | 2,777 | 4,248 |
| 128 | 2,153 | 2,437 | 3,088 | 4,153 | 6,547 |
| 256 | 2,117 | 2,398 | 3,012 | 4,232 | 6,552 |

Using the exploratory **100-ms dispatch p99 budget**, none of the default cold V2 points from
16 through 256 workers passed. This does not locate a healthy threshold below 16 workers;
those lower counts were not measured. The result is a failure region for these cold starts,
not a universal limit on asynchronous SQL concurrency.

At 100 ms RTT, increasing workers from 64 to 128 increased cold-window throughput only from
397 to 455 completed cycles/sec while scheduler p99 grew from 2.777 to 4.153 seconds.
At 256 workers, throughput was about 446/sec with 4.232-second scheduler p99.
At 200 ms RTT, 128/256 workers completed only about 79 cycles/sec, versus 140/sec at 64:
more queued demand did not produce more useful work during the cold window.

## V1, V2, non-pooled, and experimental scheduling at 100 ms RTT

All entries below use 64 workers. Values are medians of three repetitions.

| Mode or setting | Dispatch p99 ms | Completed cycles/sec | Peak process threads | First acquisition from offer p99 ms |
|---|---:|---:|---:|---:|
| V2, default | 2,777 | 397 | 29 | 3,536 |
| V2, LongRunning | 14.5 | 454 | 91 | 2,436 |
| V1, cold | 0.9 | 55 | 35 | 7,512 |
| Non-pooled | 1.8 | 20 | 45 | 3,713 |
| V2, prewarmed | 13.4 | 616 | 27 | 56.9 |

**Low scheduler delay does not mean fast SQL acquisition.** V1 kept the scheduler responsive,
but cold pool ramp-up and first-acquisition latency were much worse in this eight-second window.
This does not prove V1 is slower at steady state with a fully populated pool.
Non-pooled opens also kept scheduler p99 low but paid physical connection establishment on
every cycle and achieved much less throughput.

**LongRunning is a useful diagnostic comparison, not a demonstrated production solution.**
It reduced V2 scheduler p99 by roughly 99.5% in this case and increased completed cold-window
work about 14%, but peak process threads grew from 29 to 91. At 256 workers / 100 ms RTT,
it reached median scheduler p99 81 ms, approximately 609 cycles/sec, and 148 process threads.
At zero injected RTT, that 256-worker LongRunning case failed the scheduler budget in all three
runs (124-315 ms), despite passing at 50/100/200 ms RTT. The relationship is not monotonic.

LongRunning changes the initial dispatch thread; it does not make physical connection creation
asynchronous. Continuations after an incomplete await can still run on the ThreadPool, and
dispatching an acquisition that merely waits for an available connection can create needless
dedicated-thread churn. These experiments do not establish a safe global recommendation.

## ThreadPool knobs are not substitutes for an asynchronous open path

At 64 workers / 100 ms RTT:

| V2 setting | Dispatch p99 ms | Completed cycles/sec | Peak process threads |
|---|---:|---:|---:|
| TP min 16 / max 32767 | 2,777 | 397 | 29 |
| TP min 64 / max 32767 | 908 | 458 | 76 |
| TP min 16 / max 32 | 2,772 | 397 | 29 |
| TP min 16 / max 16 | 3,104 | 377 | 27 |
| Pool max 16, default TP | 911 | 134 | 29 |

Raising the minimum reduced the stall but left nearly a second of scheduler delay and used
many more threads. Tightening the maximum to 16 worsened both tail and completed work.
Reducing pool capacity limited simultaneous creation but traded scheduler pressure for checkout
waits and much lower throughput. None of those changes met the 100-ms budget.

## Prewarming and successful-only charts hide the issue

The prewarmed V2 control passed the scheduler budget at all five RTTs. Its median dispatch p99
was 17.5, 17.6, 3.1, 13.4, and 5.1 ms at 0/20/50/100/200 ms RTT respectively.
At 100 ms RTT it completed about 616 cycles/sec, close to the rough limit of 64 connections
each occupied by an approximately 100-ms query round trip.

This is why BDN warmup plus a fixture that fills the pool before measurement cannot establish
cold-start async health. The existing warm-pool throughput benchmark remains useful, but it
needs the separate fresh-process companion added in this work.

Open timing alone also misses worker admission. At 100 ms RTT, default V2's first-open p99
measured from inside the worker was about 1.311 seconds, while first acquisition measured from
offering the worker task was about 3.536 seconds. LongRunning's first-open duration was larger
(about 2.431 seconds), but its offer-to-open total was smaller (about 2.436 seconds).
Looking only at the inner open timer would misread that tradeoff.

These acquisition percentiles are conditional on a successful first open. First opens cancelled
at the deadline are not successful latency samples, so even offer-to-open p99 can understate the
full request tail. The raw outcomes and in-flight counts must be read alongside it.

The Open/close-only workload reached over a million cycles/sec under added RTT once connections
were reused. That does not mean the network became faster: most iterations did no network I/O.
It is not an adequate replacement for Open/query/close when studying persistent network waits.

## Limits and missing observations are part of the result

There were zero counted SQL failures before the eight-second cutoff across this sweep.
**This is not evidence that no request could exceed the fifteen-second connect timeout.**
The experiment cancels in-flight work before that timeout. Longer observation and a separate
deadline test are needed for natural timeout incidence.

At default V2 / 64 workers / 100 ms RTT, about 50 probe opportunities were missing/dropped
per run. At 256 workers / 200 ms RTT, that rose to about 200. Missing probes do not erase the
observed multi-second failure, but they prevent treating a small completed-probe tail as proof
of health. The new gate rejects insufficient coverage.

Sample auditing found a harness weakness in 11 of the 15 extremely fast Open/close-only runs:
the 100,000-completion cap filled before every worker's first open was recorded.
Those historical runs now report first-open p99 as **N/A**, rather than inventing coverage.
The raw results and original summary are preserved. All Open/query/close cases retained every
worker's first operation. The future harness now always retains each worker's first completion
in addition to the bounded general sample; a high-throughput validation exercised that fix.

## Regression-test additions and acceptance protocol

The PerformanceTests executable now has a dedicated `--openasync-health` mode, a fresh-process
matrix launcher, raw outcome/probe/resource exports, graph renderers, and explicit health gates.
No production driver behavior was changed in this investigation.

The primary reproduction to preserve is:

1. Pooled V2, 64 workers, pool max 100, TP min/max 16/32767, cold process/pool,
   Open/query/close, 100 ms added RTT, three independent process repetitions.
2. Record both whole-window posting-cohort p99 and worst one-second completion-cohort p99,
   missed/outstanding probes, first acquisition from task offer, failures, and process threads.
3. Require baseline failure to reproduce before crediting a candidate; this sweep did so in
   all three repetitions. The absolute `--require-healthy-sql` gate rejects over-budget,
   insufficient, failed, or empty SQL runs.
4. Match candidate and baseline conditions and alternate build order. Require the candidate
   to meet the predeclared scheduler budget in every repetition without sacrificing completed
   work or hiding costs in dedicated threads, memory, or queueing.
5. Retain warm V2, cold V1, non-pooled, and 256-worker controls. Extend duration beyond the
   connection timeout for a separate timeout-resilience test; do not reinterpret these
   eight-second observations as that test.

These checks make the cold-start problem reproducible and prevent obvious false wins.
They do not turn a three-run developer-host study into a release guarantee.
Exact commands and settings are documented in `eng/pipelines/perf/openasync-health.md`.

## Provenance

Measured driver revision: `23b42bb80902e47879569a8c5d658bb7145db579`; diagnostic harness
additions were uncommitted on top. `manifest.json` records driver and harness assembly SHA-256
hashes, runtime information, cases, and repetitions. Netem artifacts record applied parameters,
packet counts, zero drops, and cleanup.

The exact measured C# harness is preserved as `measured-OpenAsyncHealthExperiment.cs`.
The post-sweep first-worker-retention correction changes future harness builds, not the driver
or historical measurements. `summary-original.csv` preserves the original summary;
`summary.csv` is recomputed from the unchanged raw JSON, exposing incomplete first-worker
coverage and offer-to-open latency. The report colors a cell red if any repetition fails,
amber if coverage is insufficient, and green only if all scheduler repetitions pass.

## Report clarity and regression-coverage revision

The graphs now explicitly identify cell values as **ThreadPool dispatch p99 in milliseconds,
median of the per-run p99 values**. Each health heatmap carries its own red/green/amber/gray
legend. Cases display the actual pooling version, worker count, initialization state, pool
capacity, TP min/max, LongRunning selection, and whether a query runs. The stable abbreviated
IDs remain in raw artifacts for compatibility; they no longer substitute for graph labels.
The original 240 measurements are unchanged.

The new `--suite open-paths` is deliberately symmetric rather than V2-heavy. At 64 workers it
contains 20 success scenarios: both Open/close and Open/query/close for V1/V2 cold, initialized
driver with a cleared pool, warm available-pool, and warm oversubscribed-pool cases; plus
non-pooled cold and initialized-driver cases. "Warm non-pooled" never means a cached physical
connection. The native/managed SNI selection is explicit.

All 20 scenarios executed real local SQL work under **both Windows managed and native SNI**:
40 single-repetition validation executions, with no pre-deadline SQL failures and complete
first-worker records. These validate the new workload plumbing, not performance improvements
or the full RTT matrix. They are intentionally not plotted alongside the original controlled
three-repetition WSL study. In both SNI variants the warm oversubscribed query cases had many
pending OpenAsync tasks; non-pooled initialized cases still performed asynchronous physical
opens rather than being mislabeled pooled reuses.

The new `--baseline-summary` comparison checks matched settings, RTTs, repetitions, runtime,
OS, and CPU count. In addition to the scheduler/workload gate, it rejects more than 10% median
completed-work throughput regression or a process-thread peak above the larger of a 25%
increase and eight additional threads. These initial tolerances are explicit and must be
calibrated on the performance host. A scheduler improvement alone cannot hide reduced work or
dedicated-thread growth.

**This is broad core success-path coverage, not a claim of exhaustive OpenAsync protection.**
Natural timeout/cancellation phases, reconnect/broken connections, authentication callbacks,
transactions, failover, MARS and .NET Framework require targeted scenarios and existing
functional/manual tests. The default observation deadline does not certify those paths.
The executable gates are not yet wired as a required scheduled PR CI job. The exact coverage
matrix, commands, validation scope, and these gaps are documented in
`eng/pipelines/perf/openasync-health.md`.

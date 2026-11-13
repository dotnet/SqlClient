# OpenAsync health boundaries: first measured findings

## Status and scope

**63 independent-process runs completed: 27 authenticated SQL baselines and 36 stalled-prelogin
negative controls, with three repetitions per setting. The authenticated latency sweep is
still pending.** Another playground owned the WSL SQL netem queue at 50 ms per direction
(100 ms added RTT), and no private SQL credential was available for a new process. That queue
was not changed and credentials were not read from the running app.

The measurements below are Windows, managed SNI, .NET 10, 16 runtime-visible logical processors.
SQL runs use the local MSSQLSERVER instance with integrated authentication and **zero injected RTT**.
The negative control accepts TCP on an ephemeral port but deliberately never answers prelogin.
It is **not** a simulation of SQL at 100 ms RTT. Neither series validates the exact reported
64-worker / 100-ms-RTT symptom; the next sweep must reproduce that under authenticated SQL.
This developer workstation was not reserved like a performance lab. Three repetitions establish
an observed range, not a population confidence interval.

See **report.html** for four graphs and all 21 configurations. Raw results, launch settings,
logs, assembly hashes, runtime information, and randomized ordering are preserved in
`sql-baseline/` and `stalled-controls/`.

## Authenticated SQL: cold V2 perturbs the scheduler even without injected RTT

Numbers below are medians across three runs. Brackets give the observed min/max of run-level p99.
The baseline is 64 workers, pool max 100, ThreadPool min/max 16/32767, cold pool, Open/query/close.

| Setting | Dispatch p99 ms [range] | Peak process threads | First successful open p99 ms |
|---|---:|---:|---:|
| V2 baseline | 394 [385, 437] | 52 | 369 |
| V2 LongRunning | 45 [38, 46] | 93 | 533 |
| V2, TP minimum 64 | 444 [438, 446] | 81 | 427 |
| V2, TP maximum 16 | 412 [397, 436] | 32 | 366 |
| V2, pool capacity 16 | 408 [303, 420] | 45 | 406 |
| V2, prewarmed pool | 94 [47, 108] | 47 | 16 |
| V1 | 6 [0.5, 17] | 49 | 639 |

No SQL failures were counted before the deadline in these 27 runs. Work in flight at the
deadline was cancelled and accounted separately. V2 baseline throughput was roughly 18,595
completed cycles/sec (range 18,016-20,627); LongRunning was roughly 19,567 (15,955-21,014).
These overlapping short-run ranges do **not** establish a throughput improvement.

**LongRunning reduced scheduler disruption but was not a free win.** Total process threads
grew approximately 79%, and first-open latency increased. Two of its three SQL repetitions
also had missed producer ticks, so only one met the strict scheduler-coverage gate.
V1 also had two insufficient-coverage repetitions. Low p99 alone is not certified health.
The 100-ms dispatch budget is an investigative criterion, not a documented product SLA.

Increasing TP minimum to 64 did not improve this cold SQL case. Reducing the maximum did not
fix it either. Raising workers from 16 to 256 changed median dispatch p99 from about 374 ms to
416 ms, while median throughput fell from about 19,537 to 17,299 completed cycles/sec. This is
not evidence that more concurrency is more capacity.

Cold-process JIT, authentication, TLS setup, and driver initialization contribute to these
results. The experiment alone does not attribute every millisecond to pool creation.
Prewarming removes several of those costs together. That is exactly why a benchmark that
warms connections and the ThreadPool before observing work can miss this user-visible behavior.

## Stalled prelogin: scheduling boundaries and misleading low tails

This negative control uses a three-second connect timeout and one burst per worker. All opens
are expected to fail or be cancelled; a scheduler pass here is **not** healthy SQL performance.
The workload is observed for eight seconds, then cancelled, with a one-second probe recovery.

| Setting | Dispatch p99 median ms | Peak process threads | Coverage interpretation |
|---|---:|---:|---|
| V2, 16 workers | 913 | 31 | Over budget |
| V2, 32 workers | 3,056 | 33 | Over budget; probes dropped |
| V2, 64 workers | 8,012 | 35 | Over budget; 256 probe opportunities dropped |
| V2, 128 workers | 8,032 | 35 | Over budget; probes dropped |
| V2, 256 workers | 8,026 | 35 | Over budget; probes dropped |
| V2, 64 workers, LongRunning | 2.4 | 93 | Scheduler passes all three repetitions |
| V2, 256 workers, LongRunning | 2,971 | 157 | Over budget; range 2,948-4,972 ms |
| V2, 64 workers, TP min 64 | 896 | 78 | Still over budget |
| V2, 64 workers, TP max 16 | 2.0 completed-only | 29 | **Insufficient: 320 missing/dropped probes** |
| V1, 64 workers | 2.0 | 30 | Scheduler healthy; 62 SQL operations still in flight at deadline |
| Non-pooled, 64 workers | 0.9 | 48 | Scheduler healthy; opens fail as expected |

**The approximately eight-second figures are not natural recovery times.** Cancellation at
the measurement deadline released blocked work, allowing the recorded callbacks to run during
recovery. Without that intervention their delay could have been longer.

For V2 at 64 workers, the largest one-second p99 **during the workload** was only about 1.9 ms,
yet posting-cohort p99 including recovery was about 8,012 ms. Fast early callbacks were visible;
the stalled callbacks were not in any completion cohort until recovery. With TP max 16,
64 probes remained outstanding after recovery and 256 further opportunities were dropped.
Reporting only completed probes would incorrectly make that severe stall look healthy.

LongRunning at 256 workers also failed the budget. This is consistent with the known limitation:
only initial work is moved to dedicated threads; continuations after incomplete awaits can run
on the ThreadPool and later perform blocking connection creation. The observations support the
limitation, but do not replace a stack trace establishing exactly where each thread was blocked.

## Performance-test changes informed by these observations

Added a `--openasync-health case.json result.json` mode to the existing PerformanceTests
executable. Unlike a warmed BDN iteration, each invocation is one fresh process.
The existing warm-pool benchmark remains unchanged.

The new mode exports worker offer/start times, first-open and subsequent-open timestamps,
successful and failed operations, cancellation, unstarted workers, deadline counters, and the
existing bounded ThreadPool observer's raw probes and resource series. This avoids conflating
worker scheduling with OpenAsync and avoids dropping failures from the story.

The sweep script supplies randomized repeated worker, TP minimum/maximum, pool-size,
V1/V2, pooling, warm/cold, and LongRunning cases. It can own an optional Linux netem queue
for an explicit RTT grid and refuses to replace an existing queue.

The `--require-healthy-sql` gate rejects any repetition over the 100-ms p99 budget, with
insufficient probe coverage, workload failures, unstarted workers, no completed SQL work, or an
unsuccessful process. It cannot certify negative controls as healthy SQL. Throughput and process
resource non-regression remain separate matched-baseline requirements.

Raw operation records are capped at 100,000 per process. Most high-throughput SQL cases reach
that cap; counters remain complete, but all-open percentiles in the report describe retained
samples, not full-run operation distributions. First-wave records and the independent scheduler
probe stream remain available. No production driver fix was made and LongRunning is not being
declared the solution.

## Required next experiment

Stop other netem-owning workloads, enter the SQL password privately, and run the authenticated
matrix at **0, 20, 50, 100, and 200 ms added RTT**, three fresh-process repetitions each.
100 ms RTT means **50 ms per direction**, not the TUI setting of 100 ms per direction.
Start with 64 workers / pool 100 / TP min 16 / max 32767, then pair LongRunning, warm pool,
TP min 64, TP cap 16, V1, non-pooled, and 16/32/128/256-worker cases.

Run baseline and candidate under identical conditions, alternating build order. Require the
baseline failure to reproduce before crediting an improvement. Report every repetition,
including failed/incomplete runs, producer misses, and dedicated-thread cost. Increase repetition
count and duration on a quiet dedicated host before making a release/performance claim.

Exact commands and interpretation rules are in `eng/pipelines/perf/openasync-health.md`.
The source revision for the driver is `23b42bb80902e47879569a8c5d658bb7145db579`; the new
diagnostic harness is an uncommitted addition on top, identified by assembly hashes in the
run manifests. The Windows SQL connection string uses integrated authentication and no password
was stored in artifacts.

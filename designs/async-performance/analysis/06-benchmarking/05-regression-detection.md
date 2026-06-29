# 05 — Regression detection

The 04/05 changes are async optimizations, but several are not confined to the async path — they
touch shared sync code, concurrency-sensitive locks, and pooled buffers where a mistake corrupts
data rather than just slowing it down. This document is about catching the **negative** impacts:
the silent sync-path regression, the variance that masquerades as a win, and the correctness bugs
that only a soak test finds.

The user's instruction is explicit: *extra care must be taken to detect any negative impacts.* This
is that care, written down.

---

## The sync-path guard is non-negotiable

The single highest-risk failure mode is making an async path faster while quietly slowing the
heavily-used synchronous `Open()` / `Read()` paths that the whole ecosystem depends on. Items that
are **not** async-isolated are the offenders:

| Item | Why it touches sync | Required guard measurement |
| --- | --- | --- |
| CE-5 semaphore handle locks | The lock is shared by sync + async send/receive | Sync send/receive latency, MARS on/off |
| CMD-3 ConcurrentQueueSemaphore | Stream lock used on both paths | Sync read throughput under contention |
| CMD-4 continuation-mode | Changes the shared packet path | Sync large-read parity (Compat ON unchanged) |
| CMD-5 setchars char pool | Runs on both TVP paths | Both TVP paths, sync + async |
| CMD-6 multiplexer packet pool | Shared packet buffers (Compat OFF) | Sync read parity under multiplexer |

For every one of these, the baseline matrix ([03-baselines](03-baselines.md)) **must** include the
sync variant, and the merge gate fails if the sync number regresses beyond the tolerated threshold —
even if the async number improved. `SqlConnectionRunner` benchmarking both `Open()` and
`OpenAsync()`, and the ThreadStarvation app's `--mode both`, exist precisely to make this cheap.

## Separate the win from the noise

A delta smaller than the rig's variability is not a result. Defend against false positives:

1. **Measure the noise floor first** — run the baseline twice and record per-metric run-to-run
   spread (see [03-baselines](03-baselines.md)). Reject any "win" below that floor.
2. **Control the machine** — disable turbo/SpeedStep, pin to physical cores, close background load,
   run on AC power. BenchmarkDotNet's multiple launches + warmup already reduce variance; do not
   fight it with a noisy host.
3. **Use the statistics BenchmarkDotNet gives you** — report mean **with** standard deviation and
   the p95/p99, not a bare mean. An async win that only moves the mean but widens the tail is
   suspect.
4. **Repeat before believing** — a surprising delta (good or bad) is rerun on a clean host before it
   is trusted or acted on.

## Watch the guard metrics, not just the headline

Every item has a guard metric ([01-what-to-measure](01-what-to-measure.md)). The classic negative
impacts these catch:

- **Pooling that trades allocation for CPU** — CMD-1/CMD-5/CMD-6 lower allocated bytes but add
  rent/return bookkeeping; watch CPU time/op and full-read time so the GC win is not erased by
  slower steady-state.
- **Cancellation registration that helps None but hurts the token path** — CMD-2 skips registration
  for `CancellationToken.None`; measure the *with-token* path too so it did not regress.
- **A copy removed that re-appears elsewhere** — zero-copy items must show the copies-per-packet
  counter actually dropped, not just a faster wall-clock that could be measurement luck.

## Correctness-as-performance hazards

Some "regressions" are data corruption, not slowdowns. The pooling and locking items can fail
silently in ways a single benchmark run will not show:

- **ArrayPool double-return / use-after-return** (CMD-1, CMD-5, CMD-6) corrupts a replayed read.
  Guard with a counting `ArrayPool` seam asserting `rent == return` and debug-mode buffer poisoning,
  plus a soak run that would surface intermittent corruption.
- **Deadlock / livelock / ordering changes** (CE-5, CMD-3) — `SemaphoreSlim` is not FIFO; a fairness
  change can stall under load. Require a stress run **and a 1-hour soak** with MARS on and off. PR
  #1357 is the precedent: it reverted exactly because a locking change misbehaved under sustained
  MARS load.

## Switch-interaction matrix

A change can be correct under the default switches and broken under a non-default combination. Before
merge, validate each item against the relevant cross-product of:

- `UseCompatibilityProcessSni` — ON (7.1 default) and OFF (multiplexer).
- `MakeReadAsyncBlocking` — the legacy sync-over-async compat path.
- `UseConnectionPoolV2` — the channel pool.

CMD-1 must be checked in both Compat modes (it peaks ON); CMD-4/CMD-6 only exist OFF; the
connection items are switch-agnostic but should still be confirmed not to interact.

## CI trending and the merge gate

Day-to-day work uses the simple local diff ([03-baselines](03-baselines.md)). The heavier,
periodic layer catches slow drift and gates merges:

1. **Threshold + review, not pure automation** — perf numbers are noisy; a CI threshold flags a
   suspected regression for human review rather than auto-failing on every wobble.
2. **Trend the EventCounters** — `alloc-rate`, `gc-heap-size`, `threadpool-queue-length` from
   `System.Runtime` plus the SqlClient provider give a continuous signal between formal baseline
   runs.
3. **Feed the existing baseline workstream** — register results with the Feature 42261 /
   PerfLab infrastructure 04 references so regressions are visible team-wide, not just locally.
4. **Single-purpose PRs** — 04 already mandates one change per PR; this also makes a regression
   trivially bisectable to the item that caused it.

## The negative-impact checklist

Before merging any 04/05 item, confirm:

- [ ] Sync-path guard metric measured and not regressed beyond threshold (mandatory for
  non-async-isolated items).
- [ ] Win exceeds the recorded noise floor.
- [ ] Guard metrics (CPU, with-token path, copies counter) checked, not just the headline.
- [ ] Pooling items: `rent == return` asserted; soak run clean.
- [ ] Locking items: stress + 1-hour soak, MARS on and off, clean.
- [ ] Switch-interaction matrix validated for the regimes the item touches.
- [ ] Result artifact committed next to the baseline for review.

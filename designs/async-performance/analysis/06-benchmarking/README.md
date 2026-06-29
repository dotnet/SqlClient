# Benchmarking — Measuring Async Performance Work Before It Starts

**Date**: 2026-06-29
**Scope**: The measurement infrastructure that must exist *before* the
[04-quick-wins](../04-quick-wins/README.md) and
[05-fundamental-improvements](../05-fundamental-improvements/README.md) work begins
**Inputs**: existing `tests/PerformanceTests` (BenchmarkDotNet) harness, the
[ThreadStarvation app](../../apps/ThreadStarvation/ANALYSIS.md), and the per-item criteria in 04/05

---

## Purpose

The 04 and 05 proposals are bets that specific changes will make async paths faster, allocate less,
or stop starving the thread pool. None of those bets can be *settled* without a measurement rig that
exists **before** the first line of product code changes. This analysis defines that rig: what to
measure, how to measure it, what baselines to capture, which flows need real environments, and how
to detect the negative impacts that careless async changes can introduce.

The guiding constraint is the one 04 already states for every item — *"benchmark baseline before
merge"* and *"tie each item to a measured delta"*. This document turns that one-liner into an
actionable, repeatable practice.

## Why measurement-first, not measurement-after

Several items in 04/05 only show their value under conditions the default local loopback hides:

- **Connection-establishment wins (CE-1..5)** are thread-starvation and latency wins. On a localhost
  with sub-millisecond RTT and an unconstrained thread pool, an async TCP connect looks identical to
  a blocking one. The win only appears under injected latency *and* concurrency.
- **Allocation wins (CMD-1, CMD-3, CMD-5, CMD-6, zero-copy)** need multi-packet payloads to exercise
  the snapshot/replay and PLP paths. A single-packet `SELECT 1` allocates almost nothing.
- **Large-read throughput (CMD-4)** is the 250x slowdown — invisible until a value spans hundreds of
  packets.

If we start changing code first and measuring second, we cannot distinguish a real improvement from
run-to-run noise, and we cannot prove we did not regress the heavily-used **sync** paths that several
non-async-isolated items (CE-5, CMD-3, CMD-4, CMD-6) touch.

## The "before work starts" checklist

This is the gate. Complete every item before merging the first 04/05 change.

1. **Stand up the harness matrix** — the existing BenchmarkDotNet `PerformanceTests` project plus the
   `ThreadStarvation` app, extended to cover each item's primary metric (see
   [02-how-to-measure](02-how-to-measure.md)).
2. **Capture baselines** — record the current numbers for every metric in every regime that an item
   targets, on a pinned baseline commit (see [03-baselines](03-baselines.md)).
3. **Establish the noise floor** — run each baseline at least twice and record the run-to-run delta,
   so a "win" smaller than the noise floor is rejected (see
   [05-regression-detection](05-regression-detection.md)).
4. **Define per-item pass/fail thresholds** — each 04/05 item gets a target metric, a minimum
   improvement, and a maximum tolerated regression on the sync path.
5. **Wire the diagnosers** — `MemoryDiagnoser` and `ThreadingDiagnoser` on every relevant benchmark,
   plus EventSource/EventCounters for field-observable metrics.
6. **Stand up the staged environments** — the latency-injected, large-payload, MARS, and strict-TLS
   setups that the connection and large-read items require (see
   [04-environments-and-staging](04-environments-and-staging.md)).

## Analyses

| # | Topic |
| --- | --- |
| [01](01-what-to-measure.md) | The metric catalog — latency, throughput, allocations, thread-pool, copies — mapped to each 04/05 item |
| [02](02-how-to-measure.md) | The harnesses and tools — BenchmarkDotNet, the ThreadStarvation app, EventCounters, traces |
| [03](03-baselines.md) | Capturing and storing baselines before the work starts, and the regime/TFM matrix |
| [04](04-environments-and-staging.md) | The complex setups — latency injection, large payloads, MARS, strict TLS, constrained thread pools |
| [05](05-regression-detection.md) | Detecting negative impacts — sync-path guards, switch matrix, variance control, soak tests |

## Method

This analysis is grounded in the harness that already exists in the repo
(`src/Microsoft.Data.SqlClient/tests/PerformanceTests`, a BenchmarkDotNet runner with
`MemoryDiagnoser` and `ThreadingDiagnoser` already wired) and the `ThreadStarvation` repro app under
[apps/](../../apps/ThreadStarvation/ANALYSIS.md). Item references (CE-*, CMD-*) point at the 04/05
proposals so each measurement traces back to the change it is meant to validate.

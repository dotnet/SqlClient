# 03 — Baselines

A baseline is a recorded set of numbers, on a known commit, in a known environment, that every later
measurement is compared against. Without it, "20% faster" is meaningless. This document defines what
to capture, how to pin it, and how to store it so the comparison is honest.

---

## Pin the baseline commit

Before any 04/05 product change lands, tag a baseline commit — the tip of the branch the work starts
from. Every item's before/after delta is measured against benchmarks built from this exact commit,
not against a developer's memory of "what it used to do".

- Tag it (e.g. `async-perf-baseline-2026-06-29`) so reruns are reproducible.
- Rebuild the baseline benchmarks from the tag whenever the comparison machine or SQL Server version
  changes — numbers are not portable across hardware (see below).

## Record the environment with every result

A number without its environment is noise. Store these alongside every baseline and every later
run, because BenchmarkDotNet results are **not** comparable across differing hardware or server
versions:

- CPU model, core count, and whether turbo/SpeedStep is disabled.
- OS and .NET TFM (`net8.0`, `net9.0`, `net462`).
- SQL Server version/edition and where it runs (local container, same-host, remote, Azure SQL).
- Network path and RTT to the server (loopback vs injected-latency vs cloud).
- Managed vs native SNI (`UseManagedNetworkingOnWindows`).
- The switch regime (`UseCompatibilityProcessSni`, `MakeReadAsyncBlocking`, `UseConnectionPoolV2`).

BenchmarkDotNet already emits a host-environment block in its summary; keep it with the result file.

## The baseline matrix

Each item targets specific conditions, so the baseline is not one number — it is a small matrix.
Capture the cross-product that is relevant per item, not the full Cartesian explosion.

| Dimension | Values | Why it matters |
| --- | --- | --- |
| Sync vs async | `Open`/`Read` **and** `OpenAsync`/`ReadAsync` | Guard the sync path on non-isolated items |
| Regime | Compat ON (7.1 default), Compat OFF (deferred) | CMD-1 peaks ON; CMD-4/CMD-6 require OFF |
| TFM | net8.0, net9.0, net462 (where applicable) | net462/native SNI are out of scope but must not regress |
| SNI | Managed, Native (Windows) | Items are managed-SNI; native is the guard |
| Payload | Single-packet, multi-packet/large | Allocation + large-read items need multi-packet |
| Network | Loopback, injected-latency | Connection items only show value under latency |
| Concurrency | 1, N constrained-pool | Starvation only emerges under load |

> **Capture both sync and async, always.** Half the items are not async-isolated; the only way to
> prove they did not slow `Open()` / `Read()` is to have a sync baseline to compare against.

## Store results as checked-in artifacts

The `MarkdownExporter.GitHub` exporter already produces a committable table. Use it.

1. Save each runner's exported markdown under a results folder keyed by date + commit + environment
   tag.
2. Commit the baseline results so reviewers can diff a PR's numbers against them in-tree.
3. For the ThreadStarvation app, capture its end-of-run summary (total time, per-query average, slow
   count, error count) and the monitor's thread-pool growth trace.

This keeps the loop **simple and iterative**: a developer reruns one runner, drops the new markdown
next to the baseline, and the delta is a literal text diff — no dashboard required for day-to-day
work. CI trending (below) is the heavier, periodic layer on top.

## Establish the noise floor first

Before trusting any delta, measure the rig's own variability: run the same baseline twice on the
same machine and record the run-to-run spread per metric. That spread is the **noise floor**. Any
later "improvement" smaller than the noise floor is rejected as un-provable. This is cheap, must be
done once per environment, and is the first defense against celebrating noise — expanded in
[05-regression-detection](05-regression-detection.md).

## Tie baselines to the existing baseline workstream

04 references a *"Feature 42261 baseline workstream"* and *"PerfLab / BenchmarkDotNet"* as the home
for the merge-time baseline. Where that infrastructure exists, register these baselines there so the
per-item deltas feed the same system the rest of the team reads, rather than living only in local
result files.

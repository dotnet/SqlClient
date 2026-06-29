# 04 — Environments and staging

Some wins are invisible on a developer laptop talking to a localhost SQL container. The conditions
that make them visible — network latency, multi-packet payloads, concurrency, encryption, MARS —
require deliberate staging. This document catalogs the environments each item needs and how to build
them, cheaply where possible.

---

## Why local loopback lies

The default `runnerconfig.json` connection string is
`Server=tcp:localhost; Integrated Security=true; Initial Catalog=sqlclient-perf-db;`. That is the
right *fast-iteration* target, but it hides exactly the effects 04/05 target:

- Sub-millisecond RTT makes an async TCP connect indistinguishable from a blocking one (CE-1..4).
- A small `SELECT` fits in one 8 KB packet, so the snapshot/replay and PLP paths never engage
  (CMD-1, CMD-4, zero-copy).
- An unconstrained thread pool never starves (CE-1, CE-4, CE-5).

So loopback is for **inner-loop iteration**; the staged environments below are for **proving the
win** and for the merge baseline.

---

## Environment catalog

### Latency injection (connection-establishment items)

The CE-* items convert held threads into released ones during I/O. The value scales with how long
the I/O takes, so we must inject realistic RTT.

- **Linux**: `tc` + `netem` to add delay and jitter on the loopback or egress interface, e.g.
  `tc qdisc add dev lo root netem delay 50ms 5ms`. Pair with packet loss
  (`netem loss 1%`) to exercise the multi-endpoint / retry paths (CE-1 parallel connect).
- **Windows**: a WAN-emulation tool (clumsy, NetLimiter) for the same effect on managed SNI.
- **Cloud target**: an actual Azure SQL instance in a different region is the highest-fidelity
  latency source and validates the real customer scenario behind issues #979 / #601.

Capture open latency at several RTTs (≈1 ms loopback, 20 ms, 50 ms, 100 ms) so the win is shown as a
function of latency, not a single point.

### Large multi-packet payloads (allocation + large-read items)

CMD-1, CMD-4, and the zero-copy path only engage when a value spans many TDS packets. Stage a table
with deliberately large columns:

- `varbinary(max)` / `nvarchar(max)` rows of **multiple MB** each — large enough to reproduce the
  snapshot/replay O(n²) behavior and the #593 ~13 GB allocation figure.
- A range of sizes (8 KB just-over-one-packet, 256 KB, 1 MB, 10 MB) so allocation and throughput are
  plotted against payload size, where the regressions and wins are size-dependent.
- The existing `Table.Build(...).AddColumn(...).InsertBulkRows(...)` helpers in the PerformanceTests
  `DBFramework` already create and seed tables programmatically — extend them with a large-payload
  pattern rather than hand-authoring SQL.

### Constrained thread pool (starvation items)

Starvation is an emergent property of concurrency against a bounded pool. Reproduce it with the
ThreadStarvation app's `--min-threads` / `--max-threads` / `--io-threads` knobs, driving N parallel
queries (`--mode both` for the sync-vs-async contrast). The metric is time-to-starvation and queue
growth, captured by the built-in monitor.

### Encryption regimes (TLS items)

CE-2 (async TLS handshake) and the zero-copy TLS-over-TDS framing must be measured across encryption
modes, because the handshake and per-packet framing cost differs:

- `Encrypt=Optional`, `Encrypt=Mandatory`, and `Encrypt=Strict` (TDS 8.0, TLS-first, ALPN).
- Strict mode is where the async handshake cost is highest and the win largest — it must be a
  first-class staged config, not an afterthought.

### MARS on/off (locking + multiplexer items)

CE-5 (handle locks) and CMD-3/CMD-4/CMD-6 (multiplexer, stream locks) interact with MARS session
multiplexing. The PR #1357 revert is the cautionary precedent: a locking change that looked fine
without MARS broke under it. Every locking/multiplexer item must be measured **MARS-on and
MARS-off** — `SqlConnectionRunner` already parameterizes `MARS`, and the ThreadStarvation app has a
`--mars` variant.

### Always Encrypted and TVP (CMD-5)

CMD-5 (`SetChars_FromReader` char pooling) is on the TVP streaming path. Stage a TVP insert workload
and, where the column path overlaps, an Always Encrypted column set, so the pooling change is
exercised on both TVP paths it touches.

---

## Staging cost ladder

Order the environments by setup cost so iterative work uses the cheapest rung that still shows the
effect, and the expensive rungs are reserved for the merge baseline.

| Rung | Setup cost | Use for |
| --- | --- | --- |
| Local loopback | None (exists) | Inner-loop iteration, allocation microbenches |
| Loopback + `netem` latency | Low (one command) | CE-* latency sensitivity, day-to-day |
| Constrained-pool concurrency | Low (app flags) | Starvation onset, CE-1/CE-4 |
| Large-payload seeded tables | Medium (table seed) | CMD-1, CMD-4, zero-copy |
| Remote / Azure SQL target | Medium–high | High-fidelity latency, merge baseline |
| Strict-TLS + MARS + AE matrix | High (cert/config) | CE-2, CMD-3/4/6, CMD-5 merge baseline |

The goal is that a developer can validate most items at the low rungs in minutes, and only the
merge-gating baseline run pays for the high rungs.

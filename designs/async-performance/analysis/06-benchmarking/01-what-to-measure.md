# 01 — What to measure

The metric catalog. Each metric is defined once, with the unit, the tool that captures it, and the
04/05 items it validates. The rule: **every item must name a primary metric and a guard metric**
before any code changes. The primary metric is the win we expect; the guard metric is what we watch
to prove we did not break something else (usually the sync path or allocations).

---

## Metric families

### Latency

Wall-clock time for a single logical operation. Report **distribution**, not just the mean —
async wins frequently show up at the tail (p95/p99), not the median.

| Metric | Unit | Captured by | Primary for |
| --- | --- | --- | --- |
| Connection open latency | ms (p50/p95/p99) | BenchmarkDotNet `OpenAsyncConnection` | CE-1, CE-2, CE-3, CE-4 |
| First-row latency (`ExecuteReaderAsync` → first `ReadAsync`) | ms | BenchmarkDotNet | CMD-2, zero-copy |
| Full-read time (open reader → reader drained) | ms | BenchmarkDotNet | CMD-1, CMD-4, zero-copy |

### Throughput

Work completed per unit time. The right denominator depends on the path — rows for narrow reads,
bytes for large-payload reads, operations for connect.

| Metric | Unit | Captured by | Primary for |
| --- | --- | --- | --- |
| Read throughput | MB/s | BenchmarkDotNet large-payload runner | CMD-4, zero-copy |
| Row throughput | rows/s | BenchmarkDotNet `DataTypeReaderAsync` | CMD-1, CMD-2 |
| Connect throughput under concurrency | opens/s | ThreadStarvation app | CE-1, CE-4 |

### Allocations and GC

The allocation items live or die here. BenchmarkDotNet's `MemoryDiagnoser` (already enabled in
`BenchmarkConfig`) reports allocated bytes per operation and Gen0/1/2 collection counts.

| Metric | Unit | Captured by | Primary for |
| --- | --- | --- | --- |
| Allocated bytes / op | bytes | `MemoryDiagnoser` | CMD-1, CMD-3, CMD-5, CMD-6, zero-copy |
| Gen0 / Gen1 / Gen2 collections | count / 1k ops | `MemoryDiagnoser` | CMD-1, zero-copy |
| Peak managed heap during large read | MB | `dotnet-gcdump` / `dotnet-counters` | CMD-1, CMD-4 |

> CMD-1's headline is issue #593's ~13 GB allocated to read a 10 MB value. That number is only
> reproducible with a multi-packet payload and the snapshot/replay path active — see
> [04-environments-and-staging](04-environments-and-staging.md).

### Thread-pool health

The connection-establishment items are fundamentally about *not holding a thread* during I/O.
BenchmarkDotNet's `ThreadingDiagnoser` (also already enabled) reports completed work items and lock
contention; the `ThreadStarvation` app surfaces the starvation onset directly.

| Metric | Unit | Captured by | Primary for |
| --- | --- | --- | --- |
| Completed work items / op | count | `ThreadingDiagnoser` | CE-1..5, CMD-3 |
| Lock contention count | count | `ThreadingDiagnoser` | CE-5, CMD-3 |
| Thread-pool growth under load | threads over time | ThreadStarvation monitor | CE-1, CE-4, CE-5 |
| Time-to-starvation (constrained pool) | ms / # queued | ThreadStarvation app | CE-1, CE-4 |

### Copies (instrumentation metric)

The zero-copy work (05) is justified by the per-packet copy chain. This is not a wall-clock metric —
it is a **structural** one captured by instrumentation counters in debug/diagnostic builds, so a
"zero-copy" change can be proven to actually remove a copy rather than just look faster.

| Metric | Unit | Captured by | Primary for |
| --- | --- | --- | --- |
| Copies per received packet | count | debug EventCounter / test seam | zero-copy (05-01, 05-02) |
| Staging-buffer rentals per read | count | counting `ArrayPool` seam | CMD-1, zero-copy |

### CPU

Secondary, but a regression guard: an allocation win that burns more CPU (e.g. pooling overhead) is
not free. Capture CPU time per op where a change adds bookkeeping (pooling, cancellation
registration).

| Metric | Unit | Captured by | Guard for |
| --- | --- | --- | --- |
| CPU time / op | ms | BenchmarkDotNet `-p:EventPipeProfiler` / PerfView | CMD-1, CMD-2, CMD-3 |

---

## Per-item measurement map

Each 04/05 item, its primary metric, and the guard metric that detects collateral damage.

| Item | Primary metric | Guard metric |
| --- | --- | --- |
| CE-1 async TCP connect | Connect throughput under constrained pool | Sync `Open()` latency unchanged |
| CE-2 async TLS handshake | Open latency (strict encryption) | Sync `Open()` latency unchanged |
| CE-3 async DNS | Open latency under concurrency | Sync `Open()` latency unchanged |
| CE-4 async pre-login read | Time-to-starvation | Login-phase error parity (no new errors) |
| CE-5 semaphore handle locks | Lock contention; MARS throughput | **Sync send/receive latency** (not async-isolated) |
| CMD-1 snapshot buffer pool | Allocated bytes/op (large read) | Full-read time; rent==return balance |
| CMD-2 cancellation token | Allocated bytes/op; first-row latency | Cancellation timing parity |
| CMD-3 ConcurrentQueueSemaphore | Allocated bytes/op (contended I/O) | Throughput under contention; no deadlock |
| CMD-4 continuation-mode | Read throughput (MB/s) large read | **Sync read** parity; Compat-ON path unchanged |
| CMD-5 setchars char pool | Allocated bytes/op (TVP) | Both TVP paths correct; rent==return |
| CMD-6 multiplexer packet pool | Allocated bytes/op (Compat OFF) | Multiplexer correctness; rent==return |
| 05 zero-copy / thin reader | Copies per packet; allocated bytes/op | Sync read parity; MARS + TLS framing intact |

The **bold** guard metrics are the dangerous ones: items that are not async-isolated touch shared
sync code, so the guard is a sync-path measurement that must show *no regression*. This is the
single most important rule in the whole document — see
[05-regression-detection](05-regression-detection.md).

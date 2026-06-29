# Executive Summary: Unix Async Performance — Thread Starvation

**[Feature 42261](https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/42261)** |
SqlClient | Unix Async Performance - Thread Starvation in Parallel ExecuteReaderAsync

| Field | Value |
| --- | --- |
| **Start Date** | 2026-04-01 |
| **Target Date** | 2026-06-30 |
| **Effort** | 20 story points |
| **Risk** | Medium |
| **Related GitHub Issue** | [dotnet/SqlClient#3459](https://github.com/dotnet/SqlClient/issues/3459) |

---

## Problem Statement

Parallel `ExecuteReaderAsync` calls on Unix/Linux suffer from thread pool starvation, producing
latencies orders of magnitude worse than equivalent sync calls. A `SELECT 1` that completes in
microseconds on the server can take >200 ms or timeout entirely under concurrent async load on Unix.

**Root cause:** On Unix, managed SNI routes all async I/O completions through the .NET thread pool
(no separate IOCP threads as on Windows). Under concurrent async load, four execution patterns
collide:

1. **Async-over-sync** — `OpenAsync()` blocks a thread on `Socket.Select()` in `TryConnectParallel`
   and on the `SslStream` handshake.
2. **Sync-over-async** — TDS login/pre-login reads block via `ReadSniSyncOverAsync()`, waiting for
   completions that need the same thread pool threads.
3. **Pure async** — `ExecuteReaderAsync`/`ReadAsync` completions queue to the thread pool but can't
   run because threads are blocked by (1) and (2).
4. **Handle-level locking** — `lock(this)` in `SniTcpHandle.Receive` and `lock(DemuxerSync)` in
   `SniMarsConnection` serialize all operations, blocking threads that could process completions.

The result is a deadlock-like stall: sync-over-async reads wait for completions, completions need
thread pool threads, and threads are blocked by async-over-sync opens and handle locks.

---

## Scope

| Dimension | Boundary |
| -- | -- |
| **Platform** | Unix/Linux only (managed SNI) |
| **Scenario** | Parallel `ExecuteReaderAsync` under concurrent load |
| **Endpoints** | Local SQL Server, Remote SQL Server, Azure SQL DB, Microsoft Fabric |
| **Out of scope** | Native SNI changes (Windows-only C++ library); ChannelDbConnectionPool (#3356) — independent, progressing on its own schedule |

---

## Related GitHub Issues

| Issue | Title | Relevance |
| --- | --- | --- |
| [#422](https://github.com/dotnet/SqlClient/issues/422) | MARS very slow / timeouts on Linux | Handle-level locking in MARS demuxer |
| [#601](https://github.com/dotnet/SqlClient/issues/601) | Async opening of connections in parallel is slow/blocking | Serialized connection creation in pool |
| [#979](https://github.com/dotnet/SqlClient/issues/979) | OpenAsync() blocks on network I/O | No async SNI open API |
| [#1530](https://github.com/dotnet/SqlClient/issues/1530) | Intermittent error 258 from thread starvation | Thread pool exhaustion under MARS load |
| [#1562](https://github.com/dotnet/SqlClient/issues/1562) | Huge performance problem with async | 2–5x slowdown for basic async vs sync |
| [#2152](https://github.com/dotnet/SqlClient/issues/2152) | Thread pool starvation acquiring access token | Cascading 20-min outages on token refresh |
| [#3118](https://github.com/dotnet/SqlClient/issues/3118) | Pre-login error due to thread starvation | Spurious connection failures under load |

---

## Mapping to Async Performance Research

The `plans/async-performance/` research identified 7 priority areas and 5 root causes. The following
table shows how each maps to Feature 42261's Unix-scoped thread starvation problem.

### Root Causes Within Scope

| Root Cause | In Scope? | Rationale |
| --- | --- | --- |
| RC1: Connection pool blocking synchronization | Dependency only | ChannelDbConnectionPool (#3356) is tracked separately; this feature benefits from it but doesn't own it |
| RC2: TDS async snapshot/replay (O(n²)) | Partially | Amplified on Unix by thread pool contention; experimental continuation mode exists behind AppContext switches |
| RC3: Native SNI lacks async open APIs | Out of scope | Native SNI is Windows-only; managed SNI open path is in scope |
| RC4: MARS multiplexing on managed SNI | **In scope** | Global `lock(DemuxerSync)` and `ConcurrentQueueSemaphore` serialization are primary Unix-specific bottlenecks |
| RC5: Missing true-async implementations | Partially | Sync-over-async login/pre-login reads directly cause starvation on Unix |

### Priority Area Applicability

| Priority | Area | Relevance to 42261 |
| --- | --- | --- |
| P1 | ChannelDbConnectionPool | **Dependency** — complementary, not owned by this feature |
| P2 | Async TDS Read Path | **Partial overlap** — continuation mode reduces thread-blocking retries; graduation of `UseCompatibilityProcessSni=false` path benefits Unix more than Windows |
| P3 | True Async Transactions | **Low** — bounded per-call overhead, not a primary starvation driver |
| P4 | Async SNI Connection Opening | **High** — async managed TCP connect, SSL handshake, pre-login, and login are directly in scope |
| P5 | Allocation Reduction | **Medium** — `ConcurrentQueueSemaphore` TCS allocations and buffer pool gaps contribute to GC-induced thread stalls |
| P6 | Packet Locking Redesign | **High** — `SniTcpHandle` locking and MARS demuxer global lock are primary Unix starvation vectors |
| P7 | Async TVP Sources | **Out of scope** — niche, no thread starvation impact |

---

## Delivery Plan

### Child User Stories

All 8 stories are in **New** state. They follow a measure-first, spike-then-iterate approach.

| Step | Work Item | Title | Assigned To | Purpose |
| --- | --- | --- | --- | --- |
| 1 | 43438 | Baseline Performance Characterization | Paul Medynski | Quantify async vs sync across 4 endpoint types; establish p50/p95/p99 baselines |
| 2 | 43088 | PerfLab Investigation & Requirements Spike | Apoorv Deshmukh | Evaluate PerfLab infrastructure for continuous benchmarking |
| 3 | 43439 | Automated Benchmark Suite | Apoorv Deshmukh | Build repeatable benchmark harness for all endpoint types |
| 4 | 43440 | Implementation Option Analysis Spike | Paul Medynski | Spike on 3 approaches: incremental managed SNI fixes, new async `ISniHandle` layer, or greenfield async data path |
| 5 | 43441 | Implementation Iteration 1 | Unassigned | First implementation pass based on spike findings |
| 6 | 43442 | Implementation Iteration 2 | Unassigned | Second implementation pass |
| 7 | 43443 | Implementation Iteration 3 — Hardening | Unassigned | Stability, edge cases, regression testing |
| 8 | 43444 | Multi-Endpoint Integration Testing | Unassigned | Validate across Local, Remote, Azure SQL DB, and Fabric endpoints |

### Phasing

```text
Phase 1: Measure (Steps 1–3)     Phase 2: Spike (Step 4)     Phase 3: Build (Steps 5–7)     Phase 4: Validate (Step 8)
───────────────────────────────   ───────────────────────     ─────────────────────────────   ──────────────────────────
• Baseline all 4 endpoints        • Evaluate 3 options:       • Iterative implementation       • 1-hour sustained load
• Establish improvement targets     - Incremental SNI fixes     targeting agreed metrics       • All 4 endpoint types
• Stand up benchmark suite          - New async ISniHandle    • No regressions in sync paths   • Memory leak / handle
• PerfLab integration               - Greenfield async path  • AppContext switch gating          exhaustion checks
```

---

## Acceptance Criteria

1. Baseline async vs sync performance characterized across all 4 endpoint types with quantitative
   metrics (p50/p95/p99 latency, throughput, thread pool saturation)
2. Measurable improvement targets defined during baselining (Step 1) and agreed upon by the team
3. Implementation achieves the agreed improvement targets as validated by the benchmark suite across
   all endpoint types
4. No regressions in existing sync code paths or in existing FunctionalTests/ManualTests suites on Unix
5. 1-hour sustained load test completes without memory leaks, handle exhaustion, or gradual degradation

---

## Key Technical Findings from Research

### Existing Experimental Code (AppContext Switches)

Two experimental switches already gate code that partially addresses the starvation:

```text
UseCompatibilityProcessSni = false       → enables packet multiplexer
  └─ UseCompatibilityAsyncBehaviour = false  → enables continuation-based PLP reads
```

When both are set to `false`, async reads resume from the last offset instead of replaying all prior
packets, eliminating O(n²) replay overhead. This code exists today but is experimental and untested
at scale. Graduating this path to default is a key option for Step 4.

### Managed SNI Architecture Gaps

| Gap | Impact | Recommended Fix |
| --- | --- | --- |
| `TryConnectParallel` uses blocking `Socket.Select()` | Blocks thread for entire TCP connect duration | Replace with `Socket.ConnectAsync()` pipeline |
| `SslStream.AuthenticateAsClient()` called synchronously | Blocks thread for TLS handshake (~5–50 ms) | Use `AuthenticateAsClientAsync()` with async pipeline |
| `ConcurrentQueueSemaphore` on every read/write | Serializes all I/O + allocates TCS per contended op | Replace with write-coalescing channel or `SemaphoreSlim(1,1)` |
| `lock(DemuxerSync)` in MARS | Global lock serializes all MARS sessions | Channel-based per-session multiplexer |
| `lock(this)` in `SniTcpHandle.Receive` | Blocks threads during concurrent receives | `SemaphoreSlim` async-friendly lock |

### Three Implementation Options for Spike (Step 4)

| Option | Description | Pro | Con |
| --- | --- | --- | --- |
| **A: Incremental managed SNI fixes** | Replace locks with `SemaphoreSlim`, add async connect/SSL, fix `ReadSniSyncOverAsync` | Low risk, targeted fixes | May yield diminishing returns; doesn't address fundamental async-over-sync architecture |
| **B: New async ISniHandle layer** | New async interface (`ISniHandleAsync`) with async open/read/write, implemented alongside existing sync handles | Clean separation of async path; doesn't break sync | Moderate scope; two code paths to maintain |
| **C: Greenfield async data path** | Fully separate async pipeline from `ExecuteReaderAsync` through TDS parsing to TCP socket | Eliminates all architecture mismatch | Very high scope; maintaining two parallel execution pipelines |

---

## Risks

| Risk | Severity | Mitigation |
| --- | --- | --- |
| MARS code path instability — prior rewrite (PR #1357) was merged then reverted | High | Extensive MARS-specific testing; avoid monolithic rewrites |
| Managed SNI blast radius — changes affect all Unix users, not just async-heavy workloads | High | AppContext switch gating; validate sync and low-concurrency scenarios |
| Separate async path may be necessary — incremental fixes may yield diminishing returns | Medium | Step 4 spike evaluates all 3 options before committing |
| ChannelDbConnectionPool dependency — full improvement requires both bodies of work | Low | Features are independent; starvation reduction compounds but is individually valuable |

---

## Dependency: ChannelDbConnectionPool (#3356)

The ChannelDbConnectionPool replaces `WaitHandle`-based blocking with `System.Threading.Channels`
for async-first connection pooling. It is tracked separately and will proceed on its own schedule.
Key status:

- Core get/return/create logic is merged
- Remaining: transaction support, warmup, pruning, rate limiting, metrics
- Gated behind `UseConnectionPoolV2` AppContext switch (currently throws `NotImplementedException`)

**Relationship:** The pool redesign eliminates blocking during **connection acquisition**; Feature
42261 eliminates blocking during **connection creation, I/O, and MARS multiplexing**. Best results
come when both are complete, but each delivers independent value.

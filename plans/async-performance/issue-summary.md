# Open Issues Related to Async Performance

Categorized inventory of open issues in dotnet/SqlClient related to async performance. Data gathered
from GitHub on 2026-03-27.

---

## Category 1: Connection Pool — Serialized Opens & Blocking Locks

These issues relate to the connection pool's use of WaitHandle-based synchronization and serial
connection creation, causing severe throughput degradation under concurrent async workloads.

### [#3356 — Redesign the SqlClient Connection Pool to Improve Performance and Async Support](https://github.com/dotnet/SqlClient/issues/3356)

- **Status:** Approved, actively being implemented
- **Labels:** Performance, Approved
- **Created:** 2025-05-19
- **Summary:** Epic/tracking issue for the `ChannelDbConnectionPool` redesign. Contains design
  document, work plan, and links to all sub-tasks. The new pool uses `System.Threading.Channels` for
  async-first queuing, replacing WaitHandle-based locks.

- **Sub-issues completed:** #3352 (scaffolding), #3380, #3396 (stub + tests),
  #3404 (get/return connections), #3746 (transacted pool split)

- **Sub-issues remaining:** Transaction support (#3743, #3805), pool warmup, pool pruning, rate
  limiting, context-aware timeouts, tracing/metrics

- **Switch status:** The `UseConnectionPoolV2` AppContext switch already exists to gate the new
  pool, but setting it to `true` **throws `NotImplementedException`** — the `ChannelDbConnectionPool`
  class is scaffolded but not functional. This confirms the pool v2 work is still early despite
  several sub-issues being marked complete.

### [#601 — Async opening of connections in parallel is slow/blocking](https://github.com/dotnet/SqlClient/issues/601)

- **Status:** Open (since 2020-06-12)
- **Labels:** Performance
- **Summary:** Opening 100 connections via `OpenAsync()` is serialized in pooled mode, creating them
  one at a time. In high-latency environments (e.g., Azure cross-region), this causes multi-minute
  startup delays. Root cause: the pool uses a semaphore that serializes physical connection
  creation. Non-pooled mode opens 8 at a time (limited by thread pool).

- **Impact:** Cold-start latency for web servers, serverless, autoscaling scenarios. Users report
  several-minute warm-up times.

### [#979 — SqlConnection.OpenAsync() may block on network I/O during connection creation](https://github.com/dotnet/SqlClient/issues/979)

- **Status:** Open (since 2021-03-10)
- **Labels:** Performance
- **Summary:** `OpenAsync()` calls down through `SNIOpenSyncEx` which does synchronous network I/O
  to the SQL Server. The native SNI layer has no async connection-opening API. This blocks the
  calling thread despite being called via an async API.

- **Impact:** UI thread hangs (WinForms/WPF), thread pool starvation in ASP.NET under load.

### [#2152 — Lock contention/thread pool starvation when acquiring access token](https://github.com/dotnet/SqlClient/issues/2152)

- **Status:** Open (since 2023-09-14)
- **Summary:** Under high concurrent load with Azure AD/Entra ID authentication, token acquisition
  blocks threads. Combined with the pool's WaitHandle locks, this causes cascading thread pool
  starvation → connection timeouts for ~20 minutes until tokens are cached.

- **Workaround:** `ThreadPool.SetMinThreads(100, 100)` before opening connections.
- **Impact:** Production outages lasting up to 20 minutes on token refresh.

### [#3118 — SqlConnection.Open raises "pre-login handshake" error due to thread starvation](https://github.com/dotnet/SqlClient/issues/3118)

- **Status:** Open (since 2025-01-16)
- **Summary:** When the process experiences thread starvation, `ReadSniSyncOverAsync()` in managed
  SNI fails during the pre-login handshake. The error message is misleading — the real cause is
  thread pool exhaustion.

- **Impact:** Spurious connection failures under load on Linux/managed SNI.

- **Switch relevance:** The `MakeReadAsyncBlocking` switch forces `_syncOverAsync = true` in
  `TryProcessDone`, which uses the same `ReadSniSyncOverAsync` path that fails here. Enabling
  `MakeReadAsyncBlocking` would increase exposure to this class of failure by routing more reads
  through the sync-over-async path.

---

## Category 2: Async Data Reading — Snapshot/Replay Overhead

These issues relate to the TDS parser's async read mechanism, which snapshots and replays the entire
packet chain on every async yield, causing exponential degradation with data size.

> **Switch context:** There is already experimental code behind AppContext switches that addresses
> the core snapshot/replay problem. Setting `UseCompatibilityProcessSni=false` enables a packet
> multiplexer, and setting `UseCompatibilityAsyncBehaviour=false` enables continuation-based
> snapshot replay (resume from where the read left off, instead of replaying from the start). Both
> default to their legacy values and the new code paths are considered experimental. See
> [appcontext-switches.md](appcontext-switches.md) for details.

### [#593 — Reading large data (binary, text) asynchronously is extremely slow](https://github.com/dotnet/SqlClient/issues/593)

- **Status:** Open (since 2020-06-05), Approved
- **Labels:** Performance, Approved
- **Comments:** 282 (most-commented async perf issue)
- **Summary:** Reading a 10MB `VARBINARY(MAX)` value asynchronously takes ~5 seconds vs ~20ms
  synchronously — a 250x slowdown. At 20MB, async takes ~52 seconds. The root cause is the TDS async
  snapshot mechanism that replays the entire packet chain on every new packet received.

- **Key quote** (Wraith2): *"The long term fix would be to rewrite async reads to avoid replaying
  the entire packet chain on every receipt. This is akin to brain surgery."*

- **Additional findings:** `SqlBinary` ctor copies the byte array (allocates 1/3 of total), and
  async path has excessive GC pressure (13GB allocated for reading 10MB).

- **Merged PRs:** #3377 (async string perf improvement), #3534 (async multi-packet fixes)

- **Switch status:** The experimental continuation-based PLP read path (gated by
  `UseCompatibilityProcessSni=false` + `UseCompatibilityAsyncBehaviour=false`) directly targets
  this issue's root cause. Rather than replaying all previously received packets on each async
  retry, the new path tracks a continuation offset and resumes mid-stream. This code exists today
  in `TryReadPlpBytes` but is behind compat defaults. The issue's 250x slowdown for 10MB reads
  should be dramatically reduced under the new path, since retry cost becomes O(1) per packet
  instead of O(n) where n is packets already received.

### [#1562 — Huge performance problem with async](https://github.com/dotnet/SqlClient/issues/1562)

- **Status:** Open (since 2022-03-28)
- **Labels:** Performance, Repro Available
- **Summary:** Even with small fields (no large data), async operations show massive overhead. Under
  load (250 threads × 10 instances), `OpenAsync` takes 1.6s vs <1ms for sync `Open`.
  `ExecuteReaderAsync` adds 0.3s overhead vs 0.002s sync.

- **Root cause:** Combination of connection pool serialization and async overhead in the TDS parser.
  Benchmark shows async is consistently 2–5x slower for basic operations.

- **Community impact:** Users report "SqlClient is just broken for async", forcing workarounds like
  using sync in ASP.NET Identity.

- **Switch relevance:** The `MakeReadAsyncBlocking` switch was likely introduced as a partial
  mitigation for this class of issue — it forces sync behaviour in `TryProcessDone` to avoid the
  snapshot/replay cost for DONE tokens. However, it trades async scalability for lower per-call
  overhead, so it helps single-connection latency at the expense of concurrency.

### [#2408 — ReadAsync CancellationTokenSource performance problem](https://github.com/dotnet/SqlClient/issues/2408)

- **Status:** Open (since 2024-03-15)
- **Labels:** Performance
- **Summary:** Using `ReadAsync(cancellationToken)` with a `CancellationTokenSource` (vs
  `CancellationToken.None`) causes significant performance degradation due to
  registration/unregistration of cancellation event handlers on every call.

- **Assessment:** Deemed unavoidable with current architecture — the cost of registering a
  cancellation callback is inherent to the pattern.

---

## Category 3: MARS & Managed SNI

### [#422 — Queries with MARS are very slow / time out on Linux](https://github.com/dotnet/SqlClient/issues/422)

- **Status:** Open (since 2020-02-12)
- **Labels:** Performance
- **Comments:** 107
- **Summary:** MARS connections on Linux (managed SNI) are dramatically slower than on Windows
  (native SNI) or without MARS enabled. Root cause involves thread pool starvation in the managed
  SNI's socket handling combined with MARS multiplexing overhead.

- **Workaround:** Increase `ThreadPool.SetMinThreads` to match concurrent connection count.

- **Switch relevance:** On Windows, users can set `UseManagedNetworkingOnWindows=true` to reproduce
  this issue (confirming it's a managed SNI problem). Conversely, there is no switch to use native
  SNI on Linux — the managed SNI is the only option. The `UseCompatibilityProcessSni` switch
  controls the packet multiplexer which is related to MARS packet handling, but the multiplexer
  is a higher-level construct (TDS packet reassembly) distinct from the MARS SMUX layer in
  managed SNI.

### [#2418 — [Design change] Connection-level packet locking](https://github.com/dotnet/SqlClient/issues/2418)

- **Status:** Open (since 2024-03-19), Up-for-Grabs
- **Summary:** Proposal to move SNI-level locking from stream-level `ConcurrentQueueSemaphore` to
  connection-level `SemaphoreSlim`. Current approach uses `Monitor.Enter` (blocking) for sync paths
  and stream-level async locks for async paths — an inconsistent design that hurts thread pool
  utilization.

- **Related:** A prior MARS rewrite by Wraith2 (#1357) was merged then reverted.

### [#1530 — Intermittent Unknown error 258 with no obvious cause](https://github.com/dotnet/SqlClient/issues/1530)

- **Status:** Open (since 2022-03-03)
- **Labels:** Area\Managed SNI
- **Summary:** Intermittent timeout errors (`Win32Exception 258`) from `EndExecuteReaderAsync` when
  SQL Server shows no long-running queries. Likely related to thread pool starvation in managed
  SNI's async callback handling.

---

## Category 4: Missing Async APIs & Design Gaps

### [#113 — Implement async transaction begin/commit/rollback methods](https://github.com/dotnet/SqlClient/issues/113)

- **Status:** Open (since 2019-05-22), Approved
- **Summary:** .NET Core 3.0 added `DbTransaction.CommitAsync/RollbackAsync` and
  `DbConnection.BeginTransactionAsync`. SqlClient has not implemented these as truly async
  operations — they currently delegate to sync implementations.

- **Impact:** Prevents end-to-end async transaction workflows.

### [#1554 — Consider not doing a roundtrip for BeginTransaction](https://github.com/dotnet/SqlClient/issues/1554)

- **Status:** Open (since 2022-03-19)
- **Labels:** Performance
- **Summary:** Npgsql defers `BEGIN TRANSACTION` to the first command, eliminating a network
  roundtrip. SqlClient could do the same, saving latency per transaction.

### [#982 — Allow TVPs to be populated via asynchronous data sources](https://github.com/dotnet/SqlClient/issues/982)

- **Status:** Open (since 2021-03-11)
- **Summary:** Table-Valued Parameters (TVPs) only accept synchronous `IEnumerable` data sources.
  There is no way to stream async data (e.g., from another async reader) into a TVP without
  blocking.

---

## Category 5: Allocation & Buffer Management

### [Discussion #3918 — Use pooling for the buffer in SetChars_FromReader](https://github.com/dotnet/SqlClient/discussions/3918)

- **Created:** 2026-01-28
- **Summary:** During TVP string processing, `SetChars_FromReader` allocates a new `char[]` on every
  call. Proposal to use `ArrayPool<char>` instead.

---

## Cross-Cutting: AppContext Switch Implications

The AppContext switch analysis (see [appcontext-switches.md](appcontext-switches.md)) reveals
several findings that cut across the issue categories:

1. **The snapshot/replay fix exists but is experimental.** Issues #593 and #1562 describe
   the core async read performance problem. The continuation-based snapshot path behind
   `UseCompatibilityProcessSni=false` + `UseCompatibilityAsyncBehaviour=false` is designed to
   solve this, but both switches default to legacy mode. The new code paths have been merged
   (PRs #2714, #3534) but are not enabled by default.

2. **Switch dependencies create an all-or-nothing adoption model.** Because
   `UseCompatibilityProcessSni=true` forcibly overrides `UseCompatibilityAsyncBehaviour` to `true`,
   users cannot adopt the continuation-based reads without also adopting the packet multiplexer.
   This raises the risk of the new path — it's a large behavioural change, not an incremental one.

3. **The pool v2 gate exists but the implementation doesn't.** `UseConnectionPoolV2=true` throws
   `NotImplementedException`. This means the pool redesign (#3356) is not yet testable even by
   early adopters, despite multiple sub-PRs being merged.

4. **`MakeReadAsyncBlocking` is a symptom, not a solution.** Its existence confirms that async
   DONE token processing had regressions. It's a point fix that forces sync behaviour in one
   specific method, trading scalability for stability. It does not address the underlying
   snapshot/replay cost.

5. **No switch exists for async connection open.** Issues #601 and #979 (serialized
   `OpenAsync`, sync `SNIOpenSyncEx`) have no AppContext switch workaround. These require
   architectural changes (async SNI open API, concurrent pool creation) that are not yet gated.

---

## Related Merged Work

| PR | Title | Merged |
| ---- | ------- | -------- |
| #3534 | Async multi-packet fixes | 2025-09-08 |
| #3404 | Get/Return pooled connections (ChannelDbConnectionPool) | 2025-10-20 |
| #3396 | Add ChannelDbConnectionPool stub and unit tests | 2025-06-06 |
| #3377 | Improve async string perf and fix reading chars | 2025-06-04 |
| #3352 | New pool scaffolding | 2025-05-22 |
| #2714 | Add partial packet detection and fixup | 2025-02-11 |
| #2663 | TDS Reader implementation | 2024-07-23 |
| #902 | Add ValueTask stream overloads on SNI streams | 2021-07-13 |
| #499 | Move datareader caches down to internal connection | 2020-07-16 |
| #389 | Add managed packet recycling | 2020-05-21 |

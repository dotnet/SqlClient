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

---

## Category 2: Async Data Reading — Snapshot/Replay Overhead

These issues relate to the TDS parser's async read mechanism, which snapshots and replays the entire
packet chain on every async yield, causing exponential degradation with data size.

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

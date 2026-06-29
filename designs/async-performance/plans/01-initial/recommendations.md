# Recommendations for Async Performance Improvement

Prioritized recommendations based on analysis of open issues, community feedback, and in-progress
work in dotnet/SqlClient.

---

## Priority 1: Complete the ChannelDbConnectionPool (In Progress)

**Addresses:** #3356, #601, #979, #2152, #3118
**Impact:** High — resolves the #1 source of async performance complaints
**Effort:** Medium — already well underway

### Why P1

This is the #1 priority because the connection pool's `WaitHandle`-based blocking affects **every**
`OpenAsync()` caller under concurrency — the widest blast radius of any async issue. Its blocking
creates a thread pool starvation cascade that amplifies all other async problems (token refresh, SNI
callbacks, timeouts), causing 20-minute production outages (#2152). Work is already in progress with
core logic merged, giving a favorable effort-to-value ratio. Most importantly, the pool is the
foundation: P3 (async transactions), P4 (async opens), and P5 (allocations) all deliver their full
value only after the pool stops blocking threads.
([Detailed rationale](01-connection-pool/priority-rationale.md))

### Status

The core get/return/create logic is merged. Remaining work:

- Transaction enlistment support
- Pool warmup and pruning
- Rate limiting (opt-in)
- Metrics and tracing
- Comprehensive integration testing

### P1 Recommendation

1. Prioritize completing the transaction support PRs (#3743, #3805)
2. Implement pool warmup early — it directly addresses cold-start scenarios (#601)
3. Make rate limiting opt-in, not on by default (per community consensus)
4. Add OTEL metrics (#2211) from the start rather than retrofitting later
5. Plan a phased rollout: AppContext switch → opt-in by documentation → default

---

## Priority 2: Rewrite Async TDS Read Path to Eliminate Snapshot/Replay

**Addresses:** #593, #1562
**Impact:** High — eliminates 250x slowdown for large async reads
**Effort:** ~~Very High~~ High (revised — see AppContext switch analysis)

### Why P2

This has the **worst measured performance degradation** of any async issue: 250x slower, 658x more
memory for a 10MB read. It is also the only issue that is
**algorithmically broken** (O(n²) in packet count) rather than a constant-factor problem. It is not
P1 because the effort is extreme (rewriting ~13K lines of TDS parser), the user base is narrower
(only affects large async reads), and a workaround exists (`SequentialAccess` + `GetStream()`). But
it outranks P3–P7 because the degradation is unbounded and grows with data size, and the 282-comment
issue (#593) represents the most community frustration of any async performance topic.
([Detailed rationale](02-tds-async-reads/priority-rationale.md))

### Current State

Incremental fixes (#3377, #3534, #2714, #2663) have improved the constant factor but the fundamental
O(n²) replay problem remains **under default settings**.

> **AppContext switch analysis finding:** A continuation-based PLP read path that eliminates the
> O(n²) replay already exists behind two experimental switches:
>
> ```text
> UseCompatibilityProcessSni = false
>      └─ enables → UseCompatibilityAsyncBehaviour = false
>           └─ enables → Snapshot.ContinueEnabled = true
>                └─ enables → continuation-based PLP reads
> ```
>
> When both switches are set to `false`, `TryReadByteArrayWithContinue`,
> `TryReadStringWithContinue`, and `TryReadPlpBytes` resume from the last offset instead of
> restarting — precisely the fix for the O(n²) blowup on large values. This code already exists in
> the codebase but is gated as experimental. See
> [appcontext-switches.md](appcontext-switches.md#2-usecompatibilityasyncbehaviour) for full
> analysis.

This shifts the primary P2 effort from "write new continuation logic" to "harden, test, and
graduate the existing experimental continuation mode."

### P2 Recommendation (Revised)

**Option C (New, Preferred): Graduate the Existing Continuation Mode** Harden the experimental
`UseCompatibilityProcessSni=false` + `UseCompatibilityAsyncBehaviour=false` path and promote it
to default.

1. **Audit** the multiplexer path (`TdsParserStateObject.Multiplexer.cs`) and continuation logic
   for correctness across connection resets, MARS sessions, and attention signals
2. **Benchmark** against the #593 scenario (10MB async read) to validate O(n²) elimination
3. **Identify gaps** in continuation coverage — which PLP read operations are _not_ covered?
4. **Harden** stricter `AppendPacketData` assertions in the multiplexer that may trigger in edge
   cases
5. **Graduate** via phased rollout: AppContext switch opt-in → default → remove compat path

- Pro — Code already exists; risk is stabilisation, not greenfield implementation
- Pro — The multiplexer also provides clean packet boundaries, benefiting other async work
- Con — The multiplexer adds per-packet `Packet` object allocation overhead
- Con — Experimental status means limited test coverage today

**Option A: State Machine Transformation** (Long-term, lower urgency if Option C succeeds)

- Eliminates the quadratic complexity entirely for all read operations, not just PLP
- Massive refactor of the core TDS parsing loop
- PR #2663 (TDS Reader) lays groundwork
- Urgency reduced if Option C covers the worst cases (large PLP column reads)

**Option B: Streaming Read for Large Values** (Short-term)

- Addresses the worst-case scenario without rewriting everything
- `CommandBehavior.SequentialAccess` + `GetStream()` may already partially address this
- Needs verification for async correctness

**Short-term:** Document that `SequentialAccess` + streaming APIs are the recommended workaround for
large value reads in async code paths.

**Medium-term:** Pursue Option C — audit, benchmark, and graduate the existing continuation mode.

---

## Priority 3: True Async Transaction Methods

**Addresses:** #113, #1554
**Impact:** Medium — enables end-to-end async transaction workflows
**Effort:** Medium

### Why P3

Transaction begin/commit/rollback are called on nearly every request in OLTP workloads — higher
frequency than connection opens (P4) or large reads (P2). The fix is moderate effort with a clear
path (async variant of one TDS method + three API overrides), and the deferred-begin optimization
(Fix 3.4) independently saves a network round trip per transaction. It ranks below P1–P2 because the
per-call overhead is bounded (one round trip, not cascading or quadratic), but above P4–P7 because
of its higher call frequency and lower implementation risk.
([Detailed rationale](03-async-transactions/priority-rationale.md))

### P3 Recommendation

1. Implement `BeginTransactionAsync` with true async TDS communication
2. Implement `CommitAsync` and `RollbackAsync` as true async
3. Consider deferred `BEGIN TRANSACTION` (Npgsql pattern from #1554) — prepend to first command to
   eliminate a roundtrip

4. This work benefits from and should be sequenced after the connection pool redesign, which
   establishes the async infrastructure

---

## Priority 4: Async SNI Connection Opening

**Addresses:** #979, #601
**Impact:** Medium-High — removes the last major blocking point in OpenAsync
**Effort:** High (requires native SNI changes or managed SNI improvements)

### Why P4

This removes the last thread-blocking layer in `OpenAsync()` after P1 fixes the pool. It matters
most during cold starts and pool expansion, where each blocked thread is unavailable for 5–500ms
depending on network latency. It ranks below P1 (pool affects all opens, not just physical creates),
P2 (250x per-read penalty vs one-time per-connection cost), and P3 (transactions occur per-request
vs per-connection). It requires multi-layer changes across SNI, SSL, PreLogin, and TDS Login, making
it higher effort than P3 or P5. Native SNI has no async API, so this effectively means steering
toward managed SNI for async workloads.
([Detailed rationale](04-async-sni-opens/priority-rationale.md))

### P4 Recommendation

1. For managed SNI: Ensure `ConnectAsync` is used for TCP connections (may already be partially done
   — verify current state)

2. For native SNI: Add an async connection open API (`SNIOpenAsync`) or document that managed SNI
   should be preferred for async workloads

3. Consider making managed SNI the default on all platforms once it reaches performance parity with
   native SNI

> **AppContext switch caveat:** Recommending `UseManagedNetworkingOnWindows=true` for async
> workloads has implications beyond the open path. Managed SNI replaces IOCP-based I/O completions
> (dedicated I/O thread pool) with `NetworkStream.ReadAsync` / `SslStream.ReadAsync` on the .NET
> thread pool, changing **all** steady-state async read/write characteristics. This trade-off
> should be benchmarked and documented before recommending managed SNI as the default on Windows
> for async scenarios. See [appcontext-switches.md](appcontext-switches.md#5-usemanagednetworkingonwindows).

---

## Priority 5: Reduce Allocation Overhead in Async Paths

**Addresses:** #593, #2408, Discussion #3918
**Impact:** Medium — reduces GC pressure and improves throughput
**Effort:** Low-Medium (incremental improvements)

### Why P5

These are low-risk, low-effort constant-factor improvements — `ArrayPool` swaps, `UnsafeRegister`
substitutions — that deliver reliable GC pressure reduction. They rank below P1–P4 because they
don't fix architectural problems (blocking, O(n²) algorithm, missing async APIs), but above P6–P7
because they are universally beneficial across all async workloads, carry minimal risk, and can be
done at any time as quick wins or starter contributions. GC pauses disproportionately hurt async
code by blocking all threads simultaneously.
([Detailed rationale](05-allocation-reduction/priority-rationale.md))

### Specific Opportunities

1. **ArrayPool for TDS packet buffers** — Packet buffers in the snapshot chain are never returned to
   the pool. Use `ArrayPool<byte>.Shared` with proper lifecycle management.

2. **Reduce CancellationToken registration overhead** — Issue
   #2408 shows that `CancellationTokenSource` adds measurable
   overhead. Consider using `CancellationToken.UnsafeRegister` where safe, caching registration
   handles, and skipping registration when the token is `CancellationToken.None` (already done?
   verify).

3. **Buffer pooling in TVP processing** — Discussion #3918 identifies `char[]` allocations in
   `SetChars_FromReader` that could use `ArrayPool<char>`.

4. **ValueTask in hot paths** — Verify all async hot paths return `ValueTask` instead of `Task`
   where the operation frequently completes synchronously (PR #902 started this for SNI streams).

---

## Priority 6: Connection-Level Packet Locking Redesign

**Addresses:** #2418, #422, #1530
**Impact:** Medium — improves MARS performance and reduces thread starvation
**Effort:** Medium-High

### Why P6

This primarily helps MARS users on managed SNI (Linux) — a real problem (107 comments on #422) but a
narrowing audience as connection pooling reduces the need for MARS. A prior MARS rewrite (PR #1357)
was merged then reverted, indicating high implementation risk in the most concurrency-sensitive part
of the driver. A production workaround exists (`ThreadPool.SetMinThreads`). These risk and scope
factors place it below P5's safe, universal improvements, but above P7 because MARS failures cause
actual production errors rather than suboptimal thread usage.
([Detailed rationale](06-packet-locking/priority-rationale.md))

### P6 Recommendation

1. Move from stream-level `ConcurrentQueueSemaphore` to connection-level `SemaphoreSlim` for
   read/write locking

2. Unify sync (`Monitor.Enter`) and async lock paths into a single mechanism
3. Carefully test MARS scenarios — the reverted PR #1357 is a cautionary tale
4. This is marked Up-for-Grabs and could be a good community contribution target with proper
   guidance

> **AppContext switch caveat:** The `UseCompatibilityProcessSni=false` multiplexer path introduces
> new concurrency state (`_partialPacket`, `Packet` object tracking, stricter packet boundary
> assertions) with its own locking assumptions. Any locking redesign must be validated against both
> the compat and multiplexer packet processing paths — especially if P2's recommendation to
> graduate the multiplexer to default is pursued. See
> [appcontext-switches.md](appcontext-switches.md#3-usecompatibilityprocesssni).

---

## Priority 7: Async TVP Data Sources

**Addresses:** #982
**Impact:** Low-Medium — niche but important for streaming scenarios
**Effort:** Medium

### Why P7

This addresses the narrowest audience — the intersection of TVP users who also need async streaming
of TVP data. Most TVP usage involves pre-materialized `DataTable` or in-memory collections where
`IAsyncEnumerable` offers no benefit. A simple workaround exists (pre-collect data into a
`List<SqlDataRecord>`). Only one issue (#982) requests this, with minimal community engagement. It
is valuable for API completeness and alignment with modern .NET patterns, but has no dependencies on
other priorities and can be deferred indefinitely without impacting the critical async performance
improvement path. ([Detailed rationale](07-async-tvp/priority-rationale.md))

### P7 Recommendation

Add `IAsyncEnumerable<T>` support for TVP data population. This enables:

- Streaming data from one async reader into a TVP
- Pipeline-style processing without intermediate materialization
- Alignment with modern C# patterns

---

## Implementation Sequencing

```text
Phase 1 (Now)           Phase 2 (Next)           Phase 3 (Future)
─────────────────       ─────────────────        ─────────────────
Complete Pool V2        Async TDS reads          Async SNI opens
  - Transactions        Async transactions       Async TVP sources
  - Warmup/Pruning      Allocation reduction     Packet locking
  - Metrics             SequentialAccess docs     redesign
```

### Dependencies

- Pool V2 is independent and should ship first
- Async TDS reads benefit from the TDS Reader (#2663) groundwork
- Async transactions benefit from Pool V2's async infrastructure
- Packet locking redesign depends on codebase unification progress

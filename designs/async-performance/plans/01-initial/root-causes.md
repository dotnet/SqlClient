# Root Causes of Async Performance Problems

Analysis of the fundamental architectural issues causing async performance degradation in
Microsoft.Data.SqlClient, drawn from open issues and community discussions.

---

## Root Cause 1: Connection Pool Uses Blocking Synchronization

**Issues:** #601, #979, #2152, #3118, #3356

The legacy connection pool (`WaitHandleDbConnectionPool`) uses `WaitHandle`-derived synchronization
primitives throughout its implementation. These are incompatible with async/await because:

1. **`WaitHandle.WaitOne()` blocks the thread** — When the pool is exhausted, `OpenAsync()` callers
   block a thread pool thread waiting for a connection to be returned. Under high concurrency, this
   leads to thread pool starvation.

2. **Connection creation is serialized** — A single semaphore guards physical connection creation.
   Even when the pool needs 100 new connections, they are opened one at a time. In high-latency
   environments (cross-region Azure), this causes startup delays measured in minutes.

3. **Async opens are funneled through a background thread** — The pool collects async open requests
   into a queue and processes them synchronously on a dedicated thread. This means `OpenAsync()` is
   strictly slower than `Open()` because it adds queuing and cross-thread marshaling overhead on top
   of the same synchronous work.

### Thread Pool Starvation Cascade

The blocking synchronization creates a cascade failure mode:

```text
High concurrency load
  → Many OpenAsync() calls
    → Pool semaphore blocks thread pool threads
      → Thread pool runs low on threads
        → Async continuations (token refresh, SNI callbacks) can't be scheduled
          → Connection timeouts / token acquisition failures
            → More retries → more blocked threads
              → Complete thread pool starvation (~20 min recovery)
```

This pattern is documented in issue #2152, where Azure AD token acquisition under load causes
20-minute outage windows.

---

## Root Cause 2: TDS Async Snapshot/Replay Mechanism

**Issues:** #593, #1562, #2408

The TDS parser's async read path uses a "snapshot and replay" mechanism that is fundamentally O(n²)
in the number of packets:

### How It Works

When reading data asynchronously:

1. The parser starts reading from the first packet
2. If more data is needed, it takes a **snapshot** of all state
3. It yields control (returns an incomplete Task)
4. When the next packet arrives, it **replays** from the snapshot — re-parsing all previously
   received packets from the beginning

5. This repeats for every packet

### Why It's Catastrophic for Large Data

For a data value spanning N packets:

- Packet 1: parse 1 packet → yield
- Packet 2: replay from start, parse 2 packets → yield
- Packet 3: replay from start, parse 3 packets → yield
- ...
- Packet N: replay from start, parse N packets → done

Total parsing work = 1 + 2 + 3 + ... + N = **N(N+1)/2** — quadratic in the number of packets.

### Measured Impact

From issue #593 benchmarks (10MB VARBINARY(MAX)):

| Method | Time | Memory Allocated |
| --- | --- | --- |
| Sync | 20ms | 20 MB |
| Async | 5,000ms | 13,156 MB |

The async path is **250x slower** and allocates **658x more memory**. The excessive memory comes
from packet buffers captured in the snapshot chain that can't be released until the entire read
completes.

At 20MB, async takes ~52 seconds — the time grows super-linearly.

### Design Limitation

The snapshot/replay approach was designed to avoid modifying the synchronous parser logic. The sync
path can simply block waiting for the next packet, but async must yield and resume. Rather than
restructuring the parser as a state machine that can resume mid-parse, the original design chose to
replay from scratch. Fixing this requires rewriting the core TDS parsing loop — described by a
contributor as "akin to brain surgery."

### Recent Mitigations

- PR #3377 improved async string reading performance
- PR #3534 fixed async multi-packet handling
- PR #2714 added partial packet detection
- PR #2663 introduced a TDS Reader abstraction

These are incremental improvements that reduce the constant factor but don't eliminate the quadratic
fundamental.

---

## Root Cause 3: Native SNI Lacks Async APIs for Connection Opening

**Issue:** #979

The native SNI (Windows) library exposes `SNIOpenSyncEx` for establishing connections but has no
async equivalent. This means `SqlConnection.OpenAsync()` ultimately does:

```text
OpenAsync()
  → Task.Run or similar wrapper
    → SNIOpenSyncEx()  // blocks on TCP connect, SSL handshake, login
```

On the managed SNI side (Linux/cross-platform), the `TryConnectParallel` method also has blocking
code paths, but with different failure modes (issue #2192 — connections getting stuck).

### Impact

Every `OpenAsync()` call blocks a thread pool thread for the entire duration of:

- DNS resolution
- TCP connection establishment
- TLS handshake
- Pre-login/login protocol exchange

This is typically 5-50ms locally, but can be 100ms+ in cloud environments, and seconds with network
issues.

---

## Root Cause 4: MARS Multiplexing on Managed SNI

**Issues:** #422, #1530, #2418

MARS (Multiple Active Result Sets) multiplexes multiple logical TDS sessions over a single physical
TCP connection. On managed SNI (Linux), this creates unique problems:

1. **MARS header overhead reduces effective packet size** — The MARS header consumes part of each
   TDS packet, potentially requiring 2 network packets where 1 would suffice without MARS. This
   doubles the impact of any per-packet overhead.

2. **Packet-level locking uses blocking primitives** — The current design uses `Monitor.Enter`
   (sync) and `ConcurrentQueueSemaphore` (async) at the stream level. When thread pool threads are
   blocked by these locks during high-throughput MARS operations, async continuations can't run.

3. **Platform differences in socket behavior** — The Unix socket implementation in .NET has
   different threading characteristics than Windows, exacerbating threadpool starvation (see
   dotnet/runtime#32016).

### Result

MARS on Linux + managed SNI + high concurrency = frequent timeout errors that don't correspond to
actual SQL Server load. The workaround is to massively over-provision the thread pool with
`SetMinThreads`.

---

## Root Cause 5: Missing True-Async Implementations

**Issues:** #113, #982, #1554

Several SqlClient APIs nominally support async but actually delegate to synchronous implementations:

| API | Async Status |
| ----- | ------------- |
| `BeginTransactionAsync` | Calls sync `BeginTransaction` |
| `CommitAsync` | Calls sync `Commit` |
| `RollbackAsync` | Calls sync `Rollback` |
| TVP data population | Only `IEnumerable` — no `IAsyncEnumerable` |
| `BeginTransaction` | Always does a network roundtrip (could be deferred) |

These "fake async" APIs don't just waste an async wrapper — they contribute to thread pool pressure
because the underlying sync work blocks the thread that's supposed to be running async
continuations.

---

## Cross-Cutting Theme: Thread Pool Starvation

The common thread across all five root causes is **thread pool starvation**. SqlClient's internally
synchronous architecture consumes thread pool threads for:

- Waiting on pool semaphores (Root Cause 1)
- Replaying packet chains (Root Cause 2)
- Blocking on SNI connection establishment (Root Cause 3)
- MARS packet-level locks (Root Cause 4)
- Fake-async API wrappers (Root Cause 5)

Each of these individually might be tolerable, but in combination they create a system where async
workloads consume more thread pool resources than equivalent sync workloads — the exact opposite of
what async is designed to achieve.

Users consistently report that switching from async to sync APIs **improves** performance and
reliability, which indicates a fundamental design issue rather than a minor optimization
opportunity.

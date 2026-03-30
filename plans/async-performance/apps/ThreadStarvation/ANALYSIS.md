# ThreadStarvation — Program Analysis

## Purpose

Reproduces **thread-pool starvation** when executing many parallel SQL queries using
`Microsoft.Data.SqlClient`. Designed to surface latency spikes and thread-pool exhaustion under
high concurrency with configurable parameters.

## Test Structure

The app builds one or more test passes based on CLI options:

| Option | Effect |
| ------ | ------ |
| `--mode async` (default) | One async pass |
| `--mode sync` | One sync pass |
| `--mode both` | Sync pass, then async pass |
| `--mars` | Adds a MARS variant of each pass |
| `--launch parallel` (default) | `Parallel.ForEachAsync` with `Thread.Sleep` |
| `--launch whenall` | `Task.WhenAll` with `Task.Run` per query |

All passes use the same connection string, connection count, and thread-pool settings. The
`--mode both` option enables direct sync-vs-async comparison, which is the defining symptom in
issue [#1562](https://github.com/dotnet/SqlClient/issues/1562).

## Execution Flow

1. Parse CLI options and build the connection string (with `--connect-timeout`
   and `--pooling` overrides applied).
2. Configure thread pool: `SetMinThreads` / `SetMaxThreads` per
   `--min-threads`, `--max-threads`, `--io-threads`.
3. Optionally enable SqlClient event logging (`--log-events`).
4. Optionally pause for `dotnet-trace` attachment (`--trace`).
5. For each pass:
   - Warm up with a single query (`throwOnError: true`).
   - Run N parallel queries using the selected launch strategy.
   - A background monitor task prints thread-pool stats every
     `--monitor-interval` ms.
   - Report total time, per-query average, slow count, error count.

## Key Observations

### 1. `Thread.Sleep` inside an async body

```csharp
async (i, token) =>
{
    Thread.Sleep(options.SleepMs); // blocks a thread-pool thread
    await ExecuteQueryAsync(…);
}
```

This deliberately pins a thread-pool thread for `--sleep` ms (default 500) before each query,
simulating the starvation trigger. With 200 iterations and `MaxDegreeOfParallelism = 200`, up to
200 threads can be blocked simultaneously. This is the intentional stress vector — it forces the
thread pool to grow or starve. Use `--sleep 0` to disable.

### 2. Thread-pool configuration

```csharp
ThreadPool.SetMinThreads(options.MinThreads, options.IoThreads);
ThreadPool.SetMaxThreads(options.MaxThreads, options.IoThreads);
```

- Worker min/max default to **1000** — pre-ramps the pool so the `Thread.Sleep` stalls don't
  cause slow ramp-up delays.
- IOCP min/max default to **1** — this has no effect on Unix (epoll-based I/O), but constrains
  IOCP completions on Windows to a single thread.

### 3. Connection pooling disabled by default

`--pooling` is off by default. This forces every iteration to negotiate a full TLS + TDS
handshake + token acquisition, maximizing per-connection cost and I/O completion traffic. Pass
`--pooling` to enable pooling.

### 4. Launch strategies

The `--launch` option selects between two strategies:

- **`parallel`** (default) — `Parallel.ForEachAsync` with `Thread.Sleep` injection. Gives
  controlled, throttled thread-pool pressure.
- **`whenall`** — `Task.WhenAll` with `Task.Run` per query. Fires all queries concurrently with
  less scheduling overhead.

### 5. Slow-query detection

Queries taking longer than `--slow` ms (default 200) are flagged as `SLOW`. Under healthy
conditions a `SELECT 1` completes in single-digit milliseconds, so any `SLOW` log indicates
thread-pool contention delaying continuations.

### 6. Error handling

- Connection/command errors are caught and logged per-connection but don't abort the run (unless
  during warmup).
- `AppDomain.UnhandledException` handler logs to stderr as a safety net.
- Use `--verbose` to see full stack traces.

## What It Measures

| Metric | How |
| ------ | --- |
| Thread-pool saturation | Monitor task prints available vs. max worker/IOCP threads |
| Per-query latency | `Stopwatch` around `ExecuteReaderAsync` + `ReadAsync` |
| Starvation-induced slowdowns | Queries exceeding `--slow` threshold logged as `SLOW` |
| Total throughput | Wall-clock time / query count |

## Intended Diagnosis

The scenario exercises the path where `SqlClient` async continuations (from `OpenAsync`,
`ExecuteReaderAsync`, `ReadAsync`) compete for thread-pool threads that are blocked by
`Thread.Sleep`. On Unix, managed SNI uses `SslStream` async I/O whose completions queue to the
thread pool — if all worker threads are sleeping, continuations stall.

This reproduces real-world patterns where application code blocks threads (e.g.,
synchronous-over-async, lock contention) while SqlClient async operations need completion threads
to make progress.

## Sync, Async, and Their Inversions

SqlClient's TDS parser and SNI layer support four execution patterns. Understanding them is
essential because this program — despite calling only `async` APIs — triggers all four internally,
and the inversions (sync-over-async, async-over-sync) are the root causes of the thread starvation
it measures.

### The Four Patterns

#### 1. Pure Sync

The caller's thread performs all work start-to-finish, blocking on I/O as needed. No task
machinery, no snapshots, no callbacks.

```text
App thread:  ──Open()──Send()──Read(blocking)──Parse──Return──
```

In SqlClient, sync reads go through `TdsParserStateObject` with `_syncOverAsync = true`. On
managed SNI, `SniTcpHandle.Receive()` does a blocking `Stream.Read()` inside `lock(this)`. On
native SNI, it calls `SNIReadSync()` which does a direct blocking socket read.

**Cost:** One thread blocked per I/O wait. No snapshot/replay overhead, no task allocations.

#### 2. Pure Async

The caller issues an I/O request and receives a `Task`. The thread is released back to the pool.
When the OS signals I/O completion, a callback resumes the work on a (potentially different)
thread.

```text
App thread:     ──OpenAsync()──await──(released)
                                           ╭──callback──Parse──Return──
Completion:     ───────────────────────────╯
```

In SqlClient, async reads set `_syncOverAsync = false`. `ReadSni()` issues an SNI read with a
`TaskCompletionSource` callback. When the TDS parser needs more data mid-parse, it takes a
**snapshot** of parser state and returns `NeedMoreData`. When data arrives, the SNI callback fires,
the parser replays from the snapshot, and parsing continues. This snapshot/replay is what causes
the O(n²) penalty for large data (#593).

**Cost:** No thread blocked during I/O — but snapshot/replay overhead per yield point, plus
`Task`/`TCS` allocations.

#### 3. Sync-over-Async

Synchronous code that blocks the calling thread waiting for an async I/O operation to complete.
The work is initiated asynchronously (to leverage kernel async I/O) but the calling thread spins
or waits on a synchronization primitive rather than yielding.

```text
App thread:    ──async read──mres.Wait()──(blocked)──
                                             ╭──signaled──continue──
I/O complete:  ─────────────────────signal──╯
```

In SqlClient, `ReadSniSyncOverAsync()` is the canonical example. It exists because the TDS parser
was originally designed around synchronous reads — the ASM (async state machine) was bolted on
later. When the parser is in a code path that can't yield (login handshake, pre-login, attention
processing), it calls `ReadSniSyncOverAsync()` to block until data arrives even though the
underlying I/O is async.

**How it works per platform:**

| Platform | Mechanism | Thread impact |
| -------- | --------- | ------------- |
| Windows (native SNI) | `SNIReadAsync()` posts to IOCP → completion signals a Win32 semaphore → `WaitForSingleObject()` blocks caller | Caller blocked on OS semaphore. Completion runs on IOCP thread. |
| Unix (managed SNI) | `SniTcpHandle.Receive()` does `_stream.Read()` (sync socket read) inside `lock(this)` | Caller blocked on socket I/O. Same thread does everything. |

The `_syncOverAsync` flag controls this mode. It is set `true` by:

- `SqlDataReader` when executing sync reads
- `TdsParser.TryProcessDone()` when the `MakeReadAsyncBlocking` switch is on
- Login/pre-login/attention code paths that can't structurally yield

**Cost:** Thread blocked (like sync), but with additional overhead of async I/O setup, semaphore
signaling, and potential cross-thread marshaling. Worst of both worlds under contention.

#### 4. Async-over-Sync

An `async` API that internally performs blocking synchronous work, typically on a thread-pool
thread. The caller gets a `Task` and believes they're non-blocking, but a thread is consumed
behind the scenes.

```text
App thread:     ──OpenAsync()──returns Task──(free)──
Background:     ──TryConnectParallel()──Socket.Select(BLOCKS)──
                ──TLS(BLOCKS)──Login(BLOCKS)──complete Task──
```

In SqlClient, `OpenAsync()` is the primary example. There is no async SNI open API on either
platform:

- **Windows:** `SNIOpenSyncEx()` — no async variant exists in the native C++ library
- **Unix:** `TryConnectParallel()` uses non-blocking `Socket.Connect()` but then calls
  `Socket.Select()` (synchronous blocking poll)

The connection pool dispatches `OpenAsync()` calls to a dedicated background thread
(`WaitForPendingOpen`) that processes them serially. This means `OpenAsync()` is always **slower**
than `Open()` — it adds queuing and cross-thread marshaling on top of identical blocking work.

**Cost:** Thread blocked (like sync), plus task allocation, cross-thread marshaling, and queuing
delay. The `async` keyword is a lie — it's sync work in async clothing.

### Comparison

| Pattern | Blocked? | Snapshot? | Tasks? | When used |
| ------- | :------: | :-------: | :----: | --------- |
| Pure sync | Yes (caller) | No | No | `Open()`, `ExecuteReader()`, `Read()` |
| Pure async | No | Yes | Yes | `ExecuteReaderAsync()`, `ReadAsync()` post-connect |
| Sync-over-async | Yes (caller) | No | Partial | Login, pre-login, `MakeReadAsyncBlocking` |
| Async-over-sync | Yes (bg) | No | Yes | `OpenAsync()` → SNI connect, token wrappers |

### How This Program Hits All Four

The three API calls in `ExecuteQueryAsync` traverse all four patterns internally:

```text
await connection.OpenAsync(ct)
├─ ASYNC-OVER-SYNC: Returns Task, but blocks a thread on:
│  ├─ Socket.Select() in TryConnectParallel (TCP connect)
│  ├─ SslStream handshake (TLS negotiation)
│  └─ SYNC-OVER-ASYNC: TDS login/pre-login reads use
│     ReadSniSyncOverAsync() — can't yield mid-handshake
│
await command.ExecuteReaderAsync(ct)
├─ PURE ASYNC: Sets _syncOverAsync = false
│  ├─ Sends query via TDS (async write)
│  └─ ReadSni() with TCS callback → NeedMoreData → yields
│     └─ SNI callback on thread-pool thread → resumes parser
│
await reader.ReadAsync(ct)
├─ PURE ASYNC: Fetches next row via snapshot/replay
│  └─ For SELECT 1, data fits in one packet — no replay
│     └─ Completes synchronously (returns completed Task)
```

Under the thread pressure created by `Thread.Sleep` on 200 threads:

- The async-over-sync opens (`OpenAsync`) consume threads that can't process completions
- The pure async completions (`ExecuteReaderAsync` callbacks) queue to the thread pool but can't
  run — all threads are sleeping or blocked in SNI opens
- The sync-over-async reads during login (`ReadSniSyncOverAsync`) block waiting for completions
  that need threads to fire
- **Deadlock-like stall**: sync-over-async login reads wait for completions → completions need
  thread-pool threads → threads are blocked by `Thread.Sleep` and other sync-over-async waits

This is why `SELECT 1` — a query that completes in microseconds on the server — can take >200 ms
or timeout entirely under this program's workload.

### The `MakeReadAsyncBlocking` Switch

The `Switch.Microsoft.Data.SqlClient.MakeReadAsyncBlocking` AppContext switch forces
`_syncOverAsync = true` in `TryProcessDone()`, converting what would be pure-async DONE token
reads into sync-over-async blocking reads.

**Trade-off:**

| Aspect | Switch OFF (default) | Switch ON |
| ------ | -------------------- | --------- |
| Read mode | Pure async with snapshot/replay | Sync-over-async (blocks thread) |
| Large data perf | O(n²) snapshot replay | O(n) but thread blocked per read |
| Thread pool impact | Low per-read | High — each read blocks a thread |
| Best for | High concurrency, small results | Low concurrency, large results |

This program does **not** set the switch. If it did, the pure-async `ExecuteReaderAsync`/
`ReadAsync` path would become sync-over-async, blocking an additional thread per query during
data reading — worsening the starvation it's designed to measure.

## Cross-Reference with Parent Analysis

This program exercises multiple root causes identified in the
[async-performance research](../../README.md). The table below maps program behaviors to root
causes from [root-causes.md](../../root-causes.md) and open issues from
[issue-summary.md](../../issue-summary.md).

### Root Causes Exercised

| Root Cause | How This Program Triggers It |
| ---------- | ---------------------------- |
| **RC1 — Connection Pool Blocking** | Disabled by default (`--pooling` off), so the pool serialization path is *bypassed*. Each connection does a full physical open. |
| **RC3 — No Async SNI Open** | Every iteration calls `OpenAsync()` which blocks a thread for TCP + TLS + login. With 200 concurrent opens, 200 threads are blocked simultaneously. |
| **RC4 — MARS on Managed SNI** | The `--mars` pass hits the global `lock(DemuxerSync)` in `SniMarsConnection` and `ConcurrentQueueSemaphore` serialization — the known Unix bottleneck. |
| **RC5 — Missing True-Async** | Token acquisition uses `MSAL`, which has its own async paths, but the TDS login sequence wrapping it is sync-over-async. |
| **Cross-cutting: Starvation** | The `--sleep` injection simulates app-side thread blocking, forcing SqlClient's async continuations to compete for the same thread pool. |

Root Cause 2 (TDS snapshot/replay) is **not** meaningfully exercised because `SELECT 1` returns a
trivial single-packet result — there is no large data to trigger the O(n²) replay.

### Applicable GitHub Issues

| Issue | Title | Relevance |
| ----- | ----- | --------- |
| [#601](https://github.com/dotnet/SqlClient/issues/601) | Async opening of connections in parallel is slow/blocking | Directly reproduced: 200 parallel `OpenAsync()` with no pooling. Login latency under thread pressure is the core measurement. |
| [#979](https://github.com/dotnet/SqlClient/issues/979) | `SqlConnection.OpenAsync()` may block on network I/O | Every `OpenAsync()` blocks a thread through managed SNI's `TryConnectParallel` → `Socket.Select()`. |
| [#2152](https://github.com/dotnet/SqlClient/issues/2152) | Lock contention / thread pool starvation acquiring access token | Token acquisition competes with `Thread.Sleep`-blocked threads, reproducing the starvation cascade. |
| [#3118](https://github.com/dotnet/SqlClient/issues/3118) | `SqlConnection.Open` pre-login error due to thread starvation | Under starvation, `ReadSniSyncOverAsync()` in managed SNI can fail during pre-login. The `SLOW` and `ERROR` logging captures these. |
| [#422](https://github.com/dotnet/SqlClient/issues/422) | Queries with MARS are very slow / time out on Linux | The `--mars` pass directly reproduces this. The global demuxer lock in `SniMarsConnection` serializes all sessions under managed SNI. |
| [#1562](https://github.com/dotnet/SqlClient/issues/1562) | Huge performance problem with async | The `--mode both` option enables direct sync-vs-async comparison matching this issue's symptoms. |
| [#1530](https://github.com/dotnet/SqlClient/issues/1530) | Intermittent Unknown error 258 | Under heavy starvation, managed SNI timeout errors may surface as "error 258" in `ERROR` log lines. |

### Issues NOT Exercised

| Issue | Why Not |
| ----- | ------- |
| [#593](https://github.com/dotnet/SqlClient/issues/593) | `SELECT 1` returns a tiny value — no large data for snapshot/replay O(n²) |
| [#2408](https://github.com/dotnet/SqlClient/issues/2408) | No `CancellationTokenSource` churn — the token comes from the launch framework |
| [#113](https://github.com/dotnet/SqlClient/issues/113) | No transactions used |
| [#3356](https://github.com/dotnet/SqlClient/issues/3356) | Pool is disabled by default; `ChannelDbConnectionPool` is not tested |

### Platform-Specific Relevance

Per [platform-differences.md](../../platform-differences.md), this program is most diagnostic on
**Unix/Linux** where:

- All async I/O completions share the .NET thread pool (no separate IOCP threads)
- `SniTcpHandle.Receive()` takes `lock(this)` on the entire handle
- `SniMarsConnection` uses a global `lock(DemuxerSync)` for all sends
- `Socket.Select()` blocking in `TryConnectParallel` consumes a managed thread

On Windows with native SNI, the same program would show less starvation because IOCP completions
run on dedicated I/O threads that don't compete with the `Thread.Sleep`-blocked worker threads.

## Default Configuration

All values are tunable via CLI options. Run `--help` for the full list.

| Setting | Default |
| ------- | ------- |
| Target | `net10.0` |
| Connections | 200 |
| Mode | `async` |
| Launch | `parallel` |
| Query | `SELECT 1;` |
| Sleep | 500 ms |
| Connect Timeout | 120 s |
| Command Timeout | 30 s |
| Pooling | off |
| Worker Threads | 1000 min / 1000 max |
| IOCP Threads | 1 min / 1 max |
| Slow Threshold | 200 ms |
| Monitor Interval | 100 ms |

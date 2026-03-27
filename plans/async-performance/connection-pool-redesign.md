# ChannelDbConnectionPool — Redesign Progress

Tracking the ongoing connection pool redesign effort (issue #3356, discussion #2612).

---

## Background

The `ChannelDbConnectionPool` is a ground-up rewrite of the SqlClient connection pool, replacing the
legacy `WaitHandleDbConnectionPool`. It was proposed in discussion #2612 (June 2024) and formalized
as issue #3356 (May 2025).

## Design Principles

Based on the design document in #3356 and discussion #2612:

1. **Async-first** — Uses `System.Threading.Channels` as the core data structure, providing native
   async wait/read/write APIs without blocking threads.

2. **Parallel connection opening** — Removes the single-semaphore bottleneck that serializes
   physical connection creation.

3. **Task-based background work** — Pool warmup and pruning use `Task` and the managed thread pool
   instead of dedicated threads.

4. **Modeled after Npgsql** — The design is based on Npgsql's `PoolingDataSource`, which is proven
   in production at scale.

## Architecture

```text
┌──────────────────────────────────────────────┐
│             ChannelDbConnectionPool          │
│                                              │
│  ┌────────────────────────────────────────┐  │
│  │     Channel<DbConnectionInternal>      │  │
│  │  (idle connection queue - async r/w)   │  │
│  └────────────────────────────────────────┘  │
│                                              │
│  ┌───────────────┐  ┌─────────────────────┐  │
│  │ SemaphoreSlim │  │ TransactedConnPool  │  │
│  │ (max pool sz) │  │ (enlisted conns)    │  │
│  └───────────────┘  └─────────────────────┘  │
│                                              │
│  ┌──────────────────────────────────────────┐│
│  │ Background Tasks: warmup, pruning        ││
│  └──────────────────────────────────────────┘│
└──────────────────────────────────────────────┘
```

### Key Workflows

**Get Connection (OpenAsync):**

1. Async acquire counting semaphore (bounded by max pool size)
2. Try read idle connection from Channel
3. If Channel is empty, create new physical connection in parallel
4. Return connection to caller

**Return Connection (Close/Dispose):**

1. If connection is healthy, write back to Channel
2. Release counting semaphore
3. Any waiters on the semaphore are unblocked asynchronously

**Warmup:**

- Async task creates connections to reach `MinPoolSize`
- Written to tail of Channel

**Pruning:**

- Background task periodically removes connections exceeding `ConnectionLifetime`

## Implementation Progress

### Completed

| PR | Title | Merged | Description |
| ---- | ------- | -------- | ------------- |
| #3352 | New pool scaffolding | 2025-05-22 | AppContext switch, factory routing |
| #3396 | ChannelDbConnectionPool stub + tests | 2025-06-06 | Core class skeleton, unit tests |
| #3404 | Get/Return pooled connections | 2025-10-20 | Main acquire/release logic |
| #3435 | SqlConnectionFactory cleanup | 2025-07-18 | Refactor factory hierarchy |
| #3746 | Split out TransactedConnectionPool | 2025-11-07 | Separate transaction-enlisted pool |

### In Progress / Remaining

| Task | Issue/PR | Status |
| ------ | ---------- | -------- |
| Transaction support (enlist) | #3743 | Planned |
| Transaction support (tests) | #3805 | Open PR |
| Pool warmup | — | Planned |
| Pool pruning (with clear counter) | — | Planned |
| Rate limiting connection opens | — | Planned |
| Context-aware timeouts | — | Planned |
| Tracing and metrics | — | Planned |

## Activation

The new pool is activated via an AppContext switch:

```csharp
AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2", true);
```

This allows opt-in testing without breaking existing users.

## Community Discussion Highlights

From discussion #2612 (36 comments):

- **@roji (Npgsql maintainer):** Questioned whether the counting semaphore should gate idle
  connection retrieval (not just creation). Noted that rate-limiting connection creation should
  ideally be a server-side concern, not client-side.

- **@edwardneal:** Emphasized that WaitHandle contention has application-wide impact — blocking
  non-SqlClient async code in the same process. Removing WaitHandles should improve async
  performance across the entire application, not just SQL operations.

- **@roji:** Pointed out that `SemaphoreSlim` doesn't guarantee FIFO ordering, which could starve
  some callers. Suggested `Interlocked` operations might be simpler if the semaphore isn't used for
  blocking waits.

- **@mdaigle (Microsoft):** Acknowledged throttling concerns. Plans to release as experimental
  (AppContext switch) for real-world validation before making it default.

## Benchmark Results (from #3356)

Preliminary benchmarks from the POC implementation:

| Scenario | Legacy Pool | ChannelDbConnectionPool | Improvement |
| ---------- | ------------- | ------------------------ | ------------- |
| Warm pool (.NET Framework) | 2,100ms | 90ms | **23x** |
| Cold pool (parallel opens) | Serialized | Parallel | Significant |

> Note: These are early POC numbers, subject to change. Official benchmarks planned.

## Remaining Concerns

1. **Transaction support complexity** — Distributed transactions and enlistment add significant
   complexity. Multiple PRs (#3743, #3746, #3805) address this.

2. **Rate limiting debate** — Whether to throttle connection creation by default. Community prefers
   opt-in. Microsoft wants some protection for recovery scenarios.

3. **FIFO fairness** — Channel provides FIFO for idle connections, but the gating semaphore may not
   provide FIFO for waiters.

4. **Integration testing** — The new pool needs validation against diverse real-world scenarios
   (failover, Azure SQL serverless resume, token refresh under load).

5. **Doesn't fix async TDS reads** — The pool redesign addresses connection open/close performance
   but does not address the snapshot/replay issue in the TDS parser (issue #593).

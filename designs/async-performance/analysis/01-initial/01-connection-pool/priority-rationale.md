# Priority Rationale: Why P1

## Ranking: Priority 1 of 7

The connection pool redesign is ranked highest because it addresses the **single largest source of
async performance complaints** across the entire dotnet/SqlClient issue tracker, and it is the
foundation upon which several other improvements depend.

## Justification

### 1. Widest Blast Radius (5 Open Issues)

The pool's blocking behavior is implicated in more open issues than any other async problem area:
\#3356, #601, #979, #2152, #3118. Every SqlClient user who calls `OpenAsync()` under any concurrency
hits this — it is not a niche scenario.

By contrast, the TDS read slowdown (P2) requires reading large data values, async transactions (P3)
only affect transaction-using code paths, and MARS locking (P6) only affects MARS-enabled
connections.

### 2. Cascading Failure Mode

The pool's `WaitHandle`-based synchronization creates a **thread pool starvation cascade** that
amplifies other problems. When pool semaphores block thread pool threads, async continuations
throughout the system cannot run — including token refresh callbacks, SNI read completions, and
timer-based timeouts.

Issue #2152 documents this cascade causing **20-minute production outages** during Azure AD token
refresh. The pool is both the trigger and the bottleneck — fixing it breaks the cascade even if
other components remain synchronous.

### 3. Work Already In Progress

The `ChannelDbConnectionPool` is partially implemented with core get/return/create logic already
merged (PRs #3352, #3396, #3404). This means the remaining work starts from a working foundation,
not from scratch. The effort-to-value ratio is highly favorable compared to P2's "brain surgery" TDS
rewrite.

### 4. Foundation for Other Priorities

Several other priorities deliver their full value only after the pool is fixed:

- **P4 (Async SNI Opens):** A truly async `OpenAsync()` offers limited benefit if the pool
  immediately blocks the returned thread with `WaitHandle.WaitOne()`.

- **P3 (Async Transactions):** Async transaction methods reduce value if acquiring a connection to
  begin the transaction still blocks.

- **P5 (Allocation Reduction):** Reducing per-operation GC pressure matters less when blocked
  threads aren't executing operations at all.

### 5. Incremental Delivery Possible

The connection pool fix can be delivered incrementally behind an AppContext switch
(`UseConnectionPoolV2`), allowing phased rollout:

1. Fix factory routing → pool is testable
2. Add transaction support → pool covers main scenarios
3. Add warmup/pruning → pool matches production needs
4. Flip default → users automatically benefit

This opt-in model dramatically reduces risk compared to other priorities that require changing
shared code paths (P2, P6).

### 6. Direct Impact on "Async is Broken" Perception

Issue #1562 reports that `OpenAsync()` takes **1.6 seconds** under load vs <1ms for sync `Open()`.
This highly visible, easily reproduced discrepancy drives the "SqlClient is broken for async"
narrative. Fixing the pool directly removes this perception gap, even if other async inefficiencies
remain.

## Why Not Higher

This is already P1 — there is no higher ranking. If there were, the TDS read path (P2) would compete
because its 250x measured slowdown is technically worse per-operation, but the pool affects more
users and more scenarios.

## Why Not Lower

Every argument for ranking the pool lower fails:

- "Work is already in progress, so it doesn't need prioritization" — Incorrect. The factory routing
  is broken (throws `NotImplementedException`) and 7 methods remain unimplemented. It needs active
  prioritization to reach completion.

- "Other improvements could be done first" — They could, but their impact would be muted by the pool
  bottleneck.

- "Users can use sync instead" — This defeats the purpose of async in ASP.NET Core, Azure Functions,
  and other async-first frameworks.

# Research: Pool Pruning

**Feature**: Pool Pruning for ChannelDbConnectionPool  
**Date**: 2026-04-10

## Research Topics

### 1. Npgsql PoolingDataSource Pruning Algorithm

**Decision**: Adopt Npgsql's sampling-based pruning with median calculation, adapted for SqlClient's channel pool.

**Rationale**: Npgsql's approach is production-proven, avoids the WaitHandle pool's complex two-stack aging, and fits naturally with a channel-based idle store. The median of idle samples smooths out transient demand spikes, preventing over-pruning.

**Alternatives considered**:
- **WaitHandle two-stack aging** — Connections age through `_stackNew` → `_stackOld` over two cleanup cycles. Each cycle destroys from `_stackOld` while above MinPoolSize, then promotes `_stackNew` to `_stackOld`. Rejected because it requires dual data structures and doesn't adapt to observed usage levels — it prunes based on age alone, not demand.
- **Fixed percentage pruning** — Prune a fixed fraction of idle connections each cycle. Rejected because it doesn't adapt to workload patterns and can be too aggressive or too conservative.

**Findings**:

Npgsql `PoolingDataSource` (file: `src/Npgsql/PoolingDataSource.cs`):

```
Fields:
  _pruningSampleSize = ConnectionIdleLifetime / ConnectionPruningInterval  (rounded up)
  _pruningMedianIndex = _pruningSampleSize / 2 - 1  (0-based index after sort)
  _pruningSamples = int[_pruningSampleSize]
  _pruningSampleIndex = 0  (current write position in circular buffer)
  _pruningTimerEnabled = false
  _pruningTimer = Timer (one-shot, re-armed each callback)

Algorithm (PruneIdleConnectors):
  1. Lock _pruningTimer
  2. Record _idleCount into _pruningSamples[_pruningSampleIndex]
  3. If sampleIndex < sampleSize - 1:
     - Increment index, rearm timer, return (still collecting samples)
  4. If sampleIndex == sampleSize - 1 (buffer full):
     - Sort samples array
     - toPrune = samples[medianIndex]  (the median idle count)
     - Reset sampleIndex to 0
     - Rearm timer
  5. Release lock
  6. While toPrune > 0 AND numConnectors > MinConnections:
     - Read one connector from idle channel
     - If valid (CheckIdleConnector), close it
     - Decrement toPrune

Key behavior:
  - toPrune IS the median idle count, NOT "prune down to median"
  - Timer is one-shot (Timeout.InfiniteTimeSpan for period), re-armed each callback
  - Timer enabled only when numConnectors > MinConnections (UpdatePruningTimer)
  - Lock prevents overlap between timer callbacks
  - CheckIdleConnector also enforces ConnectionLifetime expiry
```

### 2. WaitHandle Pool CleanupCallback (Reference Comparison)

**Decision**: Use for reference only. Do not replicate the two-stack pattern.

**Rationale**: The two-stack approach is well-tested but doesn't adapt to observed usage. The sampling approach provides better sizing decisions.

**Findings**:

```
WaitHandleDbConnectionPool.CleanupCallback:
  - _cleanupWait = random(12..23) * 10 * 1000  → 120-240 seconds (randomized to avoid thundering herd)
  - Timer fires periodically (both initial delay and period = _cleanupWait)
  - Phase 1: While Count > MinPoolSize, pop from _stackOld and DestroyObject
    - Transaction roots (IsTransactionRoot) are NOT destroyed — sent to stasis instead
    - Non-blocking semaphore acquisition (WaitOne(0)) prevents contention
  - Phase 2: Move all connections from _stackNew → _stackOld (promotion)
  - Phase 3: QueuePoolCreateRequest() to replenish back to MinPoolSize
  - Timer created in Startup(), disposed in Shutdown()
```

**Differences from Npgsql approach**:

| Aspect | WaitHandle Pool | Npgsql | Our Plan |
|--------|----------------|--------|----------|
| Algorithm | Two-stack aging | Sampling + median | Sampling + median |
| Interval | 120-240s random | Configurable (default 10s) | Configurable (default 10s) |
| Adaptation | None (age-based) | Usage-based (median) | Usage-based (median) |
| Overlap prevention | Non-blocking semaphore | Lock on timer | Lock on timer |
| Timer type | Periodic | One-shot, re-armed | One-shot, re-armed |
| Transaction roots | SetInStasis (preserved) | N/A (no System.Transactions) | Deferred to transactions feature |

### 3. Channel Pool Integration Points

**Decision**: Pruning integrates with the existing `RemoveConnection` flow and adds new timer + sampling state.

**Rationale**: `RemoveConnection` already handles slot release, null wake-up, and disposal. Pruning just needs to select which idle connections to remove.

**Findings**:

Integration points in `ChannelDbConnectionPool`:
1. **Timer creation** → `Startup()` (currently `NotImplementedException`)
2. **Timer disposal** → `Shutdown()` (currently `NotImplementedException`)
3. **Idle connection removal** → existing `RemoveConnection(connection)` — releases slot, writes null wake-up, disposes
4. **Idle connection reading** → existing `_idleConnectionReader.TryRead()` — non-blocking read from channel
5. **Connection count** → `_connectionSlots.ReservationCount` — total managed connections
6. **Pool state check** → `State` — check for `ShuttingDown` to stop pruning
7. **MinPoolSize** → `PoolGroupOptions.MinPoolSize` — floor for pruning

New state needed:
- `_pruningTimer: Timer` — one-shot timer, re-armed each callback
- `_pruningSamples: int[]` — circular buffer of idle count samples
- `_pruningSampleIndex: int` — current write position
- `_pruningSampleSize: int` — `ConnectionLifetime / PruningInterval` (rounded up)
- `_pruningMedianIndex: int` — index of median after sort
- `_pruningSamplingInterval: TimeSpan` — from connection string keyword
- `_pruningTimerEnabled: volatile bool` — whether timer is active
- `_idleCount: volatile int` — live count of idle connections (NOT in Npgsql — we need to add this)

### 4. Idle Count Tracking

**Decision**: Add a `volatile int _idleCount` field to ChannelDbConnectionPool, incremented when a connection is written to the idle channel and decremented when read.

**Rationale**: Npgsql maintains `_idleCount` as a separate volatile field rather than querying the channel's internal count (which isn't exposed). The channel doesn't expose its item count, so we need our own counter for sampling.

**Findings**:

Npgsql's pattern:
- `Interlocked.Increment(ref _idleCount)` in `Return()` when writing to idle channel
- `Interlocked.Decrement(ref _idleCount)` in `CheckIdleConnector()` when reading from idle channel
- Read via `pool._idleCount` in pruning callback (volatile read)

ChannelDbConnectionPool adaptation:
- Increment in `ReturnInternalConnection` when writing to `_idleConnectionWriter`
- Decrement in `GetIdleConnection` when successfully reading a non-null connection
- Also decrement when null is read? No — nulls are wake-up signals, not idle connections
- Also decrement in pruning callback when removing idle connections

### 5. Connection String Keyword

**Decision**: Add `Connection Pruning Interval` keyword with default 10 seconds.

**Rationale**: Matches Npgsql's keyword name and default. Stored in `DbConnectionPoolGroupOptions` since it's pool-level configuration.

**Findings**:

Current `DbConnectionPoolGroupOptions` has: `MinPoolSize`, `MaxPoolSize`, `CreationTimeout`, `LoadBalanceTimeout`, `HasTransactionAffinity`, `PoolByIdentity`, `UseLoadBalancing`.

New keyword needs to flow through:
1. `SqlConnectionStringBuilder` — parse and validate
2. `SqlConnectionString` — internal connection string representation
3. `DbConnectionPoolGroupOptions` — consumed by pool

Note: The spec says the pruning interval should be a connection string keyword. However, `ConnectionLifetime` is also needed for the sample window size calculation. ConnectionLifetime is currently expressed via `LoadBalanceTimeout` in the pool options (mapped from the `Connection Lifetime` / `Load Balance Timeout` keyword). The sample size = `LoadBalanceTimeout / PruningInterval`.

If `LoadBalanceTimeout` is 0 (disabled/default), we need a fallback for sample size. Options:
- Use a fixed default sample size (e.g., 5 or 10 samples)
- Require ConnectionLifetime > 0 for pruning to work on the sample calculation
- Default: if ConnectionLifetime == 0, use a fixed window (e.g., 30 samples = 5 min at 10s interval)

**Decision**: If `LoadBalanceTimeout` is 0, use a fixed 5-minute window (i.e., `300 / PruningInterval` samples). This gives a reasonable observation period without requiring the user to set ConnectionLifetime.

### 6. Timer Lifecycle and Overlap

**Decision**: Use one-shot timer re-armed at the end of each callback, protected by `lock (_pruningTimer)`.

**Rationale**: One-shot prevents overlap intrinsically — the timer won't fire again until explicitly re-armed. Lock prevents races between `UpdatePruningTimer` (called on connection open/close) and the timer callback.

**Findings**:
- Npgsql uses `_pruningTimer.Change(_pruningSamplingInterval, Timeout.InfiniteTimeSpan)` (one-shot)
- Timer is only enabled when `_numConnectors > MinConnections` (`UpdatePruningTimer`)
- On connection close that brings count to MinConnections, timer is disabled
- On connection open that takes count above MinConnections, timer is enabled
- Startup does NOT directly enable the timer — it's enabled lazily via `UpdatePruningTimer` when connections exceed MinPoolSize

### 7. Shared Timer with Idle Timeout (FR-008)

**Decision**: Defer shared timer implementation. For the pruning feature, create a dedicated pruning timer. When idle timeout is added later, the two can be merged.

**Rationale**: FR-008 specifies sharing the timer with idle timeout, but idle timeout is a separate feature (`.github/plans/connection-pool/idle-timeout/`). Building the pruning timer as a clean, separate component makes it straightforward to merge later without blocking either feature.

**Alternatives considered**:
- Build shared timer infrastructure now — rejected as over-engineering; YAGNI until idle timeout is actually implemented.

# Implementation Plan: Pool Pruning

**Branch**: `dev/mdaigle/connection-pool-designs` | **Date**: 2026-04-10 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `.github/plans/connection-pool/pruning/spec.md`

## Summary

Implement periodic idle connection pruning in `ChannelDbConnectionPool` using a sampling-based approach modeled on Npgsql's `PoolingDataSource`. The pool collects idle count samples at a configurable interval, computes the median of recent samples once the buffer is full, and closes that many idle connections (floored at MinPoolSize). This brings the pool toward observed usage levels during low-demand periods without over-pruning during transient lulls.

## Technical Context

**Language/Version**: C# / .NET 8.0+ and .NET Framework 4.6.2  
**Primary Dependencies**: `System.Threading.Timer`, `System.Threading.Channels`, existing `ConnectionPoolSlots`  
**Storage**: N/A (in-memory pool state only)  
**Testing**: MSTest (`tests/UnitTests/ConnectionPool/`)  
**Target Platform**: Windows, Linux, macOS (all SqlClient targets)  
**Project Type**: Library (ADO.NET data provider)  
**Performance Goals**: Timer overhead < 1μs per tick; no allocations in steady-state pruning path; pruning should complete within one pruning interval  
**Constraints**: Must not block connection acquisition; no public API changes beyond connection string keyword  
**Scale/Scope**: One pool per connection string; typical 10-100 MaxPoolSize

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The project constitution is a blank template (no project-specific principles defined). No gates to check.

**Post-design re-check**: N/A — constitution has no actionable gates.

## Project Structure

### Documentation (this feature)

```text
.github/plans/connection-pool/pruning/
├── plan.md              # This file
├── research.md          # Phase 0 output — Npgsql analysis, WaitHandle comparison
├── data-model.md        # Phase 1 output — entities, state transitions
├── quickstart.md        # Phase 1 output — user-facing summary  
├── spec.md              # Feature specification (pre-existing)
├── outline.md           # Feature outline (pre-existing)
└── checklists/
    └── requirements.md  # Quality checklist (pre-existing)
```

### Source Code (repository root)

```text
src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/
├── ConnectionPool/
│   ├── ChannelDbConnectionPool.cs          # PRIMARY — add pruning timer + callback + _idleCount
│   ├── DbConnectionPoolOptions.cs          # Add PruningInterval property
│   ├── ConnectionPoolSlots.cs              # No changes expected
│   └── WaitHandleDbConnectionPool.cs       # Reference only
├── SqlConnectionString.cs                  # Add pruning interval parsing
└── SqlConnectionStringBuilder.cs           # Add Connection Pruning Interval keyword

src/Microsoft.Data.SqlClient/tests/UnitTests/ConnectionPool/
└── ChannelDbConnectionPoolTest.cs          # Add pruning unit tests
```

**Structure Decision**: Single project — all changes in the unified `src/Microsoft.Data.SqlClient/src/` directory. No new files needed; all changes extend existing files.

## Detailed Design

### 1. Connection String Keyword Pipeline

**File**: `SqlConnectionStringBuilder.cs`  
Add `ConnectionPruningInterval` property (int, seconds, default 10). Validation: >= 0 (0 disables pruning).

**File**: `SqlConnectionString.cs`  
Parse the keyword in the internal connection string representation.

**File**: `DbConnectionPoolGroupOptions.cs`  
Add `int PruningInterval` property. Populated from `SqlConnectionString` during pool group creation.

### 2. New Fields in ChannelDbConnectionPool

```csharp
// Pruning state
private readonly Timer _pruningTimer;
private readonly TimeSpan _pruningSamplingInterval;
private readonly int _pruningSampleSize;
private readonly int[] _pruningSamples;
private readonly int _pruningMedianIndex;
private volatile bool _pruningTimerEnabled;
private int _pruningSampleIndex;

// Idle count tracking  
private volatile int _idleCount;

// MinPoolSize accessor
private int MinPoolSize => PoolGroupOptions.MinPoolSize;
```

### 3. Constructor Changes

```csharp
// In constructor, after existing initialization:
var pruningIntervalSeconds = PoolGroupOptions.PruningInterval;
if (pruningIntervalSeconds > 0)
{
    _pruningSamplingInterval = TimeSpan.FromSeconds(pruningIntervalSeconds);
    
    // Sample window: ConnectionLifetime / PruningInterval, or 300s / PruningInterval if lifetime is 0
    var lifetimeSeconds = (int)PoolGroupOptions.LoadBalanceTimeout.TotalSeconds;
    if (lifetimeSeconds <= 0) lifetimeSeconds = 300; // Default 5-minute window
    
    _pruningSampleSize = DivideRoundingUp(lifetimeSeconds, pruningIntervalSeconds);
    _pruningMedianIndex = DivideRoundingUp(_pruningSampleSize, 2) - 1;
    _pruningSamples = new int[_pruningSampleSize];
    
    _pruningTimer = new Timer(PruneIdleConnections, this, Timeout.Infinite, Timeout.Infinite);
}
```

### 4. UpdatePruningTimer Method

Called whenever a connection is opened or closed:

```csharp
private void UpdatePruningTimer()
{
    if (_pruningTimer is null) return; // Pruning disabled
    
    lock (_pruningTimer)
    {
        var count = _connectionSlots.ReservationCount;
        if (count > MinPoolSize && !_pruningTimerEnabled)
        {
            _pruningTimerEnabled = true;
            _pruningTimer.Change(_pruningSamplingInterval, Timeout.InfiniteTimeSpan);
        }
        else if (count <= MinPoolSize && _pruningTimerEnabled)
        {
            _pruningTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _pruningSampleIndex = 0;
            _pruningTimerEnabled = false;
        }
    }
}
```

**Integration points**:
- Call after `_connectionSlots.Add()` succeeds in `OpenNewInternalConnection`
- Call after `RemoveConnection` completes

### 5. PruneIdleConnections Callback (Static)

```csharp
private static void PruneIdleConnections(object? state)
{
    var pool = (ChannelDbConnectionPool)state!;
    var samples = pool._pruningSamples;
    int toPrune;
    
    lock (pool._pruningTimer)
    {
        if (!pool._pruningTimerEnabled) return;
        
        var sampleIndex = pool._pruningSampleIndex;
        samples[sampleIndex] = pool._idleCount;
        
        if (sampleIndex != pool._pruningSampleSize - 1)
        {
            // Not enough samples yet — increment and re-arm
            pool._pruningSampleIndex = sampleIndex + 1;
            pool._pruningTimer.Change(pool._pruningSamplingInterval, Timeout.InfiniteTimeSpan);
            return;
        }
        
        // Buffer full — compute median
        Array.Sort(samples);
        toPrune = samples[pool._pruningMedianIndex];
        pool._pruningSampleIndex = 0;
        pool._pruningTimer.Change(pool._pruningSamplingInterval, Timeout.InfiniteTimeSpan);
    }
    
    // Prune outside the lock
    while (toPrune > 0 &&
           pool._connectionSlots.ReservationCount > pool.MinPoolSize &&
           pool._idleConnectionReader.TryRead(out var connection))
    {
        if (connection is null) continue;
        
        Interlocked.Decrement(ref pool._idleCount); // not in Npgsql; see note below
        
        if (pool.IsLiveConnection(connection))
        {
            pool.RemoveConnection(connection);
            toPrune--;
        }
        else
        {
            pool.RemoveConnection(connection); // Dead connection — remove regardless
        }
    }
}
```

**Note on idle count**: Npgsql decrements `_idleCount` in `CheckIdleConnector` which is called both during normal retrieval and during pruning. We can follow the same pattern by decrementing in a shared helper.

### 6. Idle Count Integration

**ReturnInternalConnection** — after writing to channel:
```csharp
var written = _idleConnectionWriter.TryWrite(connection);
Debug.Assert(written);
Interlocked.Increment(ref _idleCount);
```

**GetIdleConnection** — when reading a non-null connection:
```csharp
if (connection is null) continue;  // Don't decrement for null wake-ups
Interlocked.Decrement(ref _idleCount);
```

### 7. Startup / Shutdown

**Startup**: No explicit pruning work needed — timer is lazy-enabled via `UpdatePruningTimer` when connections exceed MinPoolSize.

**Shutdown**:
```csharp
public void Shutdown()
{
    State = ShuttingDown;
    _pruningTimer?.Dispose();
    // ... existing shutdown logic (close idle channel, etc.)
}
```

### 8. Requirement Traceability

| Requirement | Implementation |
|-------------|---------------|
| FR-001 (periodic evaluation) | `PruneIdleConnections` callback + `_pruningTimer` |
| FR-002 (sampling + median) | `_pruningSamples[]` + `Array.Sort` + `_pruningMedianIndex` |
| FR-003 (MinPoolSize floor) | `ReservationCount > MinPoolSize` guard in prune loop |
| FR-004 (slot release) | `RemoveConnection` → `_connectionSlots.TryRemove` + `Dispose` |
| FR-005 (connection string keyword) | `Connection Pruning Interval` in `SqlConnectionStringBuilder` |
| FR-006 (stop on shutdown) | `_pruningTimerEnabled` check + `Dispose` in `Shutdown` |
| FR-007 (no overlap) | One-shot timer + `lock (_pruningTimer)` |
| FR-008 (shared timer) | Deferred — dedicated pruning timer for now; merge with idle timeout later |

## Complexity Tracking

No constitution violations to justify.

## Risk Assessment

| Risk | Mitigation |
|------|-----------|
| Pruning too aggressively during brief lulls | Median of N samples smooths transient drops |
| Timer callback races with Shutdown | Lock + `_pruningTimerEnabled` guard + Timer.Dispose |
| `_idleCount` drifts under concurrency | Volatile field + Interlocked operations; self-correcting across samples |
| Connection string keyword not wired end-to-end | Test keyword parsing in SqlConnectionStringBuilder tests |
| Pruning during pool Clear | Clear sets `_clearCounter`; pruned connections' stale counter caught on next use. No direct conflict since prune reads from idle channel (same as Clear). |

## Dependencies on Other Features

| Feature | Dependency Type | Notes |
|---------|----------------|-------|
| Idle Timeout | Future merge target | FR-008 shared timer deferred; timer will be merged when idle timeout is implemented |
| Shutdown | Prerequisite | `Shutdown()` must be implemented to dispose timer; minimal implementation sufficient |
| Warmup | Independent | Warmup replenishes to MinPoolSize; pruning reduces toward MinPoolSize. No conflict. |
| Transactions | Independent | Transaction roots checked at connection return, before entering idle channel. Pruning only removes from idle channel. |
| Clear | Low coupling | Both read from idle channel; Clear uses `_clearCounter` generation; no lock ordering conflict |

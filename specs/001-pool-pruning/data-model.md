# Data Model: Pool Pruning

**Feature**: Pool Pruning for ChannelDbConnectionPool  
**Date**: 2026-04-10

## Entities

### PruningSampleBuffer

A fixed-size circular buffer that stores idle connection count samples collected at each pruning interval tick.

| Field | Type | Description |
|-------|------|-------------|
| `_pruningSamples` | `int[]` | Fixed-size array of idle count snapshots |
| `_pruningSampleIndex` | `int` | Current write position (0-based, wraps at `_pruningSampleSize`) |
| `_pruningSampleSize` | `int` | Total number of samples = `ceil(ConnectionLifetimeSeconds / PruningIntervalSeconds)`. If ConnectionLifetime is 0, defaults to `ceil(300 / PruningIntervalSeconds)` (5-minute window). |
| `_pruningMedianIndex` | `int` | Index of the median element after sorting = `ceil(_pruningSampleSize / 2) - 1` |

**Lifecycle**:
- Created in constructor (once per pool instance)
- Written to on each pruning timer tick
- Read (sorted + median extracted) when sample buffer is full
- Reset (`_pruningSampleIndex = 0`) after each full cycle + prune

**Invariants**:
- `0 <= _pruningSampleIndex < _pruningSampleSize`
- `_pruningSampleSize >= 1`  
- `_pruningMedianIndex >= 0 && _pruningMedianIndex < _pruningSampleSize`
- Array is always exactly `_pruningSampleSize` elements

### IdleCount

A volatile integer counter tracking the number of connections currently idle in the channel.

| Field | Type | Description |
|-------|------|-------------|
| `_idleCount` | `volatile int` | Current number of idle connections in the channel |

**Update points**:
- **Increment**: When a connection is written to `_idleConnectionWriter` in `ReturnInternalConnection`
- **Decrement**: When a non-null connection is read from `_idleConnectionReader` in `GetIdleConnection`
- **Decrement**: When a connection is removed from the channel during pruning

**Invariants**:
- `_idleCount >= 0` (may briefly be negative under high concurrency due to volatile vs atomic, but self-correcting)

### PruningTimer

The one-shot timer that drives the pruning callback.

| Field | Type | Description |
|-------|------|-------------|
| `_pruningTimer` | `Timer` | One-shot timer, re-armed after each callback |
| `_pruningTimerEnabled` | `volatile bool` | Whether the timer is active |
| `_pruningSamplingInterval` | `TimeSpan` | Interval between samples (from connection string) |

**Lifecycle**:
- Timer object created in constructor (initialized to `Timeout.Infinite`)
- Enabled when connection count exceeds `MinPoolSize` (via `UpdatePruningTimer`)
- Disabled when connection count drops to `MinPoolSize` (via `UpdatePruningTimer`)  
- Disposed in `Shutdown()`

**Invariants**:
- Timer is never periodic — always one-shot (`Timeout.InfiniteTimeSpan` for period)
- Timer callback re-arms the timer at the end of each invocation
- `lock (_pruningTimer)` protects all timer state mutations

## State Transitions

```
Pool Created (constructor)
  → Timer exists but disabled (Timeout.Infinite)
  → _pruningSampleIndex = 0
  → _pruningTimerEnabled = false

Connection opened (numConnectors > MinPoolSize)
  → UpdatePruningTimer() enables timer
  → Timer fires after _pruningSamplingInterval

Timer fires (sample collection)
  → Record _idleCount into _pruningSamples[_pruningSampleIndex]
  → If buffer not full: increment index, re-arm, return
  → If buffer full: sort, extract median, reset index, re-arm

Prune execution (after median extracted)
  → Read up to `toPrune` connections from idle channel
  → Close valid connections via RemoveConnection
  → Stop at MinPoolSize floor

Connection closed (numConnectors == MinPoolSize)
  → UpdatePruningTimer() disables timer
  → _pruningSampleIndex reset to 0

Pool shutdown
  → Timer disposed
  → No further callbacks
```

## Relationships

```
ChannelDbConnectionPool
  ├── ConnectionPoolSlots (_connectionSlots)
  │     └── ReservationCount → total managed connections
  ├── Channel<DbConnectionInternal?> (idle store)
  │     ├── _idleConnectionReader → read idle connections
  │     └── _idleConnectionWriter → return idle connections
  ├── PruningSampleBuffer (new)
  │     ├── _pruningSamples[]
  │     ├── _pruningSampleIndex
  │     └── _pruningMedianIndex
  ├── PruningTimer (new)
  │     ├── _pruningTimer
  │     ├── _pruningTimerEnabled
  │     └── _pruningSamplingInterval
  ├── _idleCount (new) → sampled by pruning
  └── DbConnectionPoolGroupOptions
        ├── MinPoolSize → pruning floor
        ├── MaxPoolSize → capacity
        └── PruningInterval (new) → sampling cadence
```

# Fix 2: Implement Startup/Shutdown Lifecycle

**Priority:** Critical — pool must start up to serve connections
**Risk:** Low

## Problem

`Startup()` and `Shutdown()` both throw `NotImplementedException`. The pool cannot function without
these lifecycle methods.

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/ChannelDbConnectionPool.cs`

## Changes Required

### Startup()

Called by the factory after pool construction. Should:

1. Initialize the `Channel<DbConnectionInternal?>` (bounded, capacity = max pool size)
2. Initialize `ConnectionPoolSlots` with max pool size
3. If `MinPoolSize > 0`, schedule an async warmup task (see Fix 4)
4. Set pool state to "running"

Reference: `WaitHandleDbConnectionPool` constructor (lines ~100-200) handles equivalent
initialization including:

- `_poolSemaphore = new Semaphore(maxPoolSize, maxPoolSize)`
- `_creationSemaphore = new Semaphore(1, 1)`
- Timer-based cleanup scheduling

The new pool should NOT use Semaphore/WaitHandle — use `SemaphoreSlim` for any counting needs.

### Shutdown()

Called when the pool is being torn down. Should:

1. Mark pool as "shutting down" (prevent new acquisitions)
2. Complete the channel writer (`.TryComplete()`)
3. Drain remaining idle connections from the channel and close them
4. Cancel any background tasks (warmup, pruning)
5. Dispose `ConnectionPoolSlots`

## Testing

- Unit test: Create pool → `Startup()` → verify state → `Shutdown()` → verify all connections
  closed.

- Unit test: After `Shutdown()`, `GetInternalConnection()` should throw or return an error.

## Design Notes

The `WaitHandleDbConnectionPool` doesn't have explicit `Startup/Shutdown` methods — it does
everything in the constructor/finalizer. The `IDbConnectionPool` interface requires these methods,
so the new pool should use them as the formal lifecycle entry points.

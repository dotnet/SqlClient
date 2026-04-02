# Fix 2: Unify Sync/Async Locking in SniNpHandle

**Priority:** Low (Named Pipes less common than TCP)
**Complexity:** Medium
**Risk:** Medium

## Problem

`SniNpHandle.Send()` (line ~276) uses `Monitor.Enter(this)` — the same blocking pattern as
`SniTcpHandle`. Named Pipe connections are less common but still affected by thread pool starvation
under contention.

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ManagedSni/SniNpHandle.netcore.cs`

- `Send()` (line ~276)
- `Monitor.Enter(this)`

## Changes Required

Mirror the `SemaphoreSlim` approach from Fix 1:

1. Replace `Monitor.Enter(this)` with `SemaphoreSlim.Wait()` for sync
2. Add `SemaphoreSlim.WaitAsync()` for async send path
3. Separate read and write locks

The implementation is identical in structure to Fix 1 but for Named Pipes.

## Testing

- Integration test with Named Pipe connections
- Primarily relevant for local SQL Server (Named Pipes are local-only)

## Risk

- Medium — same as Fix 1
- Lower priority because Named Pipes are less commonly used in high-throughput scenarios where
  thread pool starvation is a concern

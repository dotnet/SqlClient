# Fix 3: Implement Clear() for Pool Reset

**Priority:** Critical — required for `SqlConnection.ClearPool()` / `ClearAllPools()`
**Risk:** Low

## Problem

`Clear()` throws `NotImplementedException`. Users and tests call `SqlConnection.ClearPool()` and
`SqlConnection.ClearAllPools()` frequently.

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/ChannelDbConnectionPool.cs`

## Changes Required

Implement `Clear()` to:

1. Drain all idle connections from the channel and close them
2. Mark all in-use connections (tracked in `ConnectionPoolSlots`) for disposal on return — when they
   come back via `ReturnInternalConnection()`, close them instead of returning to the channel

3. Increment a "clear counter" so returned connections with stale generation numbers are discarded

Reference: `WaitHandleDbConnectionPool.Clear()` uses a similar generation-counter approach.

### Implementation Sketch

```csharp
public override void Clear()
{
    Interlocked.Increment(ref _clearCounter);

    // Drain channel of idle connections
    while (_idleChannel.Reader.TryRead(out var connection))
    {
        if (connection != null)
        {
            DestroyConnection(connection);
        }
    }

    // In-use connections will be destroyed on return
    // (ReturnInternalConnection checks _clearCounter)
}
```

## Testing

- Unit test: Add connections to pool → `Clear()` → verify all idle connections closed.

- Unit test: Connection returned after `Clear()` is destroyed, not recycled.
- Integration test: `SqlConnection.ClearPool()` works with V2 pool.

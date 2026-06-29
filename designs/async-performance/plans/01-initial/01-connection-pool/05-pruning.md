# Fix 5: Implement Pool Pruning

**Priority:** High — prevents connection leaks and unbounded pool growth
**Risk:** Low

## Problem

The V1 pool uses a timer callback (`PruneConnectionPoolGroups`) with 4-minute due time and 30-second
period (defined in `SqlConnectionFactory.cs` line ~843). It removes connections that exceed
`ConnectionLifetime` and trims idle connections back toward `MinPoolSize`. The V2 pool has no
pruning.

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/ChannelDbConnectionPool.cs`

## Changes Required

Add a background `Task` that periodically prunes the idle channel:

1. Run on a timer (e.g., every 30 seconds)
2. Read connections from the channel
3. Check each connection:
   - Has it exceeded `ConnectionLifetime`? → Close it
   - Is the pool oversized (idle count > `MinPoolSize` and no recent demand)? → Close it
   - Is the connection broken (`IsConnectionAlive()` check)? → Close it
4. Return healthy connections back to the channel

### Implementation Sketch

```csharp
private async Task PruneAsync(CancellationToken cancellationToken)
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

    while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
    {
        int count = _idleChannel.Reader.Count;
        for (int i = 0; i < count; i++)
        {
            if (!_idleChannel.Reader.TryRead(out var connection) || connection == null)
                break;

            if (ShouldPrune(connection))
            {
                DestroyConnection(connection);
            }
            else
            {
                _idleChannel.Writer.TryWrite(connection);
            }
        }
    }
}
```

### Clear Counter Integration

On `Clear()`, increment a clear counter. During pruning (and in `ReturnInternalConnection`), compare
the connection's generation to the current counter and discard stale connections.

## Testing

- Unit test: Add connections with expired lifetime → prune → verify closed.
- Unit test: Prune doesn't remove connections below `MinPoolSize`.
- Unit test: Prune removes broken connections.

## Reference

- V1 pruning: `WaitHandleDbConnectionPool` uses `_stackOld` / `_stackNew` with a timer that moves
  `_stackNew` → `_stackOld` → destroy pattern.

- V2 pruning should be simpler since Channel provides FIFO ordering — older connections are read
  first.

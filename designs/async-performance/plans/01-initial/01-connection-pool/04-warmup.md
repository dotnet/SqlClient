# Fix 4: Implement Pool Warmup

**Priority:** High — directly addresses cold-start issues (#601)
**Risk:** Low

## Problem

The legacy pool warms up via `QueuePoolCreateRequest()` which schedules a background callback to
create connections up to `MinPoolSize`. The new pool has no warmup implementation.

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/ChannelDbConnectionPool.cs`

## Changes Required

After `Startup()`, if `MinPoolSize > 0`, launch an async warmup task:

```csharp
private async Task WarmupAsync(CancellationToken cancellationToken)
{
    var tasks = new List<Task>();

    for (int i = 0; i < _options.MinPoolSize; i++)
    {
        tasks.Add(CreateAndAddConnectionAsync(cancellationToken));
    }

    // Open connections in PARALLEL — this is the key improvement over V1
    await Task.WhenAll(tasks).ConfigureAwait(false);
}

private async Task CreateAndAddConnectionAsync(CancellationToken ct)
{
    var connection = await OpenNewInternalConnection(ct).ConfigureAwait(false);
    _idleChannel.Writer.TryWrite(connection);
}
```

### Key Design Decision: Parallel Creation

The V1 pool creates warmup connections serially (one semaphore-guarded creation at a time). The V2
pool should create them in parallel — this is the primary value proposition for cold-start
scenarios.

Consider a configurable concurrency limit (e.g., 4–8 concurrent opens) to avoid overwhelming the
server during warmup while still being much faster than serial creation.

## Testing

- Unit test: Pool with `MinPoolSize=10`, verify 10 connections created after startup.
- Benchmark: Compare cold-start time for 100 connections, V1 vs V2.
- Integration test: Pool warmup completes before first `OpenAsync()` returns.

## Risk

- Low — warmup is fire-and-forget; if it fails, connections are created on demand.
- Add error handling per-connection (don't let one failure abort all warmup).

# Fix 5: End-to-End Async Open Pipeline

**Priority:** High (but depends on Fixes 1-4)
**Complexity:** High
**Risk:** Medium

## Problem

Even with individual async components (Fixes 1-4), the overall `OpenAsync()` flow is orchestrated
synchronously in `SqlInternalConnectionTds`. The `LoginNoFailover()` / `LoginWithFailover()` methods
contain retry loops, routing redirect handling, and error recovery — all synchronous.

## Location

**Files:**

- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/Connection/SqlConnectionInternal.cs`
  - `LoginNoFailover()` (line ~3138)
  - `LoginWithFailover()`
  - `AttemptOneLogin()` (line ~2177)
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlConnection.cs`
  - `OpenAsync()` — the public entry point
  - `TryOpen()` with `TaskCompletionSource<DbConnectionInternal>` retry pattern

## Changes Required

### 1. Async LoginNoFailover

```csharp
private async ValueTask LoginNoFailoverAsync(
    ServerInfo serverInfo, ..., CancellationToken cancellationToken)
{
    int routingAttempts = 0;

    while (routingAttempts < MaxRoutingAttempts)
    {
        try
        {
            await AttemptOneLoginAsync(serverInfo, ..., cancellationToken)
                .ConfigureAwait(false);

            if (_parser.RoutingInfo != null)
            {
                // Handle routing redirect
                serverInfo = new ServerInfo(_parser.RoutingInfo);
                routingAttempts++;
                continue;
            }

            return; // Success
        }
        catch (SqlException ex) when (IsTransient(ex))
        {
            if (routingAttempts >= MaxRoutingAttempts)
                throw;
            // Retry
        }
    }
}
```

### 2. Wire Into SqlConnection.OpenAsync()

The current `OpenAsync()` in `SqlConnection.cs` calls `TryOpen()` which uses a
`TaskCompletionSource<DbConnectionInternal>` pattern. This needs to be updated to call the async
login path when the pool provides a new (not cached) connection:

```csharp
// In the connection factory, when creating a new internal connection:
if (isAsync)
{
    await internalConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
}
else
{
    internalConnection.Open();
}
```

### 3. Preserve Sync Path

The sync `Open()` path must continue to work unchanged. This means:

- Keep all existing sync methods
- Async variants are separate methods (not sync-over-async or async-over-sync)
- The `bool isAsync` parameter already exists in some call sites — use it to route to the correct
  path

## End-to-End Async Flow (Target State)

```text
SqlConnection.OpenAsync()
  → Pool.GetInternalConnectionAsync()       [from P1 pool redesign]
    → SqlInternalConnectionTds.OpenAsync()
      → LoginNoFailoverAsync()
        → AttemptOneLoginAsync()
          → SniTcpHandle.CreateAsync()       [Fix 1: async TCP]
          → EnableSslAsync()                  [Fix 2: async TLS]
          → SendPreLoginHandshakeAsync()      [Fix 3: async prelogin]
          → TdsLoginAsync()                   [Fix 4: async login]
        ← Returns without blocking any thread
```

## Testing

- End-to-end benchmark: `OpenAsync()` with V2 pool, all async components
- Thread test: `OpenAsync()` on a thread pool with only 1 thread — must not deadlock
- Redirect test: Server routing redirect handled asynchronously
- Failover test: Failover partner connection handled asynchronously
- Timeout test: Connection timeout works correctly in async path
- Cancel test: Cancellation token cancels at each stage

## Risk

- Medium — the login flow has many code paths (failover, routing, reconnection, different auth
  modes). Each must work correctly in async.

- This is the final integration step — it depends on all of Fixes 1-4.

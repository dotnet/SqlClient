# Fix 1: Wire Up Factory Routing

**Priority:** Critical — pool V2 cannot be used at all without this
**Risk:** Low — guarded by AppContext switch

## Problem

In `SqlConnectionFactory.cs`, the `UseConnectionPoolV2` code path throws `NotImplementedException`
instead of constructing a `ChannelDbConnectionPool`:

```csharp
if (LocalAppContextSwitches.UseConnectionPoolV2)
{
    throw new NotImplementedException();  // ← Must be replaced
}
else
{
    newPool = new WaitHandleDbConnectionPool(...);
}
```

## Location

**File:** `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlConnectionFactory.cs`
**Method:** `DbConnectionPoolGroup.GetConnectionPool()` (around line 180)

## Changes Required

1. Replace `throw new NotImplementedException()` with:

   ```csharp
   newPool = new ChannelDbConnectionPool(
       connectionPoolIdentity,
       connectionPoolOptions,
       connectionPoolProviderInfo,
       owningObject,
       this);
   ```

2. Verify the `ChannelDbConnectionPool` constructor signature matches the parameters available at
   this call site. The existing `IDbConnectionPool` interface defines the contract.

3. Ensure `ChannelDbConnectionPool` is accessible from `SqlConnectionFactory` (same
   namespace/assembly — it is).

## Testing

- Unit test: Set `UseConnectionPoolV2 = true`, create a `SqlConnection`, verify pool construction
  doesn't throw.

- This test will initially fail on `Startup()` (Fix 2), so it should be submitted together with Fix
  2.

## Risk Assessment

- **Very low risk** — The `UseConnectionPoolV2` switch defaults to `false`. No production code is
  affected unless the switch is explicitly enabled.

- Must verify that existing tests with `UseConnectionPoolV2 = false` still pass.

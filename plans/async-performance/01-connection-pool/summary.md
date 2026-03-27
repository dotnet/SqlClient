# Priority 1: Complete the ChannelDbConnectionPool

**Addresses:** #3356, #601, #979, #2152, #3118
**Impact:** High — resolves the #1 source of async performance complaints

## Current State

The `ChannelDbConnectionPool` is partially implemented at
[ConnectionPool/ChannelDbConnectionPool.cs](../../../src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/ChannelDbConnectionPool.cs).

### What's Working

- Channel-based idle connection queue (`Channel<DbConnectionInternal?>`)
- `GetInternalConnection()` — async connection acquisition
- `ReturnInternalConnection()` — connection return to idle channel
- `OpenNewInternalConnection()` — physical connection creation via `ConnectionPoolSlots`
- `SemaphoreSlim` for sync-over-async protection
- AppContext switch (`UseConnectionPoolV2`) in `LocalAppContextSwitches.cs` (line 121)

### What Throws NotImplementedException

- `ErrorOccurred` property
- `Clear()`
- `PutObjectFromTransactedPool()`
- `ReplaceConnection()`
- `Shutdown()`
- `Startup()`
- `TransactionEnded()`

### Factory Routing Is Broken

In
[SqlConnectionFactory.cs](../../../src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlConnectionFactory.cs)
line ~180, the V2 code path throws `NotImplementedException()` instead of constructing a
`ChannelDbConnectionPool`.

## Incremental Fixes

| # | Fix | File | Priority |
| --- | ----- | ------ | ---------- |
| 1 | [Wire up factory routing](01-factory-routing.md) | SqlConnectionFactory.cs | Critical |
| 2 | [Implement Startup/Shutdown lifecycle](02-lifecycle.md) | ChannelDbConnectionPool.cs | Critical |
| 3 | [Implement Clear() for pool reset](03-clear.md) | ChannelDbConnectionPool.cs | Critical |
| 4 | [Implement pool warmup](04-warmup.md) | ChannelDbConnectionPool.cs | High |
| 5 | [Implement pool pruning](05-pruning.md) | ChannelDbConnectionPool.cs | High |
| 6 | [Implement transaction enlistment](06-transactions.md) | ChannelDbConnectionPool.cs + TransactedConnectionPool.cs | High |
| 7 | [Implement error handling and backoff](07-error-handling.md) | ChannelDbConnectionPool.cs | High |
| 8 | [Add opt-in rate limiting](08-rate-limiting.md) | ChannelDbConnectionPool.cs | Medium |
| 9 | [Add metrics and tracing](09-metrics.md) | ChannelDbConnectionPool.cs | Medium |
| 10 | [Integration test suite](10-integration-tests.md) | Tests/ | Medium |

## Dependencies

- Fixes 1–3 must be done first (pool is non-functional without them)
- Fix 6 depends on `TransactedConnectionPool.cs` refactoring (PR #3746 already merged)
- Fixes 4, 5, 7 are independent of each other
- Fixes 8, 9, 10 can be done in any order after 1–7

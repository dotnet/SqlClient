# Fix 7: Implement Error Handling and Backoff

**Priority:** High — production pools must handle transient errors gracefully
**Risk:** Low-Medium

## Problem

`ErrorOccurred` property throws `NotImplementedException`. The V1 pool has sophisticated error
handling with exponential backoff (`_errorWait` defaults to 5 seconds) and an `_errorEvent`
ManualResetEvent that signals error state to all waiters. The V2 pool has none of this.

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/ChannelDbConnectionPool.cs`

## Changes Required

1. **Implement `ErrorOccurred` property** — Track whether recent connection attempts have failed.

2. **Error backoff** — When a connection creation fails, apply a short delay before the next attempt
   to prevent hammering a down server:

   ```csharp
   private DateTime _errorExpiry = DateTime.MinValue;

   private bool IsInErrorState => DateTime.UtcNow < _errorExpiry;

   private void OnConnectionError(Exception ex)
   {
       _errorExpiry = DateTime.UtcNow.AddSeconds(5); // backoff period
   }
   ```

3. **Error propagation** — When the pool is in error state, new `GetInternalConnection()` calls
   should either:

   - Wait for the backoff period to expire, then retry
   - Or throw immediately with the original error context

4. **Recovery** — After a successful connection, clear the error state.

5. **Implement `ReplaceConnection()`** — When a connection is found to be broken, replace it with a
   new one:

   ```csharp
   public override void ReplaceConnection(DbConnectionInternal oldConnection)
   {
       DestroyConnection(oldConnection);
       // Schedule async creation of a replacement
       _ = CreateAndAddConnectionAsync(CancellationToken.None);
   }
   ```

## Reference

The V1 pool's error handling is in `WaitHandleDbConnectionPool`:

- `_errorEvent` ManualResetEvent — signals error to all `WaitForAvailableConnection()` callers
- `_errorWait` — backoff duration (default 5 seconds)
- `_errorTimer` — timer that clears error state
- Multiple retry paths with different wait strategies

The V2 pool should use simpler async-friendly patterns (no WaitHandle).

## Testing

- Unit test: Connection failure → pool enters error state → backoff applied
- Unit test: Successful connection after error → error state cleared
- Unit test: `ReplaceConnection` destroys old and creates new

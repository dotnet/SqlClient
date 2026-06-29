# Fix 8: Add Opt-In Rate Limiting

**Priority:** Medium — community consensus is opt-in, not default
**Risk:** Low

## Problem

The V1 pool uses a `_creationSemaphore` (Semaphore(1,1)) that serializes physical connection
creation to protect SQL Server from connection floods. This is the primary cause of slow cold starts
(#601). The V2 pool should allow parallel creation by default but offer opt-in rate limiting.

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/ChannelDbConnectionPool.cs`

## Changes Required

1. **No default rate limiting** — The V2 pool should create connections in parallel by default (this
   is the key perf win).

2. **Add a `ConnectionCreationRateLimit` option** — integer property controlling max concurrent
   physical connection creations. Default = 0 (unlimited).

3. **Implementation** — If rate limit > 0, use `SemaphoreSlim(rateLimit)`:

   ```csharp
   if (_connectionCreationRateLimit > 0)
   {
       await _creationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
       try
       {
           return await OpenNewInternalConnection(ct).ConfigureAwait(false);
       }
       finally
       {
           _creationSemaphore.Release();
       }
   }
   ```

4. **Consider whether this should be a connection string keyword or AppContext switch** — community
   feedback from discussion #2612 favors it being opt-in and application-level, not
   per-connection-string.

## Discussion Context

From #2612:

- @roji: Rate limiting should be server-side, not client-side
- @mdaigle: Microsoft received feedback about protecting recovery scenarios
- Consensus: Make it opt-in, not default

## Testing

- Benchmark: Parallel vs rate-limited cold start comparison
- Unit test: Rate limit of 1 → connections created serially
- Unit test: Rate limit of 0 → connections created in parallel

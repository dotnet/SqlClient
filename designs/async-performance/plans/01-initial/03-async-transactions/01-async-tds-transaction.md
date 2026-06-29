# Fix 1: Add Async TdsExecuteTransactionManagerRequest

**Priority:** High — foundation for all async transaction work
**Complexity:** Medium
**Risk:** Medium

## Problem

`TdsExecuteTransactionManagerRequest()` (TdsParser.cs line 9819) is the sole code path for sending
BEGIN, COMMIT, and ROLLBACK to SQL Server. It is purely synchronous:

1. Acquires `_connHandler._parserLock.Wait()` — a blocking `SemaphoreSlim` wait
2. Sets `_asyncWrite = false` — disables async writes
3. Writes the transaction RPC to the out buffer
4. Calls `stateObj.WritePacket(TdsEnums.HARDFLUSH)` — synchronous flush
5. Calls `Run()` to read the response — synchronous read

## Location

**File:** `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParser.cs`
**Method:** `TdsExecuteTransactionManagerRequest()` (line ~9819)

## Changes Required

### 1. Create Async Variant

```csharp
internal async ValueTask<SqlDataReader> TdsExecuteTransactionManagerRequestAsync(
    byte[] buffer,
    TdsEnums.TransactionManagerRequestType request,
    string transactionName,
    TdsEnums.TransactionManagerIsolationLevel isoLevel,
    int timeout,
    SqlInternalTransaction transaction,
    TdsParserStateObject stateObj,
    bool isDelegateControlRequest,
    CancellationToken cancellationToken)
{
    // 1. Async lock acquisition
    await _connHandler._parserLock.WaitAsync(cancellationToken)
        .ConfigureAwait(false);
    try
    {
        // 2. Write transaction RPC (same as sync — buffer writing is in-memory)
        WriteTransactionManagerRequest(buffer, request, transactionName,
            isoLevel, transaction, stateObj, isDelegateControlRequest);

        // 3. Async flush
        await stateObj.WritePacketAsync(TdsEnums.HARDFLUSH, cancellationToken)
            .ConfigureAwait(false);

        // 4. Async response read
        await RunAsync(RunBehavior.UntilDone, null, null, null, stateObj)
            .ConfigureAwait(false);
    }
    finally
    {
        _connHandler._parserLock.Release();
    }
}
```

### 2. Extract Write Logic

Factor the buffer-writing portion of `TdsExecuteTransactionManagerRequest()` into a shared helper
`WriteTransactionManagerRequest()` that both sync and async paths can call. The write portion is
pure in-memory buffer manipulation and doesn't need to be async.

### 3. Verify WritePacketAsync Exists

Check whether `TdsParserStateObject.WritePacketAsync()` exists. If not, it needs to be created. The
existing `WritePacket()` method calls `WriteSni()` which has both sync and async paths — the
infrastructure may already be available.

### 4. Verify RunAsync Exists

The async variant of `Run()` (which wraps `TryRun()`) needs to exist for reading the transaction
response. `TryRun()` already returns `TdsOperationStatus.NeedMoreData` for the async case — there
should be an async wrapper that awaits data availability.

## Testing

- Unit test: `TdsExecuteTransactionManagerRequestAsync` sends correct TDS bytes
- Integration test: Async BEGIN → execute query → async COMMIT succeeds
- Integration test: Async BEGIN → async ROLLBACK succeeds
- Concurrency test: Multiple async transactions on different connections

## Risk

- Medium — the parser lock is shared state. If async acquisition introduces a deadlock path, it
  could hang connections.

- The `_asyncWrite = false` line in the sync path suggests async writes have known issues in this
  context. Investigate whether the async flush is safe.

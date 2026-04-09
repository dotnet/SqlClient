# Fix 2: Override CommitAsync/RollbackAsync

**Priority:** Medium — user-facing async API
**Complexity:** Low (once Fix 1 is done)
**Risk:** Low

## Problem

`SqlTransaction` does not override `DbTransaction.CommitAsync()` or `DbTransaction.RollbackAsync()`.
The base implementation just calls `Commit()` / `Rollback()` synchronously.

## Location

**File:** `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlTransaction.cs`

## Changes Required

### Override CommitAsync

```csharp
public override async Task CommitAsync(CancellationToken cancellationToken = default)
{
    using (DiagnosticTransactionScope diagnosticScope =
        SqlClientDiagnosticListenerExtensions.CreateTransactionCommitScope(...))
    {
        cancellationToken.ThrowIfCancellationRequested();

        SqlStatistics statistics = null;
        try
        {
            statistics = SqlStatistics.StartTimer(Statistics);
            await _internalTransaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            diagnosticScope.SetException(e);
            throw;
        }
        finally
        {
            SqlStatistics.StopTimer(statistics);
        }
    }
}
```

### Override RollbackAsync

Same pattern as `CommitAsync` but calling `_internalTransaction.RollbackAsync()`.

### Add Async Methods to SqlInternalTransaction

```csharp
internal async Task CommitAsync(CancellationToken cancellationToken)
{
    // Validate state (same as sync)
    if (_innerConnection.IsDoNotAccessGetterRecoverableError)
        _innerConnection.DoomThisConnection();

    await _innerConnection.ExecuteTransactionAsync(
        TransactionRequest.Commit, null, IsolationLevel.Unspecified,
        _internalTransaction, false, cancellationToken)
        .ConfigureAwait(false);
}
```

This requires `SqlInternalConnectionTds.ExecuteTransactionAsync()` which calls
`TdsExecuteTransactionManagerRequestAsync()` from Fix 1.

## Public API Changes

These methods override existing `virtual` methods on `DbTransaction`, so they don't change the
public API surface. However, they should still be added to the reference assemblies:

- `netcore/ref/Microsoft.Data.SqlClient.cs`
- `netfx/ref/Microsoft.Data.SqlClient.cs`

## Testing

- Integration test: `await transaction.CommitAsync()` succeeds
- Integration test: `await transaction.RollbackAsync()` succeeds
- Thread test: `CommitAsync` doesn't block the calling thread
- Cancellation test: Cancellation token works correctly

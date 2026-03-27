# Fix 3: Override BeginTransactionAsync

**Priority:** Medium — user-facing async API
**Complexity:** Low (once Fix 1 is done)
**Risk:** Low

## Problem

`SqlConnection.BeginTransactionAsync()` is not overridden. The base
`DbConnection.BeginTransactionAsync()` calls `BeginTransaction()` synchronously.

## Location

**File:** `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlConnection.cs`

## Changes Required

### Override BeginTransactionAsync

```csharp
protected override async ValueTask<DbTransaction> BeginDbTransactionAsync(
    IsolationLevel isolationLevel,
    CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();

    SqlTransaction transaction = new SqlTransaction(_innerConnection, this,
        isolationLevel, null);

    await _innerConnection.ExecuteTransactionAsync(
        TransactionRequest.Begin, null, isolationLevel,
        transaction._internalTransaction, false, cancellationToken)
        .ConfigureAwait(false);

    return transaction;
}
```

### Add Public Convenience Method

```csharp
public new async Task<SqlTransaction> BeginTransactionAsync(
    CancellationToken cancellationToken = default)
{
    return (SqlTransaction)await base.BeginTransactionAsync(cancellationToken)
        .ConfigureAwait(false);
}

public new async Task<SqlTransaction> BeginTransactionAsync(
    IsolationLevel iso,
    CancellationToken cancellationToken = default)
{
    return (SqlTransaction)await base.BeginTransactionAsync(iso, cancellationToken)
        .ConfigureAwait(false);
}
```

## Public API Changes

New public method overloads on `SqlConnection`:

- `BeginTransactionAsync(CancellationToken)`
- `BeginTransactionAsync(IsolationLevel, CancellationToken)`

These must be added to reference assemblies in `netcore/ref/` and `netfx/ref/`.

## Testing

- Integration test: `await conn.BeginTransactionAsync()` returns valid transaction
- Integration test: Full async flow: OpenAsync → BeginTransactionAsync → ExecuteNonQueryAsync →
  CommitAsync

- Thread test: BeginTransactionAsync doesn't block the calling thread

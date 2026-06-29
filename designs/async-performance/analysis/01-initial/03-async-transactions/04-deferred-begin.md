# Fix 4: Deferred BEGIN TRANSACTION

**Priority:** Medium — eliminates one network roundtrip per transaction
**Complexity:** Medium
**Risk:** Medium

## Problem

In Npgsql, `BeginTransaction()` doesn't send anything to the server — it sets an in-memory flag. The
actual `BEGIN TRANSACTION` is prepended to the first command executed within the transaction scope.
This eliminates a full network roundtrip per transaction.

SqlClient always sends `BEGIN TRANSACTION` immediately, even if the user calls `BeginTransaction()`
and then immediately executes a command.

## Location

**Files:**

- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlTransaction.cs`
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlInternalTransaction.cs`
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParser.cs`
  - `TdsExecuteTransactionManagerRequest()` (line ~9819)

## Changes Required

### 1. Defer Transaction Start

In `SqlInternalTransaction`, add a `_deferred` flag:

```csharp
internal class SqlInternalTransaction
{
    private bool _deferred = false;
    private IsolationLevel _deferredIsolationLevel;

    internal void BeginDeferred(IsolationLevel iso)
    {
        _deferred = true;
        _deferredIsolationLevel = iso;
        _transactionState = TransactionState.Pending;
    }

    internal bool IsDeferredAndNotStarted =>
        _deferred && _transactionState == TransactionState.Pending;
}
```

### 2. Prepend BEGIN on First Command

In `TdsParser` command execution (before sending an RPC or SQL batch), check if there's a deferred
transaction:

```csharp
// In TdsExecuteRPC or equivalent:
if (connection._currentTransaction?.IsDeferredAndNotStarted == true)
{
    // Send BEGIN TRANSACTION + command in same batch
    TdsExecuteTransactionManagerRequest(..., Begin, ...);
    connection._currentTransaction._deferred = false;
}
```

### 3. Handle Edge Cases

- **Commit/Rollback on deferred transaction** — If no commands were executed, Commit becomes a no-op
  (nothing to commit), Rollback becomes a no-op.

- **Error during BEGIN** — If the prepended BEGIN fails, the command also fails. The user sees the
  error on their first command, not on `BeginTransaction()`. This is a behavioral change that should
  be documented.

- **Transaction isolation level** — Must be sent with the deferred BEGIN.

### 4. Make Opt-In Initially

Add an AppContext switch or connection string keyword to enable deferred transactions, since this is
a behavioral change:

```csharp
AppContext.SetSwitch(
    "Switch.Microsoft.Data.SqlClient.DeferTransactionStart", true);
```

## Testing

- Benchmark: Transaction start + single command — measure roundtrip savings
- Integration test: Deferred BEGIN + single command produces correct results
- Integration test: Deferred Commit without commands is no-op
- Integration test: Deferred Rollback without commands is no-op
- Integration test: Error during deferred BEGIN is surfaced on first command

## Risk

- Medium — behavioral change. Users who catch exceptions on `BeginTransaction()` will now see them
  on the first command instead.

- The Npgsql precedent shows this is viable in production.

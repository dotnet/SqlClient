# Fix 6: Implement Transaction Enlistment

**Priority:** High — required for any application using `System.Transactions`
**Risk:** Medium — transaction correctness is critical

## Problem

`PutObjectFromTransactedPool()` and `TransactionEnded()` throw `NotImplementedException`. Without
these, connections enlisted in `System.Transactions` (distributed transactions, `TransactionScope`)
cannot be managed by the V2 pool.

## Location

## **Files**

## `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/ChannelDbConnectionPool.cs`

  `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/TransactedConnectionPool.cs`

## Background

The `TransactedConnectionPool` (refactored in PR #3746) maintains a `Dictionary<Transaction,
TransactedConnectionList>` mapping active transactions to their enlisted connections. When a
connection is enlisted in a transaction:

1. It's removed from the idle channel
2. Stored in the `TransactedConnectionPool` keyed by the `Transaction` object
3. When another `OpenAsync()` is called within the same transaction scope, the pool returns the
   already-enlisted connection

4. When the transaction completes, the connection is returned to the idle channel

## Changes Required

### PutObjectFromTransactedPool()

Called when a transaction completes and the connection should return to the main pool:

```csharp
public override void PutObjectFromTransactedPool(DbConnectionInternal connection)
{
    if (IsLiveConnection(connection))
    {
        _idleChannel.Writer.TryWrite(connection);
    }
    else
    {
        DestroyConnection(connection);
    }
}
```

### TransactionEnded()

Called when the transaction has fully completed (committed or rolled back):

```csharp
public override void TransactionEnded(Transaction transaction,
    DbConnectionInternal connection)
{
    _transactedConnectionPool.TransactionEnded(transaction, connection);
}
```

### GetInternalConnection() — Transaction-Aware Path

The existing `GetInternalConnection()` must check for an active transaction first:

```csharp
// Before checking the idle channel:
if (Transaction.Current != null)
{
    var enlisted = _transactedConnectionPool.TryGetConnection(Transaction.Current);
    if (enlisted != null)
        return enlisted;
}
```

## Testing

- PR #3805 already has transaction pool tests — integrate with V2 pool
- Unit test: Enlist connection → same-transaction open returns same connection
- Unit test: Transaction commit → connection returns to idle pool
- Unit test: Transaction rollback → connection returns to idle pool
- Integration test: `TransactionScope` with V2 pool

## Risk

- Medium — transaction correctness is critical. Bugs here cause data corruption.
- The `TransactedConnectionPool` is already refactored (PR #3746), so the integration surface is
  well-defined.

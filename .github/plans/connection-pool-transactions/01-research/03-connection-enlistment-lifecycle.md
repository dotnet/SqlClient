# Connection Enlistment Lifecycle

The full journey of a connection through transaction enlistment: how it gets enlisted, what state it holds, and how it detaches.

This document builds on the delegated/propagated distinction covered in [02-delegated-transactions-and-pspe.md](02-delegated-transactions-and-pspe.md).

## Two Enlistment Points

A connection can be enlisted in a transaction at two moments:

1. **Fresh connection** — `SqlConnection.Open()` → `CompleteLogin()` → checks `ConnectionOptions.Enlist` and `RoutingInfo == null` → reads `ADP.GetCurrentTransaction()` (= `Transaction.Current`) → calls `Enlist(tx)`.

2. **Reused pooled connection** — Pool calls `ActivateConnection(transaction)` → `Activate(tx)` → checks `ConnectionOptions.Enlist` → calls `Enlist(tx)` if a transaction is present, or `Enlist(null)` to un-enlist a previously enlisted connection.

Both converge on the same `Enlist()` method, which delegates to `EnlistNonNull()` (the delegated vs propagated decision tree) or handles the null case (un-enlistment).

## The Enlist Connection String Keyword

- Default: `true`
- When `true`: auto-enlistment happens at both enlistment points above
- When `false`: auto-enlistment is skipped entirely — `CompleteLogin()` and `Activate()` both check this flag
- Manual enlistment via `SqlConnection.EnlistTransaction()` still works regardless of this setting
- This keyword also controls `HasTransactionAffinity` in the pool (covered in [05-connection-transaction-binding.md](05-connection-transaction-binding.md))

## The EnlistedTransaction Property

Defined in `DbConnectionInternal`. This is the primary state that tracks whether a connection is currently participating in a transaction.

**Two backing fields:**
- `_enlistedTransaction` — a **clone** of the transaction (why cloning matters is covered in [04-transaction-cloning.md](04-transaction-cloning.md))
- `_enlistedTransactionOriginal` — the original reference

**Setter mechanics:**
1. Creates a clone via `value.Clone()`
2. Under `lock(this)`, atomically swaps via `Interlocked.Exchange`
3. Stores original reference
4. In `finally`, disposes the old clone (if any) or the new clone if the store failed

**Set:** by `Enlist()` when enlisting.
**Cleared:** by `DetachTransaction()` when detaching.

## Detachment: Two Methods

**`DetachCurrentTransactionIfEnded()`** — A passive optimization. Called during `CloseConnection()`. Reads `TransactionInformation.Status`; if the transaction is no longer `Active`, calls `DetachTransaction()` immediately. This way, the connection goes straight to the general pool instead of being unnecessarily parked in the transacted pool.

**`DetachTransaction()`** — Active detachment. Locks on the **transaction object** (not the connection), unhooks the `TransactionCompleted` event handler, sets `EnlistedTransaction = null`. If `IsTxRootWaitingForTxEnd` is true (connection is in stasis), triggers `DelegatedTransactionEnded()`. The lock on the transaction object prevents races between the user thread (closing connection) and the System.Transactions thread (firing `TransactionCompleted`).

## Manual Enlistment

`SqlConnection.EnlistTransaction(Transaction)` validates guards, then delegates to the same `Enlist()` method. Key validations:
- **No-op** if already enlisted in the same transaction
- **Throws** if enlisted in a *different* active transaction (`TransactionPresent` error)
- **Throws** if there's an active `SqlTransaction` (`BEGIN TRAN`) on the connection (`LocalTransactionPresent` error)

## ADP.GetCurrentTransaction / SetCurrentTransaction

Simple wrappers around `Transaction.Current`:

```csharp
internal static Transaction GetCurrentTransaction() => Transaction.Current;
internal static void SetCurrentTransaction(Transaction transaction) => Transaction.Current = transaction;
```

**Why they exist:** The pool may need to wait asynchronously for a connection (e.g., pool is full). Since `Transaction.Current` is thread-local by default, it doesn't flow across `await`. The pool captures the transaction before any `await` via `GetCurrentTransaction()`, then restores it via `SetCurrentTransaction()` on the completing thread so that `ActivateConnection` sees the correct transaction for enlistment.

## Key Source Locations

| File | Relevant Members |
|------|-----------------|
| `SqlConnectionInternal.cs` | `CompleteLogin()` (~2335), `Activate()` (~2030), `Enlist()` (~2343), `EnlistNonNull()` |
| `DbConnectionInternal.cs` | `EnlistedTransaction` (~170), `ActivateConnection()` (~337), `DetachTransaction()` (~595), `DetachCurrentTransactionIfEnded()` (~569) |
| `SqlConnection.cs` | `EnlistTransaction()` (~1557) |
| `AdapterUtil.cs` | `GetCurrentTransaction()` (~740), `SetCurrentTransaction()` (~1140) |

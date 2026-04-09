# How SqlClient Enlists in Transactions

The code path from `SqlConnection.Open()` through auto-enlistment, and the full lifecycle of the `EnlistedTransaction` property.

## Topics to Research

- [x] Auto-enlistment: `SqlConnection.Open()` → `ActivateConnection()` → `EnlistTransaction()` code path
- [x] `DbConnectionInternal.EnlistTransaction()` — what it does to the connection
- [x] `EnlistedTransaction` property — when it's set, when it's nulled, thread safety
- [x] `DetachTransaction()` vs `DetachCurrentTransactionIfEnded()` — who calls each, when
- [x] Manual enlistment via `SqlConnection.EnlistTransaction(Transaction)` — how it differs from auto
- [x] The `Enlist` connection string keyword — what it controls
- [x] What `ADP.SetCurrentTransaction()` / `ADP.GetCurrentTransaction()` do and why they exist
- [x] `PrepareConnection()` in both pool implementations — how `Transaction` flows through
- [x] Transaction cloning — `Transaction.Clone()` usage, benefits, safe disposal patterns

## Findings

### Two Enlistment Points

A connection can be enlisted in a transaction at two moments:

1. **Fresh connection** — `SqlConnection.Open()` → `CompleteLogin()` → checks `ConnectionOptions.Enlist` and `RoutingInfo == null` → reads `ADP.GetCurrentTransaction()` (= `Transaction.Current`) → calls `Enlist(tx)`. Located at `SqlConnectionInternal.cs` line ~2335.

2. **Reused pooled connection** — Pool calls `ActivateConnection(transaction)` → virtual `Activate(tx)` → `SqlConnectionInternal.Activate()` checks `ConnectionOptions.Enlist` → calls `Enlist(tx)` if transaction is non-null, or `Enlist(null)` to un-enlist. Located at `SqlConnectionInternal.cs` line ~2030.

Both converge on the same `Enlist()` method at `SqlConnectionInternal.cs` line ~2343.

### Why the Pool Captures the Transaction Early

The pool may need to wait asynchronously for a connection to become free or to open. The ambient transaction (`Transaction.Current`) may not flow across that async boundary (it's thread-local by default). So the pool captures the transaction before any `await`, then restores it via `ADP.SetCurrentTransaction()` on the thread that completes the async operation, so that `ActivateConnection` sees the right transaction.

### The `EnlistedTransaction` Property

Defined in `DbConnectionInternal.cs` line ~170.

**Two backing fields:**
- `_enlistedTransaction` — a **clone** of the transaction (survives original disposal)
- `_enlistedTransactionOriginal` — the original reference (used to detect when the original has been disposed)

**Setter mechanics:**
1. Creates a clone via `value.Clone()`
2. Under `lock (this)`, atomically swaps via `Interlocked.Exchange(ref _enlistedTransaction, valueClone)`
3. Stores original reference in `_enlistedTransactionOriginal`
4. In `finally`, disposes the old clone (if any) and disposes the new clone if the store failed

**Setting:** `Enlist()` sets it when enlisting.
**Clearing:** `DetachTransaction()` sets it to null and unhooks the `TransactionCompleted` event handler.

### Transaction Cloning

`Transaction.Clone()` creates a new `Transaction` object that represents the same underlying transaction but has an independent lifetime. SqlClient clones transactions in two places for different reasons:

**1. `EnlistedTransaction` setter** — The connection needs to query transaction state (e.g., `TransactionInformation.Status`) even after the user's `TransactionScope` has been disposed. If it held the original, those queries would throw `ObjectDisposedException`. The clone keeps the metadata accessible.

**2. `TransactedConnectionPool.PutTransactedObject()`** — The transacted pool uses a `Dictionary<Transaction, TransactedConnectionList>`. The clone is used as the dictionary key. This avoids holding the original alive and ensures the key remains valid for lookup even after the user-facing transaction object is disposed.

**Safe disposal pattern** — Both places use the same approach:
- Clone inside a `try` block
- Store the clone (transfer ownership)
- Set the local reference to `null` so the `finally` block doesn't dispose it
- If storage fails (race condition, exception), the `finally` block disposes the clone
- When the clone is no longer needed, the owner disposes it explicitly: the `EnlistedTransaction` setter disposes the old clone, and `TransactedConnectionList.Dispose()` disposes the key clone

**No `DependentTransaction` usage** — SqlClient uses `Transaction.Clone()` exclusively, not `Transaction.DependentClone()`.

### `DetachTransaction()` vs `DetachCurrentTransactionIfEnded()`

**`DetachCurrentTransactionIfEnded()`** (line ~569) — Passive check. Reads `TransactionInformation.Status`; if not `Active`, calls `DetachTransaction`. Called during `CloseConnection()` as an optimization: if the transaction already completed, detach immediately so the connection goes to the general pool instead of the transacted pool.

**`DetachTransaction()`** (line ~595) — Active detachment. Locks on the **transaction object** (not the connection), unhooks `TransactionCompleted`, sets `EnlistedTransaction = null`. If `IsTxRootWaitingForTxEnd`, triggers `DelegatedTransactionEnded()`. The lock on the transaction object prevents races between the user thread (closing connection) and the System.Transactions thread (firing `TransactionCompleted`).

### Manual Enlistment

`SqlConnection.EnlistTransaction(Transaction)` → validates no conflicting active transaction → delegates to `SqlConnectionInternal.EnlistTransaction()` → calls the same `Enlist()` method. Key guards:
- No-op if already enlisted in the same transaction
- Throws `TransactionPresent` if enlisted in a *different* active transaction
- Throws `LocalTransactionPresent` if there's an active `SqlTransaction` (BEGIN TRAN)

### `ADP.GetCurrentTransaction()` / `SetCurrentTransaction()`

Simple wrappers around `Transaction.Current` (in `AdapterUtil.cs`):
```csharp
internal static Transaction GetCurrentTransaction() => Transaction.Current;
internal static void SetCurrentTransaction(Transaction transaction) => Transaction.Current = transaction;
```
No custom `AsyncLocal`. They exist so the pool can save/restore the ambient transaction across async boundaries.

### The `Enlist` Connection String Keyword

- Default: `true`
- When `false`: auto-enlistment is skipped in both `CompleteLogin()` and `Activate()`
- Manual enlistment via `SqlConnection.EnlistTransaction()` still works regardless
- Checked at `SqlConnectionInternal.cs` lines ~2335 and ~2041

### Key Source Locations

| File | Members |
|------|---------|
| `SqlConnectionInternal.cs` | `CompleteLogin()` (~2335), `Activate()` (~2030), `Enlist()` (~2343), `EnlistNonNull()` |
| `DbConnectionInternal.cs` | `EnlistedTransaction` (~170), `ActivateConnection()` (~337), `DetachTransaction()` (~595), `DetachCurrentTransactionIfEnded()` (~569) |
| `SqlConnection.cs` | `EnlistTransaction()` (~1557) |
| `AdapterUtil.cs` | `GetCurrentTransaction()` (~740), `SetCurrentTransaction()` (~1140) |
| `TransactedConnectionPool.cs` | `PutTransactedObject()` — clone as dictionary key (~212) |

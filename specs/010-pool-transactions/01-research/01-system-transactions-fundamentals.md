# System.Transactions Fundamentals

How the `System.Transactions` framework works from the perspective of a consumer like SqlClient. No SqlClient-specific knowledge is needed — this is purely about the framework itself.

## What Problem Does System.Transactions Solve?

`System.Transactions` provides a single programming model for both local and distributed transactions. You write code the same way regardless of whether the transaction ends up being managed by one database or coordinated across multiple resources by MSDTC (Microsoft Distributed Transaction Coordinator).

## Two Key APIs

**`TransactionScope`** sets `Transaction.Current` (an ambient, thread-local value) so that resource managers like SqlClient can auto-detect and auto-enlist. This is the most common pattern:

```csharp
using (var scope = new TransactionScope())
{
    // Transaction.Current is non-null here
    // Any SqlConnection opened will auto-enlist
    conn.Open();
    // ...
    scope.Complete(); // marks for commit
} // Dispose() commits if Complete() was called, otherwise rolls back
```

**`CommittableTransaction`** gives explicit control. The caller creates a transaction and manually passes it to resources:

```csharp
var tx = new CommittableTransaction();
conn.EnlistTransaction(tx);
// ...
tx.Commit();
```

## Lightweight vs Distributed Transactions

System.Transactions starts every transaction as **lightweight** — a single durable resource manager, no MSDTC involvement, fast. This optimization is called Promotable Single Phase Enlistment (PSPE).

The transaction **promotes** to **distributed** when a second durable resource enlists (e.g., a second `SqlConnection` to a different server within the same scope). Distributed transactions require MSDTC and are significantly slower due to inter-process communication and durable logging.

The key insight: most transactions only touch one database, so PSPE avoidance of MSDTC is a major performance win.

## TransactionScope Options

`TransactionScope` accepts a `TransactionScopeOption` that controls how it interacts with any existing ambient transaction:

| Option | Behavior | Effect on `Transaction.Current` |
|--------|----------|-------------------------------|
| `Required` (default) | Joins the ambient transaction, or creates a new one if none exists | Same `Transaction` object in nested scopes |
| `RequiresNew` | Always creates a new, independent transaction; suspends the outer one | Different `Transaction.Current` inside the scope |
| `Suppress` | Runs without any ambient transaction | `Transaction.Current` is `null` inside the scope |

## The TransactionCompleted Event

`Transaction.TransactionCompleted` fires once, after the transaction outcome is determined (committed or rolled back).

**Critical threading detail:** The thread that fires this event is **non-deterministic**:
- For local (lightweight) transactions, it may fire on the user thread during `TransactionScope.Dispose()`
- For distributed transactions, it typically fires on a thread pool thread from MSDTC

Any code handling this event must be thread-safe.

## Transaction Timeout

Default is ~60 seconds (configurable via `TransactionScope` constructor or machine config). If the scope isn't completed before the timeout, the transaction is aborted. `TransactionCompleted` fires with `TransactionStatus.Aborted`.

From a resource manager's perspective, a timeout is just another rollback — no special handling path is needed.

## Two-Phase Commit (2PC)

Two-phase commit only applies to distributed transactions:

1. **Phase 1 (Prepare):** The coordinator asks each enlisted resource to prepare. Resources do their work and vote "prepared" or "abort."
2. **Phase 2 (Commit/Rollback):** If all voted "prepared," the coordinator says "commit." If any voted "abort," the coordinator says "rollback."

SqlClient implements this via `IEnlistmentNotification`. The connection pool doesn't interact with 2PC directly — it only observes the `TransactionCompleted` event after the protocol completes.

## Async and Transaction.Current

By default, `Transaction.Current` is stored in **thread-local storage** and does **not** flow across `await` boundaries. After an `await`, a different thread may resume execution and `Transaction.Current` will be `null`.

`TransactionScopeAsyncFlowOption.Enabled` changes the storage from thread-local to `AsyncLocal<T>`, allowing the transaction to survive `await`:

```csharp
using (var scope = new TransactionScope(
    TransactionScopeAsyncFlowOption.Enabled))
{
    await conn.OpenAsync(); // Transaction.Current flows across this await
    scope.Complete();
}
```

This is directly relevant to connection pooling: the pool reads `Transaction.Current` when deciding whether to check the transacted pool. If async flow isn't enabled, post-`await` code won't see the transaction.

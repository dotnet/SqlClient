# Threading and Synchronization

How transactions, connections, and pool operations interact across threads.

This document builds on the return/cleanup paths from [06-connection-return-and-cleanup-paths.md](06-connection-return-and-cleanup-paths.md) and the PSPE callback threading from [02-delegated-transactions-and-pspe.md](02-delegated-transactions-and-pspe.md).

## The Two Competing Threads

1. **User thread** — calls `connection.Close()` → `ReturnInternalConnection` → pool decides where connection goes
2. **System.Transactions thread pool thread** — fires `TransactionCompleted` → `CleanupConnectionOnTransactionCompletion` → `DetachTransaction` → possibly `DelegatedTransactionEnded`

These run concurrently. The critical race: when the user closes a connection, is the transaction still active? The answer determines routing (transacted pool vs idle pool vs stasis vs destroy).

## How the Race Is Prevented

The pool takes `lock(connection)` before checking `EnlistedTransaction`:

```
ReturnInternalConnection / DeactivateObject:
  lock (connection) {
      transaction = connection.EnlistedTransaction;  // read under lock
      if (transaction != null) → transacted pool
      else → idle pool
  }
```

Meanwhile, `DetachTransaction` (from the `TransactionCompleted` callback) takes `lock(transaction)` — **not** `lock(connection)` — and clears `EnlistedTransaction`.

The two locks are on **different objects**. They don't directly exclude each other. The design relies on ordering:
- If `TransactionCompleted` hasn't fired: `EnlistedTransaction` is non-null → transacted pool → cleaned up later when `TransactionCompleted` fires
- If `TransactionCompleted` already fired: `EnlistedTransaction` is null → idle pool (correct, transaction is done)
- The lock on the connection prevents the `EnlistedTransaction` field from changing during the check

## Which Thread Fires TransactionCompleted?

A **System.Transactions thread pool thread** — not the user thread. Not deterministic. Can fire at any point after the transaction scope disposes, commits, or rolls back.

## DelegatedTransactionEnded Call Chain

Called from `DetachTransaction` when `IsTxRootWaitingForTxEnd == true` (connection in stasis):

```
TransactionCompleted (SysTx thread)
  → TransactionCompletedEvent
    → CleanupConnectionOnTransactionCompletion
      → DetachTransaction
        → lock(transaction)
          → DelegatedTransactionEnded (if in stasis)
            → TerminateStasis
            → pool.PutObjectFromTransactedPool
```

All on the System.Transactions thread, holding `lock(transaction)`.

## Lock Hierarchy

| Component | Lock Target | Purpose |
|-----------|------------|---------|
| Pool return path (both impls) | `lock(connection)` | Check `EnlistedTransaction` atomically |
| `DetachTransaction` | `lock(transaction)` | Clear enlistment, unsubscribe event |
| `EnlistedTransaction` setter | `lock(this)` (= connection) | Atomic clone swap |
| `SqlDelegatedTransaction` | `lock(connection)` | Promote/Commit/Rollback server commands |
| `TransactedConnectionPool` | `lock(TransactedConnections)` outer, `lock(connections)` inner | Dictionary + per-txn list safety |

No single enforced hierarchy. Deadlocks are avoided by:
- Never nesting connection lock inside transaction lock (or vice versa)
- Pool operations don't hold locks when calling into System.Transactions
- `SqlDelegatedTransaction` does pool cleanup *outside* the connection lock

## SetInStasis / TerminateStasis

Just flip the `IsTxRootWaitingForTxEnd` flag — no locks themselves. Rely on caller context:
- `SetInStasis` called inside `lock(connection)` from `DeactivateObject`
- `TerminateStasis` called inside `lock(transaction)` from `DelegatedTransactionEnded`

## Channel Safety

`ChannelDbConnectionPool.PutObjectFromTransactedPool` calls `_idleConnectionWriter.TryWrite(connection)` from the System.Transactions thread. This is safe because `Channel<T>.Writer.TryWrite` is thread-safe by design (built for multi-producer scenarios).

## ADP.GetCurrentTransaction / SetCurrentTransaction

Thin wrappers around `Transaction.Current`. `SetCurrentTransaction` exists because when the pool awaits asynchronously, `Transaction.Current` might not flow across the `await` (it's thread-local by default). The pool captures it before `await`, restores it after, ensuring `ActivateConnection` sees the right transaction for enlistment. See [03-connection-enlistment-lifecycle.md](03-connection-enlistment-lifecycle.md) for details.

## Key Source Locations

| File | Relevant Members |
|------|-----------------|
| `DbConnectionInternal.cs` | `TransactionCompletedEvent` (~945), `TransactionOutcomeEnlist` (~964), `DetachTransaction` (~605), `DelegatedTransactionEnded` (~519), `EnlistedTransaction` setter (~170) |
| `WaitHandleDbConnectionPool.cs` | `DeactivateObject` lock on connection (~623), `ReturnInternalConnection` lock (~1357) |
| `ChannelDbConnectionPool.cs` | `ReturnInternalConnection` lock on connection (~225) |
| `SqlDelegatedTransaction.cs` | All methods lock on connection; cleanup outside lock |
| `TransactedConnectionPool.cs` | Two-level locking: `TransactedConnections` (outer) → `connections` list (inner) |
| `AdapterUtil.cs` | `GetCurrentTransaction()`, `SetCurrentTransaction()` |

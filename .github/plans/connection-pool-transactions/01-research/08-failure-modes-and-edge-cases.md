# Failure Modes and Edge Cases

What can go wrong at the intersection of transactions and connection pooling. This is the capstone topic — it assumes familiarity with all prior research documents.

## Inventory

1. [Transaction timeout](#1-transaction-timeout)
2. [Connection death while parked](#2-connection-death-while-parked-in-transacted-pool)
3. [Pool shutdown with active transactions](#3-pool-shutdown-with-active-transactions)
4. [Pool clear](#4-pool-clear)
5. [Connection leak / GC reclamation](#5-connection-leak--gc-reclamation)
6. [ReplaceConnection on an enlisted connection](#6-replaceconnection-on-an-enlisted-connection)
7. [Two transactions on the same connection](#7-two-transactions-on-the-same-connection)
8. [MSDTC unavailable during promotion](#8-msdtc-unavailable-during-promotion)
9. [CanBePooled becomes false while in transacted pool](#9-canbepooled-becomes-false-while-in-transacted-pool)

## Known Issues in Existing Pool

### CleanupCallback Race Condition

There is a known minor race condition in `WaitHandleDbConnectionPool.CleanupCallback`:

```csharp
lock (obj)
{
    if (obj.IsTransactionRoot)
    {
        shouldDestroy = false;
    }
}
// ← Race window: TransactionCompleted could fire between lock release and SetInStasis
if (shouldDestroy)
    DestroyObject(obj);
else
    obj.SetInStasis();
```

If `TransactionCompleted` fires between the lock release and `SetInStasis()`, the stasis counter may be incremented without a corresponding decrement. The source code comment acknowledges this and says it would require "more substantial re-architecture of the pool" to fix.

### DestroyObject Guard

`DestroyObject` has a safety guard: if `obj.IsTxRootWaitingForTxEnd` is true, it logs a trace and does nothing. This prevents premature disposal of a connection in stasis. The `TransactionCompleted` event will eventually call `PutObjectFromTransactedPool`, which calls `DestroyObject` again after `TerminateStasis` clears the flag.

`ChannelDbConnectionPool.RemoveConnection` does not currently have this guard.

## Findings

### 1. Transaction timeout

No special handling needed. A transaction timeout is just normal completion — `TransactionCompleted` fires with `TransactionStatus.Aborted`. The standard cleanup path runs: `TransactionCompletedEvent` → `DelegatedTransactionEnded` / `DetachTransaction` → `PutObjectFromTransactedPool` → connection returns to idle pool or is destroyed.

### 2. Connection death while parked in transacted pool

Lazy detection only. Parked transacted connections are **not** proactively health-checked. When a connection is retrieved from the transacted pool via `GetTransactedObject`, the caller health-checks it. If dead, it's destroyed and a new connection is created. The transacted pool itself never discovers a dead connection on its own.

See the health check asymmetry in [05-connection-transaction-binding.md](05-connection-transaction-binding.md) — dead roots throw, dead non-roots are silently discarded.

### 3. Pool shutdown with active transactions

- **WaitHandle pool:** `DeactivateObject` checks for pool shutdown. If the pool is shutting down and the connection `IsTransactionRoot`, it enters stasis (`SetInStasis`) to keep the connection alive until the transaction completes. Non-root connections are destroyed.
- **Channel pool:** `Shutdown` and `Clear` currently throw `NotImplementedException`.

See stasis mechanics in [06-connection-return-and-cleanup-paths.md](06-connection-return-and-cleanup-paths.md).

### 4. Pool clear

`ClearPool` marks connections as non-poolable (`CanBePooled = false`) but does **not** directly touch the transacted pool. When connections are later retrieved from the transacted pool, the `CanBePooled` check causes them to be destroyed instead of reused. This is an indirect, lazy cleanup mechanism.

### 5. Connection leak / GC reclamation

GC-triggered reclamation calls `DetachCurrentTransactionIfEnded` + `DeactivateObject`. If the transaction is still active, the connection stays enlisted (it's still needed). If the transaction has already ended, it gets cleaned up through the normal deactivation path. The key method is `DetachCurrentTransactionIfEnded` — it only detaches if `Transaction.TransactionInformation.Status != Active`.

### 6. ReplaceConnection on an enlisted connection

`ReplaceConnection` creates a new physical connection and transfers the old connection's transaction to it. There is **no** `IsTransactionRoot` guard.

Two sub-cases:

- **Propagated (DTC) non-root connections:** Correct behavior. The distributed transaction survives the connection break because it's managed by MSDTC. The new connection can re-join via the propagation cookie (TDS `PROPAGATE_DTCTOKEN`). This is the useful case.
- **Delegated root connections:** The transaction is already doomed. The server-side local transaction is tied to the session that created it. When the physical connection breaks, that session dies and the server rolls back the local transaction. No mechanism exists to reconnect and resume it — confirmed via public `IPromotableSinglePhaseNotification` documentation (the PSPE contract requires the RM to handle `SinglePhaseCommit`/`Rollback`/`Promote` callbacks on the live connection).

The lack of guard is harmless-by-circumstance for roots: the trigger is a broken connection, and a broken root means the transaction was already lost. But it warrants a test to verify the transaction correctly aborts (see design decision #4 in the design section).

### 7. Two transactions on the same connection

`EnlistTransaction` validates: if you call it with a different `Transaction` than the one the connection is already enlisted in, it throws `InvalidOperationException`. You must complete or rollback the current transaction before enlisting in a new one. This is enforced in `DbConnectionInternal.EnlistTransaction` — it checks whether `EnlistedTransaction` is non-null and different from the requested transaction.

### 8. MSDTC unavailable during promotion

If MSDTC is unavailable when `Promote` is called (e.g., second connection opens in the same `TransactionScope`), the `Promote` callback on `SqlDelegatedTransaction` throws. System.Transactions converts this to a `TransactionAbortedException`. The original root connection's local transaction is rolled back on the server side. Both connections end up with failed enlistments.

### 9. CanBePooled becomes false while in transacted pool

This is the `LoadBalanceTimeout` / `ConnectionLifetime` case. The connection is parked in the transacted pool, and while waiting, its lifetime expires. The connection isn't proactively removed — similar to dead connection detection, it's lazy. On the next retrieval from the transacted pool, `CanBePooled` returns false, and the connection is destroyed instead of returned to the caller. A new connection is created to replace it.

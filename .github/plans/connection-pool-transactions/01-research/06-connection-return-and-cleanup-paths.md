# Connection Return and Cleanup Paths

Every code path that moves a connection from "in use" to "somewhere else," and how the stasis mechanism keeps delegated transaction roots alive.

This document builds on binding rules from [05-connection-transaction-binding.md](05-connection-transaction-binding.md) and the root connection concept from [02-delegated-transactions-and-pspe.md](02-delegated-transactions-and-pspe.md).

## Paths Inventory

| Trigger | Entry Point | Transaction State | Destination |
|---------|-------------|-------------------|-------------|
| User calls `SqlConnection.Close()` | `DbConnectionInternal.CloseConnection()` | May be enlisted or not | Transacted pool or idle pool |
| Transaction commits/rolls back | `TransactionCompletedEvent` | Enlisted → detaching | `PutObjectFromTransactedPool` → idle or destroy |
| Pool pruning timer | `CleanupCallback` | May be transaction root | Destroy or stasis (WaitHandle only) |
| Pool shutdown | `Shutdown()` → affects `DeactivateObject` | May be transaction root | Destroy or stasis (WaitHandle only) |
| Pool clear (`SqlConnection.ClearPool`) | `Clear()` | May be enlisted | Doom + destroy |
| Connection reclamation (GC/leak) | `ReclaimEmancipatedObjects` | May be transaction root | Destroy or stasis |
| Load balance timeout | Sets `DoNotPool` | May be enlisted | Next return → destroy |
| `ReplaceConnection` | Creates new, removes old | Old may be enlisted | Old destroyed, new activated |

## Path A: User Closes Connection

1. `DbConnectionInternal.CloseConnection()` is called.
2. `DetachCurrentTransactionIfEnded()` checks if the enlisted transaction is already dead — if so, clears `EnlistedTransaction` right there (see [03-connection-enlistment-lifecycle.md](03-connection-enlistment-lifecycle.md)).
3. `Pool.ReturnInternalConnection()` is called, which calls `DeactivateConnection()` and then checks `EnlistedTransaction`:
   - **If still enlisted** (transaction is active): the connection goes to `TransactedConnectionPool.PutTransactedObject()` where it waits until Path B fires.
   - **If not enlisted** (transaction already ended or none): returns directly to the general pool.

## Path B: Transaction Completes Asynchronously

The `TransactionCompleted` event fires on `DbConnectionInternal`, which calls `CleanupConnectionOnTransactionCompletion()`. Two sub-paths:

### B1 — Normal path (connection is in the transacted pool)

1. `DetachTransaction()` clears `EnlistedTransaction`.
2. `Pool.TransactionEnded()` → `TransactedConnectionPool.TransactionEnded()` removes the connection from the `TransactedConnections` dictionary.
3. `Pool.PutObjectFromTransactedPool()` calls `ResetConnection()` and returns it to the general pool.

### B2 — Stasis path (delegated transaction root)

Applies when a connection is a delegated transaction root that was put in stasis because it had nowhere to go.

1. `DetachTransaction()` sees `IsTxRootWaitingForTxEnd == true`.
2. `DelegatedTransactionEnded()` fires, calls `TerminateStasis()`, then:
   - If `_pooledCount == 1` (closed, pooled): calls `Pool.PutObjectFromTransactedPool()` → general pool.
   - If `_pooledCount == -1` (closed, non-pooled): calls `Dispose()`.

## Path C: Pool Pruning

1. Timer pops old connections from the idle stack.
2. For each: checks `IsTransactionRoot` under lock.
   - If true: `SetInStasis()` — can't destroy, transaction is still running.
   - If false: `DestroyObject()`.

## The Stasis Mechanism

Stasis is a state where a delegated transaction root connection is "parked" with no owner — not in the idle pool, not held by user code, not in the transacted pool. It exists only because the delegated transaction hasn't finished yet and the connection must stay alive to support the PSPE callbacks (`SinglePhaseCommit`/`Rollback`/`Promote`).

### When It Happens

During `DeactivateObject` (user closed the connection) when the connection is a transaction root but can't be placed anywhere:
- **Pool is shutting down** — can't put it in the idle stack
- **`CanBePooled` is false** (e.g., `LoadBalanceTimeout` expired) — can't put it in any pool
- **`Pool == null`** — edge case, can't put it anywhere
- **`EnlistedTransaction` is already null** — can't put it in transacted pool (no key)

### Why Not Just Leave It in the Idle Stack?

Any `TryGetConnection` caller could pop it and get a connection still bound to an unfinished delegated transaction. You'd have to teach every idle-pool consumer to check `IsTransactionRoot` and skip, spreading transaction-awareness across the entire pool.

### Why Not Block and Wait?

Pool shutdown is non-blocking — `Shutdown()` sets state and returns immediately. There's nowhere to block; the pool group pruner runs periodically checking `Count == 0`. Stasis **is** the wait mechanism — it's event-driven via `TransactionCompleted`.

### How Stasis Resolves

1. `DelegatedTransactionEnded()` calls `TerminateStasis()` (clears `IsTxRootWaitingForTxEnd`)
2. Then calls `Pool.PutObjectFromTransactedPool()`
3. `PutObjectFromTransactedPool` sees the connection is doomed/pool shutting down → `DestroyObject()`
4. `DestroyObject()` removes from `_objectList`, decrements count
5. Pool group pruner eventually sees `Count == 0` and finishes teardown

### Stasis Call Sites

| # | Location | Condition |
|---|----------|----------|
| 1 | `WaitHandleDbConnectionPool.CleanupCallback` | Timer age-out of a transaction root |
| 2 | `WaitHandleDbConnectionPool.DeactivateObject` | Pool shutting down + `IsTransactionRoot` |
| 3 | `WaitHandleDbConnectionPool.DeactivateObject` | `IsTransactionRoot` + `Pool == null` |
| 4 | `WaitHandleDbConnectionPool.DeactivateObject` | `!CanBePooled` + `IsTransactionRoot` + `!IsConnectionDoomed` |
| 5 | `DbConnectionInternal.CloseConnection` | Non-pooled connection + `IsTransactionRoot` |

## Convergence: PutObjectFromTransactedPool

All post-transaction return paths funnel through `PutObjectFromTransactedPool()`:
- **Pool running + `CanBePooled`**: `ResetConnection()` → general pool.
- **Otherwise**: destroy/dispose.

## Race: Close and TransactionCompleted Fire Simultaneously

`CloseConnection()` calls `DetachCurrentTransactionIfEnded()` before returning to the pool. If the transaction has already completed, the connection goes straight to the general pool via Path A (with `EnlistedTransaction == null`). If the transaction is still active, the connection parks in the transacted pool and Path B handles the return. The lock inside `DetachTransaction()` on the transaction object prevents both paths from racing.

## Diagram

```mermaid
flowchart TD
    subgraph TRIGGER["Trigger Events"]
        A["User calls<br/>SqlConnection.Close()"]
        B["Transaction commits<br/>or rolls back<br/>(async notification)"]
    end

    subgraph PATH_A["Path A: Connection Close (user-initiated)"]
        A --> A1["DbConnectionInternal<br/>.CloseConnection()"]
        A1 --> A2["DetachCurrentTransactionIfEnded()"]
        A2 --> A3{"Transaction<br/>already ended?"}
        A3 -- Yes --> A3a["DetachTransaction()<br/>sets EnlistedTransaction = null"]
        A3 -- No --> A3b["EnlistedTransaction<br/>remains set"]
        A3a --> A4["Pool.ReturnInternalConnection()"]
        A3b --> A4
        A4 --> A5["DeactivateConnection()"]
        A5 --> A6{"EnlistedTransaction<br/>!= null?"}
        A6 -- Yes --> TXPOOL["TransactedConnectionPool<br/>.PutTransactedObject()"]
        A6 -- No --> GENERAL["Return to<br/>General Pool"]
    end

    subgraph PATH_B["Path B: Transaction Completion (async event)"]
        B --> B1["DbConnectionInternal<br/>.TransactionCompletedEvent()"]
        B1 --> B2["CleanupConnectionOnTransactionCompletion()"]
        B2 --> B3["DetachTransaction()<br/>sets EnlistedTransaction = null"]
        B3 --> B4{"IsTxRootWaitingForTxEnd<br/>(in stasis)?"}
        B4 -- Yes --> STASIS_EXIT["DelegatedTransactionEnded()<br/>TerminateStasis()"]
        STASIS_EXIT --> STASIS_POOL{"_pooledCount?"}
        STASIS_POOL -- "1 (closed, pooled)" --> PUT["Pool.PutObjectFromTransactedPool()"]
        STASIS_POOL -- "-1 (closed, non-pooled)" --> DISPOSE["Dispose()"]
        B4 -- No --> B5["Pool.TransactionEnded()"]
        B5 --> B6["TransactedConnectionPool<br/>.TransactionEnded()"]
        B6 --> B7["Remove connection from<br/>TransactedConnections dict"]
        B7 --> PUT
    end

    subgraph RETURN["PutObjectFromTransactedPool"]
        PUT --> R1{"Pool running &<br/>CanBePooled?"}
        R1 -- Yes --> R2["ResetConnection()"]
        R2 --> GENERAL
        R1 -- No --> DESTROY["RemoveConnection()<br/>/ Dispose()"]
    end

    TXPOOL -.->|"Connection waits here<br/>until transaction ends"| PATH_B

    style TRIGGER fill:#fff3cd,stroke:#856404
    style PATH_A fill:#d4edda,stroke:#155724
    style PATH_B fill:#cce5ff,stroke:#004085
    style RETURN fill:#f8d7da,stroke:#721c24
    style TXPOOL fill:#e2d5f1,stroke:#6f42c1
    style GENERAL fill:#d4edda,stroke:#155724,stroke-width:3px
    style DESTROY fill:#f5c6cb,stroke:#721c24
```

## Key Source Files

| File | Relevant Methods |
|------|-----------------|
| `DbConnectionInternal.cs` | `CloseConnection`, `DetachCurrentTransactionIfEnded`, `DetachTransaction`, `TransactionCompletedEvent`, `CleanupConnectionOnTransactionCompletion`, `DelegatedTransactionEnded`, `SetInStasis`, `TerminateStasis` |
| `TransactedConnectionPool.cs` | `GetTransactedObject`, `PutTransactedObject`, `TransactionEnded` |
| `WaitHandleDbConnectionPool.cs` | `DeactivateObject`, `DestroyObject`, `PutObjectFromTransactedPool`, `TransactionEnded`, `GetFromTransactedPool`, `CleanupCallback` |
| `ChannelDbConnectionPool.cs` | `ReturnInternalConnection`, `RemoveConnection`, `PutObjectFromTransactedPool`, `TransactionEnded`, `GetFromTransactedPool` |

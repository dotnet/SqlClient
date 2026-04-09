# Delegated Transactions and Promotion

Why delegated transactions exist, how promotion works, and why the "transaction root" concept drives most of the stasis complexity.

## Topics to Research

- [x] Why delegated transactions exist (perf optimization to avoid MSDTC for single-connection transactions)
- [x] `IPromotableSinglePhaseNotification` — the interface, who implements it
- [x] The promotion sequence — step by step, what triggers it
- [x] Why the connection that promoted owns the transaction and can't be reused by others
- [x] What happens to the transaction if the root connection is destroyed prematurely
- [x] Delegated vs propagated — terminology across layers

## Findings

### IsTransactionRoot vs IsTxRootWaitingForTxEnd

| Property | Meaning |
|----------|---------|
| `IsTransactionRoot` | This connection **owns** a delegated transaction. It was the connection that promoted a lightweight `TransactionScope` into a full distributed transaction. Set to `true` when `DelegatedTransaction` is assigned. |
| `IsTxRootWaitingForTxEnd` | This connection is a transaction root **and** has been placed into stasis — it has no user owner, it's not in any pool data structure, and it's just waiting for the `TransactionCompleted` event to fire so it can be cleaned up. Set by `SetInStasis()`. |

A connection can be `IsTransactionRoot == true` but `IsTxRootWaitingForTxEnd == false` — that's the normal state while a user is actively using it within a transaction.

### The Stasis Mechanism

**What it is:** A state where a delegated transaction root connection is "parked" with no owner — not in the idle pool, not held by user code, not in the transacted pool. It exists only because the delegated transaction hasn't finished yet and the connection must stay alive to support commit/rollback promotion.

**When it happens:** During `DeactivateObject` (user closed the connection) when the connection is a transaction root but can't be placed anywhere:
- Pool is shutting down — can't put it in idle stack
- `CanBePooled` is false (e.g., `LoadBalanceTimeout` expired) — can't put it in any pool
- `Pool == null` — edge case, can't put it anywhere
- `EnlistedTransaction` is already null — can't put it in transacted pool (no key)

**Why not just leave it in the idle stack?**
- Any `TryGetConnection` caller could pop it and get a broken connection still bound to an unfinished delegated transaction
- You'd have to teach every stack consumer to check `IsTransactionRoot` and skip, spreading transaction-awareness across the entire pool

**Why not "mark the pool as shutting down and wait"?**
- Pool shutdown is non-blocking — `Shutdown()` sets state and returns immediately
- There's nowhere to block; the pool group pruner runs periodically checking `Count == 0`
- Stasis **is** the wait mechanism — it's event-driven via `TransactionCompleted`

**How stasis resolves:**
1. `DelegatedTransactionEnded()` calls `TerminateStasis()` (clears `IsTxRootWaitingForTxEnd`)
2. Then calls `Pool.TransactionEnded()` → `PutObjectFromTransactedPool()`
3. `PutObjectFromTransactedPool` sees the connection is doomed/pool shutting down → `DestroyObject()`
4. `DestroyObject()` removes from `_objectList`, decrements count
5. Pool group pruner eventually sees `Count == 0` and finishes teardown

### Key Source Locations

| File | Relevant members |
|------|-----------------|
| `src/.../ProviderBase/DbConnectionInternal.cs` | `IsTransactionRoot` (virtual, line ~144), `IsTxRootWaitingForTxEnd` (line ~149), `SetInStasis()` (line ~776), `TerminateStasis()` (line ~937), `DelegatedTransactionEnded()` (line ~519) |

### Why Delegated Transactions Exist

Without PSPE, every `TransactionScope` with a database connection would require MSDTC (Distributed Transaction Coordinator) — expensive inter-process communication, durable logging, full 2PC. But most transactions only touch one database on one connection. PSPE lets System.Transactions *delegate* the transaction to that single resource manager, avoiding MSDTC entirely.

### IPromotableSinglePhaseNotification (PSPE Interface)

System.Transactions defines this interface with four callbacks:

| Method | When System.Transactions calls it |
|--------|----------------------------------|
| `Initialize()` | Right after delegation is accepted — "start managing this transaction" |
| `Promote()` | A second resource enlists — "give me a DTC token so I can escalate" |
| `SinglePhaseCommit(enlistment)` | Transaction completing, never promoted — "commit it yourself" |
| `Rollback(enlistment)` | Transaction aborting — "roll it back yourself" |

SqlClient implements this with `SqlDelegatedTransaction`.

### SqlDelegatedTransaction

Implements `IPromotableSinglePhaseNotification`. Created in `EnlistNonNull()`. Holds a reference to the root connection (`_connection`) and an `_atomicTransaction` reference to the System.Transactions `Transaction`.

**Initialize():** Defects from any prior enlistment, creates `SqlInternalTransaction(TransactionType.Delegated)`, sends `BEGIN TRANSACTION` to SQL Server with the mapped isolation level, sets `_active = true`.

**Promote():** Sends `TransactionRequest.Promote` to SQL Server under `lock(connection)`. Server returns a DTC propagation token (byte[]). Returns the token to System.Transactions, which uses it to create a full distributed transaction. On failure, dooms the connection.

**SinglePhaseCommit(enlistment):** Under `lock(connection)`, sets `_active = false`, clears `_connection`, sends `COMMIT` to SQL Server. Reports `Committed()`, `Aborted()`, or `InDoubt()` to System.Transactions based on outcome. Calls `CleanupConnectionOnTransactionCompletion()` outside the lock.

**Rollback(enlistment):** Under `lock(connection)`, sets `_active = false`, clears `_connection`, sends `ROLLBACK` if not already aborted. Always reports `Aborted()`. Dooms connection on SQL errors.

**Threading:** Explicitly documented as multithreaded. Pattern: `GetValidConnection()` (no lock) → `lock(connection)` → validate `_active` → do SQL work → clear state → unlock → pool cleanup outside lock.

### The Enlistment Sequence (EnlistNonNull)

```
EnlistNonNull(transaction)
├── Try EnlistPromotableSinglePhase(delegate)
│   ├── Returns true  → DELEGATED path
│   │   → System.Transactions calls delegate.Initialize() → BEGIN TRANSACTION
│   │   → DelegatedTransaction = delegate
│   │   → IsTransactionRoot = true
│   │   → System.Transactions will call SinglePhaseCommit/Rollback/Promote later
│   │
│   └── Returns false → PROPAGATED path (another resource already delegated)
│       → Get DTC cookie (GetTransactionCookie via GetDTCAddress)
│       → PropagateTransactionCookie() → TDS Propagate request to server
│       → IsEnlistedInTransaction = true
│
└── Both paths: EnlistedTransaction = transaction (base class, stores clone)
```

### Why the Root Connection Can't Be Destroyed

`SqlDelegatedTransaction` holds `_connection`. System.Transactions calls `SinglePhaseCommit` or `Rollback` *later* — potentially after the user calls `connection.Close()`. If the connection were destroyed (physically disconnected), those callbacks couldn't send COMMIT/ROLLBACK to the server. The transaction would be left in an indeterminate state on the server.

This is exactly why stasis exists: user closes root connection → can't pool it (others would grab it) → can't destroy it (callbacks need it) → park in stasis until `TransactionCompleted` fires.

### Terminology Across Layers

The two enlistment paths have inconsistent names across layers:

| Layer | Delegated (PSPE) Path | Non-Delegated (DTC) Path |
|-------|----------------------|--------------------------|
| System.Transactions | `EnlistPromotableSinglePhase` | (promotion triggers DTC) |
| SqlConnectionInternal logs | `"delegated to transaction"` | `"delegation not possible, enlisting"` |
| SqlConnectionInternal props | `DelegatedTransaction`, `IsTransactionRoot` | `IsEnlistedInTransaction` |
| SqlInternalTransaction | `TransactionType.Delegated` | `TransactionType.Distributed` |
| TDS protocol | `Begin/Promote/Commit/Rollback` | `Propagate` |
| DbConnectionInternal (base) | `EnlistedTransaction` (both paths) | `EnlistedTransaction` (both paths) |

"Delegated vs propagated" is clearer than "delegated vs enlisted" since `EnlistedTransaction` applies to both. Tracked in `03-design/terminology-renames.md`.

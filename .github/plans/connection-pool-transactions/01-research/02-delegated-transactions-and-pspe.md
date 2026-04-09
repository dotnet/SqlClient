# Delegated Transactions and PSPE

How SqlClient avoids MSDTC for single-connection transactions, and why this creates the "transaction root" concept that drives much of the pool's complexity.

This document builds on the System.Transactions fundamentals covered in [01-system-transactions-fundamentals.md](01-system-transactions-fundamentals.md).

## Why Delegated Transactions Exist

Without PSPE (Promotable Single Phase Enlistment), every `TransactionScope` with a database connection would require MSDTC — expensive inter-process communication, durable logging, full two-phase commit. But most transactions only touch one database on one connection. PSPE lets System.Transactions *delegate* the transaction to that single resource manager, avoiding MSDTC entirely.

## The PSPE Interface

System.Transactions defines `IPromotableSinglePhaseNotification` with four callbacks:

| Method | When System.Transactions Calls It |
|--------|----------------------------------|
| `Initialize()` | Right after delegation is accepted — "start managing this transaction" |
| `Promote()` | A second resource enlists — "give me a DTC token so I can escalate" |
| `SinglePhaseCommit(enlistment)` | Transaction completing, never promoted — "commit it yourself" |
| `Rollback(enlistment)` | Transaction aborting — "roll it back yourself" |

The key contract: once a resource manager accepts delegation via `EnlistPromotableSinglePhase`, System.Transactions will call one of these callbacks later to resolve the transaction. The resource manager **must keep its connection alive** to handle these callbacks.

## SqlDelegatedTransaction

SqlClient implements `IPromotableSinglePhaseNotification` with `SqlDelegatedTransaction`. It holds a reference to the root connection (`_connection`) and the System.Transactions `Transaction` object.

**Initialize():** Creates a `SqlInternalTransaction(TransactionType.Delegated)`, sends `BEGIN TRANSACTION` to SQL Server with the mapped isolation level.

**Promote():** Under `lock(connection)`, sends `TransactionRequest.Promote` to SQL Server. The server returns a DTC propagation token (byte[]). Returns the token to System.Transactions, which uses it to create a full distributed transaction.

**SinglePhaseCommit(enlistment):** Under `lock(connection)`, sends `COMMIT` to SQL Server. Reports the outcome (`Committed`, `Aborted`, or `InDoubt`) to System.Transactions.

**Rollback(enlistment):** Under `lock(connection)`, sends `ROLLBACK` if not already aborted. Always reports `Aborted`.

**Threading:** All callbacks are explicitly documented as potentially running on different threads. The pattern is: get valid connection reference (no lock) → `lock(connection)` → validate state → do SQL work → clear state → unlock → pool cleanup outside lock.

## The Enlistment Decision: Delegated vs Propagated

When a connection enlists in a transaction (via `EnlistNonNull`), there are two possible paths:

```
EnlistNonNull(transaction)
├── Try EnlistPromotableSinglePhase(delegate)
│   ├── Returns true  → DELEGATED path
│   │   → System.Transactions calls delegate.Initialize() → BEGIN TRANSACTION
│   │   → DelegatedTransaction = delegate
│   │   → IsTransactionRoot = true
│   │
│   └── Returns false → PROPAGATED path (another resource already delegated)
│       → Get DTC cookie via GetDTCAddress / GetTransactionCookie
│       → PropagateTransactionCookie() → TDS Propagate request to server
│       → IsEnlistedInTransaction = true
│
└── Both paths: EnlistedTransaction = transaction (stores clone)
```

`EnlistPromotableSinglePhase` succeeds only if no other resource manager has already claimed delegation for this transaction. The first connection gets the delegated path; subsequent connections (same transaction, different server) get the propagated path and trigger promotion.

## IsTransactionRoot and IsTxRootWaitingForTxEnd

These two properties track the state of a delegated root connection:

| Property | Meaning |
|----------|---------|
| `IsTransactionRoot` | This connection owns a delegated transaction — it accepted PSPE delegation. Set to `true` when `DelegatedTransaction` is assigned. |
| `IsTxRootWaitingForTxEnd` | This connection is a transaction root **and** has been placed into "stasis" — parked with no owner, waiting for the transaction to complete. Set by `SetInStasis()`. |

A connection can be `IsTransactionRoot == true` but `IsTxRootWaitingForTxEnd == false` — that's the normal state while a user is actively using it within a transaction.

## Why the Root Connection Can't Be Destroyed

`SqlDelegatedTransaction` holds `_connection`. System.Transactions will call `SinglePhaseCommit` or `Rollback` *later* — potentially after the user calls `connection.Close()`. If the connection were destroyed (physically disconnected), those callbacks couldn't send `COMMIT`/`ROLLBACK` to the server. The transaction would be left in an indeterminate state on the server side.

This is the fundamental reason for the "stasis" mechanism in the pool (covered in [06-connection-return-and-cleanup-paths.md](06-connection-return-and-cleanup-paths.md)): when a user closes a root connection and it can't be placed in a pool, it must be kept alive until the transaction resolves.

If the root connection dies unexpectedly (network failure, server kill), the delegated transaction is **doomed**. The server-side local transaction is tied to the session that created it — there is no recovery mechanism. The PSPE callbacks will attempt to operate on a dead connection and the transaction will abort. This is confirmed by the public `IPromotableSinglePhaseNotification` documentation.

## Terminology Across Layers

The two enlistment paths have inconsistent names across different layers of the code:

| Layer | PSPE Path | Non-PSPE Path |
|-------|-----------|---------------|
| System.Transactions | `EnlistPromotableSinglePhase` | (promotion triggers DTC) |
| SqlConnectionInternal logs | `"delegated to transaction"` | `"delegation not possible, enlisting"` |
| SqlConnectionInternal properties | `DelegatedTransaction`, `IsTransactionRoot` | `IsEnlistedInTransaction` |
| SqlInternalTransaction | `TransactionType.Delegated` | `TransactionType.Distributed` |
| TDS protocol | `Begin/Promote/Commit/Rollback` | `Propagate` |
| DbConnectionInternal (base) | `EnlistedTransaction` (both paths) | `EnlistedTransaction` (both paths) |

**"Delegated vs propagated"** is the clearest way to refer to the two paths, since `EnlistedTransaction` applies to both and "enlisted" is ambiguous. The TDS layer naming aligns: the delegated path uses `BEGIN`/`COMMIT`/`ROLLBACK`, while the propagated path uses `PROPAGATE`.

## Key Source Locations

| File | Relevant Members |
|------|-----------------|
| `DbConnectionInternal.cs` | `IsTransactionRoot` (~144), `IsTxRootWaitingForTxEnd` (~149), `SetInStasis()` (~776), `TerminateStasis()` (~937), `DelegatedTransactionEnded()` (~519) |
| `SqlConnectionInternal.cs` | `EnlistNonNull()`, `SqlDelegatedTransaction` inner class |

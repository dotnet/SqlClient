# MARS, Connection Pooling, and Transactions

This document covers how Multiple Active Result Sets (MARS) interacts with connection pooling and System.Transactions. MARS is a SQL Server feature that allows multiple active commands on a single physical connection simultaneously by multiplexing TDS sessions over one transport connection.

## What MARS Is

Without MARS, a `SqlConnection` supports exactly one active operation at a time. If you have an open `SqlDataReader`, you cannot execute another command on the same connection until the reader is closed. MARS removes this restriction by introducing **TDS session multiplexing**: each command/reader gets its own `TdsParserStateObject` (session) from a `TdsParserSessionPool`, all sharing the same underlying `TdsParser` (physical connection).

MARS is enabled by the connection string keyword `MultipleActiveResultSets=True`.

### The Session Pool

When MARS is on, the `TdsParser` owns a `TdsParserSessionPool` — a lightweight cache of `TdsParserStateObject` instances:

- `GetSession(owner)` — returns a free session or creates a new one (up to `MaxInactiveCount = 10` cached)
- `PutSession(session)` — returns a session for reuse or disposes it if the cache is full or the session is bad
- `Deactivate()` — reclaims orphaned sessions (readers that were GC'd without being closed)
- `Dispose()` — cleans up all sessions when the physical connection is being torn down

This is a **per-connection pool of TDS sessions**, completely separate from the **connection pool** that manages physical connections.

## MARS × Connection Return (Deactivation)

When a `SqlConnection` is closed and the internal connection returns to the pool, the deactivation sequence is:

1. **`SqlReferenceCollection.Deactivate()`** — Iterates all tracked `SqlDataReader`, `SqlCommand`, and `SqlBulkCopy` objects. Calls `CloseReaderFromConnection()` on any open readers, `OnConnectionClosed()` on commands and bulk copy objects. This cleans up user-facing objects that were left open when the user closed the connection.

2. **`SqlConnectionInternal.Deactivate()`** — Calls `_parser.Deactivate(isDoomed)`, which:
   - If MARS is on: calls `_sessionPool.Deactivate()` (reclaims orphaned sessions)
   - If MARS is off: drains any pending data on `_physicalStateObj`
   - Rolls back any uncommitted local transaction (non-delegated, non-distributed)

3. The connection then enters the pool's return path (`ReturnInternalConnection` / `DeactivateObject`), which routes it to the transacted pool, idle pool, or destruction.

**Key point:** By the time the connection reaches the pool's return path, all MARS sessions should be cleaned up. The session pool exists inside the physical connection, not in the connection pool's scope.

## MARS × Transactions

### Transaction ID in MARS Headers

Every TDS request sent over a MARS session includes a **MARS header** containing the current `_transactionId` (from `SqlInternalTransaction`). This is how SQL Server associates each multiplexed request with the correct server-side transaction. The `WriteMarsHeaderData` method in `TdsParser` writes this header.

### Open Results Block Delegated Transaction Completion

When MARS is on and a delegated transaction (PSPE) tries to commit or rollback, `ExecuteTransaction2005` checks `internalTransaction.OpenResultsCount`. If results are still open (readers consuming data), the commit/rollback **cannot proceed** — the connection is doomed and `CannotCompleteDelegatedTransactionWithOpenResults` is thrown.

```csharp
if (internalTransaction.OpenResultsCount != 0)
{
    throw SQL.CannotCompleteDelegatedTransactionWithOpenResults(this, _parser.MARSOn);
}
```

This means: if a user opens readers on a MARS connection inside a `TransactionScope` and then lets the scope dispose without closing the readers first, the delegated transaction cannot commit, the connection is doomed, and the error message includes whether MARS is on.

### MARS Session Acquisition for Delegated Transactions

When MARS is on and a delegated transaction needs to send a TM request (BEGIN, COMMIT, ROLLBACK), it acquires a dedicated session from the `TdsParserSessionPool`:

```csharp
if (_parser.MARSOn)
{
    stateObj = _parser.GetSession(this);
    mustPutSession = true;
}
```

The session is returned via `PutSession()` after the TM request completes. This ensures TM requests don't collide with user data operations on other sessions.

## MARS × Pool Lookup

MARS has **no direct effect** on connection pool lookup. The `GetFromTransactedPool()` path checks `Transaction.Current` and returns the physical connection associated with that transaction. Whether MARS is enabled on that connection doesn't change the lookup behavior — the same physical connection is returned regardless.

However, MARS indirectly matters because:
- With MARS on, multiple overlapping commands/readers can share one physical connection within the same transaction scope. The user opens the connection once, and multiple active results coexist.
- With MARS off, the user can still open and close `SqlConnection` multiple times within the same scope, getting the same physical connection back each time. But only one active result at a time is permitted.

## MARS × Connection Reuse Validation

When a connection is retrieved from the pool (transacted or idle), `ValidateConnectionForExecute` runs before any command executes:

- **MARS on:** Checks that the specific `SqlCommand` doesn't already have a live reader (per-command check). Multiple readers on different commands are allowed.
- **MARS off:** Checks that the entire connection has no live reader and no outstanding async commands. If either exists, the error message "There is already an open DataReader associated with this Connection which must be closed first" is thrown.

## MARS × Transaction Scope Options

| Scenario | MARS On | MARS Off |
|----------|---------|----------|
| Single `Required` scope, multiple readers | Allowed — each reader gets own TDS session | Error — only one reader at a time |
| Nested `RequiresNew` scope | Different physical connection (different transaction) | Same rule — different connection |
| `Suppress` scope | Bypasses transacted pool entirely | Same — bypasses transacted pool |

MARS doesn't change which physical connection is selected. It changes what the user can do with that connection once they have it.

## MARS × Connection Deactivation and Transactions

The `SqlConnectionInternal.Deactivate()` method has special handling for transaction roots:

```csharp
if (!(IsTransactionRoot && Pool == null))
{
    _parser.Deactivate(IsConnectionDoomed);
    if (!IsConnectionDoomed)
    {
        ResetConnection();
    }
}
```

If the connection is a delegated transaction root and has no pool (non-pooled connection), parser deactivation (including MARS session cleanup and transaction rollback) is **deferred** until the transaction completes. This prevents premature cleanup of the physical connection's state while PSPE callbacks still need it.

For pooled transaction roots, the pool handles the coordination — the connection goes into the transacted pool (or stasis) and deactivation of the parser happens at the right time.

## Implications for Pool Implementation

1. **Session pool is internal to the connection.** The connection pool doesn't need to know about MARS sessions. `TdsParserSessionPool` is managed entirely by `TdsParser`/`SqlConnectionInternal`. By the time a connection returns to the connection pool, MARS sessions are cleaned up.

2. **Open results block transaction completion.** If the pool triggers transaction finalization (e.g., `TransactionCompleted`) while MARS readers are still open, the delegated transaction path will throw/doom. This is handled at the `SqlConnectionInternal` level, not the pool level.

3. **No pool-level MARS branching needed.** The pool return path, transacted pool lookup, and cleanup paths don't branch on whether MARS is enabled. The `ReturnInternalConnection` / `DeactivateObject` logic is the same.

4. **`ValidateConnectionForExecute` is per-use, not per-pool.** The MARS validation runs when the user tries to execute a command, not when the pool hands out a connection. The pool doesn't enforce MARS reader limits.

## Key Source Files

| File | MARS-Relevant Code |
|------|-------------------|
| `TdsParserSessionPool.cs` | `GetSession`, `PutSession`, `Deactivate`, `Dispose` — MARS session management |
| `TdsParser.cs` | `Deactivate()` — MARS session cleanup; `WriteMarsHeaderData` — transaction ID in MARS headers |
| `SqlConnectionInternal.cs` | `ValidateConnectionForExecute` — per-command MARS check; `ExecuteTransaction2005` — OpenResultsCount check; `Deactivate()` — deferred cleanup for transaction roots |
| `SqlReferenceCollection.cs` | `Deactivate()` → `NotifyItem()` → `CloseReaderFromConnection()` — reader cleanup on connection close |
| `SqlInternalTransaction.cs` | `OpenResultsCount`, `_transactionId` — MARS header data |

# Design Note: Session Isolation Level and Connection Pooling

**Status**: Informational — background for PRs [#4330](https://github.com/dotnet/SqlClient/pull/4330) and [#4335](https://github.com/dotnet/SqlClient/pull/4335)
**Related issues**: [#96](https://github.com/dotnet/SqlClient/issues/96), [#146](https://github.com/dotnet/SqlClient/issues/146)

This is a design-rationale note, not a feature specification. It explains why two
superficially identical bugs — both described as "TransactionScope + connection pooling +
wrong isolation level" — are in fact **opposite failures** that require two separate,
independently switchable fixes.

---

## 1. The one shared fact

`sp_reset_connection` (issued when a pooled physical connection is handed back out)
**does not behave consistently with respect to the session `transaction_isolation_level`**:

| Server | Effect of the reset on session isolation level |
|---|---|
| On-prem SQL Server | Does **not** reset it — the level survives |
| Azure SQL DB | **Does** reset it to the database default (e.g. `read committed snapshot`) |

Both issues stem from this single inconsistency, but they sit on opposite sides of it:

- **#96** — the level *survives* when it should not → **leak**
- **#146** — the level *is wiped* when it should not be → **silent downgrade**

No single change can simultaneously "stop the level surviving" and "make the level survive".

---

## 2. Issue #96 — isolation level leaks *out* of a completed transaction

### Symptom

```csharp
using (var tx = conn.BeginTransaction(IsolationLevel.Serializable)) { /* ... */ }
conn.Close();                       // physical connection returns to the pool

conn.Open();                        // new logical connection, possibly an unrelated caller
// sys.dm_exec_sessions.transaction_isolation_level == 4 (Serializable)   <-- BUG
```

### Characteristics

- Reproduces on **on-prem SQL Server** (and anywhere the reset does not clear the level).
- Reproduces with plain **`SqlTransaction`**, not only `TransactionScope`.
- The transaction is **already completed** (committed or rolled back).
- The victim is an **unrelated later consumer** of the same pooled physical connection.

### Root cause

The TDS `TM_BEGIN_XACT` request carries an `ISOLATION_LEVEL` byte and mutates session state.
`TM_COMMIT_XACT` / `TM_ROLLBACK_XACT` deliberately send `ISOLATION_LEVEL = 0x00`
("no isolation level change requested; use current") — this is **by design in MS-TDS**.
Nothing restores `READ COMMITTED`, and `sp_reset_connection` does not either.

### Impact

In autocommit mode every statement runs in an implicit transaction at the current session
level, so an unrelated request can silently execute under `SERIALIZABLE` (extra blocking and
deadlocks) or `SNAPSHOT` (unexpected optimistic-concurrency semantics). Reported repeatedly
since 2017 with production impact.

### Fix shape (PR #4330)

| | |
|---|---|
| Code path | `SqlConnectionInternal.Deactivate()` — the pool **return** / close path |
| Mechanism | Track `_isolationLevelDirty` when a TM `Begin` sets a non-default level; on pool return issue `SET TRANSACTION ISOLATION LEVEL READ COMMITTED;` |
| Direction | **Scrub** session state on the way back into the pool |
| App context switch | `Switch.Microsoft.Data.SqlClient.UseLegacyIsolationLevelBehavior` |
| Error handling | A plain T-SQL rejection (e.g. Synapse dedicated pools accept only `READ UNCOMMITTED`) degrades gracefully; transport failures doom the connection |
| Cost | **One extra round trip on `Close()`**, paid only when a previous `Begin` raised the isolation level. The queued `sp_reset_connection` rides this batch's TDS header instead of the next user's first command, so the reset is not billed twice — but the batch itself is an exchange the legacy close path did not make. |

---

## 3. Issue #146 — isolation level is lost *inside* a live `TransactionScope`

### Symptom

```csharp
using var scope = new TransactionScope(
    TransactionScopeOption.RequiresNew,
    new TransactionOptions { IsolationLevel = IsolationLevel.Serializable });

TestExec(cs);   // open #1 -> "serializable"
TestExec(cs);   // open #2 -> "read committed snapshot"    <-- BUG (Azure SQL DB only)
```

### Characteristics

- Reproduces on **Azure SQL DB only** — the exact inverse of the #96 server matrix.
- Requires an ambient **`TransactionScope`**; plain `SqlTransaction` cannot reach this path.
- The transaction is **still open and still ambient**.
- The victim is the **same caller inside the same scope**, on its 2nd and later `Open()`.
- Disappears with `Pooling=No`.

### Root cause

1. The first `Open()` inside the scope enlists the connection and sends
   `SET TRANSACTION ISOLATION LEVEL <ambient>`.
2. `Close()` returns the physical connection to the **transacted pool**, still enlisted in the
   same `Transaction`.
3. The second `Open()` receives the same physical connection back.
   `SqlConnectionInternal.Enlist(Transaction)` observes
   `transaction.Equals(EnlistedTransaction)` and **short-circuits** — by design it sends
   nothing, because the connection is considered already enlisted.
4. A pending `sp_reset_connection_keep_transaction` piggybacks the next batch. On Azure SQL DB
   that reset clears the session isolation level back to the database default.

The bug is therefore a gap in an optimization: the short-circuit assumes the session still
carries the level set in step 1.

### Impact

A silent, hard-to-detect **downgrade** of a level the developer explicitly requested. Callers
relying on `SNAPSHOT` lose optimistic-concurrency conflict detection; callers relying on the
documented `TransactionScope` `Serializable` default silently run read-committed-snapshot.

### Fix shape (PR #4335)

| | |
|---|---|
| Code path | `SqlConnectionInternal.Enlist()` — the **re-enlistment / checkout** path (the `else if` on the equality short-circuit) |
| Mechanism | When a reset is pending, re-issue `SET TRANSACTION ISOLATION LEVEL <ambient>` mapped from `Transaction.IsolationLevel` |
| Direction | **Re-assert** session state on the way back out of the pool |
| App context switch | `Switch.Microsoft.Data.SqlClient.UseLegacyTransactionScopeIsolationBehavior` |
| Special case | `Snapshot` is intentionally **not** re-asserted — switching to `SNAPSHOT` while a transaction is open causes SQL Server to fail and roll back the transaction, and the delegated transaction was already begun under snapshot isolation by the TM request |
| Cost | One extra round trip per pooled re-checkout inside a scope, on all back ends |

---

## 4. Side-by-side comparison

| Dimension | #96 / PR #4330 | #146 / PR #4335 |
|---|---|---|
| Failure mode | Level **persists** when it should be cleared | Level **is cleared** when it should persist |
| Transaction state | Already **completed** | Still **open / ambient** |
| Who is harmed | An **unrelated later** pool consumer | The **same caller**, next `Open()` in the scope |
| Affected servers | On-prem SQL Server (reset does not clear) | Azure SQL DB (reset does clear) |
| API surface | `SqlTransaction` **and** `TransactionScope` | `TransactionScope` only |
| Trigger | TM `Begin` set a non-default level | Re-enlist short-circuit with a pending reset |
| Code path | `Deactivate()` (pool **return** / close) | `Enlist()` (pool **checkout**) |
| T-SQL emitted | `SET ... READ COMMITTED` (fixed value) | `SET ... <ambient level>` (dynamic value) |
| Trigger condition | `_isolationLevelDirty` | `_parser._fResetConnection` on the equal-transaction branch |
| Direction of fix | **Scrub** session state | **Re-assert** session state |
| App context switch | `UseLegacyIsolationLevelBehavior` | `UseLegacyTransactionScopeIsolationBehavior` |
| `Snapshot` handling | Reset to `READ COMMITTED` like any other level | Deliberately **skipped** |

---

## 5. Why neither fix subsumes the other

**Would #4330 alone fix #146?** No. It runs only on pool **return** and only ever writes
`READ COMMITTED`. The #146 repro never completes the transaction between the two opens, and
even if the reset did run it would write the *wrong* level — the ambient level is
`Serializable` / `ReadUncommitted`, not `READ COMMITTED`. Applying it there would make #146
strictly worse, turning an Azure-only bug into a universal one.

**Would #4335 alone fix #96?** No. It fires only inside `Enlist()` on the
"same transaction re-attach" branch — i.e. while an ambient `TransactionScope` is still open.
The #96 repro has **no live transaction** at the point of leakage, and its `SqlTransaction`
variant never goes through `Enlist()` at all, so the leak path is never reached.

**Could one generalized fix cover both?** Only by conflating two opposite intents in a single
place:

- On pool **return**, the desired behavior is to *forget* the level (#96).
- On pool **checkout inside a live scope**, the desired behavior is to *remember and re-apply*
  it (#146).

They live at opposite ends of the pooling lifecycle and require different values written,
different trigger conditions, and different `Snapshot` semantics. Merging them would also mean
a single app context switch governing two unrelated behavior changes, preventing an
application from opting into one without the other.

---

## 6. Why the two PRs should still be reviewed together

- They touch the same file (`SqlConnectionInternal.cs`), use the same helper pattern, add tests
  to the same folder (`tests/ManualTests/SQL/TransactionTest/`), and both extend
  `LocalAppContextSwitches` and its test helper — so they **will conflict textually** and should
  be sequenced.
- Both rest on the same `sp_reset_connection` premise, which a reviewer need only validate once.
- With both merged the end-to-end behavior becomes coherent:
  - inside a live scope, the ambient level is honored on every open (#4335);
  - once the transaction ends and the connection is pooled, the level is scrubbed (#4330).
- Two separate switches is the correct granularity: an application can opt out of the extra
  round trip introduced by either PR while keeping the other's fix.

### A note on cost

Both PRs add one extra round trip; they differ only in *when* it is paid. #4330 pays it on
`Close()`, once per connection that raised its isolation level. #4335 pays it on every pooled
re-checkout inside a scope. Neither is free, and #4330's earlier claim of "no extra round trip"
was incorrect: `PrepareResetConnection` performs no I/O of its own (it only sets a flag that is
consumed at the next packet write), so the legacy close path sent nothing at all.

#4330 deliberately performs this I/O in `Deactivate()` rather than in `ResetConnection()`.
`ResetConnection()` is also invoked by the pool from `PutObjectFromTransactedPool`, which runs on
the `System.Transactions` transaction-completion callback thread while holding a lock on the
connection; that call site explicitly avoids socket work on a thread it does not own. `Deactivate()`
always runs on the closing thread and every pooled return passes through it, so it covers the same
cases without breaking that contract.

### Suggested review order

1. **#4330** first — broader blast radius (all servers, both `SqlTransaction` and
   `TransactionScope`), long-standing and frequently requested, and self-contained on the
   deactivate path.
2. **#4335** second — rebase onto #4330, then resolve the open performance question
   (unconditional `SET` vs. Azure-gated vs. deferring the `SET` so it prefixes the user's next
   batch).

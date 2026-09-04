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
| Code path | `SqlConnectionInternal.Activate()` — the pool **checkout** path, before enlistment |
| Mechanism | Track `_isolationLevelDirty` when a TM `Begin` sets a non-default level; on the next checkout, if the connection is not enlisted, issue `SET TRANSACTION ISOLATION LEVEL READ COMMITTED;` |
| Direction | **Scrub** stale session state on the way out of the pool |
| Error handling | A plain T-SQL rejection (e.g. Synapse dedicated pools accept only `READ UNCOMMITTED`) degrades gracefully; transport failures doom the connection |
| Cost | **One extra round trip on `Open()`**, paid only when a previous `Begin` raised the isolation level *and* the connection is actually reused. The queued `sp_reset_connection` rides this batch's TDS header instead of the caller's first command, so the reset is not billed twice — but the batch itself is an exchange the legacy path did not make. |

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
| Code path | `Activate()` (pool **checkout**, not enlisted) | `Enlist()` (pool **checkout**, re-attaching to the same transaction) |
| T-SQL emitted | `SET ... READ COMMITTED` (fixed value) | `SET ... <ambient level>` (dynamic value) |
| Trigger condition | `_isolationLevelDirty` | `_parser._fResetConnection` on the equal-transaction branch |
| Direction of fix | **Scrub** session state | **Re-assert** session state |
| `Snapshot` handling | Reset to `READ COMMITTED` like any other level | Deliberately **skipped** |

---

## 5. Why neither fix subsumes the other

**Would #4330 alone fix #146?** No — and it is explicitly built not to make #146 worse. #4330
only ever writes `READ COMMITTED`, which is the *wrong* level for the #146 repro (the ambient
level there is `Serializable` / `ReadUncommitted`). Its scrub is therefore gated on the
connection **not** being enlisted, so it never fires on the re-attach path #4335 owns. Without
that gate it would turn #146 from an Azure-only bug into a universal one.

**Would #4335 alone fix #96?** No. It fires only inside `Enlist()` on the
"same transaction re-attach" branch — i.e. while an ambient `TransactionScope` is still open.
The #96 repro has **no live transaction** at the point of leakage, and its `SqlTransaction`
variant never goes through `Enlist()` at all, so the leak path is never reached.

**Could one generalized fix cover both?** Only by conflating two opposite intents in a single
place:

- On checkout of a connection with **no live transaction**, the desired behavior is to *forget*
  the stale level (#96).
- On checkout of a connection **re-attaching to a still-live scope**, the desired behavior is to
  *remember and re-apply* it (#146).

Both now sit on the checkout side of the pooling lifecycle, but they are distinguished by
enlistment state and require different values written, different trigger conditions, and
different `Snapshot` semantics. Merging them would also mean a single app context switch
governing two unrelated behavior changes, preventing an application from opting into one
without the other.

---

## 6. Why the two PRs should still be reviewed together

- They touch the same file (`SqlConnectionInternal.cs`), use the same helper pattern, add tests
  to the same folder (`tests/ManualTests/SQL/TransactionTest/`), and both extend
  `LocalAppContextSwitches` and its test helper — so they **will conflict textually** and should
  be sequenced.
- Both rest on the same `sp_reset_connection` premise, which a reviewer need only validate once.
- With both merged the end-to-end behavior becomes coherent:
  - inside a live scope, the ambient level is honored on every open (#4335);
  - once the transaction ends and the connection is vended again, the stale level is scrubbed
    (#4330).
- Two separate switches is the correct granularity: an application can opt out of the extra
  round trip introduced by either PR while keeping the other's fix.

### A note on cost

Both PRs add one extra round trip; they differ only in *which* checkout pays it. #4330 pays it
on the first `Open()` after a connection whose isolation level was raised is reused. #4335 pays
it on every pooled re-checkout inside a scope. Neither is free, and #4330's earlier claim of "no
extra round trip" was incorrect: `PrepareResetConnection` performs no I/O of its own (it only
sets a flag that is consumed at the next packet write), so the legacy close path sent nothing at
all.

#4330 performs this I/O in `Activate()` rather than on the pool-return side. Two constraints
rule out the return path:

- On return the connection may still be enlisted in a live `TransactionScope`, because `Close()`
  is routinely called inside the scope. Issuing `SET` there would downgrade the level for the
  next connection vended into that same scope from the transacted pool — exactly the #146 defect.
- `ResetConnection()`, the other pool-return hook, is also invoked by the pool from
  `PutObjectFromTransactedPool`, which runs on the `System.Transactions` transaction-completion
  callback thread while holding a lock on the connection; that call site explicitly avoids socket
  work on a thread it does not own.

`Activate()` is subject to neither: it always runs on the thread performing the checkout, the
previous transaction has ended by then, and the enlistment gate keeps it out of #4335's way. It
also means the cost is only paid by connections that are actually reused.

### Suggested review order

1. **#4330** first — broader blast radius (all servers, both `SqlTransaction` and
   `TransactionScope`), long-standing and frequently requested, and self-contained on the
   activate path.
2. **#4335** second — rebase onto #4330, then resolve the open performance question
   (unconditional `SET` vs. Azure-gated vs. deferring the `SET` so it prefixes the user's next
   batch).

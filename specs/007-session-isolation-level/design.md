# Design Note: Session Isolation Level and Connection Pooling

**Status**: Informational — background for PRs [#4330](https://github.com/dotnet/SqlClient/pull/4330) and [#4335](https://github.com/dotnet/SqlClient/pull/4335)
**Related issues**: [#96](https://github.com/dotnet/SqlClient/issues/96), [#146](https://github.com/dotnet/SqlClient/issues/146)

This is a design-rationale note, not a feature specification. It explains why two
superficially identical bugs — both described as "TransactionScope + connection pooling +
wrong isolation level" — are in fact **opposite failures** that require two separate fixes.

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
| Mechanism | Track `_isolationLevelDirty` when a TM `Begin` or successful ambient reassertion sets a non-default level; on the next checkout, if the connection is neither enlisted nor an active delegated root, issue `SET TRANSACTION ISOLATION LEVEL READ COMMITTED;` |
| Direction | **Scrub** stale session state on the way out of the pool |
| App context switch | `Switch.Microsoft.Data.SqlClient.EnableTransactionIsolationLevelReset` (default `false`; the fix is initially opt-in) |
| Error handling | Synapse dedicated pools are skipped up front; any reset failure dooms the connection so an unknown isolation level is never handed to the caller |
| Timeout | The reset uses the caller's remaining `Open` budget, including time consumed in the pool. `Command Timeout` does not apply; `Connect Timeout=0` remains intentionally infinite. Existing pool-wait/login compatibility behavior is unchanged. |
| Cost | **One extra round trip on `Open()`**, paid only when a previous `Begin` or reassertion set a tracked non-default level *and* the connection is eligible for reset and actually reused. The queued `sp_reset_connection` rides this batch's TDS header instead of the caller's first command, so the reset is not billed twice — but the batch itself is an exchange the legacy path did not make. |

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
| Isolation levels | Re-assert `ReadUncommitted`, `RepeatableRead`, `Serializable`, and `Snapshot`. Skip `ReadCommitted` (no reassertion needed), `Unspecified`, and `Chaos` (no statement mapping). Reasserting `Snapshot` preserves the level under which the delegated transaction began; it does not introduce Snapshot into a transaction begun at another level. |
| Activation | Unconditional; no compatibility switch |
| Cost | One extra round trip on a pooled re-checkout with a reset pending and one of the mapped non-default levels, on all back ends |

#### Measured cost

`TransactionScopeIsolationRunner` (perf pipeline build `169256`, baseline MDS 7.0.2 vs this
branch, medians of three interleaved reps, `OpensPerScope = 5` — one enlist plus four pooled
re-checkouts):

| Benchmark | Baseline 7.0.2 | With fix |
|---|---|---|
| `OpensOutsideScope` | 1.680 ms | 1.635 ms |
| `OpensInsideScope_ReadCommitted` | 2.016 ms | 1.958 ms |
| `OpensInsideScope_Serializable` | 2.058 ms | 2.579 ms |
| `OpensInsideScope_SerializableAsync` | 2.679 ms | 3.060 ms |

The binary-independent signal is the within-build gap between `_Serializable` and
`_ReadCommitted`, which varies only by whether the re-assert fires: **+0.04 ms** on the baseline
(noise, neither re-asserts) versus **+0.62 ms** with the fix. That is roughly **0.15 ms and 8 KB
per re-assert**, with Gen0 rising from 7 to 11 per iteration. The runner targets `localhost`, so
these are loopback numbers and represent a floor; over a real network each re-assert costs
approximately one additional round trip.

The `_ReadCommitted` row is flat against baseline, confirming the skip is genuinely free rather
than merely cheap.

#### Alternatives considered

- **Gate on Azure SQL DB.** Rejected: the behavior is a server-version detail, not a contract,
  and an on-prem configuration that begins honoring the reset would silently regress.
- **Defer the `SET` so it prefixes the user's next batch.** This would fold the extra exchange
  into work already being sent. Rejected for now: it needs a command-execution hook that does not
  exist today, and it would reorder the level change relative to anything not issued through
  `SqlCommand`. Worth revisiting as an optimization once the pooling rewrite settles.
- **Re-assert only when the level differs from `READ COMMITTED`.** This is what ships — see the
  isolation-level mapping above.

---

## 4. Side-by-side comparison

| Dimension | #96 / PR #4330 | #146 / PR #4335 |
|---|---|---|
| Failure mode | Level **persists** when it should be cleared | Level **is cleared** when it should persist |
| Transaction state | Already **completed** | Still **open / ambient** |
| Who is harmed | An **unrelated later** pool consumer | The **same caller**, next `Open()` in the scope |
| Affected servers | On-prem SQL Server (reset does not clear) | Azure SQL DB (reset does clear) |
| API surface | `SqlTransaction` **and** `TransactionScope` | `TransactionScope` only |
| Trigger | TM `Begin` or ambient reassertion set a non-default level | Re-enlist short-circuit with a pending reset |
| Code path | `Activate()` (pool **checkout**, neither enlisted nor a delegated root) | `Enlist()` (pool **checkout**, re-attaching to the same transaction) |
| T-SQL emitted | `SET ... READ COMMITTED` (fixed value) | `SET ... <ambient level>` (dynamic value) |
| Trigger condition | `_isolationLevelDirty` | `_parser._fResetConnection` on the equal-transaction branch |
| Activation | Opt-in via `EnableTransactionIsolationLevelReset` (default `false`) | Unconditional; independent of the #96 switch |
| Direction of fix | **Scrub** session state | **Re-assert** session state |
| `Snapshot` handling | Reset to `READ COMMITTED` like any other non-default level after the transaction ends | Re-assert `SNAPSHOT` inside the same transaction |
| `ReadCommitted` handling | Does not mark the session dirty | **Skipped** — the reset already lands there |

---

## 5. Why neither fix subsumes the other

**Would #4330 alone fix #146?** No — and it is explicitly built not to make #146 worse. #4330
only ever writes `READ COMMITTED`, which is the *wrong* level for the #146 repro (the ambient
level there is `Serializable` / `ReadUncommitted`). Its scrub is therefore gated on the
connection **neither** being enlisted **nor** an active delegated root, so it never fires on the re-attach path #4335 owns. Without
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
different `Snapshot` semantics. The #96 reset is independently gated so applications can opt
into that behavior without changing the #146 path.

---

## 6. Why the two PRs should still be reviewed together

- They touch the same file (`SqlConnectionInternal.cs`) and add tests to the same folder
  (`tests/ManualTests/SQL/TransactionTest/`), so their combined activation/enlistment behavior
  must be validated when integrating the second fix.
- Both rest on the same `sp_reset_connection` premise, which a reviewer need only validate once.
- With both merged the end-to-end behavior becomes coherent:
  - inside a live scope, the ambient level is honored on every open (#4335);
  - once the transaction ends and the connection is vended again, the stale level is scrubbed
    when the reset switch is enabled (#4330).
- The #96 switch controls only its stale-session scrub; opting in does not alter the #146
  re-enlistment behavior.

### A note on cost

Both PRs add one extra round trip; they differ only in *which* checkout pays it. #4330 pays it
on the first `Open()` after a connection whose isolation level was raised is reused. #4335 pays
it on re-checkouts with a reset pending and a mapped non-default isolation level; `ReadCommitted`,
`Unspecified`, and `Chaos` add no reassertion round trip. Neither is free, and #4330's earlier claim of "no
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

`Activate()` runs on the checkout path. The enlistment and delegated-root gates defer the scrub
until transaction ownership has ended, retaining the dirty flag for a later checkout. The cost
is only paid by connections that are actually reused.

### Endpoint classification boundary

The dedicated Synapse exemption matches the `.sql.azuresynapse.` host segment, consistent with
`IsAzureSynapseOnDemandEndpoint`, rather than a list of cloud domain suffixes. Only the host is
inspected (protocol prefix, instance/pipe name and port are stripped), and the segment must
directly follow a single workspace label, optionally followed by `.privatelink`. Workspace names
ending in `-ondemand` (serverless, including Private Link) remain eligible for the reset. Names
that merely contain the segment elsewhere (for example `contoso.workspace.sql.azuresynapse.net`
or `workspace.example.sql.azuresynapse.net`) are not exempt.

Because labels after the segment are not validated, any cloud suffix, including future or custom
ones such as `workspace.sql.azuresynapse.net.example`, is treated as a dedicated endpoint and
skips the reset. This is a deliberate tradeoff: it avoids maintaining a suffix list and covers
sovereign clouds, at the cost of skipping the opt-in reset for a non-Synapse server that happens
to use such a DNS name. Custom aliases that do not contain the segment are not detected; they
follow the normal opt-in reset path, where a rejection dooms the connection and fails `Open`
rather than silently handing out a potentially dirty session. This static classification does
not establish runtime capabilities of arbitrary TDS endpoints.

#### Dirty-tracking interaction

#4330 sets `_isolationLevelDirty` in `ExecuteTransaction2005` on a TM `Begin` carrying a
non-default level. In the common delegated case that `Begin` is the first `Open()` inside the
scope — the same event that establishes the level #4335 later re-asserts — so the re-assert
re-writes an already-tracked value and nothing goes untracked.

For a connection that joined an **already-promoted** transaction via
`PropagateTransactionCookie`, no local `Begin` runs. The integrated reassertion path therefore
also sets `_isolationLevelDirty` after a successful non-default `SET`, so the next eligible
checkout can scrub that level when the #96 switch is enabled.

### Integration order

**#4335 merged first.** #4330 merges that main revision and validates both regression suites
together. Reassertion remains unconditional, while scrubbing remains opt-in. The reset uses
the remaining caller timeout; broader remaining-budget work for other in-open calls, including
reassertion, remains tracked in #4582.

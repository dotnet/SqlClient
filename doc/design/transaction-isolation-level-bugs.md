# Transaction Isolation Level Bugs: Executive Summary

**Date:** April 2026  
**Scope:** GH #96 and GH #146  
**Status:** Investigation complete; test suite created; fix implementation pending

---

## Problem Statement

`sp_reset_connection` — the server-side procedure that SQL Server executes when a pooled connection is reused — does **not** reliably reset the session isolation level. This causes two distinct bugs depending on the server environment:

| Bug | Environment | Symptom |
|-----|------------|---------|
| **GH #96** | All (on-prem + Azure SQL) | Isolation level **leaks** to the next pool consumer after a transaction completes |
| **GH #146** | Azure SQL only | Isolation level is **incorrectly reset** mid-scope when a pooled connection is reused within the same `TransactionScope` |

Both bugs are silent — no exception is thrown. The application executes queries at the wrong isolation level, leading to data consistency issues that are difficult to diagnose.

---

## Root Cause

### How connection reset works today

When a `SqlConnection` is returned to the pool and later reused, the driver sets a flag in the next outgoing TDS packet header:

| TDS Header Flag | Constant | When Used |
|-----------------|----------|-----------|
| `ST_RESET_CONNECTION` (0x08) | Full reset | Connection has no active enlisted transaction |
| `ST_RESET_CONNECTION_PRESERVE_TRANSACTION` (0x10) | Reset preserving transaction | Connection is enlisted in a `TransactionScope` that is still active |

The server responds by executing `sp_reset_connection`, which resets database context, language, session settings, and temporary objects — but **not** the session isolation level.

The driver's `ResetConnection()` method (`SqlConnectionInternal.cs`) resets its cached `_originalDatabase` and `_currentLanguage` values, but has **no code** to track or reset the isolation level.

### GH #96: Isolation level leaks across pool round-trips

**Sequence:**
1. App opens connection from pool, begins `Serializable` transaction, commits, closes connection (returns to pool)
2. App opens connection from pool — gets same physical connection
3. Session isolation level is still `Serializable`, even though no transaction is active

**Affected scenarios:** `SqlTransaction` (commit, rollback, dispose), `TransactionScope` (complete, abort), MARS connections — all leak equally.

### GH #146: Azure SQL resets isolation level when it shouldn't

**Sequence:**
1. App opens `TransactionScope` with `Serializable`
2. Opens first `SqlConnection`, enlists in scope, executes query at `Serializable`, closes connection (returns to pool)
3. Opens second `SqlConnection` within same scope — reuses same pooled connection with `PRESERVE_TRANSACTION` flag
4. Azure SQL's `sp_reset_connection` incorrectly resets the isolation level to `ReadCommitted` despite the `PRESERVE_TRANSACTION` flag
5. Second connection executes at `ReadCommitted` instead of `Serializable`

This bug is **Azure SQL-specific** — on-prem SQL Server correctly preserves the isolation level with `PRESERVE_TRANSACTION`.

---

## Behavior Matrix

The table below maps every combination of transaction type, lifecycle event, and server environment to expected vs. actual behavior:

### Key

- **Expected:** The isolation level the next pool consumer *should* see
- **Actual:** What currently happens without any fix
- ✅ = Correct behavior (no bug)
- ❌ = Incorrect behavior (bug)

### After transaction completes (GH #96)

| Transaction Type | Lifecycle | Expected Next Consumer | Actual (On-Prem) | Actual (Azure SQL) |
|-----------------|-----------|----------------------|-------------------|-------------------|
| `SqlTransaction(Serializable)` | Commit | ReadCommitted | ❌ Serializable | ❌ Serializable |
| `SqlTransaction(Serializable)` | Rollback | ReadCommitted | ❌ Serializable | ❌ Serializable |
| `SqlTransaction(Serializable)` | Dispose (implicit rollback) | ReadCommitted | ❌ Serializable | ❌ Serializable |
| `SqlTransaction(RepeatableRead)` | Commit | ReadCommitted | ❌ RepeatableRead | ❌ RepeatableRead |
| `SqlTransaction(ReadUncommitted)` | Commit | ReadCommitted | ❌ ReadUncommitted | ❌ ReadUncommitted |
| `SqlTransaction(ReadCommitted)` | Commit | ReadCommitted | ✅ ReadCommitted | ✅ ReadCommitted |
| `TransactionScope(Serializable)` | Complete | ReadCommitted | ❌ Serializable | ❌ Serializable |
| `TransactionScope(Serializable)` | Abort (no Complete) | ReadCommitted | ❌ Serializable | ❌ Serializable |
| `TransactionScope(RepeatableRead)` | Complete | ReadCommitted | ❌ RepeatableRead | ❌ RepeatableRead |
| `TransactionScope(ReadCommitted)` | Complete | ReadCommitted | ✅ ReadCommitted | ✅ ReadCommitted |
| MARS + `SqlTransaction(Serializable)` | Commit | ReadCommitted | ❌ Serializable | ❌ Serializable |
| MARS + `TransactionScope(Serializable)` | Complete | ReadCommitted | ❌ Serializable | ❌ Serializable |
| MARS + concurrent readers + `Serializable` | Commit | ReadCommitted | ❌ Serializable | ❌ Serializable |
| Non-pooled (any) | Any | ReadCommitted | ✅ ReadCommitted | ✅ ReadCommitted |

### Within active TransactionScope (GH #146)

| Scenario | Expected 2nd Connection | Actual (On-Prem) | Actual (Azure SQL) |
|----------|------------------------|-------------------|-------------------|
| `TransactionScope(Serializable)`, two connections | Serializable | ✅ Serializable | ❌ ReadCommitted |
| `TransactionScope(RepeatableRead)`, two connections | RepeatableRead | ✅ RepeatableRead | ❌ ReadCommitted |
| `TransactionScope(ReadUncommitted)`, two connections | ReadUncommitted | ✅ ReadUncommitted | ❌ ReadCommitted |
| `TransactionScope(ReadCommitted)`, two connections | ReadCommitted | ✅ ReadCommitted | ✅ ReadCommitted |

---

## MARS Considerations

MARS connections use **session multiplexing** — multiple logical TDS sessions share one physical TCP connection. This affects the reset mechanism:

| Aspect | Non-MARS | MARS |
|--------|----------|------|
| Reset serialization | Synchronous; flag cleared immediately | Async via `AutoResetEvent`; first session to execute sends the reset, others skip it |
| Reset confirmation | Inline | Via read callback (`CheckSetResetConnectionState`) |
| Transaction scope | Single `_currentTransaction` per parser | Same — shared across all MARS sessions |
| Isolation level bug impact | Same as MARS | Same severity; additional complexity in fix |

The reset flag `_fResetConnection` lives on `TdsParser` (shared), while per-session tracking (`_fResetConnectionSent`, `_fResetEventOwned`) lives on individual `TdsParserStateObject` instances. Any fix must respect this serialization model.

---

## Code Path Summary

```
SqlConnection.Close()
  └─> Pool return
        └─> Deactivate()
              ├─> TdsParser.Deactivate()         // drains pending data, reclaims MARS sessions
              └─> ResetConnection()               // ← WHERE THE FIX GOES
                    ├─> PrepareResetConnection(preserveTx: bool)
                    │     └─> Sets _fResetConnection = true
                    │         Sets _fPreserveTransaction = (EnlistedTransaction != null)
                    ├─> Resets _originalDatabase, _currentLanguage
                    └─> ⚠️ NO isolation level tracking or reset

Next SqlConnection.Open() (reuses pooled connection)
  └─> First command execution
        └─> CheckResetConnection(stateObj)
              ├─> [MARS] Acquires _resetConnectionEvent
              ├─> Sets TDS header: ST_RESET_CONNECTION or ST_RESET_CONNECTION_PRESERVE_TRANSACTION
              └─> Server executes sp_reset_connection
                    └─> ⚠️ Does NOT reset isolation level (GH #96)
                    └─> ⚠️ Azure: PRESERVE_TRANSACTION incorrectly resets it (GH #146)
```

---

## Test Coverage

A comprehensive test suite has been created at `tests/ManualTests/SQL/TransactionTest/TransactionIsolationLevelTest.cs` covering 6 categories:

| Category | Test Count | What It Covers |
|----------|-----------|----------------|
| 1. SqlTransaction leak | 5 | Commit, rollback, dispose, ReadCommitted baseline, successive transactions |
| 2. TransactionScope leak | 3 | Complete, abort, ReadCommitted baseline |
| 3. TransactionScope preservation | 1 | Two connections within same scope (GH #146) |
| 4. Mixed scenarios | 2 | TransactionScope then SqlTransaction; successive scopes with different levels |
| 5. Non-pooled baselines | 2 | Sanity checks confirming fresh connections are clean |
| 6. MARS | 3 | SqlTransaction + MARS, TransactionScope + MARS, concurrent readers + MARS |

**Current baseline without any fix applied:** 10 pass (baselines + ReadCommitted cases), 21 fail (all leak/preservation scenarios — confirming both bugs).

Detection method: `DBCC USEROPTIONS` (works on Azure SQL without `VIEW SERVER STATE` permission, unlike `sys.dm_exec_sessions`).

---

## Solution Approaches (High Level)

### Fix for GH #96: Reset isolation level on pool return

**Approach A — Client-side `SET TRANSACTION ISOLATION LEVEL READ COMMITTED`:**
Track whether the session used a non-default isolation level (e.g., via a `_hasNonDefaultIsolationLevel` flag on `SqlConnectionInternal`). In `ResetConnection()`, if the flag is set, execute `SET TRANSACTION ISOLATION LEVEL READ COMMITTED` to explicitly reset it before the connection is reused.

- **Pros:** Deterministic, works on all server versions, no server-side dependency
- **Cons:** Extra round-trip on pool return (only when non-default level was used)

**Approach B — Piggyback on the TDS reset:**
Send the `SET` command in-band with the reset, avoiding a separate round-trip.

- **Pros:** No extra round-trip
- **Cons:** More complex; must integrate with MARS serialization

### Fix for GH #146: Reapply isolation level after PRESERVE_TRANSACTION reset

After `sp_reset_connection` with `PRESERVE_TRANSACTION`, if the connection has a delegated transaction with a known isolation level, re-send `SET TRANSACTION ISOLATION LEVEL` to restore it. The `SqlDelegatedTransaction` already stores `_isolationLevel`, so the information is available.

- **Pros:** Targeted fix, only applies on Azure SQL with PRESERVE_TRANSACTION
- **Cons:** Extra round-trip; needs to detect when re-application is necessary

### Interaction between fixes

Both fixes modify the reset/reactivation path. They must be implemented and tested together to avoid one fix interfering with the other. The test suite is designed to validate both simultaneously.

---

## Next Steps

1. **Review and lock in test suite** — Team reviews the 16 test methods (32 parameterized cases) to ensure coverage is complete
2. **Implement GH #96 fix** — Add isolation level tracking + reset in `ResetConnection()`
3. **Implement GH #146 fix** — Add isolation level re-application after PRESERVE_TRANSACTION
4. **Validate** — All 21 currently-failing tests should pass; all 10 currently-passing tests must continue to pass
5. **Performance assessment** — Measure impact of extra round-trip on pool return/reuse

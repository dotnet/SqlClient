# Recommendations

Fixes are prioritised by the severity rankings in [03-issues.md](03-issues.md).

---

## Fix 1 — Implement `ChannelDbConnectionPool.ReplaceConnection` (Issue A)

### Goal

Make transparent reconnection work when the V2 connection pool is enabled.

### Approach

Implement `ReplaceConnection` in `ChannelDbConnectionPool` to mirror the behaviour of
`WaitHandleDbConnectionPool.ReplaceConnection`:

1. Create a new physical connection via `ConnectionFactory.CreatePooledConnection` (which passes
   `recoverySessionData` through `SqlConnectionFactory.CreateConnection`).
2. Remove the old connection from the slot tracking structure.
3. Add the new connection.
4. Call `PrepareConnection` on the new connection.
5. Call `PrepareForReplaceConnection`, `DeactivateConnection`, and `Dispose` on the old connection.

### Files to change

| File | Change |
| ---- | ------ |
| `ChannelDbConnectionPool.cs` | Replace `throw new NotImplementedException()` with working implementation |

### Tests

- Unit test: mock a broken connection, enable V2 pool, verify `ReplaceConnection` returns a live
  connection and disposes the old one.
- Functional test: open connection, `USE [db]`, break the physical connection (e.g. `kill
  session_id` on the server), execute another command, verify `CurrentDatabase` matches the `USE`d
  database.

---

## Fix 2 — Make `CurrentSessionData` snapshot atomic (Issue B)

### Goal

Prevent race conditions when snapshotting session state for recovery.

### Approach

Instead of writing `_database` and `_language` into the live `SessionData` object on every property
access, take an explicit snapshot under the parser lock. The parser lock is already held by
`GetSessionAndReconnectIfNeeded` and by `ValidateAndReconnect` (which holds `_reconnectLock`).

Option A — Snapshot method:

```csharp
internal SessionData SnapshotSessionData()
{
    if (_currentSessionData == null)
        return null;

    // Write transient fields under caller's lock
    _currentSessionData._database = CurrentDatabase;
    _currentSessionData._language = _currentLanguage;
    return _currentSessionData;
}
```

Replace the `CurrentSessionData` property getter with a plain auto-property (or read-only field) and
call `SnapshotSessionData()` from `ValidateAndReconnect` where the lock is already held.

Option B — Copy-on-read:

Clone the `SessionData` at snapshot time so that any later mutations (e.g. ENV_CHANGE arriving on a
different thread) don't affect the recovery data. This is safer but allocates.

### Files to change

| File | Change |
| ---- | ------ |
| `SqlConnectionInternal.cs` | Replace `CurrentSessionData` getter; add `SnapshotSessionData()` |
| `SqlConnection.cs` | Call `SnapshotSessionData()` in `ValidateAndReconnect` instead of reading property |

### Tests

- Hard to unit-test race conditions directly. Add a stress test that repeatedly executes `USE [db]`
  and forces reconnections concurrently, then asserts `CurrentDatabase` is correct after each
  reconnection.

---

## Fix 3 — Document `USE [db]` vs `ChangeDatabase` resilience difference (Issue F)

### Goal

Help users understand that `USE [db]` via `SqlCommand` has the same resilience as `ChangeDatabase()`
when session recovery is supported, but **neither** survives a full connection failure that doesn't
support session recovery. The recommended pattern is to check `SqlConnection.Database` after any
retry.

### Approach

- Add a remark in the XML doc for `SqlConnection.ChangeDatabase` noting that both `ChangeDatabase()`
  and `USE [db]` are tracked by the driver's session recovery mechanism.
- Add a remark noting that if the application catches a `SqlException` and retries, it should
  re-establish the desired database context because the retry may land on a fresh connection.
- Add a doc sample showing the resilient pattern.

### Files to change

| File | Change |
| ---- | ------ |
| `doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml` | Add resilience remarks to `ChangeDatabase` entry |
| `doc/samples/` | Add a sample demonstrating database context verification after retry |

---

## Fix 4 (Optional) — Warn when `USE [db]` is detected without session recovery (Issue F)

### Goal

Surface a diagnostic event when the driver detects that `CurrentDatabase` changed via ENV_CHANGE but
session recovery is not enabled. This helps users understand they are at risk of context loss.

### Approach

In `OnEnvChange`, when `ENV_DATABASE` is received and `_fConnectionOpen == true` (i.e. a runtime
database change, not login), check `_sessionRecoveryAcknowledged`. If `false`, emit an `EventSource`
trace at Warning level.

### Files to change

| File | Change |
| ---- | ------ |
| `SqlConnectionInternal.cs` | Add warning trace in `OnEnvChange` case `ENV_DATABASE` |

### Risk

Low — trace-only, no behaviour change.

---

## Non-recommendations

### Do not update `_originalDatabase` after `USE [db]`

It is tempting to update `_originalDatabase` when `ENV_DATABASE` arrives on an open connection, so
that a subsequent pool reset keeps the `USE`d database. However, this would break pool isolation: a
connection returned to the pool would not reset to the initial catalog, leaking database context to
the next consumer. The current behaviour (freeze `_originalDatabase` at login time) is correct.

### Do not attempt recovery without server support

If the server does not acknowledge `FEATUREEXT_SRECOVERY`, the driver cannot reconstruct the
session. Attempting a client-side `USE [db]` after reconnection would be fragile (the database might
not exist on a failover replica, the user might not have access, etc.). The correct approach is to
surface the error and let the application decide.

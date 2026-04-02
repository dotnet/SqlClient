# Reconnection Flows and Database Context Behaviour

Every code path that can create a new physical connection while the `SqlConnection` remains "open"
to the caller is listed below. For each flow, the analysis records whether database context is
preserved.

---

## Flow 1: Command-triggered transparent reconnect (sync)

**Entry**: `SqlCommand.RunExecuteNonQueryTds()` / `RunExecuteReaderTds()`

```text
1. ValidateAndReconnect(null, timeout)
2.   _sessionRecoveryAcknowledged? ── No  → return null → command proceeds on broken conn → SqlException
                                    │ Yes ↓
3.   ValidateSNIConnection()? ─── OK  → return null → command proceeds normally
                                │ broken ↓
4.   cData = tdsConn.CurrentSessionData   ← _database = CurrentDatabase (e.g. "MyDb")
5.   _recoverySessionData = cData
6.   DoomThisConnection()
7.   Task.Run(ReconnectAsync)
8.     ForceNewConnection = true
9.     OpenAsync → TryReplaceConnection → CreateConnection(recoverySessionData)
10.      new SqlConnectionInternal(recoverySessionData)
11.        Login: CurrentDatabase = InitialCatalog     ← temporarily wrong
12.        TdsLogin sends _recoverySessionData._database = "MyDb" in recovery packet
13.        Server restores session → ENV_CHANGE → CurrentDatabase = server's DB
14.      CompleteLogin:
14a.       recoveredDatabase = _recoverySessionData._database ("MyDb")
14b.       _recoverySessionData = null
14c.       if CurrentDatabase != recoveredDatabase:
14d.         → execute USE [MyDb] on the wire → server switches to MyDb
14e.         → ENV_CHANGE → CurrentDatabase = "MyDb"
15.  ReconnectAsync returns
16.  Command re-executes on new connection (CurrentDatabase = "MyDb")
```

**Database context preserved?** Yes — if session recovery is acknowledged by the server and
`_unrecoverableStatesCount == 0`. Even if the server does not properly restore the database
context, the fix in `CompleteLogin()` detects the mismatch and issues a `USE` command to force
alignment between client and server. See [Issue G in 03-issues.md](03-issues.md).

**Files**: `SqlCommand.NonQuery.cs` line 756, `SqlCommand.Reader.cs` line 1265, `SqlConnection.cs`
lines 1662–1830, `SqlConnectionInternal.cs` constructor.

---

## Flow 2: Command-triggered transparent reconnect (async)

Same as Flow 1 except reconnection completes asynchronously and the command execution is chained as
a continuation via `RunExecuteNonQueryTdsSetupReconnnectContinuation` /
`RunExecuteReaderTdsSetupReconnectContinuation`.

**Database context preserved?** Same as Flow 1.

---

## Flow 3: `RepairInnerConnection` path (`ChangeDatabase`, `EnlistTransaction`)

**Entry**: `SqlConnection.ChangeDatabase()`, `SqlConnection.EnlistTransaction()`

```text
1. RepairInnerConnection()
2.   WaitForPendingReconnection()
3.   tdsConn.GetSessionAndReconnectIfNeeded(this)
4.     _parserLock.Wait()
5.     ValidateAndReconnect(releaseLock, timeout)   ← same as Flow 1
6.     AsyncHelper.WaitForCompletion(reconnectTask)
7. InnerConnection.ChangeDatabase(database) / EnlistTransaction(tx)
```

**Database context preserved?** Yes — the reconnect restores the session, then the caller's database
change runs on the recovered connection. The `ChangeDatabase` call overwrites the context
immediately after, so even if recovery landed on the wrong database, the explicit change corrects
it.

**Files**: `SqlConnection.cs` lines 1330–1347, 1540–1590, 1847–1863.

---

## Flow 4: Pool recycle (`Deactivate` → `ResetConnection`)

**Entry**: `SqlConnection.Close()` → pool returns connection

```text
1. Deactivate()
2.   _parser.Deactivate()
3.   ResetConnection()
4.     PrepareResetConnection(preserveTransaction)   ← sets TDS flag
5.     CurrentDatabase = _originalDatabase            ← back to InitialCatalog
```

Next user to acquire this connection:

```text
6. First TDS packet piggybacks sp_reset_connection header flag
7. Server executes sp_reset_connection → resets to login database
8. Server sends ENV_SPRESETCONNECTIONACK → SessionData.Reset() (clears _database, _delta)
9. Server sends ENV_CHANGE(ENV_DATABASE) → CurrentDatabase = login database
```

**Database context preserved?** No — by design. Pool reset intentionally reverts to the initial
catalog. This is correct behaviour; a pooled connection must not leak database context to the next
consumer.

**Files**: `SqlConnectionInternal.cs` lines 2063–2122, 3883–3907.

---

## Flow 5: Session recovery not acknowledged (fallback)

**Condition**: `_sessionRecoveryAcknowledged == false` (server does not support session recovery, or
`ConnectRetryCount == 0`).

```text
1. ValidateAndReconnect()
2.   _connectRetryCount > 0? ── No  → return null
                               │ Yes ↓
3.   _sessionRecoveryAcknowledged? ── No → return null
```

The broken connection is **not** detected by `ValidateAndReconnect`. The next TDS write/read fails
with a `SqlException`. The user must handle the error themselves. If they retry by re-opening or
re-executing:

- Pooled: new connection from pool → initial catalog
- Non-pooled: new connection → initial catalog

**Database context preserved?** No. There is no mechanism to recover the database context when
session recovery is unsupported. The error surfaces to the caller, so this is not a silent data
corruption — the user explicitly sees a failure.

**Files**: `SqlConnection.cs` lines 1750–1755.

---

## Flow 6: Unrecoverable session state

**Condition**: `_sessionRecoveryAcknowledged == true` but `cData._unrecoverableStatesCount > 0`.

```text
1. ValidateAndReconnect()
2.   cData = tdsConn.CurrentSessionData
3.   cData._unrecoverableStatesCount > 0
4.   → OnError(SQL.CR_UnrecoverableServer)   ← throws SqlException
```

**Database context preserved?** No — but the error is explicit. The caller knows the connection is
broken.

**Files**: `SqlConnection.cs` lines 1823–1828.

---

## Flow 7: MARS with active sessions

**Condition**: Multiple Active Result Sets enabled, more than one active session.

```text
1. ValidateAndReconnect()
2.   _sessionPool.ActiveSessionsCount > 0
3.   → OnError(SQL.CR_UnrecoverableClient)   ← throws SqlException
```

**Database context preserved?** No — recovery is not possible with multiple active sessions. Error
is explicit.

**Files**: `SqlConnection.cs` lines 1761–1770.

---

## Flow 8: `ChannelDbConnectionPool` (V2 pool) reconnection

**Condition**: `UseConnectionPoolV2` AppContext switch is enabled.

```text
1. ValidateAndReconnect → ReconnectAsync → OpenAsync
2.   ForceNewConnection = true
3.   TryReplaceConnection → TryGetConnection
4.     connectionPool.ReplaceConnection(...)
5.       throw new NotImplementedException()   ← CRASH
```

**Database context preserved?** No — `ReplaceConnection` is not implemented. The reconnection fails
with `NotImplementedException`, which propagates as an unhandled error.

**Files**: `ChannelDbConnectionPool.cs` lines 173–177.

---

## Flow 9: Non-pooled connection reconnection

**Condition**: `Pooling=false` in connection string.

```text
1. ValidateAndReconnect → ReconnectAsync → OpenAsync
2.   ForceNewConnection = true
3.   TryReplaceConnection = TryOpenConnectionInternal
4.     connectionFactory.TryGetConnection
5.       pool == null → CreateNonPooledConnection
6.         SqlConnectionFactory.CreateConnection(recoverySessionData = null)  ← BUG?
```

Wait — let me verify this. `CreateNonPooledConnection` calls `CreateConnection` but for non-pooled
it goes through a different path. Let me check whether `recoverySessionData` is passed.

Looking at `SqlConnectionFactory.TryGetConnection` (`SqlConnectionFactory.cs` line 380–470):

```text
pool == null (non-pooled):
  → CreateNonPooledConnection(owningConnection, poolGroup, userOptions)
```

And `CreateNonPooledConnection` (`SqlConnectionFactory.cs` line 485):

```text
return CreateConnection(options, poolKey, null, null, owningConnection, userOptions)
```

In `CreateConnection` (`SqlConnectionFactory.cs` line 580):

```text
recoverySessionData = sqlOwningConnection._recoverySessionData   ← READ from SqlConnection
```

So `recoverySessionData` IS read from `sqlOwningConnection._recoverySessionData`, which was set by
`ValidateAndReconnect`. Non-pooled connections DO get recovery data.

**Database context preserved?** Yes — same mechanism as pooled connections. Recovery data is passed
through `sqlOwningConnection._recoverySessionData`.

**Files**: `SqlConnectionFactory.cs` lines 485, 580–608.

---

## Summary Table

| Flow | Trigger | Context Preserved? | Silent? | Notes |
| ---- | ------- | ------------------ | ------- | ----- |
| 1 | Command execution (sync) | Yes | Yes (transparent) | Requires session recovery support |
| 2 | Command execution (async) | Yes | Yes (transparent) | Same as 1 |
| 3 | `ChangeDatabase`/`EnlistTransaction` | Yes | Yes (transparent) | `RepairInnerConnection` runs first |
| 4 | Pool recycle | No (by design) | N/A | Pool reset is intentional |
| 5 | No session recovery support | No | No (SqlException) | Error surfaces to caller |
| 6 | Unrecoverable session state | No | No (SqlException) | Error surfaces to caller |
| 7 | MARS active sessions | No | No (SqlException) | Error surfaces to caller |
| 8 | V2 pool (`ChannelDbConnectionPool`) | No | No (NotImplementedException) | **Bug**: `ReplaceConnection` unimplemented |
| 9 | Non-pooled connection | Yes | Yes (transparent) | Recovery data passed correctly |

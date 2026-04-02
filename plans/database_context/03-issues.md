# Identified Issues

Seven issues were found during the analysis. They are ordered from highest to lowest severity
relative to the invariant: *internal reconnections must maintain the current database context*.

---

## Issue G — `CompleteLogin` does not verify database context after session recovery

**Severity**: High (silent data corruption — client and server on different databases)
**Conditions**: Session recovery succeeds but the server does not restore the database context
**Default impact**: After reconnection, `CurrentDatabase` reflects whatever the server sent in
`ENV_CHANGE` (the initial catalog), not the database the session was actually using before the
connection dropped. Subsequent queries silently execute against the wrong database.

### Description

During a reconnection with session recovery, the client sends the correct database in the recovery
feature request (`_recoverySessionData._database`). The server is supposed to restore the session to
that database and confirm via `ENV_CHANGE(ENV_DATABASE)`. However, `CompleteLogin()` never checks
whether the server actually honoured the recovery request. It unconditionally nulls
`_recoverySessionData` and trusts whatever `CurrentDatabase` was set to by the server's
`ENV_CHANGE`.

If the server fails to restore the database (sends the initial catalog in `ENV_CHANGE`, or omits the
database `ENV_CHANGE` entirely), the client silently ends up on the wrong database.

### Root cause

`CompleteLogin()` in `SqlConnectionInternal.cs` (line ~2315) nulls `_recoverySessionData` without
comparing the recovered database against `CurrentDatabase`. There is no corrective action when they
differ.

### Location

`SqlConnectionInternal.cs`, `CompleteLogin()` method — the block after encryption validation where
`_recoverySessionData = null` is set.

### Effect

After a transparent reconnection:
- `SqlConnection.Database` returns the initial catalog instead of the `USE`-switched database
- All subsequent queries execute against the wrong database
- The mismatch is completely silent — no exception, no warning

This is the root cause of [dotnet/SqlClient#4108](https://github.com/dotnet/SqlClient/issues/4108).

### Fix applied

See [04-recommendations.md](04-recommendations.md) Fix 1. In `CompleteLogin()`, after session
recovery is acknowledged and encryption is verified, the fix compares `CurrentDatabase` against the
recovered database from `_recoverySessionData`. If they differ, it issues a `USE [database]` command
to force the server to the correct database, ensuring both client and server agree.

---

## Issue A — `ChannelDbConnectionPool.ReplaceConnection` throws `NotImplementedException`

**Severity**: High (crash on reconnect)
**Conditions**: `UseConnectionPoolV2` AppContext switch enabled
**Default impact**: None (switch defaults to `false`)

### Description

`ChannelDbConnectionPool.ReplaceConnection()` is a stub that throws `NotImplementedException`. The
transparent reconnection flow calls `connectionPool.ReplaceConnection()` via `TryGetConnection` when
`ForceNewConnection == true`. With the V2 pool enabled, any reconnection attempt crashes instead of
recovering.

### Location

`ChannelDbConnectionPool.cs` lines 173–177:

```csharp
public DbConnectionInternal ReplaceConnection(
    DbConnection owningObject,
    DbConnectionOptions userOptions,
    DbConnectionInternal oldConnection)
{
    throw new NotImplementedException();
}
```

### Call chain

```text
ReconnectAsync
  → OpenAsync
    → TryOpenInner (ForceNewConnection == true)
      → TryReplaceConnection
        → TryOpenConnectionInternal
          → SqlConnectionFactory.TryGetConnection
            → connectionPool.ReplaceConnection(...)   ← throws
```

### Effect

Database context is lost. The `NotImplementedException` propagates out of `ReconnectAsync`, which
catches `SqlException` but not `NotImplementedException`, so it escapes as an unhandled failure. The
connection becomes unusable.

---

## Issue B — `CurrentSessionData` getter is not thread-safe

**Severity**: Medium (potential silent data corruption)
**Conditions**: Concurrent access to the connection from multiple threads (e.g. timer-based health
checks, or async continuations racing with foreground work)
**Default impact**: Low probability, but when hit, recovery may target the wrong database

### Description

The `CurrentSessionData` property writes `_database` and `_language` to the `SessionData` object on
every read:

```csharp
internal SessionData CurrentSessionData
{
    get
    {
        if (_currentSessionData != null)
        {
            _currentSessionData._database = CurrentDatabase;   // ← non-atomic write
            _currentSessionData._language = _currentLanguage;  // ← non-atomic write
        }
        return _currentSessionData;
    }
}
```

`SqlConnectionInternal.cs` lines 530–537.

If `ValidateAndReconnect` calls `CurrentSessionData` at the same moment another thread modifies
`CurrentDatabase` (e.g. from a completed ENV_CHANGE callback), the snapshot may capture a
partially-updated state. For example, `_database` could be written as the old value while
`_language` gets the new value, or vice-versa.

### Effect

The session recovery feature request encodes a snapshot with inconsistent database/language. The
server may restore the session to the wrong database or wrong language.

---

## Issue C — `Login()` overwrites `CurrentDatabase` before session recovery completes

**Severity**: Low (self-correcting)
**Conditions**: Every reconnection
**Default impact**: Transient — corrected by server ENV_CHANGE response

### Description

In `Login()` at `SqlConnectionInternal.cs` line 2976:

```csharp
CurrentDatabase = server.ResolvedDatabaseName;
```

`ServerInfo.ResolvedDatabaseName` is always `ConnectionOptions.InitialCatalog`. During reconnection,
this overwrites `CurrentDatabase` from (for example) `"MyDb"` to `"master"` before the TDS login
packet is even sent.

The server processes the login, applies session recovery (including the database change back to
`"MyDb"`), and sends ENV_CHANGE tokens that correct `CurrentDatabase`. So the final state is
correct.

### Effect

Between the `Login()` call and the ENV_CHANGE response, `CurrentDatabase` is temporarily wrong. Any
code that reads `CurrentDatabase` in this window (diagnostic tracing, health monitoring, etc.) sees
the initial catalog instead of the recovered database. No functional impact on the TDS protocol
because the recovery feature request carries the correct `_database` value from the snapshot taken
*before* `Login()` was called.

---

## Issue D — `_originalDatabase` set to `_initialDatabase` on reconnect (not current)

**Severity**: Low (correct for pooling, surprising for non-pooled)
**Conditions**: Every reconnection with recovery data
**Default impact**: `ResetConnection()` after reconnect targets the original initial catalog, not
the USE'd database

### Description

In the `SqlConnectionInternal` constructor during reconnection:

```csharp
_originalDatabase = _recoverySessionData._initialDatabase;
```

`SqlConnectionInternal.cs` line 384.

`_originalDatabase` is the target for `ResetConnection()`. After reconnection, it is set to the
**first login's** initial database, not the database the session was recovered to.

For pooled connections this is correct: pool reset should return the connection to the initial
catalog regardless of any `USE` commands. For non-pooled connections, `ResetConnection` is not
called, so this is moot.

### Effect

None in practice — `_originalDatabase` is only consumed by `ResetConnection()`, which is only called
during pool deactivation.

---

## Issue E — `SessionData` copy constructor does not copy `_database`

**Severity**: Low (compensated by `CurrentSessionData` getter)
**Conditions**: Every reconnection

### Description

The `SessionData` copy constructor copies baselines but not transient state:

```csharp
public SessionData(SessionData recoveryData)
{
    _initialDatabase = recoveryData._initialDatabase;
    _initialCollation = recoveryData._initialCollation;
    _initialLanguage = recoveryData._initialLanguage;
    _resolvedAliases = recoveryData._resolvedAliases;
    // _database, _language, _collation are NOT copied
}
```

`SessionData.cs` lines 46–60.

This is not a bug because `_database` and `_language` are populated just-in-time by the
`CurrentSessionData` getter before the recovery data is read by
`WriteSessionRecoveryFeatureRequest`. The `CurrentSessionData` getter writes `_database =
CurrentDatabase` before returning. The copy constructor is only used to initialise the **new**
connection's `_currentSessionData`, not to read recovery values.

### Effect

None — by design. Noted here for completeness because the missing copy looks suspicious on first
read.

---

## Issue F — No database context recovery when session recovery is unsupported

**Severity**: Informational
**Conditions**: Server does not support `FEATUREEXT_SRECOVERY`, or `ConnectRetryCount == 0`
**Default impact**: Connection breaks surface as `SqlException`; no silent context loss

### Description

When `_sessionRecoveryAcknowledged == false`, `ValidateAndReconnect()` does not check
`ValidateSNIConnection()` and returns `null`. The broken connection is only detected when the next
TDS write/read fails. The error propagates to the caller as a `SqlException`.

If the caller has retry logic (whether application-level or via `SqlRetryLogicProvider`), the retry
acquires a new connection from the pool. That connection has been reset via `sp_reset_connection`
and is in the initial catalog, not the database the user `USE`d to.

This is not a bug in the driver — session recovery is the mechanism for preserving context, and
without it the driver cannot help. However, the behaviour is surprising for users who expect `USE
[db]` to be durable.

### Effect

Users who rely on `USE [db]` instead of `SqlConnection.ChangeDatabase()` may see their database
context lost after a transient failure + retry. The recommended mitigation is to use
`ChangeDatabase()` or to re-issue `USE [db]` after any error.

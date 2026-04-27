# SQL Server Source Analysis: Session Recovery and Database Context

## Purpose

This document examines the SQL Server engine's session recovery implementation (from the internal
`dotnet-sqlclient` repository indexed via Bluebird MCP) to identify how the server handles database
context during reconnection and where issue #4108 could manifest.

---

## Server-Side Session Recovery Flow

### 1. Parsing the Client's Recovery Request

**File**: `/sql/ntdbms/tds/src/featureext.cpp`

`CSessionRecoveryFeatureExtension::ParseFeatureData` (lines 1279–1432) handles the
`FEATUREEXT_SRECOVERY` data from the client's Login7 packet:

1. Creates two `CTdsSessionState` objects:
   - `m_pInitTdsSessionStateData` — baseline state from the initial login
   - `m_pTdsSessionStateDataToBe` — target state that the client wants restored

2. Parses the **initial chunk** into `m_pInitTdsSessionStateData`.

3. **Clones** the initial data into `m_pTdsSessionStateDataToBe`, then parses the **delta chunk**
   on top. The client only sends changed values; unchanged values inherit from the clone.

4. Marks the physical connection as "Recovered" via `m_pPhyConn->SetRecovered()`.

### 2. Parsing Each Data Chunk

**File**: `/sql/ntdbms/tds/src/featureext.cpp`

`CSessionRecoveryFeatureExtension::ParseSessionDataChunk` (lines 857–1279) deserializes each
`SessionRecoveryData` block. The TDS format is:

```
SessionRecoveryData = Length
                      RecoveryDatabase      (BYTE len + WCHAR data)
                      RecoveryCollation      (BYTE len + 0 or 5 bytes)
                      RecoveryLanguage       (BYTE len + WCHAR data)
                      SessionStateDataSet    (sequence of stateId + length + value)
```

Key handling for the database field:

```cpp
// Parse RecoveryDatabase, BYTE len + WCHAR data
if (0 != cchByteLen)
{
    pTdsSS->SetRecoverDb(
        reinterpret_cast<const WCHAR *>(pbCurr + sizeof(BYTE)), cchByteLen);
}
```

**Critical detail**: If the client sends a zero-length database in the delta (ToBe) chunk — meaning
"no change from initial" — the ToBe object retains the **cloned initial** value. The server will
then restore the session to the initial database, not the current one.

### 3. Database Determination During Recovery Login

**File**: `/sql/ntdbms/frontend/ods/login.cpp`

`LoginUseDbHelper::FDetermineSessionDb` (lines 6442–7066) determines the session's database using a
**3-source priority algorithm**:

| Priority | Source | Description |
|----------|--------|-------------|
| **Source #0** (highest) | Recovery feature extension | `pTdsSS->GetRecoverDb()` from the ToBe chunk |
| Source #1 | Login record | `pss->PchLoginDbName` (initial catalog from connection string) |
| Source #2 | User's `syslogins` row | Default database for the login |

For recovered connections (`pSess->FIsRecovered()`):

```cpp
if (pSess->FIsRecovered() && !pSess->FSDS())
{
    CTdsSessionState* pTdsSS = pSess->PTdsSessionState(fIsRedoLogin);
    BYTE cchLen = 0;
    pwchLoginDb = const_cast<WCHAR *>(pTdsSS->GetRecoverDb(cchLen));
    cbLoginDb = cchLen * sizeof(WCHAR);

    DBG_ASSERT(cbLoginDb > 0);  // recovery db can never be empty
```

Source #0 is **mandatory** for recovered sessions. If the database USE fails (database dropped,
offline, etc.), the connection fails entirely (outcome #7) rather than silently falling back to a
different database.

The server then compares the initial and ToBe databases, and if they differ, sets a
`fDbCollPendingUpdate` flag so the session's stored database/collation pair gets updated on first
use.

### 4. Other Session State Recovery

**File**: `/sql/ntdbms/frontend/execenv/session.cpp`

`CSession::FRecoverSessionStateFromTDS` (lines 1682–1737) recovers session state **excluding
database, collation, and language** — those are handled separately by `FDetermineSessionDb` and
`FDetermineSessionLang` during the login flow. This function restores:

- Identity insert settings
- User options (lock timeout, text size, date format, etc.)
- Context info buffer

### 5. Server Sends Back Session State

**File**: `/sql/ntdbms/tds/src/tdsimpl.cpp`

`CTds74::SendSessionStateDataImpl` (lines 7236–7452) serializes the session state snapshot back to
the client as a `FEATUREEXT_SESSIONRECOVERY` acknowledgment. The `WriteFeatureAck` function:

```cpp
bool CSessionRecoveryFeatureExtension::WriteFeatureAck(CNetConnection * pNetConn)
{
    // ...
    pCOut->SendSessionStateData(m_pTdsSessionStateDataToBe, true /*fFeatureAck*/);
    return true;
}
```

---

## How Issue #4108 Can Manifest

### Path 1: Client sends zero-length database delta (reference equality bug concern)

In the client's `WriteSessionRecoveryFeatureRequest` (TdsParser.cs, line 8963):

```csharp
currentLength += 1 + 2 * (reconnectData._initialDatabase == reconnectData._database
    ? 0
    : TdsParserStaticMethods.NullAwareStringLength(reconnectData._database));
```

If `_initialDatabase == _database` evaluates to `true`, the client sends a zero-length database in
the ToBe chunk. The server clones the Init chunk's database — which is correct only if the user
never switched databases. In C#, `string ==` performs value comparison (not reference), so this
comparison is correct in normal operation.

**However**, if `_database` is `null` (which happens after `SessionData.Reset()`), the comparison
is `_initialDatabase == null`. If `_initialDatabase` is non-null, the result is `false` and the
null database gets serialized — this would be a protocol error. The `CurrentSessionData` getter is
supposed to set `_database = CurrentDatabase` before the snapshot, which prevents this case. But
if the getter's write races with another operation (Issue B — thread safety), null could leak
through.

### Path 2: Server correctly restores DB but client ignores it

This is the **original root cause** of #4108. Even when the server properly restores the database
and sends `ENV_CHANGE(ENV_DATABASE)` with the correct database, the old `CompleteLogin()` code
simply set `_recoverySessionData = null` without checking whether `CurrentDatabase` matched the
recovery target. If the ENV_CHANGE was somehow missed, overwritten, or carried the wrong database,
the client silently diverged.

**The fix** (implemented in PR #4130) adds a post-recovery verification: if `CurrentDatabase`
differs from the recovery target, a `USE [db]` is issued to force alignment.

### Path 3: Double reconnection — stale `_initialDatabase`

After the first recovery:
1. `CompleteLogin()` sets `_currentSessionData._initialDatabase = CurrentDatabase`.
2. If the connection breaks again immediately, the new `_recoverySessionData._initialDatabase`
   is the **recovered** database (e.g. `"my_database"`), not the original `InitialCatalog`.
3. The client sends Init = `"my_database"` and ToBe = `"my_database"` (same) → zero-length delta.
4. The server clones Init and gets `"my_database"` in ToBe → correctly restores it.

This path **works correctly** because `_initialDatabase` is updated to `CurrentDatabase` after
each successful login. The delta is effectively "no change" and the server restores the correct
database.

### Path 4: Pool reset between USE and reconnection

If the connection is returned to the pool after `USE [db]`:
1. `ResetConnection()` sets `CurrentDatabase = _originalDatabase` (initial catalog).
2. Server executes `sp_reset_connection` → database context reverts.
3. `SessionData.Reset()` clears `_database` to `null`.
4. Next acquisition uses the connection from the pool, already on initial catalog.

If the connection breaks **after** pool reset but **before** the next user acquires it, the
recovery data has `_database = null` and `_initialDatabase = InitialCatalog`. The recovery
correctly restores to `InitialCatalog`. This is correct behavior — pool reset intentionally
discards the `USE` state.

### Path 5: MARS connections

With MARS enabled, `SqlConnection.ServerProcessId` returns 0, so `KILL {spid}` via that property
doesn't work. The manual tests account for this by using `SELECT @@SPID` instead. However, MARS
has an additional constraint: if multiple active result sets exist when the connection breaks,
`ValidateAndReconnect` throws `CR_UnrecoverableClient` — no silent recovery occurs.

If there is exactly one active result set (or none) and the connection breaks, MARS reconnection
follows the same path as non-MARS. The session data snapshot and recovery request include the
correct `_database` value.

---

## Test Coverage Analysis

| Scenario | Unit Test | Manual Test | Adequacy |
|----------|-----------|-------------|----------|
| Basic USE → reconnect → correct recovery | Yes | Yes | Good |
| ChangeDatabase → reconnect | Yes | Yes | Good |
| Server returns wrong DB (ENV_CHANGE) | Yes (3 behaviors) | N/A | Good (can't control real server) |
| Server omits ENV_CHANGE entirely | Yes | N/A | Good |
| Pooled connection | Yes | Yes | Good |
| MARS | No | Yes | Medium — no simulated server test |
| Multiple database switches | No | Yes | Medium — no simulated server test |
| Double kill/reconnection | No | Yes | Medium — complex timing |
| Stress loop (100 iterations) | No | Yes | Good for catching intermittent failures |
| CREATE TABLE after reconnect (proof of server context) | No | Yes | Best evidence test |
| Pooled + double kill | No | No | **Gap** |
| MARS + buggy server recovery | No | No | **Gap** |
| Async double kill | No | No | **Gap** |

### Gaps to Consider

1. **Pooled + double kill**: No test combines connection pooling with two consecutive connection
   kills. The pool's `ReplaceConnection` path may have different timing for `_initialDatabase`
   update.

2. **MARS + buggy server**: The unit test infrastructure supports MARS but no test combines MARS
   with `RecoveryDatabaseBehavior.SendInitialCatalog`. However, the fix in `CompleteLogin` is
   invoked identically for MARS and non-MARS connections, so the risk is low.

3. **Async double kill**: The async reconnection paths (`RunExecuteReaderTdsSetupReconnectContinuation`)
   differ from sync in continuation scheduling. A double kill during async reconnection exercises
   different code paths.

---

## Conclusion

The SQL Server engine correctly implements session recovery for database context when the client
sends the right data. The server uses a **mandatory priority** for Source #0 (recovery database)
that never silently falls back to a different database.

The root cause of #4108 is on the **client side**: `CompleteLogin()` did not verify whether the
server actually applied the recovered database context. The fix (post-recovery `USE` command) is
correct and handles all identified failure paths — buggy server response, missing ENV_CHANGE, and
edge cases — without adding overhead in the common case where the server behaves correctly.

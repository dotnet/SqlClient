# Architecture: Database Context Tracking and Session Recovery

## Key Data Structures

### `SqlConnectionInternal` fields

| Field | Type | Set during | Purpose |
| ----- | ---- | ---------- | ------- |
| `CurrentDatabase` | `string` | Login, ENV_CHANGE | The database the server considers active right now |
| `_originalDatabase` | `string` | Login, constructor | Reset target for pool recycling (`ResetConnection()` restores to this value) |
| `_currentSessionData` | `SessionData` | Constructor | Live session state, snapshotted before reconnection |
| `_recoverySessionData` | `SessionData` | Constructor (param) | Saved session from the broken connection, used to build the recovery login packet |
| `_fConnectionOpen` | `bool` | `CompleteLogin()` | Guards whether ENV_CHANGE updates `_originalDatabase` |
| `_sessionRecoveryAcknowledged` | `bool` | `OnFeatureExtAck()` | Whether the server supports session recovery |

### `SessionData` fields

| Field | Mutated by | Cleared by `Reset()` | Purpose |
| ----- | ---------- | -------------------- | ------- |
| `_initialDatabase` | `CompleteLogin()` (first login only) | No | Immutable baseline from the login the server confirmed |
| `_database` | `CurrentSessionData` getter | Yes (set to `null`) | Current database, written just-in-time before snapshot |
| `_initialLanguage` | `CompleteLogin()` | No | Immutable baseline language |
| `_language` | `CurrentSessionData` getter | Yes | Current language |
| `_initialCollation` | `CompleteLogin()` | No | Immutable baseline collation |
| `_collation` | `OnEnvChange()` | Yes | Current collation |
| `_delta[]` | `OnFeatureExtAck()`, `SQLSESSIONSTATE` token handler | Yes | Per-stateID session variable changes |
| `_initialState[]` | `OnFeatureExtAck()` (first login only) | No | Per-stateID session variable baselines |
| `_unrecoverableStatesCount` | `SQLSESSIONSTATE` token handler | Yes | Count of non-recoverable session states |

`Reset()` is called when `ENV_SPRESETCONNECTIONACK` arrives (server acknowledged
`sp_reset_connection`). It clears delta/current state but preserves the immutable baselines.

## How `CurrentDatabase` is set

### During login

```text
Login()                           → CurrentDatabase = server.ResolvedDatabaseName
                                    (= ConnectionOptions.InitialCatalog)
Server login response             → ENV_CHANGE(ENV_DATABASE) → CurrentDatabase = newValue
CompleteLogin()                   → _currentSessionData._initialDatabase = CurrentDatabase
                                    (only when _recoverySessionData == null, i.e. first login)
```

`SqlConnectionInternal.cs` line 2976—sets `CurrentDatabase` to `InitialCatalog` immediately. The
server then confirms (or overrides) via ENV_CHANGE before `CompleteLogin()` captures it.

### During normal operation

```text
USE [MyDb] via SqlCommand     → server response → ENV_CHANGE(ENV_DATABASE)
OnEnvChange()                 → CurrentDatabase = "MyDb"
                                 _originalDatabase NOT updated (guarded by _fConnectionOpen)
```

`SqlConnectionInternal.cs` lines 1155–1164. After the connection is open, `_originalDatabase` is frozen.

### During pool reset

```text
Deactivate() → ResetConnection()
  → _parser.PrepareResetConnection()     (sets TDS header flag for sp_reset_connection)
  → CurrentDatabase = _originalDatabase  (resets to initial catalog immediately)
```

`SqlConnectionInternal.cs` lines 3895–3907.

### `CurrentSessionData` getter (just-in-time snapshot)

```csharp
internal SessionData CurrentSessionData
{
    get
    {
        if (_currentSessionData != null)
        {
            _currentSessionData._database = CurrentDatabase;
            _currentSessionData._language = _currentLanguage;
        }
        return _currentSessionData;
    }
}
```

`SqlConnectionInternal.cs` lines 530–537. This is called by `ValidateAndReconnect()` right before
saving recovery data for reconnection.

## Session Recovery Protocol

When `ConnectRetryCount > 0` (default: **1**), the driver negotiates `FEATUREEXT_SRECOVERY` with the
server during login. On reconnection, `WriteSessionRecoveryFeatureRequest()` encodes:

1. **Initial state**: `_initialDatabase`, `_initialCollation`, `_initialLanguage`, `_initialState[]`
2. **Current deltas**: `_database` (if different from `_initialDatabase`), `_language`,
   `_collation`, `_delta[]`

The server uses the initial state + deltas to rebuild the session. If `_database !=
_initialDatabase`, the server switches to `_database` after login.

### `WriteSessionRecoveryFeatureRequest` — relevant excerpt

```text
TdsParser.cs line 8963:
  initialLength += ... _initialDatabase ...
TdsParser.cs line 8966:
  currentLength += ... (_initialDatabase == _database ? 0 : _database) ...
TdsParser.cs line 9017:
  WriteIdentifier(_database != _initialDatabase ? _database : null, ...)
```

When `_database` equals `_initialDatabase`, a zero-length identifier is written (meaning "no
change"). When they differ, the current database name is written and the server applies it.

## Flow: How `ValidateAndReconnect` triggers recovery

```text
SqlCommand.RunExecuteNonQueryTds()
  → SqlConnection.ValidateAndReconnect()
       check _connectRetryCount > 0
       check _sessionRecoveryAcknowledged
       check !stateObj.ValidateSNIConnection()        ← physical connection broken?
       SessionData cData = tdsConn.CurrentSessionData ← snapshot (writes _database = CurrentDatabase)
       _recoverySessionData = cData                   ← save for new connection
       tdsConn.DoomThisConnection()
       Task.Run → ReconnectAsync()
         → ForceNewConnection = true
         → OpenAsync()
           → TryReplaceConnection()
             → SqlConnectionFactory.CreateConnection()
               → new SqlConnectionInternal(..., recoverySessionData)
                   constructor: _originalDatabase = recoverySessionData._initialDatabase
                   Login()
                     → CurrentDatabase = InitialCatalog
                     → login.database = CurrentDatabase
                     → TdsLogin(..., _recoverySessionData, ...)
                       → WriteSessionRecoveryFeatureRequest(recoverySessionData, ...)
                   Server processes login + recovery → ENV_CHANGE(ENV_DATABASE) → CurrentDatabase updated
                   CompleteLogin()
                     → _recoverySessionData = null
```

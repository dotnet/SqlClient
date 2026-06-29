# Fix 4: Async TDS Login

**Priority:** Medium
**Complexity:** High
**Risk:** Medium

## Problem

`TdsParser.TdsLogin()` (line ~7100) sends the LOGIN7 packet and reads the response synchronously.
This includes:

- Building the login packet with credentials, application name, etc.
- Flushing the packet to the network
- Reading the login response (may include feature extension acks, envchange tokens)
- Processing SSPI/federated auth challenges (multi-roundtrip)

## Location

**File:** `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParser.cs`

- `TdsLogin()` (line ~7100)
- `SqlInternalConnectionTds.AttemptOneLogin()` (line ~2177 in SqlConnectionInternal.cs)
- `SqlInternalConnectionTds.LoginNoFailover()` (line ~3138)

## Changes Required

### 1. Create TdsLoginAsync

```csharp
internal async ValueTask TdsLoginAsync(
    TdsLogin rec,
    TdsParserStateObject stateObj,
    CancellationToken cancellationToken)
{
    // Build login packet (in-memory — same as sync)
    WriteLogin7Packet(rec, stateObj);

    // Async flush
    await stateObj.WritePacketAsync(TdsEnums.HARDFLUSH, cancellationToken)
        .ConfigureAwait(false);

    // Async response processing
    await ProcessLoginResponseAsync(stateObj, cancellationToken)
        .ConfigureAwait(false);
}
```

### 2. Async Login Response Processing

The login response can include multiple TDS tokens:

- LOGINACK — success
- ENVCHANGE — database/language/packet size changes
- FEATUREEXTACK — feature extension acknowledgments
- ERROR — login failure
- SSPI challenge — requires sending another token

The response processing uses `TryRun()` which already supports async via the snapshot mechanism, so
this may already work with the existing async infrastructure.

### 3. Async AttemptOneLogin / LoginNoFailover

These methods in `SqlInternalConnectionTds` wrap `TdsLogin()` and need async variants:

```csharp
private async ValueTask AttemptOneLoginAsync(
    ServerInfo serverInfo, ..., CancellationToken cancellationToken)
{
    // Create physical connection (async from Fix 1-3)
    await _parser.ConnectAsync(serverInfo, this, ..., cancellationToken)
        .ConfigureAwait(false);

    // Login (async)
    await _parser.TdsLoginAsync(rec, _parser._physicalStateObj, cancellationToken)
        .ConfigureAwait(false);
}
```

## Testing

- Integration test: Full async login to SQL Server
- Test all authentication modes: SQL auth, Windows auth, Entra ID
- Test SSPI negotiation (may be multi-roundtrip)
- Test login failure handling (bad password, permission denied)
- Test connection routing/redirect scenarios

## Risk

- Medium — login is complex with many code paths (auth modes, SSPI, federated auth, feature
  negotiation). Each needs an async variant.

- SSPI challenges involve native interop which may be inherently synchronous.

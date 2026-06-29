# Fix 3: Async Prelogin Handshake

**Priority:** Medium
**Complexity:** Medium
**Risk:** Medium

## Problem

After the SNI connection and SSL setup, `TdsParser.Connect()` performs a prelogin handshake:

1. `SendPreLoginHandshake()` — sends the PRELOGIN packet
2. `ConsumePreLoginHandshake()` — reads and processes the server's response

Both are synchronous, blocking the thread during the network roundtrip.

## Location

**File:** `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParser.cs`

- `Connect()` method (line ~160)
- `SendPreLoginHandshake()`
- `ConsumePreLoginHandshake()`

## Changes Required

### 1. Async Prelogin Send

The prelogin packet is assembled in memory then flushed. The send is relatively simple — write to
the outbound buffer then flush:

```csharp
private async ValueTask SendPreLoginHandshakeAsync(
    TdsParserStateObject stateObj,
    CancellationToken cancellationToken)
{
    // Assemble prelogin packet in _outBuff (same as sync — in-memory)
    BuildPreLoginPayload(stateObj);

    // Async flush
    await stateObj.WritePacketAsync(TdsEnums.HARDFLUSH, cancellationToken)
        .ConfigureAwait(false);
}
```

### 2. Async Prelogin Consume

Reading the prelogin response requires reading from the network:

```csharp
private async ValueTask ConsumePreLoginHandshakeAsync(
    TdsParserStateObject stateObj,
    CancellationToken cancellationToken)
{
    // Read prelogin response packet
    await stateObj.ReadNetworkPacketAsync(cancellationToken)
        .ConfigureAwait(false);

    // Parse response (in-memory — same as sync)
    ParsePreLoginResponse(stateObj, ...);
}
```

### 3. Require `ReadNetworkPacketAsync` / `WritePacketAsync`

These state object methods may need to be created or verified. The `TdsParserStateObject` has
`TryReadNetworkPacket()` which returns `NeedMoreData` — the async wrapper should await the
underlying SNI read completion.

## Testing

- Integration test: Prelogin handshake completes asynchronously
- Test TDS version negotiation works correctly
- Test encryption negotiation (Optional, Mandatory, Strict)
- Verify MARS capability detection works

## Risk

- Medium — the prelogin handshake involves protocol negotiation. Errors here prevent any connection
  from being established.

- The sync path must be preserved for `Open()` callers.

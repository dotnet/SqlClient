# Fix 3: Modernize MARS Send Flow Control

**Priority:** Medium — directly addresses #422 (MARS slow on Linux)
**Complexity:** High
**Risk:** High

## Problem

`SniMarsHandle.Send()` (line ~161) uses a spin-wait loop with `lock(this)` and
`ManualResetEventSlim._ackEvent` for MARS send flow control:

```csharp
while (true)
{
    lock (this)                          // Blocks
    {
        if (_sequenceNumber < _sendHighwater)
            break;
    }
    _ackEvent.Wait();                     // Blocks waiting for ACK
    lock (this)                          // Blocks again
    {
        _ackEvent.Reset();
    }
}
lock (this)                              // Third lock
{
    muxedPacket = SetPacketSMUXHeader(packet);
}
return _connection.Send(muxedPacket);     // May block on TCP send lock
```

This has 3-4 lock acquisitions per send, plus a blocking wait on ACK events. Under MARS with
multiple sessions, this creates severe thread pool contention.

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs`

- `Send()` (line ~161)
- `InternalSendAsync()` (line ~169)
- `_ackEvent` ManualResetEventSlim

## Changes Required

### 1. Replace ManualResetEventSlim with SemaphoreSlim

```csharp
private readonly SemaphoreSlim _sendCredit =
    new SemaphoreSlim(initialSendHighwater, maxSendHighwater);

public override async ValueTask<uint> SendAsync(SniPacket packet,
    CancellationToken cancellationToken)
{
    // Wait for send credit (non-blocking for async)
    await _sendCredit.WaitAsync(cancellationToken).ConfigureAwait(false);

    SniPacket muxedPacket;
    lock (this) // Still need atomicity for sequence number + header
    {
        muxedPacket = SetPacketSMUXHeader(packet);
    }

    return await _connection.SendAsync(muxedPacket, cancellationToken)
        .ConfigureAwait(false);
}
```

### 2. Release Credits on ACK

When the server sends an ACK (increasing the send highwater):

```csharp
internal void HandleAck(uint newHighwater)
{
    int newCredits = (int)(newHighwater - _sendHighwater);
    _sendHighwater = newHighwater;
    _sendCredit.Release(newCredits);
}
```

### 3. Keep Sync Path for Sync Callers

```csharp
public override uint Send(SniPacket packet)
{
    _sendCredit.Wait(); // Sync wait — still blocks, but cleaner
    // ... same as above but sync
}
```

## Cautionary Note

PR #1357 was a more comprehensive MARS rewrite by Wraith2 that was merged then reverted. The issues
were not fully documented. Any changes to MARS must be incremental and heavily tested.

## Testing

- MARS integration test: Multiple concurrent readers on same connection
- Benchmark: MARS performance on Linux — compare to pre-change baseline
- Test ACK flow control works correctly
- Test high-watermark adjustment works correctly
- Stress test: Many sessions on one physical connection

## Risk

- High — MARS flow control is critical for correctness. Incorrect sequence numbering causes protocol
  violations.

- The reverted PR #1357 is evidence that this area is fragile.
- Recommend incremental changes with extensive testing at each step.

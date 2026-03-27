# Fix 1: Replace Monitor.Enter with SemaphoreSlim in SniTcpHandle

**Priority:** Medium
**Complexity:** Medium
**Risk:** Medium

## Problem

`SniTcpHandle.Send()` (line ~816) uses dual blocking locks:

```csharp
Monitor.Enter(this);        // Blocks thread pool thread
lock (_sendSync)             // Nested blocking lock
{
    packet.WriteToStream(_stream);
}
Monitor.Exit(this);
```

For sync callers, this is fine. But when async callers (via `SendAsync`) need to send a packet and
the lock is held, the thread pool thread blocks entirely. Under high throughput, this contributes to
thread pool starvation.

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs`

- `Send()` (line ~816)
- `_sendSync` object (line ~150)
- Out-of-band handling with `Monitor.TryEnter`

## Changes Required

### 1. Replace with SemaphoreSlim

```csharp
private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

public override uint Send(SniPacket packet)
{
    _sendLock.Wait(); // Still sync for sync callers
    try
    {
        packet.WriteToStream(_stream);
        return TdsEnums.SNI_SUCCESS;
    }
    catch (Exception ex)
    {
        return ReportTcpSNIError(ex);
    }
    finally
    {
        _sendLock.Release();
    }
}

public override async ValueTask<uint> SendAsync(SniPacket packet,
    CancellationToken cancellationToken)
{
    await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        await packet.WriteToStreamAsync(_stream, cancellationToken)
            .ConfigureAwait(false);
        return TdsEnums.SNI_SUCCESS;
    }
    catch (Exception ex)
    {
        return ReportTcpSNIError(ex);
    }
    finally
    {
        _sendLock.Release();
    }
}
```

### 2. Handle Out-of-Band (Attention) Packets

Attention packets currently use `Monitor.TryEnter` to attempt a non-blocking send. With
`SemaphoreSlim`, use a zero-timeout wait:

```csharp
if (packet.IsOutOfBand)
{
    if (!_sendLock.Wait(0)) // Non-blocking attempt
    {
        // Can't send now — queue for later or skip
        return TdsEnums.SNI_QUEUE_FULL;
    }
}
else
{
    _sendLock.Wait();
}
```

### 3. Add Read Lock (Separate)

Add a separate `SemaphoreSlim` for reads:

```csharp
private readonly SemaphoreSlim _receiveLock = new SemaphoreSlim(1, 1);
```

### 4. Remove Stream-Level ConcurrentQueueSemaphore

If the connection-level lock is sufficient, the stream-level `ConcurrentQueueSemaphore` in
`SniSslStream`/`SniNetworkStream` becomes redundant. Remove it to eliminate the double-locking
overhead.

**Caution:** Verify that no code path bypasses the connection-level lock
to access the stream directly.

## Testing

- Integration test: Basic send/receive works with new locking
- Concurrency test: Multiple concurrent sends are serialized correctly
- Attention test: Out-of-band packets still interrupt correctly
- MARS test: Multiple sessions sharing a physical connection work correctly
- Thread pool test: Async sends don't block thread pool threads

## Risk

- Medium — changing locking strategy can introduce subtle race conditions
- The interaction with MARS multiplexing must be carefully validated
- PR #1357 (reverted MARS rewrite) is a cautionary example
- Start with non-MARS TCP connections only, extend to MARS later

# Fix 4: Replace MARS Receive Queue Locking

**Priority:** Low-Medium
**Complexity:** Medium
**Risk:** Medium

## Problem

`SniMarsHandle.ReceiveAsync()` (line ~297) uses `lock(_receivedPacketQueue)` to protect a packet
queue. The `SniMarsConnection` demuxer also locks on the same or related objects (8
`lock(DemuxerSync)` statements).

## Location

**Files:**

- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ManagedSni/SniMarsHandle.netcore.cs`
  - `ReceiveAsync()` (line ~297) — `lock(_receivedPacketQueue)`
  - Packet enqueue (line ~348)
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ManagedSni/SniMarsConnection.netcore.cs`
  - `DemuxerSync` lock object (8 occurrences at line 62+)

## Changes Required

### 1. Replace Queue Lock with ConcurrentQueue

The `_receivedPacketQueue` can be replaced with `ConcurrentQueue<SniPacket>`, eliminating the `lock`
entirely:

```csharp
// Current:
private readonly Queue<SniPacket> _receivedPacketQueue = new();

// Proposed:
private readonly ConcurrentQueue<SniPacket> _receivedPacketQueue = new();
```

### 2. Use AsyncAutoResetEvent for Notification

When a packet is enqueued by the demuxer, signal the waiting session:

```csharp
private readonly SemaphoreSlim _packetAvailable = new SemaphoreSlim(0);

// Demuxer enqueues packet:
_receivedPacketQueue.Enqueue(packet);
_packetAvailable.Release();

// Session receives:
await _packetAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
_receivedPacketQueue.TryDequeue(out var packet);
```

### 3. Demuxer Lock Reduction

The `DemuxerSync` lock in `SniMarsConnection` protects the demultiplexing of incoming packets to
their target MARS sessions. This is inherently single-threaded (one physical connection, one
reader). If the demuxer runs on a dedicated async loop, the lock may be unnecessary.

Evaluate whether the demuxer can be restructured as a single-reader `Channel` consumer, eliminating
locks.

## Testing

- MARS integration test: Multiple concurrent sessions receive packets correctly
- Test packet ordering is preserved per session
- Stress test: High-throughput MARS with many sessions

## Risk

- Medium — MARS packet ordering is critical. Incorrect demultiplexing causes data corruption.

- The `ConcurrentQueue` + `SemaphoreSlim` pattern is well-established but must be carefully
  validated against the MARS protocol requirements.

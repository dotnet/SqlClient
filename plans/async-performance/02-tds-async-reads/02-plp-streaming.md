# Fix 2: Eliminate Snapshot Replay for PLP Reads

**Priority:** High — PLP reads are the worst-case scenario (#593)
**Complexity:** High
**Risk:** Medium-High

## Problem

Reading a `VARBINARY(MAX)` or `NVARCHAR(MAX)` column that spans many packets triggers the
snapshot/replay mechanism repeatedly. For 10MB of data with 8KB packets, that's ~1,280 packets, each
requiring a full replay from the start.

The existing `TryReadPlpBytes()` (line 2343) does have some optimization via `SetSnapshotStorage()`
/ `TryTakeSnapshotStorage()` — it stores the partially-read buffer in the snapshot so it doesn't
re-allocate on replay. But the replay still re-reads all previously processed packet data.

## Location

**File:** `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParserStateObject.cs`

- `TryReadPlpBytes()` (line 2343)
- `AddSnapshotDataSize()` / `GetPacketDataOffset()` — byte tracking (line 4488+)

## Changes Required

### Approach: Offset-Based Resume for PLP Reads

Instead of replaying from the start, use the existing `RunningDataSize` tracking in `PacketData` to
skip already-processed data:

1. **On first PLP yield:** Record the total bytes successfully consumed (`GetSnapshotTotalSize()`
   already tracks this)

2. **On replay:** Instead of re-reading from packet 1, use `GetPacketDataOffset()` to locate the
   packet containing the resume position, then skip forward within that packet to the exact byte
   offset.

3. **Implementation in `TryReadPlpBytes()`:**

   ```csharp
   // At the start of TryReadPlpBytes, check for resume:
   if (_snapshot != null && _snapshot.CanContinue)
   {
       int alreadyRead = _snapshot.GetSnapshotTotalSize();
       // Skip 'alreadyRead' bytes — they're already in the output buffer
       // (retrieved via TryTakeSnapshotStorage)
       // Start reading from the current packet position
   }
   ```

4. **The key insight:** PLP data is append-only. We never need to "re-interpret" already-read bytes
   — we just need to copy them from packets to the output buffer. On resume, we can skip the copy
   for already-processed data.

### What Already Exists

The codebase already has building blocks for this:

- `PacketData.RunningDataSize` — cumulative byte offset per packet
- `GetPacketDataOffset()` — fast lookup of position within packet chain
- `SetSnapshotStorage()` — stores the partially-filled output buffer
- `AddSnapshotDataSize()` — tracks how many bytes have been consumed

The gap is that `TryReadPlpBytes()` doesn't use these to skip work on replay — it still processes
all packets from the start.

## Testing

- Benchmark: 10MB VARBINARY(MAX) async read — target should be close to sync perf
- Benchmark: 20MB, 50MB — verify linear (not quadratic) scaling
- Unit test: PLP read across 1, 2, 5, 100 packets produces correct output
- Memory test: Verify reduced allocation (no re-reading of processed packets)

## Risk

- Medium-High — PLP reading has complex edge cases (null chunks, partial chunks at packet
  boundaries, chunk terminator detection)

- The `UseCompatibilityAsyncBehaviour` switch (line 2380) provides a fallback to the old path if the
  new logic has issues

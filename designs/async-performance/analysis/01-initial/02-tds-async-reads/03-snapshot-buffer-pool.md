# Fix 3: Pool Snapshot Packet Buffers

**Priority:** Medium — reduces GC pressure in async reads
**Complexity:** Low
**Risk:** Low

## Problem

Each `PacketData` node in the snapshot chain stores a `byte[]` buffer. These buffers are allocated
with `new byte[]` and never returned to a pool. For a 10MB async read with 8KB packets, that's
~1,280 buffer allocations totaling ~10MB — none of which are pooled. The GC impact of this is
severe: issue #593 reports 13GB allocated for a 10MB read.

## Location

**File:** `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParserStateObject.cs`

- `StateSnapshot.PacketData` class (line 4668)
- `StateSnapshot.AppendPacketData()` (line 5056)

## Changes Required

### 1. Use ArrayPool for PacketData Buffers

In `AppendPacketData()`, rent from `ArrayPool<byte>.Shared` instead of `new byte[]`:

```csharp
// Current:
_data.Buffer = new byte[read];
Buffer.BlockCopy(buffer, 0, _data.Buffer, 0, read);

// Proposed:
_data.Buffer = ArrayPool<byte>.Shared.Rent(read);
_data.BufferLength = read; // Track actual length since rented may be larger
Buffer.BlockCopy(buffer, 0, _data.Buffer, 0, read);
```

### 2. Return Buffers on Snapshot Cleanup

When the snapshot is cleared (after a successful complete read or on error), return all rented
buffers:

```csharp
internal void Clear()
{
    var current = _head;
    while (current != null)
    {
        if (current.Buffer != null)
        {
            ArrayPool<byte>.Shared.Return(current.Buffer);
            current.Buffer = null;
        }
        current = current.NextPacket;
    }
}
```

### 3. Update Cached Snapshot Reuse

The `_cachedSnapshot` pattern already reuses `StateSnapshot` objects. Ensure buffer lifecycle is
compatible — buffers must be returned before the snapshot is reused for a new operation.

## Testing

- Benchmark: 10MB async read — measure Gen0/Gen1/Gen2 GC counts before and after
- Unit test: Snapshot create → use → clear → verify no buffer leaks
- Memory profiler: Verify ArrayPool is returning buffers correctly

## Risk

- Low — ArrayPool is already used elsewhere in the codebase (TdsParser.cs lines 1426, 3560, 12962).
  The pattern is well-established.

- Care needed with `clearArray: true` if buffers may contain sensitive data (connection strings,
  encrypted values).

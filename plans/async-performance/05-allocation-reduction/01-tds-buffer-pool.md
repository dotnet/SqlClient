# Fix 1: Pool TDS Packet Buffers (inBuff/outBuff)

**Priority:** Medium
**Complexity:** Low
**Risk:** Low

## Problem

`TdsParserStateObject` allocates `_inBuff` and `_outBuff` with `new byte[size]` (lines ~1433, 1581).
These are per-connection allocations that are never pooled. With default packet size of 8000 bytes,
every connection allocates 16KB.

During connection negotiation, the packet size may change (server can negotiate a different size),
causing a buffer reallocation with temporary copy.

## Location

**File:** `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParserStateObject.cs`

- `_inBuff` allocation (line ~1433)
- `_outBuff` allocation (line ~1581)
- Buffer resize during negotiation (line ~1560)

## Changes Required

### 1. Rent Buffers from ArrayPool

```csharp
// Current:
_inBuff = new byte[size];

// Proposed:
_inBuff = ArrayPool<byte>.Shared.Rent(size);
_inBuffActualSize = size; // Track actual usable size (rented may be larger)
```

### 2. Return Buffers on Dispose

```csharp
protected override void Dispose(bool disposing)
{
    if (_inBuff != null)
    {
        ArrayPool<byte>.Shared.Return(_inBuff, clearArray: true);
        _inBuff = null;
    }
    if (_outBuff != null)
    {
        ArrayPool<byte>.Shared.Return(_outBuff, clearArray: true);
        _outBuff = null;
    }
    base.Dispose(disposing);
}
```

### 3. Handle Buffer Resize

During packet size negotiation, return the old buffer and rent a new one:

```csharp
// In buffer resize logic:
var oldBuff = _inBuff;
_inBuff = ArrayPool<byte>.Shared.Rent(newSize);
Buffer.BlockCopy(oldBuff, 0, _inBuff, 0, dataLength);
ArrayPool<byte>.Shared.Return(oldBuff, clearArray: true);
```

## Notes

- Use `clearArray: true` because buffers may contain sensitive data (credentials, encrypted
  payloads)

- `ArrayPool.Rent` may return a buffer larger than requested — all code that uses `_inBuff.Length`
  must be audited to use the tracked actual size instead

## Testing

- Unit test: Buffer allocation and deallocation work correctly
- Memory profiler: Verify reduced Gen0 allocations during connection churn
- Stress test: Rapid open/close cycles don't leak pooled buffers

## Risk

- Low — ArrayPool is already used elsewhere in the codebase
- Must audit all references to `_inBuff.Length` / `_outBuff.Length`

# Fix 2: Pool Snapshot Packet Data Buffers

**Priority:** Medium — directly impacts async large-data read performance
**Complexity:** Low
**Risk:** Low

## Problem

Same as P2/Fix 3 (02-tds-async-reads/03-snapshot-buffer-pool.md). The `StateSnapshot.PacketData`
linked list allocates a `new byte[]` for every captured packet. For a 10MB async read with 8KB
packets, that's ~1,280 allocations totaling 10MB — contributing to the 13GB total allocation
reported in issue #593.

## Location

**File:** `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParserStateObject.cs`

- `StateSnapshot.PacketData` (line ~4668)
- `StateSnapshot.AppendPacketData()` (line ~5056)

## Changes Required

See
[02-tds-async-reads/03-snapshot-buffer-pool.md](../02-tds-async-reads/03-snapshot-buffer-pool.md)
for detailed implementation. Summary:

1. Use `ArrayPool<byte>.Shared.Rent()` in `AppendPacketData()`
2. Return all rented buffers in `StateSnapshot.Clear()`
3. Handle cached snapshot reuse correctly

## Note

This fix is cross-referenced here because it fits both the allocation reduction category and the TDS
async reads category. It should be implemented once, not twice.

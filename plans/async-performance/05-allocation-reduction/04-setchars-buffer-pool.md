# Fix 4: Pool Char Buffers in SetChars_FromReader

**Priority:** Low
**Complexity:** Low
**Risk:** Low

## Problem

`SetChars_FromReader()` in `ValueUtilsSmi.cs` (line 2402) allocates a `new char[chunkSize]`
(typically 8192 chars = 16KB) for each column read during TVP processing. When bulk-inserting
structured types with multiple character columns, this creates many transient allocations.

Discussion #3918 identified this as an optimization opportunity.

## Location

**File:** `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/Server/ValueUtilsSmi.cs`
**Method:** `SetChars_FromReader()` (line ~2402, allocation at line ~2415)

## Changes Required

```csharp
// Current:
char[] buffer = new char[chunkSize];

// Proposed:
char[] buffer = ArrayPool<char>.Shared.Rent(chunkSize);
try
{
    // ... existing reading loop ...
}
finally
{
    ArrayPool<char>.Shared.Return(buffer);
}
```

### Similar Pattern for SetBytes_FromReader

If a `SetBytes_FromReader` equivalent exists with similar `new byte[]` allocation, apply the same
fix.

## Testing

- Unit test: TVP with large character columns produces correct data
- Memory profiler: Verify reduced char[] allocations during TVP operations
- Integration test: Bulk insert with TVPs works correctly

## Risk

- Low — straightforward ArrayPool usage in a self-contained method
- Buffer contents don't contain sensitive data (column read values), so `clearArray: false` is
  acceptable

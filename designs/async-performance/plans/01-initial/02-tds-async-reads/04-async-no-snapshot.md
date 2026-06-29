# Fix 4: Async-Without-Snapshot for Sequential Access

**Priority:** High — provides a workaround path for large data reads
**Complexity:** Medium
**Risk:** Medium

## Problem

The `_asyncReadWithoutSnapshot` field (line 263) represents an alternative async mode that skips the
snapshot mechanism entirely. The assertion at line 3504 enforces that exactly one of `_snapshot` or
`_asyncReadWithoutSnapshot` is active:

```csharp
Debug.Assert((_snapshot != null) ^ _asyncReadWithoutSnapshot);
```

This mode is designed for streaming/sequential access patterns where the caller reads data
forward-only and doesn't need the ability to replay. Currently it appears to be partially wired up
but its usage scope is limited.

## Location

**Files:**

- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParserStateObject.cs`
  - `_asyncReadWithoutSnapshot` field (line 263)
  - `TryReadNetworkPacket()` (line 3400) — branch on snapshot vs no-snapshot
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlDataReader.cs`
  - Lines ~5795-5835 — sets `_asyncReadWithoutSnapshot`
  - Sequential access mode handling

## Changes Required

### 1. Audit Current No-Snapshot Usage

Determine exactly when `_asyncReadWithoutSnapshot` is enabled:

- Is it used with `CommandBehavior.SequentialAccess`?
- What operations support it?
- What operations still require snapshot?

### 2. Enable No-Snapshot Mode for Common Patterns

For `CommandBehavior.SequentialAccess` + async reads:

- `GetFieldValueAsync<T>()` — should use no-snapshot for forward-only streaming
- `GetStream()` / `GetTextReader()` — already designed for streaming
- `ReadAsync()` — row advancement should work without replay
- `IsDBNullAsync()` — simple check, no replay needed

### 3. Document the SequentialAccess Pattern

Even without code changes, `CommandBehavior.SequentialAccess` combined with streaming APIs
(`GetStream()`, `GetTextReader()`) may already avoid the worst of the snapshot replay problem.
Create documentation showing users how to use this pattern for large data:

```csharp
// Recommended pattern for large async reads
using var reader = await cmd.ExecuteReaderAsync(
    CommandBehavior.SequentialAccess);
while (await reader.ReadAsync())
{
    using var stream = reader.GetStream(0);
    await stream.CopyToAsync(destination);
}
```

### 4. Ensure Correctness of No-Snapshot Path

The no-snapshot path must handle:

- Packet boundaries (data split across packets)
- Partial reads (not enough data in current packet)
- Error recovery (connection loss mid-read)

Without snapshots, the parser cannot replay on error — it must fail fast.

## Testing

- Benchmark: 10MB VARBINARY(MAX) with SequentialAccess+GetStream — compare to default mode

- Unit test: SequentialAccess async read produces correct data
- Unit test: Verify no snapshot allocations in no-snapshot mode
- Regression: Non-SequentialAccess reads still work correctly

## Risk

- Medium — removing snapshot capability means errors during reads are not recoverable by replay.
  This is the correct trade-off for streaming, but must be clearly communicated to users.

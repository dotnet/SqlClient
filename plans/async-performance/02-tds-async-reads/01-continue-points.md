# Fix 1: Expand Continue-Point Coverage

**Priority:** Medium — reduces replay overhead for common operations
**Complexity:** Medium
**Risk:** Medium — must not break existing async read correctness

## Problem

The `StateSnapshot` already supports "continue points" via `CaptureAsContinue()` /
`MoveToContinue()` / `RequestContinue()`. When enabled, instead of replaying from the very start of
a multi-packet read, the parser can resume from the last captured continue point. However, this is
only used in a subset of read operations.

## Location

**Files:**

- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParserStateObject.cs`
  - `StateSnapshot.CaptureAsContinue()` (line ~5170)
  - `StateSnapshot.MoveToContinue()` (line ~5133)
  - `StateSnapshot.RequestContinue()` (line ~5196)
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParserStateObject.Multiplexer.cs`
  - `ProcessSniPacket()` — where continue decisions are made

## Changes Required

### 1. Audit Current Continue-Point Usage

Identify all `TryRead*` methods in `TdsParserStateObject` and `TdsParser` that participate in
multi-packet async reads:

- `TryReadPlpBytes()` (line 2343) — PLP large value reading
- `TryReadByteArray()` (line 1666) — array reads
- `TryReadString()` / `TryReadStringWithEncoding()` — string reads
- `TryReadSqlValue()` — value deserialization
- `TryRun()` (line 2412) — main token loop

For each, determine: does it use continue points? If not, can it?

### 2. Add Continue Points to PLP Reading

`TryReadPlpBytes()` reads large `MAX`-typed columns in chunks. Each chunk is a PLP segment.
Currently, if the read yields mid-way, the entire PLP read replays from the first chunk. Add a
continue point after each successfully processed PLP chunk:

```csharp
// After successfully reading a PLP chunk:
if (_snapshot != null)
{
    _snapshot.CaptureAsContinue();
}
```

This would reduce the replay from O(n²) to O(n × chunk_size), where each replay only needs to
re-process the current chunk.

### 3. Add Continue Points to Token Loop

In `TryRun()`, after each complete token is processed, capture a continue point. This way, if the
next token needs more data, the parser resumes from the last complete token instead of the start of
the result set.

## Testing

- Benchmark: Large VARBINARY(MAX) async read — measure improvement vs baseline
- Unit test: Multi-packet read with continue points produces correct results
- Regression: All existing async tests must pass
- The `_permettReplayStackTraceToDiffer` DEBUG flag can help validate replay correctness

## Risk

- Medium — continue points change the replay semantics. If state is not correctly captured, the
  parser could produce corrupt data or infinite loops.

- Recommend extensive testing with `s_checkNetworkPacketRetryStacks` enabled.

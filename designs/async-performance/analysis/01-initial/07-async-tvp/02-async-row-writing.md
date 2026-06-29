# Fix 7.2: Async Row Writing in TdsParser

**Complexity:** Medium
**Risk:** Medium — touches the TDS writing pipeline

## Problem

The TVP row-writing pipeline from `TdsParser.WriteSmiParameter()` through
`ValueUtilsSmi.SetIEnumerableOfSqlDataRecord_Unchecked()` is fully synchronous. Even after accepting
`IAsyncEnumerable<SqlDataRecord>` (Fix 7.1), the underlying TDS write operations that serialize each
row's values are synchronous.

The flow:

1. `WriteSmiParameter()` (line 11096) — entry point
2. `ValueUtilsSmi.SetCompatibleValueV200()` (line 11160) — routes to setter
3. `SetIEnumerableOfSqlDataRecord_Unchecked()` (line 3538) — sync row loop
4. `FillCompatibleSettersFromRecord()` — writes column values via SMI setters
5. SMI setters ultimately call into `TdsParser.WriteXxx()` methods

When writing large TVPs, TDS buffer flushes may need to wait for network I/O, but the sync path
blocks the thread during these flushes.

## Current Code

### TdsParser.WriteSmiParameter (line 11096)

```csharp
internal void WriteSmiParameter(SqlParameter param, int paramIndex,
    bool sendDefault, TdsParserStateObject stateObj)
{
    // ... setup ...
    ValueUtilsSmi.SetCompatibleValueV200(
        sink, setters, 0, metaData[0], value, typeCode, 0, 0, null,
        stateObj, peekAhead);  // synchronous call
}
```

### ValueUtilsSmi FillCompatibleSettersFromRecord

This method writes each column using typed setters (`SetInt32`, `SetString`, etc.) which route
through the SMI layer to `TdsParser` write methods. These writes are synchronous and can trigger
buffer flushes.

## Proposed Fix

### Step 1: Async WriteSmiParameter

Add an async variant that handles the async enumerable case:

```csharp
internal async Task WriteSmiParameterAsync(SqlParameter param, int paramIndex,
    bool sendDefault, TdsParserStateObject stateObj)
{
    // Same setup as sync version...

    if (peekAhead?.AsyncEnumerator != null)
    {
        await ValueUtilsSmi.SetCompatibleValueV200Async(
            sink, setters, 0, metaData[0], asyncValue, typeCode,
            0, 0, null, stateObj, peekAhead).ConfigureAwait(false);
    }
    else
    {
        // Fall back to sync for non-async data sources
        ValueUtilsSmi.SetCompatibleValueV200(
            sink, setters, 0, metaData[0], value, typeCode,
            0, 0, null, stateObj, peekAhead);
    }
}
```

### Step 2: Async-Aware SMI Setters

The SMI setter layer writes column values and triggers TDS buffer flushes. The key integration point
is making buffer flushes async:

```csharp
// In the async row loop, after writing each row's data:
if (stateObj.HasPendingData)
{
    await stateObj.FlushAsync(CancellationToken.None).ConfigureAwait(false);
}
```

### Step 3: Integration with Existing Async Write Infrastructure

`TdsParser` already has async write support for `SqlBulkCopy` via `WriteBulkCopyValue()` (line
11899), which returns `Task` for async buffer flushes. The pattern:

```csharp
// WriteBulkCopyValue already handles async writes:
internal Task WriteBulkCopyValue(object value, SqlMetaDataPriv metadata,
    TdsParserStateObject stateObj, bool isSqlType, bool isDataFeed, bool isNull)
{
    // ... writes data ...
    // Returns Task if async flush needed, null if sync completed
}
```

Reuse this pattern for TVP row writing — when a write returns a non-null Task, await it before
proceeding to the next row.

## Files to Modify

| File | Change |
| ------ | -------- |
| `TdsParser.cs` | Add `WriteSmiParameterAsync()` variant |
| `ValueUtilsSmi.cs` | Add `SetCompatibleValueV200Async()` and async row loop |
| `TdsParser.cs` | Route async TVP parameters through async write path |

## Call Chain (After Fix)

```text
SqlCommand.ExecuteReaderAsync()
  → TdsParser.WriteSmiParameterAsync()         // new async entry
    → ValueUtilsSmi.SetCompatibleValueV200Async()  // new async routing
      → SetIEnumerableOfSqlDataRecord_UncheckedAsync()  // from Fix 7.1
        → FillCompatibleSettersFromRecordAsync()   // new — awaits flushes
          → stateObj.FlushAsync()                  // existing
```

## Testing

- Functional test: Large TVP (10K+ rows) via async path — verify no thread blocking
- Functional test: TVP with large column values triggering buffer flushes
- Performance test: Compare sync vs async TVP write throughput
- Unit test: Verify async flush is triggered correctly during row iteration

## Compatibility

- Sync paths unchanged — only activated when `IAsyncEnumerable` source is provided
- Requires Fix 7.1 as prerequisite

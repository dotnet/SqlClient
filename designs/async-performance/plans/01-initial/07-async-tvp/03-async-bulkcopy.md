# Fix 7.3: Async SqlBulkCopy Enumeration

**Complexity:** Medium
**Risk:** Low — extends existing async patterns

## Problem

`SqlBulkCopy` already supports `ReadAsync()` for `DbDataReader` sources (line 1192), but:

1. **DataTable/DataRow[] sources** use synchronous `_rowEnumerator.MoveNext()` (line 1259) — these
   are in-memory so this is acceptable

2. **IDataReader sources** use synchronous `Read()` (line 1242) even during `WriteToServerAsync()`
3. **No IAsyncEnumerable support** — there is no way to stream rows from an async source that isn't
   a `DbDataReader`

4. **WriteBulkCopyValue** (line 11899) already returns `Task` for async flushes, but the overall
   copy loop could be more consistently async

## Current Code

### ReadFromRowSourceAsync (line 1181)

```csharp
private Task ReadFromRowSourceAsync(CancellationToken cts)
{
    if (_dbDataReaderRowSource != null)  // DbDataReader — async read
    {
        return _dbDataReaderRowSource.ReadAsync(cts).ContinueWith(...);
    }
    else  // IDataReader, DataTable — sync read
    {
        _hasMoreRowToCopy = ReadFromRowSource();  // blocks thread
        return null;
    }
}
```

### ReadFromRowSource (line 1243) — sync only

```csharp
private bool ReadFromRowSource()
{
    // IDataReader path:
    return ((IDataReader)_rowSource).Read();  // sync

    // DataTable/DataRow path:
    _rowEnumerator.MoveNext();  // sync (acceptable — in-memory)
}
```

### Main Copy Loop (line 2533)

```csharp
for (i = rowsSoFar; ... && _hasMoreRowToCopy; i++)
{
    // ReadFromRowSourceAsync — may or may not be async
    // CopyColumnsAsync — async per-column writing
    // WriteBulkCopyValue — returns Task for async flushes
}
```

## Proposed Fix

### Step 1: Accept IAsyncEnumerable in SqlBulkCopy

Add a new `WriteToServerAsync` overload:

```csharp
#if NET
public Task WriteToServerAsync(IAsyncEnumerable<DataRow> rows,
    CancellationToken cancellationToken = default)
{
    // Store async enumerator as the row source
    _asyncRowEnumerator = rows.GetAsyncEnumerator(cancellationToken);
    _asyncRowSource = true;
    return WriteToServerInternalAsync(cancellationToken);
}
#endif
```

### Step 2: Async Row Reading for IAsyncEnumerable

Extend `ReadFromRowSourceAsync` to handle the async enumerator:

```csharp
private async Task ReadFromRowSourceAsync(CancellationToken cts)
{
    if (_dbDataReaderRowSource != null)
    {
        _hasMoreRowToCopy = await _dbDataReaderRowSource.ReadAsync(cts)
            .ConfigureAwait(false);
    }
    else if (_asyncRowEnumerator != null)
    {
        _hasMoreRowToCopy = await _asyncRowEnumerator.MoveNextAsync()
            .ConfigureAwait(false);
        if (_hasMoreRowToCopy)
            _currentRow = _asyncRowEnumerator.Current;
    }
    else
    {
        _hasMoreRowToCopy = ReadFromRowSource();
    }
}
```

### Step 3: Ensure Clean Disposal

```csharp
// In Dispose/cleanup:
if (_asyncRowEnumerator != null)
{
    await _asyncRowEnumerator.DisposeAsync().ConfigureAwait(false);
}
```

## Public API Changes

```csharp
// New overload (NET only):
public Task WriteToServerAsync(IAsyncEnumerable<DataRow> rows,
    CancellationToken cancellationToken = default);
```

This requires updating reference assemblies in `netcore/ref/`.

## Files to Modify

| File | Change |
| ------ | -------- |
| `SqlBulkCopy.cs` | Add `IAsyncEnumerable<DataRow>` overload and async row reading |
| `SqlBulkCopy.cs` | Add `_asyncRowEnumerator` field and disposal |
| `netcore/ref/Microsoft.Data.SqlClient.cs` | Add new public API surface |

## Testing

- Functional test: BulkCopy from `IAsyncEnumerable<DataRow>` — verify row count and data
- Functional test: Cancellation during async enumeration
- Functional test: Large batch (100K+ rows) via async enumerable
- Performance test: Compare `DbDataReader` vs `IAsyncEnumerable` throughput
- Unit test: Verify `DisposeAsync` called on enumerator

## Compatibility

- New API is additive — no breaking changes
- Only available on `#if NET` (not .NET Framework)
- Existing `WriteToServer`/`WriteToServerAsync` overloads unchanged
- `WriteBulkCopyValue` already supports async flushes — no TdsParser changes needed

## Relationship to Other Fixes

- **Independent** of Fixes 7.1/7.2 — BulkCopy has its own write pipeline separate from TVP
- **Benefits from** Fix 5.1 (TDS buffer pooling) — BulkCopy allocates write buffers
- The existing `DbDataReader.ReadAsync()` path already works; this fix extends the pattern to
  non-DbDataReader async sources

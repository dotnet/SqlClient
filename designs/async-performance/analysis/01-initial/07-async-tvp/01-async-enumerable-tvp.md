# Fix 7.1: Accept IAsyncEnumerable<SqlDataRecord> for TVPs

**Complexity:** Medium
**Risk:** Low — additive API, no behavioral changes

## Problem

TVP data sources are limited to synchronous interfaces:

- `DataTable` — fully materialized
- `DbDataReader` — `Read()` is synchronous on many implementations
- `IEnumerable<SqlDataRecord>` — `MoveNext()` is synchronous

When the data source is slow (database cursor, HTTP stream, etc.), calling `MoveNext()` blocks the
thread during async command execution, defeating the purpose of `ExecuteReaderAsync`.

## Current Code

### SqlParameter.GetActualFieldsAndProperties (~line 1319)

```csharp
// IEnumerable<SqlDataRecord> path
value is IEnumerable<SqlDataRecord> enumerable
// ...
IEnumerator<SqlDataRecord> enumerator = enumerable.GetEnumerator();
enumerator.MoveNext();  // synchronous — blocks thread
SqlDataRecord firstRecord = enumerator.Current;
```

### ValueUtilsSmi.SetIEnumerableOfSqlDataRecord_Unchecked (~line 3538)

```csharp
// Main row enumeration loop — entirely synchronous
while (enumerator.MoveNext())  // blocks thread per row
{
    setters.NewElement();
    SqlDataRecord record = enumerator.Current;
    FillCompatibleSettersFromRecord(setters, mdFields, record, defaults);
    recordNumber++;
}
setters.EndElements();
```

## Proposed Fix

### Step 1: New Parameter Acceptance

Add `IAsyncEnumerable<SqlDataRecord>` support in `SqlParameter`:

```csharp
// In GetActualFieldsAndProperties, add new branch:
else if (value is IAsyncEnumerable<SqlDataRecord> asyncEnumerable)
{
    IAsyncEnumerator<SqlDataRecord> asyncEnumerator = asyncEnumerable.GetAsyncEnumerator();
    // Peek-ahead must become async
    await asyncEnumerator.MoveNextAsync();
    SqlDataRecord firstRecord = asyncEnumerator.Current;

    // Store in ParameterPeekAheadValue (needs new field)
    peekAhead = new ParameterPeekAheadValue
    {
        AsyncEnumerator = asyncEnumerator,
        FirstRecord = firstRecord
    };
}
```

### Step 2: Extend ParameterPeekAheadValue

```csharp
internal struct ParameterPeekAheadValue
{
    internal IEnumerator<SqlDataRecord> Enumerator;      // existing
    internal IAsyncEnumerator<SqlDataRecord> AsyncEnumerator;  // new
    internal SqlDataRecord FirstRecord;
}
```

### Step 3: Async Row Loop in ValueUtilsSmi

Add new method alongside existing sync version:

```csharp
internal static async Task SetIEnumerableOfSqlDataRecord_UncheckedAsync(
    SmiEventSink_Default sink,
    SmiTypedGetterSetter setters,
    SmiExtendedMetaData[] mdFields,
    IAsyncEnumerable<SqlDataRecord> value,
    ParameterPeekAheadValue peekAhead,
    bool[] defaults)
{
    int recordNumber = 0;
    IAsyncEnumerator<SqlDataRecord> enumerator;

    if (peekAhead?.FirstRecord != null)
    {
        enumerator = peekAhead.AsyncEnumerator;
        setters.NewElement();
        FillCompatibleSettersFromRecord(setters, mdFields, peekAhead.FirstRecord, defaults);
        recordNumber++;
    }
    else
    {
        enumerator = value.GetAsyncEnumerator();
    }

    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
    {
        setters.NewElement();
        SqlDataRecord record = enumerator.Current;

        if (record.FieldCount != mdFields.Length)
            throw SQL.EnumeratedRecordFieldCountChanged(recordNumber);

        FillCompatibleSettersFromRecord(setters, mdFields, record, defaults);
        recordNumber++;
    }
    setters.EndElements();
}
```

## Files to Modify

| File | Change |
| ------ | -------- |
| `SqlParameter.cs` | Add `IAsyncEnumerable<SqlDataRecord>` check in `GetActualFieldsAndProperties` |
| `ParameterPeekAheadValue` (internal struct) | Add `AsyncEnumerator` field |
| `ValueUtilsSmi.cs` | Add `SetIEnumerableOfSqlDataRecord_UncheckedAsync` |
| `TdsParser.cs` | Route async TVPs to async write path from `WriteSmiParameter` |

## Public API Changes

```csharp
// SqlParameter.Value already accepts object — no new public API needed
// The type check is internal; users just assign:
param.Value = asyncEnumerable;  // IAsyncEnumerable<SqlDataRecord>
```

No reference assembly changes required since `Value` is `object`.

## Testing

- Unit test: Verify `IAsyncEnumerable<SqlDataRecord>` accepted as parameter value
- Unit test: Verify peek-ahead works with async enumerator
- Functional test: TVP with async data source, verify correct row count and values
- Functional test: Cancellation during async enumeration
- Functional test: Exception during `MoveNextAsync` propagation

## Compatibility

- Opt-in: users must explicitly provide `IAsyncEnumerable<SqlDataRecord>`
- Existing sync paths unchanged
- Requires `#if NET` guard — `IAsyncEnumerable` not available on .NET Framework

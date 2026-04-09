# Priority 7: Async TVP Data Sources

**Addresses:** #982
**Impact:** Low-Medium — niche but important for streaming scenarios
**Effort:** Medium

## Current State

### TVP Data Flow

TVPs (Table-Valued Parameters) accept data via synchronous interfaces:

- `DataTable` — fully materialized in memory
- `DbDataReader` — synchronous forward-only cursor
- `IEnumerable<SqlDataRecord>` — synchronous enumeration

The writing pipeline in `TdsParser.cs`:

- `WriteTvpTypeInfo()` (line ~11367) — writes column metadata
- `WriteTvpColumnMetaData()` (line ~11405) — writes column definitions
- `WriteBulkCopyValue()` (line ~11899) — writes each row's values

### No Async Enumerable Support

- `IAsyncEnumerable<SqlDataRecord>` is not supported anywhere
- `SqlBulkCopy` also uses synchronous row enumeration
- All row iteration uses `foreach` / `IEnumerator.MoveNext()`

### Data Source Interfaces (SqlParameter.cs)

`SqlParameter` accepts TVP data sources via:

- `Value` property — can hold `DataTable`, `SqlDataReader`, `IEnumerable<SqlDataRecord>`
- Schema detection via `GetSchemaTable()` — synchronous

## Incremental Fixes

| # | Fix | Complexity | Impact |
| --- | ----- | ----------- | -------- |
| 1 | [Accept IAsyncEnumerable<SqlDataRecord> for TVPs](01-async-enumerable-tvp.md) | Medium | Medium |
| 2 | [Async row writing in TdsParser](02-async-row-writing.md) | Medium | Medium |
| 3 | [Async SqlBulkCopy enumeration](03-async-bulkcopy.md) | Medium | Medium |

## Dependencies

- Fix 1 is the API surface change
- Fix 2 is the underlying async TDS write support
- Fix 3 extends the pattern to SqlBulkCopy
- All are independent of other priority areas

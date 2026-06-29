# Priority Rationale: Why P7

## Ranking: Priority 7 of 7

Async TVP data sources is ranked lowest because it addresses a niche usage pattern, has a
straightforward workaround, and does not cause cascading failures or severe performance degradation.

## Justification

### 1. Correctness Improvement for Streaming Scenarios

The current TVP interface forces synchronous enumeration via `IEnumerable<SqlDataRecord>`. When TVP
data is sourced from an async pipeline (HTTP stream, async database cursor, message queue), the
calling code must either:

- Block a thread in `MoveNext()` while waiting for async data
- Pre-materialize all data into memory before passing to the TVP

Adding `IAsyncEnumerable<SqlDataRecord>` support would allow truly streaming async TVP population,
which is the correct API for this use case.

### 2. Aligns with Modern .NET Patterns

`IAsyncEnumerable<T>` is a first-class citizen in .NET since .NET Core 3.0. Supporting it for TVPs
brings SqlClient in line with other data providers (e.g., Npgsql supports async enumeration for COPY
operations) and with the broader .NET ecosystem expectation that streaming APIs support async.

## Why Lowest Priority

### 1. Very Narrow Audience

TVPs are a specialized SQL Server feature used primarily for:

- Batch insert operations (where `SqlBulkCopy` is usually preferred)
- Passing structured parameters to stored procedures

The subset of TVP users who also need async streaming of TVP data is small. Most TVP usage involves
pre-materialized `DataTable` or in-memory collections where `IAsyncEnumerable` offers no benefit.

### 2. Simple Workaround Exists

Users needing async TVP data can pre-collect their data before parameterizing:

```csharp
var records = new List<SqlDataRecord>();
await foreach (var item in asyncSource)
{
    var record = new SqlDataRecord(metadata);
    record.SetValues(item.Values);
    records.Add(record);
}
param.Value = records;  // IEnumerable<SqlDataRecord>
```

This consumes more memory but is functionally correct and adds minimal latency for typical TVP sizes
(hundreds to thousands of rows).

### 3. No Production Failures

Unlike P1 (thread pool starvation → outages), P2 (250x slowdowns), or P6 (MARS timeouts), the lack
of async TVP support does not cause failures. It causes suboptimal thread usage during a specific,
short-duration operation.

### 4. Single Issue, Low Community Demand

Only issue #982 requests this capability, with minimal community engagement compared to the
high-comment counts of P1–P6 issues:

| Priority | Key Issue | Comments |
| ---------- | ----------- | ---------- |
| P1 | #601 | 50+ |
| P2 | #593 | 282 |
| P3 | #113 | 30+ |
| P4 | #979 | 20+ |
| P6 | #422 | 107 |
| **P7** | **#982** | **~10** |

### 5. Independent and Deferrable

P7 has no dependencies on other priorities and does not block any other work. It can be implemented
at any time without affecting the critical path of async performance improvement. This makes it
ideal for a future milestone after the foundational issues (P1–P4) are resolved.

## Recommended Approach

Defer until P1–P4 are substantially complete. When implementing:

1. Start with Fix 7.1 (API surface for `IAsyncEnumerable<SqlDataRecord>`)
2. Guard with `#if NET` — `IAsyncEnumerable` is not available on .NET Framework
3. Consider also adding `IAsyncEnumerable<DataRow>` for `SqlBulkCopy` (Fix 7.3) at the same time, as
   the pattern is similar

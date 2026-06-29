# Priority Rationale: Why P2

## Ranking: Priority 2 of 7

The TDS async read path rewrite is ranked second because it addresses the **most dramatic measured
performance degradation** in the driver — a 250x slowdown — but its very high implementation effort
and risk prevent it from being P1.

## Justification

### 1. Worst Measured Performance Gap

Issue #593 demonstrates the most extreme async performance degradation in SqlClient:

| Metric | Sync | Async | Ratio |
| -------- | ------ | ------- | ------- |
| Time (10MB read) | 20ms | 5,000ms | 250x slower |
| Memory allocated | 20MB | 13,156MB | 658x more |
| Time (20MB read) | ~40ms | 52,000ms | ~1,300x slower |

No other async performance issue in the tracker shows degradation of this magnitude. The 282
comments on #593 make it the most-discussed async performance issue in the repository.

### 2. Algorithmic Complexity (Not Just Constant Factor)

Unlike other priorities that address constant-factor overhead (extra allocations, unnecessary
blocking, missing async APIs), the TDS read issue is **algorithmically broken** — O(n²) in the
number of packets. This means:

- The problem gets worse as data sizes grow
- Incremental micro-optimizations cannot fix it
- It will remain the dominant bottleneck for any large-data async workload even after all other
  priorities are addressed

This algorithmic nature makes it uniquely important and justifies its high ranking despite the
difficulty.

### 3. Affects Real Workloads

While the worst case requires large `VARBINARY(MAX)` or `NVARCHAR(MAX)` columns, the snapshot/replay
overhead also degrades performance for:

- Any multi-packet result sets (common with queries returning many rows)
- JSON/XML columns (frequently large)
- BLOB storage patterns
- Reporting queries with wide result sets

Issue #1562 shows that even without large individual values, async operations are 2–5x slower under
load, partly due to snapshot/replay overhead on multi-packet responses.

### 4. Partially Mitigated by Workarounds

`CommandBehavior.SequentialAccess` combined with `GetStream()` can avoid the full replay penalty for
large column reads. This existing workaround, while not well-documented, reduces the urgency
somewhat compared to P1 where no workaround exists. This is a key factor in ranking it P2 rather
than P1.

## Why Not P1

Three factors prevent this from being the highest priority:

1. **Implementation effort is "Very High"** — A contributor described the full fix as "akin to brain
   surgery." The TDS parser is ~13,000 lines of intricate protocol code with 20+ years of evolution.
   A state machine rewrite risks regressions across every data type and edge case.

2. **Narrower user base than P1** — The pool affects every `OpenAsync()` caller. The TDS read issue
   primarily affects users reading large data values asynchronously. Users reading small results, or
   using sync methods, are unaffected.

3. **Incremental fixes already underway** — PRs #3377, #3534, #2714, and #2663 have progressively
   improved the constant factor. Continue-point expansion (Fix 2.1) and async-without-snapshot for
   sequential access (Fix 2.4) can deliver meaningful improvements without the full rewrite.

## Why Not Lower

Ranking this below P3–P4 would be incorrect because:

- **Async transactions (P3)** save one network roundtrip per transaction — a constant-time
  improvement. The TDS read issue causes unbounded degradation.

- **Async SNI opens (P4)** save blocking during connection establishment — a one-time cost per
  connection. The TDS read issue affects every read operation for the lifetime of the connection.

- **The 282-comment issue** represents significant community frustration and reputational damage
  that warrants high priority.

## Recommended Approach Given Priority

Since P2 is critical but high-effort, pursue a **dual strategy:**

1. **Short-term** (can start immediately): Expand continue-point coverage (Fix 2.1) and enable
   async-without-snapshot for sequential access (Fix 2.4). These deliver meaningful improvement with
   manageable risk.

2. **Long-term** (plan now, execute after P1): Design the state machine parser (Fix 2.5) as a phased
   architectural investment, potentially leveraging the TDS Reader abstraction from PR #2663.

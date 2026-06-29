# Priority Rationale: Why P3

## Ranking: Priority 3 of 7

Async transaction methods are ranked third because they complete a critical gap in the end-to-end
async story, are relatively straightforward to implement, and the fix eliminates a high-frequency
source of unnecessary thread blocking.

## Justification

### 1. Completes the Async API Contract

.NET Core 3.0 introduced `DbTransaction.CommitAsync()`, `RollbackAsync()`, and
`DbConnection.BeginTransactionAsync()` as part of the standard ADO.NET API surface. SqlClient
inherits these but does not override them — the base class implementations simply call the
synchronous methods.

This means users writing correct async code are unknowingly blocking threads on every transaction
boundary:

```csharp
await using var conn = new SqlConnection(cs);
await conn.OpenAsync();                    // benefits from P1 pool fix
await using var tx = conn.BeginTransaction();   // BLOCKS — sync roundtrip
// ... async work ...
await tx.CommitAsync();                    // BLOCKS — calls sync Commit()
```

With P1 and P3 both completed, this entire sequence becomes truly async.

### 2. High Call Frequency

Transaction begin/commit/rollback are among the most frequently called operations after `Open` and
`ExecuteReader`. In a typical OLTP workload, every business operation involves at least one
`BeginTransaction` + `Commit` pair. The per-call overhead is small (one network roundtrip), but the
cumulative thread-blocking impact is significant at scale.

### 3. Moderate Effort, Clear Path

The implementation path is well-defined:

- `TdsExecuteTransactionManagerRequest()` (TdsParser.cs line ~9819) needs an async variant that uses
  `_parserLock.WaitAsync()` instead of `.Wait()` and enables async writes

- `SqlTransaction.CommitAsync()`/`RollbackAsync()` need overrides that call the async TDS method

- `SqlConnection.BeginTransactionAsync()` needs an override
- The deferred-begin optimization (Fix 3.4) is independently valuable

The risk is manageable because transaction TDS operations use the same RPC mechanism as other
commands, and the async infrastructure for RPC calls already exists.

### 4. Deferred BEGIN Eliminates a Round Trip

Fix 3.4 (deferred `BEGIN TRANSACTION`) saves a full network round trip by prepending `BEGIN TRAN` to
the first command. This is the approach used by Npgsql (issue #1554) and is valuable even without
fully async transaction methods.

In high-latency environments (cloud, cross-region), eliminating one round trip per transaction can
save 5–50ms per operation — a meaningful improvement for latency-sensitive workloads.

## Why Not P1 or P2

- **vs P1 (Connection Pool):** The pool blocks far more users and causes cascading failures.
  Transaction overhead is a constant cost (one round trip) that doesn't cascade.

- **vs P2 (TDS Reads):** The TDS read issue causes unbounded degradation (250x+). Transaction
  overhead is bounded at one round trip per begin/commit.

## Why Not Lower

Ranking this below P4 (Async SNI Opens) would be incorrect because:

- **Transactions are called more often** than connection opens. In a pooled scenario, connections
  are opened rarely but transactions occur on every request.

- **The fix is simpler** — P4 requires changes across SNI, TDS, and the login pipeline. P3 adds
  async variants of a single TDS method and three API overrides.

- **P3 has independent value** — The deferred-begin optimization (Fix 3.4) is valuable regardless of
  whether P4 is done.

## Sequencing Consideration

P3 benefits from P1 being done first: the new `ChannelDbConnectionPool` provides async-friendly
infrastructure that transaction enlistment can build on. However, P3 is not blocked by P1 — the
async TDS transaction method works with either pool implementation.

# Priority 3: True Async Transaction Methods

**Addresses:** #113, #1554
**Impact:** Medium — enables end-to-end async transaction workflows
**Effort:** Medium

## Current State

### SqlTransaction (SqlTransaction.cs)

- `Commit()` (line 94) — synchronous, calls `InternalTransaction.Commit()`
- `Rollback()` (line 145) — synchronous, calls `InternalTransaction.Rollback()`
- **No async overrides** — `CommitAsync` and `RollbackAsync` are inherited from `DbTransaction`
  which just calls the sync methods

### SqlInternalTransaction (SqlInternalTransaction.cs)

- `Commit()` → `TdsParser.TdsExecuteTransactionManagerRequest()` — fully synchronous
- `Rollback()` → same path
- State machine: `Pending → Active → Committed/Aborted`

### TDS Transaction Execution (TdsParser.cs line 9819)

- `TdsExecuteTransactionManagerRequest()` — acquires parser lock, disables async writes, sends RPC
  synchronously, waits for response

- Sends `TransactionManagerRequestType.Begin/Commit/Rollback` as TDS RPC
- Line 9831: `_connHandler._parserLock.Wait()` — blocking lock acquisition
- Line 9836: `_asyncWrite = false` — explicitly disables async

### SqlConnection.BeginTransaction (SqlConnection.cs)

- No `BeginTransactionAsync()` override
- Always synchronous

## Incremental Fixes

| # | Fix | Complexity | Impact |
| --- | ----- | ----------- | -------- |
| 1 | [Add async TdsExecuteTransactionManagerRequest](01-async-tds-transaction.md) | Medium | High |
| 2 | [Override CommitAsync/RollbackAsync](02-async-commit-rollback.md) | Low | Medium |
| 3 | [Override BeginTransactionAsync](03-async-begin-transaction.md) | Low | Medium |
| 4 | [Deferred BEGIN TRANSACTION](04-deferred-begin.md) | Medium | Medium |

## Dependencies

- Fix 1 is the foundation — everything else depends on it
- Fixes 2 and 3 are independent of each other
- Fix 4 is independent but benefits from Fix 1 infrastructure

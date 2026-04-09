# Pool Comparison & Open Design Decisions

## Structural Differences

| Aspect | WaitHandleDbConnectionPool | ChannelDbConnectionPool |
|--------|---------------------------|------------------------|
| Idle storage | `ConcurrentStack<T>` (old + new generations) | `Channel<T>` (single FIFO) |
| Capacity tracking | Semaphore-based | `SemaphoreSlim` + channel |
| Stasis handling | Full stasis mechanism in `DeactivateObject` | **Not implemented** — simpler design only checks `EnlistedTransaction` |
| Deactivation | Complex multi-branch with stasis, `rootTxn` flags | `ReturnInternalConnection` is simpler — routes to transacted pool or idle channel |
| Cleanup timer | Two-generation aging (new → old → destroy) | Single idle timeout per connection |
| `DestroyObject` guard | Checks `IsTxRootWaitingForTxEnd` — no-ops if true | No guard; `RemoveConnection` disposes immediately |

## Open Decisions

### 1. Stasis strategy for `ReturnInternalConnection`

The WaitHandle pool enters stasis in 4 places (+ 1 in `DbConnectionInternal`). The Channel pool enters stasis in **0 places**.

**All `SetInStasis` call sites:**

| # | Location | Condition | Channel pool equivalent |
|---|----------|-----------|------------------------|
| 1 | `WaitHandleDbConnectionPool.CleanupCallback` | Timer age-out of a transaction root | No cleanup timer stasis — connection would be destroyed |
| 2 | `WaitHandleDbConnectionPool.DeactivateObject` | Pool shutting down + `IsTransactionRoot` | `RemoveConnection` — destroys immediately |
| 3 | `WaitHandleDbConnectionPool.DeactivateObject` | `IsTransactionRoot` + `Pool == null` | Not handled |
| 4 | `WaitHandleDbConnectionPool.DeactivateObject` | `!CanBePooled` + `IsTransactionRoot` + `!IsConnectionDoomed` | `RemoveConnection` — destroys immediately |
| 5 | `DbConnectionInternal.CloseConnection` | Non-pooled connection + `IsTransactionRoot` | Shared code — same behavior in both pools |

**Options:**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| A) Port stasis as-is | Add `SetInStasis` calls for the same edge cases | Exact parity with existing pool | Carries the known CleanupCallback race condition |
| B) Route through transacted pool | Always put `IsTransactionRoot` connections into the transacted pool when they can't go idle | Eliminates stasis complexity; aligns with channel pool's simpler design | Need to verify transacted pool handles shutdown correctly; need a transaction key |
| C) Defer | Document delegated transaction promotion as unsupported in pool v2 | Ship faster | Edge cases around shutdown + transaction roots will misbehave |

**Decision:** _TBD_

### 2. CleanupCallback race condition

The existing pool has a known race between `CleanupCallback` checking `IsTransactionRoot` under lock and calling `SetInStasis` outside the lock. The `TransactionCompleted` event can fire in between.

Should the new pool:
- Accept the same race (if porting stasis)?
- Redesign to eliminate it (e.g., do `SetInStasis` under the lock)?
- Avoid the issue entirely by choosing option B or C above?

**Decision:** _TBD_

### 3. `IsTxRootWaitingForTxEnd` guard in `RemoveConnection`

`WaitHandleDbConnectionPool.DestroyObject` no-ops when `IsTxRootWaitingForTxEnd` is true. `ChannelDbConnectionPool.RemoveConnection` has no such guard. This is a safety gap regardless of the stasis decision — if any code path puts a connection into stasis, `RemoveConnection` must not dispose it.

**Decision:** Add the guard. This is not optional if stasis is supported in any form.

### 4. Root connection death dooms the delegated transaction

Public documentation for `IPromotableSinglePhaseNotification` confirms that the PSPE contract requires the resource manager to handle `SinglePhaseCommit`, `Rollback`, and `Promote` callbacks—all of which need the live connection to issue `COMMIT`/`ROLLBACK` to SQL Server. There is no documented recovery path where a new physical connection can resume an orphaned server-side local transaction.

This means:
- If the root connection dies (broken pipe, server kill, etc.), the delegated transaction is **doomed**. The server-side local transaction is tied to the session that created it.
- `ReplaceConnection` on a dead root will create a new connection that inherits the `Transaction` object, but the server-side transaction is already gone. The `Rollback` PSPE callback will run against a connection that has no matching transaction.
- The existing code has no explicit guard preventing `ReplaceConnection` on a transaction root, but safety comes from circumstance: the only trigger is a broken connection, and a broken root means the transaction is already lost.

**Action:** Add a test that verifies a root connection break correctly dooms the delegated transaction (transaction aborts, no silent data loss). This confirms the contract and guards against future regressions if `ReplaceConnection` logic changes.

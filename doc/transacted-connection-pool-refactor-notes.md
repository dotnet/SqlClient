# Transacted Connection Pool Refactor — Session Notes

## What Was Done

### 1. Transaction Support in ChannelDbConnectionPool

Implemented full transaction support in `ChannelDbConnectionPool` (the channel-based "pool v2"), bringing it to parity with `WaitHandleDbConnectionPool`. Changes made to `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/ChannelDbConnectionPool.cs`:

- **`HasTransactionAffinity`** property — reads `PoolGroupOptions.HasTransactionAffinity`
- **`GetFromTransactedPool()`** — checks the `TransactedConnectionPool` for a connection associated with the current ambient transaction, validates liveness before returning
- **`ReturnInternalConnection()`** — routes connections with `EnlistedTransaction != null` to `TransactedConnectionPool.PutTransactedObject()` inside a lock; otherwise returns to idle channel
- **`GetInternalConnection()`** — calls `GetFromTransactedPool()` first when `HasTransactionAffinity` is true
- **`PrepareConnection()`** — accepts and passes `Transaction?` parameter to `ActivateConnection()`
- **`TransactionEnded()`** — delegates to `TransactedConnectionPool.TransactionEnded()`
- **`PutObjectFromTransactedPool()`** — resets and returns connections to idle channel, or destroys them if not poolable
- **Async path** — activated `ADP.SetCurrentTransaction()` call in `TryGetConnection` (was a TODO)

### 2. Parameterized Tests

Created `DbConnectionPoolTransactionTest.cs` with 23 test cases parameterized over both pool implementations (`"WaitHandle"` and `"Channel"`), totaling 46 test executions. Covers:
- Transaction routing (enlisted connections go to transacted pool)
- Lifecycle (commit/rollback cleanup)
- Connection reuse within same transaction
- Transaction affinity disabled behavior
- Nested transactions (`TransactionScopeOption.Required` vs `RequiresNew`)
- Mixed transacted/non-transacted workloads
- Concurrent access
- Sequential transaction isolation

Updated `ChannelDbConnectionPoolTest.cs` to remove `NotImplementedException` tests for newly implemented methods.

**All 133 tests pass** (46 new parameterized + 87 existing).

---

## Key Concepts Discussed

### Transaction Lifecycle — How Connections Flow

```
User calls SqlConnection.Close()
        │
        ▼
DbConnectionInternal.CloseConnection()
        │
        ▼
  Pool.ReturnInternalConnection(obj)
        │
        ├── obj.EnlistedTransaction != null?
        │     YES → TransactedConnectionPool.PutTransactedObject(txn, obj)
        │              (parked by transaction key; reusable by same txn)
        │     NO  → return to idle pool (channel or stack)
        │
        ▼
  Later: Transaction completes (commit/rollback)
        │
        ▼
  TransactionCompletedEvent fires
        │
        ▼
  DbConnectionInternal.TransactionCompletedEvent()
        │
        ├── Is delegated transaction root?
        │     YES → DelegatedTransactionEnded() → Pool.TransactionEnded()
        │     NO  → DetachTransaction() → Pool.TransactionEnded()
        │
        ▼
  TransactedConnectionPool.TransactionEnded(txn)
        │
        ├── Removes connection list for that transaction key
        ├── For each connection: Pool.PutObjectFromTransactedPool(obj)
        │
        ▼
  PutObjectFromTransactedPool(obj)
        │
        ├── Connection healthy & poolable? → return to idle pool
        └── Otherwise → destroy
```

### IsTransactionRoot vs IsTxRootWaitingForTxEnd

| Property | Meaning |
|----------|---------|
| `IsTransactionRoot` | This connection **owns** a delegated transaction. It was the connection that promoted a lightweight `TransactionScope` into a full distributed transaction. Set to `true` when `DelegatedTransaction` is assigned. |
| `IsTxRootWaitingForTxEnd` | This connection is a transaction root **and** has been placed into stasis — it has no user owner, it's not in any pool data structure, and it's just waiting for the `TransactionCompleted` event to fire so it can be cleaned up. Set by `SetInStasis()`. |

A connection can be `IsTransactionRoot == true` but `IsTxRootWaitingForTxEnd == false` — that's the normal state while a user is actively using it within a transaction.

### The Stasis Mechanism

**What it is:** A state where a delegated transaction root connection is "parked" with no owner — not in the idle pool, not held by user code, not in the transacted pool. It exists only because the delegated transaction hasn't finished yet and the connection must stay alive to support commit/rollback promotion.

**When it happens:** During `DeactivateObject` (user closed the connection) when the connection is a transaction root but can't be placed anywhere:
- Pool is shutting down — can't put it in idle stack
- `CanBePooled` is false (e.g., `LoadBalanceTimeout` expired) — can't put it in any pool
- `Pool == null` — edge case, can't put it anywhere
- `EnlistedTransaction` is already null — can't put it in transacted pool (no key)

**Why not just leave it in the idle stack?**
- Any `TryGetConnection` caller could pop it and get a broken connection still bound to an unfinished delegated transaction
- You'd have to teach every stack consumer to check `IsTransactionRoot` and skip, spreading transaction-awareness across the entire pool

**Why not "mark the pool as shutting down and wait"?**
- Pool shutdown is non-blocking — `Shutdown()` sets state and returns immediately
- There's nowhere to block; the pool group pruner runs periodically checking `Count == 0`
- Stasis **is** the wait mechanism — it's event-driven via `TransactionCompleted`

**How it resolves:** When the `TransactionCompleted` event fires:
1. `DelegatedTransactionEnded()` calls `TerminateStasis()` (clears `IsTxRootWaitingForTxEnd`)
2. Then calls `Pool.TransactionEnded()` → `PutObjectFromTransactedPool()`
3. `PutObjectFromTransactedPool` sees the connection is doomed/pool shutting down → `DestroyObject()`
4. `DestroyObject()` removes from `_objectList`, decrements count
5. Pool group pruner eventually sees `Count == 0` and finishes teardown

### Race Condition in CleanupCallback

There is a **known minor race condition** in WaitHandleDbConnectionPool's `CleanupCallback`:

```csharp
lock (obj)
{
    if (obj.IsTransactionRoot)
    {
        shouldDestroy = false;
    }
}
// ← Race window here: TransactionCompleted could fire between lock release and SetInStasis
if (shouldDestroy)
    DestroyObject(obj);
else
    obj.SetInStasis();
```

If `TransactionCompleted` fires in the window between the lock release and `SetInStasis()`, the stasis counter may be incremented without a corresponding decrement. The comment in the code acknowledges this and says it would require "more substantial re-architecture of the pool" to fix.

### DestroyObject Guard

`DestroyObject` has a guard: if `obj.IsTxRootWaitingForTxEnd` is true, it logs a trace and **does nothing** — it doesn't dispose the connection. This is a safety net so that if someone calls `DestroyObject` on a connection in stasis, it's a no-op. The `TransactionCompleted` event will eventually call `PutObjectFromTransactedPool`, which will call `DestroyObject` again after `TerminateStasis` has cleared the flag.

---

## Design Differences: ChannelDbConnectionPool vs WaitHandleDbConnectionPool

| Aspect | WaitHandleDbConnectionPool | ChannelDbConnectionPool |
|--------|---------------------------|------------------------|
| Idle storage | `ConcurrentStack<T>` (old + new) | `Channel<T>` (single FIFO) |
| Capacity tracking | Semaphore-based | `SemaphoreSlim` + channel |
| Stasis handling | Full stasis mechanism in `DeactivateObject` | **Not implemented** — simpler design only checks `EnlistedTransaction` |
| `DeactivateObject` | Complex multi-branch with stasis, rootTxn flags | `ReturnInternalConnection` is simpler — routes to transacted pool or idle channel |
| Cleanup timer | Two-generation aging (new → old → destroy) | Single idle timeout per connection |

The ChannelDbConnectionPool deliberately does **not** replicate the stasis mechanism. Its simpler `ReturnInternalConnection` only checks `EnlistedTransaction != null` vs returning to idle pool. This means the edge cases around `IsTransactionRoot` with null `EnlistedTransaction` during pool shutdown are not handled the same way. This is an area for future investigation if delegated transaction scenarios need to be supported.

---

## Files Modified

| File | Change |
|------|--------|
| `src/.../ConnectionPool/ChannelDbConnectionPool.cs` | Added transaction support (8 methods/properties) |
| `tests/UnitTests/.../ConnectionPool/DbConnectionPoolTransactionTest.cs` | **NEW** — 23 parameterized tests × 2 pool types |
| `tests/UnitTests/.../ConnectionPool/ChannelDbConnectionPoolTest.cs` | Removed `NotImplementedException` tests for implemented methods |
| `doc/transacted-connection-return-paths.md` | **NEW** — Mermaid diagram of return paths |

## Build & Test Commands

```bash
# Build
dotnet build src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj

# Run parameterized transaction tests
dotnet test src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj \
  --filter "FullyQualifiedName~DbConnectionPoolTransactionTest"

# Run all pool tests
dotnet test src/Microsoft.Data.SqlClient/tests/UnitTests/Microsoft.Data.SqlClient.UnitTests.csproj \
  --filter "FullyQualifiedName~ConnectionPool"
```

## Open Questions / Future Work

1. **Stasis in ChannelDbConnectionPool**: The channel pool does not implement stasis. If delegated transaction scenarios (transaction promotion) are needed, this gap needs addressing.
2. **Race condition elimination**: The `CleanupCallback` race window in WaitHandleDbConnectionPool is acknowledged but unfixed. A re-architecture could eliminate it.
3. **`IsTransactionRoot` with null `EnlistedTransaction`**: This edge case (connection is a delegated transaction root but the transaction reference has been detached) is only handled via stasis in the old pool. The new pool may silently mishandle this.
4. **Mermaid rendering**: The diagram in `doc/transacted-connection-return-paths.md` requires a Mermaid-capable renderer (VS Code extension `bierner.markdown-mermaid` or `mermaid-cli` for PNG export).

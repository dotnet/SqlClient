# Design: Transaction Support in ChannelDbConnectionPool

This document describes the changes needed to `ChannelDbConnectionPool` to meet the transaction support requirements defined in [requirements.md](../02-requirements/requirements.md).

## Design Principles

1. **Reuse shared infrastructure.** `TransactedConnectionPool`, `DbConnectionInternal` (stasis, enlistment, `DetachTransaction`), and `SqlDelegatedTransaction` already handle most transaction logic. The pool's job is to route connections correctly and protect transaction roots from premature destruction.

2. **Minimize divergence from WaitHandle pool behavior.** For transaction-related paths, the observable outcomes must match. Internal simplifications are fine as long as they don't change what the user or `System.Transactions` sees.

3. **Keep the Channel pool's simpler structure.** The WaitHandle pool has 7 return branches with 4 stasis entry points. We should aim for fewer branches by rethinking when stasis is actually needed vs. when the transacted pool already provides the necessary protection.

---

## Gap Analysis: Current State vs Requirements

| Requirement | Current Channel Pool | Status |
|-------------|---------------------|--------|
| REQ-1.x Connection reuse | `GetFromTransactedPool` + `PutTransactedObject` | ✅ Working |
| REQ-2.x Transaction isolation | `TransactedConnectionPool` keyed by `Transaction` | ✅ Working |
| REQ-3.x Commit/rollback cleanup | `TransactionEnded` → `PutObjectFromTransactedPool` | ✅ Working |
| REQ-4.x Nested scopes | Handled by `Transaction.Current` ambient + pool lookup | ✅ Working (no pool changes needed) |
| REQ-5.1 Lifetime immunity during transaction | `IsLiveConnection` checks `LoadBalanceTimeout` — may destroy transacted connections | ❌ Gap |
| REQ-5.2 Post-transaction lifetime check | `PutObjectFromTransactedPool` doesn't check lifetime | ❌ Gap |
| REQ-6.1 Root survival during shutdown | `Shutdown` throws `NotImplementedException` | ❌ Gap |
| REQ-6.2 Post-tx destroy on shutdown | `PutObjectFromTransactedPool` checks `State is Running` | ✅ Working |
| REQ-6.3 ClearPool lazy invalidation | `Clear` throws `NotImplementedException` | ❌ Gap |
| REQ-6.4 Non-root destroy on shutdown | No shutdown implementation | ❌ Gap |
| REQ-7.x Promotion | Handled by `SqlDelegatedTransaction` / `DbConnectionInternal` | ✅ Working (no pool changes needed) |
| REQ-8.1 Transaction timeout | Standard `TransactionCompleted` path | ✅ Working |
| REQ-8.2-8.4 Dead connection handling | `GetFromTransactedPool` has root vs non-root check | ✅ Working |
| REQ-8.5 Pool exhaustion | Transacted connections counted via `_connectionSlots` | ✅ Working |
| REQ-8.6 Failed open in scope | `PrepareConnection` catches and returns on failure | ✅ Working |
| REQ-9.1 Async transaction flow | `TryGetConnection` captures `Transaction` in `AsyncState` | ✅ Working |
| REQ-9.2 Thread safety of cleanup | `TransactionEnded` → `PutObjectFromTransactedPool` is thread-safe | ✅ Working |
| REQ-9.3 Concurrent async opens | `TransactedConnectionPool` list supports multiple connections per tx | ✅ Working |

### Summary of Gaps

1. **`Shutdown()` not implemented** — affects REQ-6.1, REQ-6.2, REQ-6.4
2. **`Clear()` not implemented** — affects REQ-6.3
3. **`RemoveConnection` has no transaction root guard** — could destroy a root connection during stasis or shutdown
4. **Lifetime/timeout checks don't exempt transacted connections** — affects REQ-5.1, REQ-5.2
5. **No stasis mechanism for transaction roots** — affects REQ-6.1

---

## Design Decision: Stasis Strategy

### Analysis

Stasis exists in the WaitHandle pool to handle one core scenario: **a delegated transaction root connection that cannot go to the idle pool and cannot be destroyed**. This happens when:
- Pool is shutting down + connection is a transaction root
- Connection is not poolable (`CanBePooled == false`) + connection is a transaction root + not doomed

In these cases, the connection has no valid destination — it can't go idle (others would grab it), it can't be destroyed (PSPE callbacks need it), and the transacted pool won't take it (it's already been removed from the transacted pool, or the pool is shutting down).

### Decision: Port stasis as-is

The stasis mechanism is small (a flag + counter on `DbConnectionInternal`) and is already fully implemented in the shared base class. The cost of porting it is low — we just need to add the `SetInStasis` calls and the `IsTxRootWaitingForTxEnd` guard in the right places. The alternative (routing through the transacted pool) would require changes to how `PutTransactedObject` works during shutdown, which is riskier.

**Rationale:**
- `SetInStasis()`, `TerminateStasis()`, `IsTxRootWaitingForTxEnd`, and `DelegatedTransactionEnded()` are all on `DbConnectionInternal` — shared code, already works
- The `TransactionCompleted` event will call `DelegatedTransactionEnded()` which calls `TerminateStasis()` and then `PutObjectFromTransactedPool()` — all of which the Channel pool already implements
- The WaitHandle pool's stasis has a known race in `CleanupCallback` that SqlClient does not have an equivalent to in the Channel pool (no two-generation timer), so that particular race does not apply

---

## Design: Changes by Component

### 1. `ReturnInternalConnection` — Add stasis branches

Current return path:
```
ReturnInternalConnection(connection):
├── !IsLiveConnection? → REMOVE
├── IsConnectionDoomed? → REMOVE
├── CanBePooled?
│   └── lock(connection)
│       ├── EnlistedTransaction != null? → TRANSACTED POOL
│       ├── ShuttingDown? → REMOVE
│       └── else → IDLE CHANNEL
├── ShuttingDown? → REMOVE
└── else → REMOVE
```

Proposed return path:
```
ReturnInternalConnection(connection):
├── !IsLiveConnection? → REMOVE (with tx root guard)
├── DeactivateConnection()
├── IsConnectionDoomed? → REMOVE (with tx root guard)
├── CanBePooled?
│   └── lock(connection)
│       ├── EnlistedTransaction != null? → TRANSACTED POOL
│       ├── ShuttingDown?
│       │   ├── IsTransactionRoot? → STASIS
│       │   └── else → REMOVE
│       └── else → IDLE CHANNEL
├── IsTransactionRoot && !IsConnectionDoomed? → STASIS
├── ShuttingDown? → REMOVE
└── else → REMOVE
```

**Changes:**
- When pool is shutting down and `IsTransactionRoot`, enter stasis instead of destroying
- When `!CanBePooled` and `IsTransactionRoot` and `!IsConnectionDoomed`, enter stasis
- Both stasis paths call `connection.SetInStasis()` and rely on `TransactionCompleted` → `DelegatedTransactionEnded()` → `PutObjectFromTransactedPool()` to eventually return or destroy the connection

### 2. `RemoveConnection` — Add transaction root guard

```csharp
private void RemoveConnection(DbConnectionInternal connection)
{
    if (connection.IsTxRootWaitingForTxEnd)
    {
        SqlClientEventSource.Log.TryPoolerTraceEvent(
            "<prov.DbConnectionPool.RemoveConnection|RES|CPOOL> {0}, Connection {1}, " +
            "Skip: in stasis, waiting for transaction end.",
            Id,
            connection.ObjectID);
        return;
    }

    _connectionSlots.TryRemove(connection);
    _idleConnectionWriter.TryWrite(null);
    connection.Dispose();
}
```

This prevents any code path (including `IsLiveConnection` failures) from accidentally disposing a stasis connection.

### 3. `PutObjectFromTransactedPool` — Add lifetime check

Currently:
```csharp
if (State is Running && connection.CanBePooled)
{
    connection.ResetConnection();
    _idleConnectionWriter.TryWrite(connection);
}
```

Proposed:
```csharp
if (State is Running && connection.CanBePooled && IsLiveConnection(connection))
{
    connection.ResetConnection();
    _idleConnectionWriter.TryWrite(connection);
}
else
{
    RemoveConnection(connection);
}
```

This addresses REQ-5.2 — if a connection exceeded its lifetime during the transaction, it's destroyed at transaction completion rather than returned to the idle pool.

### 4. `IsLiveConnection` — Exempt transacted connections

Currently `IsLiveConnection` checks `LoadBalanceTimeout` unconditionally. When called on a connection being retrieved from the transacted pool (`GetFromTransactedPool`), it could incorrectly destroy a connection that must stay alive for the transaction.

However, looking at the current code, `GetFromTransactedPool` does NOT call `IsLiveConnection` — it calls `IsConnectionAlive()` directly (health check only, no lifetime check). So the transacted pool retrieval path is already correct.

The concern is about the general `GetInternalConnection` loop, which calls `IsLiveConnection` on connections retrieved from the idle channel. Transacted connections aren't in the idle channel, so this is also fine.

**No change needed.** The existing code structure already exempts transacted connections from lifetime checks by using `IsConnectionAlive()` (health only) in `GetFromTransactedPool` and `IsLiveConnection()` (health + lifetime) only for idle pool connections.

### 5. `Shutdown` — Implement

```csharp
public void Shutdown()
{
    State = ShuttingDown;
    _idleConnectionWriter.TryComplete();
}
```

When `State` is `ShuttingDown`:
- `ReturnInternalConnection` routes transaction roots to stasis and destroys everything else
- `PutObjectFromTransactedPool` destroys connections (already works: `State is Running` check fails)
- The connection channel is completed, which wakes up any waiters with a `ChannelClosedException`

We do **not** proactively drain the transacted pool during shutdown. Connections parked there will be cleaned up when their `TransactionCompleted` events fire and route through `PutObjectFromTransactedPool`.

Idle connections remaining in the channel should be drained and disposed:

```csharp
public void Shutdown()
{
    State = ShuttingDown;
    _idleConnectionWriter.TryComplete();

    // Drain idle connections
    while (_idleConnectionReader.TryRead(out var connection))
    {
        if (connection != null)
        {
            RemoveConnection(connection);
        }
    }
}
```

### 6. `Clear` — Implement

`ClearPool` should mark all connections as non-poolable without destroying transacted connections:

```csharp
public void Clear()
{
    // Drain all idle connections from the channel and destroy them
    while (_idleConnectionReader.TryRead(out var connection))
    {
        if (connection != null)
        {
            RemoveConnection(connection);
        }
    }

    // Transacted connections are NOT directly touched.
    // They will be destroyed lazily:
    // - At retrieval time: IsLiveConnection/CanBePooled check fails
    // - At transaction completion: PutObjectFromTransactedPool sees CanBePooled == false
    //
    // Note: The existing WaitHandle pool iterates all objects and sets CanBePooled = false.
    // The Channel pool doesn't maintain a master list of all connections. Instead, we rely on
    // the connection's own state checks at retrieval and return time.
}
```

**Open question:** The WaitHandle pool marks individual connections as non-poolable by iterating a comprehensive list. The Channel pool doesn't maintain such a list (connections are either in the channel, in the transacted pool, or in use). We need to decide whether to:
- (A) Add a tracking collection of all outstanding connections to enable marking them
- (B) Use a pool-level generation counter — connections created before the clear are considered stale

Option B is simpler: increment a counter on `Clear()`, and check it in `IsLiveConnection`:
```csharp
private int _clearGeneration;

public void Clear()
{
    Interlocked.Increment(ref _clearGeneration);

    // Drain idle connections
    while (_idleConnectionReader.TryRead(out var connection))
    {
        if (connection != null)
        {
            RemoveConnection(connection);
        }
    }
}
```

Each `DbConnectionInternal` would store its `_poolGeneration` at creation time (set when the connection is first added to the pool via `ConnectionPoolSlots.Add`), and `IsLiveConnection` would compare:
```csharp
if (connection.PoolGeneration != _clearGeneration)
{
    return false;
}
```

This follows the same pattern as Npgsql's `PoolingDataSource`, which uses a `_clearCounter` on the pool and a matching counter on each connector, compared at return and retrieval time.

**Decision:** Use the generation counter approach. `ConnectionPoolSlots` is a fixed-capacity array with CAS-based slot management — it is not iterable by design (no `IEnumerable`, no snapshot API). Adding iteration would break its concurrency model. The generation counter is simple, lock-free, and avoids modifying `ConnectionPoolSlots` or `DbConnectionInternal` in ways that affect other pool implementations.

Implementation:
- Add `private int _clearGeneration` field to `ChannelDbConnectionPool`
- Add `internal int PoolGeneration` property to `DbConnectionInternal` (set at creation time when added to the pool)
- `Clear()` increments `_clearGeneration` and drains idle connections
- `IsLiveConnection` rejects connections whose generation doesn't match the pool's current generation
- Transacted connections with a stale generation will be destroyed when retrieved from the transacted pool (health check in `GetFromTransactedPool`) or when the transaction completes (`PutObjectFromTransactedPool` calls `IsLiveConnection`)

---

## Concurrency Analysis

### Race: `ReturnInternalConnection` vs `TransactionCompleted`

This race is analyzed in [07-threading-and-synchronization.md](../01-research/07-threading-and-synchronization.md). The design is safe because:

1. `ReturnInternalConnection` locks `connection` when reading `EnlistedTransaction`
2. `DetachTransaction` locks `transaction` when clearing `EnlistedTransaction`
3. These are different lock objects — no deadlock
4. The ordering doesn't matter: either the user thread sees the transaction as active (→ transacted pool) or as ended (→ idle pool). Both outcomes are correct.

### Race: `Shutdown` vs `TransactionCompleted`

- `Shutdown` sets `State = ShuttingDown` and drains idle connections
- `PutObjectFromTransactedPool` checks `State is Running` — if shutdown happened, destroys connection
- `ReturnInternalConnection` checks `State == ShuttingDown` — routes to stasis or destroy
- These checks don't need to be atomic because the `TransactionCompleted` callback will eventually clean up regardless of the ordering

### Race: `Clear` vs active connections

- `Clear` marks connections via `DoNotPoolThisConnection()`
- Active connections will see `CanBePooled == false` when they return
- `ReturnInternalConnection` already handles `!CanBePooled` — routes to stasis (if tx root) or destroy
- Transacted connections see `CanBePooled == false` at retrieval or transaction completion

### Stasis → `PutObjectFromTransactedPool` safety

When a connection is in stasis:
1. `TransactionCompleted` fires on SysTx thread
2. `CleanupConnectionOnTransactionCompletion` → `DetachTransaction` → `DelegatedTransactionEnded`
3. `DelegatedTransactionEnded` calls `TerminateStasis()` then `Pool.PutObjectFromTransactedPool()`
4. `PutObjectFromTransactedPool` sees `State != Running` (shutdown) → calls `RemoveConnection`
5. `RemoveConnection` sees `IsTxRootWaitingForTxEnd == false` (stasis terminated) → disposes

This chain is the same as the WaitHandle pool and is safe.

---

## Summary of Changes

| File | Change | Requirements |
|------|--------|-------------|
| `ChannelDbConnectionPool.ReturnInternalConnection` | Add stasis branches for `IsTransactionRoot` during shutdown and when `!CanBePooled` | REQ-6.1 |
| `ChannelDbConnectionPool.RemoveConnection` | Add `IsTxRootWaitingForTxEnd` guard | REQ-6.1, REQ-8.2 |
| `ChannelDbConnectionPool.PutObjectFromTransactedPool` | Add `IsLiveConnection` check before returning to idle | REQ-5.2 |
| `ChannelDbConnectionPool.Shutdown` | Basic shutdown **deferred to [separate PR](../../shutdown/outline.md)**. This PR adds transaction-aware stasis branches on top. | REQ-6.1, REQ-6.2, REQ-6.4 |
| `ChannelDbConnectionPool.Clear` | **Deferred to [separate PR](../../clear/outline.md)**: generation counter, drain idle connections | REQ-6.3 |

No changes needed to:
- `TransactedConnectionPool` — works as-is
- `SqlDelegatedTransaction` — PSPE callbacks unchanged
- `GetFromTransactedPool` — retrieval logic already correct
- `GetInternalConnection` / `TryGetConnection` — transaction flow already correct

Minor additions (deferred to [clear](../../clear/outline.md)):
- `DbConnectionInternal` — add `PoolGeneration` property (used only by Channel pool's `Clear` generation counter)
- `ChannelDbConnectionPool` — add `_clearGeneration` field

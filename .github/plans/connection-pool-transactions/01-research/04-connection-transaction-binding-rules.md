# Connection-Transaction Binding Rules

When can two `SqlConnection` objects share a physical connection? When must they get separate ones? How does the pool decide?

## Topics to Research

- [x] Same `TransactionScope` + same connection string → do they share a physical connection?
- [x] Same `TransactionScope` + different connection string → separate physical connections?
- [x] `TransactionScopeOption.RequiresNew` — forces a new transaction; what happens to pool lookup?
- [x] `TransactionScopeOption.Suppress` — no ambient transaction; does the pool skip the transacted pool?
- [x] `HasTransactionAffinity` — what does it control, who sets it, what's the default?
- [x] `DbConnectionPoolGroupOptions.HasTransactionAffinity` — the code path that enables/disables transacted pool lookup
- [x] Multiple connections enlisted in the same transaction — how the transacted pool stores and retrieves them
- [x] The selection logic in `GetTransactedObject` — which connection is returned when multiple are parked?

## Findings

### HasTransactionAffinity

Gates whether the transacted pool is consulted at all. Defined in `DbConnectionPoolGroupOptions` (immutable, set via constructor). The value comes from the **`Enlist` connection string keyword** (default `true`):

```csharp
// SqlConnectionFactory.cs line ~748
poolingOptions = new DbConnectionPoolGroupOptions(
    ...,
    opt.Enlist);  // ← becomes HasTransactionAffinity
```

- `Enlist=true` (default) → pool checks `Transaction.Current` and uses the transacted pool
- `Enlist=false` → pool ignores transactions entirely, never looks at `Transaction.Current`

### Lookup Order in TryGetConnection

1. **If `HasTransactionAffinity`**: Call `GetFromTransactedPool(out transaction)`
   - Reads `Transaction.Current` via `ADP.GetCurrentTransaction()`
   - If non-null and a matching connection exists in `TransactedConnectionPool` dictionary → return it (LIFO)
   - If null → return null immediately
2. **If step 1 returned null**: Go to general idle pool (semaphore/wait handles or channel)
3. **If idle pool empty**: Create new connection (if under `MaxPoolSize`)

Transacted pool is always checked **first** when there's an ambient transaction.

### Connection Reuse: Same Transaction + Same Connection String

- First `Open()`: No transacted connection → idle pool or create new → enlist → user uses → `Close()` parks in `TransactedConnectionPool` keyed by transaction
- Second `Open()`: Transacted pool lookup finds the parked connection → returns it → **same physical connection reused** without re-enlisting

This is the core purpose of the transacted pool.

### Separate Connections: Same Transaction + Different Connection String

Each connection string → own `DbConnectionPool` → own `TransactedConnectionPool`. No sharing across connection strings. If both are in the same `TransactionScope`, the second triggers **promotion** (lightweight → distributed via MSDTC).

### Multiple Connections Per Transaction

`TransactedConnectionPool` stores `Dictionary<Transaction, TransactedConnectionList>`. `TransactedConnectionList` extends `List<DbConnectionInternal>`, so multiple connections can be parked for one transaction. `GetTransactedObject` returns LIFO (last added = first returned). Remaining connections stay parked until `TransactionCompleted` cleans them up.

### TransactionScopeOption Effects

| Option | `Transaction.Current` | Pool behavior |
|--------|----------------------|---------------|
| `Required` (default) | Existing or new transaction | Transacted pool consulted with that transaction as key |
| `RequiresNew` | New, different transaction | Transacted pool consulted but no match (different key) → idle pool |
| `Suppress` | `null` | `GetFromTransactedPool` sees null → skips transacted pool entirely → idle pool |

No explicit `Suppress` handling — it works implicitly because `Transaction.Current == null`.

### Health Check Asymmetry on Retrieval

When pulling from the transacted pool, different checks based on root status:

| Connection type | Check | On failure |
|----------------|-------|------------|
| Transaction root (`IsTransactionRoot`) | `IsConnectionAlive(throwOnException: true)` | **Throw** — transaction is doomed without its root |
| Non-root | `IsConnectionAlive()` | Silently discard, return null → caller tries idle pool |

### Key Source Locations

| File | Relevant members |
|------|-----------------|
| `SqlConnectionFactory.cs` | `CreateConnectionPoolGroupOptions()` — sets `HasTransactionAffinity` from `opt.Enlist` |
| `DbConnectionPoolOptions.cs` | `DbConnectionPoolGroupOptions.HasTransactionAffinity` property |
| `WaitHandleDbConnectionPool.cs` | `HasTransactionAffinity` (line ~268), `GetFromTransactedPool()` (line ~1187), `TryGetConnection()` (line ~936) |
| `ChannelDbConnectionPool.cs` | `GetFromTransactedPool()` (line ~614) |
| `TransactedConnectionPool.cs` | `GetTransactedObject()` (line ~108), `TransactedConnectionList` (line ~34) |

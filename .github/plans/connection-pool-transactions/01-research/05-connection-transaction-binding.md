# Connection-Transaction Binding Rules

When can two `SqlConnection` objects share a physical connection? When must they get separate ones? How does the pool decide?

This document builds on the enlistment concepts from [03-connection-enlistment-lifecycle.md](03-connection-enlistment-lifecycle.md) and the transacted pool dictionary from [04-transaction-cloning.md](04-transaction-cloning.md).

## HasTransactionAffinity

This property gates whether the pool consults the transacted pool at all. It is set once when the pool is created, based on the **`Enlist` connection string keyword** (default `true`):

```csharp
// SqlConnectionFactory.cs
poolingOptions = new DbConnectionPoolGroupOptions(
    ...,
    opt.Enlist);  // → becomes HasTransactionAffinity
```

- `Enlist=true` (default): pool checks `Transaction.Current` and uses the transacted pool
- `Enlist=false`: pool ignores transactions entirely, never looks at `Transaction.Current`

## Lookup Order in TryGetConnection

When the pool needs to provide a connection, it follows this order:

1. **Transacted pool** (if `HasTransactionAffinity`): Call `GetFromTransactedPool()`, which reads `Transaction.Current`. If a matching connection exists in the `TransactedConnectionPool` dictionary for that transaction, return it (LIFO). If no ambient transaction or no match, return null.
2. **General idle pool**: Pop from the idle stack/channel.
3. **Create new**: If under `MaxPoolSize`, open a new connection.

The transacted pool is always checked **first** when there's an ambient transaction.

## Connection Reuse Within a Transaction

This is the core purpose of the transacted pool:

- **First `Open()`**: No transacted connection exists → idle pool or create new → enlist → user uses → `Close()` parks the connection in `TransactedConnectionPool` keyed by transaction
- **Second `Open()`**: Transacted pool lookup finds the parked connection → returns it → **same physical connection reused** without re-enlisting

Same `TransactionScope` + same connection string = same physical connection.

## Separate Connections: Different Connection Strings

Each connection string gets its own `DbConnectionPool` and its own `TransactedConnectionPool`. No sharing across connection strings. If both connections are in the same `TransactionScope`, the second triggers **promotion** (lightweight → distributed via MSDTC).

## Multiple Connections Per Transaction

`TransactedConnectionPool` stores `Dictionary<Transaction, TransactedConnectionList>`. `TransactedConnectionList` extends `List<DbConnectionInternal>`, so multiple connections can be parked for one transaction. `GetTransactedObject` returns LIFO (last added = first returned). Remaining connections stay parked until `TransactionCompleted` cleans them up.

## TransactionScopeOption Effects on the Pool

| Option | `Transaction.Current` | Pool Behavior |
|--------|----------------------|---------------|
| `Required` (default) | Existing or new transaction | Transacted pool consulted with that transaction as key |
| `RequiresNew` | New, different transaction | Transacted pool consulted but no match (different key) → idle pool |
| `Suppress` | `null` | `GetFromTransactedPool` sees null → skips transacted pool → idle pool |

No explicit `Suppress` handling in the pool — it works implicitly because `Transaction.Current == null`.

## Health Check Asymmetry on Retrieval

When pulling a connection from the transacted pool, different checks apply based on root status:

| Connection Type | Health Check | On Failure |
|----------------|-------------|------------|
| Transaction root (`IsTransactionRoot`) | `IsConnectionAlive(throwOnException: true)` | **Throw** — the transaction is doomed without its root |
| Non-root | `IsConnectionAlive()` | Silently discard, return null — caller tries idle pool |

A dead non-root connection is a recoverable situation (create a new one and propagate the DTC cookie). A dead root is unrecoverable because the delegated transaction is tied to that specific server session (see [02-delegated-transactions-and-pspe.md](02-delegated-transactions-and-pspe.md)).

## Key Source Locations

| File | Relevant Members |
|------|-----------------|
| `SqlConnectionFactory.cs` | `CreateConnectionPoolGroupOptions()` — sets `HasTransactionAffinity` |
| `DbConnectionPoolOptions.cs` | `DbConnectionPoolGroupOptions.HasTransactionAffinity` |
| `WaitHandleDbConnectionPool.cs` | `HasTransactionAffinity` (~268), `GetFromTransactedPool()` (~1187), `TryGetConnection()` (~936) |
| `ChannelDbConnectionPool.cs` | `GetFromTransactedPool()` (~614) |
| `TransactedConnectionPool.cs` | `GetTransactedObject()` (~108), `TransactedConnectionList` (~34) |

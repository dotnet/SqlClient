# Reconnection and Retry Mechanisms in Microsoft.Data.SqlClient

## Purpose

This document catalogues every mechanism through which `SqlConnection` or `SqlCommand` may
re-execute or re-establish work after a failure. Each mechanism is described with its trigger,
scope, configuration, database-context implications, and links to the authoritative public
documentation.

The mechanisms fall into two fundamentally different categories:

| Category | Scope | Who drives it | User visibility |
| -------- | ----- | ------------- | --------------- |
| **Connection-level** | Physical TCP/TDS session | Driver internals | Transparent — the application does not see the retry loop |
| **Command-level** | Logical operation (`Open`, `ExecuteReader`, …) | `SqlRetryLogicBaseProvider` | Semi-transparent — the application opts in and can observe via `Retrying` event |

> **Key distinction:** Connection-level retries attempt to restore the *physical link* (and, where
> possible, the session state). Command-level retries re-execute the *entire operation* from
> scratch, which means a new connection may be obtained from the pool and all session state set
> outside the connection string (e.g. `USE [db]`, `SET` options) is lost unless the application
> restores it.

---

## 1  Idle Connection Resiliency (Connection Retry)

### What it is

A driver-internal mechanism that transparently reconnects a broken physical connection *before* (or
instead of) surfacing an error to the application. It is sometimes called **connection resiliency**
or **idle connection recovery**.

### When it triggers

1. A command is about to execute (`ExecuteReader`, `ExecuteNonQuery`, etc.).
2. The driver calls `SqlConnection.ValidateAndReconnect()` which validates the SNI link via
   `TdsParserStateObject.ValidateSNIConnection()`.
3. If the link is dead **and** session recovery was negotiated with the server
   (`_sessionRecoveryAcknowledged == true`), the driver enters the reconnect loop.

### Configuration

| Connection string keyword | Default | Azure SQL | Synapse On-Demand | Range |
| ------------------------- | ------- | --------- | ----------------- | ----- |
| `ConnectRetryCount` | 1 | 2 (auto) | 5 (auto) | 0 – 255 |
| `ConnectRetryInterval` | 10 s | 10 s | 10 s | 1 – 60 s |

Setting `ConnectRetryCount=0` disables both idle connection resiliency and session recovery
negotiation entirely. The elevated defaults for Azure endpoints are applied automatically in
`SqlConnection.CacheConnectionStringProperties()` ([SqlConnection.cs, line
~483](../../../src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlConnection.cs)).

### Code path

```text
SqlCommand.RunExecuteNonQueryTds / RunExecuteReaderTds
  └─ SqlConnection.ValidateAndReconnect(beforeDisconnect, timeout)
       ├─ ValidateSNIConnection()          → link alive? return null (no-op)
       ├─ Check _sessionRecoveryAcknowledged
       ├─ Snapshot CurrentSessionData      → captures _database, _initialDatabase, _delta[]
       ├─ DoomThisConnection()
       └─ Task.Factory.StartNew → ReconnectAsync(timeout)
            └─ for (attempt = 0; attempt < _connectRetryCount; attempt++)
                 ├─ ForceNewConnection = true
                 ├─ OpenAsync()            → full login, sends session-recovery feature request
                 ├─ On success → return    (session state restored by server)
                 ├─ On SqlException:
                 │    ├─ last attempt?     → throw CR_AllAttemptsFailed
                 │    └─ timeout soon?     → throw CR_NextAttemptWillExceedQueryTimeout
                 └─ Task.Delay(ConnectRetryInterval * 1000)
```

### Session state and database context

During the reconnection login, the driver sends a **session recovery feature request**
(`TdsParser.WriteSessionRecoveryFeatureRequest`) containing the `SessionData` snapshot. This tells
the server to replay the session's accumulated state — including the current database — so that the
reconnected session is in the same logical state as the old one.

If session recovery succeeds, the **current database is preserved** transparently.

### Failure modes for database context

| Condition | Outcome |
| --------- | ------- |
| Server does not support session recovery | `_sessionRecoveryAcknowledged` is `false`; `ValidateAndReconnect` returns `null`; the broken link surfaces as a `SqlException` on the next command — **no transparent retry** |
| Unrecoverable session states (e.g. open `MARS` sessions, temp tables, certain `SET` options) | `CR_UnrecoverableClient` / `CR_UnrecoverableServer` error thrown — **no transparent retry** |
| `ConnectRetryCount = 0` | Session recovery feature is not negotiated; no transparent reconnect |

### Public documentation

- [SqlConnection.ConnectionString —
  `ConnectRetryCount`](https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring):
  *"Controls the number of reconnection attempts after the client identifies an idle connection
  failure. Valid values are 0 to 255. The default is 1, which disables reconnecting on idle
  connection failure; a value of 0 is not allowed." [...] "When set to a positive value, connection
  resiliency is turned on. When the client detects a broken idle connection, it creates a new
  connection and restores the server-side session state."*
- [Connection string
  syntax](https://learn.microsoft.com/sql/connect/ado-net/connection-string-syntax): Lists
  `ConnectRetryCount` and `ConnectRetryInterval` as connection string keywords.
- [SQL Server connection
  pooling](https://learn.microsoft.com/sql/connect/ado-net/sql-server-connection-pooling): Describes
  how severed connections are detected and how pool recycling interacts with connection lifetime.

---

## 2  Connection Open Retry (Connection Retry)

### What it is

Retries of `SqlConnection.Open()` / `OpenAsync()` driven by the **configurable retry logic**
provider attached to the connection. This is *not* the same as idle connection resiliency — it wraps
the *initial* `Open` call (or any subsequent explicit `Open` after a `Close`).

### When it triggers

`SqlConnection.Open()` checks `IsProviderRetriable`. If true, the call is funnelled through
`TryOpenWithRetry()` → `RetryLogicProvider.Execute(this, () => TryOpen(…))`. The retry provider
catches exceptions, evaluates `TransientPredicate` and `RetryCondition`, and re-invokes the delegate
after an interval.

### Configuration

```csharp
// Assign a custom provider before opening
connection.RetryLogicProvider = SqlConfigurableRetryFactory.CreateFixedRetryProvider(
    new SqlRetryLogicOption
    {
        NumberOfTries = 5,
        DeltaTime = TimeSpan.FromSeconds(1),
        TransientErrors = new[] { 4060, 40197, 40501, 40613, 49918, 49919, 49920, 11001 }
    });
connection.Open();   // retried up to 5 times on transient errors
```

Alternatively, retry logic can be configured globally via `appsettings.json` /
`SqlConfigurableRetryLogicManager`.

### Database context implications

Each retry calls `Open()` from scratch. The connection obtained is brand-new (from the pool or
freshly created). Database context is determined solely by the `Initial Catalog` in the connection
string — **no prior session state carries over**.

This is expected behaviour: the connection was never open, so there is no prior state to restore.

### Public documentation

- [Configurable retry logic in
  SqlClient](https://learn.microsoft.com/sql/connect/ado-net/configurable-retry-logic): Overview,
  configuration, and API reference for `SqlRetryLogicBaseProvider` and
  `SqlConfigurableRetryFactory`.
- [SqlConnection
  class](https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection): Documents
  the `RetryLogicProvider` property.

---

## 3  Command Execution Retry (Command Retry)

### What it is

Retries of individual command executions (`ExecuteNonQuery`, `ExecuteReader`, `ExecuteScalar`,
`ExecuteXmlReader` and their `Async` variants) driven by the `SqlRetryLogicBaseProvider` attached to
the *command*.

### When it triggers

Each public `Execute*` method checks `IsProviderRetriable`. If true, execution is wrapped in a retry
delegate. For example:

```text
SqlCommand.ExecuteNonQuery()
  └─ InternalExecuteNonQueryWithRetry(…)
       └─ RetryLogicProvider.Execute(sender: this, function: () => InternalExecuteNonQuery(…))
```

The retry provider catches `SqlException`, evaluates `TransientPredicate(e)` and
`RetryCondition(sender)`, and re-invokes the delegate on match.

### Configuration

```csharp
command.RetryLogicProvider = SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(
    new SqlRetryLogicOption
    {
        NumberOfTries = 3,
        DeltaTime = TimeSpan.FromMilliseconds(500),
        TransientErrors = new[] { 1205 }   // deadlock victim
    });
int rows = command.ExecuteNonQuery();   // retried up to 3 times on deadlock
```

### How it differs from connection retries

| Aspect | Idle Connection Resiliency (§1) | Command Retry (§3) |
| ------ | ------------------------------- | ------------------ |
| **Scope** | Reconnects the physical TCP/TDS session | Re-executes the SQL statement from scratch |
| **Trigger** | Dead SNI link detected before command runs | `SqlException` thrown *during* command execution |
| **Driven by** | `ConnectRetryCount` / `ConnectRetryInterval` in connection string | `SqlRetryLogicBaseProvider` on `SqlCommand.RetryLogicProvider` |
| **Session state** | Preserved via TDS session recovery feature request | **Lost** — the re-executed command runs on whatever session state the connection currently has |
| **Transaction awareness** | N/A (reconnected session cannot be in a user transaction) | `RetryCondition` rejects retry if the command has an active `SqlTransaction` or ambient `Transaction.Current` |
| **Idempotency** | Transparent — the command has not started yet | Caller's responsibility — the same SQL runs again; non-idempotent operations (INSERT, UPDATE) may double-execute |
| **Default state** | Enabled (`ConnectRetryCount ≥ 1`) | Disabled (no-op provider unless explicitly configured) |
| **Available since** | Always (Microsoft.Data.SqlClient 1.0) | Microsoft.Data.SqlClient 3.0 |

### Database context implications

Command retry re-invokes the command delegate on the **same `SqlConnection`**. If the connection's
physical link is still healthy, the session state (including current database) is unchanged.
However, if the transient error caused the physical connection to die and be replaced by the pool,
the new connection starts at `Initial Catalog`.

> **Important:** Command retry does **not** call `ValidateAndReconnect`. It simply re-calls the
> inner execution method. Any physical reconnection that happens is a side-effect of the pool
> returning a different connection after the old one was doomed.

### Retry condition guards

`SqlRetryLogic.RetryCondition(sender)` prevents retries when:

1. There is an ambient `System.Transactions.Transaction.Current`.
2. The `SqlCommand` has a non-null `Transaction` property (explicit `SqlTransaction`).
3. A user-supplied `PreCondition` predicate returns false.

These guards exist because retrying inside a transaction is almost always incorrect — the
transaction is doomed after the first failure.

### Public documentation

- [Configurable retry logic in
  SqlClient](https://learn.microsoft.com/sql/connect/ado-net/configurable-retry-logic): Full
  walkthrough of `SqlRetryLogicOption`, `SqlConfigurableRetryFactory`, and per-command
  configuration.
- [Step 4: Connect resiliently to SQL with
  ADO.NET](https://learn.microsoft.com/sql/connect/ado-net/step-4-connect-resiliently-sql-ado-net):
  Application-level retry pattern with transient error list (4060, 40197, 40501, 40613, 49918,
  49919, 49920, 11001).

---

## 4  Pool Reset (`sp_reset_connection`)

### What it is

Not a retry mechanism per se, but a state-transition that resets session state on a pooled
connection before reuse. Understanding it is essential because it determines what database context a
recycled connection starts with.

### When it triggers

When a pooled connection is reused, `TdsParser.PrepareResetConnection()` piggybacks an
`sp_reset_connection` (or `sp_reset_connection_keep_transaction`) request onto the **first TDS
request** sent on that session. The server responds with `ENV_SPRESETCONNECTIONACK`, which triggers
`SessionData.Reset()`.

### Effect on database context

`sp_reset_connection` resets the session to its **login defaults**, which means:

- Current database → reverts to the database from the login (typically `Initial Catalog`, or
  `master` if none specified).
- All `SET` options → reverted to server defaults.
- Temporary tables → dropped.
- Session-level state → cleared.

Any database context set via `USE [db]` during the previous lease of the connection is **lost**.

### Public documentation

- [SQL Server connection
  pooling](https://learn.microsoft.com/sql/connect/ado-net/sql-server-connection-pooling): *"The
  pool is automatically cleared when a fatal error occurs, such as a failover."*
- [Pool fragmentation due to many
  databases](https://learn.microsoft.com/sql/connect/ado-net/sql-server-connection-pooling#pool-fragmentation-due-to-many-databases):
  The official docs explicitly show the `USE` pattern for switching databases on a pooled connection
  but note the pool-fragmentation implications.

---

## 5  Application-Level Custom Retry (Not driver-managed)

### What it is

A retry loop implemented entirely in application code, outside the driver. Microsoft's documentation
provides a reference implementation for this pattern.

### Example (from Microsoft Learn)

```csharp
for (int tries = 1; tries <= maxRetries; tries++)
{
    try
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        // execute command …
        break;
    }
    catch (SqlException e) when (IsTransient(e.Number))
    {
        if (tries == maxRetries) throw;
        Thread.Sleep(TimeSpan.FromSeconds(Math.Pow(2, tries)));
    }
}
```

### Database context implications

Because each retry creates (or re-opens) a connection, the database context always starts at
`Initial Catalog`. Any session state from a previous attempt is irrelevant — it was on a different
physical connection.

### Public documentation

- [Step 4: Connect resiliently to SQL with
  ADO.NET](https://learn.microsoft.com/sql/connect/ado-net/step-4-connect-resiliently-sql-ado-net):
  Full C# sample with transient error classification.

---

## Summary: Retry Mechanisms at a Glance

| # | Mechanism | Level | Trigger | Preserves DB Context? | Enabled by Default? |
| - | --------- | ----- | ------- | --------------------- | ------------------- |
| 1 | Idle Connection Resiliency | Connection | Dead SNI link detected before command | **Yes** (via TDS session recovery) | Yes (`ConnectRetryCount=1`) |
| 2 | Connection Open Retry | Connection | `SqlException` during `Open()` | N/A (no prior session) | No (requires `RetryLogicProvider`) |
| 3 | Command Execution Retry | Command | `SqlException` during `Execute*()` | **No** — same connection, but if physical link was replaced, state is lost | No (requires `RetryLogicProvider`) |
| 4 | Pool Reset | Connection | Pooled connection reused | **No** — resets to login defaults | Yes (always, for pooled connections) |
| 5 | Application Custom Retry | Both | Application-defined | **No** — new connection each attempt | N/A (application code) |

---

## Relationship to Issue #4108

The bug described in issue #4108 — *"SqlConnection doesn't restore database in the new session if
connection is lost"* — sits at the intersection of mechanisms 1 and 4:

1. **If idle connection resiliency fires (§1)**, the session recovery feature request *should*
   restore the `USE`-switched database. If it does not, there is a driver bug in the `SessionData`
   snapshot or the recovery protocol.
2. **If the connection is instead replaced from the pool (§4)**, `sp_reset_connection` clears the
   `USE` context and the connection reverts to `Initial Catalog`. This is **by design** — pool reset
   always returns to login defaults.
3. **Command-level retry (§3)**, if configured, would re-execute the query on whatever connection
   state exists *after* the physical link was replaced — meaning the database context may have been
   lost.

The detailed analysis of each flow and its database-context behaviour is in
[02-flows.md](02-flows.md) and [03-issues.md](03-issues.md).

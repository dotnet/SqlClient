# Connection Pool Transaction Support

Goal: Add full transaction support to `ChannelDbConnectionPool`, the new pool implementation behind the `UseConnectionPoolV2` AppContext switch.

## Stage 1 — Research

Understand how System.Transactions works, how connections interact with transactions, and how the existing pool handles all of this.

**Status:** Complete

| # | Document | Status | Description |
|---|----------|--------|-------------|
| 1 | [System.Transactions overview](01-research/01-system-transactions-overview.md) | Complete | How the framework works as a consumer: lightweight vs distributed, promotion, `TransactionScope`, `TransactionCompleted`, two-phase commit |
| 2 | [SqlClient transaction enlistment](01-research/02-sqlclient-transaction-enlistment.md) | Complete | Code path from `Open()` through auto-enlistment, `EnlistedTransaction` lifecycle, `DetachTransaction` |
| 3 | [Delegated transactions & promotion](01-research/03-delegated-transactions-and-promotion.md) | Complete | `IsTransactionRoot`, `IsTxRootWaitingForTxEnd`, stasis mechanism, why it exists |
| 4 | [Connection-transaction binding rules](01-research/04-connection-transaction-binding-rules.md) | Complete | When connections share/separate, `HasTransactionAffinity`, `TransactionScopeOption` behavior |
| 5 | [Threading model](01-research/05-threading-model.md) | Complete | `Transaction.Current`, which thread fires events, lock ordering, async interactions |
| 6 | [Connection return & cleanup paths](01-research/06-connection-return-and-cleanup-paths.md) | Complete | Every path from "in use" to "somewhere else," diagram, race condition analysis |
| 7 | ~~TransactedConnectionPool data structure~~ | Removed | Deleted — content was inaccurate |
| 8 | [Failure modes & edge cases](01-research/08-failure-modes-and-edge-cases.md) | Complete | Timeouts, dead connections, pool shutdown, ReplaceConnection on roots, known race conditions, `DestroyObject` guard gap |

## Stage 2 — Requirements

Define the desired end-user behavior independent of implementation. What should a user observe when using `SqlConnection` with transactions on pool v2?

**Status:** Not started

| Document | Description |
|----------|-------------|
| Connection reuse within a transaction | _TODO_ — Same connection string within same `TransactionScope` should return the same physical connection |
| Transaction isolation between callers | _TODO_ — Different transactions must never share a physical connection |
| Commit/rollback cleanup | _TODO_ — Connections should return to the general pool after transaction completion |
| Nested transaction scopes | _TODO_ — `Required` vs `RequiresNew` vs `Suppress` behavior |
| Connection lifetime + transactions | _TODO_ — What happens when `LoadBalanceTimeout` expires on a transacted connection |
| Pool shutdown + transactions | _TODO_ — What happens when the pool is shutting down but a transaction is still active |
| Delegated transaction promotion | _TODO_ — What happens when a lightweight transaction is promoted to distributed |
| Error cases | _TODO_ — Transaction timeout, connection failure mid-transaction, pool exhaustion during transaction |
| Async scenarios | _TODO_ — `TransactionScopeAsyncFlowOption.Enabled`, async connection open within a scope |

## Stage 3 — Design

Design a solution for `ChannelDbConnectionPool` that meets the requirements. Decide what to keep, change, or simplify from the `WaitHandleDbConnectionPool` approach.

**Status:** Not started

| Document | Description |
|----------|-------------|
| [Pool comparison & open decisions](03-design/pool-comparison-and-decisions.md) | Structural differences between pool implementations, stasis strategy options, open design decisions |
| Architecture overview | _TODO_ — How `ChannelDbConnectionPool`, `TransactedConnectionPool`, and `DbConnectionInternal` collaborate |
| Concurrency model | _TODO_ — Locking strategy, race condition analysis, channel vs lock interactions |

## Stage 4 — Implementation

Implement and test the solution.

**Status:** Not started

| Document | Description |
|----------|-------------|
| Work plan | _TODO_ — Checklist of implementation tasks derived from design decisions |
| Changelog | _TODO_ — Track what was changed in each file and why |
| Test plan | _TODO_ — Unit test coverage map, integration test scenarios, edge case tests |
| Root disconnect test | _TODO_ — Verify that a physical connection break on a delegated transaction root correctly dooms the transaction (aborts, no silent data loss, no reconnect recovery) |

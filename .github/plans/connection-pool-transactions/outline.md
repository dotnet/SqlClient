# Connection Pool Transaction Support

Goal: Add full transaction support to `ChannelDbConnectionPool`, the new pool implementation behind the `UseConnectionPoolV2` AppContext switch.

## Stage 1 — Research

Understand how System.Transactions works, how connections interact with transactions, and how the existing pool handles all of this. Documents are ordered as a primer — each builds on the ones before it.

**Status:** Complete

### Layer 1: Foundations (no SqlClient knowledge needed)

| # | Document | Description |
|---|----------|-------------|
| 1 | [System.Transactions fundamentals](01-research/01-system-transactions-fundamentals.md) | TransactionScope, lightweight vs distributed, promotion trigger, TransactionCompleted event, timeout, 2PC, async flow |

### Layer 2: How SqlClient uses System.Transactions (single connection focus)

| # | Document | Description |
|---|----------|-------------|
| 2 | [Delegated transactions and PSPE](01-research/02-delegated-transactions-and-pspe.md) | PSPE optimization, SqlDelegatedTransaction, delegated vs propagated paths, IsTransactionRoot, why the root can't be destroyed |
| 3 | [Connection enlistment lifecycle](01-research/03-connection-enlistment-lifecycle.md) | Two enlistment points, EnlistedTransaction property, DetachTransaction, manual enlistment, ADP async bridging |
| 4 | [Transaction cloning](01-research/04-transaction-cloning.md) | Clone() mechanics, why SqlClient clones (EnlistedTransaction + dictionary keys), safe disposal pattern, DependentClone |

### Layer 3: Pool-level behavior (multiple connections, data structures)

| # | Document | Description |
|---|----------|-------------|
| 5 | [Connection-transaction binding](01-research/05-connection-transaction-binding.md) | HasTransactionAffinity, lookup order, connection reuse, TransactionScopeOption effects, health check asymmetry |
| 6 | [Connection return and cleanup paths](01-research/06-connection-return-and-cleanup-paths.md) | Path A (user close), Path B (transaction complete), stasis mechanism (consolidated), WaitHandle vs Channel comparison |

### Layer 4: Cross-cutting concerns

| # | Document | Description |
|---|----------|-------------|
| 7 | [Threading and synchronization](01-research/07-threading-and-synchronization.md) | Two competing threads, lock hierarchy, DelegatedTransactionEnded call chain, channel safety |
| 8 | [Failure modes and edge cases](01-research/08-failure-modes-and-edge-cases.md) | 9 failure modes, CleanupCallback race, DestroyObject guard gap, root connection death |

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

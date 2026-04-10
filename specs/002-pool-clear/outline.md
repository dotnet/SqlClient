# Connection Pool Clear (`ClearPool`)

Goal: Implement `Clear()` in `ChannelDbConnectionPool` so that `SqlConnection.ClearPool()` / `SqlConnection.ClearAllPools()` work with pool v2.

**Status:** Not started — deferred from the [transactions](../transactions/outline.md) PR to keep that change focused on transaction support.

## Background

The design for `Clear()` was developed as part of the transaction support design:
- [Design document § Clear](../transactions/03-design/design.md#6-clear--implement) — generation counter approach, concurrency analysis
- [REQ-6.3](../transactions/02-requirements/requirements.md) — ClearPool lazy invalidation requirement

### Key Design Decisions (already made)

1. **Generation counter** — increment a pool-level `_clearGeneration` on `Clear()`, stamp each connection with `PoolGeneration` at creation time, reject stale connections in `IsLiveConnection`. Follows Npgsql's `PoolingDataSource` pattern.
2. **ConnectionPoolSlots is not iterable** — the CAS-based slot array has no iteration API by design. This rules out the "mark all connections" approach used by `WaitHandleDbConnectionPool`.
3. **Transacted connections destroyed lazily** — at retrieval time (`GetFromTransactedPool` health check) or at transaction completion (`PutObjectFromTransactedPool` → `IsLiveConnection`).

## Phasing

### Phase 1: Basic Clear (this PR)
Non-transaction-aware clear — generation counter, drain idle connections, reject stale connections at return/retrieval time.

- [ ] Add `PoolGeneration` property to `DbConnectionInternal`
- [ ] Add `_clearGeneration` field to `ChannelDbConnectionPool`
- [ ] Stamp `PoolGeneration` when a connection is added to the pool
- [ ] Check generation in `IsLiveConnection`
- [ ] Implement `Clear()`: increment generation, drain idle channel
- [ ] Add unit tests

### Phase 2: Transaction-Aware Clear (connection-pool-transactions PR)
Ensure transacted connections with stale generations are destroyed lazily at the right points.

- [ ] Verify `GetFromTransactedPool` health check rejects stale-generation connections
- [ ] Verify `PutObjectFromTransactedPool` → `IsLiveConnection` destroys stale-generation connections at transaction completion
- [ ] Add unit tests for clear vs active transacted connections

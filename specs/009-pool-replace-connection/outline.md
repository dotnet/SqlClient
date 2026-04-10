# Connection Replacement

Goal: Implement `ReplaceConnection` in `ChannelDbConnectionPool` so that the connection resiliency retry path (`ConnectRetryCount`/`ConnectRetryInterval`) can swap a broken connection for a fresh one, preserving the caller's ambient transaction.

**Status:** Not started

## Background

When a connection experiences a transient failure and the driver's internal retry logic fires (`ConnectRetryCount > 0`), the retry path in `SqlConnectionFactory` / `SqlConnectionInternal.TryReplaceConnection` calls `IDbConnectionPool.ReplaceConnection()` to obtain a fresh connection. The WaitHandle pool implements this today; the Channel pool currently throws `NotImplementedException`.

### Key Behaviors (from WaitHandle pool)
- Creates a new connection via `UserCreateRequest` / connection factory
- Enlists the new connection in the caller's ambient transaction (if any) via `PrepareConnection`
- Calls `PrepareForReplaceConnection()` and `DeactivateConnection()` on the old connection
- Disposes the old connection
- Reuses the pool slot so capacity is unaffected

## Key Decisions

1. **Slot reuse**: The replacement connection reuses the old connection's pool slot — no capacity accounting changes needed.
2. **Transaction transfer**: If the caller has an ambient transaction, the new connection must be enlisted in it.
3. **Failure path**: If replacement creation fails, the error propagates to the caller and the pool slot is released.

## Stages

### Stage 1 — Research
- [ ] Trace the full `ReplaceConnection` call path from `SqlConnection` through `SqlConnectionFactory` to the pool
- [ ] Understand `PrepareForReplaceConnection()` and `DeactivateConnection()` lifecycle
- [ ] Understand how `PrepareConnection` enlists in ambient transactions

### Stage 2 — Requirements
- [ ] Define user-observable behavior and acceptance criteria

### Stage 3 — Design
- [ ] Design the `ReplaceConnection` implementation for `ChannelDbConnectionPool`

### Stage 4 — Implementation
- [ ] Implement and test

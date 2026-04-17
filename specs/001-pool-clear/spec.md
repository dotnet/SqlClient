# Feature Specification: Pool Clear

**Feature Branch**: `dev/mdaigle/connection-pool-designs`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "Implement Clear() in ChannelDbConnectionPool so that SqlConnection.ClearPool() and SqlConnection.ClearAllPools() work with pool v2, using a generation counter approach to lazily invalidate connections."

## User Scenarios & Testing

### User Story 1 - Clear a Specific Connection Pool

As an application developer, I want to call `SqlConnection.ClearPool(connection)` to invalidate all connections in a specific pool, so that the next connection request creates a fresh physical connection (e.g., after a failover, credential rotation, or configuration change on the server).

**Independent Test**: Can be tested by opening connections, calling `ClearPool`, then verifying that subsequent connection requests do not reuse pre-clear connections.

**Acceptance Scenarios**:

1. **Given** a pool with 10 idle connections, **When** `ClearPool` is called, **Then** all idle connections are closed and new connection requests create fresh physical connections.
2. **Given** a pool with 5 busy (in-use) connections and 5 idle connections, **When** `ClearPool` is called, **Then** idle connections are closed immediately.

---

### User Story 2 - Clear All Connection Pools

As an application developer, I want to call `SqlConnection.ClearAllPools()` to invalidate connections across all pools in the application, so that I can recover from broad infrastructure changes (e.g., DNS migration, certificate rollover).

**Independent Test**: Can be tested by creating connections to multiple connection strings, calling `ClearAllPools`, and verifying all pools produce fresh connections.

**Acceptance Scenarios**:

1. **Given** multiple pools each with idle connections, **When** `ClearAllPools` is called, **Then** all idle connections across all pools are closed.

---

### User Story 3 - Lazy Invalidation of Busy Connections

As the pool infrastructure, I need busy connections that were opened before a clear to be destroyed when they are returned to the pool, so that clearing does not interrupt active operations but still ensures all pre-clear connections are eventually removed.

**Independent Test**: Can be tested by opening a connection, calling `ClearPool`, executing a query on the open connection (should succeed), closing the connection, then verifying the pool destroyed it rather than reusing it.

**Acceptance Scenarios**:

1. **Given** a connection opened and in use before a clear, **When** `ClearPool` is called, **Then** the connection continues to work normally.
2. **Given** a connection opened and in use before a pool clear, **When** the connection is returned to the pool, **Then** the pool detects that it predates the clear and destroys it.
3. **Given** a connection opened after a pool clear, **When** the connection is returned to the pool, **Then** it is returned to the idle channel normally.

---

### User Story 4 - Multiple Consecutive Clears

As an application developer, I want multiple/concurrent calls to `ClearPool` to behave correctly, so that pool state is not corrupted.

**Independent Test**: Can be tested by calling `ClearPool` multiple times in rapid succession and verifying no exceptions, no connection leaks, and correct pool behavior afterward.

**Acceptance Scenarios**:

1. **Given** a pool with connections, **When** `ClearPool` is called twice rapidly, **Then** both calls complete without error, and only connections predating both clears are invalidated.

---

### Edge Cases

- What happens if `ClearPool` is called on an empty pool? The generation counter increments but no connections are closed. Subsequent connections are fresh.
- What happens if `ClearPool` is called during pool shutdown? The clear should be a no-op or complete harmlessly — shutdown already destroys all connections.
- What happens with many clears causing generation counter overflow? With `int` counter, overflow at 2^31. Even at 1 clear/second, this is 68 years. Overflow is not a practical concern. If it wraps, the worst case is one stale connection survives a single retrieval cycle.
- What happens if `ClearPool` is called during pool startup? `ClearPool` can be called at any time after the pool is instantiated. If connections are being added to the pool while clearing, they are closed subject to the conditions of the clear operation.
- What happens if ClearPool is called while a SqlConnection is waiting to receive a connection from the pool? If a SqlConnection is waiting for a connection, then there are no idle connections in the pool, so `ClearPool` will not have any effect.

## Requirements

### Functional Requirements

- **FR-001**: System MUST implement a generation counter that increments atomically on each `Clear()` call.
- **FR-002**: System MUST stamp each new connection with the current pool generation at creation time.
- **FR-003**: System MUST reject connections whose generation does not match the current pool generation when they are retrieved from the idle channel or returned to the pool.
- **FR-004**: System MUST drain all idle connections from the channel on `Clear()`, closing each one.
- **FR-005**: System MUST NOT interrupt busy (in-use) connections during a clear — busy connections are destroyed lazily when returned.
- **FR-006**: System MUST allow connections opened after a clear to be pooled normally.
- **FR-007**: System MUST support concurrent calls to `Clear()` without corrupting pool state.
- **FR-008**: System MUST integrate with the existing `SqlConnection.ClearPool()` and `SqlConnection.ClearAllPools()` call paths.

### Key Entities

- **Pool Generation Counter (`_clearGeneration`)**: A pool-level `volatile int` incremented atomically via `Interlocked.Increment` on each `Clear()` call. Represents the current "epoch" of the pool.
- **Connection Generation (`ClearGeneration`)**: A property on `DbConnectionInternal` stamped when the connection is created or added to the pool. Used to compare against the pool's current generation.
- **Stale Connection**: A connection whose `ClearGeneration` does not match the pool's `_clearGeneration`. Stale connections are destroyed rather than returned to the idle channel.

## Success Criteria

### Measurable Outcomes

- **SC-001**: After `ClearPool` is called, 100% of subsequent connection acquisitions produce fresh physical connections (no pre-clear connections reused).
- **SC-002**: Busy connections continue to operate normally during and after a pool clear — no active queries are interrupted.
- **SC-003**: `ClearPool` and `ClearAllPools` work identically whether using pool v1 (WaitHandle) or pool v2 (Channel), from the caller's perspective.

## Assumptions

- The generation counter approach is preferred over the WaitHandle pool's `DoNotPoolThisConnection()` mark-all pattern because `ConnectionPoolSlots` is not iterable by design (CAS-based slot array).
- `DbConnectionInternal` can accommodate a new `ClearGeneration` property without breaking existing functionality or requiring changes to the WaitHandle pool.
- The existing `SqlConnection.ClearPool()` → `SqlConnectionFactory.ClearPool()` → `IDbConnectionPool.Clear()` call chain is already wired up and only requires the `ChannelDbConnectionPool.Clear()` implementation.
- Transacted connections with stale generations are handled separately as part of the transactions feature and are out of scope for Phase 1 of pool clear.

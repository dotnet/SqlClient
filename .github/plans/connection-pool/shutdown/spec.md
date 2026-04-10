# Feature Specification: Connection Pool Shutdown

**Feature Directory**: `.github/plans/connection-pool/shutdown`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "shutdown"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Graceful Pool Shutdown on Connection String Change (Priority: P1)

When a developer changes a connection string (e.g., switching databases or servers), the pool associated with the old connection string is shut down. Idle connections in the old pool are immediately drained and disposed. Connections that are still checked out by the application continue to function, but when they are returned, they are destroyed rather than returned to the (now-defunct) idle pool. The application experiences no errors — the old connections are silently cleaned up and new connections are created in a new pool for the new connection string.

**Why this priority**: This is the most common trigger for pool shutdown in normal application operation. Without it, stale pools accumulate and leak connections to servers the application no longer communicates with.

**Independent Test**: Open several connections with connection string A. Close some (return to pool). Change to connection string B and open a connection. Verify that the idle connections from pool A are disposed. Return the remaining checked-out connections from pool A and verify they are destroyed, not pooled.

**Acceptance Scenarios**:

1. **Given** a pool is in Running state with idle connections, **When** shutdown is triggered, **Then** all idle connections are immediately drained from the pool and disposed.
2. **Given** a pool has been shut down, **When** a checked-out connection is returned, **Then** the connection is destroyed rather than returned to the idle pool.
3. **Given** a pool has been shut down, **When** the application opens a connection with the old connection string, **Then** a new pool is created (the shutdown pool is not reused).

---

### User Story 2 - Shutdown Stops Background Timers (Priority: P1)

When a pool shuts down, all background timers (pruning timer, idle timeout timer, error state timer) are stopped and disposed. No background callbacks execute after shutdown. This prevents races between timer callbacks and the shutdown/drain process, and ensures no resources are leaked.

**Why this priority**: Timer callbacks that fire during or after shutdown can access disposed resources, cause null reference errors, or interfere with connection draining. This is a correctness and stability requirement.

**Independent Test**: Start a pool with pruning enabled. Trigger shutdown. Verify that the pruning timer is disposed and no pruning callbacks fire after shutdown. Verify no timer-related exceptions are thrown.

**Acceptance Scenarios**:

1. **Given** the pool has an active pruning timer, **When** shutdown is triggered, **Then** the pruning timer is disposed and no further pruning callbacks execute.
2. **Given** the pool has an active error state timer, **When** shutdown is triggered, **Then** the error state timer is disposed.
3. **Given** a timer callback is in progress when shutdown is called, **When** the callback completes, **Then** it does not re-arm the timer (detects shutdown state).

---

### User Story 3 - Waiters Are Unblocked on Shutdown (Priority: P1)

When callers are blocked waiting for a connection (because the pool is at capacity), shutdown unblocks them so they can fail immediately rather than waiting indefinitely. A caller blocked on the idle channel receives an indication that the pool is no longer available and surfaces an appropriate error to the application.

**Why this priority**: Without this, a pool shutdown while callers are waiting causes those callers to hang until their connection timeout expires. Immediate unblocking provides a fast, clean failure path.

**Independent Test**: Exhaust a pool so new callers are waiting on the idle channel. Trigger shutdown. Verify all waiting callers are immediately unblocked and receive an error rather than continuing to wait.

**Acceptance Scenarios**:

1. **Given** callers are waiting on the idle channel for a connection, **When** shutdown is triggered, **Then** all waiting callers are unblocked immediately.
2. **Given** a caller is unblocked by shutdown, **When** the caller handles the result, **Then** it receives an error indicating the pool is no longer available.
3. **Given** shutdown has completed, **When** a new caller attempts to get a connection from the pool, **Then** the request fails immediately (no waiting).

---

### User Story 4 - Transaction Root Survival During Shutdown (Priority: P3)

When a pool shuts down while connections are participating in distributed transactions, transaction root connections are placed in stasis rather than destroyed. They survive until their transaction completes, at which point they are destroyed. Non-root connections are destroyed immediately on return during shutdown. This ensures distributed transactions are not broken by pool shutdown.

**Why this priority**: Ranked P3 because this is a correctness refinement for distributed transaction scenarios, which are less common. The basic shutdown (P1) handles the common case; transaction-aware shutdown layers on top.

**Independent Test**: Enlist a connection as a transaction root. Trigger pool shutdown. Verify the root connection is placed in stasis (not destroyed). Complete the transaction and verify the root connection is then destroyed.

**Acceptance Scenarios**:

1. **Given** a transaction root connection is checked out and a shutdown occurs, **When** the connection is returned, **Then** it is placed in stasis rather than destroyed.
2. **Given** a transaction root connection is in stasis during shutdown, **When** its transaction completes, **Then** the connection is destroyed.
3. **Given** a non-root transacted connection is returned during shutdown, **When** the return is processed, **Then** the connection is destroyed immediately.

---

### Edge Cases

- What happens when shutdown is called while a connection creation is in progress? The in-flight creation completes (or times out). When the connection is returned, it is destroyed because the pool is shutting down.
- What happens when shutdown is called twice? The second call is a no-op; the pool is already in ShuttingDown state.
- What happens when shutdown races with a connection being returned? The `ReturnInternalConnection` method checks `State == ShuttingDown` and routes to destroy. The state check and the idle channel write are not atomic, but channel completion ensures any write after shutdown either fails (connection is destroyed) or succeeds and the connection is drained.
- What happens when the idle channel has null wake-up signals during drain? Nulls are skipped during drain; only actual connections are disposed.
- What happens when the error state timer fires during shutdown? The timer callback detects the ShuttingDown state and does not re-arm; the timer is eventually disposed.

## Clarifications

### Session 2026-04-10

- Q: Should Story 4 (ClearPool triggers shutdown) be in this spec? → A: No — remove it. `ClearPool` calls `pool.Clear()` on a running pool (generation counter invalidation), not `Shutdown()`. ClearPool is fully covered by the clear feature spec. Shutdown is triggered by pool group pruning and pool group disabling, not ClearPool.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Shutdown MUST transition the pool state from Running to ShuttingDown.
- **FR-002**: Shutdown MUST complete the idle connection channel writer so that no new idle connections can be added and any waiting readers are unblocked.
- **FR-003**: Shutdown MUST drain all remaining idle connections from the channel and dispose them.
- **FR-004**: After shutdown, connections returned to the pool MUST be destroyed (via `RemoveConnection`) rather than pooled.
- **FR-005**: Shutdown MUST dispose all background timers (pruning, idle timeout, error state) and ensure no further callbacks execute.
- **FR-006**: Calling shutdown on an already-shutting-down pool MUST be a safe no-op.
- **FR-007**: Callers waiting on the idle channel for a connection MUST be unblocked when the channel writer is completed during shutdown.
- **FR-008**: During shutdown, transaction root connections MUST be placed in stasis rather than destroyed, surviving until their transaction completes.
- **FR-009**: During shutdown, non-root transacted connections MUST be destroyed on return, same as non-transacted connections.
- **FR-010**: Connections in the transacted pool MUST be destroyed (not returned to the idle pool) when their `TransactionCompleted` event fires after shutdown.

### Key Entities

- **Pool State**: A two-value state (Running, ShuttingDown) that controls connection routing. All pool operations check this state to determine behavior.
- **Idle Channel**: The `Channel<T>` that holds idle connections. On shutdown, the writer is completed, which drains buffered connections and unblocks readers.
- **Stasis**: A holding state for transaction root connections during shutdown. The connection remains alive until its transaction completes, then is destroyed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After shutdown, zero idle connections remain in the pool — all are drained and disposed.
- **SC-002**: Callers waiting on the idle channel are unblocked within milliseconds of shutdown, not at their timeout expiry.
- **SC-003**: No background timer callbacks execute after shutdown completes — no timer-related exceptions or resource leaks.
- **SC-004**: Transaction root connections survive pool shutdown and are only destroyed after their transaction completes.

## Assumptions

- The `State` property and `DbConnectionPoolState` enum (Running, ShuttingDown) already exist in the codebase. No new states are needed.
- `ReturnInternalConnection` already checks `State == ShuttingDown` and routes to `RemoveConnection`. This existing logic is correct and relied upon.
- Channel writer completion (`TryComplete`) causes waiting `ReadAsync` callers to wake up and receive channel closed behavior. This is standard `System.Threading.Channels` semantics.
- Shutdown is called by `SqlConnectionFactory.QueuePoolForRelease()`, which handles queueing the pool for eventual disposal. Shutdown itself does not dispose the pool object.
- Transaction-aware shutdown (Story 5, FR-008/009/010) depends on the transaction support feature being implemented. If transactions are not yet available, shutdown falls back to destroying all returned connections (basic shutdown).
- Startup (`Startup()`) is the counterpart to Shutdown and will be implemented as part of this feature. It initializes background timers and may trigger warmup.

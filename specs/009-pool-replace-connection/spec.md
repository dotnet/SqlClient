# Feature Specification: Connection Replacement

**Feature Directory**: `009-pool-replace-connection`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "replace connection"

## User Scenarios & Testing

### User Story 1 — Transparent Connection Replacement on Transient Failure (Priority: P1)

When a pooled connection experiences a transient failure during use and the driver's internal retry logic fires (ConnectRetryCount > 0), the pool swaps the broken connection for a fresh one without the caller needing to close and re-open their connection. The application's retry succeeds transparently with a new underlying connection.

**Why this priority**: This is the entire purpose of connection replacement — enabling the driver's built-in connection resiliency feature. Without it, the Channel pool cannot support ConnectRetryCount-based retry, which is a core SQL Server connectivity feature.

**Independent Test**: Can be fully tested by configuring a connection with ConnectRetryCount > 0, simulating a transient failure on the underlying connection, and verifying that the driver transparently replaces the connection and the retry succeeds.

**Acceptance Scenarios**:

1. **Given** a pooled connection encounters a transient failure and ConnectRetryCount > 0, **When** the retry logic fires, **Then** the pool creates a new connection and replaces the broken one.
2. **Given** a replacement connection is created, **When** the replacement completes, **Then** the caller's connection object is now backed by the new underlying connection.
3. **Given** a replacement connection is created, **When** the old connection is replaced, **Then** the old connection is fully cleaned up (deactivated and disposed).

---

### User Story 2 — Transaction Preservation During Replacement (Priority: P1)

When a connection is replaced and the caller has an ambient transaction, the replacement connection is automatically enlisted in the same transaction. The caller's transactional context is preserved without any action on their part.

**Why this priority**: Many applications use connection replacement within transactional contexts. If the replacement connection is not enlisted in the ambient transaction, data integrity is compromised. This must ship alongside Story 1.

**Independent Test**: Can be tested by opening a connection inside a transaction scope, triggering a transient failure and replacement, and verifying the replacement connection is enlisted in the same transaction.

**Acceptance Scenarios**:

1. **Given** a connection is being replaced and the old connection is enlisted in a transaction, **When** the replacement completes, **Then** the new connection is enlisted in the same transaction.
2. **Given** a connection is being replaced and there is no ambient transaction, **When** the replacement completes, **Then** the new connection is clean with no transaction enlistment.
3. **Given** a connection is being replaced within a transaction, **When** the replacement succeeds, **Then** the caller can continue issuing commands on the same transaction without interruption.

---

### User Story 3 — Pool Capacity Preservation During Replacement (Priority: P1)

When a connection is replaced, the replacement reuses the old connection's pool slot. The pool's total connection count does not change — one connection is removed and one is added. The pool does not temporarily exceed or undercount its capacity.

**Why this priority**: If replacement caused temporary capacity changes, the pool could exceed MaxPoolSize or starve other callers during the replacement window. Capacity correctness is fundamental to pool integrity and must ship alongside Story 1.

**Independent Test**: Can be tested by filling a pool to MaxPoolSize, triggering a replacement, and verifying the pool count remains at MaxPoolSize throughout (never exceeds or drops).

**Acceptance Scenarios**:

1. **Given** the pool is at capacity, **When** a connection is replaced, **Then** the pool count remains unchanged.
2. **Given** a replacement is in progress, **When** the new connection is created, **Then** the total pool count does not temporarily exceed MaxPoolSize.

---

### User Story 4 — Replacement Failure Propagation (Priority: P2)

If creating the replacement connection fails (e.g., server still unreachable), the error is propagated to the caller. The old connection is disposed, and the pool slot is released so capacity is not leaked.

**Why this priority**: Important for correct error handling, but the primary success path (Story 1) must work first. Failure handling can be layered on.

**Independent Test**: Can be tested by making the server unreachable, triggering a replacement, and verifying the error propagates to the caller and the pool slot is freed.

**Acceptance Scenarios**:

1. **Given** a replacement is attempted, **When** creating the new connection fails, **Then** the error is propagated to the caller as an exception.
2. **Given** a replacement fails, **When** the error is propagated, **Then** the pool slot occupied by the old connection is released (no capacity leak).
3. **Given** a replacement fails, **When** the pool is queried, **Then** the pool is not in an error state — the failure is isolated to that single replacement attempt.

---

### User Story 5 — Replacement with Activation Failure Rollback (Priority: P2)

If a replacement connection is successfully created but fails during activation (e.g., transaction enlistment fails), the new connection is returned to the pool rather than leaked. The error is propagated to the caller.

**Why this priority**: Handles a narrow failure window between connection creation and activation. Important for resource safety but less common than creation failure.

**Independent Test**: Can be tested by simulating a transaction enlistment failure after the new connection is created, and verifying the new connection is returned to the pool and the error propagates.

**Acceptance Scenarios**:

1. **Given** a new replacement connection is created but activation fails, **When** the error occurs, **Then** the new connection is returned to the pool (not leaked).
2. **Given** activation fails during replacement, **When** the error is propagated, **Then** the caller receives the activation exception.

---

### Edge Cases

- What happens if ReplaceConnection is called when the pool is shut down? The replacement should fail gracefully — no new connection is created.
- What happens if the old connection's transaction has already been rolled back? The replacement connection is created clean with no transaction enlistment.
- What happens if ReplaceConnection is called on a non-pooled connection? The non-pooled path handles replacement separately through a continuation-based mechanism, not through the pool's ReplaceConnection method.
- What happens if the old connection is already disposed when ReplaceConnection is called? The replacement should still create a new connection; the old connection's disposal is a no-op if already disposed.
- What happens if ConnectRetryCount is 0? ReplaceConnection is never called — the retry path is not triggered.

## Requirements

### Functional Requirements

- **FR-001**: The pool MUST replace a broken connection with a new one when requested by the driver's retry logic.
- **FR-002**: The replacement connection MUST be enlisted in the old connection's ambient transaction if one exists.
- **FR-003**: If no ambient transaction exists, the replacement connection MUST be activated cleanly with no transaction enlistment.
- **FR-004**: The old connection MUST be deactivated and disposed after the replacement connection is prepared.
- **FR-005**: The pool's total connection count MUST remain unchanged during a successful replacement (slot reuse).
- **FR-006**: If creating the replacement connection fails, the error MUST propagate to the caller and the pool slot MUST be released.
- **FR-007**: If the replacement connection is created but activation fails, the new connection MUST be returned to the pool (not leaked) and the error MUST propagate to the caller.
- **FR-008**: Replacement MUST record a soft connect metric (not a hard connect) since the caller's connection object is reused.
- **FR-009**: Replacement MUST be traced via the pool's event source for diagnostic visibility.

### Key Entities

- **Replacement Request**: A request from the driver's retry logic to swap a broken connection for a fresh one. Initiated when ConnectRetryCount > 0 and a transient failure triggers the ForceNewConnection flag.
- **Old Connection**: The broken connection being replaced. Its ambient transaction (if any) is transferred to the replacement. It is deactivated and disposed after the replacement is prepared.
- **Replacement Connection**: The new connection created by the pool to replace the broken one. Enlisted in the old connection's transaction and activated before the old connection is cleaned up.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Applications using ConnectRetryCount > 0 experience transparent connection recovery on transient failures without application-level retry code.
- **SC-002**: Transactional operations survive connection replacement — no transaction state is lost during the swap.
- **SC-003**: Pool capacity is never leaked or exceeded during replacement — the pool count before and after replacement is identical.
- **SC-004**: Replacement failures do not leave orphaned connections or leaked pool slots.

## Assumptions

- The driver's retry logic (ConnectRetryCount/ConnectRetryInterval) triggers replacement by setting the ForceNewConnection flag on the SqlConnection; the pool does not independently decide to replace connections.
- The old connection's EnlistedTransaction property provides access to the ambient transaction that must be transferred to the replacement.
- Connection creation during replacement uses the same factory path as user-initiated connection creation (UserCreateRequest or equivalent).
- The non-pooled replacement path (CreateReplaceConnectionContinuation) is separate from the pool's ReplaceConnection method and is out of scope for this feature.
- PrepareForReplaceConnection is a virtual hook with a default no-op; no SQL-specific override currently exists, but the hook must be called for forward compatibility.
- The pool's PrepareConnection method (or equivalent) handles PostPop, transaction enlistment via ActivateConnection, and error rollback.

# Feature Specification: Connection Timeout Awareness

**Feature Directory**: `.github/plans/connection-pool/connection-timeout`  
**Created**: 2025-07-18  
**Status**: Draft  
**Input**: User description: "connection timeout"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Connection Timeout Enforcement (Priority: P1)

A developer opens a SQL connection. The total time spent acquiring a connection—including waiting for a pool slot, waiting for an idle connection, and creating a new connection—must not exceed the configured connection timeout. If any stage causes the total time to exceed the timeout, the open fails with a timeout error immediately, regardless of which internal phase the pool was in.

**Why this priority**: This is the fundamental correctness guarantee of timeout behavior. Without it, applications can hang indefinitely when the pool is exhausted or the server is slow to respond.

**Independent Test**: Open connections until the pool is exhausted, then attempt one more open. Verify that the call fails with a timeout error within `ConnectTimeout` seconds (±tolerance), not sooner and not significantly later.

**Acceptance Scenarios**:

1. **Given** a pool at max capacity with all connections in use, **When** a new connection is requested, **Then** the request fails with a timeout error after approximately `ConnectTimeout` seconds.
2. **Given** a pool with available capacity but the server is slow to respond, **When** a connection is requested, **Then** the request fails with a timeout error after approximately `ConnectTimeout` seconds.
3. **Given** a pool with idle connections available, **When** a connection is requested, **Then** the connection is returned well within the timeout period with no unnecessary delay.
4. **Given** a connection request is in progress, **When** the timeout elapses during connection creation, **Then** the in-flight creation attempt is cancelled and a timeout error is raised.

---

### User Story 2 - Multiple Concurrent Timeout Requests (Priority: P2)

When many callers are simultaneously waiting for connections and the pool is exhausted or the server is down, each caller independently times out based on its own start time. One caller's timeout does not interfere with another caller's remaining wait time. Callers that started earlier time out first; callers that started later get their full configured timeout.

**Why this priority**: This is a correctness refinement for high-concurrency scenarios. The basic timeout (P1) must work correctly per-caller even under concurrent load. Ranked P3 because it validates concurrent correctness rather than adding new functionality.

**Independent Test**: Exhaust the pool. Start 10 concurrent connection requests at staggered intervals (e.g., 500ms apart). Verify each times out relative to its own start time, not relative to the first request's start time.

**Acceptance Scenarios**:

1. **Given** multiple callers are waiting for connections, **When** the timeout elapses for caller A, **Then** only caller A receives a timeout error; other callers continue waiting if their timeout has not elapsed.
2. **Given** a connection becomes available while multiple callers are waiting, **When** the connection is returned, **Then** the longest-waiting caller receives it (FIFO ordering).

---

### Edge Cases

- What happens when `ConnectTimeout=0` (infinite timeout)? The request waits indefinitely until a connection is available or an unrecoverable error occurs; no timeout is applied.


## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pool MUST enforce a single overall timeout (derived from `ConnectTimeout`) across the entire connection acquisition path: slot acquisition, idle connection retrieval, and new connection creation.
- **FR-002**: The pool MUST propagate a single cancellation token (sourced from the connection timeout) through all internal wait operations so that cancellation of any stage is immediate.
- **FR-003**: The pool MUST raise a timeout error (consistent with the existing `ADP.PooledOpenTimeout()` behavior) when the overall timeout elapses, regardless of which internal phase was in progress.
- **FR-004**: Multiple concurrent callers MUST each have independent timeout tracking; one caller's timeout MUST NOT affect another caller's remaining wait time.

> **Deferred to Rate Limiting feature**: Blocking period (`PoolBlockingPeriod`), error state (`ErrorOccurred`), and exponential backoff recovery are out of scope for this feature. They will be specified and implemented as part of the rate limiting feature.
>
> **Deferred to Connection Replacement feature**: `ReplaceConnection` (transient error retry with connection swap) is out of scope for this feature. See `.github/plans/connection-pool/replace-connection/`.

### Key Entities

- **Cancellation Token**: A per-request token derived from `ConnectTimeout` that flows through all pool wait operations and connection creation. Represents the overall time budget for a single connection acquisition.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Connection requests that exceed the configured timeout fail within 10% of the expected timeout duration (e.g., a 15-second timeout fails between 15.0 and 16.5 seconds).
- **SC-002**: Under concurrent load, each caller's timeout is independent — no caller waits longer than its configured `ConnectTimeout`.

## Assumptions

- The existing `ConnectTimeout` connection string keyword and `SqlConnectionStringBuilder.ConnectTimeout` property provide the timeout value. No new connection string keywords are required for timeout behavior.
- `ReplaceConnection` is deferred to its own feature. See `.github/plans/connection-pool/replace-connection/`.
- Blocking period (`PoolBlockingPeriod`), error state (`ErrorOccurred`), and exponential backoff recovery are deferred to the rate limiting feature specification.

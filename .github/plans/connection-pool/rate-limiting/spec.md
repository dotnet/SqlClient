# Feature Specification: Connection Open Rate Limiting

**Feature Directory**: `.github/plans/connection-pool/rate-limiting`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "rate limiting"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Throttled Connection Creation Under Burst Demand (Priority: P1)

A developer's application experiences a sudden burst of concurrent requests after a quiet period. The pool has few or no idle connections, so many callers simultaneously attempt to create new physical connections. Without throttling, all callers race to open connections at once, overwhelming the SQL Server with concurrent login storms. With rate limiting, the pool serializes or limits how many new connections can be created concurrently. Callers that cannot immediately create a connection wait (within their timeout budget) until the rate limiter allows them to proceed. The server receives a controlled flow of connection requests rather than an unbounded spike.

**Why this priority**: This is the core value of the feature. Unbounded concurrent creation is the primary risk of the new channel-based pool compared to the WaitHandle pool (which serialized creation via a binary semaphore). Without this, the new pool can degrade server performance under burst conditions.

**Independent Test**: Configure a pool with `MaxPoolSize=100` and no idle connections. Simultaneously open 50 connections. Verify that no more than the configured concurrency limit (e.g., 10) are being created at any instant. Verify all 50 eventually succeed within the timeout.

**Acceptance Scenarios**:

1. **Given** the pool has no idle connections and many callers request connections simultaneously, **When** the concurrency limit is reached, **Then** additional callers wait until an in-flight creation completes before starting their own.
2. **Given** a caller is waiting for the rate limiter, **When** its connection timeout elapses, **Then** the caller receives a timeout error without ever attempting to create a connection.
3. **Given** the rate limiter has available capacity, **When** a caller requests a new connection, **Then** the create proceeds immediately with no added latency.
4. **Given** a connection creation completes (success or failure), **When** capacity is released, **Then** the next waiting caller is allowed to proceed.

---

### User Story 2 - Blocking Period Fast-Fail on Connection Failure (Priority: P1)

When a connection creation attempt fails because the server is unreachable, a developer does not want every subsequent caller to independently attempt (and fail) connection creation, each consuming the full connection timeout. Instead, the pool enters an error state and immediately fails subsequent requests for a limited period, returning the most recent error. This "blocking period" prevents cascading timeouts and allows the application to fail fast and recover quickly once the server comes back.

**Why this priority**: Without blocking period, a server outage causes every concurrent caller to wait the full timeout before failing. This multiplies failure latency across all callers and can cascade into application-wide unresponsiveness. Blocking period is critical for production reliability and backwards compatibility with the WaitHandle pool.

**Independent Test**: Configure a pool pointing at an unreachable server with `PoolBlockingPeriod=AlwaysBlock`. Open a connection (expect timeout after creation failure). Immediately attempt a second open and verify it fails instantly with the cached error, not after another full timeout.

**Acceptance Scenarios**:

1. **Given** a connection creation failure has occurred and blocking period is enabled, **When** a new connection is requested within the blocking window, **Then** the request fails immediately with the same error that caused the block.
2. **Given** a connection creation failure has occurred and blocking period is enabled, **When** the blocking window expires, **Then** the next connection request attempts a fresh connection creation rather than failing immediately.
3. **Given** `PoolBlockingPeriod=NeverBlock`, **When** a connection creation failure occurs, **Then** each subsequent request independently attempts connection creation (no fast-fail behavior).
4. **Given** `PoolBlockingPeriod=Auto`, **When** connecting to an Azure SQL endpoint and a failure occurs, **Then** no blocking period is applied (same as NeverBlock).
5. **Given** `PoolBlockingPeriod=Auto`, **When** connecting to an on-premises SQL Server and a failure occurs, **Then** blocking period is applied (same as AlwaysBlock).

---

### User Story 3 - Error State Recovery with Exponential Backoff (Priority: P2)

When the pool enters an error state due to a connection creation failure, it periodically retries connection creation in the background with increasing delays between attempts (exponential backoff) rather than hammering the server. Once a retry succeeds, the error state clears and all subsequent connection requests proceed normally. This protects recovering servers from being overwhelmed by reconnection storms.

**Why this priority**: Builds on the blocking period (P1) to add graceful recovery. The blocking period establishes fast-fail; this story adds the mechanism that clears the error state. Ranked P2 because blocking period is functional without automatic recovery (callers can retry after a delay), but automatic recovery significantly improves the experience.

**Independent Test**: Point a pool at an unreachable server. Wait for the pool to enter error state. Make the server reachable again. Verify the pool automatically clears the error state and subsequent connection requests succeed without requiring application intervention.

**Acceptance Scenarios**:

1. **Given** the pool is in error state, **When** the backoff timer fires and a retry connection creation succeeds, **Then** the error state is cleared and subsequent requests attempt normal connection creation.
2. **Given** the pool is in error state, **When** the backoff timer fires and the retry fails, **Then** the backoff interval increases (up to a cap) and the pool remains in error state.
3. **Given** the pool is in error state, **When** the error is cleared, **Then** all accumulated error context (cached exception, backoff interval) is reset.

---

### User Story 4 - Rate Limiting Counts Against Connection Timeout (Priority: P2)

A developer configures a connection timeout. When a caller is waiting for the rate limiter to allow connection creation, the waiting time counts against the caller's overall connection timeout budget. The caller does not get extra time because of rate limiting. If the timeout expires while waiting for rate limiting, the caller receives a timeout error immediately without ever starting a connection creation attempt.

**Why this priority**: This ensures timeout behavior remains consistent and predictable regardless of rate limiting. Ranked P2 because the basic rate limiting (P1) must work first; timeout integration is a correctness refinement that prevents callers from hanging unexpectedly.

**Independent Test**: Configure a pool with a strict rate limit (e.g., 1 concurrent create) and a short timeout (e.g., 5 seconds). Start a long-running connection creation. Immediately start a second connection open. Verify the second caller times out after 5 seconds total (not 5 seconds after the rate limiter releases).

**Acceptance Scenarios**:

1. **Given** a caller's connection timeout is 15 seconds and the caller waits 10 seconds for rate limiting, **When** the rate limiter releases, **Then** the remaining timeout budget for connection creation is 5 seconds.
2. **Given** a caller's connection timeout expires while waiting for the rate limiter, **When** the timeout fires, **Then** the caller receives a timeout error and is removed from the rate limiter queue.

---

### User Story 5 - Pluggable Rate Limiter Architecture (Priority: P3)

The rate limiting mechanism is implemented behind an abstraction so that it can be changed or tuned in the future without modifying the pool's core connection acquisition logic. The initial implementation uses a concurrency-based approach (limiting the number of simultaneous connection creation attempts). Future implementations could add temporal rate limiting, adaptive backoff, or user-configurable strategies.

**Why this priority**: Ranked P3 because the first implementation (concurrency limit) is sufficient for launch. The abstraction enables future evolution without redesigning the pool, but the abstraction itself is only valuable if the first concrete implementation works correctly.

**Independent Test**: Verify that the pool delegates connection creation throttling to the rate limiter abstraction. Replace the rate limiter with a test implementation that tracks calls and verify the pool interacts with it correctly.

**Acceptance Scenarios**:

1. **Given** the pool is configured with the default rate limiter, **When** connections are created, **Then** the concurrency-based rate limiter throttles creation.
2. **Given** a different rate limiter implementation is injected, **When** connections are created, **Then** the pool delegates throttling to the injected implementation.

---

### Edge Cases

- What happens when the rate limiter allows a creation and it fails? The rate limiter releases its capacity so the next waiter can proceed. If blocking period is enabled, the error state is entered.
- What happens when `ClearPool` is called while the pool is in error state? The error state is cleared, and subsequent requests attempt fresh connection creation.
- What happens when a connection creation times out while the pool is also in error state? The timeout takes precedence; the caller receives a timeout error, not the cached error-state exception.
- What happens when warmup requests go through the rate limiter? Warmup respects the rate limiter like any other creation request. Warmup does not bypass throttling.
- What happens when all rate limiter capacity is consumed by long-running creates? Other callers wait (subject to their timeout). Once any creation completes (success or failure), capacity is released.
- What happens when a blocking period timer fires at the same instant a caller's timeout expires? The timeout takes precedence; the caller receives a timeout error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pool MUST limit the number of concurrent physical connection creation attempts to a configurable maximum.
- **FR-002**: Callers that cannot immediately create a connection due to rate limiting MUST wait in order (FIFO) until capacity is available or their timeout expires.
- **FR-003**: Time spent waiting for rate limiter capacity MUST count against the caller's overall connection timeout budget.
- **FR-004**: When a connection creation attempt completes (success or failure), the rate limiter MUST release capacity so the next waiting caller can proceed.
- **FR-005**: The pool MUST support three `PoolBlockingPeriod` modes: `Auto`, `AlwaysBlock`, and `NeverBlock`.
- **FR-006**: When blocking period is enabled (AlwaysBlock, or Auto for on-premises servers), the pool MUST enter an error state after a connection creation failure and immediately fail subsequent requests with the cached error.
- **FR-007**: When blocking period is disabled (NeverBlock, or Auto for Azure endpoints), the pool MUST NOT enter an error state; each request MUST independently attempt connection creation.
- **FR-008**: While in error state, the pool MUST retry connection creation in the background using exponential backoff (starting at 5 seconds, doubling each attempt, capped at 60 seconds).
- **FR-009**: When a background retry succeeds, the pool MUST clear the error state and reset backoff state so that subsequent requests proceed with normal connection creation.
- **FR-010**: The `ErrorOccurred` property MUST return `true` when the pool is in error state and `false` otherwise.
- **FR-011**: `ClearPool` MUST clear the error state in addition to invalidating pooled connections.
- **FR-012**: The rate limiter MUST be implemented behind an abstraction (interface or strategy) to allow future replacement without modifying pool logic.
- **FR-013**: The initial rate limiter implementation MUST use a concurrency-based approach (e.g., semaphore with configurable permit count).

### Key Entities

- **Rate Limiter**: An abstraction that controls how many connection creation attempts can proceed concurrently. The pool acquires a permit before creating a connection and releases it when creation completes. Callers queue in FIFO order.
- **Error State**: Represents the pool-wide condition when connection creation has failed and blocking period is active. Attributes: active flag, cached exception, current backoff interval, backoff timer.
- **Backoff Timer**: A timer that fires at exponentially increasing intervals (5s, 10s, 20s, 30s, 60s cap) to attempt recovery from error state by retrying connection creation.
- **Blocking Period Mode**: A per-pool setting (`PoolBlockingPeriod`) that controls whether error state fast-fail is active. Three modes: Auto (heuristic), AlwaysBlock, NeverBlock.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Under burst demand, the number of concurrent connection creation attempts never exceeds the configured limit.
- **SC-002**: When blocking period is active, subsequent connection requests after the first failure fail in under 100 milliseconds (fast-fail).
- **SC-003**: After a server recovers from an outage, the pool automatically resumes serving connections within the backoff cap duration (60 seconds) without requiring application intervention.
- **SC-004**: Time spent waiting for rate limiter capacity is included in the caller's total connection timeout — no callers wait longer than their configured `ConnectTimeout`.

## Assumptions

- The `PoolBlockingPeriod` connection string keyword is already parsed and available in the connection options. No new connection string keywords are required for blocking period behavior.
- The initial concurrency limit for the rate limiter will be determined during design. The WaitHandle pool used a limit of 1 (binary semaphore); a higher default may be appropriate for modern workloads.
- The exponential backoff sequence (5s, 10s, 20s, 30s, 60s cap) matches the existing WaitHandle pool behavior and is not user-configurable.
- The `Auto` blocking period mode uses the same heuristic as the existing WaitHandle pool to distinguish Azure endpoints from on-premises servers (inspects the data source for known Azure DNS suffixes).
- Rate limiting does not apply to `ReplaceConnection` — replacement creates bypass the rate limiter because they are already holding a pool slot and must not deadlock waiting for capacity.
- The rate limiter abstraction is internal (not public API). It may be made extensible in the future.

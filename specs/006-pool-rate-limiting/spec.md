# Feature Specification: Pool Rate Limiting and Blocking Period

**Feature Branch**: `dev/mdaigle/pool-rate-limit`
**Created**: 2026-05-19
**Status**: Draft
**Input**: ADO Work Item 37824 — "Implement connection open rate limiting"

## Description

Add rate limiting to `ChannelDbConnectionPool` to control how many physical connections can be
created concurrently. Without throttling, a burst of concurrent requests can trigger a login
storm against SQL Server. The implementation uses
`System.Threading.RateLimiting.ConcurrencyLimiter` from the BCL — no custom rate limiting
primitives are defined.

This feature also adds the `PoolBlockingPeriod` error state (fast-fail after a connection
creation failure) with exponential backoff recovery, matching the existing
`WaitHandleDbConnectionPool` behavior.

> Time spent waiting for the rate limiter counts against the caller's overall `ConnectTimeout`
> budget. `ReplaceConnection` (when implemented) MUST bypass the rate limiter: it already holds
> a pool slot and must not deadlock.

## User Scenarios & Testing

### User Story 1 — Throttled Connection Creation Under Burst Demand (P1)

The pool limits the number of simultaneous physical connection creation attempts. Callers that
cannot immediately create a connection wait in FIFO order until the limiter allows them to
proceed, subject to their `ConnectTimeout`.

**Acceptance Scenarios**:

1. **Given** the pool has no idle connections and many callers request connections simultaneously,
   **When** the concurrency limit is reached, **Then** additional callers wait until an in-flight
   creation completes before starting their own.
2. **Given** a caller is waiting for the rate limiter, **When** its `ConnectTimeout` elapses,
   **Then** the caller receives a timeout error without ever attempting to create a connection.
3. **Given** the rate limiter has available capacity, **When** a caller requests a new connection,
   **Then** the create proceeds immediately with no added latency.
4. **Given** a connection creation completes (success or failure), **When** the `RateLimitLease`
   is disposed, **Then** the next waiting caller is allowed to proceed.

---

### User Story 2 — Blocking Period Fast-Fail on Connection Failure (P1)

When a connection creation attempt fails because the server is unreachable, the pool enters an
error state and immediately fails subsequent requests for a limited period, returning the cached
error. This prevents cascading timeouts when the server is down.

**Acceptance Scenarios**:

1. **Given** a creation failure has occurred and blocking period is enabled, **When** a new
   connection is requested within the blocking window, **Then** the request fails immediately
   with the cached error.
2. **Given** a creation failure has occurred and blocking period is enabled, **When** the
   blocking window expires, **Then** the next request attempts fresh connection creation.
3. **Given** `PoolBlockingPeriod=NeverBlock`, **When** a creation failure occurs, **Then** each
   subsequent request independently attempts creation (no fast-fail).
4. **Given** `PoolBlockingPeriod=Auto` connecting to an Azure SQL endpoint and a failure occurs,
   **Then** no blocking period is applied (same as `NeverBlock`).
5. **Given** `PoolBlockingPeriod=Auto` connecting to an on-premises SQL Server and a failure
   occurs, **Then** the blocking period is applied (same as `AlwaysBlock`).

---

### User Story 3 — Error State Recovery with Exponential Backoff (P2)

While in the error state the pool waits using exponential backoff (5s → 10s → 20s → 30s → 60s
cap) before allowing the next attempt. Once an attempt after the backoff succeeds, the error
state clears and backoff resets.

**Acceptance Scenarios**:

1. **Given** the pool is in error state, **When** the backoff timer fires and the next caller's
   attempt succeeds, **Then** the error state is cleared and subsequent requests attempt normal
   creation.
2. **Given** the pool is in error state, **When** the backoff timer fires and the next caller's
   attempt fails, **Then** the backoff interval increases (up to the 60s cap) and the pool
   re-enters the error state.
3. **Given** the pool is in error state, **When** the error is cleared, **Then** the cached
   exception, the error flag, and the backoff interval are all reset.

---

### User Story 4 — Rate Limiting Counts Against Connection Timeout (P2)

Time spent waiting for rate limiter capacity counts against the caller's overall
`ConnectTimeout` budget.

**Acceptance Scenarios**:

1. **Given** a caller's timeout is 15s and the caller waits 10s for rate limiting, **When** the
   rate limiter releases, **Then** the remaining budget for connection creation is 5s.
2. **Given** a caller's timeout expires while waiting for the rate limiter, **When** the timeout
   fires, **Then** the caller receives a timeout error and is removed from the limiter queue.

---

### User Story 5 — Rate Limiting Built on a Concurrency Limiter (P3)

The pool supports an optional `System.Threading.RateLimiting.ConcurrencyLimiter` to throttle
concurrent physical connection creation. This is the only limiter type the pool currently needs
(pooling against on-prem SQL Server), so the pool takes a concrete `ConcurrencyLimiter?` rather
than the abstract `RateLimiter` base. Support for other limiter types can be added later if a
concrete need arises. When no limiter is supplied (`null`), no rate limiting is applied.

**Acceptance Scenarios**:

1. **Given** the pool is configured with a `ConcurrencyLimiter`, **When** connections
   are created, **Then** the limiter throttles concurrent creation to the configured maximum.
2. **Given** no limiter is supplied (`null`), **When** connections are created, **Then** the
   pool applies no rate limiting.

---

## Functional Requirements

- **FR-001**: The pool MUST limit the number of concurrent physical connection creation attempts
  to a configurable maximum.
- **FR-002**: Callers that cannot immediately create a connection due to rate limiting MUST wait
  in FIFO order until capacity is available or their timeout expires.
- **FR-003**: Time spent waiting for rate limiter capacity MUST count against the caller's
  overall connection timeout budget.
- **FR-004**: When a connection creation attempt completes (success or failure), the
  `RateLimitLease` MUST be disposed so the next waiting caller can proceed.
- **FR-005**: The pool MUST support three `PoolBlockingPeriod` modes: `Auto`, `AlwaysBlock`, and
  `NeverBlock`.
- **FR-006**: When the blocking period is enabled, the pool MUST enter an error state after a
  creation failure and immediately fail subsequent requests with the cached error.
- **FR-007**: When the blocking period is disabled, the pool MUST NOT enter an error state;
  each request MUST independently attempt creation.
- **FR-008**: While in error state, the backoff MUST use exponential growth starting at 5s,
  doubling each attempt, capped at 60s.
- **FR-009**: When an attempt succeeds, the pool MUST clear the error state and reset the
  backoff to its initial value.
- **FR-010**: The `ErrorOccurred` property MUST return `true` when in the error state and
  `false` otherwise.
- **FR-011**: `ClearPool` MUST clear the error state in addition to invalidating pooled
  connections.
- **FR-012**: The rate limiter MUST be an optional `System.Threading.RateLimiting.ConcurrencyLimiter`.
  When no limiter is supplied (`null`), the pool MUST apply no rate limiting. Support for other
  `RateLimiter` types is intentionally out of scope for now and may be added later if needed.
- **FR-013**: When a limiter is supplied, it MUST be a
  `System.Threading.RateLimiting.ConcurrencyLimiter` configured with the desired maximum number
  of concurrent connection creation attempts.

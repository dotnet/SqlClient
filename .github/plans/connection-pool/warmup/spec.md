# Feature Specification: Pool Warmup

**Feature Directory**: `warmup`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "warmup"

## User Scenarios & Testing

### User Story 1 — Background Warmup on Pool Creation (Priority: P1)

When a connection pool is first created, the pool pre-creates connections up to the configured minimum pool size in the background. This ensures that early application requests find ready-to-use connections rather than waiting for on-demand creation, reducing latency during the critical startup window.

**Why this priority**: Without warmup, every connection in the early burst of requests must be created on demand, adding connection setup latency to each request. Warmup is the core value proposition — everything else in this feature depends on it.

**Independent Test**: Can be fully tested by creating a pool with a minimum pool size of 5, waiting briefly, and verifying that 5 connections exist in the pool before any user request is made.

**Acceptance Scenarios**:

1. **Given** a pool is created with minimum pool size of N, **When** startup completes, **Then** the pool begins creating connections in the background up to N.
2. **Given** a pool is warming up, **When** a user opens a connection before warmup finishes, **Then** the user request is served immediately (either from an already-warmed connection or via on-demand creation) without waiting for warmup to complete.
3. **Given** a pool is warming up, **When** warmup creates each connection, **Then** each connection is created one at a time (serially), not in parallel.

---

### User Story 2 — Warmup Through Shared Rate Limiter (Priority: P1)

Warmup creates connections through the same rate-limiting mechanism as user-initiated requests. This prevents warmup from overwhelming the server and ensures fair resource sharing between warmup and concurrent user requests during the startup window.

**Why this priority**: Without rate limiter integration, warmup could monopolize connection creation or hammer the server with simultaneous open attempts. This is essential for production safety and must ship alongside Story 1.

**Independent Test**: Can be tested by configuring a pool with a minimum pool size of 10, observing that warmup connection creation respects the same concurrency constraints as user-initiated requests, and verifying that concurrent user requests are not starved by warmup.

**Acceptance Scenarios**:

1. **Given** warmup is creating connections, **When** a user request also needs a new connection, **Then** both warmup and user requests compete fairly through the shared rate limiter.
2. **Given** the rate limiter is at capacity, **When** warmup attempts to create the next connection, **Then** warmup waits its turn rather than bypassing the limiter.

---

### User Story 3 — Warmup Failure Resilience (Priority: P1)

If a connection fails to open during warmup, the failure is silently absorbed. The pool remains fully operational — user requests create connections on demand as needed. Warmup failures do not propagate exceptions, block pool availability, or put the pool into an error state.

**Why this priority**: Warmup runs before any user interaction. If warmup failures surfaced as exceptions or blocked pool creation, the application could fail to start due to transient server issues. Silent resilience is essential for production reliability.

**Independent Test**: Can be tested by simulating a server that rejects connections during warmup, then verifying the pool still serves user requests normally via on-demand creation once the server recovers.

**Acceptance Scenarios**:

1. **Given** warmup is in progress, **When** a connection fails to open, **Then** the failure is logged/traced but not propagated as an exception.
2. **Given** warmup fails for all connections, **When** a user subsequently opens a connection, **Then** the user request creates a connection on demand and succeeds normally.
3. **Given** warmup fails partway through (e.g., 3 of 5 connections created), **When** the pool is queried, **Then** the pool has 3 ready connections and user requests create additional connections on demand.
4. **Given** warmup fails, **When** the pool's error state is checked, **Then** the pool is NOT in an error state — warmup failures do not trigger the pool-level error/blocking-period mechanism.

---

### User Story 4 — Warmup Cancellation on Shutdown (Priority: P2)

When a pool is shut down while warmup is still in progress, warmup stops promptly. No new connections are created after shutdown begins, and any in-flight warmup connection attempt is cancelled or abandoned cleanly.

**Why this priority**: Important for clean lifecycle management, but only matters in the narrow window where shutdown races with warmup. Not needed for the initial MVP.

**Independent Test**: Can be tested by creating a pool with a large minimum pool size, immediately shutting it down, and verifying that warmup stops and no connections are created after shutdown.

**Acceptance Scenarios**:

1. **Given** warmup is in progress, **When** the pool is shut down, **Then** warmup stops and no further connections are created.
2. **Given** warmup is in progress, **When** the pool is shut down, **Then** any connections already created by warmup are cleaned up as part of normal shutdown.

---

### User Story 5 — Replenishment on Any Below-Minimum Event (Priority: P2)

Whenever the pool count drops below the minimum pool size — whether due to pruning, connection close/destroy (broken connection, lifetime expired), or idle timeout expiry — the pool automatically triggers warmup-style replenishment to restore to the minimum. This ensures the minimum pool size guarantee is maintained over the entire pool lifetime, not just at startup.

**Why this priority**: Ensures the minimum pool size guarantee is maintained over the pool's lifetime, not just at startup. Deferred to P2 because initial warmup (P1) handles the startup case; restoration addresses the steady-state case.

**Independent Test**: Can be tested by creating a pool at minimum pool size, destroying one connection (e.g., returning a broken connection), and verifying the pool creates a replacement to restore to the minimum.

**Acceptance Scenarios**:

1. **Given** pruning has reduced the pool below the minimum pool size, **When** the pruning cycle completes, **Then** the pool queues a replenishment request to create connections up to the minimum.
2. **Given** a connection is destroyed on return to the pool (broken, lifetime expired), **When** the pool count drops below the minimum pool size, **Then** the pool queues a replenishment request.
3. **Given** idle timeout expiry removes a connection, **When** the pool count drops below the minimum pool size, **Then** the pool queues a replenishment request.
4. **Given** replenishment is triggered by any event, **When** connections are created, **Then** they are created serially through the shared rate limiter, identical to initial warmup behavior.
5. **Given** the pool is at or above the minimum pool size, **When** a connection is destroyed or pruned, **Then** no replenishment is triggered.

---

### Edge Cases

- What happens if minimum pool size is 0? Warmup should be a no-op — no connections created.
- What happens if minimum pool size equals maximum pool size? Warmup fills the pool to capacity; no further connections can be created until some are returned.
- What happens if the server is completely unreachable during warmup? All warmup attempts fail silently, pool starts empty, user requests fail with normal connection timeout errors.
- What happens if the pool is cleared (ClearPool) during warmup? Warmup should detect the generation change and stop creating connections for the stale generation.
- What happens if multiple warmup/replenishment requests are queued simultaneously? Only one should execute at a time; duplicate requests are coalesced or serialized.
- What happens if warmup is still running when the first user request arrives? The user request proceeds independently — it either gets an already-warmed connection or creates one on demand.

## Requirements

### Functional Requirements

- **FR-001**: The pool MUST begin creating connections in the background immediately upon startup when minimum pool size is greater than zero.
- **FR-002**: Warmup MUST create connections serially (one at a time), not in parallel.
- **FR-003**: Warmup MUST submit connection creation requests through the same rate-limiting mechanism used by user-initiated requests.
- **FR-004**: Warmup MUST NOT block pool availability — user requests MUST be servable before warmup completes.
- **FR-005**: Warmup connection failures MUST be logged/traced but MUST NOT propagate as exceptions or put the pool into an error state.
- **FR-006**: Warmup MUST stop promptly when the pool is shut down.
- **FR-007**: When any event reduces the pool below minimum pool size (pruning, connection close/destroy, idle timeout expiry), the pool MUST automatically trigger replenishment to restore to the minimum.
- **FR-008**: Replenishment MUST use the same serial, rate-limited creation path as initial warmup, regardless of the trigger.
- **FR-009**: Warmup MUST be a no-op when minimum pool size is zero.
- **FR-010**: Warmup MUST stop if the pool is cleared (generation change) during warmup, avoiding creation of stale-generation connections.
- **FR-011**: Concurrent warmup/replenishment requests MUST be coalesced or serialized so that only one warmup loop executes at a time.

### Key Entities

- **Warmup Request**: A background task that creates connections serially up to the minimum pool size. Triggered on pool startup and after pruning reduces the pool below the minimum.
- **Replenishment Check**: The condition evaluated to determine whether the pool needs more connections — true when the current pool count is below the minimum pool size and the pool is in a running state.
- **Minimum Pool Size**: The configured floor for connection count. Warmup creates connections up to this value; pruning does not reduce below it (timer-based).

## Success Criteria

### Measurable Outcomes

- **SC-001**: The first user request to a warmed pool completes without connection creation latency when warmup has had sufficient time to finish.
- **SC-002**: Warmup does not increase connection creation failures — a pool with warmup enabled has the same or fewer user-visible errors compared to on-demand-only creation.
- **SC-003**: After any event that reduces the pool below minimum pool size, the pool restores to minimum pool size within one warmup pass duration.
- **SC-004**: Warmup and concurrent user requests share rate-limited creation fairly — neither starves the other.

## Assumptions

- Warmup runs as a fire-and-forget background task on the thread pool; no dedicated thread is allocated.
- The rate-limiting mechanism (from the rate-limiting feature) is available and functional before warmup executes.
- Minimum pool size is a static configuration value that does not change over the pool's lifetime.
- Connection creation during warmup uses the same code path as user-initiated connection creation (no special "warmup-only" creation logic).
- Pruning and idle timeout features respect the minimum pool size floor, preventing a warmup → immediate prune cycle.
- The pool's generation counter (from the clear feature) is available for warmup to check, enabling warmup to stop on pool clear.

## Clarifications

### Session 2026-04-10

- Q: Should Story 5 cover only pruning as a replenishment trigger, or all events that reduce the pool below minimum? → A: Replace with unified story covering all triggers (pruning, connection close/destroy, idle timeout expiry).

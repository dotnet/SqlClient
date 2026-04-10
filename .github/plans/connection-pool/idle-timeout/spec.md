# Feature Specification: Configurable Idle Connection Timeout

**Feature Directory**: `.github/plans/connection-pool/idle-timeout`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "idle timeout"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Idle Connection Recycling (Priority: P1)

A developer's application experiences periodic bursts of activity followed by quiet periods. During quiet periods, connections sit idle in the pool. Firewalls, load balancers, or the SQL Server itself silently kill these idle connections after their own inactivity thresholds. When the next burst arrives, the application pulls a dead connection from the pool, receives a connection error, and must retry. With idle connection timeout, the pool automatically recycles connections that have been idle longer than a configured duration, ensuring the pool contains fresh connections when the next burst arrives.

**Why this priority**: This is the core value proposition of the feature. Stale/dead connection errors are a top user pain point (see dotnet/SqlClient#343). Without idle recycling, applications must implement their own retry logic or set aggressive connection lifetimes that penalize active connections too.

**Independent Test**: Configure a pool with a short idle timeout (e.g., 5 seconds). Open a connection, use it, close it (return to pool). Wait longer than the idle timeout. Verify the idle connection has been removed from the pool. Open a new connection and verify it is a freshly created connection, not the stale one.

**Acceptance Scenarios**:

1. **Given** a connection has been idle in the pool longer than the configured idle timeout, **When** the idle timeout check runs, **Then** the connection is removed from the pool and disposed.
2. **Given** a connection is returned to the pool, **When** the idle timeout has not yet elapsed, **Then** the connection remains available in the pool for reuse.
3. **Given** a connection is actively in use by the application (checked out), **When** the idle timeout duration passes, **Then** the connection is NOT affected because it is not idle in the pool.
4. **Given** the idle timeout is set to zero (disabled), **When** connections are returned to the pool, **Then** no idle-based recycling occurs; connections remain indefinitely (subject to other expiry rules like connection lifetime).

---

### User Story 2 - Idle Timeout Distinct from Connection Lifetime (Priority: P1)

A developer uses `Connection Lifetime` (Load Balance Timeout) to rotate connections across servers behind a load balancer. They also want to recycle connections that sit idle too long to avoid firewall-killed connections. These are independent concerns. Connection Lifetime expires connections based on age since creation, regardless of activity. Idle timeout expires connections based on how long they've been sitting unused in the pool. A heavily-used connection should not be recycled due to idle timeout. A recently-created but long-idle connection should be recycled by idle timeout even if its connection lifetime hasn't expired.

**Why this priority**: Users must understand that idle timeout and connection lifetime are orthogonal. Conflating them would either over-recycle active connections or under-recycle idle ones. Both P1 because the feature's usefulness depends on this distinction being clear and correct.

**Independent Test**: Configure a pool with `Connection Lifetime=300` and `Idle Timeout=10`. Open a connection, return it, and wait 15 seconds. Verify the connection is recycled by idle timeout even though its connection lifetime hasn't expired. Separately, open a connection, use it continuously for 15 seconds, return it, and verify it is NOT recycled (it was never idle for 10 seconds).

**Acceptance Scenarios**:

1. **Given** a connection has been idle longer than the idle timeout but is younger than the connection lifetime, **When** the idle check runs, **Then** the connection is recycled by idle timeout.
2. **Given** a connection is older than the connection lifetime but has been recently active, **When** the connection is returned to the pool, **Then** it is recycled by connection lifetime (age), not idle timeout.
3. **Given** both idle timeout and connection lifetime are configured, **When** either threshold is exceeded, **Then** the connection is recycled — whichever fires first.

---

### User Story 3 - Idle Timeout Enforcement: Proactive and Lazy (Priority: P2)

A developer configures `MinPoolSize=5` and the idle timeout. Idle timeout is enforced at two points: proactively by the shared timer during periodic sweeps, and lazily when a connection is retrieved from the pool for a user request. The proactive timer sweep respects MinPoolSize as a floor — it does not remove idle-expired connections if doing so would drop the pool below MinPoolSize. However, the lazy retrieval check ignores MinPoolSize — when a caller requests a connection, any idle-expired connection pulled from the pool is discarded and the next one is tried (or a new one created), because the caller needs a working connection regardless of pool size. This dual approach matches Npgsql's proven behavior: the timer keeps the pool appropriately sized, while the lazy check ensures no caller ever receives a stale connection.

**Why this priority**: Most users don't set MinPoolSize, so the proactive sweep handles the common case. The lazy check is a safety net that guarantees correctness even when MinPoolSize prevents proactive removal. Ranked P2 because the basic idle recycling (P1) works for the majority of workloads.

**Independent Test**: Configure `MinPoolSize=3` and a short idle timeout. Open 3 connections, return them all, and wait for idle expiry. Verify the timer sweep does NOT remove them (pool is at MinPoolSize floor). Then request a connection and verify the pool discards the stale idle connection and creates a fresh one instead.

**Acceptance Scenarios**:

1. **Given** idle connections have expired but the pool is at MinPoolSize, **When** the proactive timer sweep runs, **Then** the expired connections are NOT removed (MinPoolSize floor respected).
2. **Given** idle connections have expired and the pool is above MinPoolSize, **When** the proactive timer sweep runs, **Then** expired connections are removed down to MinPoolSize.
3. **Given** an idle-expired connection is at the front of the idle queue and the pool is at or below MinPoolSize, **When** a caller requests a connection, **Then** the expired connection is discarded and the next valid connection (or a new one) is returned.
4. **Given** the pool has no valid idle connections after lazy checks discard expired ones, **When** a connection request arrives, **Then** a new connection is created on demand.

---

### User Story 4 - Configurable Idle Timeout via Connection String (Priority: P2)

A developer configures the idle timeout through the connection string using a new keyword. The timeout value is specified in seconds. A value of zero disables idle timeout. The default value balances freshness against unnecessary recycling for typical workloads.

**Why this priority**: The mechanism itself (P1) must work first. The configuration surface is P2 because a sensible default means most users don't need to configure it explicitly, but those with specific firewall or server timeout requirements need the knob.

**Independent Test**: Build a connection string with the new idle timeout keyword set to various values (0, 30, 300). Verify the pool respects each value. Verify that an invalid value (negative) is rejected during connection string parsing.

**Acceptance Scenarios**:

1. **Given** the idle timeout keyword is set to a positive value, **When** the pool is created, **Then** idle connections are recycled after that many seconds of inactivity.
2. **Given** the idle timeout keyword is set to zero, **When** the pool is created, **Then** idle timeout recycling is disabled.
3. **Given** the idle timeout keyword is omitted from the connection string, **When** the pool is created, **Then** the default idle timeout value is used.
4. **Given** the idle timeout keyword is set to a negative value, **When** the connection string is parsed, **Then** an argument exception is raised.

---

### Edge Cases

- What happens when a connection is returned to the pool at the exact moment the idle timeout would fire? The connection is treated as freshly returned (idle timer resets on return); it is not immediately expired.
- What happens when idle timeout and pruning both want to remove the same connection? Whichever checks first removes it. The other finds the connection already gone and moves on. No double-dispose occurs.
- What happens when idle-expired connections are below MinPoolSize and a timer sweep runs? The timer respects MinPoolSize and leaves them. They are only discarded lazily when a caller retrieves them.
- What happens when all idle connections expire simultaneously? The pool removes them all. Subsequent requests create new connections on demand, subject to the connection timeout.
- What happens when the idle timeout is shorter than the pruning interval? Idle connections may be removed before pruning samples them. This is expected — idle timeout and pruning are independent mechanisms.
- What happens during pool shutdown? Idle timeout processing stops; the shutdown path handles remaining connections.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pool MUST track the time each connection was last returned to the idle pool (idle start time).
- **FR-002**: The pool MUST remove and dispose connections that have been idle longer than the configured idle timeout.
- **FR-003**: The idle timeout check MUST NOT affect connections that are currently checked out (in use by the application).
- **FR-004**: A new connection string keyword MUST be exposed to configure the idle timeout in seconds.
- **FR-005**: Setting the idle timeout to zero MUST disable idle-based recycling.
- **FR-006**: The pool MUST provide a sensible default idle timeout value that balances connection freshness against unnecessary recycling.
- **FR-007**: Idle timeout MUST operate independently of connection lifetime (Load Balance Timeout). Either threshold expiring causes recycling; a connection must satisfy both to remain pooled.
- **FR-008**: Proactive idle timeout removal (timer sweep) MUST respect MinPoolSize as a floor — the timer MUST NOT remove idle-expired connections if doing so would drop the pool below MinPoolSize.
- **FR-009**: Lazy idle timeout checks on retrieval MUST discard idle-expired connections regardless of MinPoolSize. When a caller requests a connection, any idle-expired connection retrieved from the pool MUST be discarded and the next valid connection returned (or a new one created).
- **FR-010**: Idle timeout checks MUST share a timer with the pruning feature rather than introducing a separate timer.
- **FR-011**: Idle timeout removal MUST NOT cause double-dispose if pruning or another mechanism concurrently removes the same connection.

### Key Entities

- **Idle Start Time**: A per-connection timestamp recording when the connection was last returned to the idle pool. Reset each time the connection is returned. Used to calculate how long the connection has been idle.
- **Idle Timeout Configuration**: A pool-level setting (from the connection string) specifying the maximum duration a connection may remain idle before it is recycled. Zero means disabled.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Connections idle longer than the configured timeout are removed from the pool within one timer interval after expiry.
- **SC-002**: Applications that previously encountered stale connection errors after quiet periods experience zero stale connection errors when idle timeout is configured appropriately.
- **SC-003**: Active connections (checked out and in use) are never recycled by idle timeout, regardless of how long since they were last returned.

## Clarifications

### Session 2026-04-10

- Q: Should idle timeout remove connections below MinPoolSize proactively (timer), lazily (on retrieval), or both? → A: Both — timer respects MinPoolSize floor; lazy retrieval check ignores it (Option B, matches Npgsql).

## Assumptions

- The idle timeout timer is shared with the pruning timer (FR-009). The pruning feature must be implemented first or concurrently.
- The exact connection string keyword name (e.g., `Connection Idle Timeout`, `Idle Connection Lifetime`) will be decided during design. This spec does not prescribe a specific keyword name.
- The default idle timeout value will be determined during design based on common firewall/load balancer timeout thresholds. Candidates include 0 (disabled by default, opt-in) or a value like 300 seconds.
- Warmup/replenishment after idle expiry drops the pool below MinPoolSize is handled by the warmup feature, not this feature. This feature only removes expired connections.
- No new public API surface beyond the connection string keyword and its corresponding `SqlConnectionStringBuilder` property is required.

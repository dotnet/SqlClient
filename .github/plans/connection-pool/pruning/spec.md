# Feature Specification: Pool Pruning

**Feature Branch**: `dev/mdaigle/connection-pool-designs`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "Implement periodic pruning in ChannelDbConnectionPool to close excess idle connections and bring the pool back toward observed usage levels during low-demand periods, using a sampling-based approach modeled after Npgsql's PoolingDataSource."

## User Scenarios & Testing

### User Story 1 - Automatic Pool Size Reduction During Low Demand (Priority: P1)

As an application developer, I want the connection pool to automatically close excess idle connections when demand drops, so that my application doesn't hold unnecessary database connections open during quiet periods.

**Why this priority**: This is the core purpose of pruning — reducing resource consumption when connections are no longer needed. Without this, pools grow to their peak size and never shrink.

**Independent Test**: Can be tested by opening many connections under load, then stopping load and observing that the pool count decreases over the pruning interval.

**Acceptance Scenarios**:

1. **Given** a pool with 50 idle connections and recent usage showing only 10 connections in use, **When** the pruning interval elapses, **Then** the pool closes excess idle connections to approach the observed usage level.
2. **Given** a pool with connections equal to MinPoolSize, **When** the pruning interval elapses, **Then** no connections are pruned (MinPoolSize is the floor).
3. **Given** a pool with high recent usage but currently many idle connections due to a brief lull, **When** the pruning interval elapses, **Then** pruning uses the sampled usage data (not just current idle count) to avoid being too aggressive.

---

### User Story 2 - Configurable Pruning Interval (Priority: P2)

As an application developer, I want to control how frequently the pool evaluates whether to prune idle connections, so that I can tune the trade-off between resource cleanup speed and connection churn for my workload.

**Why this priority**: Different workloads have different idle patterns. A web server with consistent traffic needs less aggressive pruning than a batch job that runs periodically.

**Independent Test**: Can be tested by setting different pruning interval values and observing that pruning occurs at the configured cadence.

**Acceptance Scenarios**:

1. **Given** a pruning interval of 10 seconds (default), **When** the pool has excess idle connections, **Then** pruning is evaluated every 10 seconds.
2. **Given** a pruning interval of 5 minutes, **When** the pool has excess idle connections, **Then** pruning is not evaluated until 5 minutes have passed.
3. **Given** a pruning interval is set to zero or disabled, **When** the pool has excess idle connections, **Then** no automatic pruning occurs.

---

### User Story 3 - Graceful Behavior During Traffic Spikes After Pruning (Priority: P2)

As an application developer, I want the pool to handle traffic spikes after a pruning period without excessive connection open latency, so that my users don't experience degraded performance.

**Why this priority**: Pruning that is too aggressive can cause latency spikes when demand returns. The sampling-based approach should protect against this.

**Independent Test**: Can be tested by running a workload, allowing pruning to reduce the pool, then ramping up load and measuring connection acquisition latency.

**Acceptance Scenarios**:

1. **Given** a pool that has been pruned to match low recent usage, **When** a burst of connection requests arrives, **Then** new connections are created on demand (subject to rate limiting) without failing.
2. **Given** a pool that was recently pruned, **When** new connections are needed, **Then** the time to acquire a connection is bounded by the connection timeout, not by pruning state.

---

### User Story 4 - Pruning Stops on Pool Shutdown (Priority: P3)

As the pool infrastructure, I need pruning to stop when the pool is shutting down, so that shutdown doesn't race with pruning operations.

**Why this priority**: Correctness concern — pruning during shutdown could interfere with orderly connection cleanup.

**Independent Test**: Can be tested by initiating pool shutdown and verifying no pruning timer callbacks fire after shutdown begins.

**Acceptance Scenarios**:

1. **Given** an active pruning timer, **When** the pool transitions to ShuttingDown state, **Then** the pruning timer is cancelled and no further pruning callbacks execute.

---

### Edge Cases

- What happens when all idle connections fail their health check during pruning? The pool should destroy them and allow new ones to be created on demand.
- What happens if pruning removes connections and warmup tries to replenish simultaneously? These should not conflict — warmup adds connections through the normal creation path.
- What happens if the pruning interval is shorter than the time it takes to complete a prune cycle? The timer should not stack overlapping prune operations.

## Requirements

### Functional Requirements

- **FR-001**: System MUST periodically evaluate idle connections for pruning at a configurable interval.
- **FR-002**: System MUST sample the idle connection count at each pruning interval and use the **median** of recent idle count samples to determine the prune target. Connections above the median idle count (floored at MinPoolSize) are candidates for removal. The number of samples retained MUST be `Connection Lifetime / Pruning Interval`, so the sample window covers the full connection lifetime.
- **FR-003**: System MUST NOT prune connections below MinPoolSize.
- **FR-004**: System MUST close pruned connections and release their capacity slots so new connections can be opened.
- **FR-005**: System MUST expose the pruning interval as a connection string keyword with a default value of 10 seconds.
- **FR-006**: System MUST stop pruning when the pool enters ShuttingDown state.
- **FR-007**: System MUST NOT allow overlapping prune cycles (a prune callback must not run if a previous one is still executing).
- **FR-008**: System MUST share the pruning timer with idle timeout checks (a single timer drives both).

### Key Entities

- **Usage Sample**: A snapshot of the idle connection count at a point in time. Collected at each pruning interval tick. The **median** of recent samples determines the prune target. The sample window retains `Connection Lifetime / Pruning Interval` samples, covering the full connection lifetime.
- **Prune Target**: The median idle count from recent samples, floored at MinPoolSize. Idle connections beyond this target are pruned.
- **Pruning Timer**: A periodic timer that triggers both pruning evaluation and idle timeout checks.

## Success Criteria

### Measurable Outcomes

- **SC-001**: After a sustained period of low demand, the pool reduces its idle connection count to within a reasonable margin of the observed usage level within two pruning intervals.
- **SC-002**: Applications experience no increase in connection acquisition failures or timeout errors as a result of pruning under normal workload transitions.
- **SC-003**: Pool resource consumption (open connections) decreases proportionally when sustained demand drops by 50% or more.
- **SC-004**: Pruning correctly respects MinPoolSize under all conditions — pool never shrinks below the configured minimum.

## Clarifications

### Session 2026-04-10
- Q: How should usage samples be aggregated to determine the prune target? → A: Median of idle count samples
- Q: How many recent samples should be retained for the median calculation? → A: Connection Lifetime divided by Pruning Interval
- Q: What should the default pruning interval be? → A: 10 seconds

## Assumptions

- The channel-based idle connection store (FIFO) allows removing connections from the front without disturbing connections at the back. Connections that have been idle longest will be pruned first naturally.
- `ConnectionPoolSlots` provides an accurate count of reserved/active connections for sampling purposes via `ReservationCount`.
- The pruning timer will be implemented using `System.Threading.Timer`, consistent with the WaitHandle pool's `CleanupCallback`.
- Rate limiting (when implemented) will not interfere with pruning — pruning destroys connections but does not create them.

# Feature Specification: Traces and Metrics

**Feature Directory**: `.github/plans/connection-pool/traces-and-metrics`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "traces and metrics"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Trace Parity with WaitHandle Pool (Priority: P1)

A developer or support engineer enables EventSource tracing (`Microsoft.Data.SqlClient.EventSource`) to diagnose connection pool issues. They expect the same level of diagnostic detail from the new channel-based pool as they get from the WaitHandle pool. Every significant pool operation — getting a connection, creating a connection, returning a connection, destroying a connection, clearing the pool, entering error state, startup, shutdown, and reclaiming abandoned connections — must emit trace events with the same category and detail level. Without this, diagnosing pool issues with pool v2 is significantly harder than with pool v1.

**Why this priority**: Trace parity is the minimum bar for supportability. The WaitHandle pool emits 37 trace events covering the full connection lifecycle. The channel pool currently emits 1. Any pool operation that doesn't emit a trace event becomes a blind spot for diagnostics.

**Independent Test**: Enable EventSource tracing at the `PoolerTrace` keyword level. Open, use, and close connections with the channel pool. Verify that trace events are emitted for: pool construction, getting a connection (from idle, new creation, waiting), returning a connection, destroying/disposing a connection, startup, shutdown, and clearing.

**Acceptance Scenarios**:

1. **Given** EventSource tracing is enabled with the `PoolerTrace` keyword, **When** a connection is retrieved from the idle pool, **Then** a trace event is emitted identifying the pool and connection.
2. **Given** EventSource tracing is enabled, **When** a new physical connection is created because the idle pool is empty, **Then** a trace event is emitted identifying the pool and the new connection.
3. **Given** EventSource tracing is enabled, **When** a connection is returned to the pool, **Then** trace events are emitted for deactivation and for the routing decision (pooled, destroyed, or stasis).
4. **Given** EventSource tracing is enabled, **When** a connection is destroyed (expired, doomed, or pool shutting down), **Then** trace events are emitted for removal and disposal.
5. **Given** EventSource tracing is enabled, **When** startup or shutdown is called, **Then** a trace event is emitted with the pool identifier.

---

### User Story 2 - Metrics Parity with WaitHandle Pool (Priority: P1)

A developer uses performance counters or EventCounters to monitor connection pool health (active connections, free connections, pooled connection count, soft/hard connect/disconnect rates). The channel pool must emit the same metric signals as the WaitHandle pool so that existing monitoring dashboards and alerting continue to work when pool v2 is enabled. This includes incrementing/decrementing gauges when connections move between states (active, idle, pooled, stasis) and counting events (hard connects, hard disconnects, soft connects, soft disconnects, reclaimed connections).

**Why this priority**: Metrics parity ensures that operational monitoring is not broken when switching to pool v2. Production teams rely on these counters for capacity planning, alerting on pool exhaustion, and diagnosing connection leaks.

**Independent Test**: Enable EventCounters. Open and close connections with the channel pool. Verify that `active-hard-connections`, `number-of-pooled-connections`, `number-of-free-connections`, `soft-connects`, and `soft-disconnects` counters all reflect the correct values.

**Acceptance Scenarios**:

1. **Given** a new physical connection is created, **When** the creation completes, **Then** `HardConnectRequest` is counted and `EnterPooledConnection` increments the pooled gauge.
2. **Given** a physical connection is destroyed, **When** the disposal completes, **Then** `HardDisconnectRequest` is counted and `ExitPooledConnection` decrements the pooled gauge.
3. **Given** an idle connection is retrieved from the pool, **When** it is handed to the caller, **Then** `SoftConnectRequest` is counted and `ExitFreeConnection` decrements the free gauge.
4. **Given** a connection is returned to the idle pool, **When** it is placed in the idle channel, **Then** `SoftDisconnectRequest` is counted and `EnterFreeConnection` increments the free gauge.
5. **Given** a connection is reclaimed from GC (abandoned by the application), **When** it is reclaimed, **Then** `ReclaimedConnectionRequest` is counted.

---

### User Story 3 - Feature-Specific Traces and Metrics (Priority: P2)

As new pool features are added (pruning, idle timeout, rate limiting, error state, shutdown, clear), each feature must emit its own trace events and update relevant metrics. For example, pruning should trace when it runs and how many connections it removes. Rate limiting should trace when a caller is throttled. Error state should trace when entered and cleared. These feature-specific signals are critical for understanding pool behavior beyond basic connection lifecycle.

**Why this priority**: Ranked P2 because the baseline parity (P1) provides the essential monitoring. Feature-specific traces add deeper insight for advanced diagnostics but are not required for basic operational monitoring.

**Independent Test**: Enable tracing. Trigger pruning (wait for the pruning timer). Verify trace events are emitted showing the pruning sample, target count, and actual connections pruned. Repeat for other features.

**Acceptance Scenarios**:

1. **Given** the pruning timer fires, **When** pruning executes, **Then** trace events are emitted for: sample recorded, prune target calculated, each connection pruned.
2. **Given** a connection is removed by idle timeout, **When** the removal occurs, **Then** a trace event identifies the connection and the idle duration.
3. **Given** a caller is throttled by the rate limiter, **When** the caller waits, **Then** a trace event records the wait.
4. **Given** the pool enters or exits error state, **When** the state transition occurs, **Then** trace events record the transition, the exception (on entry), and the backoff interval.
5. **Given** the pool is cleared, **When** the generation counter increments, **Then** a trace event records the new generation.

---

### User Story 4 - OpenTelemetry Connection Pool Semantic Conventions (Priority: P3)

A developer using OpenTelemetry-based monitoring (e.g., Prometheus, Grafana, Azure Monitor) wants to see standard database connection pool metrics that follow the OpenTelemetry semantic conventions (`db.client.connections.*`). These conventions define well-known metric names and attributes that monitoring tools understand out of the box, enabling zero-configuration dashboards. Neither the WaitHandle pool nor the channel pool currently emits OTEL-standard metrics. This story adds OTEL-aligned metrics to the channel pool as a forward-looking improvement.

**Why this priority**: Ranked P3 because existing EventCounter-based metrics (P1) cover operational needs. OTEL conventions are forward-looking and will become increasingly important as the ecosystem adopts them, but they are additive — not blocking any current monitoring workflows.

**Independent Test**: Configure an OpenTelemetry collector or use `System.Diagnostics.Metrics` listeners. Open and close connections. Verify that metrics with OTEL-standard names (e.g., `db.client.connections.usage`, `db.client.connections.max`) are emitted with correct values and attributes.

**Acceptance Scenarios**:

1. **Given** a metrics listener is configured, **When** connections are in use, **Then** a metric reflecting the number of connections by state (idle, used) is emitted with the OTEL-standard name.
2. **Given** a metrics listener is configured, **When** the pool is created, **Then** a metric reflecting the maximum pool size is emitted.
3. **Given** OTEL metrics are enabled, **When** the pool already emits EventCounters, **Then** both systems report consistent values for the same underlying measurements.

---

### Edge Cases

- What happens when tracing is not enabled? Trace events are guarded by `IsEnabled` checks; no overhead when tracing is off.
- What happens when metrics and traces disagree? Metrics must be derived from the same state transitions as traces. A single code path increments the metric and emits the trace to avoid drift.
- What happens when a connection is destroyed by multiple mechanisms simultaneously (e.g., pruning and idle timeout)? Only one removal succeeds; the trace and metric are emitted once by whichever mechanism completes the removal.
- What happens to metrics during shutdown? Final metric updates occur as connections are drained. After shutdown, no further metric updates are emitted.
- What happens when a feature (e.g., pruning) is not yet implemented? Traces and metrics for that feature are added as part of the feature implementation, not retroactively.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The channel pool MUST emit `TryPoolerTraceEvent` calls for every significant pool operation, matching the categories traced by the WaitHandle pool: construction, get connection (idle hit, new creation, wait), return connection (pool, destroy, stasis), destroy/dispose, clear, error state entry/exit, startup, shutdown, reclaim, and replace connection.
- **FR-002**: The channel pool MUST call `SqlClientDiagnostics.Metrics` methods at the appropriate state transitions: `HardConnectRequest` on physical creation, `HardDisconnectRequest` on physical disposal, `SoftConnectRequest` on pool retrieval, `SoftDisconnectRequest` on pool return, `EnterPooledConnection`/`ExitPooledConnection` on pool membership changes, `EnterFreeConnection`/`ExitFreeConnection` on idle state changes, `EnterStasisConnection`/`ExitStasisConnection` on stasis transitions, and `ReclaimedConnectionRequest` on GC reclaim.
- **FR-003**: Each new pool feature (pruning, idle timeout, rate limiting, error state, shutdown, clear) MUST emit feature-specific trace events when the feature is implemented.
- **FR-004**: Trace events MUST include the pool identifier and (where applicable) the connection identifier to enable correlation.
- **FR-005**: Trace events and metric updates for the same state transition MUST occur in the same code path to prevent drift between traces and metrics.
- **FR-006**: Trace event emission MUST have negligible performance impact when tracing is disabled (guarded by `IsEnabled` checks).
- **FR-007**: The channel pool SHOULD emit OpenTelemetry semantic convention metrics (`db.client.connections.*`) via `System.Diagnostics.Metrics` for forward-looking observability.
- **FR-008**: OTEL metrics and existing EventCounters MUST report consistent values for the same underlying measurements.

### Key Entities

- **Trace Event**: An EventSource event emitted via `SqlClientEventSource.Log.TryPoolerTraceEvent` with the `PoolerTrace` keyword. Carries a formatted message with pool and connection identifiers. Consumed by ETW, EventPipe, and diagnostic tools.
- **Metric**: A numeric measurement reported via `SqlClientDiagnostics.Metrics` (gauges for current state, counters for cumulative events). Surfaced through EventCounters on .NET and PerformanceCounters on .NET Framework.
- **OTEL Metric**: A metric emitted via `System.Diagnostics.Metrics` following OpenTelemetry database connection pool semantic conventions. Forward-looking, consumed by OTEL collectors and APM tools.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The channel pool emits trace events for every operation category that the WaitHandle pool traces — zero trace gaps for equivalent operations.
- **SC-002**: EventCounter values with the channel pool match expectations: `number-of-pooled-connections` equals actual pool size, `number-of-free-connections` equals actual idle count, `soft-connects` + `soft-disconnects` match connection churn.
- **SC-003**: All feature-specific operations (prune, idle timeout removal, rate limiter throttle, error state transition, clear, shutdown) emit at least one trace event per invocation.
- **SC-004**: Tracing disabled results in no measurable throughput regression from trace instrumentation code.

## Assumptions

- The existing `SqlClientEventSource` and `SqlClientDiagnostics.Metrics` infrastructure is reused. No new tracing framework is introduced.
- Trace message formats follow the existing WaitHandle pool convention: `"<prov.DbConnectionPool.Operation|RES|CPOOL> {poolId}, ..."`.
- Feature-specific traces and metrics (FR-003) are added incrementally as part of each feature implementation, not as a separate standalone effort.
- OTEL metric names and attributes follow the OpenTelemetry database semantic conventions as defined at the time of implementation. Specific metric names will be determined during design.
- `.NET Framework` targets continue to use PerformanceCounters; `.NET 8+` targets use EventCounters and optionally `System.Diagnostics.Metrics` for OTEL.
- DiagnosticListener events (Activity-based distributed tracing for connection open/close/command) are outside the scope of this feature — they are emitted at the `SqlConnection` level, not the pool level.

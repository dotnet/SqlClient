# Fix 9: Add Metrics and Tracing

**Priority:** Medium — needed for production observability
**Risk:** Low

## Problem

The V2 pool has no metrics or event source tracing. The V1 pool has basic event source integration
but no OpenTelemetry support (see issue #2211).

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ConnectionPool/ChannelDbConnectionPool.cs`

## Changes Required

### EventSource Integration

Add events to `SqlClientEventSource` for:

- Pool created/destroyed
- Connection acquired from pool (idle hit)
- Connection created (new physical)
- Connection returned to pool
- Connection destroyed (pruned, broken, lifetime exceeded)
- Pool error state entered/exited
- Warmup started/completed

### Performance Counters (existing pattern)

The existing `SqlClientEventSource` defines counters:

- `active-hard-connections` — physical connections open
- `active-soft-connections` — logical connections open
- `number-of-pooled-connections` — connections in pool
- etc.

These must be wired into the V2 pool's lifecycle events.

### Future: OpenTelemetry Metrics (issue #2211)

Design the tracing integration points now, even if OTEL support is implemented later. This means
using well-named events that map cleanly to standard database connection pool metrics:

- `db.client.connections.idle.max` / `.min`
- `db.client.connections.max`
- `db.client.connections.usage` (idle/used)
- `db.client.connections.pending_requests`
- `db.client.connections.timeouts`
- `db.client.connections.create_time` (histogram)
- `db.client.connections.wait_time` (histogram)

## Testing

- Unit test: Verify events are emitted during pool operations
- Validate counter accuracy under concurrent load

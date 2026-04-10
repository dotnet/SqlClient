# Traces and Metrics

Goal: Ensure `ChannelDbConnectionPool` emits comprehensive EventSource traces and OpenTelemetry-ready metrics for diagnostics, monitoring, and observability.

**Status:** Not started

## Key Decisions

1. **Gap-fill plus OTEL readiness:** Fill gaps where the Channel pool is missing traces/metrics that the WaitHandle pool emits, AND implement any metrics from the standard OpenTelemetry connection pool semantic conventions that are currently missing from both pools.
2. **Incremental plus final sweep:** Add relevant traces/metrics alongside each feature (e.g., pruning metrics with pruning, rate limiter metrics with rate limiting). Do a dedicated audit pass at the end to catch anything missing.

## Stages

### Stage 1 — Research
- [ ] Inventory all `SqlClientEventSource` trace events in the WaitHandle pool
- [ ] Inventory all `SqlClientDiagnostics.Metrics` calls in the WaitHandle pool
- [ ] Inventory existing traces/metrics in the Channel pool and identify gaps
- [ ] Review OpenTelemetry database connection pool semantic conventions (`db.client.connections.*`)
- [ ] Identify OTEL metrics not currently emitted by either pool
- [ ] Review what Npgsql and EF Core expose for OTEL pool metrics

### Stage 2 — Requirements
- [ ] Define required traces and metrics (gap-fill + OTEL standard)

### Stage 3 — Design
- [ ] Design trace/metric instrumentation points
- [ ] Plan OTEL metric names and attributes

### Stage 4 — Implementation
- [ ] Implement incrementally with each feature
- [ ] Final audit sweep to fill remaining gaps
- [ ] Test metric accuracy

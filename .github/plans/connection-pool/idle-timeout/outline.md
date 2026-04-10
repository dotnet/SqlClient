# Configurable Idle Connection Timeout

Goal: Allow users to configure how long idle connections remain in the pool before being recycled, reducing stale/dead connection issues.

**Status:** Not started

## Motivation

Users frequently encounter dead connections from the pool after periods of low activity (see [dotnet/SqlClient#343](https://github.com/dotnet/SqlClient/issues/343)). Firewalls, load balancers, and server-side idle session timeouts silently kill connections that the pool still considers alive. A configurable idle timeout lets users control how aggressively the pool recycles idle connections to keep them fresh.

## Key Decisions

1. **New connection string keyword:** Exposed as a connection string setting (similar to Npgsql's `ConnectionIdleLifetime`). Exact keyword name TBD during design.
2. **Does NOT respect MinPoolSize:** Idle connections should be recycled even below MinPoolSize to keep connections fresh. Warmup/replenishment can recreate them in the background. This is configurable — users who don't want this behavior can set a long or zero idle timeout.
3. **Separate from pruning:** Pruning reduces pool size to match observed usage. Idle timeout recycles individual connections that have sat unused too long, regardless of pool size. They share a timer (see pruning outline).

## Stages

### Stage 1 — Research
- [ ] How Npgsql's `ConnectionIdleLifetime` works and its defaults
- [ ] How idle time is tracked (timestamp on last return to pool)
- [ ] Relationship to existing `Connection Lifetime` / `Load Balance Timeout` (age since creation vs time since last use)
- [ ] Interaction with pruning (shared timer, ordering of checks)
- [ ] Interaction with warmup (replenish after idle expiry drops below MinPoolSize)
- [ ] How idle connections are detected as dead today (`IsConnectionAlive` health check)
- [ ] Review [dotnet/SqlClient#343](https://github.com/dotnet/SqlClient/issues/343) for specific user scenarios

### Stage 2 — Requirements
- [ ] Define user-observable behavior

### Stage 3 — Design
- [ ] Design idle timeout mechanism, timestamp tracking, keyword, and defaults

### Stage 4 — Implementation
- [ ] Implement and test

# Pool Pruning

Goal: Implement periodic pruning in `ChannelDbConnectionPool` to close excess idle connections and bring the pool back toward observed usage levels during low-demand periods.

**Status:** Not started

## Key Decisions

1. **Sampling-based approach (Npgsql pattern):** Take samples at regular intervals to track how many connections are in use. At prune time, use these samples to determine a safe minimum and prune idle connections down to match observed usage. This avoids the WaitHandle pool's two-stack aging heuristic.
2. **Configurable cadence:** Pruning interval should be user-configurable (Npgsql uses `ConnectionPruningInterval`).
3. **Separate from idle timeout:** Pruning (reduce pool to match usage) and idle timeout (expire individual connections idle too long) are separate features with separate settings (Npgsql: `ConnectionPruningInterval` vs `ConnectionIdleLifetime`). They can share a single timer since connections idle for the full sampling duration will be caught by pruning anyway.

## Stages

### Stage 1 — Research
- [ ] How Npgsql's `PoolingDataSource` implements sampling-based pruning
- [ ] Npgsql's `ConnectionPruningInterval` setting and defaults
- [ ] How samples are collected and aggregated to determine safe minimum
- [ ] Interaction with MinPoolSize (floor for pruning)
- [ ] Interaction with shutdown (pruning timer should stop)
- [ ] Interaction with warmup (replenish after aggressive pruning?)
- [ ] How the WaitHandle pool's `CleanupCallback` timer works (for reference/comparison)

### Stage 2 — Requirements
- [ ] Define user-observable behavior

### Stage 3 — Design
- [ ] Design sampling mechanism, prune algorithm, and shared timer for channel-based pool

### Stage 4 — Implementation
- [ ] Implement and test

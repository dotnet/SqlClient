# Pool Warmup

Goal: Implement pool warmup in `ChannelDbConnectionPool` to pre-create connections up to `Min Pool Size` at pool startup, reducing latency for early requests.

**Status:** Not started

## Key Decisions

1. **Background, serial, rate-limited:** Warmup runs in the background on `Startup()`. Warmup submits open requests serially (one at a time) through the same rate limiter as user-initiated requests. This avoids hammering the server while still allowing the pool to warm up quickly if user burst requests push the rate limiter's concurrency higher.
2. **Triggered on `Startup()`:** Called by `DbConnectionPoolGroup` when the pool is first created. Not lazy/deferred to first `Open()`.
3. **Errors swallowed silently:** Warmup connection failures are logged/traced but do not propagate. If warmup fails, the pool simply has fewer pre-created connections and user requests will create connections on demand.

## Stages

### Stage 1 — Research
- [ ] How the WaitHandle pool's `Startup()` + `QueuePoolCreateRequest` + `PoolCreateRequest` loop works
- [ ] How Npgsql handles `MinPoolSize` warmup
- [ ] How warmup interacts with the rate limiter (submitting through shared path vs bypassing)
- [ ] How warmup interacts with pruning (avoid warmup → immediate prune cycle)
- [ ] How warmup interacts with idle timeout (freshly warmed connections shouldn't expire immediately)
- [ ] Background task lifecycle (cancellation on shutdown, fire-and-forget safety)

### Stage 2 — Requirements
- [ ] Define user-observable behavior

### Stage 3 — Design
- [ ] Design warmup loop, rate limiter integration, and error handling

### Stage 4 — Implementation
- [ ] Implement and test

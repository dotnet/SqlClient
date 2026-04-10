# Connection Open Rate Limiting

Goal: Rate-limit how quickly new physical connections are opened in `ChannelDbConnectionPool` to avoid overwhelming the server during burst demand, pool replenishment, or reconnection storms.

**Status:** Not started

## Key Decisions

1. **Pluggable architecture:** The pool should depend on an abstraction (interface/strategy) for rate limiting. The first implementation will use a simple concurrency limit (e.g., max 10 concurrent opens). Future implementations could use temporal rate limiting, adaptive backoff, etc. The design should make it easy to swap implementations.
2. **Internal for now, user-configurable later:** The rate limiter is an internal implementation detail initially. The architecture should support future extensibility where users can specify their own rate limiting algorithm or configure parameters.
3. **Both burst and steady-state protection:** Rate limiting applies to all connection creation — cold start bursts, reconnection after network blips, warmup, and replenishment after pruning.

## Stages

### Stage 1 — Research
- [ ] How the WaitHandle pool throttles creation (`CreationSemaphore`, serialized `PoolCreateRequest`)
- [ ] How Npgsql throttles connection creation
- [ ] How the Channel pool currently allows unbounded concurrent creation
- [ ] Interaction with connection timeout (time spent waiting for rate limit counts against timeout budget)
- [ ] Interaction with warmup (warmup should respect rate limits)
- [ ] Interaction with async opens (multiple `OpenAsync` calls racing to create)
- [ ] Rate limiter interface design patterns (e.g., `System.Threading.RateLimiting`, semaphore-based, token bucket)

### Stage 2 — Requirements
- [ ] Define user-observable behavior

### Stage 3 — Design
- [ ] Design rate limiter abstraction and first concrete implementation
- [ ] Define integration points in the connection open path

### Stage 4 — Implementation
- [ ] Implement and test

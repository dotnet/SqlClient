# Priority 6: Connection-Level Packet Locking Redesign

**Addresses:** #2418, #422, #1530
**Impact:** Medium — improves MARS performance and reduces thread starvation
**Effort:** Medium-High

## Current State

### Stream-Level Locking (Current)

The managed SNI uses `ConcurrentQueueSemaphore` at the stream level:

- `SniSslStream` has `_readAsyncSemaphore` and `_writeAsyncSemaphore` (line 20-21)
- `SniNetworkStream` has the same pattern (line 20-21)
- These wrap all `ReadAsync` / `WriteAsync` calls

### Connection-Level Locking

- `SniTcpHandle.Send()` uses `Monitor.Enter(this)` + `lock(_sendSync)` — dual blocking locks
- `SniNpHandle.Send()` uses `Monitor.Enter(this)` — blocking lock
- Out-of-band (Attention) packets use `Monitor.TryEnter(this)` — non-blocking attempt

### MARS Locking

- `SniMarsHandle` uses `lock(this)` for sequence number gating (7 occurrences)
- `lock(_receivedPacketQueue)` for receive queue (2 occurrences)
- `ManualResetEventSlim _ackEvent` for send flow control
- `SniMarsConnection` uses `lock(DemuxerSync)` (8 occurrences)

### Issue #2418 Proposal

Move locking from stream-level to connection-level (`SniTcpHandle`/`SniNpHandle`), using
`SemaphoreSlim` instead of `Monitor.Enter` for async-friendly locking.

## Incremental Fixes

| # | Fix | Complexity | Impact |
| --- | ----- | ----------- | -------- |
| 1 | [Replace Monitor.Enter with SemaphoreSlim in SniTcpHandle](01-tcp-async-locks.md) | Medium | Medium |
| 2 | [Unify sync/async locking in SniNpHandle](02-np-async-locks.md) | Medium | Low |
| 3 | [Modernize MARS send flow control](03-mars-send-flow.md) | High | Medium |
| 4 | [Replace MARS receive queue locking](04-mars-receive-lock.md) | Medium | Low-Medium |

## Dependencies

- Fix 1 is the highest-impact standalone change
- Fix 2 mirrors Fix 1 for Named Pipes
- Fixes 3-4 are MARS-specific and more complex
- A prior MARS rewrite (PR #1357) was reverted — proceed with caution

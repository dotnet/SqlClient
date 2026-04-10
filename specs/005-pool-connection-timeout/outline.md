# Connection Timeout Awareness

Goal: Ensure `ChannelDbConnectionPool` properly propagates a single cancellation token through all wait paths, implements blocking period / error state for backwards compatibility, and implements `ReplaceConnection` for transient error retry.

**Status:** Not started

## Key Decisions

1. **Single cancellation token throughout:** The current approach (one `CancellationTokenSource` from `ConnectionTimeout` covering the whole open path) is correct. Every component in the pool — rate limiter waits, idle channel reads, connection creation — should check this token. Time spent anywhere counts against the overall timeout.
2. **Blocking period required for backwards compatibility:** Implement `PoolBlockingPeriod` (Auto/AlwaysBlock/NeverBlock) and error state propagation (`ErrorOccurred`, exponential backoff on creation failures, fast-fail for all callers when server is unreachable).
3. **`ReplaceConnection` in scope:** The retry path for transient errors during open needs to be implemented as part of this feature.

## Stages

### Stage 1 — Research
- [ ] How the WaitHandle pool's error state works (`_errorWait`, `ErrorCallback`, `ErrorEvent`, exponential backoff)
- [ ] How `PoolBlockingPeriod` interacts with error state (Auto vs AlwaysBlock vs NeverBlock)
- [ ] How `ReplaceConnection` works in the WaitHandle pool (retry with old connection swap)
- [ ] How `ConnectRetryCount` / `ConnectRetryInterval` interact with pool-level timeout
- [ ] Current Channel pool timeout implementation and gaps
- [ ] How the cancellation token should flow through rate limiter waits

### Stage 2 — Requirements
- [ ] Define user-observable behavior

### Stage 3 — Design
- [ ] Design cancellation token propagation, error state machine, blocking period, and ReplaceConnection

### Stage 4 — Implementation
- [ ] Implement and test

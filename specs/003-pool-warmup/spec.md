# Feature Specification: Background Pool Warmup

**Feature Branch**: `dev/mdaigle/pool-warmup` (branched from `dev/mdaigle/pool-channel-rate-limiting`)  
**Created**: 2026-07-16  
**Status**: Draft  

## Description

Implement background pool warmup for `ChannelDbConnectionPool`. When the pool starts, it asynchronously pre-creates connections up to Min Pool Size in the background, serially (one at a time), through the shared rate-limiting mechanism used by user-initiated requests.

Whenever the pool count drops below Min Pool Size for any reason (a connection destroyed on return, idle-timeout eviction, or any other removal that crosses the floor), the pool automatically triggers replenishment using the same serial, rate-limited creation path. Pruning itself respects the Min Pool Size floor and never reduces the pool below the minimum; it is only mentioned here because it shares the same removal choke point that triggers replenishment.

## User Scenarios & Testing

### User Story 1 — Background Warmup on Pool Creation (P1)

When a pool is created with Min Pool Size > 0, it pre-creates connections in the background so early application requests find ready-to-use connections.

**Acceptance Scenarios**:

1. **Given** a pool is created with minimum pool size of N, **When** startup completes, **Then** the pool begins creating connections in the background up to N.
2. **Given** a pool is warming up, **When** a user opens a connection before warmup finishes, **Then** the user request is served immediately without waiting for warmup to complete.
3. **Given** a pool is warming up, **When** warmup creates each connection, **Then** each connection is created one at a time (serially), not in parallel.

---

### User Story 2 — Warmup Through Shared Rate Limiter (P1)

Warmup creates connections through the same rate-limiting mechanism as user-initiated requests, preventing warmup from overwhelming the server.

**Acceptance Scenarios**:

1. **Given** warmup is creating connections, **When** a user request also needs a new connection, **Then** both warmup and user requests compete fairly through the shared rate limiter.
2. **Given** the rate limiter is at capacity, **When** warmup attempts to create the next connection, **Then** warmup does not bypass the limiter: it ends the current pass without creating (rather than spinning or waiting on a permit). Saturation only occurs while user requests are actively creating connections, which fill the pool themselves; any later drop below the minimum re-triggers warmup once capacity has freed up.

---

### User Story 3 — Warmup Failure Resilience (P1)

If a connection fails to open during warmup, the failure is silently absorbed on the warmup loop — it is never propagated to the caller as an unhandled exception. Warmup participates in the pool's blocking-period error state exactly like an on-demand creation (mirroring the legacy WaitHandle pool): a genuine open failure enters the error state, and a successful open clears it. The pool remains operational — once the blocking period expires, user requests create connections on demand as needed.

**Acceptance Scenarios**:

1. **Given** warmup is in progress, **When** a connection fails to open, **Then** the failure is traced/logged and absorbed by the warmup loop rather than surfacing as an unhandled exception.
2. **Given** warmup fails and has entered the blocking-period error state, **When** a user opens a connection during the blocking window, **Then** the user request fast-fails with the cached exception (matching the WaitHandle pool); once the blocking period expires it creates a connection on demand and succeeds normally.
3. **Given** warmup's open fails, **When** the pool's error state is checked, **Then** the pool IS in the blocking-period error state — warmup uses the same shared creation path as user requests and enters/clears that state identically.
4. **Given** the pool is already in the blocking-period error state (driven there by a failing request), **When** warmup would otherwise replenish, **Then** warmup stands down while the error state is active rather than piling more doomed opens onto a struggling server (the warmup loop skips replenishment while `ErrorOccurred`, mirroring the WaitHandle pool).

---

### User Story 4 — Warmup Cancellation on Shutdown (P2)

When a pool is shut down while warmup is still in progress, warmup stops promptly. No new connections are created after shutdown begins.

**Acceptance Scenarios**:

1. **Given** warmup is in progress, **When** the pool is shut down, **Then** warmup stops and no further connections are created.
2. **Given** warmup is in progress, **When** the pool is shut down, **Then** any connections already created by warmup are cleaned up as part of normal shutdown.

---

### User Story 5 — Replenishment on Any Below-Minimum Event (P2)

Whenever the pool count drops below Min Pool Size, the pool automatically triggers warmup-style replenishment to restore to the minimum. Every connection removal funnels through a single choke point (`RemoveConnection`), which triggers replenishment when the removal leaves the pool below the minimum.

**Acceptance Scenarios**:

1. **Given** a pruning cycle runs, **When** it removes idle connections, **Then** it stops at the Min Pool Size floor and does not reduce the pool below the minimum (pruning shares the removal choke point but does not itself cause a below-minimum replenishment).
2. **Given** a connection is destroyed on return to the pool (broken, lifetime expired), **When** the pool count drops below the minimum, **Then** the pool queues a replenishment request.
3. **Given** idle-timeout eviction removes an expired connection on retrieval, **When** the pool count drops below minimum, **Then** the pool queues a replenishment request.
4. **Given** replenishment is triggered, **When** connections are created, **Then** they are created serially through the shared rate limiter, identical to initial warmup.
5. **Given** the pool is at or above the minimum pool size, **When** a connection is destroyed, **Then** no replenishment is triggered.

## Implementation Notes

- Warmup starts automatically when the pool's `Startup()` is called.
- Warmup runs on a background task, so it never blocks the caller (`Startup`/connection return) and uses no sync-over-async in the loop. The physical connection open itself is currently synchronous (executed on the background task); the connection factory does not yet expose an async open.
- Creates connections serially (one at a time) through the shared rate limiter. If the limiter is saturated (no permit available), warmup ends the pass rather than bypassing or spinning on the limiter; convergence to the minimum is handled by the concurrent user creations that saturated it and by re-triggering on the next below-minimum event.
- Warmup failures are traced/logged and absorbed by the warmup loop (never surfaced as an unhandled exception). Warmup goes through the same shared creation path as user requests, so a genuine open failure enters the blocking-period error state and a successful open clears it (mirroring the WaitHandle pool). While the error state is active, the warmup loop stands down rather than replenishing.
- If a `Clear` races with an in-flight warmup creation, the freshly created (now stale-generation) connection is not special-cased by warmup; it is harmlessly discarded by the liveness/generation check on its next retrieval. After a `Clear`, replenishment refills the pool to the minimum with fresh-generation connections.
- Idle-timeout eviction is lazy: an idle connection past its timeout is removed when it is next retrieved (via the liveness check), not by a background sweep. Pruning is separate and always respects the Min Pool Size floor.
- Concurrent warmup/replenishment requests are coalesced — a single `_warmupLoopRunning` guard ensures only one warmup loop executes at a time, and requests that arrive while it is running are dropped (the running loop re-reads the pool count each iteration and drives to the minimum on its own).
- Warmup is a no-op when Min Pool Size = 0.

## Acceptance Criteria

- When a pool starts with Min Pool Size > 0, background warmup begins immediately.
- After warmup completes, the pool contains Min Pool Size idle connections.
- Warmup runs on a background task without blocking callers and without sync-over-async in the loop (the physical open is currently synchronous, executed on the background task, pending async-open support in the factory).
- Warmup connections are created serially through the shared rate limiter.
- Warmup errors are traced/logged and never crash the application. Because warmup shares the on-demand creation path, a genuine open failure enters the pool's blocking-period error state (as it would for a user request) and a successful open clears it.
- If the pool is shut down or cleared during warmup, warmup stops gracefully.
- After any event that reduces the pool below Min Pool Size, the pool restores to the minimum through replenishment: a single warmup pass suffices when it is not racing a concurrent drain (e.g. `Clear`) or a saturated rate limiter; otherwise convergence completes across subsequent re-triggered passes.
- Concurrent warmup/replenishment requests are coalesced so only one warmup loop executes at a time.
- Unit tests validate warmup behavior for various Min Pool Size values (0, 1, N) and all replenishment triggers.

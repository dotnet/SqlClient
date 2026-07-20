# Feature Specification: Background Pool Warmup

**Feature Branch**: `dev/mdaigle/pool-warmup` (branched from `dev/mdaigle/pool-channel-rate-limiting`)  
**Created**: 2026-07-16  
**Status**: Draft  

## Description

Implement background pool warmup for `ChannelDbConnectionPool`. When the pool starts, it asynchronously pre-creates connections up to Min Pool Size in the background, serially (one at a time), through the shared rate-limiting mechanism used by user-initiated requests.

Whenever the pool count drops below Min Pool Size for any reason (pruning, connection destruction, idle timeout expiry), the pool automatically triggers replenishment using the same serial, rate-limited creation path.

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
2. **Given** the rate limiter is at capacity, **When** warmup attempts to create the next connection, **Then** warmup waits its turn rather than bypassing the limiter.

---

### User Story 3 — Warmup Failure Resilience (P1)

If a connection fails to open during warmup, the failure is silently absorbed. The pool remains fully operational — user requests create connections on demand as needed. Warmup failures never trigger the pool-level error state.

**Acceptance Scenarios**:

1. **Given** warmup is in progress, **When** a connection fails to open, **Then** the failure is traced/logged but not propagated as an exception.
2. **Given** warmup fails for all connections, **When** a user subsequently opens a connection, **Then** the user request creates a connection on demand and succeeds normally.
3. **Given** warmup fails, **When** the pool's error state is checked, **Then** the pool is NOT in error state — warmup failures do not trigger the pool-level blocking-period mechanism.
4. **Given** the pool is already in the blocking-period error state (driven there by failing user requests), **When** warmup would otherwise replenish, **Then** warmup stands down while the error state is active rather than piling more doomed opens onto a struggling server. Warmup respects the error state but never enters or clears it (mirroring the legacy WaitHandle pool).

---

### User Story 4 — Warmup Cancellation on Shutdown (P2)

When a pool is shut down while warmup is still in progress, warmup stops promptly. No new connections are created after shutdown begins.

**Acceptance Scenarios**:

1. **Given** warmup is in progress, **When** the pool is shut down, **Then** warmup stops and no further connections are created.
2. **Given** warmup is in progress, **When** the pool is shut down, **Then** any connections already created by warmup are cleaned up as part of normal shutdown.

---

### User Story 5 — Replenishment on Any Below-Minimum Event (P2)

Whenever the pool count drops below Min Pool Size for any reason, the pool automatically triggers warmup-style replenishment to restore to the minimum.

**Acceptance Scenarios**:

1. **Given** pruning has reduced the pool below the minimum pool size, **When** the pruning cycle completes, **Then** the pool queues a replenishment request.
2. **Given** a connection is destroyed on return to the pool (broken, lifetime expired), **When** the pool count drops below the minimum, **Then** the pool queues a replenishment request.
3. **Given** idle timeout expiry removes a connection, **When** the pool count drops below minimum, **Then** the pool queues a replenishment request.
4. **Given** replenishment is triggered, **When** connections are created, **Then** they are created serially through the shared rate limiter, identical to initial warmup.
5. **Given** the pool is at or above the minimum pool size, **When** a connection is destroyed, **Then** no replenishment is triggered.

## Implementation Notes

- Warmup starts automatically when the pool's `Startup()` is called.
- Warmup runs on a background task, so it never blocks the caller (`Startup`/connection return) and uses no sync-over-async in the loop. The physical connection open itself is currently synchronous (executed on the background task); the connection factory does not yet expose an async open.
- Creates connections serially (one at a time) through the shared rate limiter.
- Warmup failures are traced/logged but do not propagate or trigger error state. Warmup does, however, respect an already-active blocking-period error state and stands down while it is active.
- If a `Clear` races with an in-flight warmup creation, the freshly created (now stale-generation) connection is not special-cased by warmup; it is harmlessly discarded by the liveness/generation check on its next retrieval. After a `Clear`, replenishment refills the pool to the minimum with fresh-generation connections.
- Concurrent warmup/replenishment requests are coalesced — only one warmup loop executes at a time.
- Warmup is a no-op when Min Pool Size = 0.

## Acceptance Criteria

- When a pool starts with Min Pool Size > 0, background warmup begins immediately.
- After warmup completes, the pool contains Min Pool Size idle connections.
- Warmup runs on a background task without blocking callers and without sync-over-async in the loop (the physical open is currently synchronous, executed on the background task, pending async-open support in the factory).
- Warmup connections are created serially through the shared rate limiter.
- Warmup errors are traced/logged and do not crash the application or trigger error state.
- If the pool is shut down or cleared during warmup, warmup stops gracefully.
- After any event that reduces the pool below Min Pool Size, the pool restores to minimum within one warmup pass.
- Concurrent warmup/replenishment requests are coalesced so only one warmup loop executes at a time.
- Unit tests validate warmup behavior for various Min Pool Size values (0, 1, N) and all replenishment triggers.

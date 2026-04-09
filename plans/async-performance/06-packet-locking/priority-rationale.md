# Priority Rationale: Why P6

## Ranking: Priority 6 of 7

Connection-level packet locking redesign is ranked sixth because it primarily affects MARS users on
managed SNI (a narrowing audience), carries significant implementation risk, and a prior attempt was
reverted.

## Justification

### 1. Real Problem for MARS Users

Issue #422 (107 comments) documents severe performance degradation and timeouts when using MARS on
Linux. This is a genuine problem that causes production failures, and the root cause — blocking
`Monitor.Enter` locks on send/receive paths — is well understood.

The inconsistency between sync locking (`Monitor.Enter`) and async locking
(`ConcurrentQueueSemaphore`) means that mixed sync/async workloads on the same connection can
deadlock or starve, particularly under MARS multiplexing.

### 2. Would Unblock MARS on Linux

Completing this work would materially improve the viability of MARS-enabled workloads on Linux and
macOS, where managed SNI is the only option. Currently, the standard workaround is
`ThreadPool.SetMinThreads(100+, 100+)`, which works but is a blunt instrument that masks the real
problem.

### 3. Clear Design Direction

Issue #2418 provides a well-defined proposal: replace stream-level locks with connection-level
`SemaphoreSlim`, unifying the sync and async locking paths. This is architecturally sound and aligns
with modern .NET concurrency practices.

## Why Not Higher

### 1. MARS Usage Is Declining

MARS was designed to enable multiple active result sets on a single connection — a pattern that is
less common in modern application architectures:

- Connection pooling makes having multiple connections cheap
- ORMs like EF Core enable MARS but many workloads don't require it
- Microservices tend toward simpler, single-result-set patterns

The audience for this fix is real but narrower than P1–P5.

### 2. Prior Attempt Was Reverted

PR #1357 (by Wraith2) implemented a MARS rewrite that was merged and then reverted. This history
indicates that the area is fragile and subtle — changes to the multiplexing logic can cause
regressions that are difficult to detect in testing and only manifest under specific concurrency
patterns in production.

This is a strong signal that this work requires exceptional test coverage and should not be
attempted without a comprehensive MARS test suite.

### 3. Risk vs Reward Ratio

The locking changes touch the most concurrency-sensitive part of the driver. A regression here could
cause:

- Data corruption (packets interleaved between sessions)
- Deadlocks (incorrect lock ordering)
- Silent hangs (missed wakeups)

These failure modes are worse than the current performance issue, which at least has a known
workaround (`SetMinThreads`). The risk/reward ratio is less favorable than P1–P5.

### 4. Workaround Exists

`ThreadPool.SetMinThreads(N, N)` where N ≥ concurrent connection count effectively mitigates the
thread starvation cascade. This is not ideal, but it's simple, well-documented, and
production-proven. No comparable workaround exists for P1 (pool blocking), P2 (250x read slowdown),
or P3 (missing async APIs).

## Why Not P7

P6 is ranked above P7 because:

- **Wider impact** — MARS users are a narrowing but still significant group; TVP streaming users
  (P7) are a much smaller subset

- **Production failures** — P6 causes actual timeouts and errors (#422, #1530); P7 causes thread
  blocking during data marshaling, which is suboptimal but rarely causes failures

- **Community demand** — 107 comments on #422 vs a single issue (#982) for P7

## Recommended Approach

Given the risk:

1. Build a comprehensive MARS concurrency test suite first (parallel readers, mixed sync/async,
   attention signals, transaction interleaving)

2. Start with non-MARS locking changes (Fix 6.1, 6.2) which are lower risk
3. Address MARS locking (Fix 6.3, 6.4) only after the test suite is in place
4. Consider having this work done by someone deeply familiar with the MARS protocol — the reverted
   PR #1357 suggests this area requires specialist knowledge

# Priority Rationale: Why P5

## Ranking: Priority 5 of 7

Allocation reduction is ranked fifth because it delivers measurable but incremental throughput
improvements. These are "good engineering" optimizations that matter at scale but do not fix
architectural bottlenecks.

## Justification

### 1. Low Effort, Reliable Returns

Every fix in this category is a localized, well-understood change:

- Replace `new byte[]` with `ArrayPool<byte>.Shared.Rent()` — pattern is already used in 5+ places
  in the codebase

- Replace `CancellationToken.Register()` with `UnsafeRegister()` — one-line change
- Replace `new char[]` with `ArrayPool<char>.Shared.Rent()` — same pattern

These changes carry minimal risk and can be reviewed and merged quickly. They also serve as good
"starter" contributions for new team members or community contributors.

### 2. Cumulative GC Pressure Reduction

Issue #593 shows that the async path allocates **13,156 MB** to read 10MB of data — 658x the data
size. While most of this is due to the snapshot/replay mechanism (addressed by P2), allocation
reduction directly reduces GC pressure:

- **TDS buffers** (`_inBuff`/`_outBuff`): Allocated per connection, never pooled. With a
  100-connection pool, that's 100 × 2 × 8KB = 1.6MB of unpooled buffers that churn on connection
  recycling.

- **Snapshot packet buffers:** Allocated per async read, never returned to pool. For a 10MB read
  spanning ~1,250 packets, that's 1,250 allocations of 8KB each.

- **CancellationToken registrations:** Allocated per async operation. At 10K ops/sec, that's 10K
  delegate allocations per second.

### 3. Improves Async Paths Disproportionately

Allocation overhead is disproportionately impactful in async code because:

- Async state machines capture local variables, so each allocation in an async method tends to
  extend the lifetime of surrounding objects

- GC pauses block all threads, amplifying the thread-starvation effects of P1/P6
- Allocation-heavy code promotes objects to Gen 1/2, increasing GC cost non-linearly

## Why Not Higher

### These Are Constant-Factor Improvements

Unlike P1 (eliminates blocking), P2 (fixes O(n²) algorithm), or P3/P4 (enables truly async
operations), allocation reduction does not change the fundamental behavior of any code path. It
makes existing operations slightly faster and lighter, but does not unlock new capabilities or fix
broken patterns.

A user blocked by pool starvation (P1) or experiencing 250x read slowdowns (P2) will see no benefit
from reduced allocations — their problem is architectural.

### Partially Addressed by P2

Fix 5.2 (pool snapshot buffers) is identical to P2 Fix 2.3. If P2 is implemented, half of the
allocation reduction work is already done. This reduces the standalone value proposition of P5.

## Why Not Lower

### vs P6 (Packet Locking)

P5 is ranked above P6 because:

- P5 fixes are **low risk** — no concurrency changes, no behavioral changes
- P6 (packet locking) requires changing concurrency primitives, which is inherently riskier. A prior
  MARS rewrite (PR #1357) was reverted.

- P5 delivers measurable throughput improvement on all platforms; P6 primarily helps MARS users on
  Linux.

### vs P7 (Async TVP)

P5 affects all async workloads; P7 only affects TVP streaming, a niche scenario.

## Recommended Approach

These fixes can be done at any time, in any order, independently of other priorities. They are ideal
for:

- Filling gaps between larger priority items
- Onboarding new contributors to the async performance effort
- Delivering quick wins that demonstrate progress

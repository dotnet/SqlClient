# Fix 5: Replace ConcurrentQueueSemaphore TCS Allocation

**Priority:** Low
**Complexity:** Medium
**Risk:** Low

## Problem

`ConcurrentQueueSemaphore` (used in managed SNI for stream-level read/write locking) allocates a
`new TaskCompletionSource<bool>` for every contended async operation. Under high throughput with
contention, this creates GC pressure.

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ManagedSni/ConcurrentQueueSemaphore.netcore.cs`
**Allocation:** Line ~34 — `var tcs = new TaskCompletionSource<bool>()`

**Used by:**

- `SniSslStream.netcore.cs` (line 20-21) — read/write async semaphores
- `SniNetworkStream.netcore.cs` (line 20-21) — read/write async semaphores

## Changes Required

### Option A: Replace with SemaphoreSlim

`SemaphoreSlim` already supports `WaitAsync()` and is optimized for the common uncontended case (no
allocation when the semaphore is available):

```csharp
// Current ConcurrentQueueSemaphore with custom TCS queue
// Replace with:
private readonly SemaphoreSlim _readSemaphore = new SemaphoreSlim(1, 1);
private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
```

Pros:

- `SemaphoreSlim.WaitAsync()` is highly optimized in .NET
- No TCS allocation in the uncontended case
- Well-tested, maintained by the runtime team

Cons:

- Loses strict FIFO ordering (SemaphoreSlim doesn't guarantee fairness)
- For SNI stream locking with count=1, FIFO may not matter much

### Option B: Use IValueTaskSource Pattern

For maximum efficiency, implement a custom `IValueTaskSource` that reuses a single allocation:

```csharp
// Pool a single IValueTaskSource per semaphore instance
// Reuse it for each wait operation
```

This is more complex but eliminates all per-operation allocations.

### Recommendation

**Option A (SemaphoreSlim)** — the simplicity outweighs the FIFO concern. FIFO ordering matters for
fairness in high-contention scenarios, but the SNI stream semaphores (count=1) guard I/O operations
that are naturally serialized. Under low contention, `SemaphoreSlim.WaitAsync()` completes
synchronously with zero allocation.

## Testing

- Benchmark: High-contention read/write scenario — measure allocation reduction
- Integration test: MARS with many concurrent sessions (stresses the locks)
- Verify no behavior change in ordering or correctness

## Risk

- Low — `SemaphoreSlim` is a standard replacement for custom semaphore implementations
- If FIFO ordering turns out to matter, can be reverted

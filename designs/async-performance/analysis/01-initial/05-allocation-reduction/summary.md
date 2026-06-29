# Priority 5: Reduce Allocation Overhead in Async Paths

**Addresses:** #593, #2408, Discussion #3918
**Impact:** Medium — reduces GC pressure and improves throughput
**Effort:** Low-Medium (incremental improvements)

## Current State

### Already Using ArrayPool

The codebase already uses `ArrayPool<byte>.Shared` in several places:

- `SniNativeWrapper.cs` line 233 — SPN composition
- `SqlSequentialTextReader.cs` lines 363-421 — text streaming
- `TdsParser.cs` lines 1426, 3560, 3573, 12962-13082 — packet and value buffers

### Not Pooled — Allocation Hotspots

| Location | Allocation | Called From |
| ---------- | ----------- | ------------- |
| `TdsParserStateObject._inBuff` / `_outBuff` | `new byte[size]` per connection | Connection creation |
| `StateSnapshot.PacketData` buffers | `new byte[read]` per packet | Async reads (snapshot chain) |
| `ConcurrentQueueSemaphore` | `new TaskCompletionSource<bool>` per contended wait | Every contended SNI read/write |
| `CancellationToken.Register()` | Callback allocation per async op | Every async command/read |
| `ValueUtilsSmi.SetChars_FromReader` | `new char[chunkSize]` per column | TVP string processing |

## Incremental Fixes

| # | Fix | Complexity | Impact |
| --- | ----- | ----------- | -------- |
| 1 | [Pool TDS packet buffers (inBuff/outBuff)](01-tds-buffer-pool.md) | Low | Medium |
| 2 | [Pool snapshot packet data buffers](02-snapshot-buffer-pool.md) | Low | Medium |
| 3 | [Optimize CancellationToken handling](03-cancellation-optimization.md) | Low | Low-Medium |
| 4 | [Pool char buffers in SetChars_FromReader](04-setchars-buffer-pool.md) | Low | Low |
| 5 | [Replace ConcurrentQueueSemaphore TCS allocation](05-semaphore-optimization.md) | Medium | Low |

## Dependencies

- All fixes are independent of each other
- Fix 2 overlaps with P2 Fix 3 (snapshot buffer pool) — they are the same change
- These can be done at any time, in any order

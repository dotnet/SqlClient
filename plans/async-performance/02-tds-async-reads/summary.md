# Priority 2: Rewrite Async TDS Read Path

**Addresses:** #593, #1562
**Impact:** High — eliminates 250x slowdown for large async reads
**Effort:** Very High

## Current State

The TDS parser's async read path uses a snapshot/replay mechanism implemented in
`TdsParserStateObject.cs` (lines 4655–5200+). Key components:

- **`StateSnapshot` class** (line 4655) — stores a linked list of `PacketData` nodes, each capturing
  a received packet's buffer and metadata

- **`SnapshotStatus` enum** — `NotActive`, `ReplayStarting`, `ReplayRunning`, `ContinueRunning`
- **`_snapshot` / `_cachedSnapshot` fields** (lines 259–263) — current and cached snapshot

### The Replay Problem

When an async read needs more data:

1. `TryReadNetworkPacket()` (line 3400) creates a snapshot via `CaptureAsStart()`
2. Returns `NeedMoreData` to the caller
3. When new data arrives, `ProcessSniPacket()` appends to the snapshot chain
4. The entire read is replayed from the start via `MoveToStart()` → re-parses all packets
5. This repeats for every new packet → O(n²) total work

### Recent Improvements

- **Continue points** (`CaptureAsContinue`, `MoveToContinue`) — allow some operations to resume from
  a midpoint instead of the start. This is already partially implemented and reduces replayed work
  for some operations.

- **`_asyncReadWithoutSnapshot` field** (line 263) — an alternative mode that skips snapshotting
  entirely, enforced by XOR assertion at line 3504

- **PR #3377** — improved async string reading perf
- **PR #3534** — fixed async multi-packet handling
- **PR #2714** — added partial packet detection
- **PR #2663** — introduced TDS Reader abstraction

## Incremental Fixes

| # | Fix | Complexity | Impact |
| --- | ----- | ----------- | -------- |
| 1 | [Expand continue-point coverage](01-continue-points.md) | Medium | Medium |
| 2 | [Eliminate snapshot replay for PLP reads](02-plp-streaming.md) | High | High |
| 3 | [Pool snapshot packet buffers](03-snapshot-buffer-pool.md) | Low | Medium |
| 4 | [Async-without-snapshot for sequential access](04-async-no-snapshot.md) | Medium | High |
| 5 | [State machine TDS parser (long-term)](05-state-machine-parser.md) | Very High | Very High |

## Dependencies

- Fixes 1–4 are incremental and can be done independently
- Fix 5 is a long-term architectural change that subsumes 1–4
- All fixes are independent of the connection pool work (P1)

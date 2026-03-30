# Priority 2: Rewrite Async TDS Read Path

**Addresses:** #593, #1562
**Impact:** High — eliminates 250x slowdown for large async reads
**Effort:** ~~Very High~~ High (revised — see AppContext switch analysis below)

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

### Existing Experimental Continuation Mode (AppContext Switches)

> **Key finding from the [AppContext switch analysis](../appcontext-switches.md):** A continuation-
> based PLP read path already exists behind two experimental switches. This significantly overlaps
> with Fixes 1 and 2 below and reduces the estimated effort for this priority area.

The following switch dependency chain gates the continuation mode:

```
UseCompatibilityProcessSni = false        (default: true)
     └─ enables → UseCompatibilityAsyncBehaviour = false   (default: true)
          └─ enables → Snapshot.ContinueEnabled = true
               └─ enables → continuation-based PLP reads
```

When both switches are set to `false`:

- **`TryReadByteArrayWithContinue`** (line 1708) resumes from the last offset instead of restarting
- **`TryReadStringWithContinue`** (line 2166) resumes mid-stream for XML/string data
- **`TryReadPlpBytes`** (line 2329) passes `canContinue=true`, enabling offset tracking via
  `AddSnapshotDataSize`/`GetPacketDataOffset`
- **`ProcessSniPacket`** uses a new multiplexer path (`TdsParserStateObject.Multiplexer.cs`) that
  reassembles partial TDS packets, guaranteeing the snapshot sees only complete packets

This continuation mode eliminates the O(n²) replay for multi-packet PLP column reads (the exact
operations causing the 250x slowdown), but it is gated behind `UseCompatibilityProcessSni=false`
because it depends on the packet multiplexer and is considered experimental.

**Setting `UseCompatibilityAsyncBehaviour=false` alone has no effect** — the property getter is
hard-overridden to return `true` when `UseCompatibilityProcessSni` is still `true`.

### Other Recent Improvements

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

| # | Fix | Complexity | Impact | Notes |
| --- | ----- | ----------- | -------- | ----- |
| 1 | [Expand continue-point coverage](01-continue-points.md) | Medium | Medium | ⚠️ Overlaps with existing continuation mode behind switches — verify gaps |
| 2 | [Eliminate snapshot replay for PLP reads](02-plp-streaming.md) | ~~High~~ Medium | High | ⚠️ Largely implemented behind `UseCompatibilityAsyncBehaviour=false` — scope may reduce to hardening and enabling by default |
| 3 | [Pool snapshot packet buffers](03-snapshot-buffer-pool.md) | Low | Medium | Unaffected by switch analysis |
| 4 | [Async-without-snapshot for sequential access](04-async-no-snapshot.md) | Medium | High | Unaffected by switch analysis |
| 5 | [State machine TDS parser (long-term)](05-state-machine-parser.md) | Very High | Very High | Urgency reduced if continuation mode covers worst cases |

## Revised Assessment

The primary path forward for P2 may be **hardening and enabling the existing continuation mode** (by
making `UseCompatibilityProcessSni=false` the default or removing the compat gate) rather than
writing new continuation logic from scratch. This reframes the work as:

1. **Audit** the multiplexer path (`TdsParserStateObject.Multiplexer.cs`) and continuation logic for
   correctness across connection resets, MARS sessions, and attention signals
2. **Test** the `UseCompatibilityProcessSni=false` + `UseCompatibilityAsyncBehaviour=false` path
   against the benchmark scenarios from #593 to validate that the O(n²) replay is eliminated
3. **Identify gaps** — determine which PLP read operations are _not_ covered by the existing
   continuation mode and target those for additional continue-point coverage (Fix 1)
4. **Harden** — address stricter `AppendPacketData` assertions in the multiplexer path that may
   trigger in edge cases
5. **Graduate** — plan a phased rollout (AppContext switch opt-in → default → remove compat path)

This reduces the effort from "Very High" to "High" and shifts the risk profile from "greenfield
implementation" to "stabilisation and rollout of experimental code."

## Dependencies

- Fixes 1–4 are incremental and can be done independently
- Fix 5 is a long-term architectural change that subsumes 1–4
- All fixes are independent of the connection pool work (P1)
- Fixes 1–2 should be evaluated against the existing continuation mode before starting new work

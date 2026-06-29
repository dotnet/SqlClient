# Packet-handling switch contrast — command execution

How the command-execution quick wins shift when the packet-handling AppContext switches are toggled.
Switch semantics verified against
[`LocalAppContextSwitches.cs`](../../../../../src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/LocalAppContextSwitches.cs)
and [appcontext-switches analysis](../../01-initial/appcontext-switches.md).

---

## The packet-handling switches

| Switch | Default | Controls |
| --- | --- | --- |
| `Switch.Microsoft.Data.SqlClient.UseCompatibilityProcessSni` | `true` | Master switch. `true` = legacy `ProcessSniPacketCompat` (raw buffers, no reassembly, no `Packet` objects); `false` = new multiplexer (`TdsParserStateObject.Multiplexer.cs`): partial-packet reassembly, multi-packet splitting, allocates `Packet` objects. |
| `Switch.Microsoft.Data.SqlClient.UseCompatibilityAsyncBehaviour` | `true` | PLP async-retry behaviour. `true` = restart-from-start snapshot replay (O(n²)); `false` = continuation (resume from offset). Hard-overridden to `true` whenever `UseCompatibilityProcessSni=true` — cannot be enabled alone. |
| `Switch.Microsoft.Data.SqlClient.MakeReadAsyncBlocking` | `false` | `true` forces `_syncOverAsync` in `TryProcessDone` (DONE-token reads block instead of pending). A compatibility escape hatch, not a packet path per se. |

Dependency chain: `UseCompatibilityProcessSni=false` → enables `UseCompatibilityAsyncBehaviour=false`
→ enables `Snapshot.ContinueEnabled` → continuation PLP reads.

## Two regimes

- **Compat ON** (default ship state) — `UseCompatibilityProcessSni=true`: legacy path, O(n²) replay,
  no `Packet` allocations.
- **Compat OFF** (new pipeline) — `UseCompatibilityProcessSni=false`: multiplexer + continuation,
  O(n²) eliminated for PLP, but per-received-buffer `Packet` allocation.

---

## Per-item contrast

| # | Item | Compat ON (default) | Compat OFF (multiplexer) |
| --- | --- | --- | --- |
| 1 | Pool `StateSnapshot.PacketData` buffers | **Higher (~0.80)** — the snapshot replays and retains every packet, so it is the dominant allocator (~13 GB / 10 MB read). | **Lower (~0.60)** — continuation stops retaining the full chain; the dominant allocator shifts to the multiplexer's `Packet` objects. |
| 2 | `CancellationToken` optimization | ~0.68 — unchanged (lives on the `SqlDataReader` async entry). | ~0.68 — unchanged. |
| 3 | `ConcurrentQueueSemaphore` TCS removal | ~0.60 — unchanged (stream-level locking). | ~0.60 — unchanged. |
| 4 | Expand continuation-mode coverage | **Not active.** Prerequisite is flipping the master switch (high blast); near-term value **drops (~0.45)**. | **Where it lives (~0.78).** O(n²) already gone; work is incremental hardening — low blast. |
| 5 | `SetChars_FromReader` char pool | ~0.66 — unchanged (TVP path). | ~0.66 — unchanged. |
| 6 | Pool multiplexer `Packet` objects ([CMD-6](06-multiplexer-packet-pool.md)) | **N/A** — `Packet` objects are not allocated on the compat path. | **New win (~0.65)** — becomes the steady-state allocator; pool it. |

---

## What the contrast reveals

1. **Only items 1 and 4 are switch-sensitive; 2, 3, 5 are switch-agnostic.** The CancellationToken,
   ConcurrentQueueSemaphore, and SetChars wins are safe regardless of the packet regime — the most
   robust quick wins because they do not depend on the experimental pipeline.
2. **Items 1 and 4 are inversely valued.** With Compat ON the snapshot buffer pool is the big lever
   and CMD-4 is blocked behind a master-switch flip; with Compat OFF CMD-4 is cheap hardening and
   CMD-1's payoff shrinks. Pick one large-read strategy based on the switch direction you commit to.
3. **CMD-6 is a Compat-OFF-only successor to CMD-1.** Turning on the multiplexer trades O(n²) replay
   for per-buffer `Packet` allocations, so pooling those objects becomes the new steady-state win.
4. **`MakeReadAsyncBlocking` interacts with CMD-1.** At its default (`false`), DONE-token reads also
   use the async pend/replay path, so CMD-1 helps DONE processing too. Enabling it masks some replay
   cost but reintroduces thread-blocking — a symptom mask, not a substitute.

## Recommendation

Land the switch-agnostic trio (CMD-2, CMD-3, CMD-5) first. Then make a deliberate fork decision:
either invest in CMD-1 (snapshot pooling) as a Compat-ON improvement, or commit to graduating the
multiplexer (CMD-4) and add CMD-6 (`Packet`-object pooling) — but do not fund both large-read tracks
in parallel.

## References

- [02-tds-async-reads summary](../../01-initial/02-tds-async-reads/summary.md)
- [appcontext-switches analysis](../../01-initial/appcontext-switches.md)
- [Quick-wins index](../README.md)

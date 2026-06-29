# Blocking Locks & Hot-Path Allocations — Per-Item Anchors

This pass verified the lock and allocation anchors for the locking and pooling quick-wins to the
line. 77 `blocking-sync` and 106 `allocation` findings were recorded across the hot path; the items
below are the ones tied to specific quick-wins. Full lists are in
[results/analyzer-output.md](results/analyzer-output.md).

## CE-5 — `SniTcpHandle` send/receive locks

The quick-win replaces `Monitor.Enter(this)` / `lock(this)` in `SniTcpHandle.Send` / `Receive` with
a `SemaphoreSlim`. Confirmed (all four `.NET` configurations):

| Location | Pattern | Member |
| --- | --- | --- |
| `SniTcpHandle.netcore.cs:824` | `Monitor.TryEnter` | `Send` |
| `SniTcpHandle.netcore.cs:828` | `Monitor.Enter` | `Send` |
| `SniTcpHandle.netcore.cs:835` | `lock` | `Send` |
| `SniTcpHandle.netcore.cs:864` | `Monitor.Exit` | `Send` |
| `SniTcpHandle.netcore.cs:883` | `lock(this)` | `Receive` |
| `SniTcpHandle.netcore.cs:54` | `lock(this)` | `Dispose` |

Note the `Send` path mixes a manual `Monitor.TryEnter` / `Enter` / `Exit` triple **and** a `lock`
block, and `Dispose` also takes `lock(this)`. The CE-5 note that this is *not* async-isolated is
borne out: the same monitor guards `Send`, `Receive`, and `Dispose`, so any `SemaphoreSlim` swap
must convert all of them together (the precedent for why PR #1357 reverted).

## CMD-3 — `ConcurrentQueueSemaphore` blocking wait + per-op TCS

Both halves of CMD-3 confirmed in `ConcurrentQueueSemaphore.netcore.cs` (four `.NET` configs):

| Location | Analyzer | Detail | Member |
| --- | --- | --- | --- |
| `ConcurrentQueueSemaphore.netcore.cs:31` | blocking-sync | `_semaphore.Wait()` (blocking) | `WaitAsync` |
| `ConcurrentQueueSemaphore.netcore.cs:37` | allocation | `new TaskCompletionSource` | `WaitAsync` |

A `WaitAsync` that internally calls a blocking `_semaphore.Wait()` **and** allocates a fresh
`TaskCompletionSource` per contended op is exactly the shape CMD-3 calls out — replace with
`SemaphoreSlim(1,1)` or a pooled TCS.

## CMD-5 — `ValueUtilsSmi.SetChars_*` char buffers

| Location | Detail | Member |
| --- | --- | --- |
| `ValueUtilsSmi.cs:2415` | `new char[]` | `SetChars_FromReader` |
| `ValueUtilsSmi.cs:2352` | `new char[]` | `SetChars_FromRecord` |

CMD-5 names `SetChars_FromReader`; the analyzer surfaced a sibling `SetChars_FromRecord` with the
identical `new char[]` pattern, so the `ArrayPool<char>` change should cover both TVP paths.

## CMD-1 / CMD-6 — read & multiplexer `byte[]` allocations

The snapshot/replay and multiplexer packet allocations (`all` configs):

| Location | Detail | Member | Item |
| --- | --- | --- | --- |
| `TdsParserStateObject.Multiplexer.cs:320` | `new byte[]` | `MultiplexPackets` | CMD-6 |
| `TdsParserStateObject.Multiplexer.cs:432` | `new byte[]` | `MultiplexPackets` | CMD-6 |
| `TdsParserStateObject.Multiplexer.cs:450` | `new byte[]` | `MultiplexPackets` | CMD-6 |
| `TdsParserStateObject.cs:2395,2419,2428` | `new byte[]` | `TryReadPlpBytes` | CMD-1 (PLP read) |
| `TdsParserStateObject.cs:1726` | `new byte[]` | `TryReadByteArrayWithContinue` | CMD-1 (continuation) |

`TdsParserStateObject.cs` alone accounts for 17 `byte[]` allocations and the Multiplexer partial
adds 3 more, concentrated in the PLP / continuation read paths CMD-1 and CMD-6 target. The
multiplexer allocations are `all`-config because the partial class compiles everywhere even though
the multiplexer is only *active* when `UseCompatibilityProcessSni=false` — consistent with CMD-6
being deferred past 7.1.

## Allocation hotspots by file (context)

| File | `byte[]` / `char[]` / TCS allocations |
| --- | --- |
| `TdsParser.cs` | 39 |
| `TdsParserStateObject.cs` | 17 |
| `ValueUtilsSmi.cs` | 12 |
| `SqlDataReader.cs` | 6 |
| `SqlCommand.Reader.cs` | 5 |
| `TdsParserStateObject.Multiplexer.cs` | 3 |

`TdsParser.cs` leads the count, but most of its allocations are one-time setup (`SetPacketSize`,
buffer init) rather than per-packet replay sinks — a reminder that raw counts need the per-member
context the table above provides, not just a file ranking.

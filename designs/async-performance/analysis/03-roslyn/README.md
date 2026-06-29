# 03-roslyn — Symbol-Aware Call-Site & Async-Pattern Confirmation

**Date**: 2026-06-29
**Scope**: Microsoft.Data.SqlClient async hot path (managed SNI + TDS read/command hubs)
**Inputs**: [01-initial analysis](../01-initial/README.md),
[02-graphify reports](../02-graphify/graphify-tool-evaluation.md)
**Tool**: [`apps/AsyncRoslynAnalyzer`](../../apps/AsyncRoslynAnalyzer/)

---

## Why this pass exists

The [02-graphify evaluation](../02-graphify/graphify-tool-evaluation.md) recommended keeping the
sync-over-async, lock-contention, and allocation findings on **Roslyn-based tooling that understands
C# symbols and conditional compilation**, because the graphify tree-sitter pass:

1. **Dropped real methods** — `TryReadNetworkPacket`, `ReadSniSyncOverAsync`, and `TryProcessDone`
   never appeared as nodes, so the call graph under-represented the exact sync-over-async hotspots
   the quick-wins target (the "cross-cutting caveat" in the
   [04-quick-wins README](../04-quick-wins/README.md#cross-cutting-caveat)).
2. **Ignored conditional compilation** — it does not evaluate `#if NET` / `#if NETFRAMEWORK` /
   `_WINDOWS` / `_UNIX` branches or platform file suffixes (`.netfx.cs`, `.netcore.cs`,
   `.windows.cs`, `.unix.cs`), so edges in this multi-TFM driver are incomplete or conflated.

This pass closes that gap with a small Roslyn analyzer that parses every source file **once per
shipping build configuration** with the matching preprocessor symbols and platform-suffix rules,
then runs five syntactic analyzers and deduplicates the results across configurations.

## What the tool does

The analyzer ([`apps/AsyncRoslynAnalyzer`](../../apps/AsyncRoslynAnalyzer/)) parses with
`Microsoft.CodeAnalysis.CSharp` (not a full compilation — no symbol/feed dependencies) under these
five configurations, honoring `#if` and platform file suffixes exactly as the unified
`Microsoft.Data.SqlClient.csproj` does:

- `net8.0-unix`, `net9.0-unix` — `NET`, `NETCOREAPP`, `_UNIX` (+ cumulative `*_OR_GREATER`)
- `net8.0-windows`, `net9.0-windows` — same with `_WINDOWS`
- `net462-windows` — `NETFRAMEWORK`, `NET462`, `_WINDOWS`

Each finding records the **set of configurations** in which its location is active, so
platform-specific code (`all` vs `net8.0-unix net9.0-unix ...`) is visible at a glance.

| Analyzer | Purpose | Scope |
| --- | --- | --- |
| `call-site` | Confirm exact call sites of the dropped/anchor methods | Whole tree |
| `sync-over-async` | `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, `.RunSynchronously()` | Hot path |
| `blocking-sync` | `lock` / `lock(this)`, `Monitor.Enter/TryEnter/Exit`, blocking `Wait()`/`WaitOne()` | Hot path |
| `allocation` | `new byte[]` / `new char[]` / `new TaskCompletionSource(...)` | Hot path |
| `missing-configureawait` | `await` without `.ConfigureAwait(false)` | Hot path |

"Hot path" = files under `ManagedSni/` plus the `TdsParser*`, `TdsParserStateObject*`,
`SqlDataReader*`, `SqlCommand*`, `SqlInternalConnectionTds*`, and `ValueUtilsSmi*` hubs.

## How to run it

```bash
dotnet build designs/async-performance/apps/AsyncRoslynAnalyzer -c Release

dotnet designs/async-performance/apps/AsyncRoslynAnalyzer/bin/Release/net10.0/AsyncRoslynAnalyzer.dll \
  --src src/Microsoft.Data.SqlClient/src \
  --out designs/async-performance/analysis/03-roslyn/results
```

Optional `--targets Name1,Name2` overrides the default call-site target list. Outputs land in
[`results/`](results/): `findings.json` (full machine-readable report) and `analyzer-output.md`
(generated tables).

## Results summary

344 source files scanned across 5 configurations; **238 deduplicated findings**.

| Analyzer | Findings |
| --- | --- |
| call-site | 19 |
| sync-over-async | 35 |
| blocking-sync | 77 |
| allocation | 106 |
| missing-configureawait | 1 |

### Headline confirmations

- **The three "dropped" methods are real and on the read path.** All call sites resolved under
  `all` configurations. See [call-site-confirmation.md](call-site-confirmation.md).
- **Sync-over-async is concentrated where the quick-wins said.** `SsrpClient`, `SslOverTdsStream`,
  the `SniMarsHandle` send/receive paths, and the `SqlCommand.*Async` cleanup tails. See
  [sync-over-async.md](sync-over-async.md).
- **CE-5 / CMD-1 / CMD-3 / CMD-5 / CMD-6 anchors verified to the line.** The `SniTcpHandle`
  `Monitor`/`lock(this)` send/receive locks, the `ConcurrentQueueSemaphore` blocking `Wait()` +
  per-op `TaskCompletionSource`, the `ValueUtilsSmi.SetChars_*` `char[]`, and the multiplexer
  `Packet` `byte[]` allocations all resolved. See
  [blocking-and-allocations.md](blocking-and-allocations.md).

## Limitations

This is a **syntactic** analyzer (parse trees + lexical structure), not a semantic one. It does not
resolve overloads or types, so:

- `.Result` / `.Wait()` matches are by member name; a `.Result` on a non-`Task` would be a false
  positive (none observed in the hot-path files, which only expose `Task`-shaped members here).
- Call-site matches are by method name, not full symbol — homonyms across types would merge. The
  target names here are distinctive enough that this did not occur.
- It confirms **presence and location**, not reachability from a specific `OpenAsync` entry point.
  A full `Microsoft.CodeAnalysis.Workspaces` reachability pass is the natural next step if needed.

These limitations are acceptable for the goal: *confirm the anchors the graph missed before
implementation*. Each finding still carries a file/line a human can open and verify.

## Outputs

- [results/findings.json](results/findings.json) — full report (every finding + active configs)
- [results/analyzer-output.md](results/analyzer-output.md) — generated tables, all five analyzers
- [call-site-confirmation.md](call-site-confirmation.md) — resolves the cross-cutting caveat
- [sync-over-async.md](sync-over-async.md) — sync-over-async inventory
- [blocking-and-allocations.md](blocking-and-allocations.md) — lock + allocation anchors per item

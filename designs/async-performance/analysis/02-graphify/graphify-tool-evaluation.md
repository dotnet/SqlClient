# Graphify Tool Evaluation for Async Performance Analysis

**Date**: 2026-06-29
**Purpose**: Decide which "Graphify" to adopt for the async-performance analysis phase
**Scope**: Microsoft.Data.SqlClient (multi-target C#/.NET: `net462`, `net8.0`, `net9.0`)

---

## TL;DR

The two sites that appear to offer different "Graphify" tools are **two landing pages for the same
open-source project** — PyPI package `graphifyy`, GitHub `safishamsi/graphify`, MIT license,
maintained by Safi Shamsi. The real decision is **open-source CLI vs enterprise waitlist**, not
"tool A vs tool B". Use the open-source CLI as a **navigation and recall aid**, and keep
async-specific findings on Roslyn-based tooling that understands C# symbols and conditional
compilation.

---

## The Two Pages

| Page | What it is | Install | Stated adoption |
| --- | --- | --- | --- |
| [graphify.net](https://graphify.net/) | Technical/docs page for the open-source skill | `pip install graphifyy` | 3.7k+ GitHub stars |
| [graphifylabs.ai](https://graphifylabs.ai/) | Marketing page funneling to an enterprise/cloud waitlist | `uv tool install graphifyy` | 74k+ stars, 1.1M+ downloads, 21 Fortune 500 |

Both reference the **same** artifacts: PyPI [`graphifyy`](https://pypi.org/project/graphifyy/) and
GitHub [`safishamsi/graphify`](https://github.com/safishamsi/graphify). The CLI binary is `graphify`
in both cases.

---

## What Graphify Actually Produces

- Tree-sitter AST extraction across ~19-20 languages (C# is supported) yielding function, class,
  and call-graph nodes plus docstring edges.
- LLM-driven semantic extraction from prose (docs, papers) using your own configured model API key.
- A NetworkX graph with Leiden community detection — no vector embeddings.
- "God nodes" (high-centrality hubs) and "surprise" cross-file/cross-domain edges.
- Outputs: interactive `graph.html`, queryable `graph.json`, and a `GRAPH_REPORT.md` audit report.
- Assistant integration via `/graphify`, `/graphify query`, `/graphify path`, `/graphify explain`.

It is a **codebase-recall and orientation layer for an AI assistant**, not a performance analyzer.

---

## Fit for This Codebase

### Where it helps

- Mapping the call graph and blast radius around the hubs identified in our research —
  `TdsParser`, managed SNI handles, and `ChannelDbConnectionPool`.
- Surfacing cross-file connections an assistant might otherwise miss when tracing async paths.
- Faster orientation/onboarding when reasoning about which code paths touch async I/O.

### Where it falls short

1. **Tree-sitter, not Roslyn** — it parses syntax but does not resolve symbols/overloads and does
   not evaluate `#if NETFRAMEWORK` / `#if NET` / `_WINDOWS` / `_UNIX` branches or platform file
   suffixes (`.netfx.cs`, `.windows.cs`). In a multi-TFM driver where one logical method has
   divergent platform bodies, call edges will be incomplete or conflated.
2. **No async semantics** — it will not flag the patterns our plans care about: sync-over-async
   (`.Result`, `.Wait()`, `ReadSniSyncOverAsync`), async-over-sync (`Task.Run` wrappers), or
   lock-contention edges (`lock`, `SemaphoreSlim`, `WaitHandle`). Those are the actual root causes.
3. **Outbound LLM call** — the semantic-extraction step sends content (per graphify.net, semantic
   descriptions only, never raw source) to the model behind your API key. dotnet/SqlClient is
   public, so risk is low, but the step is not fully local.

---

## Recommendation

1. Adopt the **open-source `graphifyy` CLI** (from graphify.net / the GitHub repo) as a
   navigation/recall layer over the codebase, not as a replacement for async-aware analysis.
2. Skip the graphifylabs.ai enterprise waitlist for now — it adds nothing usable today.
3. Keep the sync-over-async, lock-contention, and thread-pool-starvation findings on Roslyn-based
   tooling (`Microsoft.CodeAnalysis` scripts, Visual Studio dependency/DGML graphs) that understands
   C# symbols and conditional compilation.
4. Before relying on Graphify output for this repo, validate that its C# call edges are correct
   across at least one `#if`-heavy file (e.g., a managed SNI path) so conditional-compilation gaps
   are understood.

---

## Output Artifacts

All run outputs are retained for future analysis, grouped by tool:

```text
graphify-python/                    # Python graphifyy 0.9.1 (tree-sitter, AST-only)
  managedsni-extract/graphify-out/  # ManagedSni: graph.json, graph.html, GRAPH_REPORT.md
  driver-extract/graphify-out/      # whole driver: graph.json, graph.html, GRAPH_REPORT.md
graphify-dotnet/                    # graphify-dotnet 0.7.0 (regex, AST-only)
  managedsni-dotnet/                # ManagedSni: graph.json, graph.html, GRAPH_REPORT.md
  driver-dotnet/                    # whole driver: graph.json, graph.html, GRAPH_REPORT.md
```

Operational note: the Python `update`/`extract` commands write `graphify-out/` next to the target
path by default (not the working directory). During analysis this created untracked files under
tracked `src/`; outputs were relocated here and `src/` was confirmed clean. Always pass `--out` (or
relocate afterward) to avoid polluting the source tree.

---

## Python Run — Managed SNI (`extract`, semantic skipped)

Ran `extract` against the managed SNI source. With no LLM backend key set, `extract` **gracefully
degrades to AST-only** — it scans, extracts, makes no external call, and prints
`no LLM backend configured; keeping Community N placeholders`.

```bash
graphify extract src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ManagedSni --out <out>
graphify cluster-only <out>   # generates GRAPH_REPORT.md; Leiden clustering is local
```

- 23 files → 355 nodes, 535 edges, 21 communities, 100% EXTRACTED, `0 input · 0 output` tokens.
- Community **naming** stayed as `Community N` placeholders (needs an LLM key); Leiden clustering
  itself ran locally.
- Output retained under `graphify-python/managedsni-extract/graphify-out/`.

### God nodes (managed SNI)

| Rank | Node | Edges | Maps to async-perf hotspot |
| --- | --- | --- | --- |
| 1 | `SniPacket` | 58 | Per-packet allocation / buffer handling |
| 2 | `SniMarsHandle` | 36 | MARS multiplexing (RC4) |
| 3 | `SniTcpHandle` | 33 | `lock(this)` in `Receive` (RC4/P6) |
| 4 | `SniNpHandle` | 26 | Named-pipe handle |
| 5 | `SniMarsConnection` | 24 | Global `lock(DemuxerSync)` (RC4/P6) |
| 6 | `SniHandle` | 22 | Base handle abstraction |
| 7 | `SslOverTdsStream` | 16 | TLS-over-TDS stream |

A notable "surprising connection" it surfaced: `SniSslStream --references--> ConcurrentQueueSemaphore`
— exactly the per-read/write serialization primitive flagged in the root-cause analysis.

### Takeaway

As a **navigation aid the hubs are spot-on**: the MARS demuxer (`SniMarsConnection`,
`SniMarsHandle`) and `SniTcpHandle` are precisely the Unix starvation hotspots from the research.
But the graph still carries **no async/lock semantics** — `SniTcpHandle`'s `lock(this)` and the
`DemuxerSync` global lock appear only as ordinary nodes/`references`, not as contention edges. The
tool points you at the right files; it does not diagnose the concurrency problem.

### API key status

No usable LLM backend key is available to this agent (Copilot does not expose one), and none is set
in the environment (`GEMINI_API_KEY` / `GOOGLE_API_KEY` / `OPENAI_API_KEY` / `ANTHROPIC_API_KEY` all
unset). The semantic layer (INFERRED edges, named communities) was therefore not exercised. To run a
true semantic `extract`, set your own key — e.g. `export GEMINI_API_KEY=...` (free tier available) —
and re-run; nothing leaves the machine until that key is set.

---

## Python Run — Whole Driver (above managed SNI)

The managed SNI run only covered the SNI layer. The biggest async bottlenecks in the research live
**above** SNI — the TDS parser and reader (`SqlDataReader`, `TdsParserStateObject`, the
snapshot/replay and sync-over-async reads). So this run scanned the entire driver root in one pass
to capture the cross-layer chain (`SqlDataReader → TdsParser → TdsParserStateObject → SNI`).

```bash
graphify extract src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient --out <out>
graphify cluster-only <out>
```

- 261 files → 4925 nodes, 9015 edges, 310 communities in ~9s, AST-only, `0` token cost.
- Output retained under `graphify-python/driver-extract/graphify-out/`.

### God nodes (driver-wide)

| Rank | Node | Edges | Relevance |
| --- | --- | --- | --- |
| 1 | `SqlDataReader` | 209 | Async read entry point (`ReadAsync`, PLP reads) |
| 2 | `TdsParserStateObject` | 155 | Snapshot/replay + sync-over-async reads (RC2) |
| 3 | `SQL` | 153 | Error/resource hub |
| 4 | `SmiMetaData` | 104 | Metadata model |
| 5 | `SqlConnectionInternal` | 85 | Connection lifecycle |
| 6 | `SqlCommand` | 84 | `ExecuteReaderAsync` entry |

The top two hubs are exactly the above-SNI async hotspots, confirming the tool orients correctly at
the driver layer too.

### Coverage gap — verified against source

Graphify captured many async-path symbols (`TryReadPlpBytes`, `Snapshot` ×21, `ReadAsync` ×15,
`ExecuteReaderAsync` ×8) but produced **0 nodes** for three methods that are central to the
sync-over-async starvation story — all of which **do exist in source**:

| Method | Source location | Graphify nodes |
| --- | --- | --- |
| `TryReadNetworkPacket` | TdsParserStateObject.cs:3408 (internal) | 0 |
| `ReadSniSyncOverAsync` | TdsParserStateObject.cs:3519 (internal) | 0 |
| `TryProcessDone` | TdsParser.cs:3621 (private) | 0 |

This is a **real extraction gap**, not absence in code: the Tree-sitter pass omits some methods
(including internal/private ones) precisely where the async starvation diagnosis needs them. The
call graph is therefore incomplete at the most important spot, reinforcing that Graphify cannot be
the sole basis for async analysis — it must be paired with Roslyn-based tooling that resolves all
method symbols.

---

## .NET Port Comparison (`graphify-dotnet`)

A separate `.NET` reimplementation exists: [`elbruno/graphify-dotnet`](https://github.com/elbruno/graphify-dotnet)
(NuGet `graphify-dotnet` 0.7.0, MIT) — *"A .NET 10 port of graphify ... using GitHub Copilot SDK and
Microsoft.Extensions.AI"* by Bruno Capuano. It is the origin of the `copilotsdk` provider idea; that
provider exists in this port, not in the Python CLI. (Do not confuse it with the unrelated NuGet
`Graphify`, a Roslyn source generator for class-hierarchy navigation.)

### Two decisive findings

1. **Copilot SDK provider** — via `Microsoft.Extensions.AI`, its wizard offers Azure OpenAI,
   Ollama, **Copilot SDK**, or None (AST-only). It is the only one of the two that can drive
   semantic extraction through GitHub Copilot without a separate cloud API key.
2. **Regex-based C# extraction, not Roslyn** — `src/Graphify/Pipeline/Extractor.cs` states *"Uses
   regex-based parsing as a pragmatic approach"* and uses `[GeneratedRegex]` for
   namespace/using/class/method. `Microsoft.CodeAnalysis` is **not** referenced (deps are
   `Microsoft.Extensions.AI`, `QuikGraph`, `TreeSitter.Bindings`; the C# path is regex). For this
   codebase that is *coarser* than the Python CLI's tree-sitter — no symbol resolution, no `#if`
   evaluation, and unlikely to yield a true call graph.

### Side-by-side

| Dimension | Python `graphifyy` 0.9.1 | `graphify-dotnet` 0.7.0 |
| --- | --- | --- |
| Runtime | Python 3.10+ (pipx) | .NET 10 global tool (host has 10.0.301) |
| C# extraction engine | Tree-sitter AST | Regex (`GeneratedRegex`) — coarser |
| Call-graph edges | Yes (`calls`, e.g. 65 in pool) | Unlikely / heuristic only |
| `#if` / multi-TFM | Weak | Weaker (regex ignores preprocessor) |
| Semantic providers | gemini/openai/claude/deepseek/kimi/ollama | Azure OpenAI / Copilot SDK / Ollama / None |
| No-key semantic option | Ollama only | Copilot SDK or Ollama |
| Maturity / surface | Established; many commands | Fresh single-author port; `run` + `mcp` + wizard |
| Install | `pipx install graphifyy` | `dotnet tool install -g graphify-dotnet` |

### Verdict

For **structural C# fidelity** the Python CLI (tree-sitter) beats the dotnet port (regex), though
*neither* uses Roslyn, so both miss the internal sync-over-async methods that matter
(`TryReadNetworkPacket`, `ReadSniSyncOverAsync`, `TryProcessDone`). The dotnet port's one real
advantage is the **Copilot SDK semantic layer** — valuable if conceptual/semantic edges matter more
than call-graph precision, but it does not improve C# structural accuracy.

---

## Run Results: `graphify-dotnet` (AST; Copilot SDK blocked)

### Install notes

- `dotnet tool install -g graphify-dotnet` fails inside the repo (its `NuGet.config` restricts feeds
  to the SqlClient Azure DevOps feed); install from outside the repo with
  `--add-source https://api.nuget.org/v3/index.json`.
- The tool's command is `graphify`, which **collides** with the Python CLI's `graphify`. Invoke the
  dotnet one by full path (`~/.dotnet/tools/graphify`) to disambiguate.

### Copilot SDK could not be exercised

The `copilotsdk` provider uses `GitHub.Copilot.SDK` (`CopilotClient`), which requires the **real
GitHub Copilot CLI** (`@github/copilot`) installed and **interactively authenticated**. Getting that
far still did not yield a working semantic run, for three successive reasons:

1. **No CLI present initially** — the only `copilot` was the VS Code Copilot Chat stub. With
   `--provider copilotsdk` the tool **silently fell back to AST-only**; forcing the stub onto `PATH`
   made the run **hang** on `CopilotClient.StartAsync()`.
2. **Bundled Linux binary missing** — after installing Node 22 and `@github/copilot@1.0.65`
   (authenticated via `copilot /login`), the SDK still failed:
   `Copilot CLI not found at .../runtimes/linux-x64/native/copilot`. graphify-dotnet 0.7.0 ships
   **no `linux-x64` native copilot** and exposes no `--cli-path`. A symlink from that path to
   `/usr/bin/copilot` was required as a workaround.
3. **Protocol version mismatch (hard blocker)** — with the symlink, the SDK launched the CLI and
   handshook, then failed:
   `Deserializing JSON-RPC result to type PingResponse failed: The JSON value could not be converted
   to System.Int64. Path: $.timestamp`. The 0.7.0 SDK expects `PingResponse.timestamp` as `Int64`,
   but Copilot CLI 1.0.65 returns a non-integer timestamp. This is baked into the precompiled SDK
   DLL and cannot be fixed from the CLI side.

**Conclusion:** the Copilot SDK semantic layer is **unusable** with graphify-dotnet 0.7.0 against the
current Copilot CLI. It would need either an older Copilot CLI whose ping returns an integer
timestamp, or a newer graphify-dotnet/SDK build. The semantic layer therefore remains unexercised on
both tools; a working no-key alternative would be the `ollama` provider (local model, not installed
here).

**Root cause (version gap):** graphify-dotnet 0.7.0 is the latest release (only `0.5.0`–`0.7.0`
exist) and bundles **`GitHub.Copilot.SDK` 0.2.1**, which expects the old Copilot CLI ping protocol
(integer `timestamp`). The installed Copilot CLI is **1.0.65** (non-integer timestamp), and the
upstream SDK is already at **1.0.4**. No authentication method (the `gh-copilot` extension, VS Code
session, or `copilot auth login` per the project's `docs/setup-copilot-sdk.md`) changes the wire
protocol, so the mismatch cannot be worked around from the CLI/auth side — only by the project
updating its bundled SDK.

### Structural comparison (driver tree, AST-only)

Python output: `graphify-python/driver-extract/graphify-out/`; dotnet output:
`graphify-dotnet/driver-dotnet/`.

| Metric | Python `graphifyy` (tree-sitter) | `graphify-dotnet` (regex) |
| --- | --- | --- |
| Nodes | 4925 | 4955 |
| Edges | 9015 | 9127 |
| Edge relations | typed: `references` 3755, `method` 2252, `calls` 1807, `contains` 1015, … | **untyped** (all `NONE`) |
| Hub type | class-centric | file-centric |
| Top hubs (by degree) | `SqlDataReader` 209, `TdsParserStateObject` 155, `SQL` 153 | `SqlUtil.cs` 324, `TdsParser.cs` 247 (files) |
| God-node report | meaningful | degenerate (lists 2-edge nodes) |

### The surprising twist — complementary gaps

The three internal sync-over-async methods the Python tree-sitter pass **missed** are actually
**captured** by the dotnet regex pass:

| Method | Python nodes | dotnet nodes |
| --- | --- | --- |
| `TryReadNetworkPacket` | 0 | 1 |
| `ReadSniSyncOverAsync` | 0 | 1 |
| `TryProcessDone` | 0 | 1 |

So the regex extractor sees more method *names* (its pattern matches `internal TdsOperationStatus
TryReadNetworkPacket(`), but it produces **untyped, file-centric** edges with **no call graph**
(`calls` relation count: 0) and a broken god-node analysis. The tree-sitter extractor produces a
**typed call graph with class hubs** but drops some internal methods. Neither is Roslyn-grade; their
weaknesses are complementary.

### Verdict (after running both)

For async-performance navigation the **Python CLI remains more useful** (typed `calls`/`references`
edges and class-level hubs that match the known hotspots). `graphify-dotnet`'s one differentiator —
Copilot-driven semantic extraction — is unavailable without an interactive GitHub Copilot CLI login,
and its AST graph is structurally weaker (untyped, file-centric, degenerate hubs). Recommendation
stands: use Graphify (either flavor) only as a navigation aid, paired with Roslyn-based tooling.

---

## References

- Open-source docs: [graphify.net](https://graphify.net/)
- Enterprise/cloud waitlist: [graphifylabs.ai](https://graphifylabs.ai/)
- Source: [github.com/safishamsi/graphify](https://github.com/safishamsi/graphify)
- Package: [pypi.org/project/graphifyy](https://pypi.org/project/graphifyy/)

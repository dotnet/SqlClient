# Quick Wins — Targeted Async Performance Changes

**Date**: 2026-06-29
**Scope**: Microsoft.Data.SqlClient async performance, excluding connection pooling
**Inputs**: [01-initial analysis](../01-initial/README.md),
[02-graphify reports](../02-graphify/graphify-tool-evaluation.md),
[03-roslyn confirmation](../03-roslyn/README.md)

---

## Purpose

A best-bang-for-the-buck shortlist of **targeted** async changes for two areas — **connection
establishment** and **command execution**. Connection pooling is intentionally excluded: the
`ChannelDbConnectionPool` is treated as already shipped and the default customers use, so serialized
physical-open issues (the pool half of #601) are considered solved.

Each candidate links to existing GitHub issues and carries a confidence score. Command execution
lists six items; the two that require the packet multiplexer are marked Compat-OFF and deferred past
the 7.1 release (where these improvements land with the packet switches still at their defaults).

> **Test coverage & unit-test effort** for every item below is analyzed in
> [07-test-coverage](../07-test-coverage/README.md). The **benchmarking baselines and regression
> guards** each item must be measured against are specified in
> [06-benchmarking](../06-benchmarking/README.md).

## Scoring legend

Four preference criteria, each rated **L / M / H**. The *preferred* direction is:

- **Blast radius** — prefer **Low** (fewer code paths / users affected)
- **Testability** — prefer **High** (easy to cover with unit tests)
- **Locality** — prefer **High** (change confined to one file/class)
- **Cohesion** — prefer **High** (change stays within one logical unit)

Two further risk-lowering flags are tracked per item:

- **Async-isolated** — `Y` if the change is confined to the async path, leaving the heavily-used
  synchronous `Open()` / `Read()` paths untouched; `N` if it modifies shared sync+async code.
- **Flag-gated** — `Y` if it should ship behind a `LocalAppContextSwitches` opt-in for a runtime
  kill switch; `Opt` if gating is optional (pure behaviour-preserving change).

The command-execution table adds a **Regime (7.1)** column: `Any` = works at the 7.1 default
(`UseCompatibilityProcessSni=true`, packet switches off); `Compat OFF — defer past 7.1` = requires
the multiplexer, which is not planned to be default-enabled in 7.1.

**Confidence** (0.0–1.0) is the likelihood the change delivers a measurable async win at acceptable
risk; it now factors in async-path isolation and flag-gatability.

---

## Connection establishment (`OpenAsync` physical-connect path)

After the pool removes serialized creation, each *physical* connection still blocks a thread for the
whole TCP-connect → TLS → pre-login → login sequence (managed SNI on Unix, and managed-on-Windows).
Native SNI on Windows has no async open API and is out of scope.

| # | Targeted change | Primary file(s) / hub | GitHub issue(s) | Blast / Test / Locality / Cohesion | Async-isolated | Flag-gated | Confidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| [1](connection-establishment/01-async-tcp-connect.md) | Make TCP connect truly async — drive `Socket.ConnectAsync` end-to-end through the `SniTcpHandle` ctor instead of synchronously completing `TryConnectParallel` / `Socket.Select` | `ManagedSni/SniTcpHandle.netcore.cs` (hub: `SniTcpHandle`, 33 edges) | [#979](https://github.com/dotnet/SqlClient/issues/979), [#601](https://github.com/dotnet/SqlClient/issues/601), [#3118](https://github.com/dotnet/SqlClient/issues/3118) | M / M / H / H | Y | Y | 0.78 |
| [2](connection-establishment/02-async-tls-handshake.md) | Async TLS handshake — use `AuthenticateAsClientAsync` (not sync-awaited) on the open path | `ManagedSni/SniSslStream.netcore.cs`, `SslOverTdsStream` | [#979](https://github.com/dotnet/SqlClient/issues/979) | L / M / H / H | Y | Y | 0.72 |
| [3](connection-establishment/03-async-dns-resolution.md) | Async DNS resolution — replace blocking `Dns.GetHostAddresses` with `GetHostAddressesAsync` in the connect helper | `ManagedSni/SniTcpHandle.netcore.cs` | [#979](https://github.com/dotnet/SqlClient/issues/979), [#601](https://github.com/dotnet/SqlClient/issues/601) | L / H / H / H | Y | Y | 0.66 |
| [4](connection-establishment/04-async-prelogin-read.md) | Async pre-login read — remove the `ReadSniSyncOverAsync` blocking read in `ConsumePreLoginHandshake` (the misleading "pre-login handshake" starvation error) | `TdsParser.cs` (PreLogin), `TdsParserStateObject.cs` | [#3118](https://github.com/dotnet/SqlClient/issues/3118), [#1530](https://github.com/dotnet/SqlClient/issues/1530) | M / M / M / H | Y | Y | 0.60 |
| [5](connection-establishment/05-semaphoreslim-handle-locks.md) | Replace `Monitor.Enter(this)` / `lock(this)` in `SniTcpHandle.Send` / `Receive` with `SemaphoreSlim` so login-phase reads do not hold blocking locks | `ManagedSni/SniTcpHandle.netcore.cs` | [#2418](https://github.com/dotnet/SqlClient/issues/2418), [#1530](https://github.com/dotnet/SqlClient/issues/1530) | M / M / H / M | N | Y | 0.50 |

All connection-establishment items are independent of the packet-handling switches (Regime: Any).
Items 1–3 are the cleanest bang-for-buck: async-path isolated, flag-gatable, a single managed-SNI
file, no protocol changes. Item 5 dropped to 0.50 — it is **not** async-isolated (the lock is shared
by sync and async send/receive) and touches MARS-adjacent locking that bit reverted PR #1357.

---

## Command execution (`ExecuteReaderAsync` / `ReadAsync` path)

The driver-report hubs `SqlDataReader` (209 edges) and `TdsParserStateObject` (155 edges) are
exactly where these land.

| # | Targeted change | Primary file(s) / hub | GitHub issue(s) | Blast / Test / Locality / Cohesion | Async-isolated | Flag-gated | Regime (7.1) | Confidence |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| [1](command-execution/01-snapshot-buffer-pool.md) | Pool the per-packet `StateSnapshot.PacketData` buffers with `ArrayPool<byte>` (today each replayed packet does `new byte[read]`; ~13 GB allocated for a 10 MB read) | `TdsParserStateObject.cs` (`StateSnapshot`) | [#593](https://github.com/dotnet/SqlClient/issues/593), [#2408](https://github.com/dotnet/SqlClient/issues/2408) | L / H / H / H | Y | Y | Any (peaks Compat ON) | 0.74 |
| [2](command-execution/02-cancellationtoken-optimization.md) | Optimize `CancellationToken` handling in `ReadAsync` — skip registration when `CancellationToken.None`, use `UnsafeRegister`, cache the registration | `SqlDataReader.cs`, `SqlCommand.Reader.cs` | [#2408](https://github.com/dotnet/SqlClient/issues/2408) | L / H / H / H | Y | Opt | Any | 0.68 |
| [3](command-execution/03-concurrentqueuesemaphore-tcs.md) | Remove the per-contended-op `TaskCompletionSource` allocation in `ConcurrentQueueSemaphore` (graphify flagged `SniSslStream → ConcurrentQueueSemaphore`); replace with `SemaphoreSlim(1,1)` or a pooled TCS | `ManagedSni/ConcurrentQueueSemaphore.netcore.cs`, `SniSslStream` / `SniNetworkStream` | [#2418](https://github.com/dotnet/SqlClient/issues/2418) | M / M / H / H | Y | Y | Any | 0.60 |
| [5](command-execution/05-setchars-char-pool.md) | Pool the `char[]` allocated per column in `SetChars_FromReader` with `ArrayPool<char>` (TVP streaming) | `ValueUtilsSmi.SetChars_FromReader` | [Discussion #3918](https://github.com/dotnet/SqlClient/discussions/3918) | L / H / H / H | N | Opt | Any | 0.66 |
| [4](command-execution/04-continuation-mode-coverage.md) | Expand continuation-mode coverage — verify/extend `*WithContinue` PLP reads so the `UseCompatibilityProcessSni=false` path covers all multi-packet column reads, then harden the multiplexer (kills the O(n²) replay) | `TdsParserStateObject.cs`, `TdsParserStateObject.Multiplexer.cs` | [#593](https://github.com/dotnet/SqlClient/issues/593), [#1562](https://github.com/dotnet/SqlClient/issues/1562) | H / M / M / H | N | Y | Compat OFF — defer past 7.1 | 0.68 |
| [6](command-execution/06-multiplexer-packet-pool.md) | Pool multiplexer `Packet` objects allocated per received buffer when the multiplexer is active | `TdsParserStateObject.Multiplexer.cs` | [#593](https://github.com/dotnet/SqlClient/issues/593), [#1562](https://github.com/dotnet/SqlClient/issues/1562) | L / H / H / H | N | Y | Compat OFF — defer past 7.1 | 0.65 |

Rows are ordered for the 7.1 reality: items 1, 2, 3, 5 work at the 7.1 default (Compat ON) and come
first, then the Compat-OFF items (4, 6). Items 1 and 2 are textbook quick wins — async-isolated, low
blast radius, trivially unit-testable. CMD-1 is especially well-suited to 7.1: its payoff **peaks**
under the default Compat-ON replay path. Items 4–6 are **not** async-isolated: 4 and 6 change the
shared packet path and require the multiplexer; item 5's `SetChars_FromReader` runs on both TVP paths
but is purely allocation-preserving. Item 4 has the **highest raw impact** (the 250x large-read
slowdown) but needs the non-default multiplexer, so it is deferred past 7.1. Item 3 is broadly
beneficial but touches concurrency-sensitive stream locks.

### Packet-handling switch sensitivity

CMD-1 and CMD-4 are the switch-sensitive items, and CMD-6 exists only when the multiplexer is
enabled (`UseCompatibilityProcessSni=false`); CMD-2, CMD-3, and CMD-5 are switch-agnostic. CMD-1
peaks under the 7.1 default (Compat ON); CMD-4 and CMD-6 require Compat OFF and are deferred past
7.1. See the full [packet-handling switch contrast](command-execution/switch-contrast.md) for the
per-item breakdown across both regimes.

---

## Cross-cutting caveat

The graphify tree-sitter pass dropped `TryReadNetworkPacket`, `ReadSniSyncOverAsync`, and
`TryProcessDone` (verified to exist in source), so its call graph under-represents the
sync-over-async hotspots several of these items target. The file/line anchors above come from the
01-initial analysis, not the graph.

The recommended Roslyn pass has now been run — see
[03-roslyn](../03-roslyn/README.md). It re-derives the exact call sites with conditional-compilation
correctness and **confirms all three dropped methods plus every CE/CMD anchor** referenced here (see
[call-site-confirmation.md](../03-roslyn/call-site-confirmation.md),
[sync-over-async.md](../03-roslyn/sync-over-async.md), and
[blocking-and-allocations.md](../03-roslyn/blocking-and-allocations.md)).

---

## Risk-lowering considerations

Beyond the four core criteria, the following reduce delivery risk for these changes.

### Ship safety and reversibility

- **Flag-gating** — ship behind a `LocalAppContextSwitches` opt-in (the repo's established pattern)
  so changes default off and have a runtime kill switch without a redeploy. Most important for the
  non-sync-isolated items (CE-5, CMD-4, CMD-3).
- **Staged rollout** — preview / opt-in → default → remove compat path.
- **Telemetry** — add or lean on EventSource counters (open latency, thread-pool starvation,
  allocation) so regressions are observable in the field, not silent.

### Correctness preservation

- **Async-path isolation** — prefer changes that leave the heavily-used sync `Open()` / `Read()`
  paths untouched (the `Async-isolated` column).
- **Behavioural contracts** — preserve exception types, error messages, and timeout/cancellation
  timing byte-for-byte; consumers pattern-match on these.
- **Platform/TFM confinement** — prefer managed-SNI `.netcore.cs` changes so `net462` and native
  SNI are untouched; keep `#if NET` / `_WINDOWS` / `_UNIX` correct.
- **Switch-interaction matrix** — validate against both `UseCompatibilityProcessSni` modes plus
  `MakeReadAsyncBlocking` and `UseConnectionPoolV2`.

### Item-type hazards

- **Pooling (CMD-1, CMD-5)** — `ArrayPool` double-return / use-after-return corrupts data; enforce
  rent↔return ownership, balance asserts, and debug-mode buffer poisoning.
- **Locking (CE-5, CMD-3)** — deadlock/livelock and ordering changes (`SemaphoreSlim` is not FIFO);
  require stress plus a 1-hour soak and MARS-on/off validation. PR #1357 is the cautionary precedent.

### Process

- **Single-purpose PRs** — one change per PR for clean bisect and revert.
- **Benchmark baseline before merge** — tie each item to a measured delta (PerfLab / BenchmarkDotNet)
  via the Feature 42261 baseline workstream. The measurement rig, baselines, and regression guards
  each item depends on are specified in [06-benchmarking](../06-benchmarking/README.md).
- **Deterministic tests** — use injectable seams (counting `ArrayPool`, fake schedulers) over
  wall-clock assertions; flaky concurrency tests are their own risk.
- **Sequencing** — order items sharing a file/subsystem (CE-1 + CE-3; CE-2 / CE-5 / CMD-3;
  CMD-1 + CMD-4) to avoid merge churn and stacked risk.

---

## Suggested sequencing

1. **7.1 default-shippable, async-isolated** — land first: command 1, 2; connection 3. Low blast
   radius, sync paths untouched, trivially testable. Command 1 peaks under the 7.1 Compat-ON default.
2. Then the async-open core (connection 1, 2) and the remaining allocation item (command 5).
3. Flag-gate and benchmark the non-async-isolated, in-scope items (command 3; connection 4, 5)
   before default-on.
4. **Post-7.1, behind the multiplexer** — command 4 (continuation-mode graduation) and command 6
   (`Packet`-object pooling), once `UseCompatibilityProcessSni=false` is ready to be default.

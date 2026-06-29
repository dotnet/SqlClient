# 07 — Test coverage and unit-test effort for the proposals

**Date**: 2026-06-29
**Scope**: Existing test coverage, gaps, and rough effort to fully unit-test each
[04-quick-wins](../04-quick-wins/README.md) and [05-fundamental-improvements](../05-fundamental-improvements/README.md)
proposal
**Method**: Scan of `src/Microsoft.Data.SqlClient/tests/` (UnitTests, FunctionalTests, ManualTests,
tools/TDS). Paths below are relative to that `tests/` root.

---

## Test infrastructure primer

| Project | Live SQL Server? | Notes |
| --- | --- | --- |
| `UnitTests` | **No** | xUnit; internals via `InternalsVisibleTo`; mixes true unit tests and in-process simulated-server tests (`SimulatedServerTests/`) |
| `FunctionalTests` | Mostly no | xUnit; harness/simulated-server based (e.g. `SslOverTdsStreamTest`, `MultiplexerTests`) |
| `ManualTests` | **Yes** | Integration against live SQL Server / Azure SQL |
| `PerformanceTests` / `StressTests` | Yes | BenchmarkDotNet / custom stress runner |

### The TDS Test Server

`tools/TDS/` (`Microsoft.SqlServer.TDS`, `TDS.EndPoint`, `TDS.Servers`) is a **managed, in-process
TDS protocol server**. It opens a **real loopback TCP socket on a background listener thread** (same
process as the test — `TDSServerEndPoint.cs` `TcpListener` + `ListenerThread`) and the driver
connects via `Data Source=localhost,<ephemeralPort>`. It can simulate PRELOGIN, LOGIN7, TLS
encryption negotiation, FedAuth, routing, transient errors, and arbitrary TDS tokens. Tests drive it
through `TdsServerFixture` / `TdsServer` (e.g. `UnitTests/SimulatedServerTests/*`).

### Seams that enable *true* unit tests (no socket)

- `SslOverTdsStream(Stream)` accepts any `Stream` → testable with `MemoryStream`
  (`FunctionalTests/SslOverTdsStreamTest.cs` already does this).
- `SniPacket.ReadFromStream(Stream)` accepts any `Stream` → packet read testable with `MemoryStream`.
- `TdsParserStateObject.TestHarness.cs` + `Snapshot` drives multi-packet async replay with no socket
  (`FunctionalTests/MultiplexerTests.cs`).
- `Common/LocalAppContextSwitchesHelper.cs` toggles `UseCompatibilityProcessSni` etc. per test.
- `TDS.EndPoint/PlaceholderStream.cs` is a pass-through stream double for `ReadAsync` + token tests.

---

## True unit tests vs the TDS Test Server

Both are in-process; the practical contrast is **pure logic over a `MemoryStream`/fake** vs **the
client stack over a loopback socket served by a background thread**.

| Dimension | True unit (MemoryStream / fakes) | TDS Test Server (loopback + background thread) |
| --- | --- | --- |
| Speed | Microseconds; no I/O | Milliseconds; socket + thread handshake |
| Determinism | High — exact bytes, no timing | Lower — real async I/O, thread scheduling |
| Byte-level injection | Trivial (malformed/partial/truncated packets, split at any boundary) | Works at TDS message level, not arbitrary socket-byte corruption |
| Exercises socket / TLS / async I/O | No | **Yes** — real `Socket`, `SslStream`, pends, callbacks |
| Concurrency / threading realism | Limited (must simulate) | **Real** — listener thread, client thread pool |
| Flakiness | Very low | Port/timing/thread flakiness possible |
| Platform / CI | Runs everywhere, no network | Needs loopback + a free port |
| Best for | parse, allocation, lock-primitive, header, state-machine logic | connect, PRELOGIN/LOGIN, encryption negotiation, cancellation, routing, end-to-end integration |

**Rule of thumb:** unit-test the *logic* of a change (parsing, allocation, lock primitive, header
overlay) with pure fakes; use the TDS Test Server for the *integration* a change touches (connect,
handshake, cancellation under real async I/O). Several proposals want **both**.

Effort legend: **S** ≈ <1 day · **M** ≈ 1–3 days · **L** ≈ >3 days (test work only, assumes the
production change exists).

---

## Coverage and effort — quick wins

| Item | Existing coverage | Type | Gap | Recommended approach | Effort |
| --- | --- | --- | --- | --- | --- |
| CE-1 async TCP connect | `SimulatedServerTests/ConnectionTests`, `ManualTests/.../SplitPacketTest` | server | no `TryConnectParallel` parallel/timeout/cancel unit test | add a connect seam (injectable connector) for unit; TDS server for integration | **L** |
| CE-2 async TLS | `FunctionalTests/SslOverTdsStreamTest` (framing), `ManualTests/.../CertificateTest*` | unit + server | no `AuthenticateAsClient` negotiation unit | extend `SslOverTdsStreamTest` for the async `Memory` path; loopback `SslStream` server for handshake | **M** |
| CE-3 async DNS | SPN-only (`SniProxyGetSqlServerSPNsTest`) | unit (adjacent) | no `GetDnsIpAddresses` test | add a resolver seam (`Func`/wrapper) → true unit easy | **M** |
| CE-4 async pre-login | `SimulatedServerTests/FeatureExtensionNegotiationTests`, state-object harness | unit + server | no `ConsumePreLoginHandshake` parse unit | feed PRELOGIN bytes through the harness/`MemoryStream` | **M** |
| CE-5 handle locks | `MultiplexerTests` (not locks), `ManualTests/.../AsyncCancelledConnectionsTest` (stress) | unit + server | no lock-primitive unit, no deadlock test | unit-test the `SemaphoreSlim` primitive; MARS soak via server | **L** |
| CMD-1 snapshot buffer pool | `FunctionalTests/MultiplexerTests`, `TdsParserStateObject.TestHarness` | unit | no rent/return balance, no `TryReadPlpBytes` parse | extend harness with a counting `ArrayPool<byte>` | **S–M** |
| CMD-2 CancellationToken | `ManualTests/.../DataReaderCancellationTest`; `IdleConnectionChannelTest` (other class) | server + unit | no "skip register when None" unit | reader over `PlaceholderStream`; assert zero registrations | **M** |
| CMD-3 `ConcurrentQueueSemaphore` | **none** | — | **complete gap** | pure class + `InternalsVisibleTo` → FIFO/balance/contention unit | **S** |
| CMD-4 continuation/multiplexer | `FunctionalTests/MultiplexerTests` (modern path) + switch helper | unit | no legacy-vs-modern contrast, partial PLP coverage | run the harness under both `UseCompatibilityProcessSni` modes | **M** |
| CMD-5 SetChars char pool | `FunctionalTests/SqlDataRecordTest` (record only) | unit (adjacent) | no `SetChars_FromReader`/`_FromRecord` unit | fake reader/record + counting `ArrayPool<char>` | **S–M** |
| CMD-6 multiplexer `Packet` pool | `FunctionalTests/MultiplexerTests` (modern path) | unit | no `Packet` alloc/pool balance | extend multiplexer harness with a counting pool | **M** |

**Standout:** CMD-3 is both a complete coverage gap **and** the cheapest to close (a pure class,
already `internal`) — the best test bang-for-buck.

---

## Coverage and effort — fundamental improvements

| Design | Existing coverage | Gap | Recommended approach | Effort |
| --- | --- | --- | --- | --- |
| D1 `Memory<byte>` socket reads | `SslOverTdsStreamTest` (adjacent stream layer) | no `SniPacket.ReadFromStreamAsync` unit | drive `SniPacket.ReadFromStream(Async)` with a `MemoryStream`; assert bytes + no extra `Task` via allocation probe | **M** |
| D2 collapse staging copy | state-object harness (indirect) | no copy-count assertion | instrument the harness to count `Buffer.BlockCopy`/reads into `_inBuff`; flag-gated A/B | **L** |
| D3 in-place header overlay | partial (`TDSPreLoginToken`, packet parse) | no `TdsHeaderReader`/SMUX overlay unit | pure parse test: feed header bytes, assert fields (big-endian length) — trivial | **S** |
| D4 thin reader + channel MARS demux | `MultiplexerTests` (modern path), MARS manual/stress | no thin-path unit, demux concurrency | demux logic over fake streams for unit; MARS soak + on/off via server/stress | **L** |
| D5 streaming the easy path | `ManualTests/.../DataStreamTest` (`GetStream`/`GetBytes`/`GetChars`) | no true-unit streaming; default-change behaviour untested | TDS server feeding multi-packet PLP into `SqlSequentialStream`/`SqlSequentialTextReader` | **M** |

---

## Cross-cutting gaps worth closing first

1. **`ConcurrentQueueSemaphore` has zero tests** (CMD-3) — trivial pure-unit win; do this regardless
   of whether CMD-3 ships.
2. **No CI job forces managed SNI on Windows** (`UseManagedNetworkingOnWindows=true`). The managed
   path that every Unix customer uses is only exercised incidentally on Windows CI. A managed-forced
   leg would protect CE-1/2/3/5 and D1–D4.
3. **AppContext switches test defaults, not behaviour** (`LocalAppContextSwitchesTest`). The
   `UseCompatibilityProcessSni` on/off contrast (CMD-4/CMD-6) needs a behavioural A/B sweep using the
   existing `LocalAppContextSwitchesHelper`.
4. **Header/parse logic is thinly covered** — D3 and CE-4 both benefit from pure parse tests that
   feed crafted/truncated headers; these are cheap and catch the bounds bugs `TdsTokenBoundsTests`
   started covering.

---

## References

- Test scan of `tests/UnitTests`, `tests/FunctionalTests`, `tests/ManualTests`, `tests/tools/TDS`
  (2026-06-29).
- [04-quick-wins](../04-quick-wins/README.md), [05-fundamental-improvements](../05-fundamental-improvements/README.md)
- [03-roslyn anchors](../03-roslyn/blocking-and-allocations.md) (the exact lines each change touches)

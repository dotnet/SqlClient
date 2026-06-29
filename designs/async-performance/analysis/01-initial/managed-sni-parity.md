# Eliminating Native C++ SNI: Managed SNI Parity Roadmap

An analysis of the changes needed to bring the managed (C#) SNI implementation to full feature and
performance parity with the native C++ SNI, enabling its removal from the project.

---

## Executive Summary

The managed SNI already covers **~90% of the native SNI's feature surface**: TCP, Named Pipes, MARS,
TLS (including TDS 8.0 / TLS First), SSRP instance discovery, SPN resolution, DNS caching,
multi-subnet failover, and Transparent Network IP Resolution are all implemented. The remaining gaps
fall into three categories:

1. **One missing protocol**: Shared Memory (`lpc:`) — Windows-only IPC
2. **Performance architecture gaps**: I/O completion model, lock granularity, buffer management
3. **.NET Framework (net462) feasibility**: Managed SNI today is `.netcore.cs`-only

The native SNI can be eliminated **for .NET 8.0+ targets** with focused work on the performance
gaps. For net462, the recommendation is to either maintain a thin native SNI shim or accept that
net462 will remain on the current native SNI until that TFM is dropped.

---

## 1. Feature Gaps

### 1.1 Shared Memory Protocol (`lpc:`)

| Aspect | Status |
|--------|--------|
| Native SNI | Full `SM_PROV` implementation |
| Managed SNI | Not implemented; `lpc:` prefix returns `ProtocolNotSupportedError` |
| Impact | Low — Shared Memory is niche (single-machine dev/test only, ~1% of connections) |

**Recommendation: Implement as `MemoryMappedFile`-based transport or deprecate.**

- Shared Memory connects via Windows kernel shared sections and synchronization events. A managed
  implementation using `System.IO.MemoryMappedFiles.MemoryMappedFile` and named `EventWaitHandle`
  is feasible but complex — it requires implementing the private SQL Server shared memory protocol
  (undocumented beyond the native SNI source).
- **Alternative**: Deprecate `lpc:` with a clear migration path (`tcp:localhost` or
  `np:\\.\pipe\...`). Shared Memory provides negligible latency benefit over TCP
  loopback on modern Windows — the kernel TCP stack's loopback optimization makes the difference
  sub-microsecond for typical queries.
- **Recommended path**: Deprecate `lpc:` with a `SqlClientDeprecationWarning` and suggest
  `tcp:` or `np:` alternatives. If telemetry shows significant usage, implement later.

### 1.2 SSPI Authentication (Net462 Only)

| Aspect | Status |
|--------|--------|
| .NET 8.0+ | `NegotiateSspiContextProvider` uses `System.Net.Security.NegotiateAuthentication` — full parity |
| net462 | Only `NativeSspiContextProvider` exists — requires native SNI for SSPI token generation |

**Recommendation: Add a net462 SSPI provider using `System.Net.Security.NegotiateStream` or direct
SSPI P/Invoke.**

- `NegotiateAuthentication` is .NET 7+ only. For net462, two options exist:
  - **Option A**: Direct P/Invoke to `secur32.dll` (`AcquireCredentialsHandle`,
    `InitializeSecurityContext`) — functionally identical to what native SNI does, but in managed
    code. This is well-understood territory; multiple open-source implementations exist.
  - **Option B**: Use `NegotiateStream` (available since .NET Framework 2.0) as a wrapper. This is
    simpler but less control over SPN handling.
- **Recommended path**: Option A (direct SSPI P/Invoke) provides the cleanest parity. The native
  SNI's SSPI code is a thin wrapper around the same Win32 APIs.

### 1.3 Summary of Feature Gaps

| Feature | .NET 8.0+ | net462 | Effort | Recommendation |
|---------|-----------|--------|--------|----------------|
| Shared Memory (`lpc:`) | Missing | Missing | Medium | Deprecate or implement later |
| SSPI (Windows Auth) | ✅ Parity | ❌ Needs native SNI | Medium | P/Invoke to secur32.dll |
| TCP | ✅ Parity | ❌ Not compiled | See §3 | Port managed SNI to net462 |
| Named Pipes | ✅ Parity | ❌ Not compiled | See §3 | Port managed SNI to net462 |
| TLS/SSL | ✅ Parity | ❌ Not compiled | See §3 | Port managed SNI to net462 |
| TLS ALPN (`Encrypt=Strict`) | ✅ Parity | ❌ See §3.3 | High | P/Invoke to SChannel or accept regression |
| MARS | ✅ Parity (functional) | ❌ Not compiled | See §3 | Port managed SNI to net462 |
| Multi-subnet failover | ✅ Parity | ❌ Not compiled | See §3 | Port managed SNI to net462 |
| DNS caching | ✅ Parity | ❌ Not compiled | See §3 | Port managed SNI to net462 |
| SSRP (SQL Browser) | ✅ Parity | ❌ Not compiled | See §3 | Port managed SNI to net462 |
| LocalDB | ✅ Windows / stub Unix | ❌ Not compiled | Low | P/Invoke identical between both |

---

## 2. Performance Gaps

These are the areas where native SNI outperforms managed SNI architecturally. Eliminating native SNI
without addressing these would regress Windows performance.

### 2.1 I/O Completion Model: IOCP vs .NET ThreadPool

**Gap**: Native SNI uses Windows I/O Completion Ports with dedicated IOCP threads. Managed SNI
routes all async I/O through the .NET thread pool.

**Impact**: Under high concurrency, managed SNI's async completions compete with application work
for thread pool threads. Native SNI's IOCP threads are separate, providing natural isolation.

**Recommendation: Leverage .NET's built-in IOCP integration — no custom work needed.**

- On Windows, .NET's `Socket.ReceiveAsync`/`SendAsync` already use IOCP internally via
  `SocketAsyncEventArgs` or the newer `ValueTask`-based overloads. The .NET thread pool's I/O
  completion port threads (`ThreadPool.GetAvailableThreads(out _, out ioThreads)`) are the same
  IOCP mechanism the native SNI uses.
- The performance gap is **not** that managed SNI lacks IOCP — it's that the managed SNI wraps
  the I/O in additional layers (`SniNetworkStream`, `SniSslStream`, `ConcurrentQueueSemaphore`)
  that add overhead and serialization. See §2.2 and §2.3.
- **Action**: Ensure the hot-path async read/write uses the most efficient .NET async socket APIs
  (`Socket.ReceiveAsync(Memory<byte>, CancellationToken)`) without unnecessary wrapping. On .NET
  8.0+, the runtime's socket implementation already uses IOCP on Windows and epoll on Linux.

### 2.2 ConcurrentQueueSemaphore Serialization (Critical)

**Gap**: `SniNetworkStream` and `SniSslStream` wrap every `ReadAsync`/`WriteAsync` call in a
`ConcurrentQueueSemaphore`, serializing all I/O to a single operation at a time per direction:

```csharp
// SniSslStream.netcore.cs
public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
{
    await _writeAsyncSemaphore.WaitAsync(ct);  // ONE write at a time
    try { await base.WriteAsync(buffer, ct); }
    finally { _writeAsyncSemaphore.Release(); }
}
```

**Impact**: This eliminates write parallelism and adds allocation overhead
(`TaskCompletionSource<bool>` per contended operation). For MARS connections with multiple active
sessions, all sessions serialize through a single write semaphore on the physical connection.

**Why it exists**: `SslStream` is not thread-safe — concurrent writes corrupt TLS framing.
`NetworkStream` has similar constraints (`Socket.SendAsync` is not safe for concurrent calls with
overlapping buffers).

**Recommendation: Replace with a write coalescing / batching design.**

1. **Write coalescing**: Instead of serializing individual writes, buffer outgoing packets in a
   `Channel<SniPacket>` and drain them sequentially on a single writer task. This avoids the
   semaphore overhead while maintaining TLS safety. It also enables Nagle-like batching — multiple
   small TDS packets can be coalesced into a single `SslStream.WriteAsync` call when the writer is
   catching up. The native SNI achieves similar behavior via its internal write queue.

2. **Read pipeline**: For reads, the `SslStream.ReadAsync` serialization is inherent (you can only
   read one TLS record at a time). The optimization here is to ensure reads don't go through
   `ConcurrentQueueSemaphore` at all — a single long-running read loop (like in `SniMarsConnection`)
   is sufficient. Replace the semaphore with a `SingleReader` channel or a simple async lock
   (`SemaphoreSlim(1, 1)` is more efficient than `ConcurrentQueueSemaphore` for single-entry).

3. **Estimated impact**: Removing the semaphore overhead on the write path alone should reduce
   per-write latency by ~1–5μs under contention and eliminate `TaskCompletionSource` allocations.

### 2.3 MARS Demultiplexer Global Lock (Critical)

**Gap**: The managed `SniMarsConnection` uses a global `lock (DemuxerSync)` that serializes ALL
MARS sessions' sends and receives on a single physical connection:

```csharp
// SniMarsConnection.netcore.cs
public void Send(SniPacket packet)
{
    lock (DemuxerSync)           // ALL sessions blocked
    {
        _lowerHandle.Send(packet);
    }
}
```

The native SNI uses **per-session critical sections** and **lock-free `DynamicQueue`** structures,
allowing sessions to operate with much finer granularity.

**Impact**: The global lock is the primary reason MARS on Linux/managed SNI is dramatically slower
than on Windows/native SNI. Under concurrent multi-statement workloads, sessions starve each other.

**Recommendation: Replace global lock with a channel-based multiplexer.**

1. **Design**: Each MARS session writes packets into a per-session `Channel<SniPacket>`. A single
   writer task drains all session channels (round-robin or priority-based) and writes to the
   physical connection sequentially — which is inherently required by the single TCP stream, but
   without blocking senders.

2. **Read demultiplexing**: The existing single-reader pattern is correct (only one read at a time
   on the physical connection). The demultiplexer should route received packets to per-session
   `Channel<SniPacket>` instances, allowing sessions to `await` their next packet independently
   without holding the global lock.

3. **Flow control**: SMUX flow control (highwater/ACK) should remain per-session but use
   `SemaphoreSlim` or channel backpressure instead of `ManualResetEventSlim` + lock.

4. **Prototype suggestion**:
   ```
   PhysicalConnection
   ├── WriterTask: reads from all session channels, writes to SslStream
   ├── ReaderTask: reads from SslStream, dispatches to session channels
   └── Sessions[]:
       ├── SendChannel: Channel<SniPacket> (bounded by flow control window)
       └── ReceiveChannel: Channel<SniPacket> (bounded by flow control window)
   ```

### 2.4 Packet Pooling and Buffer Management

**Gap**: Native SNI uses an unbounded `WritePacketCache` (LIFO stack) for aggressive packet reuse.
Managed SNI uses a bounded `ObjectPool<SniPacket>` (default size 4) backed by `ArrayPool<byte>`.

**Impact**: Under burst writes, managed SNI may exceed the pool size and allocate new packets
(triggering `ArrayPool` rentals). Under sustained load, the pool is sufficient. The constant-factor
difference is small relative to §2.2/§2.3.

**Recommendation: Tune pool sizes and consider per-connection adaptive sizing.**

- The default pool size of 4 is conservative. For MARS connections with many active sessions, this
  should scale with the session count. A pool size of `max(4, marsSessionCount * 2)` would avoid
  most spill-over allocations.
- `ArrayPool<byte>.Shared` already provides good buffer management. No fundamental change needed.
- Consider using `MemoryPool<byte>` for zero-copy scenarios where `SslStream` can write directly
  from rented memory.

### 2.5 Connection Opening: Sync-over-Async

**Gap**: Both native and managed SNI open connections synchronously. The native SNI uses
`ConnectEx` + `WaitForSingleObject` (the block happens below the CLR). The managed SNI uses
`Socket.Connect()` + `Socket.Select()` blocking poll.

**Impact**: On both platforms, `OpenAsync()` wraps the synchronous open in a background thread,
adding thread-hop overhead. But the managed blocking occurs in CLR-visible managed code, which can
interfere with thread pool hill-climbing.

**Recommendation: Implement truly async connection opening.**

- Use `Socket.ConnectAsync(EndPoint, CancellationToken)` (.NET 5+) for non-blocking connect.
- Combine with `SslStream.AuthenticateAsClientAsync()` for non-blocking TLS handshake.
- This eliminates the `WaitForPendingOpen` background thread pattern entirely.
- On net462, `Socket.ConnectAsync(SocketAsyncEventArgs)` is available as a fallback.
- **This is the single highest-ROI performance change** for managed SNI, since connection opening
  is on the critical path for every new connection and pool-miss scenario.

### 2.6 SyncOverAsync Read Path

**Gap**: The native SNI has a `m_fSupportsSyncOverAsync` fast-path that bypasses the async
machinery and does a direct synchronous socket read when appropriate. The managed SNI always goes
through the same `Receive()` path with `lock(this)` and `ReceiveTimeout` syscall.

**Impact**: The `lock(this)` in managed `Receive()` blocks ALL operations on the handle, not just
reads. The native SNI uses a more granular `CriticalSection` scoped to just the read operation.

**Recommendation:**

- Replace `lock(this)` in `SniTcpHandle.Receive()` with a dedicated read lock (`SemaphoreSlim` or
  `object _readLock`).
- For the SyncOverAsync path, avoid setting `Socket.ReceiveTimeout` on every call (it's a syscall
  on Linux). Cache the timeout value and only change it when it differs from the current setting.
- Consider implementing a fast-path equivalent: if no async read is pending, read directly from the
  socket in synchronous mode without acquiring the async serialization semaphore.

### 2.7 Performance Gap Summary

| Gap | Severity | Effort | ROI |
|-----|----------|--------|-----|
| ConcurrentQueueSemaphore serialization | Critical | Medium | High — reduces per-I/O overhead |
| MARS global lock | Critical | High | High — unblocks MARS perf on managed SNI |
| Truly async connection open | High | Medium | High — eliminates background thread + starvation |
| SyncOverAsync lock scope | Medium | Low | Medium — reduces contention on handles |
| Packet pool sizing | Low | Low | Low — minor constant-factor improvement |
| ReceiveTimeout syscall caching | Low | Low | Low — saves one syscall per read on Linux |

---

## 3. .NET Framework (net462) Feasibility

All managed SNI code currently lives in `.netcore.cs` files guarded by `#if NET`. Making it compile
for net462 requires addressing several API gaps.

### 3.1 API Availability Matrix

| API | .NET 8.0+ | net462 | Polyfill / Alternative |
|-----|-----------|--------|------------------------|
| `NegotiateAuthentication` | ✅ | ❌ | Direct P/Invoke to `secur32.dll` |
| `SslStream.AuthenticateAsClientAsync(options, CT)` | ✅ | ❌ | `AuthenticateAsClient(host, certs, protocols, checkRevocation)` |
| `SslClientAuthenticationOptions.ApplicationProtocols` (ALPN) | ✅ | ❌ | No managed alternative; see §3.3 |
| `Socket.ConnectAsync(host, port, CT)` | ✅ | ❌ | `Socket.ConnectAsync(SocketAsyncEventArgs)` |
| `Dns.GetHostEntryAsync(host, CT)` | ✅ | ❌ | `Dns.GetHostEntryAsync(host)` (no CT) |
| `X509CertificateLoader.LoadCertificateFromFile` | .NET 9+ | ❌ | `new X509Certificate2(path)` |
| `ArrayPool<byte>.Shared` | ✅ | ✅ | Via `System.Buffers` NuGet |
| `Span<T>`, `Memory<T>` | ✅ | ✅ | Via `System.Memory` NuGet |
| `ValueTask<T>` | ✅ | ❌ | Via `System.Threading.Tasks.Extensions` NuGet |
| `Channel<T>` | ✅ | ✅ | Via `System.Threading.Channels` NuGet |
| `ConcurrentQueue<T>` | ✅ | ✅ | Built-in |
| `NamedPipeClientStream` | ✅ | ✅ | Built-in |
| `MemoryMappedFile` | ✅ | ✅ | Built-in |
| `ObjectPool<T>` (custom) | ✅ | ✅ | Custom implementation, no framework dependency |

### 3.2 Recommendation for net462

**Option A: Port managed SNI to net462 with `#if` guards (Recommended if net462 must drop native
SNI)**

- Remove the `#if NET` guard from managed SNI files, replacing with `#if NETFRAMEWORK` sections
  where API differences require it.
- Add `SspiContextProvider` implementation for net462 using `secur32.dll` P/Invoke.
- Replace modern async overloads with net462-compatible equivalents behind `#if` guards.
- Add `System.Threading.Tasks.Extensions` NuGet for `ValueTask` support.
- **Effort**: Medium-High. The managed SNI code is well-structured and most APIs have net462
  equivalents. The SSPI provider is the biggest piece of new work.

**Option B: Keep native SNI for net462, managed-only for .NET 8.0+ (Recommended)**

- The project already plans to eventually drop net462 support. Investing in net462 managed SNI
  delays that transition.
- Native SNI on net462 is stable and well-tested. The performance and feature gaps are in the
  managed SNI, not the native.
- Focus engineering effort on making managed SNI excellent for .NET 8.0+ and allow net462 to
  continue using native SNI until the TFM is dropped.
- **Effort**: Low (status quo for net462).

**Recommended path: Option B.** Kill native SNI for .NET 8.0+ first, where managed SNI already
compiles and runs. Defer the net462 question until the TFM lifecycle decision is made.

### 3.3 TLS ALPN and TDS 8.0 Strict Encryption on net462

TDS 8.0 Strict encryption (`Encrypt=Strict`) requires the client to advertise the ALPN protocol
`"tds/8.0"` during the TLS ClientHello. This is a **hard blocker** for managed SNI on net462.

**How ALPN works today across targets:**

| Target | ALPN Mechanism | `Encrypt=Strict` |
|--------|---------------|-------------------|
| .NET 8.0+ (managed SNI) | `SslClientAuthenticationOptions.ApplicationProtocols` | ✅ |
| .NET 8.0+ (native SNI) | Native SChannel `SEC_APPLICATION_PROTOCOLS` | ✅ |
| net462 (native SNI) | Native SChannel `SEC_APPLICATION_PROTOCOLS` | ✅ |
| net462 (managed SNI) | **No API available** | ❌ |

**Why this is blocked**: .NET Framework's `SslStream` never exposed ALPN configuration. The
`SslClientAuthenticationOptions` class (and its `ApplicationProtocols` property) was introduced in
.NET Core 2.1 and does not exist on any version of .NET Framework (4.6.2 through 4.8.1). There is
no overload of `AuthenticateAsClient` or `AuthenticateAsClientAsync` on net462 that accepts ALPN
protocol negotiation.

The native SNI gets ALPN for free because it calls Windows SChannel directly via
`InitializeSecurityContext` with `SEC_APPLICATION_PROTOCOLS`, bypassing `SslStream` entirely.
Windows has supported ALPN at the SChannel level since Windows 8.1 / Server 2012 R2 — the OS
capability exists, but .NET Framework's managed TLS surface never wired it up.

**Code path in managed SNI** (`SniHandle.netcore.cs`):

```csharp
protected static readonly List<SslApplicationProtocol> s_tdsProtocols =
    new List<SslApplicationProtocol>(1) { new(TdsEnums.TDS8_Protocol) };  // "tds/8.0"

var sslClientOptions = new SslClientAuthenticationOptions()
{
    TargetHost = serverNameIndication,
    ApplicationProtocols = s_tdsProtocols,   // ← net462 has no equivalent
    ClientCertificates = certificate
};
await sslStream.AuthenticateAsClientAsync(sslClientOptions, token);
```

The `_tlsFirst` flag (set when `Encrypt=Strict`) selects this ALPN-bearing path in
`SniTcpHandle.EnableSsl()` and `SniNpHandle.EnableSsl()`. When `_tlsFirst` is false, the legacy
`SslStream.AuthenticateAsClient(host, certs, protocols, checkRevocation)` overload is used —
which works fine on net462 but does not support ALPN.

**Options if net462 must support `Encrypt=Strict` without native SNI:**

1. **Direct SChannel P/Invoke** — Call `AcquireCredentialsHandle` + `InitializeSecurityContext`
   with `SEC_APPLICATION_PROTOCOLS` from managed code. This replicates what the native SNI does
   but requires reimplementing TLS session management in managed P/Invoke (~1000+ lines of
   security-critical interop code). High risk, high effort.

2. **Third-party TLS library** (e.g., BouncyCastle) — Provides ALPN but adds a heavy dependency,
   introduces a second TLS implementation with its own certificate validation semantics, and has
   potential FIPS compliance issues. Not recommended for a security-sensitive data provider.

3. **Accept no `Encrypt=Strict` on net462 managed SNI** — The pragmatic choice. `Encrypt=Strict`
   is only required when connecting to SQL Server 2022+ with strict encryption policy enforced.
   The older `Encrypt=Mandatory` mode (TDS 7.x TLS-after-handshake) works without ALPN and still
   provides full transport encryption. Most net462 deployments target older SQL Server versions
   where Strict is not available.

**Recommendation**: This is a strong additional argument for §3.2 Option B (keep native SNI for
net462). The ALPN gap alone would force either a substantial SChannel reimplementation or a feature
regression for `Encrypt=Strict` — neither of which is justified given net462's expected lifecycle.

---

## 4. Recommended Execution Order

### Phase 1: Performance Parity (Prerequisite to switch default on .NET 8.0+)

1. **Truly async connection opening** (§2.5) — Replace `Socket.Select()` with
   `Socket.ConnectAsync` + `SslStream.AuthenticateAsClientAsync`. Eliminate `WaitForPendingOpen`
   background thread. This is the highest-ROI change and unblocks the async connection pool
   (`ChannelDbConnectionPool`).

2. **Replace ConcurrentQueueSemaphore with write coalescing** (§2.2) — Implement a
   `Channel<SniPacket>`-based writer for `SniSslStream` and `SniNetworkStream`. Eliminate
   `TaskCompletionSource` allocations on the write path. Maintain TLS-safety via single-writer
   draining.

3. **Fix SyncOverAsync lock scope** (§2.6) — Replace `lock(this)` in `SniTcpHandle.Receive()`
   with a dedicated read lock. Cache `Socket.ReceiveTimeout`. Add fast-path for direct sync reads.

### Phase 2: MARS Parity

4. **Redesign MARS multiplexer** (§2.3) — Replace global `lock (DemuxerSync)` with channel-based
   per-session send/receive. Single writer task drains all session channels. Single reader task
   dispatches to session channels. This is the largest change and should be validated with MARS-heavy
   benchmarks.

### Phase 3: Feature Parity

5. **Deprecate Shared Memory** (§1.1) — Add `SqlClientDeprecationWarning` for `lpc:` prefix.
   Suggest `tcp:` or `np:` alternatives. Monitor telemetry.

6. **Tune packet pool sizing** (§2.4) — Make `ObjectPool` size adaptive to MARS session count.
   Minor change, low risk.

### Phase 4: Default Switch and Native SNI Removal (.NET 8.0+)

7. **Make managed SNI the default on Windows** — Flip `UseManagedNetworkingOnWindows` to `true` for
   .NET 8.0+ builds. The AppContext switch becomes an opt-out for native SNI as a safety valve.

8. **Run full regression suite with managed-only** — Validate all functional tests, manual tests,
   and performance benchmarks with native SNI disabled.

9. **Remove native SNI interop layer** — Delete `SniNativeWrapper.cs`, `ISniNativeMethods.cs`,
   `SniNativeMethods*.cs`, `TdsParserStateObjectNative.windows.cs`, `WritePacketCache`, and all
   native SNI P/Invoke infrastructure. Remove `Microsoft.Data.SqlClient.SNI` and
   `Microsoft.Data.SqlClient.SNI.runtime` package dependencies for .NET TFMs.

### Phase 5: net462 Decision

10. **Either**: Port managed SNI to net462 (§3.2 Option A) **or** drop net462 support entirely and
    remove all `#if NETFRAMEWORK` code, native SNI, and the net462 TFM from the unified project.

---

## 5. Files Affected

### Native SNI files to eventually remove (.NET 8.0+)

| File | Purpose |
|------|---------|
| `src/Interop/Windows/Sni/SniNativeWrapper.cs` | P/Invoke wrapper |
| `src/Interop/Windows/Sni/ISniNativeMethods.cs` | Native method interface |
| `src/Interop/Windows/Sni/SniNativeMethods.netcore.cs` | .NET 8.0+ P/Invoke declarations |
| `src/Interop/Windows/Sni/SniNativeMethodsX64.netfx.cs` | net462 x64 P/Invoke |
| `src/Interop/Windows/Sni/SniNativeMethodsX86.netfx.cs` | net462 x86 P/Invoke |
| `src/Interop/Windows/Sni/SniNativeMethodsArm64.netfx.cs` | net462 ARM64 P/Invoke |
| `src/Microsoft/Data/SqlClient/TdsParserStateObjectNative.windows.cs` | Native state object |
| `src/Microsoft/Data/SqlClient/SSPI/NativeSspiContextProvider.windows.cs` | Native SSPI |

### Managed SNI files to improve

| File | Changes Needed |
|------|----------------|
| `ManagedSni/SniTcpHandle.netcore.cs` | Async connect, fix SyncOverAsync lock scope |
| `ManagedSni/SniNetworkStream.netcore.cs` | Replace ConcurrentQueueSemaphore with writer task |
| `ManagedSni/SniSslStream.netcore.cs` | Replace ConcurrentQueueSemaphore with writer task |
| `ManagedSni/SniMarsConnection.netcore.cs` | Channel-based multiplexer, remove global lock |
| `ManagedSni/SniMarsHandle.netcore.cs` | Per-session channels, flow control via backpressure |
| `ManagedSni/SniPhysicalHandle.netcore.cs` | Adaptive packet pool sizing |
| `ManagedSni/ConcurrentQueueSemaphore.netcore.cs` | Remove (replaced by write coalescing) |
| `ManagedSni/SniProxy.netcore.cs` | Shared memory deprecation warning |

### Factory / switching files to simplify

| File | Changes Needed |
|------|----------------|
| `TdsParserStateObjectFactory.windows.cs` | Remove native branch for .NET 8.0+ |
| `TdsParserStateObjectFactory.unix.cs` | Unchanged (already managed-only) |
| `LocalAppContextSwitches.cs` | Remove `UseManagedNetworkingOnWindows` switch |

---

## 6. Risk Assessment

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| **Regression on Windows** | Medium | Extensive benchmark suite; keep native SNI opt-in via AppContext switch during transition |
| **MARS behavioral differences** | Medium | Channel-based design must preserve exact SMUX flow control semantics; test with MARS-heavy workloads |
| **Shared Memory users broken** | Low | Deprecation warning gives users time to migrate; `lpc:` is rarely used outside dev scenarios |
| **net462 users impacted** | None | Unchanged if Option B is chosen |
| **Performance regression under extreme concurrency** | Medium | Profile with 1000+ concurrent connection/query workloads before switching default |
| **TLS handshake behavioral differences** | Low | Managed SNI already handles TDS 7.4 and 8.0 TLS; test against all SQL Server versions 2012–2025 |

---

## 7. Success Criteria

Before removing native SNI as the default:

1. **Functional parity**: All existing functional and manual tests pass with `UseManagedNetworkingOnWindows=true`
2. **Performance parity**: Connection open latency within 5% of native on Windows (p50, p99)
3. **Throughput parity**: Queries/sec within 5% of native under concurrent workloads (100, 500, 1000 connections)
4. **MARS parity**: Multi-session MARS throughput within 10% of native (MARS has inherent managed overhead from GC)
5. **No thread pool starvation**: Under load, managed SNI must not degrade thread pool availability worse than native SNI baseline
6. **Memory parity**: Steady-state memory usage within 15% of native (GC overhead is expected)

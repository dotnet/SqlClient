# AppContext Switches Affecting Async Behaviour

An analysis of the `AppContext` switches in Microsoft.Data.SqlClient that influence async operation
behaviour and performance. All switches are defined in `LocalAppContextSwitches.cs` and are set via
`AppContext.SetSwitch()` or `runtimeconfig.json`.

---

## Summary

| Switch | Default | Async Impact |
| ------ | ------- | ------------ |
| `MakeReadAsyncBlocking` | `false` | Forces `ReadAsync` to block synchronously in `TryProcessDone` |
| `UseCompatibilityAsyncBehaviour` | `true` | Controls snapshot continuation vs full-restart on async retries |
| `UseCompatibilityProcessSni` | `true` | Old vs new packet multiplexer in `ProcessSniPacket`; gates the other two async switches |
| `UseConnectionPoolV2` | `false` | Selects `WaitHandleDbConnectionPool` (v1) vs `ChannelDbConnectionPool` (v2, not yet implemented) |
| `UseManagedNetworkingOnWindows` | `false` | Managed vs native SNI on Windows; fundamentally different async I/O model |
| `UseMinimumLoginTimeout` | `true` | Sets 1-second floor on login timeout instead of 0 |

---

## 1. `MakeReadAsyncBlocking`

| | |
| --- | --- |
| **Full name** | `Switch.Microsoft.Data.SqlClient.MakeReadAsyncBlocking` |
| **Property** | `LocalAppContextSwitches.MakeReadAsyncBlocking` |
| **Default** | `false` |

### What it does

When enabled, the `TryProcessDone` method in `TdsParser` sets `stateObj._syncOverAsync = true`
before reading the DONE token fields (status, command, rowcount). This forces all subsequent read
operations on that state object to use the `ReadSniSyncOverAsync` path, which issues a blocking wait
for network data instead of returning `NeedMoreData` and pending.

```csharp
// TdsParser.cs:3628
private TdsOperationStatus TryProcessDone(...)
{
    if (LocalAppContextSwitches.MakeReadAsyncBlocking)
    {
        stateObj._syncOverAsync = true;  // blocks until data arrives
    }
    // reads status (UInt16), curCmd (UInt16), rowcount (Int64) synchronously
}
```

### Behaviour difference

| `false` (default) | `true` |
| --- | --- |
| `TryProcessDone` may return `NeedMoreData` and the caller retries via snapshot replay after more data arrives asynchronously | `TryProcessDone` blocks the thread until all 12 bytes (2+2+8) of the DONE token are available |
| Thread is released back to the pool while waiting | Thread is held (spins in `ReadSniSyncOverAsync`) |

### Performance implications

- **Off (default):** Better thread pool utilisation under high concurrency. The async read may need
  snapshot replay which has its own cost, but threads are not blocked.
- **On:** Eliminates snapshot replay overhead for DONE tokens, but blocks the calling thread. Can
  cause thread starvation under load. Intended as a compatibility escape hatch for applications that
  experienced regressions when async pends were introduced for DONE token processing.

### Gotchas

- This switch only affects `TryProcessDone`. It does **not** make all reads synchronous—other read
  operations in `TdsParserStateObject` are unaffected.
- Setting `_syncOverAsync = true` is **sticky** within the current read operation. Once set during
  `TryProcessDone`, subsequent reads (e.g. the attention acknowledgement check) in the same call
  chain will also block.
- The blocking wait in `ReadSniSyncOverAsync` spins calling `ReadSni` until `_inBytesRead > 0`,
  which on slow networks can hold a thread for the full network round trip.

---

## 2. `UseCompatibilityAsyncBehaviour`

| | |
| --- | --- |
| **Full name** | `Switch.Microsoft.Data.SqlClient.UseCompatibilityAsyncBehaviour` |
| **Property** | `LocalAppContextSwitches.UseCompatibilityAsyncBehaviour` |
| **Default** | `true` |

### What it does

Controls how multi-packet column reads (PLP data) handle async retries after incomplete reads.
Checked in four locations:

1. **`TryReadByteArrayWithContinue`** (line 1708) — reading multi-packet column byte arrays
2. **`TryReadStringWithContinue`** (line 2166) — reading multi-packet XML/string data
3. **`TryReadPlpBytes` overload** (line 2329) — general PLP byte stream reading
4. **`Snapshot.ContinueEnabled`** (line 5038) — snapshot continuation gate

### Behaviour difference

| `true` (default / compat) | `false` (new behaviour) |
| --- | --- |
| On async retry, **restarts from the beginning** of the PLP read. The snapshot replays all consumed packets from the start. | On async retry, **continues from where it left off**. The snapshot captures a continuation offset so the driver resumes mid-stream. |
| `Snapshot.ContinueEnabled` returns `false` | `Snapshot.ContinueEnabled` returns `true` (if continuation was requested) |
| `canContinue` passed as `false` to `TryReadPlpBytes` | `canContinue` passed as `true`, enabling `writeDataSizeToSnapshot` and offset tracking |
| Buffer stored in snapshot on partial read, but offset is not tracked | Buffer stored **with** explicit offset via `AddSnapshotDataSize`/`GetPacketDataOffset` |

### Performance implications

- **On (default / compat):** For large column values spanning many packets, every async retry
  re-reads and re-processes all previously received packets. Cost grows quadratically with the
  number of retries needed—each retry replays all prior data. Safe and well-tested.
- **Off (new):** The driver records how many bytes were already consumed and resumes from that
  point. Eliminates redundant replay of previously consumed data. Significantly faster for large
  values that span many packets (e.g. `varbinary(max)`, XML, JSON columns with multi-MB values).

### Gotchas

- **Cannot be set independently of `UseCompatibilityProcessSni`.** The property getter contains a
  hard override:
  ```csharp
  public static bool UseCompatibilityAsyncBehaviour
  {
      get
      {
          if (UseCompatibilityProcessSni)
              return true;  // forced, ignores the actual switch value
          // ...
      }
  }
  ```
  Setting `UseCompatibilityAsyncBehaviour=false` alone has **no effect** unless
  `UseCompatibilityProcessSni=false` is also set. This is a common source of confusion.
- The continuation mode (`false`) relies on the packet multiplexer providing complete, well-formed
  packets to the snapshot. If the multiplexer is not active (i.e. `UseCompatibilityProcessSni=true`),
  the snapshot may receive partial packets that break continuation offset tracking.
- The new mode is behind the compatibility default because it was introduced alongside the
  multiplexer and is considered experimental.

---

## 3. `UseCompatibilityProcessSni`

| | |
| --- | --- |
| **Full name** | `Switch.Microsoft.Data.SqlClient.UseCompatibilityProcessSni` |
| **Property** | `LocalAppContextSwitches.UseCompatibilityProcessSni` |
| **Default** | `true` |

### What it does

Controls how incoming SNI packets are processed in `TdsParserStateObject.ProcessSniPacket`. This is
the entry point for all data received from the network.

```csharp
// TdsParserStateObject.Multiplexer.cs:17
public void ProcessSniPacket(PacketHandle packet, uint error)
{
    if (LocalAppContextSwitches.UseCompatibilityProcessSni)
    {
        ProcessSniPacketCompat(packet, error);  // old path
        return;
    }
    // new multiplexer path: handles partial packets, reassembly, multi-packet splitting
}
```

### Behaviour difference

| `true` (default / compat) | `false` (new multiplexer) |
| --- | --- |
| `ProcessSniPacketCompat`: receives raw SNI buffers and passes them directly to the read buffer. No packet reassembly or splitting. | New path: maintains a `_partialPacket` state, reassembles partial TDS packets, splits buffers containing multiple complete packets. Guarantees the snapshot sees only complete TDS packets. |
| Snapshot `AppendPacketData` assertions are relaxed (guards prefixed with `UseCompatibilityProcessSni \|\|`) | Snapshot assertions enforce minimum packet length (`HEADER_LEN`) and exact data-length consistency |
| No `Packet` object allocation per received buffer | Allocates `Packet` objects to track partial/complete packet boundaries |

### Performance implications

- **On (default):** Lower per-packet overhead—no `Packet` object allocation, no partial packet
  tracking. Well-tested, stable.
- **Off:** Enables the continuation optimisation (see `UseCompatibilityAsyncBehaviour`). Has
  per-packet allocation cost for `Packet` tracking objects. Designed for better throughput on large
  multi-packet reads by enabling the continuation path.

### Gotchas

- **This is the master switch for the new async pipeline.** It gates `UseCompatibilityAsyncBehaviour`
  (see above). Setting `UseCompatibilityProcessSni=false` is a prerequisite for any of the new async
  optimisations to take effect.
- The new multiplexer path is marked as experimental. It introduces new state
  (`_partialPacket`) that must be correctly managed across connection resets, MARS sessions, and
  attention signals.
- Debug assertions in `AppendPacketData` become stricter when the multiplexer is active. Code that
  worked with the compat path may trigger assertion failures in debug builds if packets are
  malformed or partially received.
- The switch names form a dependency chain:
  ```
  UseCompatibilityProcessSni = false
       └─ enables → UseCompatibilityAsyncBehaviour = false
            └─ enables → Snapshot.ContinueEnabled = true
                 └─ enables → continuation-based PLP reads
  ```
  You must set `UseCompatibilityProcessSni=false` first; setting the others alone is silently
  overridden.

---

## 4. `UseConnectionPoolV2`

| | |
| --- | --- |
| **Full name** | `Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2` |
| **Property** | `LocalAppContextSwitches.UseConnectionPoolV2` |
| **Default** | `false` |

### What it does

Selects the connection pool implementation in `DbConnectionPoolGroup.GetConnectionPool`.

```csharp
// DbConnectionPoolGroup.cs:180
if (LocalAppContextSwitches.UseConnectionPoolV2)
{
    throw new NotImplementedException();  // v2 not yet available
}
else
{
    newPool = new WaitHandleDbConnectionPool(...);  // v1
}
```

### Behaviour difference

| `false` (default) | `true` |
| --- | --- |
| Uses `WaitHandleDbConnectionPool`: synchronisation via `WaitHandle` / `Semaphore`, blocking waits for pool slots | Intended to use `ChannelDbConnectionPool`: `System.Threading.Channels`-based async-native pool |
| Stable, production-ready | **Throws `NotImplementedException`** — not yet implemented |

### Performance implications

- **Off (default):** `WaitHandleDbConnectionPool` uses OS-level wait handles. `OpenAsync` must
  acquire a semaphore slot, which can block a thread pool thread when the pool is at capacity.
  Under high concurrency this serialises connection acquisition.
- **On:** When eventually implemented, the Channels-based pool is expected to be fully async
  (no thread blocking while waiting for a pool slot). This should eliminate the pool-level thread
  starvation seen under high `OpenAsync` concurrency.

### Gotchas

- **Setting this to `true` will crash your application** with `NotImplementedException`. It is a
  placeholder for future work. Do not enable in production.
- The v1 pool's `WaitHandle`-based design is a known bottleneck for async-heavy workloads (see
  `platform-differences.md` connection pool section). The v2 pool is expected to address this but
  is not available yet.

---

## 5. `UseManagedNetworkingOnWindows`

| | |
| --- | --- |
| **Full name** | `Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows` |
| **Property** | `LocalAppContextSwitches.UseManagedNetworking` |
| **Default** | `false` on Windows, always `true` on Unix, always `false` on .NET Framework |

### What it does

On Windows with .NET 8+, selects between the native C++ SNI library and the managed C# SNI
implementation. The choice fundamentally changes the async I/O model.

```csharp
// TdsParserStateObjectFactory.windows.cs
public TdsParserStateObject CreateTdsParserStateObject(TdsParser parser)
{
    if (LocalAppContextSwitches.UseManagedNetworking)
        return new TdsParserStateObjectManaged(parser);   // managed SNI
    else
        return new TdsParserStateObjectNative(parser);    // native SNI (IOCP)
}
```

### Behaviour difference

| Native SNI (`false`, Windows default) | Managed SNI (`true`, or Unix) |
| --- | --- |
| IOCP-based async I/O: kernel dispatches completions to I/O thread pool | `NetworkStream.ReadAsync` / `SslStream.ReadAsync`: .NET async socket layer |
| `SyncOverAsync` uses `WaitForSingleObject` on native event | `SyncOverAsync` uses `socket.ReceiveTimeout` + blocking `ReadFromStream` |
| MARS via native `DynamicQueue` + IOCP callbacks | MARS via `SniMarsConnection` with `lock` + `ManualResetEventSlim` |
| Separate IOCP thread pool for completions | All completions on .NET thread pool |

### Performance implications

- **Native (default on Windows):** Better throughput under heavy async load because IOCP
  completions do not compete with application work items on the .NET thread pool. Lower latency
  for connection establishment.
- **Managed (on Windows):** Shares the .NET thread pool for I/O completions, which can increase
  thread pool pressure. May be advantageous for TLS 1.3 scenarios or when a fully managed stack
  is required (e.g. trimming/AOT).

### Gotchas

- On **.NET Framework**, this switch is ignored—native SNI is always used.
- On **Unix**, this switch is ignored—managed SNI is always used (there is no native SNI for
  Unix).
- The property implements custom caching logic (not the standard `AcquireAndReturn` helper) and
  also checks `OperatingSystem.IsWindows()` at runtime. Setting the switch on a non-Windows OS
  has no effect.
- When `ILLink.Substitutions.xml` is used for trimming, the switch value may be baked in at
  compile time and cannot be changed at runtime, even via `AppContext.SetSwitch()`.
- Switching from native to managed SNI changes the entire I/O completion model. Performance
  characteristics documented in `platform-differences.md` for "Windows (Native SNI)" will no
  longer apply—managed SNI on Windows behaves like the Unix path.

---

## 6. `UseMinimumLoginTimeout`

| | |
| --- | --- |
| **Full name** | `Switch.Microsoft.Data.SqlClient.UseOneSecFloorInTimeoutCalculationDuringLogin` |
| **Property** | `LocalAppContextSwitches.UseMinimumLoginTimeout` |
| **Default** | `true` |

### What it does

When enabled, applies a 1-second floor to timeout calculations during the login sequence. Without
this floor, a timeout value of 0 can be computed for individual login phases, which can cause an
indefinite wait in synchronous paths or an immediate timeout in async paths.

### Behaviour difference

| `true` (default) | `false` |
| --- | --- |
| Each login phase gets at least 1 second before timing out | Login phase timeout can be 0, meaning "wait forever" (sync) or "immediate timeout" (async) |
| Predictable login behaviour under tight overall timeout budgets | Legacy behaviour; may cause intermittent login failures or hangs |

### Performance implications

Minimal. This only affects the login sequence, not steady-state data operations. The 1-second floor
prevents degenerate cases where a near-zero remaining timeout causes the login to fail immediately
on a subsequent phase (e.g. SSPI negotiation after TCP connect consumes most of the budget).

### Gotchas

- Disabling this switch can cause **non-deterministic login failures** when `ConnectTimeout` is
  small and the server is slow to respond. The async login path may time out immediately on the
  second phase if the first phase consumed the entire timeout budget.
- The name of the switch (`UseOneSecFloorInTimeoutCalculationDuringLogin`) does not match the
  property name (`UseMinimumLoginTimeout`), which can cause confusion when searching the codebase.

---

## Switch Interaction Map

The async-related switches have important dependencies:

```
UseCompatibilityProcessSni ──────┐
   (default: true)               │
                                  ▼
                    ┌─────────────────────────────┐
                    │ If true (default):           │
                    │   UseCompatibilityAsync-     │
                    │   Behaviour is FORCED true,  │
                    │   regardless of its setting  │
                    └─────────────────────────────┘
                                  │
                                  ▼
UseCompatibilityAsyncBehaviour ───┐
   (default: true)                │
                                  ▼
                    ┌─────────────────────────────┐
                    │ If true (default or forced): │
                    │   Snapshot.ContinueEnabled   │
                    │   returns false              │
                    │   → full-restart replay      │
                    └─────────────────────────────┘

MakeReadAsyncBlocking ────────────┐
   (default: false)               │
                                  ▼
                    ┌─────────────────────────────┐
                    │ Independent of the above.    │
                    │ Only affects TryProcessDone. │
                    │ Sets _syncOverAsync = true   │
                    │ for DONE token reads.        │
                    └─────────────────────────────┘

UseManagedNetworkingOnWindows ────┐
   (default: false)               │
                                  ▼
                    ┌─────────────────────────────┐
                    │ Independent. Changes the     │
                    │ entire I/O layer underneath  │
                    │ (IOCP vs .NET async sockets) │
                    └─────────────────────────────┘
```

### To enable all new async optimisations

Both multiplexer switches must be disabled together:

```csharp
AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseCompatibilityProcessSni", false);
AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseCompatibilityAsyncBehaviour", false);
```

Setting only `UseCompatibilityAsyncBehaviour=false` without also setting
`UseCompatibilityProcessSni=false` is silently ignored—the getter returns `true` anyway.

---

## Key Takeaways

1. **The defaults are conservative.** All async optimisations are behind compat switches that
   default to the legacy behaviour. This is intentional—the new code paths are experimental.

2. **`UseCompatibilityProcessSni` is the master gate.** It controls whether the packet multiplexer
   is active, and it forcibly overrides `UseCompatibilityAsyncBehaviour`. Always set it first.

3. **`MakeReadAsyncBlocking` is a targeted workaround**, not a general async control. It only
   affects `TryProcessDone` and was added to address specific regressions in DONE token
   processing.

4. **`UseConnectionPoolV2` is not usable yet.** Enabling it throws `NotImplementedException`.

5. **`UseManagedNetworkingOnWindows` changes everything.** It swaps the I/O completion model, so
   all async performance characteristics change. Enable it only if you have a specific reason
   (TLS 1.3, trimming, debugging native SNI issues).

6. **Switch values are cached.** Once read, a switch value is stored in a static field and never
   re-read. Changing a switch after the first access has no effect. Set switches **before** opening
   any `SqlConnection`.

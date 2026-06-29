# Async Performance: Windows vs Unix Platform Differences

A detailed code-level analysis of why async operations in Microsoft.Data.SqlClient perform
differently on Windows (native SNI) and Unix/Linux (managed SNI), drawn from examination of the C#
driver source in `src/Microsoft.Data.SqlClient/src/` and the native C++ SNI source in the
`Microsoft.Data.SqlClient.SNI` repository.

---

## Executive Summary

Async performance characteristics differ between Windows and Unix because each platform uses a
fundamentally different I/O completion model at the SNI layer, while sharing the same upper-layer
code (connection pool, TDS parser, snapshot/replay). The result is that each platform has a
**different set of pain points**, though both platforms suffer from the shared-code issues
(connection pool serialization, TDS snapshot/replay).

| Aspect | Windows (Native SNI) | Unix (Managed SNI) |
| -------- | --------------------- | -------------------- |
| I/O completion model | IOCP (kernel-assisted) | .NET async socket / `Stream.ReadAsync` |
| Connection opening | `SNIOpenSyncEx` — always synchronous | `TryConnectParallel` — `Socket.Select()` blocks |
| Async reads | Native IOCP callbacks, zero-copy | `ReadFromStreamAsync` on `NetworkStream` |
| SyncOverAsync reads | Native: issues async I/O + `WaitForSingleObject` | Managed: `socket.ReceiveTimeout` + blocking `ReadFromStream` |
| MARS multiplexing | SMUX via native IOCP + `DynamicQueue` | `SniMarsConnection` via `lock`, `ManualResetEventSlim` |
| Packet-level locking | `CriticalSection` (native OS primitive) | `ConcurrentQueueSemaphore` (manages semaphore + TCS queue) |
| Thread pool pressure | Moderate — IOCP threads are separate from .NET thread pool | High — all async work runs on .NET thread pool |

---

## 1. I/O Completion Model

### Windows: I/O Completion Ports (IOCP)

The native SNI creates a global IOCP during initialization (`sni.cpp`):

```cpp
// sni.cpp, SNIInitialize()
ghIoCompletionPort = CreateIoCompletionPort(INVALID_HANDLE_VALUE, NULL, 0, 0);
```

Every TCP socket is associated with this IOCP via `SNIRegisterWithIOCP()`:

```cpp
// sni.cpp
inline DWORD SNIRegisterWithIOCP(HANDLE hNwk)
{
    if (NULL == CreateIoCompletionPort(hNwk, ghIoCompletionPort, 0, 0))
    { ... }
}
```

Dedicated `SNIAsyncWait` threads process completions:

```cpp
dwError = SNICreateWaitThread(SNIAsyncWait, NULL);
```

**Consequence:** Async reads and writes on Windows use kernel-level IOCP. The OS kernel
directly posts completion packets when I/O finishes. These are processed by dedicated IOCP threads
that are separate from the .NET managed thread pool. This means native SNI async I/O does **not**
consume .NET thread pool threads while waiting.

### Unix: .NET Managed Async I/O

The managed SNI uses `NetworkStream.ReadAsync()` and `NetworkStream.WriteAsync()` from .NET's socket
layer (`SniTcpHandle.netcore.cs`):

```csharp
// SniTcpHandle.netcore.cs – ReceiveAsync()
public override uint ReceiveAsync(ref SniPacket packet)
{
    packet = RentPacket(headerSize: 0, dataSize: _bufferSize);
    packet.SetAsyncIOCompletionCallback(_receiveCallback);
    packet.ReadFromStreamAsync(_stream);  // NetworkStream.ReadAsync
    return TdsEnums.SNI_SUCCESS_IO_PENDING;
}
```

**Consequence:** All async completions run on the .NET managed thread pool. Under high
concurrency, there is contention between SQL I/O callbacks, user async continuations, token
acquisition callbacks, and all other async work in the process. This is the fundamental reason Unix
is more susceptible to thread pool starvation under async load.

### Key Difference

| | Windows (IOCP) | Unix (Thread Pool) |
| -- | --- | --- |
| Completion dispatch | Dedicated IOCP threads (separate pool) | .NET thread pool threads (shared) |
| Under load | IOCP threads are sized independently | Thread pool threads are contended by all async work |
| Starvation risk | Lower — IOCP has its own thread management | Higher — everything shares the same pool |

---

## 2. Connection Opening: `SNIOpenSyncEx` vs `TryConnectParallel`

### Windows: Fully Synchronous, IOCP-Backed Wait

Connection opening on Windows goes through `SNIOpenSyncEx` → `SNIOpenSync` → `Tcp::SocketOpenSync`
(`open.cpp`, `tcp.cpp`).

`SocketOpenSync` uses `ConnectEx` (an async Windows API) but then **waits on an event:**

```cpp
// tcp.cpp – Tcp::SocketOpenSync()
DWORD Tcp::SocketOpenSync(__in ADDRINFOW* AIW, int timeout)
{
    // Uses ConnectEx for non-blocking connect initiation,
    // then WaitForSingleObject on the overlapped event.
    // Result: the calling thread blocks until TCP connect completes.
}
```

The `SNIOpenSyncExWrapper` in `sni_wrapper.cpp` wraps this with callback infrastructure:

```cpp
// sni_wrapper.cpp
DWORD __cdecl SNIOpenSyncExWrapper(
    __inout SNI_CLIENT_CONSUMER_INFO* pClientConsumerInfo,
    __deref_inout SNI_ConnWrapper** ppConn)
{
    pConsumerInfo->fnReadComp = UnmanagedReadCallback;
    pConsumerInfo->fnWriteComp = UnmanagedWriteCallback;
    dwError = SNIOpenSyncEx(pClientConsumerInfo, &pConn);
    // ...
}
```

**There is no `SNIOpenAsyncEx`**. The P/Invoke layer called from C# confirms this —
`SniNativeMethods.netcore.cs` exposes only `SNIOpenSyncExWrapper`.

### Unix: `Socket.Select()` Blocking Poll

On Unix, `SniTcpHandle.netcore.cs` opens connections via `TryConnectParallel`:

```csharp
// SniTcpHandle.netcore.cs – TryConnectParallel()
private Socket TryConnectParallel(string hostName, int port, TimeoutTimer timeout, ...)
{
    foreach (IPAddress address in serverAddresses)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            Blocking = false  // Non-blocking socket
        };
        socket.Connect(address, port);  // Returns immediately (WouldBlock)
    }

    // BLOCKING: Socket.Select() polls until a socket connects
    Socket.Select(checkReadLst, checkWriteLst, checkErrorLst, socketSelectTimeout);

    while (connectedSocket == null && !timeout.IsExpired)
    {
        Socket.Select(checkReadLst, checkWriteLst, checkErrorLst, socketSelectTimeout);
    }
}
```

**Key difference:** The managed code attempts parallel connections to multiple addresses
(IPv4/IPv6) using non-blocking sockets, but the `Socket.Select()` call itself is a
**synchronous blocking poll** that ties up the calling thread.

### Impact on `OpenAsync()`

Both platforms ultimately block the calling thread during connection establishment. The C# code
wraps the open in a background thread when called from `OpenAsync()`:

```text
SqlConnection.OpenAsync()
  → WaitHandleDbConnectionPool queues PendingGetConnection
    → Dedicated background thread calls TryGetConnection()
      → Creates physical connection via SNI (blocks)
```

The background thread (`WaitForPendingOpen`, `WaitHandleDbConnectionPool.cs`) is a dedicated
`Thread` that processes async open requests serially. This means `OpenAsync()` is **always slower**
than `Open()` because it adds queuing and cross-thread marshaling on top of the same blocking work.

**Platform difference:** On Windows, the blocking occurs in native code below the CLR
(doesn't count toward thread pool hill-climbing). On Unix, the blocking occurs in managed code
inside `Socket.Select()`, which can be counted by the CLR's thread pool as a "working" thread,
potentially delaying hill-climbing injection of new threads.

---

## 3. SyncOverAsync Read: The Critical Hot Path

The `ReadSniSyncOverAsync` pattern is used whenever the TDS parser needs to read data synchronously
but the connection was originally opened in async mode. This is the most performance-critical
difference between platforms.

### Windows: Issue Async I/O + Wait on Semaphore

```cpp
// sni_wrapper.cpp – SNIReadSyncOverAsync()
DWORD __cdecl SNIReadSyncOverAsync(
    __inout SNI_ConnWrapper* pConn,
    __out SNI_Packet** ppNewPacket,
    int timeout)
{
    if (pConn->m_fSupportsSyncOverAsync)
    {
        // Fast path: direct synchronous read
        return SNIReadSync(pConn->m_pConn, ppNewPacket, timeout);
    }

    ::EnterCriticalSection(&pConn->m_ReadLock);

    if (!pConn->m_fPendingRead)
    {
        pConn->m_fSyncOverAsyncRead = true;
        pConn->m_fPendingRead = true;
        dwError = SNIReadAsync(pConn->m_pConn, ppNewPacket, NULL);
    }
    else
    {
        dwError = ERROR_IO_PENDING;
    }

    if (ERROR_IO_PENDING == dwError)
    {
        // Block on Win32 semaphore until IOCP completion callback fires
        dwError = ::WaitForSingleObject(pConn->m_ReadResponseReady, timeout);
        // ...
    }

    ::LeaveCriticalSection(&pConn->m_ReadLock);
}
```

The completion callback (`UnmanagedReadCallback`) signals the semaphore:

```cpp
void __stdcall UnmanagedReadCallback(LPVOID ConsKey, SNI_Packet* pPacket, DWORD dwError)
{
    SNI_ConnWrapper* pConn = (SNI_ConnWrapper*)ConsKey;

    if (!pConn->m_fSyncOverAsyncRead)
    {
        // True async: invoke managed callback directly
        pConn->m_fnReadComp(pConn->m_ConsumerKey, pPacket, dwError);
    }
    else
    {
        // SyncOverAsync: store result and signal
        pConn->m_pPacket = pPacket;
        ::ReleaseSemaphore(pConn->m_ReadResponseReady, 1, NULL);
    }
}
```

**Key properties of the Windows path:**

1. I/O is truly posted to the kernel via IOCP (zero-copy, kernel delivers data directly to the
   buffer)

2. The wait is on a Win32 semaphore — an OS primitive with microsecond-level signaling
3. The callback runs on an IOCP thread, NOT a .NET thread pool thread
4. When `m_fSupportsSyncOverAsync` is true, it bypasses the async machinery entirely and does a
   direct synchronous socket read

### Unix: Blocking Socket Read with Timeout

```csharp
// TdsParserStateObjectManaged.netcore.cs – ReadSyncOverAsync()
internal override PacketHandle ReadSyncOverAsync(int timeoutRemaining, out uint error)
{
    SniHandle sessionHandle = GetSessionSNIHandleHandleOrThrow();
    error = sessionHandle.Receive(out SniPacket packet, timeoutRemaining);
    return PacketHandle.FromManagedPacket(packet);
}
```

Which calls:

```csharp
// SniTcpHandle.netcore.cs – Receive()
public override uint Receive(out SniPacket packet, int timeoutInMilliseconds)
{
    lock (this)  // Blocks ALL other operations on this handle
    {
        if (timeoutInMilliseconds > 0)
        {
            _socket.ReceiveTimeout = timeoutInMilliseconds;
        }

        packet = RentPacket(headerSize: 0, dataSize: _bufferSize);
        packet.ReadFromStream(_stream);  // BLOCKING socket read

        return TdsEnums.SNI_SUCCESS;
    }
}
```

**Key properties of the Unix path:**

1. Uses a `lock (this)` that blocks ALL other operations on the handle
2. The socket read is a blocking `Stream.Read()` call on a `NetworkStream`
3. Sets `ReceiveTimeout` on the socket before each read (a system call on Unix)
4. No separation between I/O threads and worker threads — the blocking read consumes a .NET thread
   pool thread

### Performance Impact

| Metric | Windows (IOCP) | Unix (Managed) |
| -------- | ---------------- | ---------------- |
| Thread blocked during read | Yes (Win32 wait) | Yes (socket Read) |
| Thread type blocked | Any .NET thread | .NET thread pool thread |
| Callback thread | IOCP thread (separate pool) | .NET thread pool thread (contended) |
| Lock scope | `CriticalSection` (read lock only) | `lock(this)` (entire handle) |
| Fast-path when sync is direct | Yes (`m_fSupportsSyncOverAsync`) | No equivalent fast-path |

The practical difference: on Windows, the IOCP completion callback runs on a dedicated IOCP thread
and signals a semaphore that unblocks the waiting thread with minimal latency. On Unix, both the
blocking read and any async completion callbacks compete for the same .NET thread pool, creating a
feedback loop under load.

---

## 4. MARS (Multiple Active Result Sets) Implementation

MARS multiplexes multiple logical TDS sessions over a single physical TCP connection using the SMUX
protocol (16-byte header per packet: SMID, Flags, SessionId, Length, SequenceNumber, HighWater).

### Windows: Native SMUX via IOCP

The native `Session` class in `smux.cpp` uses:

- **`SNICritSec* m_CS`** — A critical section per session for state protection
- **`DynamicQueue m_ReadPacketQueue`** — Lock-free queue for read packets
- **`DynamicQueue m_WritePacketQueue`** — Lock-free queue for write packets
- **`DynamicQueue m_ReceivedPacketQueue`** — Queue for demuxed received packets
- **`HANDLE m_lpReadHandles[2]`** and **`HANDLE m_lpWriteHandles[2]`** — Win32 event handles for
  sync read/write signaling

Flow control:

```cpp
// smux.cpp – Session fields
DWORD m_SequenceNumberForSend;
DWORD m_HighWaterForSend;        // Max sequence number we can send
DWORD m_SequenceNumberForReceive;
DWORD m_HighWaterForReceive;     // Max sequence number peer can send
DWORD m_LastHighWaterForReceive; // Last ACK sent
```

The SMUX demultiplexer is integrated with the IOCP: when a packet arrives on the physical
connection, the IOCP callback dispatches it to the correct `Session` based on `SessionId`, and that
session's `ReadDone` callback enqueues it to the `m_ReceivedPacketQueue`. If a sync reader is
waiting, it's woken via `SetEvent`.

**Key advantage:** IOCP threads handle the demultiplexing and queueing without touching
the .NET thread pool. The `DynamicQueue` is a native lock-free concurrent queue.

### Unix: Managed SMUX via Locks and Events

The managed implementation uses `SniMarsConnection` as the demultiplexer and `SniMarsHandle` for
per-session state.

**Demultiplexer lock** (`SniMarsConnection.netcore.cs`):

```csharp
private readonly object _sync;  // Global demuxer lock

public void Send(SniPacket packet)
{
    lock (DemuxerSync)  // Serializes ALL sessions' sends
    {
        _lowerHandle.Send(packet);
    }
}

public Task SendAsync(SniPacket packet)
{
    lock (DemuxerSync)  // Also serialized for async!
    {
        return _lowerHandle.SendAsync(packet);
    }
}
```

**Session state** (`SniMarsHandle.netcore.cs`):

```csharp
private readonly Queue<SniPacket> _receivedPacketQueue;
private readonly Queue<SniPacket> _sendPacketQueue;
private readonly ManualResetEventSlim _packetEvent;  // Signals data arrival
private readonly ManualResetEventSlim _ackEvent;     // Signals flow control window

private const uint ACK_THRESHOLD = 2;
```

**Stream-level locking via ConcurrentQueueSemaphore:**

```csharp
// ConcurrentQueueSemaphore.netcore.cs
internal sealed partial class ConcurrentQueueSemaphore
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentQueue<TaskCompletionSource<bool>> _queue;

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        if (_semaphore.Wait(0, cancellationToken))
            return Task.CompletedTask;  // Fast path: immediate acquire

        // Slow path: enqueue TCS, await released via SemaphoreSlim
        var tcs = new TaskCompletionSource<bool>();
        _queue.Enqueue(tcs);
        _semaphore.WaitAsync(cancellationToken).ContinueWith(/* dequeue + complete TCS */);
        return tcs.Task;
    }
}
```

This is used in `SniNetworkStream` and `SniSslStream`:

```csharp
// SniNetworkStream – one semaphore per direction
_writeAsyncSemaphore = new ConcurrentQueueSemaphore(1);
_readAsyncSemaphore = new ConcurrentQueueSemaphore(1);
```

### MARS Performance Comparison

| Aspect | Windows (Native SMUX) | Unix (Managed SMUX) |
| -------- | ---------------------- | --------------------- |
| Demultiplexing | IOCP callback → session queue | `lock (_sync)` for all sessions |
| Send serialization | Per-session critical section | Global `lock (DemuxerSync)` blocks ALL sessions |
| Packet queues | `DynamicQueue` (native lock-free) | `Queue<SniPacket>` (requires lock) |
| Read signaling | Win32 event handles | `ManualResetEventSlim` (spin-wait then kernel) |
| Async locking | N/A (IOCP handles ordering) | `ConcurrentQueueSemaphore` (TCS + `SemaphoreSlim`) |
| Flow control | Integrated in native code | Same protocol, more lock contention |

The global `lock (DemuxerSync)` in the managed implementation is the biggest differentiator. All
MARS sessions on a connection must acquire this lock to send ANY packet, creating a serialization
bottleneck that doesn't exist in the native implementation where per-session critical sections allow
parallel session operations.

Combined with the .NET thread pool contention issue, this explains why MARS on Linux (issue #422,
107 comments) is dramatically slower: thread pool threads blocked on the global demuxer lock can't
process async completions from other sessions, creating a deadlock-like starvation pattern that
resolves only when the thread pool injects enough new threads (hence the workaround:
`ThreadPool.SetMinThreads` to pre-allocate threads).

---

## 5. Connection Pool: Same Code, Different Starvation Profiles

The `WaitHandleDbConnectionPool` is shared across all platforms. Its key synchronization primitives:

```csharp
// WaitHandleDbConnectionPool.cs – PoolWaitHandles
private sealed class PoolWaitHandles
{
    private readonly Semaphore _poolSemaphore;       // Free connection count
    private readonly ManualResetEvent _errorEvent;   // Error signal
    private readonly Semaphore _creationSemaphore;   // Serializes creation (count=1)

    private readonly WaitHandle[] _handlesWithCreate;
}
```

The get-connection path uses `WaitHandle.WaitAny()`:

```csharp
int waitResult = WaitHandle.WaitAny(
    _waitHandles.GetHandles(allowCreate),
    unchecked((int)waitForMultipleObjectsTimeout));
```

For `OpenAsync()`, requests are enqueued and processed by a dedicated **background thread**:

```csharp
// WaitHandleDbConnectionPool.cs – async open
_pendingOpens.Enqueue(pendingGetConnection);
if (_pendingOpensWaiting == 0)
{
    Thread waitOpenThread = new Thread(WaitForPendingOpen);
    waitOpenThread.IsBackground = true;
    waitOpenThread.Start();
}
```

This dedicated thread processes pending opens **one at a time** using the same
`WaitHandle.WaitAny()` loop. This is the root cause of serialized connection creation.

### Platform Effect

The pool code is identical on both platforms, but the **downstream impact** differs:

- **Windows:** The `WaitHandle.WaitAny()` blocks the background thread. IOCP read/write callbacks
  still run on IOCP threads, so existing connections can process data while new connections are
  being created. The main degradation is cold-start performance (new connections are created
  serially).

- **Unix:** The `WaitHandle.WaitAny()` blocks a managed thread. All completion callbacks (including
  those for existing connections) run on the .NET thread pool. If the thread pool is saturated by
  blocked `OpenAsync()` callers, the existing connections' async callbacks can't run, creating
  cascading starvation.

### Thread Pool Starvation Cascade (Primarily Unix)

```text
High concurrency OpenAsync() calls
  → Pool's background thread creates connections serially
  → Async callers block .NET thread pool threads via WaitHandle.WaitAny
  → .NET thread pool threads are consumed
  → Managed SNI async callbacks (ReceiveAsync completion) can't be scheduled
  → Existing connections time out (no progress on reads)
  → Token acquisition callbacks can't run (Entra ID/AAD)
  → 20-minute cascading failure (dotnet/SqlClient#2152)
```

On Windows, the IOCP separation partially mitigates steps 4-5 because native SNI callbacks run on
IOCP threads, not the .NET thread pool. The starvation still happens but takes longer to cascade and
affects fewer subsystems.

---

## 6. TDS Async Snapshot/Replay: Same on Both Platforms

The snapshot/replay mechanism in `TdsParserStateObject.cs` is **platform-independent** — it runs in
the shared C# code above the SNI layer. Both Windows and Unix are equally affected by the O(n²)
replay problem for large async reads.

```csharp
// TdsParserStateObject.cs
private SnapshotStatus _snapshotStatus;
private StateSnapshot _snapshot;

// StateSnapshot uses a linked list of PacketData
internal sealed partial class StateSnapshot
{
    private sealed partial class PacketData
    {
        public readonly byte[] Buffer;
        public readonly int Read;
        public PacketData NextPacket;    // Linked list: snap → pkt1 → pkt2 → ...
        public int RunningDataSize;
    }
}
```

On each async yield:

1. `PrepareReplaySnapshot()` is called
2. The snapshot tries to `MoveToContinue()` (skip to last known position)
3. If no continue point, `MoveToStart()` replays from the first packet
4. All previously received packets are re-parsed

The "continue point" optimization (introduced in more recent PRs) can reduce the replay cost, but
it's limited in scope and doesn't eliminate the fundamental problem for large multi-packet values.

### Platform Interaction

While the snapshot/replay code is the same, its impact differs:

- **Windows:** Replay parsing consumes CPU but runs on whichever thread called `ReadAsync`. The IOCP
  callback that delivers new packets runs on an IOCP thread and is unaffected by the replay cost.

- **Unix:** Replay parsing consumes a .NET thread pool thread. The callback that delivers the next
  packet also needs a .NET thread pool thread. If replay parsing is CPU-intensive (large values), it
  delays thread pool thread availability for callbacks, amplifying the latency.

---

## 7. Summary: Why Unix Async Is Worse

The fundamental architectural difference is **I/O completion thread separation:**

1. **Windows (native SNI)** uses IOCP threads that are managed by the OS kernel, separate from the
   .NET thread pool. This provides natural isolation between "waiting for I/O" and "processing async
   continuations."

2. **Unix (managed SNI)** uses the .NET thread pool for everything — socket I/O callbacks, async
   continuations, timer callbacks, and user code all compete for the same thread pool.

This means every issue documented in the plans is **amplified on Unix:**

| Issue | Windows Impact | Unix Impact |
| ------- | --------------- | ------------- |
| Pool serialization (#601) | Slow cold-start | Slow cold-start + thread starvation |
| SyncOverAsync reads (#979) | Blocks thread, low latency via IOCP | Blocks thread pool thread, contends with callbacks |
| Token acquisition (#2152) | Thread pool pressure | Cascading starvation (20 min) |
| MARS performance (#422) | Per-session native critical sections | Global managed lock, thread pool contention |
| Large async reads (#593) | CPU-bound replay, callbacks unaffected | CPU-bound replay delays callbacks |
| Pre-login errors (#3118) | Rare (IOCP isolation) | Common under thread starvation |

### What Makes It Worse on Unix Specifically

1. **Global MARS lock** (`lock (DemuxerSync)`) serializes all sessions' sends — native SNI uses
   per-session `SNICritSec`

2. **Socket read holding** `lock (this)` in `SniTcpHandle.Receive()` — blocks async callbacks on the
   same handle

3. **`Socket.Select()` in connection opening** — blocking poll on .NET thread
4. **`ManualResetEventSlim`** spin-waits before escalating to kernel wait — CPU waste under
   contention

5. **No equivalent to** `m_fSupportsSyncOverAsync` fast-path — managed SNI always goes through the
   full Read() path

### What Makes It Worse on Windows Specifically

1. **No async connection open API** — `SNIOpenSyncEx` is the only option; even the native
   `ConnectEx` result is waited upon synchronously

2. **`WaitForSingleObject` / `CriticalSection`** — correct but still blocks threads
3. **Native SNI is an opaque binary** — harder to debug and profile than managed code

---

## 8. Code References

| Component | Windows Path | Unix Path |
| ----------- | ------------- | ----------- |
| SNI TCP handle | `Microsoft.Data.SqlClient.SNI/tcp.cpp` | `src/.../ManagedSni/SniTcpHandle.netcore.cs` |
| SNI wrapper | `Microsoft.Data.SqlClient.SNI/sni_wrapper.cpp` | N/A (direct C# calls) |
| MARS (SMUX) | `Microsoft.Data.SqlClient.SNI/smux.cpp` | `src/.../ManagedSni/SniMarsConnection.netcore.cs`, `SniMarsHandle.netcore.cs` |
| P/Invoke | `src/.../SniNativeMethods.netcore.cs` | N/A |
| TDS state object | `src/.../TdsParserStateObjectNative.windows.cs` | `src/.../TdsParserStateObjectManaged.netcore.cs` |
| State object factory | `...Factory.windows.cs` | `...Factory.unix.cs` |
| Connection pool | `src/.../ConnectionPool/WaitHandleDbConnectionPool.cs` | (same) |
| TDS parser | `src/.../TdsParserStateObject.cs` | (same) |
| Snapshot state | `src/.../TdsParserStateObject.cs` (StateSnapshot) | (same) |
| Stream locking | N/A | `src/.../ManagedSni/ConcurrentQueueSemaphore.netcore.cs` |

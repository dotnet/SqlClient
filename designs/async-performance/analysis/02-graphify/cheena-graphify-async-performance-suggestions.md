# High-Impact Async Performance Fixes for Microsoft.Data.SqlClient

**Date**: 2026-06-25  
**Source**: Full source code trace + knowledge graph analysis of the async read path  
**Related Issue**: [dotnet/SqlClient#593](https://github.com/dotnet/SqlClient/issues/593) (169 reactions — async reads 250x slower than sync)

---

## Problem Summary

The core async performance problem stems from the TDS parser's packet-reading internals. Each async read processes **one TDS packet at a time** (4-8KB) with heavy allocations per packet.

For a 1MB result set (~125-250 TDS packets), each async `ReadAsync()` call triggers 125-250 "async pends" — each one allocating:
1. `new TaskCompletionSource<object>()` (+ its internal `Task<object>`)
2. `ExecutionContext.Capture()` (stack walk + allocation)
3. `ContinueWith` continuation Task + `Unwrap` wrapper Task

**Total**: ~625-875 unnecessary heap allocations per `ReadAsync()` for a 1MB result.

### Critical Bottlenecks

| Issue | Impact | Location |
|-------|--------|----------|
| One packet per async pend | 100+ pends for large results | `TryReadNetworkPacket()` line 3408 |
| New TCS per packet | GC pressure, allocation churn | `ReadSni()` line 3495 |
| ContinueWith + Unwrap per read | 2 Task allocations | `ExecuteAsyncCall()` line 5665 |
| ExecutionContext.Run per callback | Context switch + stack walk | `ReadAsyncCallback()` line 3685 |
| No prefetching / read-ahead | Can't pipeline reads | Entire async path |
| No coalescing of small packets | 1 syscall per 4-8KB | Network layer |

---

## Async Read Path (Full Trace)

### High-Level Call Chain

```
SqlDataReader.ReadAsync()
  → TryReadInternal()
    → TryRun(RunBehavior.ReturnImmediately)
      → TryPrepareBuffer()
        → TryReadNetworkPacket()
          → ReadSni(new TaskCompletionSource<object>())
            → SniTcpHandle.ReceiveAsync()
              → packet.ReadFromStreamAsync(_stream)
                → stream.ReadAsync().ContinueWith(...)
```

### Detailed Allocation Path

```
SqlDataReader.ReadAsync()
  → PrepareAsyncInvocation() [captures ExecutionContext]          ← ALLOCATION #1
  → InvokeAsyncCall() → context.Execute()
    → TryReadInternal() → TdsParser.TryRun()
      → TryReadByteArray() / TryReadByte()
        → TryPrepareBuffer()
          → TryReadNetworkPacket()
            → ReadSni(new TaskCompletionSource<object>())        ← ALLOCATION #2
              → ReadAsync(handle) [SNI layer]
              → returns SNI_SUCCESS_IO_PENDING
            → returns NeedMoreData
  → ExecuteAsyncCall()
    → completionSource.Task.ContinueWith(...).Unwrap()           ← ALLOCATION #3, #4
    
[SNI callback fires]
  → ReadAsyncCallback()
    → ProcessSniPacket()
    → ExecutionContext.Run(_executionContext, callback, source)   ← OVERHEAD
    → source.TrySetResult(null)
    
[Continuation fires]
  → PrepareForAsyncContinuation() [re-captures ExecutionContext] ← ALLOCATION #5
  → PrepareReplaySnapshot() → replay from snapshot
  → TryReadInternal() again... (may pend again for next packet)
```

---

## Proposed Fixes (Ordered by Impact)

### Fix 1: Packet Read-Ahead + Coalesced Network Reads

**Impact**: 50-250x improvement for large result sets  
**Complexity**: Medium  
**Risk**: Medium — must handle attention signals and cancellation

#### Problem

Each TDS packet is read individually even when multiple packets are already in the TCP receive buffer. The driver does:
```
async pend → read 4KB → process → async pend → read 4KB → ...
```

#### Solution

Read up to 64KB at a time from the stream, then parse multiple TDS packets from the buffer synchronously. Pre-fetch the next chunk while processing the current one.

```csharp
// In TdsParserStateObject — add read-ahead infrastructure:
private byte[] _readAheadBuffer = new byte[65536]; // 64KB
private Task<int> _pendingReadAhead;

internal void StartReadAhead()
{
    if (_pendingReadAhead == null && !_attentionSent)
    {
        _pendingReadAhead = _stream.ReadAsync(
            _readAheadBuffer, 0, _readAheadBuffer.Length, CancellationToken.None);
    }
}

// In TryReadNetworkPacket, check read-ahead first:
if (_pendingReadAhead != null && _pendingReadAhead.IsCompleted)
{
    // Consume pre-fetched data synchronously — no async pend needed
    ConsumeReadAhead();
    return true;
}
```

**Expected behavior**: Most packets complete synchronously from the read-ahead buffer, eliminating the async overhead entirely. One `ReadAsync` serves 8-16 TDS packets, reducing syscalls proportionally.

**Target state**:
```
async pend → read 64KB → process 8 packets synchronously → async pend → ...
```

---

### Fix 2: Replace `TaskCompletionSource<object>` with Pooled `IValueTaskSource`

**Impact**: Eliminates 2-3 heap allocations per TDS packet read  
**Complexity**: Medium  
**Risk**: Medium — wide blast radius across 2 files

#### Root Cause Location

**File**: `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParserStateObject.cs`  
**Line 3495** (inside `TryReadNetworkPacket`, async path):

```csharp
// CURRENT: Allocates a NEW TaskCompletionSource on every single packet read
ReadSni(new TaskCompletionSource<object>());
```

#### Proposed Change

Introduce a reusable `ResettableCompletionSource` field on `TdsParserStateObject`:

```csharp
// NEW field (replace line 253's _networkPacketTaskSource):
private ResettableCompletionSource _networkPacketCompletion = new();

// Line 3495 becomes:
ReadSni(_networkPacketCompletion.Reset());
```

#### New Type

```csharp
internal sealed class ResettableCompletionSource : IValueTaskSource<object>
{
    private ManualResetValueTaskSourceCore<object> _core;

    public ValueTask<object> GetValueTask() => new ValueTask<object>(this, _core.Version);
    
    public ResettableCompletionSource Reset()
    {
        _core.Reset();
        return this;
    }

    public void SetResult(object result) => _core.SetResult(result);
    public void SetException(Exception error) => _core.SetException(error);

    object IValueTaskSource<object>.GetResult(short token) => _core.GetResult(token);
    ValueTaskSourceStatus IValueTaskSource<object>.GetStatus(short token) => _core.GetStatus(token);
    void IValueTaskSource<object>.OnCompleted(
        Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}
```

#### Files Affected

| File | Line(s) | Change |
|------|---------|--------|
| `TdsParserStateObject.cs` | 253 | Replace `TaskCompletionSource<object> _networkPacketTaskSource` field |
| `TdsParserStateObject.cs` | 3495 | `ReadSni(_networkPacketCompletion.Reset())` |
| `TdsParserStateObject.cs` | 3903-3907 | `ReadSni` signature: accept `ResettableCompletionSource` |
| `TdsParserStateObject.cs` | 3619 | `ReadAsyncCallback`: access `_networkPacketCompletion` instead of `_networkPacketTaskSource` |
| `TdsParserStateObject.cs` | 3685-3691 | Callback completion: call `SetResult`/`SetException` on new type |
| `SqlDataReader.cs` | 899, 1137 | Access `_networkPacketCompletion` |
| `SqlDataReader.cs` | 5657 | `ExecuteAsyncCall`: await `GetValueTask()` instead of `.Task.ContinueWith` |
| `SqlDataReader.cs` | 5877 | `CleanupAfterAsyncInvocationInternal`: reset instead of null |

---

### Fix 3: Replace `ContinueWith` + `Unwrap` with Direct `await`

**Impact**: Eliminates 2 Task allocations per async wait  
**Complexity**: Low-Medium  
**Risk**: Low — local to SqlDataReader

#### Root Cause Location

**File**: `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlDataReader.cs`  
**Line 5665** (`ExecuteAsyncCall`):

```csharp
// CURRENT:
private Task<T> ExecuteAsyncCall<T>(AAsyncBaseCallContext<SqlDataReader, T> context)
{
    TaskCompletionSource<object> completionSource = _stateObj._networkPacketTaskSource;
    if (_cancelAsyncOnCloseToken.IsCancellationRequested || completionSource == null)
    {
        return Task.FromException<T>(ADP.ExceptionWithStackTrace(ADP.ClosedConnectionError()));
    }
    else
    {
        return completionSource.Task.ContinueWith(
            continuationFunction: SqlDataReaderBaseAsyncCallContext<T>.s_executeCallback,
            state: context,
            TaskScheduler.Default
        ).Unwrap();
    }
}
```

#### Proposed Change

```csharp
// AFTER:
private async Task<T> ExecuteAsyncCall<T>(AAsyncBaseCallContext<SqlDataReader, T> context)
{
    var completion = _stateObj._networkPacketCompletion;
    if (_cancelAsyncOnCloseToken.IsCancellationRequested || completion == null)
    {
        throw ADP.ExceptionWithStackTrace(ADP.ClosedConnectionError());
    }

    await completion.GetValueTask().ConfigureAwait(false);
    return await ContinueAsyncCall<T>(
        Task.CompletedTask,
        (SqlDataReaderBaseAsyncCallContext<T>)context
    ).ConfigureAwait(false);
}
```

Similarly, in `InvokeAsyncCall` at **line 5627**:
```csharp
// CURRENT:
task.ContinueWith(
    continuationAction: SqlDataReaderBaseAsyncCallContext<T>.s_completeCallback,
    state: context,
    TaskScheduler.Default
);

// AFTER: Direct await + CompleteAsyncCall
```

#### Files Affected

| File | Line(s) | Change |
|------|---------|--------|
| `SqlDataReader.cs` | 5653-5673 | Rewrite `ExecuteAsyncCall` to use `await` |
| `SqlDataReader.cs` | 5627-5632 | Rewrite continuation in `InvokeAsyncCall` |

---

### Fix 4: Eliminate Redundant `ExecutionContext.Capture()` on Every Continuation

**Impact**: Removes ~1 allocation + expensive stack walk per async pend  
**Complexity**: Low  
**Risk**: Low — guarded by `#if NET`

#### Root Cause Locations

1. **`SqlDataReader.cs` line 5842** (`PrepareAsyncInvocation`):
   ```csharp
   _stateObj._executionContext = ExecutionContext.Capture();
   ```
   This is the **initial** capture — correct and necessary.

2. **`SqlDataReader.cs` line 5918** (`PrepareForAsyncContinuation`):
   ```csharp
   _stateObj._executionContext = ExecutionContext.Capture();
   ```
   This **re-captures** on every continuation — unnecessary on .NET 8+.

3. **`TdsParserStateObject.cs` lines 3685-3698** (`ReadAsyncCallback`):
   ```csharp
   if (_executionContext != null)
   {
       ExecutionContext.Run(_executionContext, s_readAsyncCallbackComplete, source);
   }
   else
   {
       source.TrySetResult(null);
   }
   ```
   Runs the completion under captured context — unnecessary on modern .NET.

#### Proposed Change

On .NET 8+ (`#if NET`), skip EC capture on continuations and run callbacks directly:

```csharp
// SqlDataReader.cs PrepareForAsyncContinuation (line 5918):
#if NETFRAMEWORK
    _stateObj._executionContext = ExecutionContext.Capture();
#endif
    // On .NET 8+, the await infrastructure handles EC flow at the user boundary
```

```csharp
// TdsParserStateObject.cs ReadAsyncCallback (lines 3685-3698):
#if NET
    source.TrySetResult(null);  // .NET handles EC flow at await
#else
    if (_executionContext != null)
    {
        ExecutionContext.Run(_executionContext, s_readAsyncCallbackComplete, source);
    }
    else
    {
        source.TrySetResult(null);
    }
#endif
```

**Rationale**: Modern .NET `await` infrastructure captures and restores `ExecutionContext` at the user's `await` boundary automatically. The driver doing it per-packet was a .NET Framework 4.5 design that's now redundant.

#### Files Affected

| File | Line(s) | Change |
|------|---------|--------|
| `TdsParserStateObject.cs` | 3685-3698 | Guard EC.Run with `#if NETFRAMEWORK` |
| `SqlDataReader.cs` | 5918 | Guard re-capture with `#if NETFRAMEWORK` |

---

## Implementation Priority Summary

| Priority | Fix | Perf Gain | Allocs Saved/Packet | Complexity | Risk |
|----------|-----|-----------|---------------------|------------|------|
| **1** | Read-ahead + coalesced reads | **50-250x** for large reads | N/A (eliminates pends) | Medium | Medium |
| **2** | IValueTaskSource replacing TCS | **Zero-alloc** hot path | 2-3 objects | Medium | Medium |
| **3** | Replace ContinueWith with await | 2 fewer Tasks per pend | 2 objects | Low-Medium | Low |
| **4** | Skip ExecutionContext on .NET 8+ | ~15-30% per packet | 1 object + stack walk | Low | Low |

**Combined effect**: For a 1MB result (~125 TDS packets), these fixes eliminate approximately **625-875 heap allocations** and reduce async pend count from ~125 to ~16.

---

## Recommended Approach

Start with **Fix 1** (read-ahead + coalesced reads) — it addresses the fundamental architectural problem of per-packet granularity. This alone would likely resolve the 250x async penalty from issue #593.

Then layer on **Fixes 2-4** to eliminate remaining per-pend allocations. These are complementary: once read-ahead reduces pend count to ~16, the per-pend allocation savings matter less but still improve throughput for smaller result sets and MARS scenarios.

---

## Key Source Locations Reference

| Component | File | Key Lines |
|-----------|------|-----------|
| Per-packet TCS allocation | `TdsParserStateObject.cs` | 3495 |
| `_networkPacketTaskSource` field | `TdsParserStateObject.cs` | 253 |
| `ReadSni` method | `TdsParserStateObject.cs` | 3903-4020 |
| `ReadAsyncCallback` (completion) | `TdsParserStateObject.cs` | 3606-3730 |
| `TryReadNetworkPacket` | `TdsParserStateObject.cs` | 3408-3505 |
| `TryPrepareBuffer` | `TdsParserStateObject.cs` | 1431-1540 |
| Snapshot replay | `TdsParserStateObject.cs` | 3510-3515 |
| EC capture (initial) | `SqlDataReader.cs` | 5842 |
| EC re-capture (per continuation) | `SqlDataReader.cs` | 5918 |
| `ExecuteAsyncCall` (ContinueWith) | `SqlDataReader.cs` | 5653-5673 |
| `InvokeAsyncCall` (ContinueWith) | `SqlDataReader.cs` | 5606-5645 |
| `PrepareForAsyncContinuation` | `SqlDataReader.cs` | 5900-5930 |

---

## Validation Strategy

1. **Benchmark**: Use BenchmarkDotNet with a 1MB `SELECT` result to measure allocations before/after
2. **Functional tests**: Run existing `ReadAsync` tests (FunctionalTests + ManualTests with `Category!=Windows`)
3. **Stress test**: Concurrent `ReadAsync` with MARS enabled to validate no races in the reusable completion source
4. **Regression**: Verify `AppContext` switch `MakeReadAsyncBlocking` still works correctly

---

## Related Issues

- [#593](https://github.com/dotnet/SqlClient/issues/593) — Async large data reads 250x slower (169 👍)
- [#422](https://github.com/dotnet/SqlClient/issues/422) — MARS very slow on Linux (47 👍)
- [#601](https://github.com/dotnet/SqlClient/issues/601) — Parallel async connection opens blocking (15 👍)
- [#3356](https://github.com/dotnet/SqlClient/issues/3356) — Connection pool redesign (16 👍)
- [#979](https://github.com/dotnet/SqlClient/issues/979) — OpenAsync blocks on network I/O (8 👍)

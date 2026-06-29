# Fix 1: Async Managed SNI TCP Connect

**Priority:** High — highest-impact standalone change for managed SNI
**Complexity:** Medium
**Risk:** Medium

## Problem

`SniTcpHandle` is constructed synchronously. Its constructor calls `Connect()` or
`TryConnectParallel()` which block on `Socket.ConnectAsync()` completion. The async capability
exists at the socket level but is not exposed to callers.

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs`

- Constructor (lines ~135-180)
- `Connect()` method
- `TryConnectParallel()` method

## Changes Required

### 1. Add Static Factory Method

Replace the synchronous constructor with an async factory:

```csharp
internal static async ValueTask<SniTcpHandle> CreateAsync(
    string serverName,
    int port,
    long timerExpire,
    bool parallel,
    SqlConnectionIPAddressPreference ipPreference,
    SQLDNSInfo cachedDNSInfo,
    CancellationToken cancellationToken)
{
    var handle = new SniTcpHandle(); // private ctor — no I/O
    await handle.ConnectAsync(serverName, port, timerExpire, parallel,
        ipPreference, cachedDNSInfo, cancellationToken)
        .ConfigureAwait(false);
    return handle;
}
```

### 2. Create ConnectAsync Method

```csharp
private async ValueTask ConnectAsync(
    string serverName, int port, long timerExpire, bool parallel,
    SqlConnectionIPAddressPreference ipPreference,
    SQLDNSInfo cachedDNSInfo,
    CancellationToken cancellationToken)
{
    IPAddress[] addresses = await Dns.GetHostAddressesAsync(serverName,
        cancellationToken).ConfigureAwait(false);

    // Reuse existing parallel/sequential logic but with async connects
    if (parallel)
    {
        await TryConnectParallelAsync(addresses, port, timerExpire,
            cancellationToken).ConfigureAwait(false);
    }
    else
    {
        await ConnectSequentialAsync(addresses, port, timerExpire,
            cancellationToken).ConfigureAwait(false);
    }
}
```

### 3. Async Socket Connection

The existing `TryConnectParallel()` already uses `Socket.ConnectAsync()` internally but blocks on
the result. Change to truly await it:

```csharp
private async ValueTask TryConnectParallelAsync(
    IPAddress[] addresses, int port, long timerExpire,
    CancellationToken cancellationToken)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken);
    cts.CancelAfter(TimeoutFromExpiry(timerExpire));

    // Parallel connect to IPv4 and IPv6 addresses
    var socket = await Socket.ConnectAsync(
        /* ... existing parallel logic ... */,
        cts.Token).ConfigureAwait(false);

    _socket = socket;
    _stream = new NetworkStream(_socket, ownsSocket: true);
}
```

### 4. Update SniProxy Factory

`SniProxy` creates `SniTcpHandle` instances. Add an async path:

```csharp
internal static async ValueTask<SniPhysicalHandle> CreateConnectionHandleAsync(
    string serverName, int port, long timerExpire, bool parallel,
    SqlConnectionIPAddressPreference ipPreference,
    SQLDNSInfo cachedDNSInfo,
    CancellationToken cancellationToken)
{
    return await SniTcpHandle.CreateAsync(serverName, port, timerExpire,
        parallel, ipPreference, cachedDNSInfo, cancellationToken)
        .ConfigureAwait(false);
}
```

## Testing

- Unit test: `SniTcpHandle.CreateAsync()` connects successfully
- Thread test: `CreateAsync` doesn't block the calling thread pool thread (use a constrained thread
  pool with 1 thread)

- Timeout test: Cancellation token cancels a slow connection correctly
- Integration test: Full connection with async managed SNI

## Risk

- Medium — socket connection semantics must match exactly. DNS resolution caching, IPv4/IPv6
  preference, and timeout behavior must be preserved.

- Keep the sync constructor as a fallback for sync `Open()` callers.
